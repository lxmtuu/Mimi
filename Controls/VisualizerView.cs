using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using Mimi.Services;

namespace Mimi.Controls;

public enum VisualizerDisplayMode
{
    NeonSpectrum,
    TwinWave,
    HaloCircle
}

/// <summary>
/// A single-DrawingVisual visualizer renderer driven by
/// <see cref="CompositionTarget.Rendering"/>. It reuses its display buffers
/// and frozen brushes/pens, so the render loop does not allocate WPF controls,
/// geometries, brushes, or pens per frame.
/// </summary>
public sealed class VisualizerView : FrameworkElement
{
    private static readonly Brush SpectrumBrush = CreateSpectrumBrush();
    private static readonly Brush SpectrumGlowBrush = CreateFrozenBrush(52, 71, 230, 208);
    private static readonly Brush HaloCoreBrush = CreateFrozenBrush(48, 71, 230, 208);
    private static readonly Pen BaselinePen = CreateFrozenPen(CreateFrozenBrush(90, 130, 150, 174), 1.0);
    private static readonly Pen PeakPen = CreateFrozenPen(CreateFrozenBrush(255, 255, 143, 203), 2.0);
    private static readonly Pen WaveTealGlowPen = CreateFrozenPen(CreateFrozenBrush(64, 71, 230, 208), 5.0);
    private static readonly Pen WavePinkGlowPen = CreateFrozenPen(CreateFrozenBrush(56, 255, 92, 174), 5.0);
    private static readonly Pen WaveTealPen = CreateFrozenPen(CreateFrozenBrush(255, 71, 230, 208), 2.0);
    private static readonly Pen WavePinkPen = CreateFrozenPen(CreateFrozenBrush(255, 255, 92, 174), 2.0);
    private static readonly Pen HaloOuterPen = CreateFrozenPen(CreateFrozenBrush(120, 71, 230, 208), 1.5);
    private static readonly Pen HaloInnerPen = CreateFrozenPen(CreateFrozenBrush(105, 255, 143, 203), 1.25);
    private static readonly Pen HaloTealPen = CreateFrozenPen(CreateFrozenBrush(255, 71, 230, 208), 3.0);
    private static readonly Pen HaloPinkPen = CreateFrozenPen(CreateFrozenBrush(255, 255, 92, 174), 3.0);

    private readonly DrawingVisual visual = new();
    private readonly float[] displayedSpectrum = new float[VisualizerSampleProvider.BandCount];
    private readonly float[] peakSpectrum = new float[VisualizerSampleProvider.BandCount];

    private bool renderingAttached;
    private bool hasLastDrawnAt;
    private TimeSpan lastDrawnAt;
    private bool hasPowerCheckAt;
    private TimeSpan lastPowerCheckAt;
    private bool batterySaverEnabled;

    private bool hasMetricsWindowStart;
    private TimeSpan metricsWindowStart;
    private int framesInMetricsWindow;
    private double drawMillisecondsInWindow;

    public VisualizerView()
    {
        AddVisualChild(visual);
        AddLogicalChild(visual);
        ClipToBounds = true;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public Func<VisualizerFrame>? FrameProvider { get; set; }

    public VisualizerDisplayMode Mode { get; set; } = VisualizerDisplayMode.NeonSpectrum;

    public double FramesPerSecond { get; private set; }

    public double AverageDrawMilliseconds { get; private set; }

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index) =>
        index == 0 ? visual : throw new ArgumentOutOfRangeException(nameof(index));

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (renderingAttached) return;
        CompositionTarget.Rendering += OnRendering;
        renderingAttached = true;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (!renderingAttached) return;
        CompositionTarget.Rendering -= OnRendering;
        renderingAttached = false;
        hasLastDrawnAt = false;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (!IsVisible || ActualWidth < 2 || ActualHeight < 2) return;
        if (e is not RenderingEventArgs args) return;

        var owner = Window.GetWindow(this);
        if (owner?.WindowState == WindowState.Minimized) return;
        if (hasLastDrawnAt && args.RenderingTime <= lastDrawnAt) return;

        UpdatePowerState(args.RenderingTime);
        var sinceLastDraw = hasLastDrawnAt
            ? args.RenderingTime - lastDrawnAt
            : TimeSpan.FromSeconds(1.0 / 60.0);
        var throttled = batterySaverEnabled || owner is { IsActive: false };
        if (throttled && sinceLastDraw.TotalSeconds < 1.0 / 30.0) return;

        var smoothingElapsed = Math.Clamp(sinceLastDraw.TotalSeconds, 0.001, 0.1);
        var frame = FrameProvider?.Invoke() ?? VisualizerFrame.Silence;
        var drawStarted = Stopwatch.GetTimestamp();
        Draw(frame, smoothingElapsed);
        var drawElapsed = Stopwatch.GetElapsedTime(drawStarted);

        lastDrawnAt = args.RenderingTime;
        hasLastDrawnAt = true;
        RecordMetrics(args.RenderingTime, drawElapsed);
    }

    private void UpdatePowerState(TimeSpan renderingTime)
    {
        if (hasPowerCheckAt && renderingTime - lastPowerCheckAt < TimeSpan.FromSeconds(1)) return;
        batterySaverEnabled = PowerStatus.IsBatterySaverEnabled;
        lastPowerCheckAt = renderingTime;
        hasPowerCheckAt = true;
    }

    private void RecordMetrics(TimeSpan renderingTime, TimeSpan drawElapsed)
    {
        if (!hasMetricsWindowStart)
        {
            metricsWindowStart = renderingTime;
            hasMetricsWindowStart = true;
        }

        framesInMetricsWindow++;
        drawMillisecondsInWindow += drawElapsed.TotalMilliseconds;

        var windowElapsed = renderingTime - metricsWindowStart;
        if (windowElapsed.TotalMilliseconds < 500) return;

        FramesPerSecond = framesInMetricsWindow / windowElapsed.TotalSeconds;
        AverageDrawMilliseconds = framesInMetricsWindow == 0
            ? 0
            : drawMillisecondsInWindow / framesInMetricsWindow;
        metricsWindowStart = renderingTime;
        framesInMetricsWindow = 0;
        drawMillisecondsInWindow = 0;
    }

    private void Draw(VisualizerFrame frame, double elapsedSeconds)
    {
        var averageLevel = UpdateDisplayedSpectrum(frame, elapsedSeconds);
        using var dc = visual.RenderOpen();
        switch (Mode)
        {
            case VisualizerDisplayMode.TwinWave:
                DrawTwinWave(dc, frame);
                break;
            case VisualizerDisplayMode.HaloCircle:
                DrawHaloCircle(dc, averageLevel);
                break;
            default:
                DrawNeonSpectrum(dc);
                break;
        }
    }

    private float UpdateDisplayedSpectrum(VisualizerFrame frame, double elapsedSeconds)
    {
        var attack = SmoothingCoefficient(elapsedSeconds, 0.045);
        var release = SmoothingCoefficient(elapsedSeconds, 0.18);
        var peakFallRate = (float)(elapsedSeconds * 0.45);
        var total = 0.0f;
        var count = Math.Min(displayedSpectrum.Length, frame.Spectrum.Count);

        for (var i = 0; i < displayedSpectrum.Length; i++)
        {
            var target = i < count ? Math.Clamp(frame.Spectrum[i], 0.0f, 1.0f) : 0.0f;
            var current = displayedSpectrum[i];
            var coefficient = target > current ? attack : release;
            current += (target - current) * coefficient;
            displayedSpectrum[i] = current;
            total += current;

            peakSpectrum[i] = current >= peakSpectrum[i]
                ? current
                : Math.Max(current, peakSpectrum[i] - peakFallRate);
        }

        return displayedSpectrum.Length == 0 ? 0 : total / displayedSpectrum.Length;
    }

    private void DrawNeonSpectrum(DrawingContext dc)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        var count = displayedSpectrum.Length;
        var gap = count > 1 ? 2.0 : 0.0;
        var barWidth = (width - gap * (count - 1)) / count;
        if (barWidth < 1)
        {
            gap = 0;
            barWidth = width / count;
        }

        var baseline = height - 5;
        var maxHeight = Math.Max(1, baseline - 8);
        dc.DrawLine(BaselinePen, new Point(0, baseline), new Point(width, baseline));

        for (var i = 0; i < count; i++)
        {
            var level = displayedSpectrum[i];
            var barHeight = 3 + level * maxHeight;
            var x = i * (barWidth + gap);
            var y = baseline - barHeight;
            var radius = Math.Min(3, barWidth / 2);

            dc.DrawRoundedRectangle(
                SpectrumGlowBrush,
                null,
                new Rect(Math.Max(0, x - 1), Math.Max(0, y - 3), barWidth + 2, barHeight + 3),
                radius + 1,
                radius + 1);
            dc.DrawRoundedRectangle(
                SpectrumBrush,
                null,
                new Rect(x, y, barWidth, barHeight),
                radius,
                radius);

            var peakY = baseline - (3 + peakSpectrum[i] * maxHeight) - 3;
            dc.DrawLine(PeakPen, new Point(x, peakY), new Point(x + barWidth, peakY));
        }
    }

    private void DrawTwinWave(DrawingContext dc, VisualizerFrame frame)
    {
        var count = frame.Waveform.Count;
        if (count < 2) return;

        var width = ActualWidth;
        var height = ActualHeight;
        var centerY = height / 2;
        var amplitude = height * 0.42;
        dc.DrawLine(BaselinePen, new Point(0, centerY), new Point(width, centerY));

        var first = Math.Clamp(frame.Waveform[0], -1.0f, 1.0f);
        var previousTop = new Point(0, centerY - first * amplitude);
        var previousBottom = new Point(0, centerY + first * amplitude);

        for (var i = 1; i < count; i++)
        {
            var x = width * i / (count - 1);
            var value = Math.Clamp(frame.Waveform[i], -1.0f, 1.0f);
            var top = new Point(x, centerY - value * amplitude);
            var bottom = new Point(x, centerY + value * amplitude);

            dc.DrawLine(WaveTealGlowPen, previousTop, top);
            dc.DrawLine(WavePinkGlowPen, previousBottom, bottom);
            dc.DrawLine(WaveTealPen, previousTop, top);
            dc.DrawLine(WavePinkPen, previousBottom, bottom);

            previousTop = top;
            previousBottom = bottom;
        }
    }

    private void DrawHaloCircle(DrawingContext dc, float averageLevel)
    {
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var shortestSide = Math.Min(ActualWidth, ActualHeight);
        var baseRadius = shortestSide * 0.27;
        var maxLength = shortestSide * 0.2;

        dc.DrawEllipse(HaloCoreBrush, null, center,
            baseRadius * (0.35 + averageLevel * 0.25),
            baseRadius * (0.35 + averageLevel * 0.25));
        dc.DrawEllipse(null, HaloOuterPen, center, baseRadius, baseRadius);
        dc.DrawEllipse(null, HaloInnerPen, center, baseRadius * 0.72, baseRadius * 0.72);

        var count = displayedSpectrum.Length;
        for (var i = 0; i < count; i++)
        {
            var angle = -Math.PI / 2 + 2 * Math.PI * i / count;
            var cos = Math.Cos(angle);
            var sin = Math.Sin(angle);
            var level = displayedSpectrum[i];
            var startRadius = baseRadius + 3;
            var endRadius = startRadius + 3 + level * maxLength;
            dc.DrawLine(
                level > 0.55 ? HaloPinkPen : HaloTealPen,
                new Point(center.X + cos * startRadius, center.Y + sin * startRadius),
                new Point(center.X + cos * endRadius, center.Y + sin * endRadius));
        }
    }

    private static Brush CreateSpectrumBrush()
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 1),
            EndPoint = new Point(0, 0)
        };
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(71, 230, 208), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(57, 197, 187), 0.55));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 92, 174), 1));
        brush.Freeze();
        return brush;
    }

    private static SolidColorBrush CreateFrozenBrush(byte a, byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }

    private static Pen CreateFrozenPen(Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        pen.Freeze();
        return pen;
    }

    private static float SmoothingCoefficient(double elapsedSeconds, double timeConstantSeconds) =>
        (float)(1.0 - Math.Exp(-elapsedSeconds / timeConstantSeconds));
}
