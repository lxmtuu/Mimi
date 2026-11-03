namespace Mimi.Online;

/// <summary>
/// Search-only, streaming-only access to a permitted public catalog. The
/// implementation must not require credentials, must not download or persist
/// audio, and must reject items whose rights or availability are uncertain.
/// </summary>
public interface IOnlineCatalogService
{
    /// <summary>
    /// Searches the catalog for playable tracks. Results are already filtered
    /// to items Mimi is allowed to stream; callers never receive a blocked
    /// item to decide about.
    /// </summary>
    /// <exception cref="HttpRequestException">
    /// The catalog could not be reached or answered outside the expected
    /// contract.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The caller cancelled the search.
    /// </exception>
    Task<IReadOnlyList<OnlineTrack>> SearchTracksAsync(
        string query,
        CancellationToken cancellationToken);
}
