using Mimi.Services;

namespace Mimi.Tests;

public sealed class TagLibTrackMetadataReaderTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        "MimiMetadataTests-" + Guid.NewGuid().ToString("N"));
    private readonly TagLibTrackMetadataReader reader = new();

    public TagLibTrackMetadataReaderTests() => Directory.CreateDirectory(root);

    [Fact]
    public void CreateTrack_ForDamagedFile_FallsBackToFileName()
    {
        var path = Path.Combine(root, "Damaged Song.mp3");
        File.WriteAllBytes(path, [1, 2, 3]);

        var track = reader.CreateTrack(path);

        Assert.Equal("Damaged Song", track.Title);
        Assert.Equal("filename", track.MetadataSource);
        Assert.False(track.IsMissing);
        Assert.Equal(3, track.FileSizeBytes);
        Assert.NotNull(track.FileModifiedUtc);
    }

    [Fact]
    public void CreateTrack_ForMissingFile_MarksTrackMissing()
    {
        var path = Path.Combine(root, "not-here.flac");

        var track = reader.CreateTrack(path);

        Assert.Equal("not-here", track.Title);
        Assert.True(track.IsMissing);
    }

    [Fact]
    public void CreateTrack_ReadsTagsAndAudioPropertiesFromWaveFile()
    {
        var path = Path.Combine(root, "wave-entry.wav");
        WriteWaveFile(path);

        using (var tagFile = TagLib.File.Create(path))
        {
            tagFile.Tag.Title = "Tagged Song";
            tagFile.Tag.Performers = ["Mimi Artist"];
            tagFile.Tag.Album = "Neon Album";
            tagFile.Tag.Genres = ["Synth"];
            tagFile.Tag.Year = 2026;
            tagFile.Save();
        }

        var track = reader.CreateTrack(path);

        Assert.Equal("Tagged Song", track.Title);
        Assert.Equal("Mimi Artist", track.Artist);
        Assert.Equal("Neon Album", track.Album);
        Assert.Equal("Synth", track.Genre);
        Assert.Equal(2026, track.Year);
        Assert.Equal(8000, track.SampleRateHz);
        Assert.NotNull(track.Duration);
        Assert.InRange(track.Duration.Value.TotalSeconds, 0.9, 1.1);
        Assert.Equal("tag", track.MetadataSource);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private static void WriteWaveFile(string path)
    {
        const int sampleRate = 8000;
        const int sampleCount = 8000;
        const short channelCount = 1;
        const short bitsPerSample = 16;
        var data = new byte[sampleCount * sizeof(short)];

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF".ToCharArray());
        writer.Write(36 + data.Length);
        writer.Write("WAVE".ToCharArray());
        writer.Write("fmt ".ToCharArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channelCount);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channelCount * sizeof(short));
        writer.Write((short)(channelCount * sizeof(short)));
        writer.Write(bitsPerSample);
        writer.Write("data".ToCharArray());
        writer.Write(data.Length);
        writer.Write(data);
    }
}
