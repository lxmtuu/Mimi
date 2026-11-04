namespace Mimi.Lyrics;

/// <summary>Where Mimi found a track's offline lyrics.</summary>
public enum LyricsSourceKind
{
    SidecarFile,
    EmbeddedTag
}

/// <summary>
/// One syllable or word inside a line, with the moment it starts being sung.
/// Enhanced LRC files carry these as inline <c>&lt;mm:ss.xx&gt;</c> tags; the
/// timestamps are absolute, in the same coordinate space as the line.
/// </summary>
public sealed record LyricWord(TimeSpan Timestamp, string Text);

/// <summary>
/// One lyric line. A null timestamp means the source supplied plain,
/// unsynchronized text rather than timed LRC lines.
/// </summary>
/// <param name="Words">
/// Word timings for karaoke highlighting, or empty when the source only timed
/// the line. Concatenating their text gives back <paramref name="Text"/>, so the
/// line still renders identically either way.
/// </param>
public sealed record LyricLine(
    TimeSpan? Timestamp,
    string Text,
    IReadOnlyList<LyricWord>? Words = null)
{
    /// <summary>Word timings, never null.</summary>
    public IReadOnlyList<LyricWord> WordTimings { get; } = Words ?? [];

    /// <summary>True when this line can be highlighted word by word.</summary>
    public bool HasWordTimings => WordTimings.Count > 0;
}

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

    /// <summary>
    /// Index of the word being sung inside <paramref name="line"/>, or -1 when
    /// the line has no word timings or nothing has started yet. Uses the same
    /// offset convention as <see cref="FindCurrentLineIndex"/>.
    /// </summary>
    public int FindCurrentWordIndex(
        LyricLine line,
        TimeSpan position,
        TimeSpan userDelay)
    {
        ArgumentNullException.ThrowIfNull(line);

        var words = line.WordTimings;
        if (words.Count == 0) return -1;

        var adjustedPosition = position + DocumentOffset - userDelay;
        var current = -1;
        for (var i = 0; i < words.Count; i++)
        {
            if (words[i].Timestamp > adjustedPosition) break;
            current = i;
        }

        return current;
    }
}
