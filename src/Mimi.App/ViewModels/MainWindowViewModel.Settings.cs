using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mimi.Controls;
using Mimi.Localization;
using Mimi.Models;
using Mimi.Services;
using Mimi.Theming;
using Serilog;
using Mimi.YouTube;

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
    [NotifyPropertyChangedFor(nameof(IsRadioContentVisible))]
    private bool isSettingsOpen;

    [ObservableProperty]
    private string uiLanguage = UiLanguages.System;

    [ObservableProperty]
    private string uiTheme = BuiltInThemes.DefaultId;

    [ObservableProperty]
    private bool restorePlaybackSession = true;

    [ObservableProperty]
    private bool restoreLibraryView = true;

    [ObservableProperty]
    private bool restoreWindowPlacement = true;

    [ObservableProperty]
    private bool autoUpgradeLegacyCoverArt = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRadioAvailable))]
    private bool internetRadioEnabled = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOnlineAvailable))]
    private bool onlineCatalogEnabled = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsYouTubeAvailable))]
    private bool youTubeSearchEnabled = true;

    [ObservableProperty]
    private WindowPlacementSettings windowPlacement = new();

    public bool IsLibraryContentVisible =>
        !IsSettingsOpen && !IsRadioOpen && !IsOnlineOpen && !IsYouTubeOpen;

    public string VolumePercentLabel => $"{Volume:P0}";

    public string DataDirectory => userDataService.DataDirectory;

    public string LogDirectory => Path.Combine(DataDirectory, "logs");

    public string ThemesDirectory =>
        themeService?.ThemesDirectory ?? Path.Combine(DataDirectory, "themes");

    /// <summary>
    /// Themes offered by the Settings picker: built-ins first, then valid
    /// community themes loaded from <see cref="ThemesDirectory"/>.
    /// </summary>
    public ObservableCollection<ThemeOptionViewModel> ThemeOptions { get; } = [];

    /// <summary>
    /// The colours of the theme currently in use, shown as a small strip in
    /// Settings. Reading the palette beats discovering it by switching themes.
    /// </summary>
    public ObservableCollection<ThemeSwatch> ThemeSwatches { get; } = [];

    /// <summary>
    /// The tint applied on top of whichever theme is selected. Kept as text so
    /// the user can type a colour, and as a parsed value so everything else
    /// (preview, persistence, the theme service) uses the same answer.
    /// </summary>
    [ObservableProperty]
    private bool isCustomBackgroundEnabled;

    [ObservableProperty]
    private string customBackgroundHex = CustomBackgroundPresets[0];

    /// <summary>
    /// Short, fixed list of tints that suit the dark shell. A colour picker
    /// dialog would allow colours the app cannot render well; these are the
    /// hues the layer ladder was checked against.
    /// </summary>
    public IReadOnlyList<BackgroundTintOption> CustomBackgroundOptions { get; } =
        CustomBackgroundPresets
            .Select(hex => ThemeSurfaceCustomizer.TryParse(hex, out var color)
                ? new BackgroundTintOption(hex, Color.FromRgb(color.R, color.G, color.B))
                : null)
            .OfType<BackgroundTintOption>()
            .ToArray();

    public bool HasCustomBackgroundProblem =>
        IsCustomBackgroundEnabled && CustomBackgroundColor is null;

    public Brush CustomBackgroundPreview =>
        CustomBackgroundColor is { } color
            ? Freeze(Color.FromRgb(color.R, color.G, color.B))
            : Brushes.Transparent;

    /// <summary>
    /// The parsed tint, or null when the text is not a colour. Null is not an
    /// error by itself - it means the palette stays the theme's own.
    /// </summary>
    private ThemeColor? CustomBackgroundColor =>
        ThemeSurfaceCustomizer.TryParse(CustomBackgroundHex, out var color)
            ? color
            : null;

    private static Brush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    /// <summary>
    /// Tints offered in Settings. Hues only: the customiser decides the depth,
    /// so every one of these renders correctly in every theme.
    /// </summary>
    private static readonly string[] CustomBackgroundPresets =
    [
        "#3E8BFF",
        "#FF8FCB",
        "#B07CFF",
        "#4FD1A5",
        "#FFB020",
        "#4FE3FF"
    ];

    public string AppVersionLabel => $"Mimi {BuildInfo.Display}";

    private void ApplyLoadedSettings()
    {
        Volume = settings.Volume;
        ApplyPlaybackPreferenceSettings();
        UiLanguage = settings.UiLanguage;
        Loc.SetLanguage(UiLanguage);
        RefreshLocalizedChrome();
        // Restore the tint before the theme is applied: the first paint should
        // already be the palette the user left behind.
        isRestoringSettings = true;
        try
        {
            CustomBackgroundHex = string.IsNullOrWhiteSpace(settings.CustomBackground)
                ? CustomBackgroundPresets[0]
                : settings.CustomBackground;
            IsCustomBackgroundEnabled = settings.CustomBackgroundEnabled;
        }
        finally
        {
            isRestoringSettings = false;
        }

        UiTheme = settings.UiTheme;
        ApplyUiTheme(UiTheme);

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
        InternetRadioEnabled = settings.InternetRadioEnabled;
        OnlineCatalogEnabled = settings.OnlineCatalogEnabled;
        YouTubeSearchEnabled = settings.YouTubeSearchEnabled;

        SavedYouTubeVideos.Clear();
        foreach (var favorite in settings.YouTubeFavorites)
            SavedYouTubeVideos.Add(new YouTubeVideoItemViewModel(favorite.ToVideo()));

        RefreshYouTubeFavoriteStates();
        OnPropertyChanged(nameof(YouTubeListToggleLabel));
        WatchLibraryFolders = settings.WatchLibraryFolders;
        WatchedFolders.Clear();
        foreach (var folder in settings.WatchedFolders)
            WatchedFolders.Add(folder);
        SelectedWatchedFolder = WatchedFolders.FirstOrDefault();
        WindowPlacement = settings.WindowPlacement;
    }

    partial void OnUiLanguageChanged(string value)
    {
        var normalized = UiLanguages.Normalize(value);
        if (!string.Equals(value, normalized, StringComparison.Ordinal))
        {
            UiLanguage = normalized;
            return;
        }

        Loc.SetLanguage(normalized);
        RefreshLocalizedChrome();
        if (settingsLoaded && !isRestoringSettings)
            SaveSettings(force: true);
    }

    partial void OnUiThemeChanged(string value)
    {
        // The theme ComboBox briefly reports a null SelectedValue while
        // its item list is being re-synced (language change, theme
        // reload). That transient empty value is not a user choice and
        // must not reset or persist a different theme.
        if (string.IsNullOrWhiteSpace(value)) return;

        var normalized = ApplyUiTheme(value);
        if (!string.Equals(value, normalized, StringComparison.Ordinal))
        {
            UiTheme = normalized;
            return;
        }

        if (settingsLoaded && !isRestoringSettings)
            SaveSettings(force: true);
    }

    partial void OnIsCustomBackgroundEnabledChanged(bool value)
    {
        if (!value)
        {
            PushCustomBackground(null);
        }
        else if (CustomBackgroundColor is { } color)
        {
            PushCustomBackground(color);
        }
        else
        {
            // Enabled with unreadable text: fall back to the first preset
            // rather than silently doing nothing.
            CustomBackgroundHex = CustomBackgroundPresets[0];
            PushCustomBackground(CustomBackgroundColor);
        }

        OnPropertyChanged(nameof(HasCustomBackgroundProblem));
        OnPropertyChanged(nameof(CustomBackgroundPreview));

        if (settingsLoaded && !isRestoringSettings)
            SaveSettings(force: true);
    }

    partial void OnCustomBackgroundHexChanged(string value)
    {
        OnPropertyChanged(nameof(HasCustomBackgroundProblem));
        OnPropertyChanged(nameof(CustomBackgroundPreview));

        // Typing reaches the app only once the text is a colour; until then the
        // last good palette stays on screen instead of flashing black.
        if (IsCustomBackgroundEnabled && CustomBackgroundColor is { } color)
            PushCustomBackground(color);

        // Typing fires per keystroke; the throttled save keeps the disk quiet
        // while the palette still updates immediately.
        if (settingsLoaded && !isRestoringSettings)
            SaveSettings();
    }

    /// <summary>
    /// Hands the tint to the theme service and re-applies the current theme, so
    /// the change reaches every open surface at once.
    /// </summary>
    private void PushCustomBackground(ThemeColor? color)
    {
        if (themeService is null) return;

        themeService.CustomBackground = color;
        ApplyUiTheme(UiTheme);
    }

    /// <summary>
    /// Picks one of the preset tints from the swatch row.
    /// </summary>
    [RelayCommand]
    private void SelectCustomBackground(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return;

        CustomBackgroundHex = hex;
        IsCustomBackgroundEnabled = true;
    }

    /// <summary>
    /// Drops the tint and returns to the theme's own palette.
    /// </summary>
    [RelayCommand]
    private void ClearCustomBackground()
    {
        IsCustomBackgroundEnabled = false;
        CustomBackgroundHex = CustomBackgroundPresets[0];
    }

    private string ApplyUiTheme(string? value)
    {
        if (themeService is not null)
            themeService.CustomBackground = IsCustomBackgroundEnabled
                ? CustomBackgroundColor
                : null;

        var applied = themeService?.Apply(value) ?? BuiltInThemes.NormalizeId(value);
        RefreshThemeSwatches();
        return applied;
    }

    private void RefreshThemeSwatches()
    {
        ThemeSwatches.Clear();

        var theme = themeService?.CurrentTheme ?? BuiltInThemes.Default;
        foreach (var token in ThemeSwatch.PreviewTokens)
        {
            if (theme.TryGetColor(token, out var color))
                ThemeSwatches.Add(new ThemeSwatch(token, color));
        }
    }

    /// <summary>
    /// Aligns <see cref="ThemeOptions"/> with the themes the service can
    /// apply right now. Existing option objects are kept (only their
    /// display names refresh) so a language change never disturbs the
    /// ComboBox selection; options are added/removed/moved only when the
    /// set of available themes actually changes.
    /// </summary>
    private void RefreshThemeOptions()
    {
        var themes = themeService?.AvailableThemes ?? BuiltInThemes.All;

        for (var i = ThemeOptions.Count - 1; i >= 0; i--)
        {
            if (!themes.Any(theme => string.Equals(
                    theme.Id,
                    ThemeOptions[i].Id,
                    StringComparison.OrdinalIgnoreCase)))
            {
                ThemeOptions.RemoveAt(i);
            }
        }

        for (var i = 0; i < themes.Count; i++)
        {
            var theme = themes[i];
            var existing = ThemeOptions.FirstOrDefault(option =>
                string.Equals(
                    option.Id,
                    theme.Id,
                    StringComparison.OrdinalIgnoreCase));
            var displayName = GetThemeDisplayName(theme);
            if (existing is null)
            {
                ThemeOptions.Insert(i, new ThemeOptionViewModel(theme, displayName));
            }
            else
            {
                // A reloaded community theme can change its colours without
                // changing its id, so the sample is refreshed too.
                existing.Apply(theme, displayName);
                var currentIndex = ThemeOptions.IndexOf(existing);
                if (currentIndex != i)
                    ThemeOptions.Move(currentIndex, i);
            }
        }
    }

    private string GetThemeDisplayName(ThemeDefinition theme) => theme.Id switch
    {
        BuiltInThemes.NeonStageId => Loc["Theme_NeonStage"],
        BuiltInThemes.SakuraDreamId => Loc["Theme_SakuraDream"],
        BuiltInThemes.MidnightLiveId => Loc["Theme_MidnightLive"],
        BuiltInThemes.MonoFocusId => Loc["Theme_MonoFocus"],
        _ => theme.Name
    };

    private void RefreshLocalizedChrome()
    {
        OnPropertyChanged(nameof(SectionTitle));
        OnPropertyChanged(nameof(SelectedTracksCountLabel));
        OnPropertyChanged(nameof(SelectedTrackFavoriteMenuLabel));
        OnPropertyChanged(nameof(EffectiveVolumePercentLabel));
        OnPropertyChanged(nameof(MuteButtonLabel));
        OnPropertyChanged(nameof(ShuffleButtonLabel));
        OnPropertyChanged(nameof(RepeatModeLabel));
        OnPropertyChanged(nameof(QueueCountLabel));
        OnPropertyChanged(nameof(QueueStatusLabel));
        OnPropertyChanged(nameof(OpenWithMimiStatusLabel));
        RefreshLyricsLocalizedLabels();
        RefreshRadioStatusLabel();
        NotifySortLabelsChanged();
        RefreshPlaybackStateFilterOptions();
        RefreshFilterOptions();
        RefreshThemeOptions();

        if (lastAudioOutputStatus is { } audioOutputStatus)
            RefreshAudioOutputStatus(audioOutputStatus);

        foreach (var track in Tracks)
            track.RefreshLocalizedLabels();
        foreach (var playlist in Playlists)
            playlist.RefreshLocalizedLabels();

        if (CurrentRadioStation is not null)
        {
            RefreshRadioLocalizedLabels();
        }
        else if (CurrentTrack is null)
        {
            NowPlayingTitle = Loc["NowPlaying_ChooseSong"];
            NowPlayingSubtitle = Loc["NowPlaying_NextFavoriteWaiting"];
        }
        else
        {
            NowPlayingSubtitle = CurrentTrack.Subtitle;
        }

        UpdateTrackCount();
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

    partial void OnInternetRadioEnabledChanged(bool value)
    {
        if (!value)
            DisableInternetRadio();

        if (settingsLoaded && !isRestoringSettings)
            SaveSettings();
    }

    partial void OnOnlineCatalogEnabledChanged(bool value)
    {
        if (!value)
            DisableOnlineCatalog();

        if (settingsLoaded && !isRestoringSettings)
            SaveSettings();
    }

    partial void OnYouTubeSearchEnabledChanged(bool value)
    {
        if (!value)
            DisableYouTube();

        if (settingsLoaded && !isRestoringSettings)
            SaveSettings();
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
        OnPropertyChanged(nameof(SelectedTrackFavoriteMenuLabel));
        if (settingsLoaded && !isRestoringSettings)
            SaveSettings();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        IsRadioOpen = false;
        IsOnlineOpen = false;
        IsYouTubeOpen = false;
        IsSettingsOpen = true;

        // Settings shows where the key file lives, so it is the natural place
        // to notice a key the user just added.
        RefreshYouTubeAvailability();
    }

    [RelayCommand]
    private void CloseSettings() => IsSettingsOpen = false;

    [RelayCommand]
    private void ResetSettings()
    {
        if (!persistedStateRestored)
        {
            dialogService.ShowInformation(
                Loc["Settings_StillLoading"],
                Loc["Settings_Title"]);
            return;
        }

        if (!dialogService.Confirm(
                Loc["Settings_ResetConfirm"],
                Loc["Settings_ResetTitle"]))
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

        ConfigureLibraryFolderWatcher();
        SaveSettings(force: true);
        dialogService.ShowInformation(
            Loc["Settings_ResetDone"],
            Loc["Settings_Title"]);
    }

    [RelayCommand]
    private void OpenDataFolder() => OpenFolder(DataDirectory);

    [RelayCommand]
    private void OpenLogFolder() => OpenFolder(LogDirectory);

    [RelayCommand]
    private void OpenThemesFolder() => OpenFolder(ThemesDirectory);

    /// <summary>
    /// Writes the theme in use into the themes folder under a fresh id, then
    /// switches to that copy. Editing a real file with every token already
    /// filled in beats hand-writing 33 colours from the documentation, and the
    /// copy stays a copy: the built-in theme it came from is untouched.
    /// </summary>
    [RelayCommand]
    private void DuplicateThemeForEditing()
    {
        if (themeService is null) return;

        // Deliberately the untinted palette: a background tint is a screen
        // setting, not part of the theme file. A copy that baked the tint in
        // would keep looking pink even after the tint is turned off.
        var source = themeService.Catalog.GetOrDefault(themeService.CurrentThemeId);
        var id = ThemeFileWriter.UniqueId(source, themeService.Catalog);
        var name = $"{source.Name} · {Loc["Settings_ThemeCopySuffix"]}";

        try
        {
            Directory.CreateDirectory(ThemesDirectory);
            var path = Path.Combine(ThemesDirectory, $"{id}.json");
            File.WriteAllText(
                path,
                ThemeFileWriter.ToJson(source, id, name),
                new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            themeService.ReloadCommunityThemes();
            RefreshThemeOptions();

            // Selecting the copy proves the file the user is about to edit is
            // the one on screen.
            UiTheme = id;

            dialogService.ShowInformation(
                Loc.Format("Settings_DuplicateThemeDone", path),
                Loc["Settings_Title"]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warning(ex, "Could not duplicate theme {ThemeId}", source.Id);
            dialogService.ShowWarning(
                Loc.Format("Settings_DuplicateThemeFailed", ex.Message),
                Loc["Settings_Title"]);
        }
    }

    [RelayCommand]
    private void ReloadThemes()
    {
        if (themeService is null) return;

        themeService.ReloadCommunityThemes();
        RefreshThemeOptions();
        RefreshThemeSwatches();

        // If the previously selected community theme was removed or became
        // invalid, the service has already fallen back; mirror that here
        // so the picker and persisted setting stay in sync.
        var effectiveThemeId = themeService.CurrentThemeId;
        if (!string.Equals(UiTheme, effectiveThemeId, StringComparison.Ordinal))
            UiTheme = effectiveThemeId;
    }

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
                Loc.Format("Settings_CouldNotOpenFolder", ex.Message),
                Loc["Settings_Title"]);
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
        settings.IsMuted = IsMuted;
        settings.ShuffleEnabled = IsShuffleEnabled;
        settings.RepeatMode = RepeatMode.ToString();
        settings.VisualizerMode = VisualizerMode.ToString();
        settings.UiLanguage = UiLanguages.Normalize(UiLanguage);
        settings.CustomBackgroundEnabled =
            IsCustomBackgroundEnabled && CustomBackgroundColor is not null;
        settings.CustomBackground = CustomBackgroundColor is { } tint
            ? ThemeSurfaceCustomizer.ToDisplayString(tint)
            : string.Empty;
        settings.UiTheme =
            themeService?.NormalizeId(UiTheme) ??
            BuiltInThemes.NormalizeId(UiTheme);
        settings.RestorePlaybackSession = RestorePlaybackSession;
        settings.RestoreLibraryView = RestoreLibraryView;
        settings.RestoreWindowPlacement = RestoreWindowPlacement;
        settings.AutoUpgradeLegacyCoverArt = AutoUpgradeLegacyCoverArt;
        settings.InternetRadioEnabled = InternetRadioEnabled;
        settings.OnlineCatalogEnabled = OnlineCatalogEnabled;
        settings.YouTubeSearchEnabled = YouTubeSearchEnabled;
        settings.YouTubeFavorites =
        [
            .. SavedYouTubeVideos.Select(saved => new SavedYouTubeVideo(
                saved.VideoId,
                saved.Title,
                saved.ChannelTitle,
                saved.ThumbnailUrl?.AbsoluteUri))
        ];
        settings.WatchLibraryFolders = WatchLibraryFolders;
        settings.WatchedFolders = [.. WatchedFolders];
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

        if (RestorePlaybackSession && CurrentTrack is not null)
        {
            EnsurePlaybackQueueForCurrentTrack();
            var queueSnapshot = GetPlaybackQueueSnapshot();
            settings.PlaybackSession = new PlaybackSessionSettings
            {
                CurrentTrackPath = CurrentTrack.FilePath,
                PositionSeconds = Math.Max(
                    0,
                    audioPlayer.CurrentTime.TotalSeconds),
                QueueTrackPaths = queueSnapshot.SourcePaths,
                HistoryTrackPaths = queueSnapshot.HistoryPaths,
                UpcomingTrackPaths = queueSnapshot.UpcomingPaths
            };
        }
        else if (CurrentRadioStation is null)
        {
            settings.PlaybackSession = null;
        }
        // Internet Radio is session-only. While a station is current, keep
        // any previously saved local session untouched instead of writing a
        // stream URL or erasing the local song merely because Radio opened.
    }

    private void RestorePersistedState()
    {
        if (persistedStateRestored || disposed) return;
        persistedStateRestored = true;

        if (RestoreLibraryView)
            RestoreLibraryViewState();

        RestorePlaybackSessionState();
        CapturePersistedState();
        QueuePendingLibraryFolderChanges();
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
                TranslateKnownFilterValue(
                    saved.ArtistFilter,
                    "Filter_AllArtists",
                    "Metadata_UnknownArtist"),
                AllArtistsFilter);
            SelectedAlbumFilter = GetValidFilter(
                AlbumFilterOptions,
                TranslateKnownFilterValue(
                    saved.AlbumFilter,
                    "Filter_AllAlbums",
                    "Metadata_UnknownAlbum"),
                AllAlbumsFilter);
            SelectedGenreFilter = GetValidFilter(
                GenreFilterOptions,
                TranslateKnownFilterValue(
                    saved.GenreFilter,
                    "Filter_AllGenres",
                    "Metadata_UnknownGenre"),
                AllGenresFilter);
            SelectedFormatFilter = GetValidFilter(
                FormatFilterOptions,
                TranslateKnownFilterValue(
                    saved.FormatFilter,
                    "Filter_AllFormats"),
                AllFormatsFilter);
            SelectedPlaybackStateFilter = GetValidFilter(
                PlaybackStateFilterOptions,
                TranslateKnownFilterValue(
                    saved.PlaybackStateFilter,
                    "Filter_AllStates",
                    "Filter_Ready",
                    "Filter_HasError",
                    "Filter_Missing"),
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

            // If the user has already selected a row before this startup
            // restore gets to run, keep that newer selection instead of
            // snapping back to the selection saved by the previous session.
            SelectedTrack ??= Tracks.FirstOrDefault(track => string.Equals(
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

        // A track can already be current here only when the user started
        // playback while asynchronous library initialization was still in
        // flight (for example, a very fast first click after the window
        // appeared). That explicit choice is newer than the saved session,
        // so restoring must not load/pause the old track over it.
        if (CurrentTrack is not null)
        {
            Log.Information(
                "Skipped saved playback-session restore because playback " +
                "was already started during startup for {FilePath}",
                CurrentTrack.FilePath);
            return;
        }

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
            ApplyEffectiveVolume();
            RestorePlaybackQueue(session, track);
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
