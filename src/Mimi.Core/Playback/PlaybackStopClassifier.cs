namespace Mimi.Core.Playback;

/// <summary>
/// Classifies a non-deliberate output stop. A stop reported by the current
/// output without an exception means the output exhausted its source. The
/// decoded reader position is intentionally not required to equal its
/// reported length: WASAPI/MP3 can signal end-of-stream while the reader is
/// still a few milliseconds short of an estimated length. Deliberate stops
/// are suppressed by the audio service before this classification is used.
/// </summary>
public static class PlaybackStopClassifier
{
    /// <param name="hasException">Whether the output reported an error.</param>
    /// <param name="readerLength">
    /// Decoded length when the source exposes one. Streaming sources often
    /// report zero or nothing here.
    /// </param>
    /// <param name="reachedReportedEnd">
    /// Set when a finite streaming source has no usable length but its
    /// position reached the duration the provider reported.
    /// </param>
    public static bool IsNaturalEnd(
        bool hasException,
        long? readerLength,
        bool reachedReportedEnd = false) =>
        !hasException && (readerLength is > 0 || reachedReportedEnd);
}
