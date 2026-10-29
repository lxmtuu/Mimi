using Mimi.Models;
using Mimi.Services;

namespace Mimi.Tests;

public sealed class LibraryServiceTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        "MimiLibraryTests-" + Guid.NewGuid().ToString("N"));
    private readonly LibraryService service = new();

    public LibraryServiceTests() => Directory.CreateDirectory(root);

    [Fact]
    public void ScanFiles_FiltersMissingAndUnsupportedFiles()
    {
        var mp3 = Touch("Song One.mp3");
        var text = Touch("notes.txt");
        var missing = Path.Combine(root, "missing.flac");

        var tracks = service.ScanFiles([mp3, text, missing]).ToArray();

        var track = Assert.Single(tracks);
        Assert.Equal("Song One", track.Title);
        Assert.Equal("MP3", track.Format);
    }

    [Fact]
    public void ScanFiles_DeduplicatesPathsCaseInsensitively()
    {
        var song = Touch("Repeat.mp3");

        var tracks = service.ScanFiles([
            song,
            song,
            song.ToUpperInvariant()
        ]).ToArray();

        Assert.Single(tracks);
    }

    [Fact]
    public void ScanFolder_RecursiveFindsNestedAudioFiles()
    {
        var rootSong = Touch("root.wav");
        var nestedDirectory = Path.Combine(root, "nested");
        Directory.CreateDirectory(nestedDirectory);
        var nestedSong = Touch(Path.Combine("nested", "child.flac"));
        Touch(Path.Combine("nested", "cover.jpg"));

        var result = service.ScanFolder(root);

        Assert.Equal(0, result.SkippedDirectories);
        Assert.Equal(
            new[] { rootSong, nestedSong }.Order(StringComparer.OrdinalIgnoreCase),
            result.Tracks.Select(track => track.FilePath)
                .Order(StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void ScanFolder_NonRecursiveIgnoresSubfolders()
    {
        var rootSong = Touch("root.mp3");
        var nestedDirectory = Path.Combine(root, "nested");
        Directory.CreateDirectory(nestedDirectory);
        Touch(Path.Combine("nested", "child.mp3"));

        var result = service.ScanFolder(root, recursive: false);

        Assert.Equal(
            [rootSong],
            result.Tracks.Select(track => track.FilePath));
    }

    [Fact]
    public void ScanFolder_MissingFolderReturnsEmptyResult()
    {
        var result = service.ScanFolder(Path.Combine(root, "does-not-exist"));

        Assert.Empty(result.Tracks);
        Assert.Equal(0, result.SkippedDirectories);
    }

    [Fact]
    public async Task ScanFolderAsync_FindsSupportedFilesAndReportsProgress()
    {
        var rootSong = Touch("root.mp3");
        var childSong = Touch(Path.Combine("nested", "child.flac"));
        Touch("notes.txt");
        var progress = new RecordingProgress();

        var result = await service.ScanFolderAsync(root, progress: progress);

        Assert.Equal(
            new[] { rootSong, childSong }.Order(StringComparer.OrdinalIgnoreCase),
            result.Tracks.Select(track => track.FilePath)
                .Order(StringComparer.OrdinalIgnoreCase));
        var reports = progress.Reports;
        Assert.NotEmpty(reports);
        Assert.Equal(2, reports[^1].DiscoveredFiles);
        Assert.Equal(2, reports[^1].ProcessedFiles);
    }

    [Fact]
    public async Task ScanFolderAsync_WithCanceledToken_ThrowsOperationCanceledException()
    {
        Touch("song.mp3");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ScanFolderAsync(
                root,
                cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task ScanFilesAsync_ReusesKnownTrackWhenFileFingerprintIsUnchanged()
    {
        var song = Touch("Known.mp3");
        var known = Assert.Single(service.ScanFiles([song]));
        known.MetadataTitle = "Known Title";
        var knownTracks = new Dictionary<string, Track>(
            StringComparer.OrdinalIgnoreCase)
        {
            [song] = known
        };

        var tracks = await service.ScanFilesAsync(
            [song],
            knownTracks: knownTracks);

        Assert.Same(known, Assert.Single(tracks));
        Assert.Equal("Known Title", tracks[0].Title);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private string Touch(string relativePath)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, [1]);
        return path;
    }

    private sealed class RecordingProgress : IProgress<LibraryScanProgress>
    {
        private readonly object reportsLock = new();
        private readonly List<LibraryScanProgress> reports = [];

        public IReadOnlyList<LibraryScanProgress> Reports
        {
            get
            {
                lock (reportsLock) return reports.ToArray();
            }
        }

        public void Report(LibraryScanProgress value)
        {
            lock (reportsLock) reports.Add(value);
        }
    }
}
