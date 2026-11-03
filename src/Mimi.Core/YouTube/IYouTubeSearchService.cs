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

    /// <summary>
    /// Writes the user's key to <see cref="KeyFilePath"/>, replacing whatever
    /// was there. Returns false when the text is not a plausible key or the
    /// file cannot be written.
    /// </summary>
    bool TrySaveApiKey(string apiKey);

    /// <summary>
    /// Deletes the stored key file. Returns false only when the file exists but
    /// cannot be removed.
    /// </summary>
    bool TryClearApiKey();

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

/// <summary>
/// The official API refused the key itself, which almost always means the key
/// was pasted incompletely, was deleted in Google Cloud, or belongs to a
/// project that has not enabled YouTube Data API v3.
/// </summary>
public sealed class YouTubeInvalidKeyException(string message)
    : Exception(message);
