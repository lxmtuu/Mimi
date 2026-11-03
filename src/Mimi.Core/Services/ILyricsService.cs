using Mimi.Lyrics;
using Mimi.Models;

namespace Mimi.Services;

/// <summary>
/// Loads offline lyrics owned by the user: first a same-name sidecar
/// <c>.lrc</c> beside the audio file, then lyrics embedded in its tag.
/// Implementations return null when no usable lyrics exist instead of
/// interrupting playback.
/// </summary>
public interface ILyricsService
{
    Task<LyricsDocument?> LoadAsync(
        Track track,
        CancellationToken cancellationToken = default);
}
