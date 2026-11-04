using Mimi.Lyrics;

namespace Mimi.Tests;

/// <summary>
/// Word-level karaoke for Enhanced LRC. The load-bearing rule is that reading
/// word timings must never change what the line says: concatenating the words
/// has to reproduce the text exactly, or the same file would render differently
/// depending on whether the app understands the extra tags.
/// </summary>
public sealed class LyricsKaraokeTests
{
    [Fact]
    public void WordsCarryTheirOwnTimingsAndRebuildTheLine()
    {
        var document = LyricsTextParser.Parse(
            "[00:02.00]<00:02.00>Sing <00:02.20>with <00:02.40>Mimi",
            LyricsSourceKind.SidecarFile);

        var line = Assert.Single(document.Lines);
        Assert.Equal("Sing with Mimi", line.Text);
        Assert.True(line.HasWordTimings);

        var words = line.WordTimings;
        Assert.Equal(3, words.Count);
        Assert.Equal(["Sing ", "with ", "Mimi"], words.Select(word => word.Text));
        Assert.Equal(TimeSpan.FromSeconds(2), words[0].Timestamp);
        Assert.Equal(TimeSpan.FromMilliseconds(2200), words[1].Timestamp);
        Assert.Equal(TimeSpan.FromMilliseconds(2400), words[2].Timestamp);
        Assert.Equal(line.Text, string.Concat(words.Select(word => word.Text)));
    }

    [Fact]
    public void WordsInScriptsWithoutSpacesStayIntact()
    {
        // Japanese lyrics put one timing tag per syllable and usually leave no
        // space at all; inserting any would corrupt the line.
        var document = LyricsTextParser.Parse(
            "[00:01.00]<00:01.00>あ<00:01.50>の<00:02.00>歌",
            LyricsSourceKind.SidecarFile);

        var line = Assert.Single(document.Lines);
        Assert.Equal("あの歌", line.Text);
        Assert.Equal(["あ", "の", "歌"], line.WordTimings.Select(word => word.Text));
        Assert.Equal(line.Text, string.Concat(line.WordTimings.Select(word => word.Text)));
    }

    [Fact]
    public void TextBeforeTheFirstTagBelongsToTheFirstWord()
    {
        var document = LyricsTextParser.Parse(
            "[00:01.00]Oh <00:01.20>la <00:01.60>la",
            LyricsSourceKind.SidecarFile);

        var line = Assert.Single(document.Lines);
        Assert.Equal("Oh la la", line.Text);
        Assert.Equal(line.Text, string.Concat(line.WordTimings.Select(word => word.Text)));
    }

    [Fact]
    public void ALineWithoutWordTimingsSaysSo()
    {
        var document = LyricsTextParser.Parse(
            "[00:01.00]Just a line",
            LyricsSourceKind.SidecarFile);

        var line = Assert.Single(document.Lines);
        Assert.False(line.HasWordTimings);
        Assert.Empty(line.WordTimings);
        Assert.Equal("Just a line", line.Text);
    }

    [Fact]
    public void PlainEmbeddedLyricsNeverClaimWordTimings()
    {
        var document = LyricsTextParser.Parse(
            "First line\nSecond line",
            LyricsSourceKind.EmbeddedTag);

        Assert.All(document.Lines, line => Assert.False(line.HasWordTimings));
    }

    [Fact]
    public void TheWordCursorFollowsPlayback()
    {
        var document = LyricsTextParser.Parse(
            "[00:10.00]<00:10.00>One <00:10.50>two <00:11.00>three",
            LyricsSourceKind.SidecarFile);

        var line = Assert.Single(document.Lines);
        var noDelay = TimeSpan.Zero;

        Assert.Equal(-1, document.FindCurrentWordIndex(line, TimeSpan.FromSeconds(9), noDelay));
        Assert.Equal(0, document.FindCurrentWordIndex(line, TimeSpan.FromSeconds(10), noDelay));
        Assert.Equal(0, document.FindCurrentWordIndex(line, TimeSpan.FromMilliseconds(10400), noDelay));
        Assert.Equal(1, document.FindCurrentWordIndex(line, TimeSpan.FromMilliseconds(10500), noDelay));
        Assert.Equal(2, document.FindCurrentWordIndex(line, TimeSpan.FromSeconds(30), noDelay));
    }

    [Fact]
    public void TheWordCursorObeysBothOffsetConventions()
    {
        // The tag shifts the document, the user delay shifts the display; a word
        // has to answer to exactly the same arithmetic as its line.
        var document = LyricsTextParser.Parse(
            """
            [offset:500]
            [00:10.00]<00:10.00>One <00:10.50>two
            """,
            LyricsSourceKind.SidecarFile);

        var line = Assert.Single(document.Lines);

        // Document offset +500 ms brings the first word forward to 9.5s...
        Assert.Equal(
            0,
            document.FindCurrentWordIndex(line, TimeSpan.FromMilliseconds(9500), TimeSpan.Zero));

        // ...and a +0.3s user delay pushes it back to 9.8s.
        Assert.Equal(
            -1,
            document.FindCurrentWordIndex(
                line,
                TimeSpan.FromMilliseconds(9700),
                TimeSpan.FromMilliseconds(300)));
        Assert.Equal(
            0,
            document.FindCurrentWordIndex(
                line,
                TimeSpan.FromMilliseconds(9800),
                TimeSpan.FromMilliseconds(300)));
    }

    [Fact]
    public void ALineWithoutWordsHasNoCursor()
    {
        var document = LyricsTextParser.Parse(
            "[00:01.00]Plain",
            LyricsSourceKind.SidecarFile);

        Assert.Equal(
            -1,
            document.FindCurrentWordIndex(
                document.Lines[0],
                TimeSpan.FromSeconds(5),
                TimeSpan.Zero));
    }

    [Fact]
    public void RepeatedLineTimestampsKeepTheSameWords()
    {
        var document = LyricsTextParser.Parse(
            "[00:01.00][00:05.00]<00:01.00>Re <00:01.50>peat",
            LyricsSourceKind.SidecarFile);

        // The inline timings describe the first occurrence; the repeat reuses
        // the same text, so it reuses the same word breakdown rather than
        // inventing timings that are five seconds late.
        Assert.Equal(2, document.Lines.Count);
        Assert.All(document.Lines, line => Assert.Equal("Re peat", line.Text));
        Assert.All(document.Lines, line => Assert.Equal(2, line.WordTimings.Count));
    }
}
