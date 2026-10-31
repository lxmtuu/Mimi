using System.Reflection;
using System.Runtime.InteropServices;
using Mimi.Services;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Mimi.Tests;

public sealed class WindowsAudioDeviceNotificationTests
{
    [Fact]
    public void WindowsWatcher_ContainsSubscriberExceptionsAtNotificationBoundary()
    {
        using var watcher = new WindowsAudioDeviceWatcher();
        var field = typeof(WindowsAudioDeviceWatcher).GetField(
            "notificationClient",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var client = Assert.IsType<AudioDeviceNotificationClient>(field!.GetValue(watcher));
        watcher.Changed += (_, _) => throw new InvalidOperationException("subscriber failure");

        var exception = Record.Exception(() => client.OnDeviceAdded("test-device"));

        Assert.Null(exception);
    }

    [Fact]
    public void NotificationClient_UsesExplicitComInterfaceWithoutAutoClassInterface()
    {
        var type = typeof(AudioDeviceNotificationClient);

        Assert.True(type.IsDefined(typeof(ComVisibleAttribute), inherit: false));
        var classInterface = type.GetCustomAttribute<ClassInterfaceAttribute>();
        Assert.NotNull(classInterface);
        Assert.Equal(ClassInterfaceType.None, classInterface!.Value);
        Assert.Contains(typeof(NAudio.CoreAudioApi.Interfaces.IMMNotificationClient),
            type.GetInterfaces());
    }

    [Fact]
    public void NotificationClient_ForwardsRelevantRenderChanges_AndIgnoresOtherRoles()
    {
        var received = new List<AudioDeviceChangedEventArgs>();
        var client = new AudioDeviceNotificationClient(received.Add);

        client.OnDefaultDeviceChanged(DataFlow.Capture, Role.Multimedia, "capture-id");
        client.OnDefaultDeviceChanged(DataFlow.Render, Role.Console, "console-id");
        client.OnDefaultDeviceChanged(DataFlow.Render, Role.Multimedia, "render-id");
        client.OnDeviceAdded("added-id");
        client.OnDeviceRemoved("removed-id");
        client.OnDeviceStateChanged("state-id", DeviceState.Disabled);

        Assert.Equal(5, received.Count);
        Assert.Equal(AudioDeviceChangeKind.DefaultChanged, received[0].Kind);
        Assert.Equal("console-id", received[0].DeviceId);
        Assert.Equal(AudioDeviceChangeKind.DefaultChanged, received[1].Kind);
        Assert.Equal("render-id", received[1].DeviceId);
        Assert.Equal(AudioDeviceChangeKind.DeviceAdded, received[2].Kind);
        Assert.Equal(AudioDeviceChangeKind.DeviceRemoved, received[3].Kind);
        Assert.Equal(AudioDeviceChangeKind.DeviceStateChanged, received[4].Kind);
        Assert.Equal(DeviceState.Disabled, received[4].DeviceState);
    }

    [Fact]
    public void AudioService_DefaultDeviceBurst_IsDebouncedToOneOutputRebuild()
    {
        var first = new CountingWavePlayer();
        var replacement = new CountingWavePlayer();
        var factoryCalls = 0;
        var path = Path.Combine(Path.GetTempPath(), $"mimi-watcher-{Guid.NewGuid():N}.wav");

        try
        {
            WriteSilentWaveFile(path);
            using var audio = new AudioService(
                AudioOutputBackendPreference.ForceWasapiShared,
                _ =>
                {
                    factoryCalls++;
                    return factoryCalls switch
                    {
                        1 => first,
                        2 => replacement,
                        _ => throw new InvalidOperationException("Unexpected output recreation.")
                    };
                },
                () => throw new InvalidOperationException("WaveOut must not be used."),
                useDeviceWatcher: false);

            audio.Load(path);
            Assert.Equal(1, factoryCalls);

            var handler = typeof(AudioService).GetMethod(
                "AudioDeviceWatcher_Changed",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(handler);

            for (var i = 0; i < 5; i++)
            {
                handler!.Invoke(audio, new object?[]
                {
                    null,
                    new AudioDeviceChangedEventArgs(
                        AudioDeviceChangeKind.DefaultChanged,
                        $"default-{i}")
                });
                Thread.Sleep(40);
            }

            Assert.True(SpinWait.SpinUntil(() => factoryCalls == 2, TimeSpan.FromSeconds(3)),
                "The debounced default-device change did not rebuild the output.");
            Thread.Sleep(500);

            Assert.Equal(2, factoryCalls);
            Assert.Equal(1, first.DisposeCount);
            Assert.Equal(AudioOutputKind.WasapiShared, audio.OutputStatus.Kind);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void AudioService_IgnoresQueuedDeviceNotificationsAfterDispose()
    {
        var first = new CountingWavePlayer();
        var factoryCalls = 0;
        var path = Path.Combine(Path.GetTempPath(), $"mimi-watcher-dispose-{Guid.NewGuid():N}.wav");

        try
        {
            WriteSilentWaveFile(path);
            var audio = new AudioService(
                AudioOutputBackendPreference.ForceWasapiShared,
                _ =>
                {
                    factoryCalls++;
                    return first;
                },
                () => throw new InvalidOperationException("WaveOut must not be used."),
                useDeviceWatcher: false);

            audio.Load(path);
            Assert.Equal(1, factoryCalls);

            var handler = typeof(AudioService).GetMethod(
                "AudioDeviceWatcher_Changed",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(handler);

            audio.Dispose();
            audio.Dispose();

            for (var i = 0; i < 100; i++)
            {
                handler!.Invoke(audio, new object?[]
                {
                    null,
                    new AudioDeviceChangedEventArgs(
                        AudioDeviceChangeKind.DefaultChanged,
                        $"late-default-{i}")
                });
            }

            Thread.Sleep(400);

            Assert.Equal(1, factoryCalls);
            Assert.Equal(1, first.DisposeCount);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static void WriteSilentWaveFile(string path)
    {
        const int sampleRate = 44100;
        const int durationMilliseconds = 3000;
        const short channels = 1;
        const short bitsPerSample = 16;
        var sampleCount = sampleRate * durationMilliseconds / 1000;
        var dataLength = sampleCount * channels * bitsPerSample / 8;

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataLength);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * bitsPerSample / 8);
        writer.Write((short)(channels * bitsPerSample / 8));
        writer.Write(bitsPerSample);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(dataLength);
        writer.Write(new byte[dataLength]);
    }

    private sealed class CountingWavePlayer : IWavePlayer
    {
        public event EventHandler<StoppedEventArgs>? PlaybackStopped { add { } remove { } }
        public float Volume { get; set; } = 1f;
        public PlaybackState PlaybackState { get; private set; } = PlaybackState.Stopped;
        public WaveFormat OutputWaveFormat { get; private set; } =
            WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        public int DisposeCount { get; private set; }

        public void Init(IWaveProvider waveProvider) => OutputWaveFormat = waveProvider.WaveFormat;
        public void Play() => PlaybackState = PlaybackState.Playing;
        public void Pause() => PlaybackState = PlaybackState.Paused;
        public void Stop() => PlaybackState = PlaybackState.Stopped;
        public void Dispose()
        {
            DisposeCount++;
            PlaybackState = PlaybackState.Stopped;
        }
    }
}
