using System.IO;

namespace Mimi.Models;

/// <summary>
/// One local audio file plus the metadata and playback statistics Mimi has
/// learned about it. The file name remains the title fallback whenever a tag
/// is absent or unreadable.
/// </summary>
public sealed class Track
{
    public long Id { get; set; }

    public string FilePath { get; init; } = "";

    public string? MetadataTitle { get; set; }

    public string Title => !string.IsNullOrWhiteSpace(MetadataTitle)
        ? MetadataTitle.Trim()
        : Path.GetFileNameWithoutExtension(FilePath);

    public string Artist { get; set; } = "";

    public string Album { get; set; } = "";

    public string AlbumArtist { get; set; } = "";

    public string Genre { get; set; } = "";

    public int? Year { get; set; }

    public TimeSpan? Duration { get; set; }

    public int? BitrateKbps { get; set; }

    public int? SampleRateHz { get; set; }

    public long FileSizeBytes { get; set; }

    public DateTimeOffset? FileModifiedUtc { get; set; }

    public DateTimeOffset AddedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastPlayedAtUtc { get; set; }

    public int PlayCount { get; set; }

    public bool IsFavorite { get; set; }

    public bool IsMissing { get; set; }

    public bool HasError { get; set; }

    public string? CoverArtPath { get; set; }

    public string MetadataSource { get; set; } = "filename";

    public string Format =>
        Path.GetExtension(FilePath).TrimStart('.').ToUpperInvariant();

    public override string ToString() => Title;
}
