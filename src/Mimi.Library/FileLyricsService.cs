using System.Text;
using Mimi.Lyrics;
using Mimi.Models;
using Mimi.Services;

namespace Mimi.Services;

/// <summary>
/// Offline lyrics loader for local files. A sidecar <c>.lrc</c> with the
/// same base name as the audio file wins over embedded tag lyrics because it
/// is the source most likely to contain line timings.
/// </summary>
public sealed class FileLyricsService : ILyricsService
{
    private const long MaximumSidecarBytes = 2 * 1024 * 1024;

    private readonly Func<string, string?> embeddedLyricsReader;

    public FileLyricsService()
        : this(ReadEmbeddedLyrics)
    {
    }

    internal FileLyricsService(Func<string, string?> embeddedLyricsReader) =>
        this.embeddedLyricsReader = embeddedLyricsReader ??
            throw new ArgumentNullException(nameof(embeddedLyricsReader));

    public async Task<LyricsDocument?> LoadAsync(
        Track track,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(track);
        if (string.IsNullOrWhiteSpace(track.FilePath)) return null;

        var sidecarDocument = await TryLoadSidecarAsync(
            track.FilePath,
            cancellationToken).ConfigureAwait(false);
        if (sidecarDocument is not null) return sidecarDocument;

        cancellationToken.ThrowIfCancellationRequested();
        var embeddedText = embeddedLyricsReader(track.FilePath);
        if (string.IsNullOrWhiteSpace(embeddedText)) return null;

        var embeddedDocument = LyricsTextParser.Parse(
            embeddedText,
            LyricsSourceKind.EmbeddedTag);
        return embeddedDocument.Lines.Count > 0
            ? embeddedDocument
            : null;
    }

    private static async Task<LyricsDocument?> TryLoadSidecarAsync(
        string audioPath,
        CancellationToken cancellationToken)
    {
        string sidecarPath;
        try
        {
            sidecarPath = Path.ChangeExtension(audioPath, ".lrc");
            var info = new FileInfo(sidecarPath);
            if (!info.Exists ||
                info.Length == 0 ||
                info.Length > MaximumSidecarBytes)
            {
                return null;
            }
        }
        catch (Exception ex) when (
            ex is ArgumentException or
            IOException or
            NotSupportedException or
            UnauthorizedAccessException)
        {
            return null;
        }

        try
        {
            await using var stream = new FileStream(
                sidecarPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 4096,
                useAsync: true);
            using var reader = new StreamReader(
                stream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true);
            var text = await reader.ReadToEndAsync(cancellationToken)
                .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(text)) return null;

            var document = LyricsTextParser.Parse(
                text,
                LyricsSourceKind.SidecarFile);
            return document.Lines.Count > 0 ? document : null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (
            ex is ArgumentException or
            IOException or
            NotSupportedException or
            UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? ReadEmbeddedLyrics(string filePath)
    {
        try
        {
            using var tagFile = TagLib.File.Create(filePath);
            var lyrics = tagFile.Tag?.Lyrics;
            return string.IsNullOrWhiteSpace(lyrics) ? null : lyrics;
        }
        catch (Exception)
        {
            // Embedded lyrics are optional decoration. Damaged or
            // unsupported tags must never interrupt loading or playback.
            return null;
        }
    }
}
