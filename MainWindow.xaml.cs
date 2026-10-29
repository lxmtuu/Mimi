using System.IO;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Text.Json;
using Microsoft.Win32;
using Mimi.Models;
using Mimi.Services;

namespace Mimi;

public partial class MainWindow : Window
{
    private readonly AudioService audio = new();
    private readonly LibraryService library = new();
    private readonly ObservableCollection<Track> tracks = new();
    private readonly DispatcherTimer timer;
    private readonly System.Windows.Shapes.Rectangle[] visualizerBars = new System.Windows.Shapes.Rectangle[VisualizerSampleProvider.BandCount];
    private readonly System.Windows.Shapes.Rectangle[] visualizerGlowBars = new System.Windows.Shapes.Rectangle[VisualizerSampleProvider.BandCount];
    private static readonly System.Windows.Media.SolidColorBrush GlowTealBrush = CreateFrozenBrush(190, 71, 230, 208);
    private static readonly System.Windows.Media.SolidColorBrush GlowPinkBrush = CreateFrozenBrush(190, 255, 92, 174);
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
        InitializeVisualizer();
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
            RenderVisualizer();
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

    private void InitializeVisualizer()
    {
        // Draw a soft, wide glow layer first so the crisp spectrum bars stay on top.
        for (var i = 0; i < visualizerGlowBars.Length; i++)
        {
            var glowBar = new System.Windows.Shapes.Rectangle
            {
                RadiusX = 3,
                RadiusY = 3,
                Width = 6,
                Height = 4,
                Fill = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(180, 71, 230, 208)),
                Opacity = 0.62,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = System.Windows.Media.Color.FromRgb(71, 230, 208),
                    BlurRadius = 24,
                    ShadowDepth = 0,
                    Opacity = 1
                },
                IsHitTestVisible = false
            };
            visualizerGlowBars[i] = glowBar;
            VisualizerCanvas.Children.Add(glowBar);
        }

        for (var i = 0; i < visualizerBars.Length; i++)
        {
            var bar = new System.Windows.Shapes.Rectangle
            {
                RadiusX = 2,
                RadiusY = 2,
                Width = 4,
                Height = 3,
                Fill = new System.Windows.Media.LinearGradientBrush(
                    System.Windows.Media.Color.FromRgb(71, 230, 208),
                    System.Windows.Media.Color.FromRgb(255, 92, 174),
                    90),
                Opacity = 1,
                IsHitTestVisible = false
            };
            visualizerBars[i] = bar;
            VisualizerCanvas.Children.Add(bar);
        }

        SizeChanged += (_, _) => RenderVisualizer();
        Loaded += (_, _) => RenderVisualizer();
    }

    private void RenderVisualizer()
    {
        if (VisualizerCanvas.ActualWidth <= 0 || VisualizerCanvas.ActualHeight <= 0) return;
        var levels = audio.GetVisualizerLevels();
        var width = VisualizerCanvas.ActualWidth;
        var height = VisualizerCanvas.ActualHeight;
        var gap = 2.0;
        var barWidth = Math.Max(2, (width - gap * (visualizerBars.Length - 1)) / visualizerBars.Length);
        var count = Math.Min(levels.Length, visualizerBars.Length);

        for (var i = 0; i < count; i++)
        {
            var barHeight = Math.Clamp(6 + levels[i] * height * 0.9, 6, height);
            var x = i * (barWidth + gap);
            var y = height - barHeight;
            var bar = visualizerBars[i];
            bar.Width = barWidth;
            bar.Height = barHeight;
            System.Windows.Controls.Canvas.SetLeft(bar, x);
            System.Windows.Controls.Canvas.SetTop(bar, y);

            var glowBar = visualizerGlowBars[i];
            glowBar.Width = barWidth + 14;
            glowBar.Height = barHeight + 12;
            glowBar.Opacity = 0.72 + levels[i] * 0.28;
            var hot = levels[i] > 0.45;
            var glowColor = hot
                ? System.Windows.Media.Color.FromArgb(190, 255, 92, 174)
                : System.Windows.Media.Color.FromArgb(190, 71, 230, 208);
            glowBar.Fill = hot ? GlowPinkBrush : GlowTealBrush;
            if (glowBar.Effect is System.Windows.Media.Effects.DropShadowEffect glow)
            {
                glow.BlurRadius = 28 + levels[i] * 24;
                glow.Opacity = 1;
                glow.Color = glowColor;
            }
            System.Windows.Controls.Canvas.SetLeft(glowBar, x - 7);
            System.Windows.Controls.Canvas.SetTop(glowBar, y - 6);
        }
    }

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
        SaveSettings(force: true);
        timer.Stop();
        audio.Dispose();
        base.OnClosed(e);
    }

    private void ProgressSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => userSeeking = true;
    private void ProgressSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => userSeeking = false;
}
