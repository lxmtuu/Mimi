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
    event EventHandler<AudioOutputStatusChangedEventArgs>? OutputStatusChanged;

    bool IsPlaying { get; }
    TimeSpan CurrentTime { get; }
    TimeSpan Duration { get; }
    float Volume { get; set; }
    AudioOutputStatus OutputStatus { get; }

    /// <summary>
    /// Decodes a file and prepares it paused. A successful load does not by
    /// itself prove that an output device is currently usable.
    /// </summary>
    void Load(string path);

    /// <summary>Starts or resumes the loaded file.</summary>
    /// <exception cref="AudioOutputUnavailableException">
    /// The file is loaded, but no usable Windows audio output is available.
    /// </exception>
    void Play();
    void Pause();
    void Toggle();
    void Stop();
    void Seek(TimeSpan position);

    VisualizerFrame GetVisualizerFrame();
    float[] GetVisualizerLevels();
    string GetVisualizerDiagnostics();
}
