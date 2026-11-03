using Mimi.Services;

namespace Mimi.Tests;

public sealed class SpectrumAnalyzerTests
{
    private const int SampleRate = 44100;

    [Theory]
    [InlineData(5, 6)]
    [InlineData(46, 17)]
    [InlineData(372, 28)]
    public void SineWavePeaksInExpectedLogarithmicBand(
        int fftBin,
        int expectedBand)
    {
        var analyzer = new SpectrumAnalyzer();
        var samples = CreateSine(fftBin, amplitude: 0.8f);

        AnalyzeRepeatedly(analyzer, samples, count: 30);

        Assert.InRange(PeakBand(analyzer), expectedBand - 1, expectedBand + 1);
    }

    [Fact]
    public void SilenceDecaysExistingLevelsToZero()
    {
        var analyzer = new SpectrumAnalyzer();
        AnalyzeRepeatedly(
            analyzer,
            CreateSine(46, amplitude: 0.8f),
            count: 30);
        var peakBeforeSilence = analyzer.CreateSpectrumSnapshot().Max();
        Assert.True(peakBeforeSilence > 0.25f);

        for (var i = 0; i < 100; i++)
            analyzer.Decay(0.016);

        Assert.All(
            analyzer.CreateSpectrumSnapshot(),
            level => Assert.Equal(0f, level));
    }

    [Fact]
    public void AutomaticGainMakesQuietAndLoudMaterialVisible()
    {
        var loud = new SpectrumAnalyzer();
        var quiet = new SpectrumAnalyzer();

        AnalyzeRepeatedly(loud, CreateSine(46, 0.8f), count: 80);
        AnalyzeRepeatedly(quiet, CreateSine(46, 0.01f), count: 80);

        Assert.True(loud.CreateSpectrumSnapshot().Max() > 0.35f);
        Assert.True(quiet.CreateSpectrumSnapshot().Max() > 0.35f);
        Assert.True(
            quiet.AutomaticGainCeilingDb < loud.AutomaticGainCeilingDb);
    }

    [Fact]
    public void InvalidWindowIsIgnored()
    {
        var analyzer = new SpectrumAnalyzer();

        analyzer.Analyze(new float[10], SampleRate, 0.016);

        Assert.Equal(0, analyzer.AnalysisCount);
        Assert.All(
            analyzer.CreateSpectrumSnapshot(),
            level => Assert.Equal(0f, level));
    }

    [Fact]
    public void WaveformPreservesSignedPeakInEachBucket()
    {
        var samples = new float[SpectrumAnalyzer.FftSize];
        samples[17] = -0.75f;
        samples[23] = 0.4f;

        var waveform = SpectrumAnalyzer.BuildWaveform(samples);

        Assert.Equal(VisualizerFrame.WaveformPointCount, waveform.Length);
        Assert.Equal(-0.75f, waveform[2]);
    }

    private static float[] CreateSine(int fftBin, float amplitude)
    {
        var samples = new float[SpectrumAnalyzer.FftSize];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = amplitude * (float)Math.Sin(
                2 * Math.PI * fftBin * i / SpectrumAnalyzer.FftSize);
        }

        return samples;
    }

    private static void AnalyzeRepeatedly(
        SpectrumAnalyzer analyzer,
        float[] samples,
        int count)
    {
        for (var i = 0; i < count; i++)
            analyzer.Analyze(samples, SampleRate, 0.016);
    }

    private static int PeakBand(SpectrumAnalyzer analyzer)
    {
        var levels = analyzer.CreateSpectrumSnapshot();
        var peakBand = 0;
        for (var i = 1; i < levels.Length; i++)
        {
            if (levels[i] > levels[peakBand])
                peakBand = i;
        }

        return peakBand;
    }
}
