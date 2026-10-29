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
