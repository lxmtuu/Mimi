namespace Mimi.YouTube;

/// <summary>
/// One YouTube search result. Mimi only receives metadata from the official
/// YouTube Data API; the video itself is played by YouTube's own embedded
/// player, which the user always sees. No audio is downloaded, extracted,
/// proxied, or cached by Mimi.
/// </summary>
/// <param name="VideoId">YouTube video identifier.</param>
/// <param name="Title">Video title as reported by the API.</param>
/// <param name="ChannelTitle">Uploading channel.</param>
/// <param name="ThumbnailUrl">Public HTTPS thumbnail URL.</param>
public sealed record YouTubeVideo(
    string VideoId,
    string Title,
    string ChannelTitle,
    Uri? ThumbnailUrl)
{
    /// <summary>Canonical watch page, used for the "open on YouTube" action.</summary>
    public Uri WatchUrl => new($"https://www.youtube.com/watch?v={VideoId}");

    /// <summary>
    /// Official embed URL. Playback stays inside YouTube's player and the
    /// player remains visible for as long as it is playing.
    /// </summary>
    public Uri EmbedUrl => new(
        $"https://www.youtube.com/embed/{VideoId}" +
        "?autoplay=1&rel=0&enablejsapi=0");
}
