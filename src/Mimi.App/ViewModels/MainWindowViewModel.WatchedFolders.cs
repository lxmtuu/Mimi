using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mimi.Models;
using Mimi.Services;
using Serilog;

namespace Mimi.ViewModels;

/// <summary>
/// Watched-folder settings and background library synchronization. File
/// events are already debounced by <see cref="ILibraryFolderWatcher"/>; this
/// partial serializes them with manual scans and applies them on the UI
/// thread without deleting library records when a file disappears.
/// </summary>
public sealed partial class MainWindowViewModel
{
    private LibraryFolderChanges? pendingLibraryFolderChanges;
    private bool isProcessingLibraryFolderChanges;

    [ObservableProperty]
    private bool watchLibraryFolders = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveWatchedFolderCommand))]
    private string? selectedWatchedFolder;

    public ObservableCollection<string> WatchedFolders { get; } = [];

    partial void OnWatchLibraryFoldersChanged(bool value)
    {
        if (!settingsLoaded || isRestoringSettings) return;

        ConfigureLibraryFolderWatcher();
        SaveSettings();
    }

    private void WatchedFolders_CollectionChanged(
        object? sender,
        NotifyCollectionChangedEventArgs e)
    {
        if (!settingsLoaded || isRestoringSettings) return;

        ConfigureLibraryFolderWatcher();
        SaveSettings();
    }

    private void ConfigureLibraryFolderWatcher()
    {
        if (libraryFolderWatcher is null || disposed) return;

        try
        {
            libraryFolderWatcher.Configure(
                WatchedFolders,
                WatchLibraryFolders);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not configure watched library folders");
        }
    }

    [RelayCommand]
    private async Task AddWatchedFolder()
    {
        if (IsScanning || isProcessingLibraryFolderChanges) return;

        var pickedFolder = dialogService.PickMusicFolder();
        if (string.IsNullOrWhiteSpace(pickedFolder)) return;

        var folder = NormalizeWatchedFolder(pickedFolder);
        if (folder is null)
        {
            dialogService.ShowWarning(
                "That folder path could not be used.",
                "Settings");
            return;
        }

        var existing = WatchedFolders.FirstOrDefault(candidate =>
            string.Equals(candidate, folder, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            WatchedFolders.Add(folder);
            SelectedWatchedFolder = folder;
        }
        else
        {
            SelectedWatchedFolder = existing;
            folder = existing;
        }

        WatchLibraryFolders = true;
        SaveSettings(force: true);
        IsSettingsOpen = false;

        var result = await ScanAndAddFolderAsync(folder);
        if (result?.SkippedDirectories > 0)
        {
            dialogService.ShowInformation(
                $"{result.SkippedDirectories} folder(s) could not be read and were skipped.");
        }
    }

    [RelayCommand(CanExecute = nameof(CanRemoveWatchedFolder))]
    private void RemoveWatchedFolder()
    {
        if (SelectedWatchedFolder is null) return;

        var index = WatchedFolders.IndexOf(SelectedWatchedFolder);
        if (index < 0) return;

        WatchedFolders.RemoveAt(index);
        SelectedWatchedFolder = WatchedFolders.Count == 0
            ? null
            : WatchedFolders[Math.Min(index, WatchedFolders.Count - 1)];
        SaveSettings(force: true);
    }

    private bool CanRemoveWatchedFolder() =>
        !string.IsNullOrWhiteSpace(SelectedWatchedFolder);

    private static string? NormalizeWatchedFolder(string folder)
    {
        try
        {
            var fullPath = Path.GetFullPath(folder.Trim());
            var root = Path.GetPathRoot(fullPath);
            return string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase)
                ? fullPath
                : fullPath.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
        }
        catch (Exception ex) when (
            ex is ArgumentException or
            NotSupportedException or
            PathTooLongException)
        {
            return null;
        }
    }

    private void LibraryFolderWatcher_ChangesDetected(
        object? sender,
        LibraryFolderChanges changes)
    {
        if (disposed || !changes.HasChanges) return;

        if (uiDispatcher.CheckAccess())
        {
            QueueLibraryFolderChanges(changes);
        }
        else
        {
            uiDispatcher.BeginInvoke(() => QueueLibraryFolderChanges(changes));
        }
    }

    private void QueueLibraryFolderChanges(LibraryFolderChanges changes)
    {
        if (disposed) return;

        pendingLibraryFolderChanges =
            pendingLibraryFolderChanges?.Merge(changes) ?? changes;
        QueuePendingLibraryFolderChanges();
    }

    private void QueuePendingLibraryFolderChanges() =>
        _ = ProcessPendingLibraryFolderChangesAsync();

    private async Task ProcessPendingLibraryFolderChangesAsync()
    {
        if (disposed ||
            isProcessingLibraryFolderChanges ||
            pendingLibraryFolderChanges is null ||
            !persistedStateRestored ||
            !libraryInitialized ||
            IsScanning)
        {
            return;
        }

        var cancellation = BeginScan();
        if (cancellation is null) return;

        var changes = pendingLibraryFolderChanges;
        pendingLibraryFolderChanges = null;
        isProcessingLibraryFolderChanges = true;
        ScanStatusLabel = "Updating watched folders…";

        try
        {
            await MarkDeletedTracksAsync(changes.DeletedFilePaths);

            if (changes.ChangedFilePaths.Count > 0)
            {
                var tracks = await libraryService.ScanFilesAsync(
                    changes.ChangedFilePaths,
                    CreateScanProgress(),
                    KnownTracksByPath(),
                    cancellation.Token);
                await AddTracksAsync(tracks);
            }

            var directories = changes.DirectoriesToRescan.Count > 0
                ? changes.DirectoriesToRescan
                : changes.BufferOverflowed
                    ? WatchedFolders.ToArray()
                    : [];

            foreach (var directory in directories)
            {
                cancellation.Token.ThrowIfCancellationRequested();

                if (Directory.Exists(directory))
                {
                    var result = await libraryService.ScanFolderAsync(
                        directory,
                        recursive: true,
                        CreateScanProgress(),
                        KnownTracksByPath(),
                        cancellation.Token);
                    await AddTracksAsync(result.Tracks);
                }

                await MarkMissingTracksUnderDirectoryAsync(directory);
            }

            ScanStatusLabel = string.Empty;
            Log.Information(
                "Synchronized watched folders: {ChangedCount} changed, " +
                "{DeletedCount} deleted, {DirectoryCount} directories, " +
                "overflow={BufferOverflowed}",
                changes.ChangedFilePaths.Count,
                changes.DeletedFilePaths.Count,
                directories.Count,
                changes.BufferOverflowed);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            ScanStatusLabel = lifetimeCancellation.IsCancellationRequested
                ? string.Empty
                : "Watched-folder update canceled";
        }
        catch (Exception ex)
        {
            // Folder watching is background maintenance; log the failure
            // rather than interrupting playback with a modal dialog.
            Log.Warning(ex, "Could not synchronize watched library folders");
            ScanStatusLabel = string.Empty;
        }
        finally
        {
            isProcessingLibraryFolderChanges = false;
            EndScan(cancellation);
        }
    }

    private async Task MarkDeletedTracksAsync(IEnumerable<string> paths)
    {
        var tracksByPath = Tracks.ToDictionary(
            track => track.FilePath,
            StringComparer.OrdinalIgnoreCase);
        var updated = new List<Track>();

        foreach (var path in paths)
        {
            if (File.Exists(path)) continue;
            if (!tracksByPath.TryGetValue(path, out var item)) continue;
            if (MarkTrackMissing(item))
                updated.Add(item.Track);
        }

        if (updated.Count == 0) return;

        await PersistTracksAsync(updated);
        ApplyFilter();
    }

    private async Task MarkMissingTracksUnderDirectoryAsync(string directory)
    {
        var updated = new List<Track>();
        foreach (var item in Tracks)
        {
            if (!IsPathUnderDirectory(item.FilePath, directory) ||
                File.Exists(item.FilePath))
            {
                continue;
            }

            if (MarkTrackMissing(item))
                updated.Add(item.Track);
        }

        if (updated.Count == 0) return;

        await PersistTracksAsync(updated);
        ApplyFilter();
    }

    private static bool MarkTrackMissing(TrackItemViewModel item)
    {
        if (item.Track.IsMissing && item.HasPlaybackError)
            return false;

        item.Track.IsMissing = true;
        item.Track.HasError = true;
        item.HasPlaybackError = true;
        return true;
    }

    private static bool IsPathUnderDirectory(string filePath, string directory)
    {
        try
        {
            var fullFile = Path.GetFullPath(filePath);
            var fullDirectory = Path.GetFullPath(directory);
            if (string.Equals(
                    fullFile,
                    fullDirectory,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!fullDirectory.EndsWith(Path.DirectorySeparatorChar) &&
                !fullDirectory.EndsWith(Path.AltDirectorySeparatorChar))
            {
                fullDirectory += Path.DirectorySeparatorChar;
            }

            return fullFile.StartsWith(
                fullDirectory,
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (
            ex is ArgumentException or
            NotSupportedException or
            PathTooLongException)
        {
            return false;
        }
    }
}
