using Mimi.Services;

namespace Mimi.Tests;

public sealed class SupportedAudioFilesTests
{
    [Theory]
    [InlineData(@"C:\Music\song.mp3", true)]
    [InlineData(@"C:\Music\song.FLAC", true)]
    [InlineData(@"C:\Music\song.m4a", true)]
    [InlineData(@"C:\Music\song.ogg", false)]
    [InlineData(@"C:\Music\cover.jpg", false)]
    [InlineData(@"C:\Music\extensionless", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsSupported_MatchesKnownExtensions(string? path, bool expected) =>
        Assert.Equal(expected, SupportedAudioFiles.IsSupported(path));

    [Fact]
    public void ExtensionsAndDialogPatternStayInSync()
    {
        Assert.Equal(
            [".mp3", ".wav", ".aiff", ".aif", ".wma", ".m4a", ".aac", ".flac"],
            SupportedAudioFiles.Extensions);
        Assert.Equal(
            "*.mp3;*.wav;*.aiff;*.aif;*.wma;*.m4a;*.aac;*.flac",
            SupportedAudioFiles.FileDialogPattern);
    }
}
