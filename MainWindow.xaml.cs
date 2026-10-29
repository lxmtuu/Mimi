using System.IO;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Text.Json;
using Microsoft.Win32;
using Mimi.Controls;
using Mimi.Models;
using Mimi.Services;

namespace Mimi;

public partial class MainWindow : Window
{
    private readonly AudioService audio = new();
    private readonly LibraryService library = new();
    private readonly ObservableCollection<Track> tracks = new();
    private readonly DispatcherTimer timer;
    private static readonly System.Windows.Media.SolidColorBrush VisualizerModeInactiveBrush = CreateFrozenBrush(255, 32, 48, 68);
    private static readonly System.Windows.Media.SolidColorBrush VisualizerModeActiveTextBrush = CreateFrozenBrush(255, 7, 21, 27);
    private static readonly System.Windows.Media.SolidColorBrush VisualizerModeInactiveTextBrush = CreateFrozenBrush(255, 243, 247, 255);
    private VisualizerDisplayMode visualizerMode = VisualizerDisplayMode.NeonSpectrum;
    private StageWindow? stageWindow;
    private static readonly System.Windows.Media.SolidColorBrush FavoriteOnBrush = CreateFrozenBrush(255, 255, 143, 203);
    private static readonly System.Windows.Media.SolidColorBrush FavoriteOffBrush = CreateFrozenBrush(255, 130, 150, 174);
    private bool changingProgress;
    private bool userSeeking;
    private bool showingFavorites;
    private bool settingsLoaded;
    private DateTime lastVisualizerDiagnosticWrite = DateTime.MinValue;
    private DateTime lastSettingsSaveUtc = DateTime.MinValue;
    private AppSettings settings = new();
    private readonly HashSet<string> favorites = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> brokenPaths = new(StringComparer.OrdinalIgnoreCase);
    private Track? currentTrack;

    public MainWindow()
    {
        InitializeComponent();
        MainVisualizer.FrameProvider = audio.GetVisualizerFrame;
        SetVisualizerMode(VisualizerDisplayMode.NeonSpectrum);
        TrackList.ItemsSource = tracks;
        MigrateLegacyDataIfNeeded();
        LoadSettings();
        LoadFavorites();
        if (LoadLibrary()) PruneFavorites();
        var favoriteMenu = new MenuItem { Header = "Add to favorites" };
        favoriteMenu.Click += (_, _) => ToggleFavorite(TrackList.SelectedItem as Track);
        var removeMenu = new MenuItem { Header = "Remove from library" };
        removeMenu.Click += (_, _) => RemoveTrack(TrackList.SelectedItem as Track);
        TrackList.ContextMenu = new ContextMenu();
        TrackList.ContextMenu.Items.Add(favoriteMenu);
        TrackList.ContextMenu.Items.Add(removeMenu);
        TrackList.ContextMenu.Opened += (_, _) =>
        {
            if (TrackList.ContextMenu is not null && TrackList.SelectedItem is Track selected)
                favoriteMenu.Header = favorites.Contains(selected.FilePath) ? "Remove from favorites" : "Add to favorites";
        };
        // Track seeking via the thumb's drag lifecycle (it keeps mouse
        // capture) instead of raw mouse-down/up: releasing the mouse
        // outside the slider could otherwise leave the flag stuck and
        // freeze the progress bar.
        ProgressSlider.AddHandler(System.Windows.Controls.Primitives.Thumb.DragStartedEvent,
            new System.Windows.Controls.Primitives.DragStartedEventHandler((_, _) => userSeeking = true), true);
        ProgressSlider.AddHandler(System.Windows.Controls.Primitives.Thumb.DragCompletedEvent,
            new System.Windows.Controls.Primitives.DragCompletedEventHandler((_, _) => userSeeking = false), true);
        ProgressSlider.LostMouseCapture += (_, _) => userSeeking = false;
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        timer.Tick += (_, _) =>
        {
            RefreshPlaybackPosition();
            UpdateStageDiagnostics();
            WriteVisualizerDiagnostics();
        };
        timer.Start();
        audio.PlaybackChanged += (_, _) => Dispatcher.BeginInvoke(UpdatePlayButton);
        audio.PlaybackEnded += (_, _) => Dispatcher.BeginInvoke(new Action(() =>
            PlayRelativeInView(1, showErrors: false)));
        VolumeSlider.Value = settings.Volume;
        audio.Volume = (float)settings.Volume;
        UpdateCount();
    }

    // Release builds skip this call entirely (no per-second file writes).
    [System.Diagnostics.Conditional("DEBUG")]
    private void WriteVisualizerDiagnostics()
    {
        if ((DateTime.UtcNow - lastVisualizerDiagnosticWrite).TotalSeconds < 1) return;
        lastVisualizerDiagnosticWrite = DateTime.UtcNow;
        try
        {
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "Mimi-visualizer-debug.txt"),
                $"playing={audio.IsPlaying}; position={audio.CurrentTime}; {audio.GetVisualizerDiagnostics()}");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static System.Windows.Media.SolidColorBrush CreateFrozenBrush(byte a, byte r, byte g, byte b)
    {
        var brush = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }

    private void SetVisualizerMode(VisualizerDisplayMode mode)
    {
        visualizerMode = mode;
        MainVisualizer.Mode = mode;
        stageWindow?.SetMode(mode);
        UpdateVisualizerModeButtons();
    }

    private void UpdateVisualizerModeButtons()
    {
        SetVisualizerModeButtonState(SpectrumModeButton, visualizerMode == VisualizerDisplayMode.NeonSpectrum);
        SetVisualizerModeButtonState(WaveModeButton, visualizerMode == VisualizerDisplayMode.TwinWave);
        SetVisualizerModeButtonState(HaloModeButton, visualizerMode == VisualizerDisplayMode.HaloCircle);
    }

    private void SetVisualizerModeButtonState(Button button, bool isActive)
    {
        button.Background = isActive
            ? (System.Windows.Media.Brush)FindResource("AccentBrush")
            : VisualizerModeInactiveBrush;
        button.Foreground = isActive
            ? VisualizerModeActiveTextBrush
            : VisualizerModeInactiveTextBrush;
        button.FontWeight = isActive ? FontWeights.Bold : FontWeights.Normal;
    }

    private void SpectrumModeButton_Click(object sender, RoutedEventArgs e) =>
        SetVisualizerMode(VisualizerDisplayMode.NeonSpectrum);

    private void WaveModeButton_Click(object sender, RoutedEventArgs e) =>
        SetVisualizerMode(VisualizerDisplayMode.TwinWave);

    private void HaloModeButton_Click(object sender, RoutedEventArgs e) =>
        SetVisualizerMode(VisualizerDisplayMode.HaloCircle);

    private void StageButton_Click(object sender, RoutedEventArgs e) => ToggleStage();

    private void ToggleStage()
    {
        if (stageWindow is { IsLoaded: true })
        {
            stageWindow.Close();
            return;
        }

        OpenStage();
    }

    private void OpenStage()
    {
        if (stageWindow is { IsLoaded: true })
        {
            stageWindow.Activate();
            return;
        }

        stageWindow = new StageWindow(audio, visualizerMode) { Owner = this };
        stageWindow.SetTrack(NowTitle.Text, NowSubtitle.Text);
        stageWindow.ModeChanged += SetVisualizerMode;
        stageWindow.Closed += (_, _) => stageWindow = null;
        stageWindow.Show();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            ToggleStage();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private void UpdateStageDiagnostics() =>
        VisualizerMetricsText.Text =
            $"{MainVisualizer.FramesPerSecond:0} FPS · {MainVisualizer.AverageDrawMilliseconds:0.00} ms";

    private void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Choose music files",
            Filter = "Audio files|*.mp3;*.wav;*.aiff;*.aif;*.wma;*.m4a;*.aac;*.flac|All files|*.*",
            Multiselect = true
        };
        if (dialog.ShowDialog() == true) AddTracks(library.ScanFiles(dialog.FileNames));
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        // Built-in WPF folder picker (.NET 8) - no WinForms dependency needed.
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Choose a folder containing your music"
        };
        if (dialog.ShowDialog() != true) return;

        var result = library.ScanFolder(dialog.FolderName);
        AddTracks(result.Tracks);
        if (result.SkippedDirectories > 0)
            System.Windows.MessageBox.Show(
                $"{result.SkippedDirectories} folder(s) could not be read and were skipped.",
                "Mimi", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void AddTracks(IEnumerable<Track> additions)
    {
        var existing = tracks.Select(t => t.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var track in additions)
            if (existing.Add(track.FilePath)) tracks.Add(track);
        SaveLibrary();
        UpdateCount();
        ApplySearch();
    }

    private void HomeButton_Click(object sender, RoutedEventArgs e)
    {
        showingFavorites = false;
        SectionTitle.Text = "Your library";
        if (SearchBox.Text.Length > 0) SearchBox.Clear(); // re-filters via TextChanged
        ApplySearch();
    }

    private void LibraryButton_Click(object sender, RoutedEventArgs e)
    {
        showingFavorites = false;
        SectionTitle.Text = "Your library";
        ApplySearch();
    }

    private void FavoritesButton_Click(object sender, RoutedEventArgs e)
    {
        showingFavorites = !showingFavorites;
        SectionTitle.Text = showingFavorites ? "Your favorites" : "Your library";
        ApplySearch();
    }

    private string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mimi");

    // Mimi was previously named "MikuVerse Music". Its data lived in this
    // legacy folder; the string below is kept only so existing data can be
    // migrated (see MigrateLegacyDataIfNeeded). It is not the app name.
    private string LegacyDataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MikuVerseMusic");
    private string FavoritesFile => Path.Combine(DataDirectory, "favorites.json");
    private string LibraryFile => Path.Combine(DataDirectory, "library.json");
    private string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    // One-time copy of the saved library/favorites from the legacy
    // "MikuVerseMusic" folder to the new "Mimi" folder, so renaming the
    // app doesn't lose existing data. Existing files in the new folder
    // are never overwritten and the legacy folder is left untouched.
    private void MigrateLegacyDataIfNeeded()
    {
        try
        {
            if (!Directory.Exists(LegacyDataDirectory)) return;
            Directory.CreateDirectory(DataDirectory);
            foreach (var fileName in new[] { "favorites.json", "library.json" })
            {
                var source = Path.Combine(LegacyDataDirectory, fileName);
                var target = Path.Combine(DataDirectory, fileName);
                if (File.Exists(source) && !File.Exists(target))
                    File.Copy(source, target);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Keep the player usable if legacy data cannot be copied.
        }
    }

    // Write via a temp file + replace so a crash mid-write cannot leave a
    // truncated file behind.
    private static void WriteAllTextAtomic(string path, string contents)
    {
        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, contents);
        File.Move(tempPath, path, overwrite: true);
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                var saved = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsFile));
                if (saved is not null) settings = saved;
            }
            settings.Volume = Math.Clamp(settings.Volume, 0, 1);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            settings = new AppSettings();
        }
        settingsLoaded = true;
    }

    private void SaveSettings(bool force = false)
    {
        // Dragging the volume slider fires many changes; write at most
        // twice a second, plus one final forced write when closing.
        if (!force && (DateTime.UtcNow - lastSettingsSaveUtc).TotalMilliseconds < 500) return;
        lastSettingsSaveUtc = DateTime.UtcNow;
        try
        {
            Directory.CreateDirectory(DataDirectory);
            WriteAllTextAtomic(SettingsFile, JsonSerializer.Serialize(settings));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Settings are best-effort; never block playback on them.
        }
    }

    // Favorites pointing at songs no longer in the library are dead weight;
    // drop them once per startup after the library has been restored.
    private void PruneFavorites()
    {
        var libraryPaths = tracks.Select(t => t.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (favorites.RemoveWhere(path => !libraryPaths.Contains(path)) > 0)
            SaveFavorites();
    }

    private bool LoadLibrary()
    {
        try
        {
            if (!File.Exists(LibraryFile)) return true;
            var saved = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(LibraryFile));
            if (saved is not null) AddTracks(library.ScanFiles(saved));
            return true;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            System.Windows.MessageBox.Show($"Couldn't restore your library.\n\n{ex.Message}", "Library warning",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    private void SaveLibrary()
    {
        try
        {
            Directory.CreateDirectory(DataDirectory);
            WriteAllTextAtomic(LibraryFile, JsonSerializer.Serialize(tracks.Select(t => t.FilePath).ToArray()));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Windows.MessageBox.Show($"Couldn't save your library.\n\n{ex.Message}", "Library warning",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void LoadFavorites()
    {
        try
        {
            if (File.Exists(FavoritesFile))
            {
                var saved = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(FavoritesFile));
                if (saved is not null) favorites.UnionWith(saved);
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Keep the player usable if the favorites file is missing or damaged.
        }
    }

    private void SaveFavorites()
    {
        try
        {
            var directory = Path.GetDirectoryName(FavoritesFile)!;
            Directory.CreateDirectory(directory);
            WriteAllTextAtomic(FavoritesFile, JsonSerializer.Serialize(favorites.ToArray()));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Windows.MessageBox.Show($"Couldn't save favorites.\n\n{ex.Message}", "Mimi",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ToggleFavorite(Track? track)
    {
        if (track is null) return;
        if (!favorites.Add(track.FilePath)) favorites.Remove(track.FilePath);
        SaveFavorites();
        ApplySearch();
        TrackList.Items.Refresh();
    }

    private void RemoveTrack(Track? track)
    {
        if (track is null || !tracks.Remove(track)) return;

        if (currentTrack == track)
        {
            audio.Stop();
            currentTrack = null;
            NowTitle.Text = "Choose a song";
            NowSubtitle.Text = "Your next favorite is waiting";
            stageWindow?.SetTrack(NowTitle.Text, NowSubtitle.Text);
            changingProgress = true;
            ProgressSlider.Value = 0;
            changingProgress = false;
            UpdatePlayButton();
        }

        if (favorites.Remove(track.FilePath)) SaveFavorites();
        brokenPaths.Remove(track.FilePath);
        SaveLibrary();
        ApplySearch();
    }

    private void TrackList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete) RemoveTrack(TrackList.SelectedItem as Track);
    }

    private void FavoriteButton_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is Track track)
            UpdateFavoriteButton(button, track);
    }

    private void FavoriteButton_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // ListView may recycle item containers while scrolling; Loaded alone
        // would leave a recycled button showing the previous track's state.
        if (sender is Button button && button.DataContext is Track track)
            UpdateFavoriteButton(button, track);
    }

    private void FavoriteToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not Track track) return;
        ToggleFavorite(track);
        UpdateFavoriteButton(button, track);
        e.Handled = true;
    }

    private void UpdateFavoriteButton(Button button, Track track)
    {
        var isFavorite = favorites.Contains(track.FilePath);
        button.Content = isFavorite ? "♥" : "♡";
        button.Foreground = isFavorite ? FavoriteOnBrush : FavoriteOffBrush;
        button.ToolTip = isFavorite ? "Remove from favorites" : "Add to favorites";
    }

    private void StatusCell_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBlock text && text.DataContext is Track track)
            UpdateStatusCell(text, track);
    }

    private void StatusCell_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBlock text && text.DataContext is Track track)
            UpdateStatusCell(text, track);
    }

    private void UpdateStatusCell(TextBlock text, Track track) =>
        text.Text = brokenPaths.Contains(track.FilePath) ? "⚠" : "";

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplySearch();

    private void ApplySearch()
    {
        if (TrackList is null) return;
        var query = SearchBox.Text.Trim();
        var view = System.Windows.Data.CollectionViewSource.GetDefaultView(tracks);
        view.Filter = item => item is Track track &&
            (!showingFavorites || favorites.Contains(track.FilePath)) &&
            (string.IsNullOrWhiteSpace(query) ||
             track.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             track.Format.Contains(query, StringComparison.OrdinalIgnoreCase));
        UpdateCount();
    }

    private void UpdateCount()
    {
        if (TrackCountText is null) return;
        var visibleCount = System.Windows.Data.CollectionViewSource.GetDefaultView(tracks).Cast<Track>().Count();
        TrackCountText.Text = showingFavorites
            ? $"{visibleCount} favorite song(s)"
            : tracks.Count == 0
                ? "No songs yet - add some music to begin"
                : $"{tracks.Count} song(s) in your library";
    }

    private void TrackList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (TrackList.SelectedItem is Track track) PlayTrack(track);
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        if (currentTrack is null && TrackList.SelectedItem is Track selected) PlayTrack(selected);
        else if (currentTrack is null) PlayRelativeInView(1);
        else audio.Toggle();
        UpdatePlayButton();
    }

    // Next/Previous and auto-advance follow the order the user can see
    // (search / favorites filter applied), not the raw library order.
    private List<Track> VisibleTracks() =>
        System.Windows.Data.CollectionViewSource.GetDefaultView(tracks).Cast<Track>().ToList();

    private void PlayRelativeInView(int step, bool showErrors = true)
    {
        var visible = VisibleTracks();
        if (visible.Count == 0) return;

        // Walk in the requested direction, skipping files that fail to
        // load, so one broken song never stalls the whole list.
        var start = currentTrack is null ? -1 : visible.IndexOf(currentTrack);
        for (var attempt = 0; attempt < visible.Count; attempt++)
        {
            var index = start < 0
                ? (step >= 0 ? attempt : visible.Count - 1 - attempt)
                : ((start + step * (attempt + 1)) % visible.Count + visible.Count) % visible.Count;
            if (TryPlayTrack(visible[index], showError: false)) return;
        }

        if (showErrors)
            System.Windows.MessageBox.Show("None of the songs in this list could be played.",
                "Playback error", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void PlayTrack(Track track) => TryPlayTrack(track, showError: true);

    private bool TryPlayTrack(Track track, bool showError)
    {
        var loaded = false;
        try
        {
            audio.Load(track.FilePath);
            loaded = true;
            // Only adopt the track as current once it actually loaded, so a
            // broken file never leaves playback state pointing at it.
            currentTrack = track;
            audio.Volume = (float)VolumeSlider.Value;
            NowTitle.Text = track.Title;
            NowSubtitle.Text = $"{track.Format}  •  Local music";
            changingProgress = true;
            ProgressSlider.Value = 0;
            changingProgress = false;
            audio.Play();
            UpdatePlayButton();
            TrackList.SelectedItem = track;
            stageWindow?.SetTrack(track.Title, NowSubtitle.Text);
            return true;
        }
        catch (Exception ex)
        {
            // Load() stops and disposes any previous track before trying the
            // new one, so a failed load must not leave currentTrack behind.
            if (!loaded) currentTrack = null;
            changingProgress = false;
            brokenPaths.Add(track.FilePath);
            TrackList.Items.Refresh();
            UpdatePlayButton();
            if (showError)
                System.Windows.MessageBox.Show($"Couldn't play this file.\n\n{ex.Message}", "Playback error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    private void Previous_Click(object sender, RoutedEventArgs e) => PlayRelativeInView(-1);

    private void Next_Click(object sender, RoutedEventArgs e) => PlayRelativeInView(1);

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (audio is not null) audio.Volume = (float)e.NewValue;
        // Guarded by settingsLoaded: this event also fires while the XAML
        // default is applied during InitializeComponent, before the saved
        // value has been read - saving then would overwrite it.
        if (!settingsLoaded) return;
        settings.Volume = e.NewValue;
        SaveSettings();
    }

    private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Timer-driven updates always run under changingProgress, so any
        // remaining change is user input - mouse or keyboard alike.
        if (audio is null || changingProgress) return;
        if (audio.Duration > TimeSpan.Zero)
            audio.Seek(TimeSpan.FromSeconds(audio.Duration.TotalSeconds * ProgressSlider.Value / 100d));
    }

    private void RefreshPlaybackPosition()
    {
        var duration = audio.Duration;
        var position = audio.CurrentTime;
        changingProgress = true;
        if (!userSeeking && duration.TotalSeconds > 0)
            ProgressSlider.Value = Math.Clamp(position.TotalSeconds / duration.TotalSeconds * 100d, 0, 100);
        CurrentTimeText.Text = FormatTime(position);
        DurationText.Text = FormatTime(duration);
        changingProgress = false;
    }

    private static string FormatTime(TimeSpan time) =>
        time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss");

    private void UpdatePlayButton() => PlayButton.Content = audio.IsPlaying ? "⏸" : "▶";

    protected override void OnClosed(EventArgs e)
    {
        stageWindow?.Close();
        SaveSettings(force: true);
        timer.Stop();
        audio.Dispose();
        base.OnClosed(e);
    }

    private void ProgressSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => userSeeking = true;
    private void ProgressSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => userSeeking = false;
}
