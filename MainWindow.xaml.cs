using System.IO;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Text.Json;
using Microsoft.Win32;
using MikuVerseMusic.Models;
using MikuVerseMusic.Services;

namespace MikuVerseMusic;

public partial class MainWindow : Window
{
    private readonly AudioService audio = new();
    private readonly LibraryService library = new();
    private readonly ObservableCollection<Track> tracks = new();
    private readonly DispatcherTimer timer;
    private readonly System.Windows.Shapes.Rectangle[] visualizerBars = new System.Windows.Shapes.Rectangle[32];
    private readonly System.Windows.Shapes.Rectangle[] visualizerGlowBars = new System.Windows.Shapes.Rectangle[32];
    private bool changingProgress;
    private bool userSeeking;
    private bool showingFavorites;
    private bool wasPlaying;
    private DateTime lastVisualizerDiagnosticWrite = DateTime.MinValue;
    private readonly HashSet<string> favorites = new(StringComparer.OrdinalIgnoreCase);
    private int currentIndex = -1;

    public MainWindow()
    {
        InitializeComponent();
        InitializeVisualizer();
        TrackList.ItemsSource = tracks;
        LoadFavorites();
        LoadLibrary();
        var favoriteMenu = new MenuItem { Header = "Add to favorites" };
        favoriteMenu.Click += (_, _) => ToggleFavorite(TrackList.SelectedItem as Track);
        TrackList.ContextMenu = new ContextMenu();
        TrackList.ContextMenu.Items.Add(favoriteMenu);
        TrackList.ContextMenu.Opened += (_, _) =>
        {
            if (TrackList.ContextMenu is not null && TrackList.SelectedItem is Track selected)
                favoriteMenu.Header = favorites.Contains(selected.FilePath) ? "Remove from favorites" : "Add to favorites";
        };
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
        {
            if (tracks.Count == 0) return;
            var next = currentIndex >= 0 && currentIndex < tracks.Count - 1 ? currentIndex + 1 : 0;
            PlayTrack(tracks[next]);
        }));
        VolumeSlider.Value = 0.7;
        UpdateCount();
    }

    private void WriteVisualizerDiagnostics()
    {
        if ((DateTime.UtcNow - lastVisualizerDiagnosticWrite).TotalSeconds < 1) return;
        lastVisualizerDiagnosticWrite = DateTime.UtcNow;
        try
        {
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "MikuVerseMusic-visualizer-debug.txt"),
                $"playing={audio.IsPlaying}; position={audio.CurrentTime}; {audio.GetVisualizerDiagnostics()}");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
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

        for (var i = 0; i < visualizerBars.Length; i++)
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
            var glowColor = levels[i] > 0.45
                ? System.Windows.Media.Color.FromArgb(190, 255, 92, 174)
                : System.Windows.Media.Color.FromArgb(190, 71, 230, 208);
            glowBar.Fill = new System.Windows.Media.SolidColorBrush(glowColor);
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
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Choose a folder containing your music"
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            AddTracks(library.ScanFolder(dialog.SelectedPath));
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
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MikuVerseMusic");
    private string FavoritesFile => Path.Combine(DataDirectory, "favorites.json");
    private string LibraryFile => Path.Combine(DataDirectory, "library.json");

    private void LoadLibrary()
    {
        try
        {
            if (!File.Exists(LibraryFile)) return;
            var saved = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(LibraryFile));
            if (saved is not null) AddTracks(library.ScanFiles(saved));
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            System.Windows.MessageBox.Show($"Couldn't restore your library.\n\n{ex.Message}", "Library warning",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SaveLibrary()
    {
        try
        {
            Directory.CreateDirectory(DataDirectory);
            File.WriteAllText(LibraryFile, JsonSerializer.Serialize(tracks.Select(t => t.FilePath).ToArray()));
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
            File.WriteAllText(FavoritesFile, JsonSerializer.Serialize(favorites.ToArray()));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Windows.MessageBox.Show($"Couldn't save favorites.\n\n{ex.Message}", "MikuVerse Music",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ToggleFavorite(Track? track)
    {
        if (track is null) return;
        if (!favorites.Add(track.FilePath)) favorites.Remove(track.FilePath);
        SaveFavorites();
        ApplySearch();
    }

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
            : $"{tracks.Count} song(s) in your library";
    }

    private void TrackList_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

    private void TrackList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (TrackList.SelectedItem is Track track) PlayTrack(track);
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        if (currentIndex < 0 && TrackList.SelectedItem is Track selected) PlayTrack(selected);
        else if (currentIndex < 0 && tracks.Count > 0) PlayTrack(tracks[0]);
        else audio.Toggle();
        UpdatePlayButton();
    }

    private void PlayTrack(Track track)
    {
        try
        {
            currentIndex = tracks.IndexOf(track);
            audio.Load(track.FilePath);
            audio.Volume = (float)VolumeSlider.Value;
            NowTitle.Text = track.Title;
            NowSubtitle.Text = $"{track.Format}  •  Local music";
            ProgressSlider.Value = 0;
            audio.Play();
            UpdatePlayButton();
            TrackList.SelectedItem = track;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Couldn't play this file.\n\n{ex.Message}", "Playback error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Previous_Click(object sender, RoutedEventArgs e)
    {
        if (tracks.Count == 0) return;
        var index = currentIndex <= 0 ? tracks.Count - 1 : currentIndex - 1;
        PlayTrack(tracks[index]);
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (tracks.Count == 0) return;
        var index = currentIndex < 0 || currentIndex >= tracks.Count - 1 ? 0 : currentIndex + 1;
        PlayTrack(tracks[index]);
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (audio is not null) audio.Volume = (float)e.NewValue;
    }

    private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (audio is null || changingProgress || !userSeeking) return;
        if (audio.Duration > TimeSpan.Zero)
            audio.Seek(TimeSpan.FromSeconds(audio.Duration.TotalSeconds * ProgressSlider.Value / 100d));
    }

    private void RefreshPlaybackPosition()
    {
        var duration = audio.Duration;
        var position = audio.CurrentTime;
        wasPlaying = audio.IsPlaying;
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
        timer.Stop();
        audio.Dispose();
        base.OnClosed(e);
    }

    private void ProgressSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => userSeeking = true;
    private void ProgressSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => userSeeking = false;
}






