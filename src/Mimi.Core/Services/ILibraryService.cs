using Mimi.Models;

namespace Mimi.Services;

/// <summary>Discovers supported local audio files without mutating user data.</summary>
public interface ILibraryService
{
    IEnumerable<Track> ScanFiles(IEnumerable<string> paths);

    FolderScanResult ScanFolder(string folder, bool recursive = true);
}
