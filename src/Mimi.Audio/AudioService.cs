using System.Diagnostics;
using System.Runtime.InteropServices;
using Mimi.Core.Playback;
using NAudio;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Serilog;

namespace Mimi.Services;

/// <summary>
/// Local-file playback engine based on WASAPI shared mode. The output
/// follows the Windows default render endpoint; if that endpoint changes or
/// disappears, playback pauses and only the output stream is rebuilt around
/// the already-decoded reader. WaveOut remains a conservative fallback when
/// WASAPI cannot be opened. Online providers should be added as separate,
/// officially authorized adapters; never scrape or bypass provider access
/// controls.
/// </summary>
public sealed class AudioService : IAudioPlayer
{
    private const int WasapiLatencyMilliseconds = 100;
    private const int WaveOutLatencyMilliseconds = 200;
    private const int DeviceChangeDebounceMilliseconds = 300;
    private const int DeviceInvalidatedHResult = unchecked((int)0x88890004);
    private const string BackendPreferenceEnvironmentVariable = "MIMI_AUDIO_OUTPUT";

    private readonly object gate = new();
    private readonly AudioOutputBackendPreference backendPreference;
    private readonly Func<MMDevice?, IWavePlayer> wasapiOutputFactory;
    private readonly Func<IWavePlayer> waveOutOutputFactory;
    private readonly SynchronizationContext? synchronizationContext;
    private readonly WindowsAudioDeviceWatcher? deviceWatcher;
    private readonly System.Threading.Timer deviceChangeTimer;

    private volatile IWavePlayer? output;
    private MMDevice? outputDevice;
    private volatile string? outputDeviceId;
    private AudioFileReader? reader;
    private VisualizerSampleProvider? visualizer;
    private SwitchableSampleProvider? switchableSource;
    private VolumeSampleProvider? volumeProvider;
    private string? loadedPath;
    private PlaybackStoppedSubscription? playbackStoppedSubscription;
    private float volume = 0.7f;
    private AudioOutputStatus outputStatus;
    private volatile bool hasAvailableOutput;
    private volatile bool outputBroken;
    private bool wasapiFailureLogged;
    private long playbackGeneration;
    private int deviceChangeVersion;
    private int pendingForceOutputRebuild;
    private int pendingPlaybackInterruption;
    private volatile bool disposed;

    public AudioService()
        : this(
            ReadBackendPreference(),
            CreateWasapiOutput,
            CreateWaveOutOutput,
            useDeviceWatcher: true)
    {
    }

    // Internal dependency seam for deterministic no-output integration tests.
    // Production always uses the public parameterless constructor above.
    internal AudioService(
        AudioOutputBackendPreference backendPreference,
        Func<MMDevice?, IWavePlayer> wasapiOutputFactory,
        Func<IWavePlayer> waveOutOutputFactory,
        bool useDeviceWatcher)
    {
        this.backendPreference = backendPreference;
        this.wasapiOutputFactory = wasapiOutputFactory ??
            throw new ArgumentNullException(nameof(wasapiOutputFactory));
        this.waveOutOutputFactory = waveOutOutputFactory ??
            throw new ArgumentNullException(nameof(waveOutOutputFactory));
        synchronizationContext = SynchronizationContext.Current;
        deviceChangeTimer = new System.Threading.Timer(
            DeviceChangeTimerElapsed,
            null,
            Timeout.Infinite,
            Timeout.Infinite);

        WindowsAudioDeviceWatcher? watcher = null;
        if (useDeviceWatcher)
        {
            try
            {
                watcher = new WindowsAudioDeviceWatcher();
                watcher.Changed += AudioDeviceWatcher_Changed;
            }
            catch (Exception ex)
            {
                Log.Warning(
                    ex,
                    "Could not initialize Windows audio-device monitoring; " +
                    "playback will use the output available when a song starts");
                watcher?.Dispose();
                watcher = null;
            }
        }

        deviceWatcher = watcher;
        outputStatus = CreateDefaultOutputStatus();
        hasAvailableOutput = outputStatus.IsAvailable;
    }

    public event EventHandler? PlaybackChanged;
    public event EventHandler<PlaybackEndedEventArgs>? PlaybackEnded;
    public event EventHandler<AudioOutputStatusChangedEventArgs>? OutputStatusChanged;

    public long PlaybackGeneration
    {
        get
        {
            lock (gate)
                return playbackGeneration;
        }
    }

    public VisualizerFrame GetVisualizerFrame() =>
        visualizer?.GetFrame() ?? VisualizerFrame.Silence;

    public float[] GetVisualizerLevels()
    {
        var spectrum = GetVisualizerFrame().Spectrum;
        var levels = new float[spectrum.Count];
        for (var i = 0; i < spectrum.Count; i++)
            levels[i] = spectrum[i];
        return levels;
    }

    public string GetVisualizerDiagnostics()
    {
        var status = OutputStatus;
        var visualizerDiagnostics = visualizer?.GetDiagnostics() ??
            "visualizer=not-initialized";
        return $"{visualizerDiagnostics}; output={status.Kind}; " +
               $"backendPreference={backendPreference}; " +
               $"device={status.DeviceName}; available={status.IsAvailable}";
    }

    public bool IsPlaying
    {
        get
        {
            lock (gate)
                return IsPlayingLocked;
        }
    }

    public TimeSpan CurrentTime
    {
        get
        {
            lock (gate)
                return reader?.CurrentTime ?? TimeSpan.Zero;
        }
    }

    public TimeSpan Duration
    {
        get
        {
            lock (gate)
                return reader?.TotalTime ?? TimeSpan.Zero;
        }
    }

    public AudioOutputStatus OutputStatus
    {
        get
        {
            lock (gate)
                return outputStatus;
        }
    }

    private bool IsPlayingLocked =>
        output?.PlaybackState == PlaybackState.Playing;

    private void AdvancePlaybackGenerationLocked() =>
        playbackGeneration = unchecked(playbackGeneration + 1);

    private sealed class PlaybackStoppedSubscription(IWavePlayer player)
    {
        public IWavePlayer Player { get; } = player;

        public EventHandler<StoppedEventArgs>? Handler { get; set; }
    }

    public void Load(string path)
    {
        AudioOutputStatusChangedEventArgs? statusChanged = null;
        var loadStarted = Stopwatch.GetTimestamp();
        var decodeElapsed = TimeSpan.Zero;
        var transitionElapsed = TimeSpan.Zero;
        var outputCreateElapsed = TimeSpan.Zero;
        var reusedOutput = false;

        lock (gate)
        {
            ThrowIfDisposedLocked();
            AdvancePlaybackGenerationLocked();

            // Opening/decoding the next file does not require the current
            // reader. For a different path, build the candidate chain while
            // the old track is still intact, then stop/swap only at the
            // last moment. A failed candidate still ends in the historical
            // "no track loaded" state via the catch block below.
            var buildBeforeStoppingCurrent = reader is not null &&
                !string.Equals(
                    loadedPath,
                    path,
                    StringComparison.OrdinalIgnoreCase);

            AudioFileReader? newReader = null;
            VisualizerSampleProvider? newVisualizer = null;
            try
            {
                if (!buildBeforeStoppingCurrent)
                {
                    var preDecodeTransitionStarted = Stopwatch.GetTimestamp();
                    if (reader is not null &&
                        TryStopOutputForTrackSwapLocked())
                    {
                        // Retain output/switchable/volume as a same-format
                        // reuse candidate while releasing the old reader
                        // before reopening the same path.
                        switchableSource?.ClearSource();
                        DisposeTrackChainLocked(reader, visualizer);
                        reader = null;
                        visualizer = null;
                        loadedPath = null;
                    }
                    else
                    {
                        DisposeLoadedTrackLocked();
                    }
                    transitionElapsed += Stopwatch.GetElapsedTime(
                        preDecodeTransitionStarted);
                }

                var decodeStarted = Stopwatch.GetTimestamp();
                newReader = new AudioFileReader(path);

                // Keep the reader at unity gain and apply volume AFTER the
                // visualizer tap, so the spectrum reflects the music itself,
                // not the volume slider.
                newReader.Volume = 1.0f;
                newVisualizer = new VisualizerSampleProvider(newReader);

                // WASAPI shared mode auto-converts sample rates, but a
                // stereo float stream is the most widely accepted shape for
                // render endpoints. Mono files are therefore up-mixed after
                // the visualizer tap; stereo and multichannel files retain
                // their decoded layout for the Windows audio engine.
                ISampleProvider newOutputSource =
                    newVisualizer.WaveFormat.Channels == 1
                        ? newVisualizer.ToStereo()
                        : newVisualizer;
                decodeElapsed = Stopwatch.GetElapsedTime(decodeStarted);

                reusedOutput = CanReuseOutputLocked(newOutputSource.WaveFormat);
                var transitionStarted = Stopwatch.GetTimestamp();
                if (reusedOutput && buildBeforeStoppingCurrent)
                {
                    reusedOutput = TryStopOutputForTrackSwapLocked();
                }

                if (reusedOutput)
                {
                    var oldReader = reader;
                    var oldVisualizer = visualizer;

                    switchableSource!.SetSource(newOutputSource);
                    reader = newReader;
                    visualizer = newVisualizer;
                    loadedPath = path;
                    newReader = null;
                    newVisualizer = null;

                    DisposeTrackChainLocked(oldReader, oldVisualizer);
                    transitionElapsed +=
                        Stopwatch.GetElapsedTime(transitionStarted);
                }
                else
                {
                    DisposeLoadedTrackLocked();

                    reader = newReader;
                    visualizer = newVisualizer;
                    loadedPath = path;
                    newReader = null;
                    newVisualizer = null;
                    switchableSource = new SwitchableSampleProvider(newOutputSource);
                    volumeProvider = new VolumeSampleProvider(switchableSource)
                    {
                        Volume = volume
                    };
                    transitionElapsed +=
                        Stopwatch.GetElapsedTime(transitionStarted);

                    // A missing output must not turn a successfully decoded
                    // file into a track error. The reader remains loaded and
                    // paused; Play reports the output problem distinctly.
                    var outputCreateStarted = Stopwatch.GetTimestamp();
                    TryCreateOutputLocked(out statusChanged);
                    outputCreateElapsed =
                        Stopwatch.GetElapsedTime(outputCreateStarted);
                }
            }
            catch
            {
                DisposeLoadedTrackLocked();
                DisposeTrackChainLocked(newReader, newVisualizer);
                throw;
            }
        }

        var totalElapsed = Stopwatch.GetElapsedTime(loadStarted);
        if (totalElapsed.TotalMilliseconds >= 50)
        {
            Log.Information(
                "Audio load timing for {FilePath}: total={TotalMs:F1}ms; " +
                "decode={DecodeMs:F1}ms; transition={TransitionMs:F1}ms; " +
                "outputCreate={OutputCreateMs:F1}ms; reusedOutput={ReusedOutput}",
                path,
                totalElapsed.TotalMilliseconds,
                decodeElapsed.TotalMilliseconds,
                transitionElapsed.TotalMilliseconds,
                outputCreateElapsed.TotalMilliseconds,
                reusedOutput);
        }

        RaiseOutputStatusChanged(statusChanged);
        PlaybackChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Output_PlaybackStopped(
        object? sender,
        StoppedEventArgs e,
        PlaybackStoppedSubscription subscription)
    {
        var endedNaturally = false;
        var endedGeneration = 0L;
        var endedPosition = TimeSpan.Zero;
        var endedDuration = TimeSpan.Zero;
        var scheduleRecovery = false;
        var playbackInterrupted = false;

        lock (gate)
        {
            // A stopped callback can be queued before a same-output track
            // swap replaces the subscription. The captured subscription
            // makes that stale callback distinguishable even though the
            // IWavePlayer instance is intentionally reused.
            if (disposed || !ReferenceEquals(sender, output) ||
                !ReferenceEquals(subscription, playbackStoppedSubscription))
            {
                return;
            }

            if (e.Exception is not null)
            {
                outputBroken = true;
                scheduleRecovery = true;
                playbackInterrupted = IsLikelyOutputFailure(e.Exception);
                Log.Warning(
                    e.Exception,
                    "Audio output {OutputKind} stopped unexpectedly on {DeviceName}; " +
                    "scheduling a paused output rebuild",
                    outputStatus.Kind,
                    outputStatus.DeviceName);
            }
            else
            {
                endedNaturally = PlaybackStopClassifier.IsNaturalEnd(
                    hasException: false,
                    reader?.Length);
                if (endedNaturally)
                {
                    // Capture the ending run while the reader/output pair
                    // is still the one that stopped. Another Load may occur
                    // before subscribers finish handling the event.
                    endedGeneration = playbackGeneration;
                    endedPosition = reader!.CurrentTime;
                    endedDuration = reader.TotalTime;
                }
            }
        }

        PlaybackChanged?.Invoke(this, EventArgs.Empty);
        if (endedNaturally)
        {
            Log.Information(
                "Audio playback reached end at {Position}/{Duration} seconds",
                endedPosition.TotalSeconds,
                endedDuration.TotalSeconds);
            PlaybackEnded?.Invoke(
                this,
                new PlaybackEndedEventArgs(endedGeneration));
        }

        if (scheduleRecovery)
        {
            ScheduleOutputRebuild(
                force: true,
                playbackInterrupted: playbackInterrupted);
        }
    }

    private bool CanReuseOutputLocked(WaveFormat newFormat)
    {
        var source = switchableSource;
        return output is not null &&
               source is not null &&
               volumeProvider is not null &&
               !outputBroken &&
               outputStatus.IsAvailable &&
               WaveFormatsMatch(source.WaveFormat, newFormat);
    }

    private bool TryStopOutputForTrackSwapLocked()
    {
        if (output is null || switchableSource is null ||
            volumeProvider is null)
        {
            return false;
        }

        UnsubscribePlaybackStoppedLocked(output);
        try
        {
            output.Stop();
            return true;
        }
        catch (Exception ex)
        {
            outputBroken = true;
            Log.Warning(
                ex,
                "Audio output could not stop cleanly for a track swap on {DeviceName}; rebuilding it",
                outputStatus.DeviceName);
            return false;
        }
        finally
        {
            if (!disposed && output is not null)
                SubscribePlaybackStoppedLocked(output);
        }
    }

    private void SubscribePlaybackStoppedLocked(IWavePlayer player)
    {
        UnsubscribePlaybackStoppedLocked(player);

        var subscription = new PlaybackStoppedSubscription(player);
        subscription.Handler = (sender, e) =>
            Output_PlaybackStopped(sender, e, subscription);
        playbackStoppedSubscription = subscription;
        player.PlaybackStopped += subscription.Handler;
    }

    private void UnsubscribePlaybackStoppedLocked(IWavePlayer player)
    {
        var subscription = playbackStoppedSubscription;
        if (subscription is null ||
            !ReferenceEquals(subscription.Player, player))
        {
            return;
        }

        if (subscription.Handler is not null)
            player.PlaybackStopped -= subscription.Handler;

        playbackStoppedSubscription = null;
    }

    private static bool WaveFormatsMatch(WaveFormat left, WaveFormat right) =>
        left.Encoding == right.Encoding &&
        left.SampleRate == right.SampleRate &&
        left.Channels == right.Channels &&
        left.BitsPerSample == right.BitsPerSample &&
        left.BlockAlign == right.BlockAlign &&
        left.AverageBytesPerSecond == right.AverageBytesPerSecond;

    public void Play()
    {
        AudioOutputStatusChangedEventArgs? statusChanged = null;
        AudioOutputUnavailableException? failure = null;
        var raisePlaybackChanged = false;

        lock (gate)
        {
            ThrowIfDisposedLocked();
            if (reader is null || volumeProvider is null)
                return;

            if (output is null || outputBroken)
            {
                if (!TryCreateOutputLocked(out statusChanged))
                {
                    failure = new AudioOutputUnavailableException(outputStatus);
                    raisePlaybackChanged = true;
                }
            }

            if (failure is null && output is not null)
            {
                try
                {
                    output.Play();
                    outputBroken = false;
                    raisePlaybackChanged = true;
                }
                catch (Exception firstException)
                {
                    Log.Warning(
                        firstException,
                        "Audio output could not start on {DeviceName}; " +
                        "rebuilding it once",
                        outputStatus.DeviceName);
                    outputBroken = true;

                    if (TryCreateOutputLocked(out var retryStatusChanged))
                    {
                        statusChanged = retryStatusChanged ?? statusChanged;
                        try
                        {
                            output!.Play();
                            outputBroken = false;
                            raisePlaybackChanged = true;
                        }
                        catch (Exception secondException)
                        {
                            outputBroken = true;
                            failure = new AudioOutputUnavailableException(
                                outputStatus,
                                secondException);
                            raisePlaybackChanged = true;
                        }
                    }
                    else
                    {
                        statusChanged = retryStatusChanged ?? statusChanged;
                        failure = new AudioOutputUnavailableException(
                            outputStatus,
                            firstException);
                        raisePlaybackChanged = true;
                    }
                }
            }
        }

        RaiseOutputStatusChanged(statusChanged);
        if (raisePlaybackChanged)
            PlaybackChanged?.Invoke(this, EventArgs.Empty);

        if (failure is not null)
            throw failure;
    }

    public void Pause()
    {
        var scheduleRecovery = false;

        lock (gate)
        {
            if (disposed) return;

            try
            {
                output?.Pause();
            }
            catch (Exception ex)
            {
                outputBroken = true;
                scheduleRecovery = IsLikelyOutputFailure(ex);
                Log.Warning(
                    ex,
                    "Audio output could not pause cleanly on {DeviceName}",
                    outputStatus.DeviceName);
            }
        }

        if (scheduleRecovery)
            ScheduleOutputRebuild(force: true);

        PlaybackChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Toggle()
    {
        if (IsPlaying) Pause(); else Play();
    }

    public void Stop()
    {
        var scheduleRecovery = false;

        lock (gate)
        {
            if (disposed) return;

            if (output is not null)
            {
                // Unsubscribe first, so an explicit stop can never be
                // mistaken for a track ending naturally (which would
                // auto-advance to the next song), even when NAudio posts
                // its stopped callback asynchronously.
                UnsubscribePlaybackStoppedLocked(output);
                try
                {
                    output.Stop();
                }
                catch (Exception ex)
                {
                    outputBroken = true;
                    scheduleRecovery = IsLikelyOutputFailure(ex);
                    Log.Warning(
                        ex,
                        "Audio output could not stop cleanly on {DeviceName}",
                        outputStatus.DeviceName);
                }
                finally
                {
                    if (!disposed && output is not null)
                        SubscribePlaybackStoppedLocked(output);
                }
            }

            if (reader is not null)
                reader.Position = 0;

            AdvancePlaybackGenerationLocked();
        }

        if (scheduleRecovery)
            ScheduleOutputRebuild(force: true);

        PlaybackChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Seek(TimeSpan position)
    {
        lock (gate)
        {
            if (reader is null || disposed) return;

            AdvancePlaybackGenerationLocked();
            var maximum = Math.Max(0, reader.TotalTime.TotalSeconds);
            reader.CurrentTime = TimeSpan.FromSeconds(
                Math.Clamp(position.TotalSeconds, 0, maximum));
        }
    }

    public float Volume
    {
        get
        {
            lock (gate)
                return volume;
        }
        set
        {
            lock (gate)
            {
                volume = Math.Clamp(value, 0f, 1f);
                if (volumeProvider is not null)
                    volumeProvider.Volume = volume;
            }
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
        }

        try
        {
            deviceChangeTimer.Change(
                Timeout.Infinite,
                Timeout.Infinite);
        }
        catch (ObjectDisposedException)
        {
        }

        if (deviceWatcher is not null)
        {
            deviceWatcher.Changed -= AudioDeviceWatcher_Changed;
            deviceWatcher.Dispose();
        }

        lock (gate)
            DisposeLoadedTrackLocked();

        deviceChangeTimer.Dispose();
    }

    private bool TryCreateOutputLocked(
        out AudioOutputStatusChangedEventArgs? statusChanged)
    {
        statusChanged = null;
        if (reader is null || volumeProvider is null)
        {
            statusChanged = UpdateOutputStatusLocked(
                CreateDefaultOutputStatus());
            return false;
        }

        var previousStatus = outputStatus;
        DisposeOutputLocked();

        Exception? wasapiException = null;
        MMDevice? device = null;
        IWavePlayer? candidate = null;
        string? deviceId = null;
        var deviceName = "Windows audio output";

        try
        {
            if (deviceWatcher is not null &&
                deviceWatcher.TryGetDefaultRenderDevice(out device))
            {
                deviceId = GetDeviceId(device);
                deviceName = GetDeviceName(device);

                if (backendPreference != AudioOutputBackendPreference.ForceWaveOut)
                {
                    candidate = wasapiOutputFactory(device);
                }
            }
            else if (backendPreference != AudioOutputBackendPreference.ForceWaveOut &&
                     deviceWatcher is null)
            {
                candidate = wasapiOutputFactory(null);
            }

            if (candidate is not null)
            {
                candidate.Init(volumeProvider);
                SubscribePlaybackStoppedLocked(candidate);

                output = candidate;
                candidate = null;
                outputDevice = device;
                device = null;
                outputDeviceId = deviceId;
                outputBroken = false;
                wasapiFailureLogged = false;

                var status = new AudioOutputStatus(
                    true,
                    AudioOutputKind.WasapiShared,
                    deviceName,
                    backendPreference == AudioOutputBackendPreference.ForceWasapiShared
                        ? $"WASAPI shared output is ready (forced by {BackendPreferenceEnvironmentVariable} for diagnostics)."
                        : "WASAPI shared output is ready.",
                    backendPreference == AudioOutputBackendPreference.ForceWasapiShared
                        ? AudioOutputMessageKind.WasapiForcedReady
                        : AudioOutputMessageKind.WasapiReady);
                statusChanged = UpdateOutputStatusLocked(status);

                if (previousStatus.Kind != status.Kind ||
                    !string.Equals(
                        previousStatus.DeviceName,
                        status.DeviceName,
                        StringComparison.Ordinal))
                {
                    Log.Information(
                        "Audio output opened with WASAPI shared mode on {DeviceName}",
                        status.DeviceName);
                }

                return true;
            }

            wasapiException = new InvalidOperationException(
                backendPreference == AudioOutputBackendPreference.ForceWaveOut
                    ? $"WASAPI was skipped because {BackendPreferenceEnvironmentVariable} forces WaveOut for diagnostics."
                    : "No default WASAPI render endpoint is available.");
        }
        catch (Exception ex)
        {
            wasapiException = ex;
            if (!wasapiFailureLogged)
            {
                wasapiFailureLogged = true;
                Log.Warning(
                    ex,
                    backendPreference == AudioOutputBackendPreference.ForceWasapiShared
                        ? "Forced WASAPI shared output was unavailable for {DeviceName}; WaveOut is disabled by the diagnostic backend override"
                        : "WASAPI shared output was unavailable for {DeviceName}; trying the WaveOut fallback",
                    deviceName);
            }
            else
            {
                Log.Debug(
                    ex,
                    "WASAPI shared output remains unavailable for {DeviceName}",
                    deviceName);
            }
        }
        finally
        {
            if (candidate is not null)
            {
                UnsubscribePlaybackStoppedLocked(candidate);
                TryDisposeOutput(candidate);
            }

            device?.Dispose();
        }

        if (backendPreference != AudioOutputBackendPreference.ForceWasapiShared)
        {
            IWavePlayer? fallbackCandidate = null;
            try
            {
                fallbackCandidate = waveOutOutputFactory();
                fallbackCandidate.Init(volumeProvider);
                SubscribePlaybackStoppedLocked(fallbackCandidate);

                output = fallbackCandidate;
                fallbackCandidate = null;
                outputDevice = null;
                outputDeviceId = null;
                outputBroken = false;

                var status = new AudioOutputStatus(
                    true,
                    backendPreference == AudioOutputBackendPreference.ForceWaveOut
                        ? AudioOutputKind.WaveOutForced
                        : AudioOutputKind.WaveOutFallback,
                    deviceName,
                    backendPreference == AudioOutputBackendPreference.ForceWaveOut
                        ? $"WaveOut output is ready (forced by {BackendPreferenceEnvironmentVariable} for diagnostics)."
                        : "WASAPI was unavailable, so Mimi is using the WaveOut fallback.",
                    backendPreference == AudioOutputBackendPreference.ForceWaveOut
                        ? AudioOutputMessageKind.WaveOutForcedReady
                        : AudioOutputMessageKind.WaveOutFallbackReady);
                statusChanged = UpdateOutputStatusLocked(status);

                if (previousStatus.Kind != status.Kind)
                {
                    Log.Information(
                        backendPreference == AudioOutputBackendPreference.ForceWaveOut
                            ? "Audio output opened with WaveOut because it was forced for diagnostics"
                            : "Audio output opened with the WaveOut fallback");
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "The WaveOut fallback could not be opened");
                if (wasapiException is not null)
                    Log.Debug(wasapiException, "The preceding WASAPI attempt also failed");
            }
            finally
            {
                if (fallbackCandidate is not null)
                {
                    UnsubscribePlaybackStoppedLocked(fallbackCandidate);
                    TryDisposeOutput(fallbackCandidate);
                }
            }
        }

        output = null;
        outputDevice = null;
        outputDeviceId = null;
        outputBroken = false;

        var unavailableStatus = AudioOutputStatus.Unavailable(
            backendPreference == AudioOutputBackendPreference.ForceWasapiShared
                ? $"WASAPI shared output is unavailable, and {BackendPreferenceEnvironmentVariable} disables the WaveOut fallback for diagnostics."
                : "No audio output is available. Connect or enable an output " +
                  "device, then press Play.",
            backendPreference == AudioOutputBackendPreference.ForceWasapiShared
                ? AudioOutputMessageKind.WasapiUnavailableFallbackDisabled
                : AudioOutputMessageKind.NoOutputPressPlay);
        statusChanged = UpdateOutputStatusLocked(unavailableStatus);

        if (previousStatus.IsAvailable)
        {
            Log.Warning(
                "No usable Windows audio output is currently available");
        }

        return false;
    }

    private void AudioDeviceWatcher_Changed(
        object? sender,
        AudioDeviceChangedEventArgs e)
    {
        if (disposed) return;

        var isCurrentDevice = !string.IsNullOrWhiteSpace(e.DeviceId) &&
            string.Equals(
                e.DeviceId,
                outputDeviceId,
                StringComparison.OrdinalIgnoreCase);
        var relevant = e.Kind switch
        {
            AudioDeviceChangeKind.DefaultChanged => true,
            AudioDeviceChangeKind.DeviceAdded =>
                !hasAvailableOutput || output is null || outputBroken,
            AudioDeviceChangeKind.DeviceRemoved => isCurrentDevice,
            AudioDeviceChangeKind.DeviceStateChanged => isCurrentDevice,
            _ => false
        };
        if (!relevant) return;

        Log.Debug(
            "Windows audio-device notification {Kind} for {DeviceId} ({State})",
            e.Kind,
            e.DeviceId,
            e.DeviceState);
        // A default-role notification is not forced: the rebuild pass
        // compares the actual Multimedia endpoint and returns without
        // pausing when it is unchanged. Removal/state loss for the device
        // backing the live stream is forced because that stream can no
        // longer be trusted even if a racing default query still names it.
        ScheduleOutputRebuild(
            force: e.Kind is AudioDeviceChangeKind.DeviceRemoved or
                AudioDeviceChangeKind.DeviceStateChanged);
    }

    private void ScheduleOutputRebuild(
        bool force,
        bool playbackInterrupted = false)
    {
        if (disposed) return;

        if (force)
            Interlocked.Exchange(ref pendingForceOutputRebuild, 1);
        if (playbackInterrupted)
            Interlocked.Exchange(ref pendingPlaybackInterruption, 1);

        Interlocked.Increment(ref deviceChangeVersion);
        try
        {
            deviceChangeTimer.Change(
                DeviceChangeDebounceMilliseconds,
                Timeout.Infinite);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void DeviceChangeTimerElapsed(object? state)
    {
        var version = Volatile.Read(ref deviceChangeVersion);
        try
        {
            if (synchronizationContext is not null)
            {
                synchronizationContext.Post(
                    _ => RebuildOutputAfterDeviceChange(version),
                    null);
            }
            else
            {
                ThreadPool.QueueUserWorkItem(
                    _ => RebuildOutputAfterDeviceChange(version));
            }
        }
        catch (Exception ex)
        {
            Log.Debug(
                ex,
                "Could not schedule the audio-output rebuild");
        }
    }

    private void RebuildOutputAfterDeviceChange(int version)
    {
        AudioOutputStatusChangedEventArgs? statusChanged = null;
        var raisePlaybackChanged = false;

        lock (gate)
        {
            if (disposed || version != Volatile.Read(ref deviceChangeVersion))
                return;

            var force = Interlocked.Exchange(
                ref pendingForceOutputRebuild,
                0) != 0;
            var pendingInterruption = Interlocked.Exchange(
                ref pendingPlaybackInterruption,
                0) != 0;

            if (reader is null || volumeProvider is null)
            {
                statusChanged = UpdateOutputStatusLocked(
                    CreateDefaultOutputStatus());
            }
            else
            {
                _ = TryGetDefaultDeviceId(out var defaultDeviceId);
                var outputUsesDefault = output is WasapiOut &&
                    !string.IsNullOrWhiteSpace(outputDeviceId) &&
                    !string.IsNullOrWhiteSpace(defaultDeviceId) &&
                    string.Equals(
                        outputDeviceId,
                        defaultDeviceId,
                        StringComparison.OrdinalIgnoreCase);
                var shouldRebuild = force ||
                    outputBroken ||
                    output is null ||
                    !outputStatus.IsAvailable ||
                    output is not WasapiOut ||
                    !outputUsesDefault;

                if (!shouldRebuild)
                    return;

                var wasPlaying = IsPlayingLocked || pendingInterruption;
                var position = reader.CurrentTime;
                var previousDeviceName = outputStatus.DeviceName;

                // A new default endpoint may be speakers after headphones
                // were removed. Never move sound there automatically: stop
                // only the output, restore the reader position, and leave
                // the rebuilt stream paused for an explicit Play press.
                wasapiFailureLogged = false;
                var created = TryCreateOutputLocked(out statusChanged);

                try
                {
                    var duration = reader.TotalTime;
                    var target = position;
                    if (target < TimeSpan.Zero)
                        target = TimeSpan.Zero;
                    if (duration > TimeSpan.Zero && target > duration)
                        target = duration;
                    reader.CurrentTime = target;
                }
                catch (Exception ex)
                {
                    Log.Debug(
                        ex,
                        "Could not restore the playback position after an output rebuild");
                }

                if (wasPlaying)
                {
                    var message = created
                        ? $"Playback was paused because the audio output changed or stopped. " +
                          $"Current output: {outputStatus.DeviceName}. Press Play to continue."
                        : "The audio output became unavailable, so playback is paused. " +
                          "Connect an output device, then press Play.";
                    var messageKind = created
                        ? AudioOutputMessageKind.PlaybackInterruptedOutputChanged
                        : AudioOutputMessageKind.PlaybackInterruptedOutputUnavailable;
                    var interruptedStatus = new AudioOutputStatus(
                        outputStatus.IsAvailable,
                        outputStatus.Kind,
                        outputStatus.DeviceName,
                        message,
                        messageKind);
                    statusChanged = UpdateOutputStatusLocked(
                        interruptedStatus,
                        playbackInterrupted: true,
                        force: true);
                }

                raisePlaybackChanged = true;
                Log.Information(
                    "Audio output rebuilt after a device change. " +
                    "Previous={PreviousDevice}; Current={CurrentDevice}; " +
                    "Kind={OutputKind}; WasPlaying={WasPlaying}",
                    previousDeviceName,
                    outputStatus.DeviceName,
                    outputStatus.Kind,
                    wasPlaying);
            }
        }

        RaiseOutputStatusChanged(statusChanged);
        if (raisePlaybackChanged)
            PlaybackChanged?.Invoke(this, EventArgs.Empty);
    }

    private AudioOutputStatus CreateDefaultOutputStatus()
    {
        if (deviceWatcher is null)
        {
            return new AudioOutputStatus(
                true,
                AudioOutputKind.None,
                "Windows default output",
                "Audio output will be checked when playback starts.",
                AudioOutputMessageKind.CheckedWhenPlaybackStarts);
        }

        if (deviceWatcher.TryGetDefaultRenderDevice(out var device))
        {
            try
            {
                return new AudioOutputStatus(
                    true,
                    AudioOutputKind.None,
                    GetDeviceName(device),
                    "Windows default output is available.",
                    AudioOutputMessageKind.WindowsDefaultAvailable);
            }
            finally
            {
                device?.Dispose();
            }
        }

        return AudioOutputStatus.Unavailable(
            "No audio output is available. Connect or enable an output " +
            "device, then press Play.",
            AudioOutputMessageKind.NoOutputPressPlay);
    }

    private bool TryGetDefaultDeviceId(out string? deviceId)
    {
        deviceId = null;
        if (deviceWatcher is null ||
            !deviceWatcher.TryGetDefaultRenderDevice(out var device))
        {
            return false;
        }

        try
        {
            deviceId = GetDeviceId(device);
            return deviceId is not null;
        }
        finally
        {
            device?.Dispose();
        }
    }

    private AudioOutputStatusChangedEventArgs? UpdateOutputStatusLocked(
        AudioOutputStatus newStatus,
        bool playbackInterrupted = false,
        bool force = false)
    {
        hasAvailableOutput = newStatus.IsAvailable;
        if (!force && !playbackInterrupted && outputStatus.Equals(newStatus))
            return null;

        outputStatus = newStatus;
        return new AudioOutputStatusChangedEventArgs(
            newStatus,
            playbackInterrupted);
    }

    private void RaiseOutputStatusChanged(
        AudioOutputStatusChangedEventArgs? e)
    {
        if (e is not null && !disposed)
            OutputStatusChanged?.Invoke(this, e);
    }

    private void DisposeLoadedTrackLocked()
    {
        DisposeOutputLocked();

        switchableSource?.ClearSource();
        switchableSource = null;
        DisposeTrackChainLocked(reader, visualizer);
        reader = null;
        visualizer = null;
        volumeProvider = null;
        loadedPath = null;
    }

    private static void DisposeTrackChainLocked(
        AudioFileReader? chainReader,
        VisualizerSampleProvider? chainVisualizer)
    {
        try
        {
            chainVisualizer?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Could not dispose the visualizer sample provider");
        }

        try
        {
            chainReader?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Could not dispose the audio file reader");
        }
    }

    private void DisposeOutputLocked()
    {
        var oldOutput = output;
        var oldDevice = outputDevice;
        output = null;
        outputDevice = null;
        outputDeviceId = null;
        outputBroken = false;

        if (oldOutput is not null)
        {
            UnsubscribePlaybackStoppedLocked(oldOutput);
            TryDisposeOutput(oldOutput);
        }

        try
        {
            oldDevice?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Could not dispose the previous audio endpoint");
        }
    }

    private static void TryDisposeOutput(IWavePlayer player)
    {
        try
        {
            player.Stop();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Could not stop the previous audio output cleanly");
        }

        try
        {
            player.Dispose();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Could not dispose the previous audio output cleanly");
        }
    }

    private static string GetDeviceName(MMDevice? device)
    {
        if (device is null)
            return "Windows audio output";

        try
        {
            var name = device.FriendlyName;
            return string.IsNullOrWhiteSpace(name)
                ? "Windows audio output"
                : name;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Could not read the audio output name");
            return "Windows audio output";
        }
    }

    private static string? GetDeviceId(MMDevice? device)
    {
        if (device is null)
            return null;

        try
        {
            var id = device.ID;
            return string.IsNullOrWhiteSpace(id) ? null : id;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Could not read the audio output identifier");
            return null;
        }
    }

    private static IWavePlayer CreateWasapiOutput(MMDevice? device) =>
        device is null
            ? new WasapiOut(
                AudioClientShareMode.Shared,
                useEventSync: true,
                WasapiLatencyMilliseconds)
            : new WasapiOut(
                device,
                AudioClientShareMode.Shared,
                useEventSync: true,
                WasapiLatencyMilliseconds);

    private static IWavePlayer CreateWaveOutOutput() => new WaveOutEvent
    {
        DesiredLatency = WaveOutLatencyMilliseconds,
        NumberOfBuffers = 3
    };

    private static AudioOutputBackendPreference ReadBackendPreference()
    {
        var configured = Environment.GetEnvironmentVariable(
            BackendPreferenceEnvironmentVariable);
        if (AudioOutputBackendPreferenceParser.TryParse(
                configured,
                out var preference))
        {
            if (preference != AudioOutputBackendPreference.Automatic)
            {
                Log.Information(
                    "Audio output backend forced by {EnvironmentVariable}: {Preference}",
                    BackendPreferenceEnvironmentVariable,
                    preference);
            }

            return preference;
        }

        Log.Warning(
            "Ignoring invalid {EnvironmentVariable} value {Value}; " +
            "expected auto, wasapi, or waveout",
            BackendPreferenceEnvironmentVariable,
            configured);
        return AudioOutputBackendPreference.Automatic;
    }

    private static bool IsLikelyOutputFailure(Exception ex) =>
        ex is COMException or MmException ||
        ex.HResult == DeviceInvalidatedHResult;

    private void ThrowIfDisposedLocked() =>
        ObjectDisposedException.ThrowIf(disposed, this);
}
