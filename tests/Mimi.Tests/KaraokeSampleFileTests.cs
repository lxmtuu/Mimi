using Mimi.Lyrics;
using Xunit;

namespace Mimi.Tests;

/// <summary>
/// The sample Enhanced LRC file shipped for the runtime karaoke check
/// (<c>docs/examples/karaoke-sample.lrc</c>) is a fixture like any other: if it
/// drifts away from what the parser understands, the check it exists for
/// quietly stops proving anything. Parsing it here keeps it honest.
/// </summary>
public sealed class KaraokeSampleFileTests
{
    [Fact]
    public void TheSampleParsesIntoWordTimedLines()
    {
        var document = ParseSample();

        Assert.True(document.IsSynchronized);
        Assert.NotEmpty(document.Lines);

        // The file has to exercise both paths, or the person running the check
        // cannot tell a plain line from a broken word-timed one.
        Assert.Contains(document.Lines, line => line.HasWordTimings);
        Assert.Contains(document.Lines, line => !line.HasWordTimings);
    }

    [Fact]
    public void EveryLineInTheSampleKeepsTheJoinInvariant()
    {
        foreach (var line in ParseSample().Lines)
        {
            if (!line.HasWordTimings) continue;

            var joined = string.Concat(line.WordTimings.Select(word => word.Text));
            Assert.Equal(line.Text, joined);
        }
    }

    [Fact]
    public void WordTimingsInTheSampleOnlyMoveForward()
    {
        foreach (var line in ParseSample().Lines)
        {
            var sameLine = line.WordTimings.ToArray();
            for (var i = 1; i < sameLine.Length; i++)
            {
                Assert.True(
                    sameLine[i].Timestamp >= sameLine[i - 1].Timestamp,
                    $"Word {i} of \"{line.Text}\" starts before the word in front of it.");
            }

            // A word cannot start before its own line.
            if (line.Timestamp is { } lineStart && sameLine.Length > 0)
                Assert.True(sameLine[0].Timestamp >= lineStart);
        }
    }

    [Fact]
    public void TheSampleHasNoCommentLinesThatWouldShowUpOnScreen()
    {
        // The parser has no comment syntax: an unrecognised line becomes a
        // lyric. Instructions therefore belong in the documentation, never in
        // this file.
        foreach (var line in ParseSample().Lines)
            Assert.DoesNotContain("#", line.Text, StringComparison.Ordinal);
    }

    private static LyricsDocument ParseSample()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "docs",
            "examples",
            "karaoke-sample.lrc");
        Assert.True(File.Exists(path), $"The karaoke sample is missing: {path}");
        return LyricsTextParser.Parse(File.ReadAllText(path), LyricsSourceKind.SidecarFile);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Mimi.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new Xunit.Sdk.XunitException(
            "Could not locate the repository root (Mimi.sln) above " +
            AppContext.BaseDirectory);
    }
}
