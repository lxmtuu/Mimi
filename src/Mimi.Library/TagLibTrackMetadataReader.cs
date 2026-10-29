using System.Security.Cryptography;
using Mimi.Models;

namespace Mimi.Services;

/// <summary>
/// TagLib# metadata reader. Unsupported or damaged tags deliberately fall
/// back to the file name and basic file information so one bad file cannot
/// discard an otherwise useful library scan.
/// </summary>
public sealed class TagLibTrackMetadataReader : ITrackMetadataReader
{
    private readonly string? coverCacheDirectory;

    public TagLibTrackMetadataReader()
        : this((string?)null)
    {
    }

    public TagLibTrackMetadataReader(IUserDataService userDataService)
        : this(Path.Combine(
            userDataService.DataDirectory,
            "cache",
            "covers"))
    {
    }

    internal TagLibTrackMetadataReader(string? coverCacheDirectory) =>
        this.coverCacheDirectory = coverCacheDirectory;

    public Track CreateTrack(string filePath)
    {
        var track = new Track
        {
            FilePath = filePath,
            AddedAtUtc = DateTimeOffset.UtcNow
        };

        try
        {
            var info = new FileInfo(filePath);
            track.IsMissing = !info.Exists;
            if (info.Exists)
            {
                track.FileSizeBytes = info.Length;
                track.FileModifiedUtc = new DateTimeOffset(
                    info.LastWriteTimeUtc,
                    TimeSpan.Zero);
            }
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            track.IsMissing = true;
        }

        if (track.IsMissing) return track;

        try
        {
            using var tagFile = TagLib.File.Create(filePath);
            var tag = tagFile.Tag;
            if (tag is not null)
            {
                track.MetadataTitle = CleanOrNull(tag.Title);
                track.Artist = Clean(tag.FirstPerformer);
                track.Album = Clean(tag.Album);
                track.AlbumArtist = Clean(tag.FirstAlbumArtist);
                track.Genre = Clean(tag.FirstGenre);
                track.Year = tag.Year == 0 ? null : (int)tag.Year;
                track.CoverArtPath = CacheCoverArt(tag.Pictures);

                if (track.MetadataTitle is not null ||
                    track.Artist.Length > 0 ||
                    track.Album.Length > 0 ||
                    track.Genre.Length > 0 ||
                    track.Year is not null ||
                    track.CoverArtPath is not null)
                {
                    track.MetadataSource = "tag";
                }
            }

            var properties = tagFile.Properties;
            if (properties is not null)
            {
                if (properties.Duration > TimeSpan.Zero)
                    track.Duration = properties.Duration;
                if (properties.AudioBitrate > 0)
                    track.BitrateKbps = properties.AudioBitrate;
                if (properties.AudioSampleRate > 0)
                    track.SampleRateHz = properties.AudioSampleRate;
            }
        }
        catch (Exception)
        {
            // TagLib raises format-specific exceptions for damaged or
            // unsupported files. Keep the file-name fallback and let the
            // playback engine be the authority on whether audio can play.
            track.MetadataSource = "filename";
        }

        return track;
    }

    private string? CacheCoverArt(TagLib.IPicture[]? pictures)
    {
        if (coverCacheDirectory is null || pictures is null) return null;

        var picture = pictures.FirstOrDefault(candidate =>
            candidate?.Data?.Data is { Length: > 0 });
        var bytes = picture?.Data?.Data;
        if (bytes is null || bytes.Length == 0) return null;

        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var extension = GetImageExtension(picture?.MimeType);
        var cachePath = Path.Combine(coverCacheDirectory, hash + extension);
        if (File.Exists(cachePath)) return cachePath;

        var tempPath = cachePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(coverCacheDirectory);
            File.WriteAllBytes(tempPath, bytes);
            File.Move(tempPath, cachePath, overwrite: true);
            return cachePath;
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static string GetImageExtension(string? mimeType)
    {
        if (mimeType?.Contains("png", StringComparison.OrdinalIgnoreCase) == true)
            return ".png";
        if (mimeType?.Contains("gif", StringComparison.OrdinalIgnoreCase) == true)
            return ".gif";
        if (mimeType?.Contains("bmp", StringComparison.OrdinalIgnoreCase) == true)
            return ".bmp";
        if (mimeType?.Contains("webp", StringComparison.OrdinalIgnoreCase) == true)
            return ".webp";
        return ".jpg";
    }

    private static string Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string? CleanOrNull(string? value)
    {
        var cleaned = Clean(value);
        return cleaned.Length == 0 ? null : cleaned;
    }
}
