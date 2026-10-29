using System.IO;
using MikuVerseMusic.Models;

namespace MikuVerseMusic.Services;

public sealed class LibraryService
{
    private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    { ".mp3", ".wav", ".aiff", ".aif", ".wma", ".m4a", ".aac", ".flac" };

    public IEnumerable<Track> ScanFiles(IEnumerable<string> paths)
    {
        return paths.Where(File.Exists)
            .Where(p => Supported.Contains(Path.GetExtension(p)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(p => new Track { FilePath = p });
    }

    public IEnumerable<Track> ScanFolder(string folder, bool recursive = true)
    {
        if (!Directory.Exists(folder)) return [];
        var options = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        try { return ScanFiles(Directory.EnumerateFiles(folder, "*", options)).ToArray(); }
        catch (UnauthorizedAccessException) { return []; }
        catch (IOException) { return []; }
    }
}

