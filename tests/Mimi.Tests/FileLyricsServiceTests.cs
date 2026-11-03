using Mimi.Lyrics;
using Mimi.Models;
using Mimi.Services;

namespace Mimi.Tests;

public sealed class FileLyricsServiceTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        "MimiLyricsTests-" + Guid.NewGuid().ToString("N"));

    public FileLyricsServiceTests() => Directory.CreateDirectory(root);

    [Fact]
    public async Task LoadAsyncPrefersSameNameSidecarOverEmbeddedLyrics()
    {
        var audioPath = CreateAudioFile("song.mp3");
        await File.WriteAllTextAsync(
            Path.ChangeExtension(audioPath, ".lrc"),
            "[00:01.00]Sidecar line");
        var service = new FileLyricsService(_ => "Embedded line");

        var document = await service.LoadAsync(
            new Track { FilePath = audioPath });

        Assert.NotNull(document);
        Assert.Equal(LyricsSourceKind.SidecarFile, document.Source);
        Assert.Equal("Sidecar line", Assert.Single(document.Lines).Text);
        Assert.True(document.IsSynchronized);
    }

    [Fact]
    public async Task LoadAsyncFallsBackToEmbeddedLyrics()
    {
        var audioPath = CreateAudioFile("embedded.flac");
        var service = new FileLyricsService(_ => "Embedded first\nEmbedded second");

        var document = await service.LoadAsync(
            new Track { FilePath = audioPath });

        Assert.NotNull(document);
        Assert.Equal(LyricsSourceKind.EmbeddedTag, document.Source);
        Assert.Equal(2, document.Lines.Count);
        Assert.False(document.IsSynchronized);
    }

    [Fact]
    public async Task LoadAsyncIgnoresEmptySidecarAndUsesEmbeddedLyrics()
    {
        var audioPath = CreateAudioFile("empty.wav");
        await File.WriteAllTextAsync(
            Path.ChangeExtension(audioPath, ".lrc"),
            string.Empty);
        var service = new FileLyricsService(_ => "Still available");

        var document = await service.LoadAsync(
            new Track { FilePath = audioPath });

        Assert.NotNull(document);
        Assert.Equal(LyricsSourceKind.EmbeddedTag, document.Source);
        Assert.Equal("Still available", Assert.Single(document.Lines).Text);
    }

    [Fact]
    public async Task LoadAsyncReturnsNullWhenNoLyricsExist()
    {
        var audioPath = CreateAudioFile("quiet.ogg");
        var service = new FileLyricsService(_ => null);

        var document = await service.LoadAsync(
            new Track { FilePath = audioPath });

        Assert.Null(document);
    }

    private string CreateAudioFile(string fileName)
    {
        var path = Path.Combine(root, fileName);
        File.WriteAllBytes(path, [1, 2, 3]);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
