using Mimi.Core.Playback;

namespace Mimi.Services;

/// <summary>
/// UI-facing playback contract. Keeping this in Core lets view models be
/// tested and composed without depending on the concrete Windows/NAudio
/// playback implementation.
/// </summary>
public interface IAudioPlayer : IDisposable
{
    event EventHandler? PlaybackChanged;
    event EventHandler<PlaybackEndedEventArgs>? PlaybackEnded;
    event EventHandler<AudioOutputStatusChangedEventArgs>? OutputStatusChanged;

    /// <summary>
    /// Monotonic identifier for the current playback run. Implementations
    /// change it when a new file is loaded and when the loaded run is
    /// explicitly stopped or seeked, allowing delayed natural-end events to
    /// be matched to the run that actually ended.
    /// </summary>
    long PlaybackGeneration { get; }

    bool IsPlaying { get; }

    /// <summary>
    /// Whether the loaded source is a live network stream. Live streams do
    /// not expose a reliable duration and must never be seeked or treated as
    /// finite local tracks.
    /// </summary>
    bool IsLiveStream { get; }

    TimeSpan CurrentTime { get; }
    TimeSpan Duration { get; }
    float Volume { get; set; }
    AudioOutputStatus OutputStatus { get; }

    /// <summary>
    /// Decodes a file and prepares it paused. A successful load does not by
    /// itself prove that an output device is currently usable.
    /// </summary>
    void Load(string path);

    /// <summary>
    /// Opens a direct HTTP(S) live-stream URL and prepares it paused. The
    /// implementation must stream directly to the output and must not save,
    /// record, proxy, or persist the streamed audio.
    /// </summary>
    void LoadStream(Uri streamUrl);

    /// <summary>
    /// Opens a direct HTTP(S) URL for a finite, seekable online track and
    /// prepares it paused. Unlike <see cref="LoadStream"/> the source reports
    /// a real duration, so the transport may seek it. The implementation must
    /// stream directly and must not save, record, proxy, or persist the audio.
    /// </summary>
    void LoadRemoteAudio(Uri audioUrl);

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
