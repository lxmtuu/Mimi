using Mimi.Services;

namespace Mimi.Tests;

public sealed class AudioOutputStatusTests
{
    [Fact]
    public void UnavailableStatus_NormalizesKindAndFallbackText()
    {
        var status = new AudioOutputStatus(
            false,
            AudioOutputKind.WasapiShared,
            " ",
            " ");

        Assert.False(status.IsAvailable);
        Assert.Equal(AudioOutputKind.None, status.Kind);
        Assert.Equal("No output device", status.DeviceName);
        Assert.Equal("No audio output is available.", status.Message);
    }

    [Fact]
    public void AvailableStatus_TrimsDeviceNameAndMessage()
    {
        var status = new AudioOutputStatus(
            true,
            AudioOutputKind.WasapiShared,
            "  Headphones  ",
            "  Ready.  ");

        Assert.True(status.IsAvailable);
        Assert.Equal(AudioOutputKind.WasapiShared, status.Kind);
        Assert.Equal("Headphones", status.DeviceName);
        Assert.Equal("Ready.", status.Message);
    }

    [Fact]
    public void UnavailableException_PreservesStatusAndInnerException()
    {
        var status = AudioOutputStatus.Unavailable("No speakers are connected.");
        var inner = new InvalidOperationException("Endpoint missing");

        var exception = new AudioOutputUnavailableException(status, inner);

        Assert.Equal(status, exception.Status);
        Assert.Equal(status.Message, exception.Message);
        Assert.Same(inner, exception.InnerException);
    }

    [Theory]
    [InlineData(null, AudioOutputBackendPreference.Automatic)]
    [InlineData("", AudioOutputBackendPreference.Automatic)]
    [InlineData(" auto ", AudioOutputBackendPreference.Automatic)]
    [InlineData("WASAPI", AudioOutputBackendPreference.ForceWasapiShared)]
    [InlineData("force-wasapi-shared", AudioOutputBackendPreference.ForceWasapiShared)]
    [InlineData("waveout", AudioOutputBackendPreference.ForceWaveOut)]
    [InlineData("force_wave_out", AudioOutputBackendPreference.ForceWaveOut)]
    public void BackendPreferenceParser_RecognizesDiagnosticValues(
        string? value,
        AudioOutputBackendPreference expected)
    {
        var parsed = AudioOutputBackendPreferenceParser.TryParse(
            value,
            out var preference);

        Assert.True(parsed);
        Assert.Equal(expected, preference);
    }

    [Fact]
    public void BackendPreferenceParser_RejectsUnknownValues()
    {
        var parsed = AudioOutputBackendPreferenceParser.TryParse(
            "directsound",
            out var preference);

        Assert.False(parsed);
        Assert.Equal(AudioOutputBackendPreference.Automatic, preference);
    }

    [Fact]
    public void UnavailableException_CoercesAnAvailableStatus()
    {
        var available = new AudioOutputStatus(
            true,
            AudioOutputKind.WaveOutFallback,
            "Speakers",
            "Ready.");

        var exception = new AudioOutputUnavailableException(available);

        Assert.False(exception.Status.IsAvailable);
        Assert.Equal(AudioOutputKind.None, exception.Status.Kind);
        Assert.Equal("The audio output is unavailable.", exception.Message);
    }
}
