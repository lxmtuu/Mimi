using NAudio.Wave;

namespace MikuVerseMusic.Services;

/// <summary>Local-file playback engine. Online providers should be added as separate,
/// officially authorized adapters; never scrape or bypass provider access controls.</summary>
public sealed class AudioService : IDisposable
{
    private IWavePlayer? output;
    private AudioFileReader? reader;
    private VisualizerSampleProvider? visualizer;

    public float[] GetVisualizerLevels() => visualizer?.GetLevels() ?? new float[32];
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
        visualizer = new VisualizerSampleProvider(reader);
        output = new WaveOutEvent();
        output.Init(visualizer);
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
        output?.Stop();
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
        get => reader?.Volume ?? 0.7f;
        set { if (reader is not null) reader.Volume = Math.Clamp(value, 0f, 1f); }
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
    }

    public void Dispose() => StopAndDispose();
}
