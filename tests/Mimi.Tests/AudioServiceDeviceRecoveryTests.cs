using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Mimi.Services;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Mimi.Tests;

public sealed class AudioServiceDeviceRecoveryTests
{
    [Fact]
    public void OutputInvalidatedDuringPlayback_RebuildsPausedAndPreservesPosition_UntilExplicitResume()
    {
        WithWaveFile(path =>
        {
            var firstOutput = new FakeWavePlayer();
            var recoveredOutput = new FakeWavePlayer();
            var factoryCalls = 0;

            using var audio = new AudioService(
                AudioOutputBackendPreference.ForceWasapiShared,
                _ =>
                {
                    factoryCalls++;
                    return factoryCalls switch
                    {
                        1 => firstOutput,
                        2 => throw new IOException("Simulated endpoint is disconnected."),
                        3 => recoveredOutput,
                        _ => throw new InvalidOperationException("Unexpected output factory retry.")
                    };
                },
                () => throw new InvalidOperationException(
                    "WaveOut fallback must remain disabled in forced WASAPI mode."),
                useDeviceWatcher: false);

            using var recovered = new ManualResetEventSlim();
            AudioOutputStatusChangedEventArgs? interruption = null;
            audio.OutputStatusChanged += (_, args) =>
            {
                if (args.PlaybackInterrupted)
                {
                    interruption = args;
                    recovered.Set();
                }
            };

            audio.Load(path);
            Assert.Equal(1, factoryCalls);
            Assert.Equal(AudioOutputKind.WasapiShared, audio.OutputStatus.Kind);

            audio.Seek(TimeSpan.FromSeconds(2));
            audio.Play();
            Assert.True(audio.IsPlaying);
            Assert.Equal(PlaybackState.Playing, firstOutput.PlaybackState);

            // The fake output consumes the same IWaveProvider that a real
            // NAudio output consumes, advancing the actual AudioFileReader.
            firstOutput.AdvanceBy(TimeSpan.FromSeconds(1));
            var positionAtFailure = audio.CurrentTime;
            Assert.InRange(
                Math.Abs((positionAtFailure - TimeSpan.FromSeconds(3)).TotalMilliseconds),
                0,
                60);

            // CoreAudio invalidation is surfaced by NAudio as a stopped
            // callback with AUDCLNT_E_DEVICE_INVALIDATED (0x88890004).
            firstOutput.RaiseStopped(new COMException(
                "Simulated render endpoint removal.",
                unchecked((int)0x88890004)));

            Assert.True(
                recovered.Wait(TimeSpan.FromSeconds(5)),
                "AudioService did not publish the playback-interruption recovery status.");

            Assert.Equal(2, factoryCalls);
            Assert.Equal(AudioOutputKind.None, interruption!.Status.Kind);
            Assert.False(interruption.Status.IsAvailable);
            Assert.Contains("playback is paused", interruption.Status.Message);
            Assert.False(audio.OutputStatus.IsAvailable);
            Assert.False(audio.IsPlaying);
            Assert.Equal(PlaybackState.Stopped, recoveredOutput.PlaybackState);
            Assert.InRange(
                Math.Abs((audio.CurrentTime - positionAtFailure).TotalMilliseconds),
                0,
                60);

            // Recovery must not silently resume playback while no endpoint
            // exists. The user explicitly retries from the retained position.
            audio.Play();
            Assert.Equal(3, factoryCalls);
            Assert.True(audio.OutputStatus.IsAvailable);
            Assert.Equal(AudioOutputKind.WasapiShared, audio.OutputStatus.Kind);
            Assert.True(audio.IsPlaying);
            Assert.Equal(PlaybackState.Playing, recoveredOutput.PlaybackState);

            recoveredOutput.AdvanceBy(TimeSpan.FromMilliseconds(500));
            Assert.InRange(
                Math.Abs((audio.CurrentTime - (positionAtFailure + TimeSpan.FromMilliseconds(500))).TotalMilliseconds),
                0,
                80);
        }, durationMilliseconds: 10000);
    }

    private static void WithWaveFile(
        Action<string> action,
        int durationMilliseconds)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"mimi-device-recovery-{Guid.NewGuid():N}.wav");

        try
        {
            WriteSilentWaveFile(path, sampleRate: 44100, durationMilliseconds);
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

    private sealed class FakeWavePlayer : IWavePlayer
    {
        private IWaveProvider? provider;

        public event EventHandler<StoppedEventArgs>? PlaybackStopped;

        public float Volume { get; set; } = 1f;
        public PlaybackState PlaybackState { get; private set; } = PlaybackState.Stopped;
        public WaveFormat OutputWaveFormat =>
            provider?.WaveFormat ?? WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);

        public void Init(IWaveProvider waveProvider) =>
            provider = waveProvider ?? throw new ArgumentNullException(nameof(waveProvider));

        public void Play() => PlaybackState = PlaybackState.Playing;

        public void Pause() => PlaybackState = PlaybackState.Paused;

        public void Stop() => PlaybackState = PlaybackState.Stopped;

        public void AdvanceBy(TimeSpan duration)
        {
            if (provider is null)
                throw new InvalidOperationException("The fake output has not been initialized.");

            var bytesRemaining = (long)Math.Round(
                provider.WaveFormat.AverageBytesPerSecond * duration.TotalSeconds);
            var buffer = new byte[Math.Min(8192, provider.WaveFormat.AverageBytesPerSecond)];

            while (bytesRemaining > 0)
            {
                var requested = (int)Math.Min(buffer.Length, bytesRemaining);
                var read = provider.Read(buffer, 0, requested);
                if (read <= 0)
                    break;

                bytesRemaining -= read;
            }
        }

        public void RaiseStopped(Exception exception)
        {
            PlaybackState = PlaybackState.Stopped;
            PlaybackStopped?.Invoke(this, new StoppedEventArgs(exception));
        }

        public void Dispose()
        {
            PlaybackState = PlaybackState.Stopped;
            provider = null;
            PlaybackStopped = null;
        }
    }
}
