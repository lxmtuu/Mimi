using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Mimi.Services;

/// <summary>Local-file playback engine. Online providers should be added as separate,
/// officially authorized adapters; never scrape or bypass provider access controls.</summary>
public sealed class AudioService : IDisposable
{
    private IWavePlayer? output;
    private AudioFileReader? reader;
    private VisualizerSampleProvider? visualizer;
    private VolumeSampleProvider? volumeProvider;
    private float volume = 0.7f;

    public float[] GetVisualizerLevels()
    {
        if (visualizer is null) return new float[VisualizerSampleProvider.BandCount];
        if (!IsPlaying) visualizer.Decay();
        return visualizer.GetLevels();
    }
    public string GetVisualizerDiagnostics() => visualizer?.GetDiagnostics() ?? "visualizer=not-initialized";

    public event EventHandler? PlaybackChanged;
    public event EventHandler? PlaybackEnded;
    private bool suppressStoppedEvent;
    public bool IsPlaying => output?.PlaybackState == PlaybackState.Playing;
    public TimeSpan CurrentTime => reader?.CurrentTime ?? TimeSpan.Zero;
    public TimeSpan Duration => reader?.TotalTime ?? TimeSpan.Zero;

    public void Load(string path)
    {
        StopAndDispose();
        reader = new AudioFileReader(path);
        // Keep the reader at unity gain and apply volume AFTER the visualizer
        // tap, so the spectrum reflects the music itself, not the volume slider.
        reader.Volume = 1.0f;
        visualizer = new VisualizerSampleProvider(reader);
        volumeProvider = new VolumeSampleProvider(visualizer) { Volume = volume };
        output = new WaveOutEvent();
        output.Init(volumeProvider);
        output.PlaybackStopped += Output_PlaybackStopped;
        PlaybackChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Output_PlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (suppressStoppedEvent) return;

        var endedNaturally = e.Exception is null &&
            reader is not null &&
            reader.Length > 0 &&
            reader.Position >= reader.Length;

        PlaybackChanged?.Invoke(this, EventArgs.Empty);
        if (endedNaturally) PlaybackEnded?.Invoke(this, EventArgs.Empty);
    }

    public void Play()
    {
        if (output is null) return;
        output.Play();
        PlaybackChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Pause()
    {
        output?.Pause();
        PlaybackChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Toggle()
    {
        if (IsPlaying) Pause(); else Play();
    }

    public void Stop()
    {
        // An explicit stop must never be mistaken for a track ending
        // naturally (which would auto-advance to the next song).
        suppressStoppedEvent = true;
        try { output?.Stop(); }
        finally { suppressStoppedEvent = false; }
        if (reader is not null) reader.Position = 0;
        PlaybackChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Seek(TimeSpan position)
    {
        if (reader is null) return;
        reader.CurrentTime = TimeSpan.FromSeconds(Math.Clamp(position.TotalSeconds, 0, reader.TotalTime.TotalSeconds));
    }

    public float Volume
    {
        get => volume;
        set
        {
            volume = Math.Clamp(value, 0f, 1f);
            if (volumeProvider is not null) volumeProvider.Volume = volume;
        }
    }

    private void StopAndDispose()
    {
        if (output is not null)
        {
            suppressStoppedEvent = true;
            try
            {
                output.PlaybackStopped -= Output_PlaybackStopped;
                output.Stop();
                output.Dispose();
                output = null;
            }
            finally
            {
                suppressStoppedEvent = false;
            }
        }
        reader?.Dispose();
        reader = null;
        visualizer = null;
        volumeProvider = null;
    }

    public void Dispose() => StopAndDispose();
}
