using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using Mimi.Services;
using Serilog;

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
/// and rebuilds its frozen brush/pen palette only when theme colors change,
/// so the render loop does not allocate WPF controls, geometries, brushes,
/// or pens per frame.
/// </summary>
public sealed class VisualizerView : FrameworkElement
{
    private Brush spectrumBrush = null!;
    private Brush spectrumGlowBrush = null!;
    private Brush haloCoreBrush = null!;
    private Pen baselinePen = null!;
    private Pen peakPen = null!;
    private Pen wavePrimaryGlowPen = null!;
    private Pen wavePeakGlowPen = null!;
    private Pen wavePrimaryPen = null!;
    private Pen wavePeakPen = null!;
    private Pen haloOuterPen = null!;
    private Pen haloInnerPen = null!;
    private Pen haloPrimaryPen = null!;
    private Pen haloPeakPen = null!;

    private readonly DrawingVisual visual = new();
    private readonly float[] displayedSpectrum = new float[VisualizerFrame.SpectrumBandCount];
    private readonly float[] peakSpectrum = new float[VisualizerFrame.SpectrumBandCount];

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
        RebuildPalette();
        AddVisualChild(visual);
        AddLogicalChild(visual);
        ClipToBounds = true;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public Func<VisualizerFrame>? FrameProvider { get; set; }

    public static readonly DependencyProperty ModeProperty =
        DependencyProperty.Register(
            nameof(Mode),
            typeof(VisualizerDisplayMode),
            typeof(VisualizerView),
            new PropertyMetadata(VisualizerDisplayMode.NeonSpectrum));

    public VisualizerDisplayMode Mode
    {
        get => (VisualizerDisplayMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public static readonly DependencyProperty PrimaryColorProperty =
        DependencyProperty.Register(
            nameof(PrimaryColor),
            typeof(Color),
            typeof(VisualizerView),
            new PropertyMetadata(
                Color.FromRgb(0x47, 0xE6, 0xD0),
                OnThemeColorChanged));

    public Color PrimaryColor
    {
        get => (Color)GetValue(PrimaryColorProperty);
        set => SetValue(PrimaryColorProperty, value);
    }

    public static readonly DependencyProperty SecondaryColorProperty =
        DependencyProperty.Register(
            nameof(SecondaryColor),
            typeof(Color),
            typeof(VisualizerView),
            new PropertyMetadata(
                Color.FromRgb(0xFF, 0x8F, 0xCB),
                OnThemeColorChanged));

    public Color SecondaryColor
    {
        get => (Color)GetValue(SecondaryColorProperty);
        set => SetValue(SecondaryColorProperty, value);
    }

    public static readonly DependencyProperty PeakColorProperty =
        DependencyProperty.Register(
            nameof(PeakColor),
            typeof(Color),
            typeof(VisualizerView),
            new PropertyMetadata(
                Color.FromRgb(0xFF, 0x5C, 0xAE),
                OnThemeColorChanged));

    public Color PeakColor
    {
        get => (Color)GetValue(PeakColorProperty);
        set => SetValue(PeakColorProperty, value);
    }

    public static readonly DependencyProperty BaselineColorProperty =
        DependencyProperty.Register(
            nameof(BaselineColor),
            typeof(Color),
            typeof(VisualizerView),
            new PropertyMetadata(
                Color.FromRgb(0x82, 0x96, 0xAE),
                OnThemeColorChanged));

    public Color BaselineColor
    {
        get => (Color)GetValue(BaselineColorProperty);
        set => SetValue(BaselineColorProperty, value);
    }

    private static void OnThemeColorChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e) =>
        ((VisualizerView)dependencyObject).RebuildPalette();

    private void RebuildPalette()
    {
        var primary = WithAlpha(PrimaryColor, 255);
        var secondary = WithAlpha(SecondaryColor, 255);
        var peak = WithAlpha(PeakColor, 255);
        var baseline = BaselineColor;

        spectrumBrush = CreateSpectrumBrush(primary, peak);
        spectrumGlowBrush = CreateFrozenBrush(WithAlpha(primary, 52));
        haloCoreBrush = CreateFrozenBrush(WithAlpha(primary, 48));
        baselinePen = CreateFrozenPen(
            CreateFrozenBrush(WithAlpha(baseline, 90)),
            1.0);
        peakPen = CreateFrozenPen(CreateFrozenBrush(secondary), 2.0);
        wavePrimaryGlowPen = CreateFrozenPen(
            CreateFrozenBrush(WithAlpha(primary, 64)),
            5.0);
        wavePeakGlowPen = CreateFrozenPen(
            CreateFrozenBrush(WithAlpha(peak, 56)),
            5.0);
        wavePrimaryPen = CreateFrozenPen(CreateFrozenBrush(primary), 2.0);
        wavePeakPen = CreateFrozenPen(CreateFrozenBrush(peak), 2.0);
        haloOuterPen = CreateFrozenPen(
            CreateFrozenBrush(WithAlpha(primary, 120)),
            1.5);
        haloInnerPen = CreateFrozenPen(
            CreateFrozenBrush(WithAlpha(secondary, 105)),
            1.25);
        haloPrimaryPen = CreateFrozenPen(CreateFrozenBrush(primary), 3.0);
        haloPeakPen = CreateFrozenPen(CreateFrozenBrush(peak), 3.0);
    }

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

    /// <summary>Draw faults already logged, so a stuck loop logs once.</summary>
    private readonly HashSet<string> reportedDrawFailures =
        new(StringComparer.Ordinal);

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
        try
        {
            Draw(frame, smoothingElapsed);
        }
        catch (Exception ex)
        {
            // The visualiser is decoration: one bad frame must cost one frame,
            // not the window. Without this, a fault in here reaches the
            // dispatcher and opens a modal dialog from inside the render loop -
            // sixty times a second, which buries the app instead of reporting
            // anything useful.
            if (reportedDrawFailures.Add(CrashReport.Signature(ex)))
            {
                Log.Error(
                    ex,
                    "The visualiser could not draw a frame; drawing continues");
            }
        }

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
        dc.DrawLine(baselinePen, new Point(0, baseline), new Point(width, baseline));

        for (var i = 0; i < count; i++)
        {
            var level = displayedSpectrum[i];
            var barHeight = 3 + level * maxHeight;
            var x = i * (barWidth + gap);
            var y = baseline - barHeight;
            var radius = Math.Min(3, barWidth / 2);

            dc.DrawRoundedRectangle(
                spectrumGlowBrush,
                null,
                new Rect(Math.Max(0, x - 1), Math.Max(0, y - 3), barWidth + 2, barHeight + 3),
                radius + 1,
                radius + 1);
            dc.DrawRoundedRectangle(
                spectrumBrush,
                null,
                new Rect(x, y, barWidth, barHeight),
                radius,
                radius);

            var peakY = baseline - (3 + peakSpectrum[i] * maxHeight) - 3;
            dc.DrawLine(peakPen, new Point(x, peakY), new Point(x + barWidth, peakY));
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
        dc.DrawLine(baselinePen, new Point(0, centerY), new Point(width, centerY));

        var first = Math.Clamp(frame.Waveform[0], -1.0f, 1.0f);
        var previousTop = new Point(0, centerY - first * amplitude);
        var previousBottom = new Point(0, centerY + first * amplitude);

        for (var i = 1; i < count; i++)
        {
            var x = width * i / (count - 1);
            var value = Math.Clamp(frame.Waveform[i], -1.0f, 1.0f);
            var top = new Point(x, centerY - value * amplitude);
            var bottom = new Point(x, centerY + value * amplitude);

            dc.DrawLine(wavePrimaryGlowPen, previousTop, top);
            dc.DrawLine(wavePeakGlowPen, previousBottom, bottom);
            dc.DrawLine(wavePrimaryPen, previousTop, top);
            dc.DrawLine(wavePeakPen, previousBottom, bottom);

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

        dc.DrawEllipse(haloCoreBrush, null, center,
            baseRadius * (0.35 + averageLevel * 0.25),
            baseRadius * (0.35 + averageLevel * 0.25));
        dc.DrawEllipse(null, haloOuterPen, center, baseRadius, baseRadius);
        dc.DrawEllipse(null, haloInnerPen, center, baseRadius * 0.72, baseRadius * 0.72);

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
                level > 0.55 ? haloPeakPen : haloPrimaryPen,
                new Point(center.X + cos * startRadius, center.Y + sin * startRadius),
                new Point(center.X + cos * endRadius, center.Y + sin * endRadius));
        }
    }

    private static Brush CreateSpectrumBrush(Color primary, Color peak)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 1),
            EndPoint = new Point(0, 0)
        };
        brush.GradientStops.Add(new GradientStop(primary, 0));
        brush.GradientStops.Add(new GradientStop(
            Blend(primary, Color.FromRgb(0, 0, 0), 0.18),
            0.55));
        brush.GradientStops.Add(new GradientStop(peak, 1));
        brush.Freeze();
        return brush;
    }

    private static SolidColorBrush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Color WithAlpha(Color color, byte alpha) =>
        Color.FromArgb(alpha, color.R, color.G, color.B);

    private static Color Blend(Color from, Color to, double amount)
    {
        var clamped = Math.Clamp(amount, 0, 1);
        return Color.FromArgb(
            BlendChannel(from.A, to.A, clamped),
            BlendChannel(from.R, to.R, clamped),
            BlendChannel(from.G, to.G, clamped),
            BlendChannel(from.B, to.B, clamped));
    }

    private static byte BlendChannel(byte from, byte to, double amount) =>
        (byte)Math.Clamp(
            Math.Round(from + (to - from) * amount),
            0,
            255);

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
