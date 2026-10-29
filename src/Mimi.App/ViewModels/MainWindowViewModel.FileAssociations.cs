using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Mimi.Services;
using Serilog;

namespace Mimi.ViewModels;

/// <summary>
/// Explorer integration and command-line/named-pipe file opening. Requests
/// are briefly coalesced because Windows may launch one short-lived process
/// per selected file; merging those launches lets Mimi add every song and
/// start the first playable one instead of rapidly switching tracks.
/// </summary>
public sealed partial class MainWindowViewModel
{
    private readonly SemaphoreSlim externalOpenGate = new(1, 1);
    private readonly List<string> pendingExternalPaths = [];
    private CancellationTokenSource? externalOpenDebounce;
    private TaskCompletionSource? externalOpenBatchCompletion;
    private bool isRefreshingOpenWithMimiRegistration;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OpenWithMimiStatusLabel))]
    private bool isOpenWithMimiRegistered;

    public string OpenWithMimiStatusLabel =>
        fileAssociationService is null
            ? "Explorer integration is unavailable in this build."
            : IsOpenWithMimiRegistered
                ? $"Registered for {SupportedExtensionsLabel}."
                : $"Not registered. Supported types: {SupportedExtensionsLabel}.";

    public string OpenWithMimiExecutablePath =>
        fileAssociationService?.ExecutablePath ?? string.Empty;

    private static string SupportedExtensionsLabel => string.Join(
        ", ",
        SupportedAudioFiles.Extensions.Select(extension =>
            extension.TrimStart('.').ToUpperInvariant()));

    partial void OnIsOpenWithMimiRegisteredChanged(bool value)
    {
        if (isRefreshingOpenWithMimiRegistration || !settingsLoaded) return;

        if (fileAssociationService is null)
        {
            dialogService.ShowWarning(
                "Windows integration is unavailable in this build.",
                "Settings");
            RefreshOpenWithMimiRegistration();
            return;
        }

        try
        {
            if (value)
                fileAssociationService.Register();
            else
                fileAssociationService.Unregister();
        }
        catch (Exception ex)
        {
            Log.Warning(
                ex,
                value
                    ? "Could not register Mimi with Windows"
                    : "Could not remove Mimi's Windows registration");
            dialogService.ShowWarning(
                value
                    ? $"Couldn't register Mimi with Windows.\n\n{ex.Message}"
                    : $"Couldn't remove Mimi's Windows registration.\n\n{ex.Message}",
                "Settings");
        }
        finally
        {
            RefreshOpenWithMimiRegistration();
        }
    }

    private void RefreshOpenWithMimiRegistration()
    {
        var registered = false;
        try
        {
            registered = fileAssociationService?.IsRegistered ?? false;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not read Mimi's Explorer registration");
        }

        isRefreshingOpenWithMimiRegistration = true;
        try
        {
            IsOpenWithMimiRegistered = registered;
        }
        finally
        {
            isRefreshingOpenWithMimiRegistration = false;
        }

        OnPropertyChanged(nameof(OpenWithMimiStatusLabel));
        OnPropertyChanged(nameof(OpenWithMimiExecutablePath));
    }

    public async Task OpenExternalPathsAsync(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (disposed) return;

        if (!uiDispatcher.CheckAccess())
        {
            await await uiDispatcher.InvokeAsync(
                async () => await OpenExternalPathsAsync(paths));
            return;
        }

        var incoming = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim().Trim('"'))
            .Where(path => path != "--" &&
                           !path.StartsWith("-", StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (incoming.Length == 0) return;

        foreach (var path in incoming)
        {
            if (!pendingExternalPaths.Contains(
                    path,
                    StringComparer.OrdinalIgnoreCase))
            {
                pendingExternalPaths.Add(path);
            }
        }

        var completion = externalOpenBatchCompletion ??=
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        externalOpenDebounce?.Cancel();
        externalOpenDebounce?.Dispose();
        var debounce = CancellationTokenSource.CreateLinkedTokenSource(
            lifetimeCancellation.Token);
        externalOpenDebounce = debounce;

        _ = ProcessExternalOpenBatchAsync(debounce, completion);
        await completion.Task;
    }

    private async Task ProcessExternalOpenBatchAsync(
        CancellationTokenSource debounce,
        TaskCompletionSource completion)
    {
        try
        {
            await Task.Delay(350, debounce.Token);
        }
        catch (OperationCanceledException)
        {
            if (lifetimeCancellation.IsCancellationRequested)
                completion.TrySetCanceled();
            return;
        }

        if (!ReferenceEquals(externalOpenDebounce, debounce)) return;

        externalOpenDebounce = null;
        debounce.Dispose();

        var batch = pendingExternalPaths.ToArray();
        pendingExternalPaths.Clear();
        if (ReferenceEquals(externalOpenBatchCompletion, completion))
            externalOpenBatchCompletion = null;

        try
        {
            await ProcessExternalPathsAsync(batch);
            completion.TrySetResult();
        }
        catch (OperationCanceledException) when (
            lifetimeCancellation.IsCancellationRequested)
        {
            completion.TrySetCanceled();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not open paths received from Windows Explorer");
            completion.TrySetResult();
        }
    }

    private async Task ProcessExternalPathsAsync(IReadOnlyList<string> paths)
    {
        var gateAcquired = false;
        try
        {
            await externalOpenGate.WaitAsync(lifetimeCancellation.Token);
            gateAcquired = true;

            await InitializationTask;
            while (!disposed && IsScanning)
            {
                await Task.Delay(100, lifetimeCancellation.Token);
            }

            if (disposed) return;

            IsSettingsOpen = false;
            var files = paths.Where(File.Exists).ToArray();
            var folders = paths.Where(Directory.Exists).ToArray();
            var supportedFiles = files
                .Where(SupportedAudioFiles.IsSupported)
                .ToArray();
            var unsupportedCount = files.Length - supportedFiles.Length;

            if (supportedFiles.Length > 0)
            {
                ShowLibrary();
                ClearLibraryFilters();
                await ScanAndAddFilesAsync(supportedFiles);
            }

            var skippedDirectories = 0;
            foreach (var folder in folders)
            {
                var result = await ScanAndAddFolderAsync(folder);
                if (result is not null)
                    skippedDirectories += result.SkippedDirectories;
            }

            if (skippedDirectories > 0 && !disposed)
            {
                dialogService.ShowInformation(
                    $"{skippedDirectories} folder(s) could not be read and were skipped.");
            }

            if (supportedFiles.Length > 0)
            {
                var played = false;
                foreach (var file in supportedFiles)
                {
                    var track = Tracks.FirstOrDefault(candidate =>
                        string.Equals(
                            candidate.FilePath,
                            file,
                            StringComparison.OrdinalIgnoreCase));
                    if (track is null) continue;

                    SelectedTrack = track;
                    if (TryPlayTrackFromCurrentView(track, showError: false))
                    {
                        played = true;
                        break;
                    }
                }

                if (!played && !disposed)
                {
                    dialogService.ShowWarning(
                        "None of the opened songs could be played.",
                        "Playback error");
                }
            }
            else if (folders.Length == 0 && !disposed)
            {
                dialogService.ShowWarning(
                    unsupportedCount > 0
                        ? "Mimi doesn't support the opened file type(s)."
                        : "The opened file(s) could not be found.",
                    "Open with Mimi");
            }

            Log.Information(
                "Processed external open request: {FileCount} supported files, " +
                "{FolderCount} folders, {UnsupportedCount} unsupported files",
                supportedFiles.Length,
                folders.Length,
                unsupportedCount);
        }
        catch (OperationCanceledException) when (
            lifetimeCancellation.IsCancellationRequested)
        {
            // Shutdown is in progress.
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not process paths opened with Mimi");
            if (!disposed)
            {
                dialogService.ShowWarning(
                    $"Couldn't open those paths with Mimi.\n\n{ex.Message}",
                    "Open with Mimi");
            }
        }
        finally
        {
            if (gateAcquired)
                externalOpenGate.Release();
        }
    }

    private void CancelPendingExternalOpen()
    {
        externalOpenDebounce?.Cancel();
        externalOpenDebounce?.Dispose();
        externalOpenDebounce = null;
        pendingExternalPaths.Clear();
        externalOpenBatchCompletion?.TrySetCanceled();
        externalOpenBatchCompletion = null;
    }
}
