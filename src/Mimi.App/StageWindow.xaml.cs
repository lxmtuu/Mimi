using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Mimi.Controls;
using Mimi.Localization;
using Mimi.Services;

namespace Mimi;

public partial class StageWindow : Window
{
    private readonly DispatcherTimer cursorTimer;
    private readonly DispatcherTimer metricsTimer;

    public StageWindow(
        IAudioPlayer audio,
        VisualizerDisplayMode mode,
        LocalizationService? localizationService = null)
    {
        Loc = localizationService ?? new LocalizationService();
        InitializeComponent();
        DataContext = this;
        StageVisualizer.FrameProvider = audio.GetVisualizerFrame;
        StageVisualizer.Mode = mode;
        Cursor = Cursors.None;

        cursorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        cursorTimer.Tick += (_, _) =>
        {
            Cursor = Cursors.None;
            cursorTimer.Stop();
        };

        metricsTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        metricsTimer.Tick += (_, _) => UpdateMetrics();
        metricsTimer.Start();
        UpdateMetrics();
    }

    public LocalizationService Loc { get; }

    public event Action<VisualizerDisplayMode>? ModeChanged;

    public void SetTrack(string title, string subtitle)
    {
        StageTitle.Text = string.IsNullOrWhiteSpace(title)
            ? Loc["NowPlaying_ChooseSong"]
            : title;
        StageSubtitle.Text = subtitle;
    }

    public void SetMode(VisualizerDisplayMode mode) => StageVisualizer.Mode = mode;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
            case Key.F11:
                Close();
                e.Handled = true;
                return;
            case Key.D1:
                ChangeMode(VisualizerDisplayMode.NeonSpectrum);
                e.Handled = true;
                return;
            case Key.D2:
                ChangeMode(VisualizerDisplayMode.TwinWave);
                e.Handled = true;
                return;
            case Key.D3:
                ChangeMode(VisualizerDisplayMode.HaloCircle);
                e.Handled = true;
                return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Cursor = Cursors.Arrow;
        cursorTimer.Stop();
        cursorTimer.Start();
    }

    protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        metricsTimer.Stop();
        cursorTimer.Stop();
        Cursor = Cursors.Arrow;
        base.OnClosed(e);
    }

    private void ChangeMode(VisualizerDisplayMode mode)
    {
        StageVisualizer.Mode = mode;
        ModeChanged?.Invoke(mode);
    }

    private void UpdateMetrics() =>
        StageMetricsText.Text =
            $"{StageVisualizer.FramesPerSecond:0} FPS · {StageVisualizer.AverageDrawMilliseconds:0.00} ms";
}
