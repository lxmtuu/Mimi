using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mimi.Controls;
using Mimi.Models;
using Serilog;

namespace Mimi.ViewModels;

/// <summary>
/// Settings, window/library restoration, and paused-session behavior for
/// <see cref="MainWindowViewModel"/>. Keeping this state in a partial class
/// makes the persistence boundary explicit without adding another shared,
/// mutable settings view model.
/// </summary>
public sealed partial class MainWindowViewModel
{
    private bool isRestoringSettings;
    private bool persistedStateRestored;
    private bool playbackSessionRestoreAttempted;
    private DateTime lastPlaybackSessionSaveUtc = DateTime.MinValue;
    private Task? legacyCoverArtUpgradeTask;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLibraryContentVisible))]
    private bool isSettingsOpen;

    [ObservableProperty]
    private bool restorePlaybackSession = true;

    [ObservableProperty]
    private bool restoreLibraryView = true;

    [ObservableProperty]
    private bool restoreWindowPlacement = true;

    [ObservableProperty]
    private bool autoUpgradeLegacyCoverArt = true;

    [ObservableProperty]
    private WindowPlacementSettings windowPlacement = new();

    public bool IsLibraryContentVisible => !IsSettingsOpen;

    public string VolumePercentLabel => $"{Volume:P0}";

    public string DataDirectory => userDataService.DataDirectory;

    public string LogDirectory => Path.Combine(DataDirectory, "logs");

    public string AppVersionLabel =>
        $"Mimi {GetType().Assembly.GetName().Version?.ToString(3) ?? "0.4.0"}";

    private void ApplyLoadedSettings()
    {
        Volume = settings.Volume;
        audioPlayer.Volume = (float)settings.Volume;

        if (Enum.TryParse<VisualizerDisplayMode>(
                settings.VisualizerMode,
                ignoreCase: true,
                out var visualizerMode))
        {
            VisualizerMode = visualizerMode;
        }

        RestorePlaybackSession = settings.RestorePlaybackSession;
        RestoreLibraryView = settings.RestoreLibraryView;
        RestoreWindowPlacement = settings.RestoreWindowPlacement;
        AutoUpgradeLegacyCoverArt = settings.AutoUpgradeLegacyCoverArt;
        WindowPlacement = settings.WindowPlacement;
    }

    partial void OnRestorePlaybackSessionChanged(bool value)
    {
        if (settingsLoaded && !isRestoringSettings)
            SaveSettings();
    }

    partial void OnRestoreLibraryViewChanged(bool value)
    {
        if (settingsLoaded && !isRestoringSettings)
            SaveSettings();
    }

    partial void OnRestoreWindowPlacementChanged(bool value)
    {
        if (settingsLoaded && !isRestoringSettings)
            SaveSettings();
    }

    partial void OnAutoUpgradeLegacyCoverArtChanged(bool value)
    {
        if (!settingsLoaded || isRestoringSettings) return;

        SaveSettings();
        if (value)
            QueueLegacyCoverArtUpgrade();
    }

    partial void OnWindowPlacementChanged(WindowPlacementSettings value)
    {
        if (settingsLoaded && !isRestoringSettings)
            SaveSettings();
    }

    partial void OnVisualizerModeChanged(VisualizerDisplayMode value)
    {
        if (settingsLoaded && !isRestoringSettings)
            SaveSettings();
    }

    partial void OnSelectedTrackChanged(TrackItemViewModel? value)
    {
        if (settingsLoaded && !isRestoringSettings)
            SaveSettings();
    }

    [RelayCommand]
    private void OpenSettings() => IsSettingsOpen = true;

    [RelayCommand]
    private void CloseSettings() => IsSettingsOpen = false;

    [RelayCommand]
    private void ResetSettings()
    {
        if (!persistedStateRestored)
        {
            dialogService.ShowInformation(
                "Mimi is still loading your library. Try again in a moment.",
                "Settings");
            return;
        }

        if (!dialogService.Confirm(
                "Reset Mimi's preferences to their defaults? Your library, " +
                "favorites, playlists, and playback history will not be deleted.",
                "Reset settings"))
        {
            return;
        }

        settings = new AppSettings();
        isRestoringSettings = true;
        try
        {
            ApplyLoadedSettings();
            SelectedPlaylist = null;
            ActiveView = LibraryViewMode.Library;
            SearchText = string.Empty;
            SelectedArtistFilter = AllArtistsFilter;
            SelectedAlbumFilter = AllAlbumsFilter;
            SelectedGenreFilter = AllGenresFilter;
            SelectedFormatFilter = AllFormatsFilter;
            SelectedPlaybackStateFilter = AllPlaybackStatesFilter;
            ResetTrackSort();
            ConfigureTracksView();
        }
        finally
        {
            isRestoringSettings = false;
        }

        SaveSettings(force: true);
        dialogService.ShowInformation(
            "Settings were reset. Restart Mimi if you also want the window " +
            "to return to its default position.",
            "Settings");
    }

    [RelayCommand]
    private void OpenDataFolder() => OpenFolder(DataDirectory);

    [RelayCommand]
    private void OpenLogFolder() => OpenFolder(LogDirectory);

    private void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not open folder {Folder}", path);
            dialogService.ShowWarning(
                $"Couldn't open that folder.\n\n{ex.Message}",
                "Settings");
        }
    }

    private void SaveSettings(bool force = false)
    {
        if (!settingsLoaded || isRestoringSettings) return;

        CapturePersistedState();

        // Sliders and window moves can fire rapidly; the in-memory state is
        // always current, while disk writes are throttled and forced on exit.
        if (!force &&
            (DateTime.UtcNow - lastSettingsSaveUtc).TotalMilliseconds < 500)
        {
            return;
        }

        lastSettingsSaveUtc = DateTime.UtcNow;
        userDataService.SaveSettings(settings);
    }

    private void CapturePersistedState()
    {
        if (!settingsLoaded || isRestoringSettings) return;

        settings.Volume = Volume;
        settings.VisualizerMode = VisualizerMode.ToString();
        settings.RestorePlaybackSession = RestorePlaybackSession;
        settings.RestoreLibraryView = RestoreLibraryView;
        settings.RestoreWindowPlacement = RestoreWindowPlacement;
        settings.AutoUpgradeLegacyCoverArt = AutoUpgradeLegacyCoverArt;
        settings.WindowPlacement = RestoreWindowPlacement
            ? WindowPlacement
            : new WindowPlacementSettings();

        // Do not let a volume change made before the catalog finishes
        // loading erase the saved view/session that has not been restored.
        if (!persistedStateRestored) return;

        if (RestoreLibraryView)
        {
            var libraryView = settings.LibraryView;
            libraryView.ActiveView = ActiveView.ToString();
            libraryView.SelectedPlaylistId =
                ActiveView == LibraryViewMode.Playlist
                    ? SelectedPlaylist?.Id
                    : null;
            libraryView.SelectedTrackPath = SelectedTrack?.FilePath;
            libraryView.SearchText = SearchText;
            libraryView.ArtistFilter = SelectedArtistFilter;
            libraryView.AlbumFilter = SelectedAlbumFilter;
            libraryView.GenreFilter = SelectedGenreFilter;
            libraryView.FormatFilter = SelectedFormatFilter;
            libraryView.PlaybackStateFilter = SelectedPlaybackStateFilter;
            libraryView.SortField = trackSortField.ToString();
            libraryView.SortDirection = trackSortDirection.ToString();
        }
        else
        {
            settings.LibraryView = new LibraryViewSettings();
        }

        settings.PlaybackSession =
            RestorePlaybackSession && CurrentTrack is not null
                ? new PlaybackSessionSettings
                {
                    CurrentTrackPath = CurrentTrack.FilePath,
                    PositionSeconds = Math.Max(
                        0,
                        audioPlayer.CurrentTime.TotalSeconds)
                }
                : null;
    }

    private void RestorePersistedState()
    {
        if (persistedStateRestored || disposed) return;
        persistedStateRestored = true;

        if (RestoreLibraryView)
            RestoreLibraryViewState();

        RestorePlaybackSessionState();
        CapturePersistedState();
    }

    private void RestoreLibraryViewState()
    {
        var saved = settings.LibraryView;
        isRestoringSettings = true;
        try
        {
            if (!Enum.TryParse<LibraryViewMode>(
                    saved.ActiveView,
                    ignoreCase: true,
                    out var activeView))
            {
                activeView = LibraryViewMode.Library;
            }

            if (activeView == LibraryViewMode.Playlist)
            {
                var playlist = Playlists.FirstOrDefault(candidate =>
                    candidate.Id == saved.SelectedPlaylistId);
                if (playlist is not null)
                {
                    SelectedPlaylist = playlist;
                }
                else
                {
                    SelectedPlaylist = null;
                    ActiveView = LibraryViewMode.Library;
                }
            }
            else
            {
                SelectedPlaylist = null;
                ActiveView = activeView;
            }

            SelectedArtistFilter = GetValidFilter(
                ArtistFilterOptions,
                saved.ArtistFilter,
                AllArtistsFilter);
            SelectedAlbumFilter = GetValidFilter(
                AlbumFilterOptions,
                saved.AlbumFilter,
                AllAlbumsFilter);
            SelectedGenreFilter = GetValidFilter(
                GenreFilterOptions,
                saved.GenreFilter,
                AllGenresFilter);
            SelectedFormatFilter = GetValidFilter(
                FormatFilterOptions,
                saved.FormatFilter,
                AllFormatsFilter);
            SelectedPlaybackStateFilter = GetValidFilter(
                PlaybackStateFilterOptions,
                saved.PlaybackStateFilter,
                AllPlaybackStatesFilter);

            // Search intentionally resets custom sorting while typing, so
            // apply the saved sort only after restoring the saved query.
            SearchText = saved.SearchText;

            if (!Enum.TryParse<TrackSortField>(
                    saved.SortField,
                    ignoreCase: true,
                    out var sortField))
            {
                sortField = TrackSortField.None;
            }

            if (!Enum.TryParse<ListSortDirection>(
                    saved.SortDirection,
                    ignoreCase: true,
                    out var sortDirection))
            {
                sortDirection = ListSortDirection.Ascending;
            }

            trackSortField =
                ActiveView == LibraryViewMode.Playlist
                    ? TrackSortField.None
                    : sortField;
            trackSortDirection = sortDirection;
            NotifySortLabelsChanged();
            ConfigureTracksView();

            SelectedTrack = Tracks.FirstOrDefault(track => string.Equals(
                track.FilePath,
                saved.SelectedTrackPath,
                StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            isRestoringSettings = false;
        }
    }

    private static string GetValidFilter(
        IEnumerable<string> options,
        string? savedValue,
        string fallback) =>
        savedValue is not null && options.Contains(savedValue)
            ? savedValue
            : fallback;

    private void RestorePlaybackSessionState()
    {
        if (playbackSessionRestoreAttempted) return;
        playbackSessionRestoreAttempted = true;

        if (!RestorePlaybackSession) return;

        var session = settings.PlaybackSession;
        if (string.IsNullOrWhiteSpace(session?.CurrentTrackPath)) return;

        var track = Tracks.FirstOrDefault(candidate => string.Equals(
            candidate.FilePath,
            session.CurrentTrackPath,
            StringComparison.OrdinalIgnoreCase));
        if (track is null)
        {
            Log.Information(
                "Saved playback session track is no longer in the library: {FilePath}",
                session.CurrentTrackPath);
            settings.PlaybackSession = null;
            return;
        }

        isRestoringSettings = true;
        try
        {
            audioPlayer.Load(track.FilePath);
            CurrentTrack = track;
            currentPlayHistoryId = null;
            audioPlayer.Volume = (float)Volume;
            NowPlayingTitle = track.Title;
            NowPlayingSubtitle = track.Subtitle;
            CurrentCoverArtPath = track.CoverArtPath;

            var duration = audioPlayer.Duration;
            var targetSeconds = session.PositionSeconds;
            if (duration > TimeSpan.Zero)
            {
                targetSeconds = Math.Clamp(
                    targetSeconds,
                    0,
                    Math.Max(0, duration.TotalSeconds - 0.25));
            }

            if (targetSeconds > 0)
                audioPlayer.Seek(TimeSpan.FromSeconds(targetSeconds));

            // Load does not start output, but pause explicitly so a future
            // audio-engine change can never surprise the user at startup.
            audioPlayer.Pause();
            SelectedTrack = track;
            RefreshPlaybackPosition();
            Log.Information(
                "Restored paused playback session for {FilePath} at {PositionSeconds:0.0}s",
                track.FilePath,
                targetSeconds);
        }
        catch (Exception ex)
        {
            CurrentTrack = null;
            currentPlayHistoryId = null;
            CurrentCoverArtPath = null;
            track.Track.IsMissing = !File.Exists(track.FilePath);
            track.Track.HasError = true;
            track.HasPlaybackError = true;
            settings.PlaybackSession = null;
            _ = PersistPlaybackErrorAsync(track);
            Log.Warning(
                ex,
                "Could not restore paused playback session for {FilePath}",
                track.FilePath);
        }
        finally
        {
            isRestoringSettings = false;
            RefreshPlaybackState();
        }
    }

    private void CapturePlaybackSessionPeriodically()
    {
        if (!RestorePlaybackSession || CurrentTrack is null ||
            !persistedStateRestored || isRestoringSettings)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if ((now - lastPlaybackSessionSaveUtc).TotalSeconds < 5)
            return;

        var saved = settings.PlaybackSession;
        if (saved is not null &&
            string.Equals(
                saved.CurrentTrackPath,
                CurrentTrack.FilePath,
                StringComparison.OrdinalIgnoreCase) &&
            Math.Abs(
                saved.PositionSeconds -
                audioPlayer.CurrentTime.TotalSeconds) < 2)
        {
            lastPlaybackSessionSaveUtc = now;
            return;
        }

        lastPlaybackSessionSaveUtc = now;
        SaveSettings();
    }
}
