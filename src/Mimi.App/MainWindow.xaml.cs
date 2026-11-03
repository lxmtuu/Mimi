using System.ComponentModel;
using System.IO;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Threading;
using Mimi.Controls;
using Mimi.Services;
using Mimi.ViewModels;

namespace Mimi;

/// <summary>
/// View glue only: playback/library behavior lives in
/// <see cref="MainWindowViewModel"/>. This class wires view-specific pieces
/// (visualizer frame callback, progress-thumb drag state, fullscreen Stage,
/// and render metrics) that do not belong in a view model.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainWindowViewModel viewModel;
    private readonly IAudioPlayer audioPlayer;
    private readonly DispatcherTimer metricsTimer =
        new() { Interval = TimeSpan.FromMilliseconds(500) };
    private StageWindow? stageWindow;
    private WindowsMediaControlsService? mediaControls;
    private IntPtr mainWindowHandle;
    private bool youTubeBrowserReady;

    public MainWindow(MainWindowViewModel viewModel, IAudioPlayer audioPlayer)
    {
        this.viewModel = viewModel;
        this.audioPlayer = audioPlayer;

        InitializeComponent();
        DataContext = viewModel;
        MainVisualizer.FrameProvider = viewModel.VisualizerFrameProvider;

        // Track seeking via the thumb's drag lifecycle (it keeps mouse
        // capture) so releasing outside the slider cannot leave the view
        // model believing the user is still seeking.
        ProgressSlider.AddHandler(
            Thumb.DragStartedEvent,
            new DragStartedEventHandler((_, _) => viewModel.BeginSeek()),
            true);
        ProgressSlider.AddHandler(
            Thumb.DragCompletedEvent,
            new DragCompletedEventHandler((_, _) => viewModel.EndSeek()),
            true);
        ProgressSlider.LostMouseCapture += (_, _) => viewModel.EndSeek();

        viewModel.StageToggleRequested += ViewModel_StageToggleRequested;
        viewModel.PropertyChanged += ViewModel_PropertyChanged;

        // The API key is read from disk, so a user who adds the key file while
        // Mimi is open gets the YouTube entry on the next window activation
        // instead of having to restart.
        Activated += (_, _) => viewModel.RefreshYouTubeAvailability();
        metricsTimer.Tick += MetricsTimer_Tick;
        metricsTimer.Start();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        mainWindowHandle = new WindowInteropHelper(this).Handle;

        // With an explicit AppUserModelID, the taskbar group icon is a
        // shell identity concern rather than just the WPF window icon.
        // Publish the relaunch icon on this HWND before the window is
        // shown so the shell does not keep resolving the old shortcut icon.
        WindowsAppIdentity.TrySetWindowAppUserModelProperties(mainWindowHandle);
        WindowChromeHelper.ApplyDarkTitleBar(this);

        // SMTC is keyed to the top-level HWND. It must therefore be created
        // after WPF has created the native window, not in the view model.
        mediaControls = new WindowsMediaControlsService(
            viewModel,
            audioPlayer,
            Dispatcher);
        mediaControls.Initialize(mainWindowHandle);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (mainWindowHandle != IntPtr.Zero)
        {
            WindowsAppIdentity.TryClearWindowAppUserModelProperties(
                mainWindowHandle);
            mainWindowHandle = IntPtr.Zero;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        metricsTimer.Stop();
        metricsTimer.Tick -= MetricsTimer_Tick;
        viewModel.StageToggleRequested -= ViewModel_StageToggleRequested;
        viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        try
        {
            YouTubeBrowser.Dispose();
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(
                "Could not dispose the YouTube player: {ExceptionType}",
                ex.GetType().FullName);
        }

        stageWindow?.Close();
        mediaControls?.Dispose();
        mediaControls = null;
        viewModel.Dispose();
        base.OnClosed(e);
    }

    private void MetricsTimer_Tick(object? sender, EventArgs e) =>
        viewModel.UpdateVisualizerMetrics(
            MainVisualizer.FramesPerSecond,
            MainVisualizer.AverageDrawMilliseconds);

    private void ViewModel_StageToggleRequested(object? sender, EventArgs e) =>
        ToggleStage();

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.UiTheme))
            WindowChromeHelper.ApplyDarkTitleBar(this);

        // The embedded YouTube player is driven from here: it must react even
        // when the Stage window is closed, so this hook runs before the
        // Stage-specific early return.
        if (e.PropertyName == nameof(MainWindowViewModel.IsYouTubePlaying))
            _ = SyncYouTubePlayerAsync();

        if (stageWindow is null) return;

        if (e.PropertyName == nameof(MainWindowViewModel.VisualizerMode))
        {
            stageWindow.SetMode(viewModel.VisualizerMode);
        }
        else if (e.PropertyName is
                 nameof(MainWindowViewModel.NowPlayingTitle) or
                 nameof(MainWindowViewModel.NowPlayingSubtitle))
        {
            stageWindow.SetTrack(viewModel.NowPlayingTitle, viewModel.NowPlayingSubtitle);
        }
        else if (e.PropertyName is
                 nameof(MainWindowViewModel.CurrentLyricLine) or
                 nameof(MainWindowViewModel.PreviousLyricLine) or
                 nameof(MainWindowViewModel.NextLyricLine))
        {
            PushStageLyrics();
        }
    }

    /// <summary>
    /// Drives the single embedded player. Mimi only navigates YouTube's own
    /// player: no stream is resolved and no media is downloaded. Hiding the
    /// pane navigates the browser away, which stops playback, so YouTube audio
    /// can never keep running without a visible player.
    /// </summary>
    private async Task SyncYouTubePlayerAsync()
    {
        try
        {
            if (!viewModel.IsYouTubePlaying)
            {
                if (youTubeBrowserReady)
                    YouTubeBrowser.CoreWebView2.Navigate("about:blank");

                return;
            }

            if (!youTubeBrowserReady)
            {
                // A dedicated profile keeps the user's own YouTube session
                // (sign-in, Premium) between Mimi runs, like a browser profile.
                var userDataFolder = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "Mimi",
                    "YouTubeBrowserProfile");
                Directory.CreateDirectory(userDataFolder);

                var environment = await CoreWebView2Environment.CreateAsync(
                    userDataFolder: userDataFolder);
                await YouTubeBrowser.EnsureCoreWebView2Async(environment);
                youTubeBrowserReady = true;
            }

            var core = YouTubeBrowser.CoreWebView2;
            if (core is null) return;

            var video = viewModel.CurrentYouTubeVideo?.Video;
            if (video is null) return;

            core.Navigate(video.EmbedUrl.AbsoluteUri);
        }
        catch (Exception ex)
        {
            // A missing WebView2 Runtime must not break the rest of Mimi.
            Serilog.Log.Warning(
                "Could not start the YouTube player: {ExceptionType}",
                ex.GetType().FullName);
        }
    }

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

        stageWindow = new StageWindow(
            audioPlayer,
            viewModel.VisualizerMode,
            viewModel.Loc)
        {
            Owner = this
        };
        stageWindow.SetTrack(viewModel.NowPlayingTitle, viewModel.NowPlayingSubtitle);
        PushStageLyrics();
        stageWindow.ModeChanged += StageWindow_ModeChanged;
        stageWindow.Closed += StageWindow_Closed;
        stageWindow.Show();
    }

    private void PushStageLyrics() =>
        stageWindow?.SetLyrics(
            viewModel.PreviousLyricLine?.Text,
            viewModel.CurrentLyricLine?.Text,
            viewModel.NextLyricLine?.Text);

    private void StageWindow_ModeChanged(VisualizerDisplayMode mode) =>
        viewModel.VisualizerMode = mode;

    private void StageWindow_Closed(object? sender, EventArgs e)
    {
        if (sender is StageWindow stage)
        {
            stage.ModeChanged -= StageWindow_ModeChanged;
            stage.Closed -= StageWindow_Closed;
        }

        stageWindow = null;
    }
}
