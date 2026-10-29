using NAudio.Dsp;

namespace Mimi.Services;

/// <summary>
/// The stateful, testable analysis stage of the Stage Engine. It applies a
/// Hann window, calculates logarithmic FFT bands, converts them to dB,
/// applies AGC, and smooths or decays the displayed spectrum. Instances are
/// intended to be called from one analysis thread.
/// </summary>
public sealed class SpectrumAnalyzer
{
    public const int FftSize = 2048;
    public const int BandCount = VisualizerFrame.SpectrumBandCount;

    private const int FftOrder = 11;
    private const double MinFrequency = 30.0;
    private const double MaxFrequency = 16000.0;
    private const double DynamicRangeDb = 42.0;
    private const double SilencePeak = 0.0005;
    private const double AttackTimeConstantSeconds = 0.035;
    private const double ReleaseTimeConstantSeconds = 0.16;

    private readonly Complex[] fftBuffer = new Complex[FftSize];
    private readonly float[] bandDb = new float[BandCount];
    private readonly float[] spectrumLevels = new float[BandCount];

    private float automaticGainCeilingDb = -18.0f;
    private bool automaticGainInitialized;

    public long AnalysisCount { get; private set; }

    public float LastRawMagnitude { get; private set; }

    public float AutomaticGainCeilingDb => automaticGainCeilingDb;

    public ReadOnlySpan<float> CurrentLevels => spectrumLevels;

    public float[] CreateSpectrumSnapshot() => (float[])spectrumLevels.Clone();

    public void Analyze(
        ReadOnlySpan<float> samples,
        int sampleRate,
        double elapsedSeconds)
    {
        if (samples.Length != FftSize || sampleRate <= 0) return;

        var elapsed = Math.Clamp(elapsedSeconds, 0.001, 0.1);
        var windowPeak = 0.0f;

        for (var i = 0; i < FftSize; i++)
        {
            windowPeak = Math.Max(windowPeak, Math.Abs(samples[i]));
            var hann = 0.5 - 0.5 * Math.Cos(
                2.0 * Math.PI * i / (FftSize - 1));
            fftBuffer[i].X = (float)(samples[i] * hann);
            fftBuffer[i].Y = 0;
        }

        FastFourierTransform.FFT(true, FftOrder, fftBuffer);

        var binHz = (double)sampleRate / FftSize;
        var maxBin = FftSize / 2;
        var maxDb = -120.0;

        for (var band = 0; band < BandCount; band++)
        {
            var lowFrequency = MinFrequency *
                Math.Pow(MaxFrequency / MinFrequency, (double)band / BandCount);
            var highFrequency = MinFrequency *
                Math.Pow(
                    MaxFrequency / MinFrequency,
                    (double)(band + 1) / BandCount);

            var lowBin = Math.Clamp(
                (int)Math.Floor(lowFrequency / binHz),
                1,
                maxBin - 1);
            var highBin = Math.Clamp(
                (int)Math.Ceiling(highFrequency / binHz),
                lowBin + 1,
                maxBin);

            double energy = 0;
            for (var bin = lowBin; bin < highBin; bin++)
            {
                var real = fftBuffer[bin].X;
                var imaginary = fftBuffer[bin].Y;
                energy += real * real + imaginary * imaginary;
            }

            var magnitude = Math.Sqrt(
                energy / (highBin - lowBin)) * 2.0 / FftSize;
            var centerFrequency = Math.Sqrt(lowFrequency * highFrequency);
            magnitude *= Math.Clamp(
                Math.Sqrt(centerFrequency / 1000.0),
                0.75,
                1.8);

            var db = 20.0 * Math.Log10(magnitude + 1e-12);
            bandDb[band] = (float)db;
            maxDb = Math.Max(maxDb, db);
        }

        LastRawMagnitude = (float)Math.Pow(10.0, maxDb / 20.0);
        AnalysisCount++;

        if (windowPeak <= SilencePeak)
        {
            Decay(elapsed);
            return;
        }

        UpdateAutomaticGain(maxDb, elapsed);
        var floorDb = automaticGainCeilingDb - DynamicRangeDb;
        var attack = SmoothingCoefficient(
            elapsed,
            AttackTimeConstantSeconds);
        var release = SmoothingCoefficient(
            elapsed,
            ReleaseTimeConstantSeconds);

        for (var band = 0; band < BandCount; band++)
        {
            var normalized = Math.Clamp(
                (bandDb[band] - floorDb) / DynamicRangeDb,
                0.0,
                1.0);
            var target = (float)Math.Pow(normalized, 1.25);
            var current = spectrumLevels[band];
            var coefficient = target > current ? attack : release;
            spectrumLevels[band] =
                current + (target - current) * coefficient;
        }
    }

    public bool Decay(double elapsedSeconds)
    {
        var elapsed = Math.Clamp(elapsedSeconds, 0.001, 0.1);
        var coefficient = SmoothingCoefficient(
            elapsed,
            ReleaseTimeConstantSeconds);
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

    public static float[] BuildWaveform(ReadOnlySpan<float> samples)
    {
        var waveform = new float[VisualizerFrame.WaveformPointCount];
        if (samples.Length != FftSize) return waveform;

        for (var point = 0; point < waveform.Length; point++)
        {
            var start = point * FftSize / waveform.Length;
            var end = Math.Max(
                start + 1,
                (point + 1) * FftSize / waveform.Length);
            var selected = 0.0f;
            var selectedMagnitude = 0.0f;

            for (var i = start; i < end; i++)
            {
                var magnitude = Math.Abs(samples[i]);
                if (magnitude <= selectedMagnitude) continue;

                selectedMagnitude = magnitude;
                selected = samples[i];
            }

            waveform[point] = Math.Clamp(selected, -1.0f, 1.0f);
        }

        return waveform;
    }

    private void UpdateAutomaticGain(double maxDb, double elapsedSeconds)
    {
        var db = (float)maxDb;
        if (!automaticGainInitialized)
        {
            automaticGainCeilingDb = db;
            automaticGainInitialized = true;
        }
        else
        {
            // Fast attack for louder material, slower release so quieter
            // songs are lifted over a few seconds without pumping per beat.
            var coefficient = SmoothingCoefficient(
                elapsedSeconds,
                db > automaticGainCeilingDb ? 0.06 : 1.2);
            automaticGainCeilingDb +=
                (db - automaticGainCeilingDb) * coefficient;
        }

        automaticGainCeilingDb = Math.Clamp(
            automaticGainCeilingDb,
            -55.0f,
            -3.0f);
    }

    private static float SmoothingCoefficient(
        double elapsedSeconds,
        double timeConstantSeconds) =>
        (float)(1.0 - Math.Exp(-elapsedSeconds / timeConstantSeconds));
}
