namespace Mimi.Models;

/// <summary>
/// One internet-radio station returned by an authorized provider. A station
/// is deliberately not a local <see cref="Track"/>: it has no file path, no
/// reliable duration, and its stream URL must never be persisted as library
/// data or treated as downloadable content.
/// </summary>
public sealed record RadioStation(
    Guid StationUuid,
    string Name,
    Uri StreamUrl,
    Uri? HomepageUrl,
    Uri? FaviconUrl,
    string Country,
    string Language,
    IReadOnlyList<string> Tags,
    string Codec,
    int BitrateKbps,
    int Votes,
    int ClickCount);
