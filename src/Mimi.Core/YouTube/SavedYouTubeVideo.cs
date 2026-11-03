namespace Mimi.YouTube;

/// <summary>
/// A YouTube item the user chose to keep.
///
/// Only identifying metadata is stored: the video id, its title, the channel,
/// and a thumbnail address. Nothing about the media is saved — no stream URL,
/// no file, no cached audio — and playback still happens only in YouTube's own
/// visible player.
/// </summary>
public sealed record SavedYouTubeVideo(
    string VideoId,
    string Title,
    string ChannelTitle,
    string? ThumbnailUrl)
{
    /// <summary>
    /// Rebuilds the search-result shape so a saved item can be shown and played
    /// by exactly the same code path as a fresh result.
    /// </summary>
    public YouTubeVideo ToVideo() => new(
        VideoId,
        Title,
        ChannelTitle,
        Uri.TryCreate(ThumbnailUrl, UriKind.Absolute, out var thumbnail) &&
        thumbnail.Scheme == Uri.UriSchemeHttps
            ? thumbnail
            : null);
}
