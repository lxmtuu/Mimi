using Mimi.Services;

namespace Mimi.Tests;

public sealed class LibraryFolderWatcherTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        "MimiFolderWatcherTests-" + Guid.NewGuid().ToString("N"));

    public LibraryFolderWatcherTests() => Directory.CreateDirectory(root);

    [Fact]
    public void ChangeBufferCoalescesFileAndDirectoryEvents()
    {
        var buffer = new LibraryFolderChangeBuffer();
        var recreated = Path.Combine(root, "recreated.mp3");
        var removed = Path.Combine(root, "removed.flac");
        var directory = Path.Combine(root, "Album");

        buffer.AddDeleted(recreated);
        buffer.AddChanged(recreated);
        buffer.AddChanged(removed);
        buffer.AddDeleted(removed);
        buffer.AddDirectory(directory);
        buffer.AddDirectory(directory);
        buffer.AddOverflow(root);

        var changes = buffer.Drain();

        Assert.Equal([recreated], changes.ChangedFilePaths);
        Assert.Equal([removed], changes.DeletedFilePaths);
        Assert.Equal([root, directory], changes.DirectoriesToRescan);
        Assert.True(changes.BufferOverflowed);
        Assert.False(buffer.Drain().HasChanges);
    }

    [Fact]
    public void ChangesMergeWithoutDuplicatePaths()
    {
        var first = new LibraryFolderChanges(
            [@"C:\Music\One.mp3"],
            [],
            [@"C:\Music"],
            false);
        var second = new LibraryFolderChanges(
            [@"c:\music\one.mp3", @"C:\Music\Two.mp3"],
            [@"C:\Music\Old.mp3"],
            [],
            true);

        var merged = first.Merge(second);

        Assert.Equal(2, merged.ChangedFilePaths.Count);
        Assert.Equal([@"C:\Music\Old.mp3"], merged.DeletedFilePaths);
        Assert.Equal([@"C:\Music"], merged.DirectoriesToRescan);
        Assert.True(merged.BufferOverflowed);
        Assert.True(merged.HasChanges);
    }

    [Fact]
    public async Task WatcherReportsCreatedAndDeletedAudioFiles()
    {
        using var watcher = new FileSystemLibraryFolderWatcher(
            TimeSpan.FromMilliseconds(50));
        var created = new TaskCompletionSource<LibraryFolderChanges>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var deleted = new TaskCompletionSource<LibraryFolderChanges>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var songPath = Path.Combine(root, "watched.mp3");

        watcher.ChangesDetected += (_, changes) =>
        {
            if (changes.ChangedFilePaths.Contains(
                    songPath,
                    StringComparer.OrdinalIgnoreCase))
            {
                created.TrySetResult(changes);
            }

            if (changes.DeletedFilePaths.Contains(
                    songPath,
                    StringComparer.OrdinalIgnoreCase))
            {
                deleted.TrySetResult(changes);
            }
        };
        watcher.Configure([root], isEnabled: true);

        await File.WriteAllTextAsync(
            Path.Combine(root, "ignored.txt"),
            "not music");
        await File.WriteAllTextAsync(songPath, "not a real MP3");

        var createdChanges = await created.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains(songPath, createdChanges.ChangedFilePaths);
        Assert.DoesNotContain(
            createdChanges.ChangedFilePaths,
            path => path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase));

        File.Delete(songPath);
        var deletedChanges = await deleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains(songPath, deletedChanges.DeletedFilePaths);
    }

    [Fact]
    public async Task DisabledWatcherDoesNotReportChanges()
    {
        using var watcher = new FileSystemLibraryFolderWatcher(
            TimeSpan.FromMilliseconds(50));
        var reported = false;
        watcher.ChangesDetected += (_, _) => reported = true;
        watcher.Configure([root], isEnabled: false);

        await File.WriteAllTextAsync(
            Path.Combine(root, "ignored.mp3"),
            "not a real MP3");
        await Task.Delay(300);

        Assert.False(reported);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
