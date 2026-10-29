using System.IO;
using Mimi.Models;

namespace Mimi.Services;

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

    /// <summary>Result of a folder scan: the tracks found plus how many
    /// directories could not be read (reported to the user, never fatal).</summary>
    public sealed record FolderScanResult(IReadOnlyList<Track> Tracks, int SkippedDirectories);

    public FolderScanResult ScanFolder(string folder, bool recursive = true)
    {
        if (!Directory.Exists(folder)) return new FolderScanResult([], 0);

        // Walk folder-by-folder so one inaccessible subfolder only skips
        // itself instead of discarding the result of the whole scan.
        var files = new List<string>();
        var skipped = 0;
        var pending = new Stack<string>();
        pending.Push(folder);

        while (pending.Count > 0)
        {
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

        return new FolderScanResult(ScanFiles(files).ToArray(), skipped);
    }
}

