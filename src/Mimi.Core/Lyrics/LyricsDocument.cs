namespace Mimi.Lyrics;

/// <summary>Where Mimi found a track's offline lyrics.</summary>
public enum LyricsSourceKind
{
    SidecarFile,
    EmbeddedTag
}

/// <summary>
/// One lyric line. A null timestamp means the source supplied plain,
/// unsynchronized text rather than timed LRC lines.
/// </summary>
public sealed record LyricLine(TimeSpan? Timestamp, string Text);

/// <summary>
/// Parsed offline lyrics for one track. Timed lines are stored in playback
/// order; plain embedded lyrics keep their original order.
/// </summary>
public sealed class LyricsDocument
{
    internal LyricsDocument(
        LyricsSourceKind source,
        IReadOnlyList<LyricLine> lines,
        TimeSpan documentOffset)
    {
        Source = source;
        Lines = lines;
        DocumentOffset = documentOffset;
    }

    public LyricsSourceKind Source { get; }

    public IReadOnlyList<LyricLine> Lines { get; }

    /// <summary>
    /// The raw value of an LRC <c>[offset:]</c> tag. Mimi follows the common
    /// LRC reading of that tag: a positive document offset makes lines appear
    /// sooner, while a negative document offset makes them appear later.
    /// </summary>
    public TimeSpan DocumentOffset { get; }

    public bool IsSynchronized =>
        Lines.Any(line => line.Timestamp.HasValue);

    /// <summary>
    /// Returns when a timed line should become current. In contrast to the
    /// document tag above, <paramref name="userDelay"/> uses the intuitive UI
    /// convention: positive values delay the display of every line.
    /// </summary>
    public TimeSpan? GetEffectiveTimestamp(
        LyricLine line,
        TimeSpan userDelay)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (line.Timestamp is not { } timestamp) return null;

        var effective = timestamp - DocumentOffset + userDelay;
        return effective < TimeSpan.Zero ? TimeSpan.Zero : effective;
    }

    /// <summary>
    /// Finds the last timed line whose effective timestamp has been reached.
    /// Returns -1 before the first line or for unsynchronized lyrics.
    /// </summary>
    public int FindCurrentLineIndex(TimeSpan position, TimeSpan userDelay)
    {
        if (!IsSynchronized || Lines.Count == 0) return -1;

        // position >= raw - DocumentOffset + userDelay
        // therefore raw <= position + DocumentOffset - userDelay.
        var adjustedPosition = position + DocumentOffset - userDelay;
        var low = 0;
        var high = Lines.Count - 1;
        var current = -1;

        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var timestamp = Lines[middle].Timestamp;
            if (timestamp.HasValue && timestamp.Value <= adjustedPosition)
            {
                current = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return current;
    }
}
