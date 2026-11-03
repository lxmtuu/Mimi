namespace Mimi.Services;

/// <summary>Progress reported while music files are discovered and read.</summary>
public sealed record LibraryScanProgress(
    int DiscoveredFiles,
    int ProcessedFiles,
    int SkippedDirectories,
    string? CurrentFilePath);
