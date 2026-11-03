using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Mimi.ViewModels;

/// <summary>
/// Opt-in online music panel. The panel hosts the official YouTube Music web
/// player in its own window, so the audio is served and played by that site.
/// Mimi does not download, extract, transcode, or cache anything from it, and
/// no online item becomes a local <see cref="TrackItemViewModel"/>, queue
/// entry, favorite, or saved-session track.
/// </summary>
public sealed partial class MainWindowViewModel
{
    [ObservableProperty]
    private bool isOnlineMusicOpen;

    /// <summary>
    /// The panel is a local user preference switch, not a provider grant:
    /// turning it off closes the window and removes the navigation entry.
    /// </summary>
    public bool IsOnlineMusicAvailable => OnlineMusicEnabled;

    partial void OnIsOnlineMusicOpenChanged(bool value)
    {
        if (!value) return;

        // Only one audio surface may own sound at a time. Opening the online
        // panel releases Internet Radio and pauses local playback so the web
        // player is never heard on top of Mimi's own transport.
        if (CurrentRadioStation is not null || IsRadioConnecting)
            DisableInternetRadio();
        else
            IsRadioOpen = false;

        if (!audioPlayer.IsPlaying) return;

        audioPlayer.Pause();
        RefreshPlaybackState();
    }

    [RelayCommand]
    private void ToggleOnlineMusic()
    {
        if (!IsOnlineMusicAvailable)
        {
            IsOnlineMusicOpen = false;
            return;
        }

        IsSettingsOpen = false;
        IsOnlineMusicOpen = !IsOnlineMusicOpen;
    }

    [RelayCommand]
    private void CloseOnlineMusic() => IsOnlineMusicOpen = false;

    /// <summary>
    /// Closes the panel when the Settings switch is turned off, mirroring the
    /// Internet Radio kill switch.
    /// </summary>
    internal void DisableOnlineMusic() => IsOnlineMusicOpen = false;
}
