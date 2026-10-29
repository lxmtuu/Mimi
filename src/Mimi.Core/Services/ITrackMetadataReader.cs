using Mimi.Models;

namespace Mimi.Services;

/// <summary>
/// Reads tags, technical audio properties, file fingerprints, and embedded
/// cover art. Implementations must fall back to file-name metadata instead of
/// failing an entire library scan because one file has damaged tags.
/// </summary>
public interface ITrackMetadataReader
{
    Track CreateTrack(string filePath);
}
