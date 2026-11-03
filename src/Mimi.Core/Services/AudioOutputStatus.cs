namespace Mimi.Services;

/// <summary>
/// Stable identifier for an audio-output status message. The UI layer maps
/// these values to localized text, while <see cref="AudioOutputStatus.Message"/>
/// remains available as an English diagnostic/fallback string.
/// </summary>
public enum AudioOutputMessageKind
{
    Custom,
    Ready,
    NoOutputAvailable,
    WasapiReady,
    WasapiForcedReady,
    WaveOutFallbackReady,
    WaveOutForcedReady,
    WasapiUnavailableFallbackDisabled,
    NoOutputPressPlay,
    PlaybackInterruptedOutputChanged,
    PlaybackInterruptedOutputUnavailable,
    CheckedWhenPlaybackStarts,
    WindowsDefaultAvailable,
    OutputUnavailable
}

/// <summary>Identifies the output technology currently used by the player.</summary>
public enum AudioOutputKind
{
    /// <summary>No output stream is open, or no output is available.</summary>
    None,

    /// <summary>WASAPI in shared mode, following the Windows default device.</summary>
    WasapiShared,

    /// <summary>Legacy Windows multimedia output, used after WASAPI fails.</summary>
    WaveOutFallback,

    /// <summary>Legacy Windows multimedia output selected for diagnostics.</summary>
    WaveOutForced
}

/// <summary>
/// Diagnostic-only backend selection. Automatic is the normal end-user
/// behavior: WASAPI shared mode first, WaveOut only if WASAPI cannot be
/// opened. The two forced values exist so the same build can be A/B-tested
/// on the same Windows machine and endpoint without adding a user-facing
/// engine switch to Settings.
/// </summary>
public enum AudioOutputBackendPreference
{
    Automatic,
    ForceWasapiShared,
    ForceWaveOut
}

/// <summary>Parses the diagnostic audio-backend preference.</summary>
public static class AudioOutputBackendPreferenceParser
{
    public static bool TryParse(
        string? value,
        out AudioOutputBackendPreference preference)
    {
        preference = AudioOutputBackendPreference.Automatic;
        if (string.IsNullOrWhiteSpace(value))
            return true;

        var normalized = value.Trim().ToLowerInvariant()
            .Replace("_", "-", StringComparison.Ordinal);
        switch (normalized)
        {
            case "auto":
            case "automatic":
            case "default":
                return true;

            case "wasapi":
            case "wasapi-shared":
            case "force-wasapi":
            case "force-wasapi-shared":
                preference = AudioOutputBackendPreference.ForceWasapiShared;
                return true;

            case "waveout":
            case "wave-out":
            case "force-waveout":
            case "force-wave-out":
                preference = AudioOutputBackendPreference.ForceWaveOut;
                return true;

            default:
                return false;
        }
    }
}

/// <summary>
/// Immutable, UI-facing snapshot of the audio output. Keeping this in Core
/// lets the application describe and test output state without exposing any
/// NAudio or Windows endpoint types.
/// </summary>
public readonly record struct AudioOutputStatus
{
    public AudioOutputStatus(
        bool isAvailable,
        AudioOutputKind kind,
        string? deviceName,
        string? message,
        AudioOutputMessageKind messageKind = AudioOutputMessageKind.Custom)
    {
        IsAvailable = isAvailable;
        Kind = isAvailable ? kind : AudioOutputKind.None;
        DeviceName = string.IsNullOrWhiteSpace(deviceName)
            ? isAvailable ? "Windows audio output" : "No output device"
            : deviceName.Trim();
        Message = string.IsNullOrWhiteSpace(message)
            ? isAvailable ? "Audio output is ready." : "No audio output is available."
            : message.Trim();
        MessageKind = messageKind == AudioOutputMessageKind.Custom &&
                      string.IsNullOrWhiteSpace(message)
            ? isAvailable
                ? AudioOutputMessageKind.Ready
                : AudioOutputMessageKind.NoOutputAvailable
            : messageKind;
    }

    public bool IsAvailable { get; }

    public AudioOutputKind Kind { get; }

    public string DeviceName { get; }

    public string Message { get; }

    public AudioOutputMessageKind MessageKind { get; }

    public static AudioOutputStatus Unavailable(
        string? message = null,
        AudioOutputMessageKind messageKind = AudioOutputMessageKind.Custom) =>
        new(false, AudioOutputKind.None, null, message, messageKind);
}

/// <summary>Raised when the usable output device or output technology changes.</summary>
public sealed class AudioOutputStatusChangedEventArgs : EventArgs
{
    public AudioOutputStatusChangedEventArgs(
        AudioOutputStatus status,
        bool playbackInterrupted)
    {
        Status = status;
        PlaybackInterrupted = playbackInterrupted;
    }

    public AudioOutputStatus Status { get; }

    /// <summary>
    /// True when a device change or output failure paused music that was
    /// actively playing. The current song and position are retained.
    /// </summary>
    public bool PlaybackInterrupted { get; }
}

/// <summary>
/// The requested file may be perfectly valid, but Windows currently has no
/// usable output through which it can be played. This distinct exception
/// prevents output problems from being recorded as corrupt-track errors.
/// </summary>
public sealed class AudioOutputUnavailableException : InvalidOperationException
{
    public AudioOutputUnavailableException(
        AudioOutputStatus status,
        Exception? innerException = null)
        : base(
            status.IsAvailable
                ? "The audio output is unavailable."
                : status.Message,
            innerException)
    {
        Status = status.IsAvailable
            ? AudioOutputStatus.Unavailable(
                Message,
                AudioOutputMessageKind.OutputUnavailable)
            : status;
    }

    public AudioOutputUnavailableException()
        : base("The audio output is unavailable.")
    {
        Status = AudioOutputStatus.Unavailable(
            Message,
            AudioOutputMessageKind.OutputUnavailable);
    }

    public AudioOutputUnavailableException(string message)
        : base(message)
    {
        Status = AudioOutputStatus.Unavailable(message);
    }

    public AudioOutputUnavailableException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        Status = AudioOutputStatus.Unavailable(message);
    }

    public AudioOutputStatus Status { get; }
}
