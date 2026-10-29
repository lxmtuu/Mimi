using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    Recent
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
    private readonly Dispatcher uiDispatcher = Dispatcher.CurrentDispatcher;
    private readonly DispatcherTimer positionTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(150)
    };
    private readonly CancellationTokenSource lifetimeCancellation = new();

    private AppSettings settings = new();
    private HashSet<string> favorites = new(StringComparer.OrdinalIgnoreCase);
    private string normalizedSearchQuery = string.Empty;
    private CancellationTokenSource? scanCancellation;
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
    private TrackItemViewModel? currentTrack;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SectionTitle))]
    private LibraryViewMode activeView;

    [ObservableProperty]
    private string trackCountLabel = "No songs yet - add some music to begin";

    [ObservableProperty]
    private string nowPlayingTitle = "Choose a song";

    [ObservableProperty]
    private string nowPlayingSubtitle = "Your next favorite is waiting";

    [ObservableProperty]
    private string? currentCoverArtPath;

    [ObservableProperty]
    private bool isPlaying;

    [ObservableProperty]
    private double progressPercent;

    [ObservableProperty]
    private string currentTimeLabel = "0:00";

    [ObservableProperty]
    private string durationLabel = "0:00";

    [ObservableProperty]
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

    public MainWindowViewModel(
        IAudioPlayer audioPlayer,
        ILibraryService libraryService,
        ILibraryCatalog libraryCatalog,
        IUserDataService userDataService,
        IDialogService dialogService)
    {
        this.audioPlayer = audioPlayer;
        this.libraryService = libraryService;
        this.libraryCatalog = libraryCatalog;
        this.userDataService = userDataService;
        this.dialogService = dialogService;

        TracksView = CollectionViewSource.GetDefaultView(Tracks);
        TracksView.Filter = FilterTrack;

        userDataService.MigrateLegacyDataIfNeeded();
        settings = userDataService.LoadSettings();
        Volume = settings.Volume;
        audioPlayer.Volume = (float)settings.Volume;
        settingsLoaded = true;

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

    public ICollectionView TracksView { get; }

    public string SectionTitle => ActiveView switch
    {
        LibraryViewMode.Favorites => "Your favorites",
        LibraryViewMode.Recent => "Recently played",
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
        ApplyFilter();
    }

    partial void OnActiveViewChanged(LibraryViewMode value) =>
        ConfigureTracksView();

    partial void OnVolumeChanged(double value)
    {
        audioPlayer.Volume = (float)value;

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
    }

    [RelayCommand]
    private void ShowHome()
    {
        ActiveView = LibraryViewMode.Library;
        SearchText = string.Empty;
        ApplyFilter();
    }

    [RelayCommand]
    private void ShowLibrary() => ActiveView = LibraryViewMode.Library;

    [RelayCommand]
    private void ToggleFavoritesView() =>
        ActiveView = ActiveView == LibraryViewMode.Favorites
            ? LibraryViewMode.Library
            : LibraryViewMode.Favorites;

    [RelayCommand]
    private void ShowRecent() => ActiveView = LibraryViewMode.Recent;

    [RelayCommand]
    private async Task AddFiles()
    {
        if (IsScanning) return;

        var fileNames = dialogService.PickAudioFiles();
        if (fileNames.Count > 0)
            await ScanAndAddFilesAsync(fileNames);
    }

    [RelayCommand]
    private async Task AddFolder()
    {
        if (IsScanning) return;

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
        if (CurrentTrack is null && SelectedTrack is not null)
            TryPlayTrack(SelectedTrack, showError: true);
        else if (CurrentTrack is null)
            PlayRelativeInView(1);
        else
            audioPlayer.Toggle();

        RefreshPlaybackState();
    }

    [RelayCommand]
    private void PlayTrack(TrackItemViewModel? track)
    {
        if (track is not null)
            TryPlayTrack(track, showError: true);
    }

    [RelayCommand]
    private void Previous() => PlayRelativeInView(-1);

    [RelayCommand]
    private void Next() => PlayRelativeInView(1);

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
    private async Task RemoveSelectedTrack() => await RemoveTrack(SelectedTrack);

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

        Tracks.Remove(track);
        if (favorites.Remove(track.FilePath) && !catalogAvailable)
            SaveFavorites();

        if (!catalogAvailable)
            SaveLibrary();

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
                return;
            }

            await ImportLegacyJsonLibraryAsync();
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
                Tracks.Clear();
                LoadFavorites();
                if (LoadLibrary()) PruneFavorites();
                ApplyFilter();
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
            if (ActiveView == LibraryViewMode.Recent)
            {
                TracksView.SortDescriptions.Add(new SortDescription(
                    nameof(TrackItemViewModel.LastPlayedAtUtc),
                    ListSortDirection.Descending));
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
            _ => true
        };
        if (!visibleInView) return false;

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
        if (ActiveView == LibraryViewMode.Favorites)
        {
            var visibleCount = TracksView.Cast<TrackItemViewModel>().Count();
            TrackCountLabel = $"{visibleCount} favorite song(s)";
            return;
        }

        if (ActiveView == LibraryViewMode.Recent)
        {
            var visibleCount = TracksView.Cast<TrackItemViewModel>().Count();
            TrackCountLabel = $"{visibleCount} recently played song(s)";
            return;
        }

        TrackCountLabel = Tracks.Count == 0
            ? "No songs yet - add some music to begin"
            : $"{Tracks.Count} song(s) in your library";
    }

    // Next/Previous and auto-advance follow the order the user can see
    // (search / favorites filter applied), not the raw library order.
    private void PlayRelativeInView(int step, bool showErrors = true)
    {
        var visible = TracksView.Cast<TrackItemViewModel>().ToList();
        if (visible.Count == 0) return;

        var currentIndex = CurrentTrack is null
            ? -1
            : visible.IndexOf(CurrentTrack);
        var candidates = PlaybackNavigator.GetCandidateIndices(
            visible.Count,
            currentIndex,
            step);

        foreach (var candidate in candidates)
        {
            // Walk in the requested direction, skipping files that fail to
            // load, so one broken song never stalls the whole list.
            if (TryPlayTrack(visible[candidate], showError: false)) return;
        }

        if (showErrors)
        {
            dialogService.ShowWarning(
                "None of the songs in this list could be played.",
                "Playback error");
        }
    }

    private bool TryPlayTrack(TrackItemViewModel track, bool showError)
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
            audioPlayer.Volume = (float)Volume;
            NowPlayingTitle = track.Title;
            NowPlayingSubtitle = track.Subtitle;
            CurrentCoverArtPath = track.CoverArtPath;
            SetProgressPercent(0);
            audioPlayer.Play();
            track.HasPlaybackError = false;
            track.Track.HasError = false;
            track.Track.IsMissing = false;
            SelectedTrack = track;
            RefreshPlaybackState();
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
        if (!disposed)
            PlayRelativeInView(1, showErrors: false);
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

    private void SaveSettings(bool force = false)
    {
        // Dragging the volume slider fires many changes; write at most twice
        // a second, plus one final forced write when the view model closes.
        if (!force &&
            (DateTime.UtcNow - lastSettingsSaveUtc).TotalMilliseconds < 500)
        {
            return;
        }

        lastSettingsSaveUtc = DateTime.UtcNow;
        userDataService.SaveSettings(settings);
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
