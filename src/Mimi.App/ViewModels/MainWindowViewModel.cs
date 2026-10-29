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
    private readonly IUserDataService userDataService;
    private readonly IDialogService dialogService;
    private readonly Dispatcher uiDispatcher = Dispatcher.CurrentDispatcher;
    private readonly DispatcherTimer positionTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(150)
    };

    private AppSettings settings = new();
    private HashSet<string> favorites = new(StringComparer.OrdinalIgnoreCase);
    private string normalizedSearchQuery = string.Empty;
    private bool changingProgress;
    private bool userSeeking;
    private bool settingsLoaded;
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
    private bool showingFavorites;

    [ObservableProperty]
    private string trackCountLabel = "No songs yet - add some music to begin";

    [ObservableProperty]
    private string nowPlayingTitle = "Choose a song";

    [ObservableProperty]
    private string nowPlayingSubtitle = "Your next favorite is waiting";

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

    public MainWindowViewModel(
        IAudioPlayer audioPlayer,
        ILibraryService libraryService,
        IUserDataService userDataService,
        IDialogService dialogService)
    {
        this.audioPlayer = audioPlayer;
        this.libraryService = libraryService;
        this.userDataService = userDataService;
        this.dialogService = dialogService;

        TracksView = CollectionViewSource.GetDefaultView(Tracks);
        TracksView.Filter = FilterTrack;

        userDataService.MigrateLegacyDataIfNeeded();
        settings = userDataService.LoadSettings();
        Volume = settings.Volume;
        audioPlayer.Volume = (float)settings.Volume;
        settingsLoaded = true;

        LoadFavorites();
        if (LoadLibrary()) PruneFavorites();

        audioPlayer.PlaybackChanged += AudioPlayer_PlaybackChanged;
        audioPlayer.PlaybackEnded += AudioPlayer_PlaybackEnded;
        positionTimer.Tick += PositionTimer_Tick;
        positionTimer.Start();

        RefreshPlaybackState();
        UpdateTrackCount();
    }

    public event EventHandler? StageToggleRequested;

    public ObservableCollection<TrackItemViewModel> Tracks { get; } = [];

    public ICollectionView TracksView { get; }

    public string SectionTitle => ShowingFavorites
        ? "Your favorites"
        : "Your library";

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

    partial void OnShowingFavoritesChanged(bool value) => ApplyFilter();

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
        ShowingFavorites = false;
        SearchText = string.Empty;
        ApplyFilter();
    }

    [RelayCommand]
    private void ShowLibrary()
    {
        ShowingFavorites = false;
        ApplyFilter();
    }

    [RelayCommand]
    private void ToggleFavoritesView() => ShowingFavorites = !ShowingFavorites;

    [RelayCommand]
    private void AddFiles()
    {
        var fileNames = dialogService.PickAudioFiles();
        if (fileNames.Count > 0)
            AddTracks(libraryService.ScanFiles(fileNames));
    }

    [RelayCommand]
    private void AddFolder()
    {
        var folder = dialogService.PickMusicFolder();
        if (string.IsNullOrWhiteSpace(folder)) return;

        var result = libraryService.ScanFolder(folder);
        AddTracks(result.Tracks);

        if (result.SkippedDirectories > 0)
        {
            dialogService.ShowInformation(
                $"{result.SkippedDirectories} folder(s) could not be read and were skipped.");
        }
    }

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
    private void ToggleFavorite(TrackItemViewModel? track)
    {
        if (track is null) return;

        track.IsFavorite = !track.IsFavorite;
        if (track.IsFavorite)
            favorites.Add(track.FilePath);
        else
            favorites.Remove(track.FilePath);

        SaveFavorites();
        ApplyFilter();
    }

    [RelayCommand]
    private void RemoveSelectedTrack() => RemoveTrack(SelectedTrack);

    [RelayCommand]
    private void RemoveTrack(TrackItemViewModel? track)
    {
        if (track is null || !Tracks.Remove(track)) return;

        if (CurrentTrack == track)
        {
            audioPlayer.Stop();
            CurrentTrack = null;
            NowPlayingTitle = "Choose a song";
            NowPlayingSubtitle = "Your next favorite is waiting";
            SetProgressPercent(0);
            RefreshPlaybackState();
        }

        if (SelectedTrack == track)
            SelectedTrack = null;

        if (favorites.Remove(track.FilePath))
            SaveFavorites();

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

    private void AddTracks(IEnumerable<Track> additions)
    {
        var existing = Tracks
            .Select(track => track.FilePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var track in additions)
        {
            if (existing.Add(track.FilePath))
            {
                Tracks.Add(new TrackItemViewModel(
                    track,
                    favorites.Contains(track.FilePath)));
            }
        }

        SaveLibrary();
        ApplyFilter();
    }

    private bool FilterTrack(object item)
    {
        if (item is not TrackItemViewModel track) return false;
        if (ShowingFavorites && !track.IsFavorite) return false;
        if (normalizedSearchQuery.Length == 0) return true;

        return track.NormalizedTitle.Contains(
                   normalizedSearchQuery,
                   StringComparison.Ordinal) ||
               track.NormalizedFormat.Contains(
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
        if (ShowingFavorites)
        {
            var visibleCount = TracksView.Cast<TrackItemViewModel>().Count();
            TrackCountLabel = $"{visibleCount} favorite song(s)";
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
            audioPlayer.Volume = (float)Volume;
            NowPlayingTitle = track.Title;
            NowPlayingSubtitle = $"{track.Format}  •  Local music";
            SetProgressPercent(0);
            audioPlayer.Play();
            track.HasPlaybackError = false;
            SelectedTrack = track;
            RefreshPlaybackState();
            return true;
        }
        catch (Exception ex)
        {
            // Load() stops and disposes any previous track before trying the
            // new one, so a failed load must not leave CurrentTrack behind.
            if (!loaded) CurrentTrack = null;

            track.HasPlaybackError = true;
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

    private void AudioPlayer_PlaybackChanged(object? sender, EventArgs e) =>
        uiDispatcher.BeginInvoke(new Action(RefreshPlaybackState));

    private void AudioPlayer_PlaybackEnded(object? sender, EventArgs e) =>
        uiDispatcher.BeginInvoke(new Action(() =>
            PlayRelativeInView(1, showErrors: false)));

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

        if (favorites.RemoveWhere(path => !libraryPaths.Contains(path)) > 0)
            SaveFavorites();
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
