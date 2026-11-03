using Mimi.Lyrics;

namespace Mimi.Tests;

public sealed class LyricsTextParserTests
{
    [Fact]
    public void ParseSortsTimedLinesAndExpandsRepeatedTimestamps()
    {
        var document = LyricsTextParser.Parse(
            """
            [ti:Neon Skyline]
            [ar:Mimi]
            [00:10.250]Second line
            [00:01.00][00:05.50]Repeated line
            """,
            LyricsSourceKind.SidecarFile);

        Assert.True(document.IsSynchronized);
        Assert.Equal(LyricsSourceKind.SidecarFile, document.Source);
        Assert.Equal(3, document.Lines.Count);
        Assert.Equal(TimeSpan.FromSeconds(1), document.Lines[0].Timestamp);
        Assert.Equal("Repeated line", document.Lines[0].Text);
        Assert.Equal(
            TimeSpan.FromMilliseconds(5500),
            document.Lines[1].Timestamp);
        Assert.Equal(
            TimeSpan.FromMilliseconds(10250),
            document.Lines[2].Timestamp);
    }

    [Fact]
    public void ParseStripsEnhancedWordTimestamps()
    {
        var document = LyricsTextParser.Parse(
            "[00:02.00]Sing <00:02.20>with <00:02.40>Mimi",
            LyricsSourceKind.SidecarFile);

        var line = Assert.Single(document.Lines);
        Assert.Equal("Sing with Mimi", line.Text);
    }

    [Fact]
    public void FindCurrentLineIndexAppliesDocumentAndUserOffsets()
    {
        var document = LyricsTextParser.Parse(
            """
            [offset:500]
            [00:02.00]First
            [00:04.00]Second
            """,
            LyricsSourceKind.SidecarFile);

        // A positive LRC document offset makes the first line effective at
        // 1.5s. A +0.25s user delay then moves it to 1.75s.
        Assert.Equal(
            TimeSpan.FromMilliseconds(1750),
            document.GetEffectiveTimestamp(
                document.Lines[0],
                TimeSpan.FromMilliseconds(250)));
        Assert.Equal(
            -1,
            document.FindCurrentLineIndex(
                TimeSpan.FromMilliseconds(1749),
                TimeSpan.FromMilliseconds(250)));
        Assert.Equal(
            0,
            document.FindCurrentLineIndex(
                TimeSpan.FromMilliseconds(1750),
                TimeSpan.FromMilliseconds(250)));
        Assert.Equal(
            1,
            document.FindCurrentLineIndex(
                TimeSpan.FromMilliseconds(3750),
                TimeSpan.FromMilliseconds(250)));
    }

    [Fact]
    public void ParseKeepsPlainEmbeddedLyricsUnsynchronized()
    {
        var document = LyricsTextParser.Parse(
            """

            First unsung line

            Second unsung line
            """,
            LyricsSourceKind.EmbeddedTag);

        Assert.False(document.IsSynchronized);
        Assert.Equal(LyricsSourceKind.EmbeddedTag, document.Source);
        Assert.Equal(2, document.Lines.Count);
        Assert.All(
            document.Lines,
            line => Assert.Null(line.Timestamp));
        Assert.Equal(
            -1,
            document.FindCurrentLineIndex(
                TimeSpan.FromSeconds(30),
                TimeSpan.Zero));
    }
}
