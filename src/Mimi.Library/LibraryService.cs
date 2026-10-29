using System.IO;
using Mimi.Models;

namespace Mimi.Services;

/// <summary>
/// Discovers local audio files and creates track records from their tags.
/// Folder walking is level-by-level so one inaccessible directory never
/// discards the result of the whole scan.
/// </summary>
public sealed class LibraryService : ILibraryService
{
    private readonly ITrackMetadataReader metadataReader;

    internal static bool IsSupportedAudioFile(string filePath) =>
        SupportedAudioFiles.IsSupported(filePath);

    public LibraryService()
        : this(new TagLibTrackMetadataReader())
    {
    }

    public LibraryService(ITrackMetadataReader metadataReader) =>
        this.metadataReader = metadataReader;

    public IEnumerable<Track> ScanFiles(IEnumerable<string> paths) =>
        FilterCandidatePaths(paths)
            .Select(path => CreateTrack(path, knownTracks: null));

    public FolderScanResult ScanFolder(string folder, bool recursive = true)
    {
        if (!Directory.Exists(folder)) return new FolderScanResult([], 0);

        var enumerated = EnumerateFiles(folder, recursive, CancellationToken.None);
        return new FolderScanResult(
            ScanFiles(enumerated.FilePaths).ToArray(),
            enumerated.SkippedDirectories);
    }

    public Task<IReadOnlyList<Track>> ScanFilesAsync(
        IEnumerable<string> paths,
        IProgress<LibraryScanProgress>? progress = null,
        IReadOnlyDictionary<string, Track>? knownTracks = null,
        CancellationToken cancellationToken = default) =>
        ScanCandidatePathsAsync(
            FilterCandidatePaths(paths),
            skippedDirectories: 0,
            progress,
            knownTracks,
            cancellationToken);

    public async Task<FolderScanResult> ScanFolderAsync(
        string folder,
        bool recursive = true,
        IProgress<LibraryScanProgress>? progress = null,
        IReadOnlyDictionary<string, Track>? knownTracks = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(folder))
        {
            progress?.Report(new LibraryScanProgress(0, 0, 0, null));
            return new FolderScanResult([], 0);
        }

        var enumerated = await Task.Run(
            () => EnumerateFiles(folder, recursive, cancellationToken),
            cancellationToken);

        var candidates = FilterCandidatePaths(enumerated.FilePaths);
        var tracks = await ScanCandidatePathsAsync(
            candidates,
            enumerated.SkippedDirectories,
            progress,
            knownTracks,
            cancellationToken);

        return new FolderScanResult(tracks, enumerated.SkippedDirectories);
    }

    private async Task<IReadOnlyList<Track>> ScanCandidatePathsAsync(
        IReadOnlyList<string> candidates,
        int skippedDirectories,
        IProgress<LibraryScanProgress>? progress,
        IReadOnlyDictionary<string, Track>? knownTracks,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (candidates.Count == 0)
        {
            progress?.Report(new LibraryScanProgress(
                0,
                0,
                skippedDirectories,
                null));
            return [];
        }

        var results = new Track?[candidates.Count];
        var processed = 0;
        var options = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount, 1, 4)
        };

        await Parallel.ForEachAsync(
            Enumerable.Range(0, candidates.Count),
            options,
            (index, _) =>
            {
                results[index] = CreateTrack(candidates[index], knownTracks);
                var completed = Interlocked.Increment(ref processed);
                progress?.Report(new LibraryScanProgress(
                    candidates.Count,
                    completed,
                    skippedDirectories,
                    candidates[index]));
                return ValueTask.CompletedTask;
            });

        return results.OfType<Track>().ToArray();
    }

    private Track CreateTrack(
        string filePath,
        IReadOnlyDictionary<string, Track>? knownTracks)
    {
        if (knownTracks is not null &&
            knownTracks.TryGetValue(filePath, out var knownTrack) &&
            HasSameFingerprint(knownTrack, filePath) &&
            HasCurrentCoverArtVariant(knownTrack))
        {
            knownTrack.IsMissing = false;
            return knownTrack;
        }

        return metadataReader.CreateTrack(filePath);
    }

    private static bool HasCurrentCoverArtVariant(Track knownTrack) =>
        knownTrack.CoverArtPath is null ||
        CoverArtPaths.IsLargeVariant(knownTrack.CoverArtPath);

    private static bool HasSameFingerprint(Track knownTrack, string filePath)
    {
        if (knownTrack.FileModifiedUtc is null) return false;

        try
        {
            var info = new FileInfo(filePath);
            if (!info.Exists) return false;

            var currentModifiedMs = new DateTimeOffset(
                info.LastWriteTimeUtc,
                TimeSpan.Zero).ToUnixTimeMilliseconds();

            return knownTrack.FileSizeBytes == info.Length &&
                   knownTrack.FileModifiedUtc.Value
                       .ToUnixTimeMilliseconds() == currentModifiedMs;
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static List<string> FilterCandidatePaths(IEnumerable<string> paths) =>
        paths.Where(File.Exists)
            .Where(SupportedAudioFiles.IsSupported)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static EnumeratedFiles EnumerateFiles(
        string folder,
        bool recursive,
        CancellationToken cancellationToken)
    {
        var files = new List<string>();
        var skipped = 0;
        var pending = new Stack<string>();
        pending.Push(folder);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();
            try
            {
                files.AddRange(Directory.EnumerateFiles(directory));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                           or System.Security.SecurityException)
            {
                skipped++;
                continue;
            }

            if (!recursive) continue;
            try
            {
                foreach (var subDirectory in Directory.EnumerateDirectories(directory))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Do not follow junctions/symlinks: they can create cycles.
                    if ((File.GetAttributes(subDirectory) & FileAttributes.ReparsePoint) != 0) continue;
                    pending.Push(subDirectory);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                           or System.Security.SecurityException)
            {
                skipped++;
            }
        }

        return new EnumeratedFiles(files, skipped);
    }

    private sealed record EnumeratedFiles(
        IReadOnlyList<string> FilePaths,
        int SkippedDirectories);
}
