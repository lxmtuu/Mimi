using System.IO;

namespace Mimi.Services;

/// <summary>
/// FileSystemWatcher-backed implementation of <see cref="ILibraryFolderWatcher"/>.
/// Events are coalesced for a short quiet period so copying or retagging a
/// song produces one library synchronization instead of one per OS event.
/// </summary>
public sealed class FileSystemLibraryFolderWatcher : ILibraryFolderWatcher
{
    private static readonly TimeSpan DefaultDebounceDelay = TimeSpan.FromSeconds(1);

    private readonly object sync = new();
    private readonly LibraryFolderChangeBuffer buffer = new();
    private readonly TimeSpan debounceDelay;
    private readonly Timer debounceTimer;
    private readonly List<FileSystemWatcher> watchers = [];
    private bool disposed;

    public FileSystemLibraryFolderWatcher()
        : this(DefaultDebounceDelay)
    {
    }

    internal FileSystemLibraryFolderWatcher(TimeSpan debounceDelay)
    {
        if (debounceDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(debounceDelay));

        this.debounceDelay = debounceDelay;
        debounceTimer = new Timer(
            _ => PublishChanges(),
            null,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);
    }

    public event EventHandler<LibraryFolderChanges>? ChangesDetected;

    public void Configure(IEnumerable<string> folders, bool isEnabled)
    {
        ArgumentNullException.ThrowIfNull(folders);

        List<FileSystemWatcher> previousWatchers;
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            debounceTimer.Change(
                Timeout.InfiniteTimeSpan,
                Timeout.InfiniteTimeSpan);
            previousWatchers = [.. watchers];
            watchers.Clear();
            buffer.Clear();

            if (isEnabled)
            {
                foreach (var root in NormalizeRoots(folders))
                {
                    if (!Directory.Exists(root)) continue;

                    try
                    {
                        watchers.Add(CreateWatcher(root));
                    }
                    catch (Exception ex) when (
                        ex is IOException or
                        UnauthorizedAccessException or
                        ArgumentException)
                    {
                        // A removed drive or inaccessible root must not stop
                        // the remaining watched folders from working.
                    }
                }
            }
        }

        foreach (var watcher in previousWatchers)
            DisposeWatcher(watcher);
    }

    public void Dispose()
    {
        List<FileSystemWatcher> watchersToDispose;
        lock (sync)
        {
            if (disposed) return;
            disposed = true;
            watchersToDispose = [.. watchers];
            watchers.Clear();
            buffer.Clear();
        }

        debounceTimer.Dispose();
        foreach (var watcher in watchersToDispose)
            DisposeWatcher(watcher);
    }

    private FileSystemWatcher CreateWatcher(string root)
    {
        var watcher = new FileSystemWatcher(root)
        {
            IncludeSubdirectories = true,
            InternalBufferSize = 64 * 1024,
            NotifyFilter = NotifyFilters.FileName |
                           NotifyFilters.DirectoryName |
                           NotifyFilters.LastWrite |
                           NotifyFilters.Size
        };

        watcher.Created += (_, e) => HandleCreated(e.FullPath);
        watcher.Changed += (_, e) => HandleChanged(e.FullPath);
        watcher.Deleted += (_, e) => HandleDeleted(e.FullPath);
        watcher.Renamed += (_, e) => HandleRenamed(e.OldFullPath, e.FullPath);
        watcher.Error += (_, _) =>
        {
            buffer.AddOverflow(root);
            SchedulePublish();
        };
        watcher.EnableRaisingEvents = true;
        return watcher;
    }

    private void HandleCreated(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        if (Directory.Exists(path))
            buffer.AddDirectory(path);
        else if (LibraryService.IsSupportedAudioFile(path))
            buffer.AddChanged(path);
        else
            return;

        SchedulePublish();
    }

    private void HandleChanged(string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            Directory.Exists(path) ||
            !LibraryService.IsSupportedAudioFile(path))
        {
            return;
        }

        buffer.AddChanged(path);
        SchedulePublish();
    }

    private void HandleDeleted(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        if (LibraryService.IsSupportedAudioFile(path))
            buffer.AddDeleted(path);
        else if (string.IsNullOrEmpty(Path.GetExtension(path)))
            buffer.AddDirectory(path);
        else
            return;

        SchedulePublish();
    }

    private void HandleRenamed(string oldPath, string newPath)
    {
        if (string.IsNullOrWhiteSpace(newPath)) return;

        if (Directory.Exists(newPath))
        {
            if (!string.IsNullOrWhiteSpace(oldPath))
                buffer.AddDirectory(oldPath);
            buffer.AddDirectory(newPath);
        }
        else
        {
            var handled = false;
            if (!string.IsNullOrWhiteSpace(oldPath) &&
                LibraryService.IsSupportedAudioFile(oldPath))
            {
                buffer.AddDeleted(oldPath);
                handled = true;
            }

            if (LibraryService.IsSupportedAudioFile(newPath))
            {
                buffer.AddChanged(newPath);
                handled = true;
            }

            if (!handled) return;
        }

        SchedulePublish();
    }

    private void SchedulePublish()
    {
        if (disposed) return;

        try
        {
            debounceTimer.Change(debounceDelay, Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException)
        {
            // Configure/Dispose raced with a file-system event.
        }
    }

    private void PublishChanges()
    {
        if (disposed) return;

        var changes = buffer.Drain();
        if (!changes.HasChanges) return;

        try
        {
            ChangesDetected?.Invoke(this, changes);
        }
        catch (Exception)
        {
            // A subscriber failure should not terminate the timer thread or
            // permanently disable folder watching.
        }
    }

    private static string[] NormalizeRoots(IEnumerable<string> folders)
    {
        var roots = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in folders)
        {
            if (string.IsNullOrWhiteSpace(folder)) continue;

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(folder.Trim());
            }
            catch (Exception ex) when (
                ex is ArgumentException or
                NotSupportedException or
                PathTooLongException)
            {
                continue;
            }

            if (seen.Add(fullPath))
                roots.Add(fullPath);
        }

        return [.. roots];
    }

    private static void DisposeWatcher(FileSystemWatcher watcher)
    {
        watcher.EnableRaisingEvents = false;
        watcher.Dispose();
    }
}

/// <summary>Thread-safe accumulator for one debounced watcher batch.</summary>
internal sealed class LibraryFolderChangeBuffer
{
    private readonly object sync = new();
    private readonly HashSet<string> changedFiles =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> deletedFiles =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> directoriesToRescan =
        new(StringComparer.OrdinalIgnoreCase);
    private bool bufferOverflowed;

    public void AddChanged(string path)
    {
        lock (sync)
        {
            deletedFiles.Remove(path);
            changedFiles.Add(path);
        }
    }

    public void AddDeleted(string path)
    {
        lock (sync)
        {
            changedFiles.Remove(path);
            deletedFiles.Add(path);
        }
    }

    public void AddDirectory(string path)
    {
        lock (sync)
            directoriesToRescan.Add(path);
    }

    public void AddOverflow(string root)
    {
        lock (sync)
        {
            bufferOverflowed = true;
            directoriesToRescan.Add(root);
        }
    }

    public void Clear()
    {
        lock (sync)
        {
            changedFiles.Clear();
            deletedFiles.Clear();
            directoriesToRescan.Clear();
            bufferOverflowed = false;
        }
    }

    public LibraryFolderChanges Drain()
    {
        lock (sync)
        {
            var changes = new LibraryFolderChanges(
                Sorted(changedFiles),
                Sorted(deletedFiles),
                Sorted(directoriesToRescan),
                bufferOverflowed);
            changedFiles.Clear();
            deletedFiles.Clear();
            directoriesToRescan.Clear();
            bufferOverflowed = false;
            return changes;
        }
    }

    private static string[] Sorted(IEnumerable<string> values) =>
        values.Order(StringComparer.OrdinalIgnoreCase).ToArray();
}
