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
    /// True when the identifier has the shape YouTube uses. The embed page
    /// validates this again before it builds a URL, because a malformed id
    /// would otherwise load YouTube's own error page.
    /// </summary>
    public bool HasValidVideoId => VideoId.Length == 11 && VideoId.All(
        character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
}
