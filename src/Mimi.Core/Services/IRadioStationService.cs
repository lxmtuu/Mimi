using Mimi.Models;

namespace Mimi.Services;

/// <summary>
/// Read-only internet-radio catalog used by the opt-in Radio view.
/// Implementations must use the provider's documented API and must not
/// download, record, proxy, or persist station audio streams.
/// </summary>
public interface IRadioStationService
{
    /// <summary>Returns a bounded list of currently healthy stations.</summary>
    Task<IReadOnlyList<RadioStation>> GetTopStationsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Searches station names with a bounded result count.</summary>
    Task<IReadOnlyList<RadioStation>> SearchStationsAsync(
        string query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tells the provider that the user actually started this station. This
    /// is a best-effort attribution/statistics call and must be invoked once
    /// per user-selected playback start, never for hover, preload, or search.
    /// </summary>
    Task RegisterStationClickAsync(
        Guid stationUuid,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A provider or network failure that the UI can present without exposing
/// transport details, request URLs, or the user's search query.
/// </summary>
public sealed class RadioServiceException : Exception
{
    public RadioServiceException(string message)
        : base(message)
    {
    }

    public RadioServiceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
