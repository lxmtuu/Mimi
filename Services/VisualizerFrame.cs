namespace Mimi.Services;

/// <summary>
/// An immutable snapshot published by the visualizer analysis worker.
/// The renderer may read a frame without locking because the arrays owned by
/// a published frame are never modified afterwards.
/// </summary>
public sealed class VisualizerFrame
{
    public const int WaveformPointCount = 256;

    internal VisualizerFrame(float[] spectrum, float[] waveform, long frameId)
    {
        Spectrum = spectrum;
        Waveform = waveform;
        FrameId = frameId;
    }

    public static VisualizerFrame Silence { get; } = new(
        new float[VisualizerSampleProvider.BandCount],
        new float[WaveformPointCount],
        frameId: 0);

    public IReadOnlyList<float> Spectrum { get; }

    public IReadOnlyList<float> Waveform { get; }

    public long FrameId { get; }
}
