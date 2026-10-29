using NAudio.Dsp;
using NAudio.Wave;

namespace Mimi.Services;

/// <summary>Analyzes playback audio into a smoothed 32-band logarithmic FFT spectrum.</summary>
public sealed class VisualizerSampleProvider : ISampleProvider
{
    private const int FftSize = 2048;
    private const int FftOrder = 11;
    public const int BandCount = 32;

    private readonly ISampleProvider source;
    private readonly Complex[] fftBuffer = new Complex[FftSize];
    private readonly float[] levels = new float[BandCount];
    private readonly float[] sampleWindow = new float[FftSize];
    private int samplesCollected;
    private readonly object levelsLock = new();
    private long totalSamplesRead;
    private long fftCount;
    private float lastInputPeak;
    private float lastRawMagnitude;

    public VisualizerSampleProvider(ISampleProvider source) => this.source = source;

    public string GetDiagnostics()
    {
        lock (levelsLock)
            return $"samples={totalSamplesRead}; fft={fftCount}; inputPeak={lastInputPeak:F5}; rawMagnitude={lastRawMagnitude:F6}; levelMax={levels.Max():F4}; levels={string.Join(',', levels.Select(v => v.ToString("F3")))}";
    }

    public WaveFormat WaveFormat => source.WaveFormat;

    public float[] GetLevels()
    {
        lock (levelsLock) return (float[])levels.Clone();
    }

    /// <summary>Fades the spectrum toward silence. Called while playback is
    /// paused or stopped so the bars fall instead of freezing mid-height.</summary>
    public void Decay()
    {
        lock (levelsLock)
            for (var i = 0; i < levels.Length; i++)
                levels[i] *= 0.82f;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        var read = source.Read(buffer, offset, count);
        if (read <= 0) return read;

        var peak = 0f;
        for (var i = 0; i < read; i++)
            peak = Math.Max(peak, Math.Abs(buffer[offset + i]));

        lock (levelsLock)
        {
            totalSamplesRead += read;
            lastInputPeak = peak;
        }

        var channels = Math.Max(1, WaveFormat.Channels);
        var frames = read / channels;
        for (var frame = 0; frame < frames; frame++)
        {
            double mono = 0;
            for (var channel = 0; channel < channels; channel++)
                mono += buffer[offset + frame * channels + channel];

            sampleWindow[samplesCollected++] = (float)(mono / channels);
            if (samplesCollected == FftSize)
            {
                AnalyzeWindow();
                samplesCollected = 0;
            }
        }

        return read;
    }

    private void AnalyzeWindow()
    {
        for (var i = 0; i < FftSize; i++)
        {
            var hann = 0.5 - 0.5 * Math.Cos(2.0 * Math.PI * i / (FftSize - 1));
            fftBuffer[i].X = (float)(sampleWindow[i] * hann);
            fftBuffer[i].Y = 0;
        }

        FastFourierTransform.FFT(true, FftOrder, fftBuffer);

        var maxBin = FftSize / 2;
        lock (levelsLock)
        {
            var maxMagnitude = 0.0;
            for (var band = 0; band < BandCount; band++)
            {
                var low = Math.Max(1, (int)Math.Floor(Math.Pow(maxBin, (double)band / BandCount)));
                var high = Math.Min(maxBin, Math.Max(low + 1,
                    (int)Math.Floor(Math.Pow(maxBin, (double)(band + 1) / BandCount))));

                double energy = 0;
                var bins = 0;
                for (var bin = low; bin < high; bin++)
                {
                    var real = fftBuffer[bin].X;
                    var imaginary = fftBuffer[bin].Y;
                    energy += real * real + imaginary * imaginary;
                    bins++;
                }

                var magnitude = bins > 0 ? Math.Sqrt(energy / bins) : 0;
                maxMagnitude = Math.Max(maxMagnitude, magnitude);

                // Diagnostics showed max band magnitude around 0.013 during playback.
                // Boost the measured spectrum before clamping it to a visible range.
                var target = (float)Math.Clamp(magnitude * 40.0, 0.025, 1.0);
                levels[band] = target > levels[band]
                    ? levels[band] + (target - levels[band]) * 0.72f
                    : levels[band] * 0.84f;
            }

            lastRawMagnitude = (float)maxMagnitude;
            fftCount++;
        }
    }
}