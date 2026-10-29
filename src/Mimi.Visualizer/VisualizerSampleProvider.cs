using System.Diagnostics;
using NAudio.Wave;

namespace Mimi.Services;

/// <summary>
/// Taps decoded audio into a lock-free ring buffer. A dedicated worker thread
/// analyzes the newest samples with an FFT and publishes immutable frames,
/// keeping FFT work off NAudio's playback callback thread.
/// </summary>
public sealed class VisualizerSampleProvider : ISampleProvider, IDisposable
{
    public const int BandCount = SpectrumAnalyzer.BandCount;

    private const int FftSize = SpectrumAnalyzer.FftSize;
    private const int RingBufferSize = 32768;
    private const int RingBufferMask = RingBufferSize - 1;

    private readonly ISampleProvider source;
    private readonly SpectrumAnalyzer analyzer = new();
    private readonly float[] ringBuffer = new float[RingBufferSize];
    private readonly float[] analysisWindow = new float[FftSize];
    private readonly Thread worker;

    private VisualizerFrame currentFrame = VisualizerFrame.Silence;
    private long writePosition;
    private long lastAnalyzedWritePosition;
    private long totalSamplesRead;
    private long frameId;
    private float lastInputPeak;
    private volatile bool disposed;

    public VisualizerSampleProvider(ISampleProvider source)
    {
        this.source = source;
        worker = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = "Mimi Visualizer FFT",
            Priority = ThreadPriority.BelowNormal
        };
        worker.Start();
    }

    public WaveFormat WaveFormat => source.WaveFormat;

    public VisualizerFrame GetFrame() => Volatile.Read(ref currentFrame);

    public string GetDiagnostics()
    {
        var frame = GetFrame();
        var levelMax = 0.0f;
        for (var i = 0; i < frame.Spectrum.Count; i++)
            levelMax = Math.Max(levelMax, frame.Spectrum[i]);

        return $"samples={Interlocked.Read(ref totalSamplesRead)}; " +
               $"fft={analyzer.AnalysisCount}; frame={frame.FrameId}; " +
               $"inputPeak={lastInputPeak:F5}; " +
               $"rawMagnitude={analyzer.LastRawMagnitude:F6}; " +
               $"agcCeilingDb={analyzer.AutomaticGainCeilingDb:F1}; " +
               $"levelMax={levelMax:F4}";
    }

    public int Read(float[] buffer, int offset, int count)
    {
        var read = source.Read(buffer, offset, count);
        if (read <= 0) return read;

        var channels = Math.Max(1, WaveFormat.Channels);
        var frames = read / channels;
        var peak = 0.0f;
        var position = Volatile.Read(ref writePosition);

        for (var frame = 0; frame < frames; frame++)
        {
            double mono = 0;
            var sampleOffset = offset + frame * channels;
            for (var channel = 0; channel < channels; channel++)
                mono += buffer[sampleOffset + channel];

            var sample = (float)(mono / channels);
            peak = Math.Max(peak, Math.Abs(sample));
            ringBuffer[(int)(position & RingBufferMask)] = sample;
            position++;
        }

        // Publish the new write position only after the samples are in place.
        Volatile.Write(ref writePosition, position);
        Interlocked.Add(ref totalSamplesRead, frames);
        lastInputPeak = peak;
        return read;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (Thread.CurrentThread != worker)
            worker.Join(TimeSpan.FromMilliseconds(250));
    }

    private void WorkerLoop()
    {
        var lastTimestamp = Stopwatch.GetTimestamp();
        while (!disposed)
        {
            Thread.Sleep(16);
            if (disposed) break;

            var now = Stopwatch.GetTimestamp();
            var elapsed = Stopwatch.GetElapsedTime(lastTimestamp, now);
            lastTimestamp = now;
            try
            {
                PublishFrame(elapsed);
            }
            catch (Exception)
            {
                // Visualization must never take playback down with it.
                if (!disposed)
                    Volatile.Write(ref currentFrame, VisualizerFrame.Silence);
            }
        }
    }

    private void PublishFrame(TimeSpan elapsed)
    {
        var elapsedSeconds = Math.Clamp(elapsed.TotalSeconds, 0.001, 0.1);
        var write = Volatile.Read(ref writePosition);

        if (write != lastAnalyzedWritePosition && write >= FftSize)
        {
            lastAnalyzedWritePosition = write;
            CopyLatestWindow(write);
            analyzer.Analyze(
                analysisWindow,
                WaveFormat.SampleRate,
                elapsedSeconds);
            PublishCurrentLevels(
                SpectrumAnalyzer.BuildWaveform(analysisWindow));
            return;
        }

        if (analyzer.Decay(elapsedSeconds))
        {
            PublishCurrentLevels(
                new float[VisualizerFrame.WaveformPointCount]);
        }
        else if (!ReferenceEquals(GetFrame(), VisualizerFrame.Silence))
        {
            Volatile.Write(ref currentFrame, VisualizerFrame.Silence);
        }
    }

    private void CopyLatestWindow(long write)
    {
        var start = write - FftSize;
        for (var i = 0; i < FftSize; i++)
            analysisWindow[i] = ringBuffer[(int)((start + i) & RingBufferMask)];
    }

    private void PublishCurrentLevels(float[] waveform)
    {
        Volatile.Write(ref currentFrame, new VisualizerFrame(
            analyzer.CreateSpectrumSnapshot(),
            waveform,
            Interlocked.Increment(ref frameId)));
    }
}
