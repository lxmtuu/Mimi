using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
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
        metricsTimer.Tick += MetricsTimer_Tick;
        metricsTimer.Start();
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

    protected override void OnClosed(EventArgs e)
    {
        metricsTimer.Stop();
        metricsTimer.Tick -= MetricsTimer_Tick;
        viewModel.StageToggleRequested -= ViewModel_StageToggleRequested;
        viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        stageWindow?.Close();
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
            viewModel.VisualizerMode)
        {
            Owner = this
        };
        stageWindow.SetTrack(viewModel.NowPlayingTitle, viewModel.NowPlayingSubtitle);
        stageWindow.ModeChanged += StageWindow_ModeChanged;
        stageWindow.Closed += StageWindow_Closed;
        stageWindow.Show();
    }

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
