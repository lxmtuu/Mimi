namespace Mimi.YouTube;

/// <summary>
/// Search-only access to YouTube's official Data API. Implementations return
/// metadata only: they must never resolve stream URLs, download media, or
/// require a credential shipped with a distributed build.
/// </summary>
public interface IYouTubeSearchService
{
    /// <summary>
    /// True when a user-supplied API key was found. When false the feature
    /// stays hidden instead of failing at search time.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Where the user should place the key file. Shown in the UI so the
    /// configuration step is discoverable without reading documentation.
    /// </summary>
    string KeyFilePath { get; }

    /// <exception cref="YouTubeQuotaExceededException">
    /// The daily quota for this key is used up.
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// The API could not be reached or rejected the key.
    /// </exception>
    Task<IReadOnlyList<YouTubeVideo>> SearchAsync(
        string query,
        CancellationToken cancellationToken);
}

/// <summary>
/// The official API answered with a quota or rate-limit refusal. This is a
/// normal, expected state for the free quota, not a bug in Mimi.
/// </summary>
public sealed class YouTubeQuotaExceededException(string message)
    : Exception(message);
