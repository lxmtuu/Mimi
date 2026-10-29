using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mimi.Behaviors;
using Mimi.Controls;
using Mimi.Core.Playback;
using Mimi.Core.Text;
using Mimi.Models;
using Mimi.Services;
using Serilog;

namespace Mimi.ViewModels;

public enum LibraryViewMode
{
    Library,
    Favorites,
    Recent,
    Playlist
}

public enum TrackSortField
{
    None,
    Title,
    Artist,
    Album,
    Genre,
    Year,
    Duration,
    Format
}

/// <summary>
/// Owns the main window's presentation and playback state. Native dialogs,
/// file persistence, library scanning, and audio output sit behind injected
/// services so this class contains application behavior rather than WPF
/// control plumbing.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly IAudioPlayer audioPlayer;
    private readonly ILibraryService libraryService;
    private readonly ILibraryCatalog libraryCatalog;
    private readonly IUserDataService userDataService;
    private readonly IDialogService dialogService;
    private readonly ICoverArtOptimizer? coverArtOptimizer;
    private readonly ILibraryFolderWatcher? libraryFolderWatcher;
    private readonly IFileAssociationService? fileAssociationService;
    private readonly Dispatcher uiDispatcher = Dispatcher.CurrentDispatcher;
    private readonly DispatcherTimer positionTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(150)
    };
    private readonly CancellationTokenSource lifetimeCancellation = new();

    private const string AllArtistsFilter = "All artists";
    private const string AllAlbumsFilter = "All albums";
    private const string AllGenresFilter = "All genres";
    private const string AllFormatsFilter = "All formats";
    private const string AllPlaybackStatesFilter = "All states";
    private const string ReadyPlaybackStateFilter = "Ready";
    private const string ErrorPlaybackStateFilter = "Has error";
    private const string MissingPlaybackStateFilter = "Missing";

    private AppSettings settings = new();
    private HashSet<string> favorites = new(StringComparer.OrdinalIgnoreCase);
    private string normalizedSearchQuery = string.Empty;
    private CancellationTokenSource? scanCancellation;
    private CancellationTokenSource? searchCancellation;
    private HashSet<string>? catalogSearchPaths;
    private IReadOnlyList<string> selectedPlaylistTrackPaths = [];
    private HashSet<string> selectedPlaylistPathSet =
        new(StringComparer.OrdinalIgnoreCase);
    private int searchRequestVersion;
    private bool catalogSearchActive;
    private TrackSortField trackSortField;
    private ListSortDirection trackSortDirection = ListSortDirection.Ascending;
    private long? currentPlayHistoryId;
    private bool changingProgress;
    private bool userSeeking;
    private bool settingsLoaded;
    private bool catalogAvailable;
    private bool libraryInitialized;
    private bool disposed;
    private DateTime lastSettingsSaveUtc = DateTime.MinValue;
    private DateTime lastVisualizerDiagnosticWrite = DateTime.MinValue;

    [ObservableProperty]
    private TrackItemViewModel? selectedTrack;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TaskbarProgressState))]
    [NotifyPropertyChangedFor(nameof(TaskbarDescription))]
    private TrackItemViewModel? currentTrack;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPlaylistReorderEnabled))]
    private string searchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SectionTitle))]
    [NotifyPropertyChangedFor(nameof(IsPlaylistView))]
    [NotifyPropertyChangedFor(nameof(IsPlaylistReorderEnabled))]
    private LibraryViewMode activeView;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SectionTitle))]
    [NotifyPropertyChangedFor(nameof(HasSelectedPlaylist))]
    [NotifyPropertyChangedFor(nameof(IsPlaylistView))]
    [NotifyPropertyChangedFor(nameof(IsPlaylistReorderEnabled))]
    private PlaylistItemViewModel? selectedPlaylist;

    [ObservableProperty]
    private string trackCountLabel = "No songs yet - add some music to begin";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TaskbarDescription))]
    private string nowPlayingTitle = "Choose a song";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TaskbarDescription))]
    private string nowPlayingSubtitle = "Your next favorite is waiting";

    [ObservableProperty]
    private string? currentCoverArtPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TaskbarPlayButtonVisibility))]
    [NotifyPropertyChangedFor(nameof(TaskbarPauseButtonVisibility))]
    [NotifyPropertyChangedFor(nameof(TaskbarProgressState))]
    private bool isPlaying;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TaskbarProgressValue))]
    private double progressPercent;

    [ObservableProperty]
    private string currentTimeLabel = "0:00";

    [ObservableProperty]
    private string durationLabel = "0:00";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VolumePercentLabel))]
    [NotifyPropertyChangedFor(nameof(EffectiveVolumePercentLabel))]
    private double volume = 0.7;

    [ObservableProperty]
    private VisualizerDisplayMode visualizerMode = VisualizerDisplayMode.NeonSpectrum;

    [ObservableProperty]
    private string visualizerMetricsLabel = "0 FPS · 0.00 ms";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelScanCommand))]
    private bool isScanning;

    [ObservableProperty]
    private double scanProgressPercent;

    [ObservableProperty]
    private string scanStatusLabel = string.Empty;

    [ObservableProperty]
    private bool isSearchingCatalog;

    [ObservableProperty]
    private string searchStatusLabel = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveAdvancedFilters))]
    private string selectedArtistFilter = AllArtistsFilter;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveAdvancedFilters))]
    private string selectedAlbumFilter = AllAlbumsFilter;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveAdvancedFilters))]
    private string selectedGenreFilter = AllGenresFilter;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveAdvancedFilters))]
    private string selectedFormatFilter = AllFormatsFilter;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveAdvancedFilters))]
    private string selectedPlaybackStateFilter = AllPlaybackStatesFilter;

    public MainWindowViewModel(
        IAudioPlayer audioPlayer,
        ILibraryService libraryService,
        ILibraryCatalog libraryCatalog,
        IUserDataService userDataService,
        IDialogService dialogService,
        ICoverArtOptimizer? coverArtOptimizer = null,
        ILibraryFolderWatcher? libraryFolderWatcher = null,
        IFileAssociationService? fileAssociationService = null)
    {
        this.audioPlayer = audioPlayer;
        this.libraryService = libraryService;
        this.libraryCatalog = libraryCatalog;
        this.userDataService = userDataService;
        this.dialogService = dialogService;
        this.coverArtOptimizer = coverArtOptimizer;
        this.libraryFolderWatcher = libraryFolderWatcher;
        this.fileAssociationService = fileAssociationService;

        TracksView = CollectionViewSource.GetDefaultView(Tracks);
        TracksView.Filter = FilterTrack;
        SelectedTracks.CollectionChanged += SelectedTracks_CollectionChanged;
        WatchedFolders.CollectionChanged += WatchedFolders_CollectionChanged;

        userDataService.MigrateLegacyDataIfNeeded();
        settings = userDataService.LoadSettings();
        ApplyLoadedSettings();
        settingsLoaded = true;

        if (libraryFolderWatcher is not null)
        {
            libraryFolderWatcher.ChangesDetected +=
                LibraryFolderWatcher_ChangesDetected;
            ConfigureLibraryFolderWatcher();
        }

        RefreshOpenWithMimiRegistration();

        audioPlayer.PlaybackChanged += AudioPlayer_PlaybackChanged;
        audioPlayer.PlaybackEnded += AudioPlayer_PlaybackEnded;
        positionTimer.Tick += PositionTimer_Tick;
        positionTimer.Start();

        RefreshPlaybackState();
        UpdateTrackCount();

        InitializationTask = InitializeLibraryAsync();
    }

    public event EventHandler? StageToggleRequested;

    public Task InitializationTask { get; }

    public ObservableCollection<TrackItemViewModel> Tracks { get; } = [];

    public ObservableCollection<PlaylistItemViewModel> Playlists { get; } = [];

    public ObservableCollection<TrackItemViewModel> SelectedTracks { get; } = [];

    public ObservableCollection<string> ArtistFilterOptions { get; } =
        [AllArtistsFilter];

    public ObservableCollection<string> AlbumFilterOptions { get; } =
        [AllAlbumsFilter];

    public ObservableCollection<string> GenreFilterOptions { get; } =
        [AllGenresFilter];

    public ObservableCollection<string> FormatFilterOptions { get; } =
        [AllFormatsFilter];

    public IReadOnlyList<string> PlaybackStateFilterOptions { get; } =
    [
        AllPlaybackStatesFilter,
        ReadyPlaybackStateFilter,
        ErrorPlaybackStateFilter,
        MissingPlaybackStateFilter
    ];

    public ICollectionView TracksView { get; }

    public bool HasSelectedTracks => SelectedTracks.Count > 0;

    public bool HasMultipleSelectedTracks => SelectedTracks.Count > 1;

    public string SelectedTracksCountLabel =>
        $"{SelectedTracks.Count} selected";

    public bool HasActiveAdvancedFilters =>
        !string.Equals(
            SelectedArtistFilter,
            AllArtistsFilter,
            StringComparison.Ordinal) ||
        !string.Equals(
            SelectedAlbumFilter,
            AllAlbumsFilter,
            StringComparison.Ordinal) ||
        !string.Equals(
            SelectedGenreFilter,
            AllGenresFilter,
            StringComparison.Ordinal) ||
        !string.Equals(
            SelectedFormatFilter,
            AllFormatsFilter,
            StringComparison.Ordinal) ||
        !string.Equals(
            SelectedPlaybackStateFilter,
            AllPlaybackStatesFilter,
            StringComparison.Ordinal);

    public string TitleSortLabel =>
        GetSortLabel("TITLE", TrackSortField.Title);

    public string ArtistSortLabel =>
        GetSortLabel("ARTIST", TrackSortField.Artist);

    public string AlbumSortLabel =>
        GetSortLabel("ALBUM", TrackSortField.Album);

    public string GenreSortLabel =>
        GetSortLabel("GENRE", TrackSortField.Genre);

    public string YearSortLabel =>
        GetSortLabel("YEAR", TrackSortField.Year);

    public string DurationSortLabel =>
        GetSortLabel("TIME", TrackSortField.Duration);

    public string FormatSortLabel =>
        GetSortLabel("FORMAT", TrackSortField.Format);

    public bool HasSelectedPlaylist => SelectedPlaylist is not null;

    public bool IsPlaylistView =>
        ActiveView == LibraryViewMode.Playlist && SelectedPlaylist is not null;

    public bool IsPlaylistReorderEnabled =>
        IsPlaylistView && normalizedSearchQuery.Length == 0;

    public string SectionTitle => ActiveView switch
    {
        LibraryViewMode.Favorites => "Your favorites",
        LibraryViewMode.Recent => "Recently played",
        LibraryViewMode.Playlist => SelectedPlaylist?.Name ?? "Playlist",
        _ => "Your library"
    };

    public Func<VisualizerFrame> VisualizerFrameProvider =>
        audioPlayer.GetVisualizerFrame;

    public void BeginSeek() => userSeeking = true;

    public void EndSeek() => userSeeking = false;

    public void UpdateVisualizerMetrics(
        double framesPerSecond,
        double averageDrawMilliseconds) =>
        VisualizerMetricsLabel =
            $"{framesPerSecond:0} FPS · {averageDrawMilliseconds:0.00} ms";

    partial void OnSearchTextChanged(string value)
    {
        normalizedSearchQuery = TextSearch.Normalize(value);
        catalogSearchActive = false;
        ResetTrackSort();
        OnPropertyChanged(nameof(IsPlaylistReorderEnabled));
        QueueCatalogSearch(value);
        ConfigureTracksView();
        SaveSettings();
    }

    partial void OnActiveViewChanged(LibraryViewMode value)
    {
        ResetTrackSort();
        ConfigureTracksView();
        SaveSettings();
    }

    partial void OnSelectedArtistFilterChanged(string value)
    {
        ApplyFilter();
        SaveSettings();
    }

    partial void OnSelectedAlbumFilterChanged(string value)
    {
        ApplyFilter();
        SaveSettings();
    }

    partial void OnSelectedGenreFilterChanged(string value)
    {
        ApplyFilter();
        SaveSettings();
    }

    partial void OnSelectedFormatFilterChanged(string value)
    {
        ApplyFilter();
        SaveSettings();
    }

    partial void OnSelectedPlaybackStateFilterChanged(string value)
    {
        ApplyFilter();
        SaveSettings();
    }

    private void SelectedTracks_CollectionChanged(
        object? sender,
        NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasSelectedTracks));
        OnPropertyChanged(nameof(HasMultipleSelectedTracks));
        OnPropertyChanged(nameof(SelectedTracksCountLabel));
    }

    partial void OnSelectedPlaylistChanged(PlaylistItemViewModel? value)
    {
        if (value is not null)
        {
            IsSettingsOpen = false;
            _ = ShowPlaylistAsync(value);
            SaveSettings();
            return;
        }

        ClearSelectedPlaylistOrder();
        if (ActiveView == LibraryViewMode.Playlist)
            ActiveView = LibraryViewMode.Library;
        else
            ApplyFilter();

        SaveSettings();
    }

    partial void OnVolumeChanged(double value)
    {
        // Moving the slider away from silence is the natural way to leave
        // mute; changing it while settings are first applied is not input.
        if (settingsLoaded && IsMuted && value > 0)
            IsMuted = false;
        else
            ApplyEffectiveVolume();

        // The saved value is assigned before settingsLoaded is set. Saving it
        // back during that initial assignment would be redundant.
        if (!settingsLoaded) return;

        settings.Volume = value;
        SaveSettings();
    }

    partial void OnProgressPercentChanged(double value)
    {
        // Timer-driven updates always run under changingProgress, so any
        // remaining change is user input - mouse or keyboard alike.
        if (changingProgress || audioPlayer.Duration <= TimeSpan.Zero) return;

        audioPlayer.Seek(TimeSpan.FromSeconds(
            audioPlayer.Duration.TotalSeconds * value / 100d));
        SaveSettings();
    }

    [RelayCommand]
    private void ShowHome()
    {
        IsSettingsOpen = false;
        SelectedPlaylist = null;
        ActiveView = LibraryViewMode.Library;
        SearchText = string.Empty;
        ApplyFilter();
    }

    [RelayCommand]
    private void ShowLibrary()
    {
        IsSettingsOpen = false;
        SelectedPlaylist = null;
        ActiveView = LibraryViewMode.Library;
    }

    [RelayCommand]
    private void ToggleFavoritesView()
    {
        IsSettingsOpen = false;
        SelectedPlaylist = null;
        ActiveView = ActiveView == LibraryViewMode.Favorites
            ? LibraryViewMode.Library
            : LibraryViewMode.Favorites;
    }

    [RelayCommand]
    private void ShowRecent()
    {
        IsSettingsOpen = false;
        SelectedPlaylist = null;
        ActiveView = LibraryViewMode.Recent;
    }

    [RelayCommand]
    private async Task ShowPlaylist(PlaylistItemViewModel? playlist)
    {
        if (playlist is null) return;

        IsSettingsOpen = false;

        if (SelectedPlaylist != playlist)
            SelectedPlaylist = playlist;
        else
            await ShowPlaylistAsync(playlist);
    }

    [RelayCommand]
    private async Task CreatePlaylist()
    {
        await InitializationTask;
        if (!EnsureCatalogForPlaylists()) return;

        var name = dialogService.PromptText(
            "New playlist",
            "Name your new playlist:",
            "New playlist");
        if (string.IsNullOrWhiteSpace(name)) return;

        try
        {
            var created = await libraryCatalog.CreatePlaylistAsync(
                name,
                lifetimeCancellation.Token);
            var item = new PlaylistItemViewModel(created);
            InsertPlaylistSorted(item);
            SelectedPlaylist = item;
        }
        catch (OperationCanceledException) when (
            lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (ArgumentException ex)
        {
            dialogService.ShowWarning(ex.Message, "Playlist");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not create playlist");
            dialogService.ShowWarning(
                $"Couldn't create that playlist.\n\n{ex.Message}",
                "Playlist");
        }
    }

    [RelayCommand]
    private async Task RenameSelectedPlaylist()
    {
        var playlist = SelectedPlaylist;
        if (playlist is null || !EnsureCatalogForPlaylists()) return;

        var name = dialogService.PromptText(
            "Rename playlist",
            "Enter a new name for this playlist:",
            playlist.Name);
        if (string.IsNullOrWhiteSpace(name) ||
            string.Equals(name.Trim(), playlist.Name, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            if (await libraryCatalog.RenamePlaylistAsync(
                    playlist.Id,
                    name,
                    lifetimeCancellation.Token))
            {
                playlist.Rename(name);
                SortPlaylists();
                OnPropertyChanged(nameof(SectionTitle));
            }
            else
            {
                await RefreshPlaylistsAsync();
            }
        }
        catch (OperationCanceledException) when (
            lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (ArgumentException ex)
        {
            dialogService.ShowWarning(ex.Message, "Playlist");
        }
        catch (Exception ex)
        {
            Log.Warning(
                ex,
                "Could not rename playlist {PlaylistId}",
                playlist.Id);
            dialogService.ShowWarning(
                $"Couldn't rename that playlist.\n\n{ex.Message}",
                "Playlist");
        }
    }

    [RelayCommand]
    private async Task DeleteSelectedPlaylist()
    {
        var playlist = SelectedPlaylist;
        if (playlist is null || !EnsureCatalogForPlaylists()) return;

        if (!dialogService.Confirm(
                $"Delete playlist \"{playlist.Name}\"?\n\n" +
                "The songs will stay in your library.",
                "Delete playlist"))
        {
            return;
        }

        try
        {
            if (!await libraryCatalog.DeletePlaylistAsync(
                    playlist.Id,
                    lifetimeCancellation.Token))
            {
                await RefreshPlaylistsAsync();
                return;
            }

            SelectedPlaylist = null;
            Playlists.Remove(playlist);
            ActiveView = LibraryViewMode.Library;
        }
        catch (OperationCanceledException) when (
            lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Log.Warning(
                ex,
                "Could not delete playlist {PlaylistId}",
                playlist.Id);
            dialogService.ShowWarning(
                $"Couldn't delete that playlist.\n\n{ex.Message}",
                "Playlist");
        }
    }

    [RelayCommand]
    private async Task PlayPlaylist(PlaylistItemViewModel? playlist)
    {
        playlist ??= SelectedPlaylist;
        if (playlist is null) return;

        if (SelectedPlaylist != playlist)
            SelectedPlaylist = playlist;

        await ShowPlaylistAsync(playlist);

        var firstTrack = TracksView.Cast<TrackItemViewModel>().FirstOrDefault();
        if (firstTrack is null)
        {
            dialogService.ShowInformation(
                "This playlist is empty. Add songs from the track list's " +
                "\"Add to playlist\" menu.");
            return;
        }

        TryPlayTrackFromCurrentView(firstTrack, showError: true);
    }

    [RelayCommand]
    private async Task PlaySelectedPlaylist() =>
        await PlayPlaylist(SelectedPlaylist);

    [RelayCommand]
    private async Task AddSelectedTrackToPlaylist(
        PlaylistItemViewModel? playlist)
    {
        var tracks = GetSelectedTracksSnapshot();
        if (playlist is null || tracks.Length == 0) return;
        await AddTracksToPlaylistAsync(playlist, tracks);
    }

    [RelayCommand]
    private async Task RemoveTrackFromSelectedPlaylist(
        TrackItemViewModel? track)
    {
        if (track is not null)
            await RemoveTrackFromSelectedPlaylistAsync(track);
    }

    [RelayCommand]
    private async Task RemoveSelectedTrackFromPlaylist()
    {
        if (!IsPlaylistView) return;

        foreach (var track in GetSelectedTracksSnapshot())
            await RemoveTrackFromSelectedPlaylistAsync(track);
    }

    [RelayCommand]
    private async Task MovePlaylistTrack(PlaylistMoveRequest? request)
    {
        if (request?.Item is TrackItemViewModel track)
            await MovePlaylistTrackAsync(track, request.TargetPosition);
    }

    [RelayCommand]
    private async Task MoveSelectedPlaylistTrackUp()
    {
        if (SelectedTrack is null) return;

        var current = IndexInSelectedPlaylist(SelectedTrack.FilePath);
        if (current > 0)
            await MovePlaylistTrackAsync(SelectedTrack, current - 1);
    }

    [RelayCommand]
    private async Task MoveSelectedPlaylistTrackDown()
    {
        if (SelectedTrack is null) return;

        var current = IndexInSelectedPlaylist(SelectedTrack.FilePath);
        if (current >= 0 && current + 1 < selectedPlaylistTrackPaths.Count)
            await MovePlaylistTrackAsync(SelectedTrack, current + 1);
    }

    [RelayCommand]
    private void SortTracks(string? fieldName)
    {
        if (ActiveView == LibraryViewMode.Playlist) return;
        if (!Enum.TryParse<TrackSortField>(
                fieldName,
                ignoreCase: true,
                out var field) ||
            field == TrackSortField.None)
        {
            return;
        }

        if (trackSortField == field)
        {
            trackSortDirection =
                trackSortDirection == ListSortDirection.Ascending
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending;
        }
        else
        {
            trackSortField = field;
            trackSortDirection = ListSortDirection.Ascending;
        }

        NotifySortLabelsChanged();
        ConfigureTracksView();
        SaveSettings();
    }

    [RelayCommand]
    private void ClearLibraryFilters()
    {
        SelectedArtistFilter = AllArtistsFilter;
        SelectedAlbumFilter = AllAlbumsFilter;
        SelectedGenreFilter = AllGenresFilter;
        SelectedFormatFilter = AllFormatsFilter;
        SelectedPlaybackStateFilter = AllPlaybackStatesFilter;
        ResetTrackSort();

        if (SearchText.Length > 0)
            SearchText = string.Empty;
        else
            ConfigureTracksView();

        SaveSettings();
    }

    [RelayCommand]
    private async Task AddFiles()
    {
        if (IsScanning) return;

        IsSettingsOpen = false;
        var fileNames = dialogService.PickAudioFiles();
        if (fileNames.Count > 0)
            await ScanAndAddFilesAsync(fileNames);
    }

    [RelayCommand]
    private async Task AddFolder()
    {
        if (IsScanning) return;

        IsSettingsOpen = false;
        var folder = dialogService.PickMusicFolder();
        if (string.IsNullOrWhiteSpace(folder)) return;

        var result = await ScanAndAddFolderAsync(folder);
        if (result?.SkippedDirectories > 0)
        {
            dialogService.ShowInformation(
                $"{result.SkippedDirectories} folder(s) could not be read and were skipped.");
        }
    }

    [RelayCommand]
    private async Task AddDroppedPaths(string[]? paths)
    {
        if (paths is null || paths.Length == 0 || IsScanning) return;

        IsSettingsOpen = false;
        var files = paths.Where(File.Exists).ToArray();
        var folders = paths.Where(Directory.Exists).ToArray();
        var skippedDirectories = 0;

        if (files.Length > 0)
            await ScanAndAddFilesAsync(files);

        foreach (var folder in folders)
        {
            var result = await ScanAndAddFolderAsync(folder);
            if (result is not null)
                skippedDirectories += result.SkippedDirectories;
        }

        if (skippedDirectories > 0)
        {
            dialogService.ShowInformation(
                $"{skippedDirectories} folder(s) could not be read and were skipped.");
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancelScan))]
    private void CancelScan()
    {
        scanCancellation?.Cancel();
        ScanStatusLabel = "Canceling scan…";
    }

    private bool CanCancelScan() => IsScanning;

    [RelayCommand]
    private void PlayPause()
    {
        if (CurrentTrack is null)
        {
            if (!TryPlayNextFromQueue(showErrors: false) &&
                SelectedTrack is not null)
            {
                TryPlayTrackFromCurrentView(SelectedTrack, showError: true);
            }
        }
        else
        {
            audioPlayer.Toggle();
        }

        RefreshPlaybackState();
        SaveSettings();
    }

    [RelayCommand]
    private void PlayTrack(TrackItemViewModel? track)
    {
        if (track is not null)
            TryPlayTrackFromCurrentView(track, showError: true);
    }

    [RelayCommand]
    private void Previous() => TryPlayPreviousFromQueue();

    [RelayCommand]
    private void Next() => TryPlayNextFromQueue(showErrors: true);

    [RelayCommand]
    private async Task ToggleFavorite(TrackItemViewModel? track)
    {
        if (track is null) return;

        var newValue = !track.IsFavorite;
        SetFavorite(track, newValue);

        if (catalogAvailable)
        {
            try
            {
                await libraryCatalog.SetFavoriteAsync(
                    track.FilePath,
                    newValue,
                    lifetimeCancellation.Token);
            }
            catch (OperationCanceledException) when (
                lifetimeCancellation.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                SetFavorite(track, !newValue);
                Log.Warning(ex, "Could not save favorite for {FilePath}", track.FilePath);
                dialogService.ShowWarning(
                    $"Couldn't save favorites.\n\n{ex.Message}",
                    "Mimi");
            }
        }
        else
        {
            SaveFavorites();
        }

        ApplyFilter();
    }

    [RelayCommand]
    private async Task ToggleSelectedFavorites()
    {
        var tracks = GetSelectedTracksSnapshot();
        if (tracks.Length == 0) return;

        var newValue = tracks.Any(track => !track.IsFavorite);
        var originalValues = tracks.ToDictionary(
            track => track,
            track => track.IsFavorite);

        foreach (var track in tracks)
            SetFavorite(track, newValue);

        if (catalogAvailable)
        {
            try
            {
                foreach (var track in tracks)
                {
                    await libraryCatalog.SetFavoriteAsync(
                        track.FilePath,
                        newValue,
                        lifetimeCancellation.Token);
                }
            }
            catch (OperationCanceledException) when (
                lifetimeCancellation.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                foreach (var (track, wasFavorite) in originalValues)
                    SetFavorite(track, wasFavorite);

                Log.Warning(ex, "Could not save favorites for selected tracks");
                dialogService.ShowWarning(
                    $"Couldn't save favorites.\n\n{ex.Message}",
                    "Mimi");
            }
        }
        else
        {
            SaveFavorites();
        }

        ApplyFilter();
    }

    [RelayCommand]
    private async Task RemoveSelectedTrack()
    {
        foreach (var track in GetSelectedTracksSnapshot())
            await RemoveTrack(track);
    }

    [RelayCommand]
    private async Task RemoveTrack(TrackItemViewModel? track)
    {
        if (track is null || !Tracks.Contains(track)) return;

        if (catalogAvailable)
        {
            try
            {
                await libraryCatalog.RemoveTrackAsync(
                    track.FilePath,
                    lifetimeCancellation.Token);
            }
            catch (OperationCanceledException) when (
                lifetimeCancellation.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not remove {FilePath} from catalog", track.FilePath);
                dialogService.ShowWarning(
                    $"Couldn't remove this song from the library.\n\n{ex.Message}",
                    "Library warning");
                return;
            }
        }

        if (CurrentTrack == track)
        {
            audioPlayer.Stop();
            CurrentTrack = null;
            currentPlayHistoryId = null;
            CurrentCoverArtPath = null;
            NowPlayingTitle = "Choose a song";
            NowPlayingSubtitle = "Your next favorite is waiting";
            SetProgressPercent(0);
            RefreshPlaybackState();
        }

        if (SelectedTrack == track)
            SelectedTrack = null;

        RemoveTrackFromPlaybackQueue(track);
        SaveSettings();
        Tracks.Remove(track);
        RefreshFilterOptions();
        catalogSearchPaths?.Remove(track.FilePath);

        if (selectedPlaylistPathSet.Remove(track.FilePath))
        {
            selectedPlaylistTrackPaths = selectedPlaylistTrackPaths
                .Where(path => !string.Equals(
                    path,
                    track.FilePath,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            RefreshSelectedPlaylistPositions();
            SelectedPlaylist?.UpdateTrackCount(selectedPlaylistTrackPaths.Count);
        }

        if (favorites.Remove(track.FilePath) && !catalogAvailable)
            SaveFavorites();

        if (!catalogAvailable)
        {
            SaveLibrary();
        }
        else
        {
            await RefreshPlaylistsAsync();
            if (!string.IsNullOrWhiteSpace(SearchText))
                QueueCatalogSearch(SearchText);
        }

        ApplyFilter();
    }

    [RelayCommand]
    private void SelectSpectrumMode() =>
        VisualizerMode = VisualizerDisplayMode.NeonSpectrum;

    [RelayCommand]
    private void SelectTwinWaveMode() =>
        VisualizerMode = VisualizerDisplayMode.TwinWave;

    [RelayCommand]
    private void SelectHaloMode() =>
        VisualizerMode = VisualizerDisplayMode.HaloCircle;

    [RelayCommand]
    private void ToggleStage() =>
        StageToggleRequested?.Invoke(this, EventArgs.Empty);

    private async Task InitializeLibraryAsync()
    {
        if (libraryInitialized) return;
        libraryInitialized = true;

        try
        {
            await libraryCatalog.InitializeAsync(lifetimeCancellation.Token);
            catalogAvailable = true;

            var storedTracks = await libraryCatalog.LoadTracksAsync(
                lifetimeCancellation.Token);
            if (storedTracks.Count > 0)
            {
                favorites = storedTracks
                    .Where(track => track.IsFavorite)
                    .Select(track => track.FilePath)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                AddTracksToCollection(storedTracks);
                await RefreshPlaylistsAsync();
                RestorePersistedState();
                QueueLegacyCoverArtUpgrade();
                if (!string.IsNullOrWhiteSpace(SearchText))
                    QueueCatalogSearch(SearchText);
                return;
            }

            await ImportLegacyJsonLibraryAsync();
            await RefreshPlaylistsAsync();
            RestorePersistedState();
            if (!string.IsNullOrWhiteSpace(SearchText))
                QueueCatalogSearch(SearchText);
        }
        catch (OperationCanceledException) when (
            lifetimeCancellation.IsCancellationRequested)
        {
            // The window is closing; there is nothing left to initialize.
        }
        catch (Exception ex)
        {
            catalogAvailable = false;
            Log.Error(
                ex,
                "SQLite library catalog is unavailable; falling back to JSON data");

            try
            {
                SelectedPlaylist = null;
                Playlists.Clear();
                Tracks.Clear();
                RefreshFilterOptions();
                LoadFavorites();
                if (LoadLibrary()) PruneFavorites();
                ApplyFilter();
                RestorePersistedState();
            }
            catch (Exception fallbackException)
            {
                Log.Error(
                    fallbackException,
                    "JSON library fallback also failed during startup");
                dialogService.ShowWarning(
                    $"Couldn't restore your library.\n\n{fallbackException.Message}",
                    "Library warning");
            }
        }
    }

    private void QueueLegacyCoverArtUpgrade()
    {
        if (coverArtOptimizer is null || disposed ||
            !AutoUpgradeLegacyCoverArt ||
            legacyCoverArtUpgradeTask is { IsCompleted: false })
        {
            return;
        }

        legacyCoverArtUpgradeTask = UpgradeLegacyCoverArtAsync();
    }

    private async Task UpgradeLegacyCoverArtAsync()
    {
        var optimizer = coverArtOptimizer;
        if (optimizer is null) return;

        var legacyPaths = Tracks
            .Select(track => track.CoverArtPath)
            .Where(path => !string.IsNullOrWhiteSpace(path) &&
                           !CoverArtPaths.IsLargeVariant(path))
            .Select(path => path!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (legacyPaths.Length == 0) return;

        try
        {
            var replacements = await Task.Run(
                () =>
                {
                    var map = new Dictionary<string, string>(
                        StringComparer.OrdinalIgnoreCase);
                    foreach (var legacyPath in legacyPaths)
                    {
                        lifetimeCancellation.Token
                            .ThrowIfCancellationRequested();
                        var optimizedPath =
                            optimizer.CreateResizedVariants(legacyPath);
                        if (!string.IsNullOrWhiteSpace(optimizedPath))
                            map[legacyPath] = optimizedPath;
                    }

                    return map;
                },
                lifetimeCancellation.Token);
            if (replacements.Count == 0) return;

            var updatedTracks = new List<Track>();
            foreach (var item in Tracks)
            {
                if (item.CoverArtPath is null ||
                    !replacements.TryGetValue(
                        item.CoverArtPath,
                        out var optimizedPath))
                {
                    continue;
                }

                item.UpdateCoverArtPath(optimizedPath);
                updatedTracks.Add(item.Track);
                if (item == CurrentTrack)
                    CurrentCoverArtPath = optimizedPath;
            }

            if (updatedTracks.Count == 0) return;

            if (catalogAvailable)
            {
                await libraryCatalog.UpsertTracksAsync(
                    updatedTracks,
                    lifetimeCancellation.Token);
            }
            else
            {
                SaveLibrary();
            }

            Log.Information(
                "Upgraded {CoverCount} legacy cover cache entries to resized variants",
                replacements.Count);
        }
        catch (OperationCanceledException) when (
            lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not upgrade legacy cover art cache entries");
        }
    }

    private async Task ImportLegacyJsonLibraryAsync()
    {
        LoadFavorites();

        IReadOnlyList<string> paths;
        try
        {
            paths = userDataService.LoadLibraryPaths();
        }
        catch (Exception ex) when (
            ex is IOException or JsonException or UnauthorizedAccessException)
        {
            Log.Warning(ex, "Could not read the legacy JSON library for import");
            dialogService.ShowWarning(
                $"Couldn't restore your library.\n\n{ex.Message}",
                "Library warning");
            return;
        }

        if (paths.Count == 0) return;

        var importedTracks = await ScanAndAddFilesAsync(paths);
        if (importedTracks.Count == 0) return;

        PruneFavorites();

        try
        {
            userDataService.ArchiveImportedLibraryData();
            Log.Information(
                "Imported {TrackCount} legacy JSON library tracks into SQLite",
                importedTracks.Count);
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            // The SQLite import already succeeded. Keeping the JSON files is
            // safer than failing startup because their backup rename failed.
            Log.Warning(
                ex,
                "Imported the JSON library but could not archive the legacy files");
        }
    }

    private async Task<IReadOnlyList<Track>> ScanAndAddFilesAsync(
        IEnumerable<string> paths)
    {
        var cancellation = BeginScan();
        if (cancellation is null) return [];

        try
        {
            var tracks = await libraryService.ScanFilesAsync(
                paths,
                CreateScanProgress(),
                KnownTracksByPath(),
                cancellation.Token);
            return await AddTracksAsync(tracks);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            ScanStatusLabel = "Scan canceled";
            return [];
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Music file scan failed");
            dialogService.ShowWarning(
                $"Couldn't scan those music files.\n\n{ex.Message}",
                "Library warning");
            return [];
        }
        finally
        {
            EndScan(cancellation);
        }
    }

    private async Task<FolderScanResult?> ScanAndAddFolderAsync(string folder)
    {
        var cancellation = BeginScan();
        if (cancellation is null) return null;

        try
        {
            var result = await libraryService.ScanFolderAsync(
                folder,
                recursive: true,
                CreateScanProgress(),
                KnownTracksByPath(),
                cancellation.Token);
            await AddTracksAsync(result.Tracks);
            return result;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            ScanStatusLabel = "Scan canceled";
            return null;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Music folder scan failed for {Folder}", folder);
            dialogService.ShowWarning(
                $"Couldn't scan that folder.\n\n{ex.Message}",
                "Library warning");
            return null;
        }
        finally
        {
            EndScan(cancellation);
        }
    }

    private CancellationTokenSource? BeginScan()
    {
        if (IsScanning) return null;

        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            lifetimeCancellation.Token);
        scanCancellation = cancellation;
        IsScanning = true;
        ScanProgressPercent = 0;
        ScanStatusLabel = "Scanning your music…";
        return cancellation;
    }

    private void EndScan(CancellationTokenSource cancellation)
    {
        if (ReferenceEquals(scanCancellation, cancellation))
            scanCancellation = null;

        IsScanning = false;
        cancellation.Dispose();
        QueuePendingLibraryFolderChanges();
    }

    private Progress<LibraryScanProgress> CreateScanProgress() =>
        new(OnScanProgressChanged);

    private void OnScanProgressChanged(LibraryScanProgress progress)
    {
        ScanProgressPercent = progress.DiscoveredFiles <= 0
            ? 0
            : Math.Clamp(
                progress.ProcessedFiles * 100d / progress.DiscoveredFiles,
                0,
                100);

        if (progress.DiscoveredFiles <= 0)
        {
            ScanStatusLabel = "Discovering music files…";
            return;
        }

        var fileName = string.IsNullOrWhiteSpace(progress.CurrentFilePath)
            ? string.Empty
            : Path.GetFileName(progress.CurrentFilePath);
        ScanStatusLabel = fileName.Length == 0
            ? $"Scanning {progress.ProcessedFiles}/{progress.DiscoveredFiles}…"
            : $"Scanning {progress.ProcessedFiles}/{progress.DiscoveredFiles} — {fileName}";
    }

    private IReadOnlyDictionary<string, Track> KnownTracksByPath() =>
        Tracks.ToDictionary(
            track => track.FilePath,
            track => track.Track,
            StringComparer.OrdinalIgnoreCase);

    private async Task<IReadOnlyList<Track>> AddTracksAsync(
        IEnumerable<Track> additions)
    {
        var changedTracks = AddTracksToCollection(additions);
        await PersistTracksAsync(changedTracks);

        if (catalogAvailable && !string.IsNullOrWhiteSpace(SearchText))
            QueueCatalogSearch(SearchText);

        return changedTracks;
    }

    private void AddTracks(IEnumerable<Track> additions)
    {
        var changedTracks = AddTracksToCollection(additions);
        if (catalogAvailable)
            _ = PersistTracksAsync(changedTracks);
        else if (changedTracks.Count > 0)
            SaveLibrary();
    }

    private List<Track> AddTracksToCollection(IEnumerable<Track> additions)
    {
        var existingByPath = Tracks.ToDictionary(
            track => track.FilePath,
            StringComparer.OrdinalIgnoreCase);
        var changedTracks = new List<Track>();

        foreach (var track in additions)
        {
            if (existingByPath.TryGetValue(track.FilePath, out var existingItem))
            {
                if (!ReferenceEquals(existingItem.Track, track))
                {
                    existingItem.UpdateMetadata(track);
                    changedTracks.Add(existingItem.Track);
                }

                continue;
            }

            var item = new TrackItemViewModel(
                track,
                favorites.Contains(track.FilePath));
            if (item.IsFavorite)
                favorites.Add(track.FilePath);

            Tracks.Add(item);
            existingByPath[track.FilePath] = item;
            changedTracks.Add(track);
        }

        RefreshFilterOptions();
        ApplyFilter();
        return changedTracks;
    }

    private async Task PersistTracksAsync(IReadOnlyCollection<Track> tracks)
    {
        if (tracks.Count == 0) return;

        if (!catalogAvailable)
        {
            SaveLibrary();
            return;
        }

        try
        {
            await libraryCatalog.UpsertTracksAsync(
                tracks,
                lifetimeCancellation.Token);
        }
        catch (OperationCanceledException) when (
            lifetimeCancellation.IsCancellationRequested)
        {
            // Shutdown is in progress; SQLite transactions keep the catalog
            // either fully at the previous state or fully at the new state.
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not save tracks to the SQLite catalog");
            dialogService.ShowWarning(
                $"Couldn't save your library.\n\n{ex.Message}",
                "Library warning");
        }
    }

    private void ConfigureTracksView()
    {
        using (TracksView.DeferRefresh())
        {
            TracksView.SortDescriptions.Clear();
            if (ActiveView == LibraryViewMode.Playlist)
            {
                TracksView.SortDescriptions.Add(new SortDescription(
                    nameof(TrackItemViewModel.PlaylistPosition),
                    ListSortDirection.Ascending));
            }
            else if (trackSortField != TrackSortField.None)
            {
                var propertyName = trackSortField switch
                {
                    TrackSortField.Title =>
                        nameof(TrackItemViewModel.Title),
                    TrackSortField.Artist =>
                        nameof(TrackItemViewModel.Artist),
                    TrackSortField.Album =>
                        nameof(TrackItemViewModel.Album),
                    TrackSortField.Genre =>
                        nameof(TrackItemViewModel.Genre),
                    TrackSortField.Year =>
                        nameof(TrackItemViewModel.YearSortValue),
                    TrackSortField.Duration =>
                        nameof(TrackItemViewModel.DurationSortSeconds),
                    TrackSortField.Format =>
                        nameof(TrackItemViewModel.Format),
                    _ => nameof(TrackItemViewModel.Title)
                };
                TracksView.SortDescriptions.Add(new SortDescription(
                    propertyName,
                    trackSortDirection));

                if (trackSortField != TrackSortField.Title)
                {
                    TracksView.SortDescriptions.Add(new SortDescription(
                        nameof(TrackItemViewModel.Title),
                        ListSortDirection.Ascending));
                }
            }
            else if (ActiveView == LibraryViewMode.Recent)
            {
                TracksView.SortDescriptions.Add(new SortDescription(
                    nameof(TrackItemViewModel.LastPlayedAtUtc),
                    ListSortDirection.Descending));
            }
            else if (catalogSearchActive && normalizedSearchQuery.Length > 0)
            {
                TracksView.SortDescriptions.Add(new SortDescription(
                    nameof(TrackItemViewModel.SearchRank),
                    ListSortDirection.Ascending));
            }
        }

        ApplyFilter();
    }

    private bool FilterTrack(object item)
    {
        if (item is not TrackItemViewModel track) return false;

        var visibleInView = ActiveView switch
        {
            LibraryViewMode.Favorites => track.IsFavorite,
            LibraryViewMode.Recent => track.LastPlayedAtUtc is not null,
            LibraryViewMode.Playlist => selectedPlaylistPathSet.Contains(
                track.FilePath),
            _ => true
        };
        if (!visibleInView) return false;
        if (!MatchesAdvancedFilters(track)) return false;

        if (catalogSearchActive && catalogSearchPaths is not null)
            return catalogSearchPaths.Contains(track.FilePath);

        return normalizedSearchQuery.Length == 0 ||
               track.NormalizedSearchText.Contains(
                   normalizedSearchQuery,
                   StringComparison.Ordinal);
    }

    private void ApplyFilter()
    {
        TracksView.Refresh();
        UpdateTrackCount();
    }

    private void UpdateTrackCount()
    {
        var visibleCount = TracksView.Cast<TrackItemViewModel>().Count();

        if (normalizedSearchQuery.Length > 0)
        {
            TrackCountLabel = $"{visibleCount} search result(s)";
            return;
        }

        if (HasActiveAdvancedFilters)
        {
            TrackCountLabel = $"{visibleCount} filtered song(s)";
            return;
        }

        TrackCountLabel = ActiveView switch
        {
            LibraryViewMode.Favorites =>
                $"{visibleCount} favorite song(s)",
            LibraryViewMode.Recent =>
                $"{visibleCount} recently played song(s)",
            LibraryViewMode.Playlist =>
                $"{visibleCount} song(s) in this playlist",
            _ => Tracks.Count == 0
                ? "No songs yet - add some music to begin"
                : $"{Tracks.Count} song(s) in your library"
        };
    }

    private TrackItemViewModel[] GetSelectedTracksSnapshot()
    {
        var selected = SelectedTracks
            .Where(track => Tracks.Contains(track))
            .Distinct()
            .ToArray();
        if (selected.Length > 0) return selected;

        return SelectedTrack is not null && Tracks.Contains(SelectedTrack)
            ? [SelectedTrack]
            : [];
    }

    private void RefreshFilterOptions()
    {
        SelectedArtistFilter = RebuildFilterOptions(
            ArtistFilterOptions,
            Tracks.Select(track => track.Artist),
            AllArtistsFilter,
            SelectedArtistFilter);
        SelectedAlbumFilter = RebuildFilterOptions(
            AlbumFilterOptions,
            Tracks.Select(track => track.Album),
            AllAlbumsFilter,
            SelectedAlbumFilter);
        SelectedGenreFilter = RebuildFilterOptions(
            GenreFilterOptions,
            Tracks.Select(track => track.Genre),
            AllGenresFilter,
            SelectedGenreFilter);
        SelectedFormatFilter = RebuildFilterOptions(
            FormatFilterOptions,
            Tracks.Select(track => track.Format),
            AllFormatsFilter,
            SelectedFormatFilter);
    }

    private static string RebuildFilterOptions(
        ObservableCollection<string> options,
        IEnumerable<string> values,
        string allLabel,
        string selectedValue)
    {
        var distinctValues = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        options.Clear();
        options.Add(allLabel);
        foreach (var value in distinctValues)
            options.Add(value);

        return options.Contains(selectedValue) ? selectedValue : allLabel;
    }

    private bool MatchesAdvancedFilters(TrackItemViewModel track)
    {
        if (!MatchesFilterValue(
                SelectedArtistFilter,
                AllArtistsFilter,
                track.Artist) ||
            !MatchesFilterValue(
                SelectedAlbumFilter,
                AllAlbumsFilter,
                track.Album) ||
            !MatchesFilterValue(
                SelectedGenreFilter,
                AllGenresFilter,
                track.Genre) ||
            !MatchesFilterValue(
                SelectedFormatFilter,
                AllFormatsFilter,
                track.Format))
        {
            return false;
        }

        return SelectedPlaybackStateFilter switch
        {
            ReadyPlaybackStateFilter =>
                !track.Track.HasError && !track.Track.IsMissing,
            ErrorPlaybackStateFilter => track.Track.HasError,
            MissingPlaybackStateFilter => track.Track.IsMissing,
            _ => true
        };
    }

    private static bool MatchesFilterValue(
        string selectedValue,
        string allLabel,
        string actualValue) =>
        string.Equals(selectedValue, allLabel, StringComparison.Ordinal) ||
        string.Equals(
            selectedValue,
            actualValue,
            StringComparison.CurrentCultureIgnoreCase);

    private string GetSortLabel(string label, TrackSortField field)
    {
        if (trackSortField != field) return label;

        return trackSortDirection == ListSortDirection.Ascending
            ? $"{label} ↑"
            : $"{label} ↓";
    }

    private void ResetTrackSort()
    {
        if (trackSortField == TrackSortField.None) return;

        trackSortField = TrackSortField.None;
        trackSortDirection = ListSortDirection.Ascending;
        NotifySortLabelsChanged();
    }

    private void NotifySortLabelsChanged()
    {
        OnPropertyChanged(nameof(TitleSortLabel));
        OnPropertyChanged(nameof(ArtistSortLabel));
        OnPropertyChanged(nameof(AlbumSortLabel));
        OnPropertyChanged(nameof(GenreSortLabel));
        OnPropertyChanged(nameof(YearSortLabel));
        OnPropertyChanged(nameof(DurationSortLabel));
        OnPropertyChanged(nameof(FormatSortLabel));
    }

    private void QueueCatalogSearch(string query)
    {
        var version = ++searchRequestVersion;
        searchCancellation?.Cancel();

        if (string.IsNullOrWhiteSpace(query) || !catalogAvailable)
        {
            catalogSearchActive = false;
            catalogSearchPaths = null;
            IsSearchingCatalog = false;
            SearchStatusLabel = string.Empty;
            return;
        }

        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            lifetimeCancellation.Token);
        searchCancellation = cancellation;
        IsSearchingCatalog = true;
        SearchStatusLabel = "Searching your library…";
        _ = SearchCatalogAsync(query, version, cancellation.Token);
    }

    private async Task SearchCatalogAsync(
        string query,
        int version,
        CancellationToken cancellationToken)
    {
        try
        {
            // A short debounce avoids a database query for every keystroke.
            await Task.Delay(180, cancellationToken);
            var results = await libraryCatalog.SearchTracksAsync(
                query,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (version != searchRequestVersion ||
                !string.Equals(SearchText, query, StringComparison.Ordinal))
            {
                return;
            }

            var tracksByPath = Tracks.ToDictionary(
                track => track.FilePath,
                StringComparer.OrdinalIgnoreCase);
            foreach (var track in Tracks)
                track.SearchRank = int.MaxValue;

            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < results.Count; index++)
            {
                if (!tracksByPath.TryGetValue(
                        results[index].FilePath,
                        out var item))
                {
                    continue;
                }

                item.SearchRank = index;
                paths.Add(item.FilePath);
            }

            catalogSearchPaths = paths;
            catalogSearchActive = true;
            SearchStatusLabel = string.Empty;
            ConfigureTracksView();
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested)
        {
            // A newer query (or shutdown) superseded this one.
        }
        catch (Exception ex)
        {
            if (version != searchRequestVersion) return;

            catalogSearchActive = false;
            SearchStatusLabel =
                "Catalog search unavailable; using local search";
            Log.Warning(ex, "Catalog FTS search failed for query {Query}", query);
            ConfigureTracksView();
        }
        finally
        {
            if (version == searchRequestVersion)
                IsSearchingCatalog = false;
        }
    }

    private bool EnsureCatalogForPlaylists()
    {
        if (catalogAvailable) return true;

        dialogService.ShowWarning(
            "Playlists are unavailable because the SQLite library catalog " +
            "could not be opened. Mimi is currently using the legacy JSON " +
            "fallback.",
            "Playlist");
        return false;
    }

    private async Task RefreshPlaylistsAsync()
    {
        if (!catalogAvailable)
        {
            Playlists.Clear();
            return;
        }

        try
        {
            var loaded = await libraryCatalog.LoadPlaylistsAsync(
                lifetimeCancellation.Token);
            var loadedById = loaded.ToDictionary(playlist => playlist.Id);

            for (var index = Playlists.Count - 1; index >= 0; index--)
            {
                var existing = Playlists[index];
                if (loadedById.ContainsKey(existing.Id)) continue;

                if (SelectedPlaylist == existing)
                    SelectedPlaylist = null;
                Playlists.RemoveAt(index);
            }

            foreach (var playlist in loaded)
            {
                var existing = Playlists.FirstOrDefault(
                    item => item.Id == playlist.Id);
                if (existing is null)
                {
                    InsertPlaylistSorted(new PlaylistItemViewModel(playlist));
                }
                else
                {
                    existing.Update(playlist);
                    if (SelectedPlaylist == existing)
                        OnPropertyChanged(nameof(SectionTitle));
                }
            }

            SortPlaylists();
        }
        catch (OperationCanceledException) when (
            lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not refresh playlists from the catalog");
        }
    }

    private void InsertPlaylistSorted(PlaylistItemViewModel item)
    {
        var index = 0;
        while (index < Playlists.Count &&
               ComparePlaylists(Playlists[index], item) <= 0)
        {
            index++;
        }

        Playlists.Insert(index, item);
    }

    private void SortPlaylists()
    {
        var ordered = Playlists
            .OrderBy(playlist => playlist, Comparer<PlaylistItemViewModel>.Create(
                ComparePlaylists))
            .ToArray();

        for (var targetIndex = 0; targetIndex < ordered.Length; targetIndex++)
        {
            var currentIndex = Playlists.IndexOf(ordered[targetIndex]);
            if (currentIndex >= 0 && currentIndex != targetIndex)
                Playlists.Move(currentIndex, targetIndex);
        }
    }

    private static int ComparePlaylists(
        PlaylistItemViewModel left,
        PlaylistItemViewModel right)
    {
        var byName = string.Compare(
            left.Name,
            right.Name,
            StringComparison.CurrentCultureIgnoreCase);
        return byName != 0 ? byName : left.Id.CompareTo(right.Id);
    }

    private async Task ShowPlaylistAsync(PlaylistItemViewModel playlist)
    {
        if (!catalogAvailable)
        {
            if (ActiveView == LibraryViewMode.Playlist)
                ActiveView = LibraryViewMode.Library;
            return;
        }

        ClearSelectedPlaylistOrder();
        ActiveView = LibraryViewMode.Playlist;

        try
        {
            var tracks = await libraryCatalog.LoadPlaylistTracksAsync(
                playlist.Id,
                lifetimeCancellation.Token);
            if (SelectedPlaylist?.Id != playlist.Id) return;

            var paths = tracks.Select(track => track.FilePath).ToArray();
            SetSelectedPlaylistOrder(paths);
            playlist.UpdateTrackCount(paths.Length);
            ApplyFilter();
        }
        catch (OperationCanceledException) when (
            lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Log.Warning(
                ex,
                "Could not load playlist {PlaylistId}",
                playlist.Id);
            if (SelectedPlaylist?.Id == playlist.Id)
                ActiveView = LibraryViewMode.Library;
            dialogService.ShowWarning(
                $"Couldn't open that playlist.\n\n{ex.Message}",
                "Playlist");
        }
    }

    private void SetSelectedPlaylistOrder(IEnumerable<string> paths)
    {
        selectedPlaylistTrackPaths = paths.ToArray();
        selectedPlaylistPathSet = new HashSet<string>(
            selectedPlaylistTrackPaths,
            StringComparer.OrdinalIgnoreCase);
        RefreshSelectedPlaylistPositions();
    }

    private void ClearSelectedPlaylistOrder()
    {
        selectedPlaylistTrackPaths = [];
        selectedPlaylistPathSet.Clear();
        foreach (var track in Tracks)
            track.PlaylistPosition = int.MaxValue;
    }

    private void RefreshSelectedPlaylistPositions()
    {
        var positions = new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < selectedPlaylistTrackPaths.Count; index++)
            positions[selectedPlaylistTrackPaths[index]] = index;

        foreach (var track in Tracks)
        {
            track.PlaylistPosition = positions.TryGetValue(
                track.FilePath,
                out var position)
                ? position
                : int.MaxValue;
        }
    }

    private int IndexInSelectedPlaylist(string filePath)
    {
        for (var index = 0; index < selectedPlaylistTrackPaths.Count; index++)
        {
            if (string.Equals(
                    selectedPlaylistTrackPaths[index],
                    filePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private async Task AddTracksToPlaylistAsync(
        PlaylistItemViewModel playlist,
        IEnumerable<TrackItemViewModel> tracks)
    {
        if (!EnsureCatalogForPlaylists()) return;

        var distinctTracks = tracks
            .GroupBy(
                track => track.FilePath,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        if (distinctTracks.Length == 0) return;

        var addedPaths = new List<string>();
        var skippedCount = 0;

        try
        {
            foreach (var track in distinctTracks)
            {
                if (await libraryCatalog.AddTrackToPlaylistAsync(
                        playlist.Id,
                        track.FilePath,
                        lifetimeCancellation.Token))
                {
                    addedPaths.Add(track.FilePath);
                }
                else
                {
                    skippedCount++;
                }
            }

            ApplyPlaylistAdditions(playlist, addedPaths);

            if (skippedCount > 0)
            {
                var message = distinctTracks.Length == 1
                    ? $"\"{distinctTracks[0].Title}\" could not be added. " +
                      "It may already be in the playlist or may not be " +
                      "saved in the catalog yet."
                    : $"{addedPaths.Count} song(s) were added to " +
                      $"\"{playlist.Name}\"; {skippedCount} were skipped " +
                      "because they were already there or not saved yet.";
                dialogService.ShowInformation(message);

                if (addedPaths.Count == 0)
                {
                    if (playlist == SelectedPlaylist)
                        await ShowPlaylistAsync(playlist);
                    else
                        await RefreshPlaylistsAsync();
                }
            }
        }
        catch (OperationCanceledException) when (
            lifetimeCancellation.IsCancellationRequested)
        {
            ApplyPlaylistAdditions(playlist, addedPaths);
        }
        catch (Exception ex)
        {
            ApplyPlaylistAdditions(playlist, addedPaths);
            Log.Warning(
                ex,
                "Could not add selected tracks to playlist {PlaylistId}",
                playlist.Id);
            dialogService.ShowWarning(
                $"Couldn't add the selected song(s) to the playlist.\n\n{ex.Message}",
                "Playlist");
        }
    }

    private void ApplyPlaylistAdditions(
        PlaylistItemViewModel playlist,
        IReadOnlyCollection<string> addedPaths)
    {
        if (addedPaths.Count == 0) return;

        if (playlist == SelectedPlaylist)
        {
            var paths = selectedPlaylistTrackPaths.ToList();
            foreach (var path in addedPaths)
            {
                if (!selectedPlaylistPathSet.Contains(path))
                    paths.Add(path);
            }

            SetSelectedPlaylistOrder(paths);
            playlist.UpdateTrackCount(paths.Count);
            ApplyFilter();
        }
        else
        {
            playlist.UpdateTrackCount(playlist.TrackCount + addedPaths.Count);
        }
    }

    private async Task RemoveTrackFromSelectedPlaylistAsync(
        TrackItemViewModel track)
    {
        var playlist = SelectedPlaylist;
        if (playlist is null || !IsPlaylistView ||
            !EnsureCatalogForPlaylists())
        {
            return;
        }

        try
        {
            if (!await libraryCatalog.RemoveTrackFromPlaylistAsync(
                    playlist.Id,
                    track.FilePath,
                    lifetimeCancellation.Token))
            {
                await ShowPlaylistAsync(playlist);
                return;
            }

            var paths = selectedPlaylistTrackPaths
                .Where(path => !string.Equals(
                    path,
                    track.FilePath,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            SetSelectedPlaylistOrder(paths);
            playlist.UpdateTrackCount(paths.Length);

            if (SelectedTrack == track)
                SelectedTrack = null;

            ApplyFilter();
        }
        catch (OperationCanceledException) when (
            lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Log.Warning(
                ex,
                "Could not remove {FilePath} from playlist {PlaylistId}",
                track.FilePath,
                playlist.Id);
            dialogService.ShowWarning(
                $"Couldn't remove that song from the playlist.\n\n{ex.Message}",
                "Playlist");
        }
    }

    private async Task MovePlaylistTrackAsync(
        TrackItemViewModel track,
        int targetPosition)
    {
        var playlist = SelectedPlaylist;
        if (playlist is null || !IsPlaylistReorderEnabled ||
            !EnsureCatalogForPlaylists())
        {
            return;
        }

        var fromPosition = IndexInSelectedPlaylist(track.FilePath);
        if (fromPosition < 0) return;

        targetPosition = Math.Clamp(
            targetPosition,
            0,
            selectedPlaylistTrackPaths.Count - 1);
        if (targetPosition == fromPosition) return;

        try
        {
            if (!await libraryCatalog.MovePlaylistTrackAsync(
                    playlist.Id,
                    fromPosition,
                    targetPosition,
                    lifetimeCancellation.Token))
            {
                await ShowPlaylistAsync(playlist);
                return;
            }

            var paths = selectedPlaylistTrackPaths.ToList();
            var moving = paths[fromPosition];
            paths.RemoveAt(fromPosition);
            paths.Insert(targetPosition, moving);
            SetSelectedPlaylistOrder(paths);
            SelectedTrack = track;
            ApplyFilter();
        }
        catch (OperationCanceledException) when (
            lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Log.Warning(
                ex,
                "Could not move {FilePath} in playlist {PlaylistId}",
                track.FilePath,
                playlist.Id);
            dialogService.ShowWarning(
                $"Couldn't rearrange that playlist.\n\n{ex.Message}",
                "Playlist");
        }
    }

    private bool TryPlayTrack(
        TrackItemViewModel track,
        bool showError,
        Action? onPlaybackStarted = null)
    {
        var loaded = false;
        try
        {
            audioPlayer.Load(track.FilePath);
            loaded = true;

            // Only adopt the track as current once it actually loaded, so a
            // broken file never leaves playback state pointing at it.
            CurrentTrack = track;
            currentPlayHistoryId = null;
            ApplyEffectiveVolume();
            NowPlayingTitle = track.Title;
            NowPlayingSubtitle = track.Subtitle;
            CurrentCoverArtPath = track.CoverArtPath;
            SetProgressPercent(0);
            audioPlayer.Play();
            track.HasPlaybackError = false;
            track.Track.HasError = false;
            track.Track.IsMissing = false;
            SelectedTrack = track;
            onPlaybackStarted?.Invoke();
            RefreshPlaybackState();
            SaveSettings();
            _ = RecordPlaybackStartedAsync(track);
            return true;
        }
        catch (Exception ex)
        {
            // Load() stops and disposes any previous track before trying the
            // new one, so a failed load must not leave CurrentTrack behind.
            if (!loaded)
            {
                CurrentTrack = null;
                currentPlayHistoryId = null;
                CurrentCoverArtPath = null;
                playbackQueue.MoveCurrentToHistory();
                RefreshUpcomingQueue();
            }

            track.HasPlaybackError = true;
            track.Track.HasError = true;
            _ = PersistPlaybackErrorAsync(track);
            RefreshPlaybackState();
            Log.Warning(ex, "Could not play {FilePath}", track.FilePath);

            if (showError)
            {
                dialogService.ShowWarning(
                    $"Couldn't play this file.\n\n{ex.Message}",
                    "Playback error");
            }

            return false;
        }
    }

    private async Task RecordPlaybackStartedAsync(TrackItemViewModel track)
    {
        track.MarkPlayed(DateTimeOffset.UtcNow);
        if (ActiveView == LibraryViewMode.Recent)
            ApplyFilter();

        if (!catalogAvailable) return;

        try
        {
            var historyId = await libraryCatalog.RecordPlaybackStartedAsync(
                track.FilePath,
                lifetimeCancellation.Token);
            if (CurrentTrack == track)
                currentPlayHistoryId = historyId > 0 ? historyId : null;
        }
        catch (OperationCanceledException) when (
            lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Log.Warning(
                ex,
                "Could not record playback history for {FilePath}",
                track.FilePath);
        }
    }

    private async Task PersistPlaybackErrorAsync(TrackItemViewModel track)
    {
        if (!catalogAvailable) return;

        try
        {
            await libraryCatalog.SetPlaybackErrorAsync(
                track.FilePath,
                hasError: true,
                lifetimeCancellation.Token);
        }
        catch (OperationCanceledException) when (
            lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Log.Warning(
                ex,
                "Could not save playback error for {FilePath}",
                track.FilePath);
        }
    }

    private async Task HandlePlaybackEndedAsync()
    {
        await CompleteCurrentPlaybackAsync();
        if (disposed) return;

        if (RepeatMode == RepeatMode.One && CurrentTrack is not null &&
            TryPlayTrack(CurrentTrack, showError: false))
        {
            return;
        }

        if (!TryPlayNextFromQueue(showErrors: false))
            RefreshPlaybackState();
    }

    private async Task CompleteCurrentPlaybackAsync()
    {
        var historyId = currentPlayHistoryId;
        currentPlayHistoryId = null;
        if (!catalogAvailable || historyId is null) return;

        try
        {
            await libraryCatalog.MarkPlaybackCompletedAsync(
                historyId.Value,
                lifetimeCancellation.Token);
        }
        catch (OperationCanceledException) when (
            lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not complete playback history {HistoryId}", historyId);
        }
    }

    private void SetFavorite(TrackItemViewModel track, bool isFavorite)
    {
        track.IsFavorite = isFavorite;
        track.Track.IsFavorite = isFavorite;

        if (isFavorite)
            favorites.Add(track.FilePath);
        else
            favorites.Remove(track.FilePath);
    }

    private void AudioPlayer_PlaybackChanged(object? sender, EventArgs e) =>
        uiDispatcher.BeginInvoke(new Action(RefreshPlaybackState));

    private void AudioPlayer_PlaybackEnded(object? sender, EventArgs e) =>
        uiDispatcher.BeginInvoke(new Action(() => _ = HandlePlaybackEndedAsync()));

    private void PositionTimer_Tick(object? sender, EventArgs e)
    {
        RefreshPlaybackPosition();
        CapturePlaybackSessionPeriodically();
        WriteVisualizerDiagnostics();
    }

    private void RefreshPlaybackPosition()
    {
        var duration = audioPlayer.Duration;
        var position = audioPlayer.CurrentTime;

        if (!userSeeking && duration.TotalSeconds > 0)
        {
            SetProgressPercent(Math.Clamp(
                position.TotalSeconds / duration.TotalSeconds * 100d,
                0,
                100));
        }

        CurrentTimeLabel = FormatTime(position);
        DurationLabel = FormatTime(duration);
        RefreshPlaybackState();
    }

    private void RefreshPlaybackState() => IsPlaying = audioPlayer.IsPlaying;

    private void SetProgressPercent(double value)
    {
        changingProgress = true;
        try
        {
            ProgressPercent = value;
        }
        finally
        {
            changingProgress = false;
        }
    }

    private bool LoadLibrary()
    {
        try
        {
            var paths = userDataService.LoadLibraryPaths();
            if (paths.Count > 0)
                AddTracks(libraryService.ScanFiles(paths));

            return true;
        }
        catch (Exception ex) when (
            ex is IOException or JsonException or UnauthorizedAccessException)
        {
            Log.Warning(ex, "Could not restore the saved library");
            dialogService.ShowWarning(
                $"Couldn't restore your library.\n\n{ex.Message}",
                "Library warning");
            return false;
        }
    }

    private void SaveLibrary()
    {
        try
        {
            userDataService.SaveLibraryPaths(
                Tracks.Select(track => track.FilePath));
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            Log.Warning(ex, "Could not save the library");
            dialogService.ShowWarning(
                $"Couldn't save your library.\n\n{ex.Message}",
                "Library warning");
        }
    }

    private void LoadFavorites()
    {
        try
        {
            favorites = userDataService.LoadFavorites();
        }
        catch (Exception ex) when (
            ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Keep the player usable if favorites are missing or damaged.
            Log.Warning(ex, "Could not restore favorites");
            favorites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void SaveFavorites()
    {
        try
        {
            userDataService.SaveFavorites(favorites);
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            Log.Warning(ex, "Could not save favorites");
            dialogService.ShowWarning(
                $"Couldn't save favorites.\n\n{ex.Message}",
                "Mimi");
        }
    }

    // Favorites pointing at songs no longer in the library are dead weight;
    // drop them once per startup after the library has been restored.
    private void PruneFavorites()
    {
        var libraryPaths = Tracks
            .Select(track => track.FilePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (favorites.RemoveWhere(path => !libraryPaths.Contains(path)) > 0 &&
            !catalogAvailable)
        {
            SaveFavorites();
        }
    }

    // Release builds skip this call entirely (no per-second file writes).
    [Conditional("DEBUG")]
    private void WriteVisualizerDiagnostics()
    {
        if ((DateTime.UtcNow - lastVisualizerDiagnosticWrite).TotalSeconds < 1)
            return;

        lastVisualizerDiagnosticWrite = DateTime.UtcNow;
        try
        {
            System.IO.File.WriteAllText(
                Path.Combine(Path.GetTempPath(), "Mimi-visualizer-debug.txt"),
                $"playing={audioPlayer.IsPlaying}; " +
                $"position={audioPlayer.CurrentTime}; " +
                audioPlayer.GetVisualizerDiagnostics());
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        lifetimeCancellation.Cancel();
        scanCancellation?.Cancel();
        searchCancellation?.Cancel();
        CancelPendingExternalOpen();
        SelectedTracks.CollectionChanged -= SelectedTracks_CollectionChanged;
        WatchedFolders.CollectionChanged -= WatchedFolders_CollectionChanged;
        if (libraryFolderWatcher is not null)
        {
            libraryFolderWatcher.ChangesDetected -=
                LibraryFolderWatcher_ChangesDetected;
            libraryFolderWatcher.Configure([], isEnabled: false);
        }

        positionTimer.Stop();
        positionTimer.Tick -= PositionTimer_Tick;
        audioPlayer.PlaybackChanged -= AudioPlayer_PlaybackChanged;
        audioPlayer.PlaybackEnded -= AudioPlayer_PlaybackEnded;
        SaveSettings(force: true);
    }

    private static string FormatTime(TimeSpan time) =>
        time.TotalHours >= 1
            ? time.ToString(@"h\:mm\:ss")
            : time.ToString(@"m\:ss");
}
