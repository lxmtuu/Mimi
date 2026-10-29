namespace Mimi.Services;

/// <summary>
/// UI-facing playback contract. Keeping this in Core lets view models be
/// tested and composed without depending on the concrete Windows/NAudio
/// playback implementation.
/// </summary>
public interface IAudioPlayer : IDisposable
{
    event EventHandler? PlaybackChanged;
    event EventHandler? PlaybackEnded;

    bool IsPlaying { get; }
    TimeSpan CurrentTime { get; }
    TimeSpan Duration { get; }
    float Volume { get; set; }

    void Load(string path);
    void Play();
    void Pause();
    void Toggle();
    void Stop();
    void Seek(TimeSpan position);

    VisualizerFrame GetVisualizerFrame();
    float[] GetVisualizerLevels();
    string GetVisualizerDiagnostics();
}
