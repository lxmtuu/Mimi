using System.IO;
using System.Text;
using Mimi.Services;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Mimi.Tests;

public sealed class AudioServiceNoOutputTests
{
    [Fact]
    public void ForcedWasapiUnavailable_KeepsDecodedTrackLoadedAndDoesNotUseFallback()
    {
        var wasapiAttempts = 0;
        var waveOutAttempts = 0;

        WithWaveFile(path =>
        {
            using var audio = new AudioService(
                AudioOutputBackendPreference.ForceWasapiShared,
                _ =>
                {
                    wasapiAttempts++;
                    throw new IOException("Simulated WASAPI endpoint failure.");
                },
                () =>
                {
                    waveOutAttempts++;
                    throw new IOException("WaveOut must not be attempted in forced WASAPI mode.");
                },
                useDeviceWatcher: false);

            AudioOutputStatus? reportedStatus = null;
            audio.OutputStatusChanged += (_, args) => reportedStatus = args.Status;

            audio.Load(path);

            Assert.Equal(1, wasapiAttempts);
            Assert.Equal(0, waveOutAttempts);
            Assert.False(audio.OutputStatus.IsAvailable);
            Assert.Equal(AudioOutputKind.None, audio.OutputStatus.Kind);
            Assert.True(audio.Duration > TimeSpan.Zero);
            Assert.False(audio.IsPlaying);
            Assert.NotNull(reportedStatus);
            Assert.False(reportedStatus.Value.IsAvailable);

            var exception = Assert.Throws<AudioOutputUnavailableException>(
                () => audio.Play());

            Assert.False(exception.Status.IsAvailable);
            Assert.Equal(2, wasapiAttempts);
            Assert.Equal(0, waveOutAttempts);
            Assert.True(audio.Duration > TimeSpan.Zero);
            Assert.False(audio.IsPlaying);
        });
    }

    [Fact]
    public void AutomaticBackendUnavailable_KeepsDecodedTrackLoadedAfterBothBackendsFail()
    {
        var wasapiAttempts = 0;
        var waveOutAttempts = 0;

        WithWaveFile(path =>
        {
            using var audio = new AudioService(
                AudioOutputBackendPreference.Automatic,
                _ =>
                {
                    wasapiAttempts++;
                    throw new IOException("Simulated WASAPI endpoint failure.");
                },
                () =>
                {
                    waveOutAttempts++;
                    throw new IOException("Simulated WaveOut endpoint failure.");
                },
                useDeviceWatcher: false);

            audio.Load(path);

            Assert.Equal(1, wasapiAttempts);
            Assert.Equal(1, waveOutAttempts);
            Assert.False(audio.OutputStatus.IsAvailable);
            Assert.True(audio.Duration > TimeSpan.Zero);

            var exception = Assert.Throws<AudioOutputUnavailableException>(
                () => audio.Play());

            Assert.False(exception.Status.IsAvailable);
            Assert.Equal(2, wasapiAttempts);
            Assert.Equal(2, waveOutAttempts);
            Assert.True(audio.Duration > TimeSpan.Zero);
            Assert.False(audio.IsPlaying);
        });
    }

    private static void WithWaveFile(Action<string> action)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"mimi-no-output-{Guid.NewGuid():N}.wav");

        try
        {
            WriteSilentWaveFile(path, sampleRate: 44100, durationMilliseconds: 150);
            action(path);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static void WriteSilentWaveFile(
        string path,
        int sampleRate,
        int durationMilliseconds)
    {
        const short channels = 1;
        const short bitsPerSample = 16;
        var sampleCount = sampleRate * durationMilliseconds / 1000;
        var dataLength = sampleCount * channels * bitsPerSample / 8;
        var byteRate = sampleRate * channels * bitsPerSample / 8;
        var blockAlign = (short)(channels * bitsPerSample / 8);

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.ASCII);

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataLength);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataLength);
        writer.Write(new byte[dataLength]);
    }
}
