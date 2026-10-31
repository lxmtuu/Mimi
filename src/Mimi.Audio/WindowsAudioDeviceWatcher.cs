using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using Serilog;

namespace Mimi.Services;

internal enum AudioDeviceChangeKind
{
    DefaultChanged,
    DeviceAdded,
    DeviceRemoved,
    DeviceStateChanged
}

internal sealed class AudioDeviceChangedEventArgs : EventArgs
{
    public AudioDeviceChangedEventArgs(
        AudioDeviceChangeKind kind,
        string? deviceId,
        DeviceState? deviceState = null)
    {
        Kind = kind;
        DeviceId = deviceId;
        DeviceState = deviceState;
    }

    public AudioDeviceChangeKind Kind { get; }

    public string? DeviceId { get; }

    public DeviceState? DeviceState { get; }
}

/// <summary>
/// Owns the Windows multimedia-device enumerator used both to query the
/// current default render endpoint and to receive endpoint notifications.
/// Callbacks only raise a lightweight managed event; rebuilding an audio
/// stream is deliberately left to <see cref="AudioService"/>, which debounces
/// and serializes that work away from the COM notification thread.
/// </summary>
internal sealed class WindowsAudioDeviceWatcher : IDisposable
{
    private readonly MMDeviceEnumerator enumerator;
    private readonly AudioDeviceNotificationClient notificationClient;
    private volatile bool disposed;
    private int disposeState;

    public WindowsAudioDeviceWatcher()
    {
        enumerator = new MMDeviceEnumerator();
        notificationClient = new AudioDeviceNotificationClient(HandleNotification);

        try
        {
            var result = enumerator.RegisterEndpointNotificationCallback(
                notificationClient);
            if (result < 0)
                Marshal.ThrowExceptionForHR(result);

            IsMonitoringAvailable = true;
        }
        catch (Exception ex)
        {
            Log.Warning(
                ex,
                "Could not register for Windows audio-device notifications; " +
                "Mimi will recover when playback is next started or fails");
        }
    }

    public event EventHandler<AudioDeviceChangedEventArgs>? Changed;

    public bool IsMonitoringAvailable { get; }

    public bool TryGetDefaultRenderDevice(
        [NotNullWhen(true)] out MMDevice? device)
    {
        device = null;
        if (disposed) return false;

        try
        {
            if (!enumerator.HasDefaultAudioEndpoint(
                    DataFlow.Render,
                    Role.Multimedia))
            {
                return false;
            }

            device = enumerator.GetDefaultAudioEndpoint(
                DataFlow.Render,
                Role.Multimedia);
            return true;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Could not query the default Windows audio output");
            device?.Dispose();
            device = null;
            return false;
        }
    }

    public bool HasDefaultRenderDevice()
    {
        if (disposed) return false;

        try
        {
            return enumerator.HasDefaultAudioEndpoint(
                DataFlow.Render,
                Role.Multimedia);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Could not check for a default Windows audio output");
            return false;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposeState, 1) != 0)
            return;

        disposed = true;

        if (IsMonitoringAvailable)
        {
            try
            {
                enumerator.UnregisterEndpointNotificationCallback(
                    notificationClient);
            }
            catch (Exception ex)
            {
                Log.Debug(
                    ex,
                    "Could not unregister Windows audio-device notifications");
            }
        }

        try
        {
            enumerator.Dispose();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Could not dispose the Windows audio-device enumerator");
        }
    }

    private void HandleNotification(AudioDeviceChangedEventArgs e)
    {
        if (disposed) return;

        try
        {
            Changed?.Invoke(this, e);
        }
        catch (Exception ex)
        {
            // A subscriber failure must never escape into the CoreAudio
            // notification callback.
            Log.Debug(ex, "An audio-device notification subscriber failed");
        }
    }
}

/// <summary>
/// COM notification receiver for Windows audio endpoint changes. It is
/// public so the built-in COM callable-wrapper support can expose it to
/// CoreAudio reliably; application code should use the watcher above.
/// </summary>
[ComVisible(true)]
public sealed class AudioDeviceNotificationClient : IMMNotificationClient
{
    private readonly Action<AudioDeviceChangedEventArgs> onChanged;

    internal AudioDeviceNotificationClient(
        Action<AudioDeviceChangedEventArgs> onChanged)
    {
        this.onChanged = onChanged ??
            throw new ArgumentNullException(nameof(onChanged));
    }

    public void OnDeviceStateChanged(string deviceId, DeviceState newState) =>
        Notify(new AudioDeviceChangedEventArgs(
            AudioDeviceChangeKind.DeviceStateChanged,
            deviceId,
            newState));

    public void OnDeviceAdded(string pwstrDeviceId) =>
        Notify(new AudioDeviceChangedEventArgs(
            AudioDeviceChangeKind.DeviceAdded,
            pwstrDeviceId));

    public void OnDeviceRemoved(string deviceId) =>
        Notify(new AudioDeviceChangedEventArgs(
            AudioDeviceChangeKind.DeviceRemoved,
            deviceId));

    public void OnDefaultDeviceChanged(
        DataFlow flow,
        Role role,
        string defaultDeviceId)
    {
        // Mimi targets the Multimedia render role. Console is included
        // because Windows' normal output pickers usually update both, and
        // the audio service verifies the actual Multimedia endpoint before
        // deciding whether a rebuild is required.
        if (flow != DataFlow.Render ||
            role is not (Role.Console or Role.Multimedia))
        {
            return;
        }

        Notify(new AudioDeviceChangedEventArgs(
            AudioDeviceChangeKind.DefaultChanged,
            defaultDeviceId));
    }

    public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
    {
        // Endpoint property changes are noisy and do not by themselves mean
        // that an already-open shared-mode stream must be rebuilt. Device
        // invalidation is handled by state/default notifications and by the
        // output's own stopped-with-error event.
    }

    private void Notify(AudioDeviceChangedEventArgs e)
    {
        try
        {
            onChanged(e);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Could not forward a Windows audio-device notification");
        }
    }
}
