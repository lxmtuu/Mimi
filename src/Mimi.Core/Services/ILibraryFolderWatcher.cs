namespace Mimi.Services;

/// <summary>
/// One debounced batch of file-system changes from the folders Mimi is
/// watching. Directory-level changes and watcher-buffer overflows ask the
/// caller to rescan because individual file events may have been missed.
/// </summary>
public sealed record LibraryFolderChanges(
    IReadOnlyList<string> ChangedFilePaths,
    IReadOnlyList<string> DeletedFilePaths,
    IReadOnlyList<string> DirectoriesToRescan,
    bool BufferOverflowed)
{
    public static LibraryFolderChanges Empty { get; } = new([], [], [], false);

    public bool HasChanges =>
        BufferOverflowed ||
        ChangedFilePaths.Count > 0 ||
        DeletedFilePaths.Count > 0 ||
        DirectoriesToRescan.Count > 0;

    public LibraryFolderChanges Merge(LibraryFolderChanges other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return new LibraryFolderChanges(
            Union(ChangedFilePaths, other.ChangedFilePaths),
            Union(DeletedFilePaths, other.DeletedFilePaths),
            Union(DirectoriesToRescan, other.DirectoriesToRescan),
            BufferOverflowed || other.BufferOverflowed);
    }

    private static string[] Union(
        IEnumerable<string>? first,
        IEnumerable<string>? second) =>
        (first ?? []).Concat(second ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}

/// <summary>
/// Watches user-selected music folders and reports debounced batches of
/// supported audio changes. Implementations may raise
/// <see cref="ChangesDetected"/> on a background thread.
/// </summary>
public interface ILibraryFolderWatcher : IDisposable
{
    event EventHandler<LibraryFolderChanges>? ChangesDetected;

    void Configure(IEnumerable<string> folders, bool isEnabled);
}
