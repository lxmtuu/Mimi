namespace Mimi.Models;

/// <summary>A user-created ordered collection of tracks.</summary>
public sealed class Playlist
{
    public long Id { get; set; }

    public string Name { get; set; } = "";

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public int TrackCount { get; set; }
}
