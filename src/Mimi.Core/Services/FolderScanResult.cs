using Mimi.Models;

namespace Mimi.Services;

/// <summary>
/// Result of a folder scan: the tracks found plus how many directories could
/// not be read. Skipped directories are reported to the user, never fatal.
/// </summary>
public sealed record FolderScanResult(
    IReadOnlyList<Track> Tracks,
    int SkippedDirectories);
