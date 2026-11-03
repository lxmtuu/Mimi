namespace Mimi.Online;

/// <summary>
/// A playable item from a permitted streaming catalog. Online items are a
/// separate playback source: they are never converted into a local
/// <c>Track</c>, never added to the library, queue, history, favorites, or
/// saved session, and are never downloaded or cached by Mimi.
/// </summary>
/// <param name="Id">Provider identifier used to resolve the stream.</param>
/// <param name="Title">Track title shown in the UI.</param>
/// <param name="Artist">Creator name; required for attribution.</param>
/// <param name="Duration">Finite duration reported by the provider.</param>
/// <param name="StreamUrl">
/// Direct provider stream endpoint. Mimi streams this URL only for the
/// current session and does not store the audio.
/// </param>
/// <param name="Permalink">Canonical page link shown to the user.</param>
/// <param name="ArtworkUrl">Artwork displayed for the current session only.</param>
/// <param name="License">License identifier reported by the provider.</param>
public sealed record OnlineTrack(
    string Id,
    string Title,
    string Artist,
    TimeSpan Duration,
    Uri StreamUrl,
    Uri Permalink,
    Uri? ArtworkUrl,
    string License)
{
    public string DurationLabel =>
        Duration >= TimeSpan.FromHours(1)
            ? $"{(int)Duration.TotalHours}:{Duration.Minutes:D2}:{Duration.Seconds:D2}"
            : $"{(int)Duration.TotalMinutes}:{Duration.Seconds:D2}";
}
