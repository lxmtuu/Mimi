using Mimi.Models;

namespace Mimi.Services;

/// <summary>Discovers supported local audio files without mutating user data.</summary>
public interface ILibraryService
{
    IEnumerable<Track> ScanFiles(IEnumerable<string> paths);

    FolderScanResult ScanFolder(string folder, bool recursive = true);

    /// <summary>
    /// Reads metadata on background threads. A known track whose file size
    /// and modified timestamp still match is reused without reading its tags.
    /// </summary>
    Task<IReadOnlyList<Track>> ScanFilesAsync(
        IEnumerable<string> paths,
        IProgress<LibraryScanProgress>? progress = null,
        IReadOnlyDictionary<string, Track>? knownTracks = null,
        CancellationToken cancellationToken = default);

    Task<FolderScanResult> ScanFolderAsync(
        string folder,
        bool recursive = true,
        IProgress<LibraryScanProgress>? progress = null,
        IReadOnlyDictionary<string, Track>? knownTracks = null,
        CancellationToken cancellationToken = default);
}
