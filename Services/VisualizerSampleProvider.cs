using System.Diagnostics;
using NAudio.Dsp;
using NAudio.Wave;

namespace Mimi.Services;

/// <summary>
/// Taps decoded audio into a lock-free ring buffer. A dedicated worker thread
/// analyzes the newest samples with an FFT and publishes immutable frames,
/// keeping FFT work off NAudio's playback callback thread.
/// </summary>
public sealed class VisualizerSampleProvider : ISampleProvider, IDisposable
{
    private const int FftSize = 2048;
    private const int FftOrder = 11;
    public const int BandCount = 32;

    private const int RingBufferSize = 32768;
    private const int RingBufferMask = RingBufferSize - 1;
    private const double MinFrequency = 30.0;
    private const double MaxFrequency = 16000.0;
    private const double DynamicRangeDb = 42.0;
    private const double SilencePeak = 0.0005;
    private const double AttackTimeConstantSeconds = 0.035;
    private const double ReleaseTimeConstantSeconds = 0.16;

    private readonly ISampleProvider source;
    private readonly float[] ringBuffer = new float[RingBufferSize];
    private readonly float[] analysisWindow = new float[FftSize];
    private readonly Complex[] fftBuffer = new Complex[FftSize];
    private readonly float[] bandDb = new float[BandCount];
    private readonly float[] spectrumLevels = new float[BandCount];
    private readonly Thread worker;

    private VisualizerFrame currentFrame = VisualizerFrame.Silence;
    private long writePosition;
    private long lastAnalyzedWritePosition;
    private long totalSamplesRead;
    private long fftCount;
    private long frameId;
    private float lastInputPeak;
    private float lastRawMagnitude;
    private float agcCeilingDb = -18.0f;
    private bool agcInitialized;
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
               $"fft={Interlocked.Read(ref fftCount)}; frame={frame.FrameId}; " +
               $"inputPeak={lastInputPeak:F5}; rawMagnitude={lastRawMagnitude:F6}; " +
               $"agcCeilingDb={agcCeilingDb:F1}; levelMax={levelMax:F4}";
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
            AnalyzeWindow(elapsedSeconds);
            PublishCurrentLevels(BuildWaveform());
            return;
        }

        if (DecaySpectrum(elapsedSeconds))
        {
            PublishCurrentLevels(new float[VisualizerFrame.WaveformPointCount]);
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

    private void AnalyzeWindow(double elapsedSeconds)
    {
        var windowPeak = 0.0f;
        for (var i = 0; i < FftSize; i++)
        {
            windowPeak = Math.Max(windowPeak, Math.Abs(analysisWindow[i]));
            var hann = 0.5 - 0.5 * Math.Cos(2.0 * Math.PI * i / (FftSize - 1));
            fftBuffer[i].X = (float)(analysisWindow[i] * hann);
            fftBuffer[i].Y = 0;
        }

        FastFourierTransform.FFT(true, FftOrder, fftBuffer);

        var sampleRate = Math.Max(1, WaveFormat.SampleRate);
        var binHz = (double)sampleRate / FftSize;
        var maxBin = FftSize / 2;
        var maxDb = -120.0;

        for (var band = 0; band < BandCount; band++)
        {
            var lowFrequency = MinFrequency *
                Math.Pow(MaxFrequency / MinFrequency, (double)band / BandCount);
            var highFrequency = MinFrequency *
                Math.Pow(MaxFrequency / MinFrequency, (double)(band + 1) / BandCount);

            var lowBin = Math.Clamp((int)Math.Floor(lowFrequency / binHz), 1, maxBin - 1);
            var highBin = Math.Clamp((int)Math.Ceiling(highFrequency / binHz), lowBin + 1, maxBin);

            double energy = 0;
            for (var bin = lowBin; bin < highBin; bin++)
            {
                var real = fftBuffer[bin].X;
                var imaginary = fftBuffer[bin].Y;
                energy += real * real + imaginary * imaginary;
            }

            var magnitude = Math.Sqrt(energy / (highBin - lowBin)) * 2.0 / FftSize;
            var centerFrequency = Math.Sqrt(lowFrequency * highFrequency);
            magnitude *= Math.Clamp(Math.Sqrt(centerFrequency / 1000.0), 0.75, 1.8);

            var db = 20.0 * Math.Log10(magnitude + 1e-12);
            bandDb[band] = (float)db;
            maxDb = Math.Max(maxDb, db);
        }

        lastRawMagnitude = (float)Math.Pow(10.0, maxDb / 20.0);
        Interlocked.Increment(ref fftCount);

        if (windowPeak <= SilencePeak)
        {
            DecaySpectrum(elapsedSeconds);
            return;
        }

        UpdateAutomaticGain(maxDb, elapsedSeconds);
        var floorDb = agcCeilingDb - DynamicRangeDb;
        var attack = SmoothingCoefficient(elapsedSeconds, AttackTimeConstantSeconds);
        var release = SmoothingCoefficient(elapsedSeconds, ReleaseTimeConstantSeconds);

        for (var band = 0; band < BandCount; band++)
        {
            var normalized = Math.Clamp((bandDb[band] - floorDb) / DynamicRangeDb, 0.0, 1.0);
            var target = (float)Math.Pow(normalized, 1.25);
            var current = spectrumLevels[band];
            var coefficient = target > current ? attack : release;
            spectrumLevels[band] = current + (target - current) * coefficient;
        }
    }

    private void UpdateAutomaticGain(double maxDb, double elapsedSeconds)
    {
        var db = (float)maxDb;
        if (!agcInitialized)
        {
            agcCeilingDb = db;
            agcInitialized = true;
        }
        else
        {
            // Fast attack for louder material, slower release so quieter songs
            // are lifted over a few seconds without pumping on every beat.
            var coefficient = SmoothingCoefficient(
                elapsedSeconds,
                db > agcCeilingDb ? 0.06 : 1.2);
            agcCeilingDb += (db - agcCeilingDb) * coefficient;
        }

        agcCeilingDb = Math.Clamp(agcCeilingDb, -55.0f, -3.0f);
    }

    private bool DecaySpectrum(double elapsedSeconds)
    {
        var coefficient = SmoothingCoefficient(elapsedSeconds, ReleaseTimeConstantSeconds);
        var anyAudible = false;
        for (var i = 0; i < spectrumLevels.Length; i++)
        {
            spectrumLevels[i] *= 1.0f - coefficient;
            if (spectrumLevels[i] < 0.001f)
                spectrumLevels[i] = 0;
            else
                anyAudible = true;
        }

        return anyAudible;
    }

    private float[] BuildWaveform()
    {
        var waveform = new float[VisualizerFrame.WaveformPointCount];
        for (var point = 0; point < waveform.Length; point++)
        {
            var start = point * FftSize / waveform.Length;
            var end = Math.Max(start + 1, (point + 1) * FftSize / waveform.Length);
            var selected = 0.0f;
            var selectedMagnitude = 0.0f;
            for (var i = start; i < end; i++)
            {
                var magnitude = Math.Abs(analysisWindow[i]);
                if (magnitude <= selectedMagnitude) continue;
                selectedMagnitude = magnitude;
                selected = analysisWindow[i];
            }

            waveform[point] = Math.Clamp(selected, -1.0f, 1.0f);
        }

        return waveform;
    }

    private void PublishCurrentLevels(float[] waveform)
    {
        var spectrum = (float[])spectrumLevels.Clone();
        Volatile.Write(ref currentFrame, new VisualizerFrame(
            spectrum,
            waveform,
            Interlocked.Increment(ref frameId)));
    }

    private static float SmoothingCoefficient(double elapsedSeconds, double timeConstantSeconds) =>
        (float)(1.0 - Math.Exp(-elapsedSeconds / timeConstantSeconds));
}
