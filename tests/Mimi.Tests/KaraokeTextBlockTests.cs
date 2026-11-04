using System.Threading;
using System.Windows.Documents;
using System.Windows.Media;
using Mimi.Controls;
using Mimi.Lyrics;

namespace Mimi.Tests;

/// <summary>
/// The karaoke control itself: the fill has to follow the song, and a line
/// without word timings has to look exactly like it did before karaoke existed.
/// </summary>
public sealed class KaraokeTextBlockTests
{
    private static readonly Brush Sung = new SolidColorBrush(Colors.HotPink);
    private static readonly Brush Pending = new SolidColorBrush(Colors.LightSteelBlue);

    [Fact]
    public void APlainLineRendersAsText()
    {
        RunOnSta(() =>
        {
            var block = new KaraokeTextBlock { PlainText = "Just a line" };

            // TextBlock keeps a single run for its Text; the point is that a
            // plain line is not split into words.
            Assert.Equal("Just a line", block.Text);
            var run = Assert.Single(block.Inlines.OfType<Run>());
            Assert.Equal("Just a line", run.Text);
        });
    }

    [Fact]
    public void WordsBecomeRunsAndTheProgressColoursThem()
    {
        RunOnSta(() =>
        {
            var block = new KaraokeTextBlock
            {
                PlainText = "Sing with Mimi",
                Words = Words("Sing ", "with ", "Mimi"),
                SungBrush = Sung,
                PendingBrush = Pending,
                SungWordCount = 0
            };

            var runs = block.Inlines.OfType<Run>().ToArray();
            Assert.Equal(["Sing ", "with ", "Mimi"], runs.Select(run => run.Text));
            Assert.Same(Sung, runs[0].Foreground);
            Assert.Same(Pending, runs[1].Foreground);
            Assert.Same(Pending, runs[2].Foreground);

            // Moving into the line must move the fill, not rebuild the text.
            block.SungWordCount = 1;
            runs = block.Inlines.OfType<Run>().ToArray();
            Assert.Same(Sung, runs[0].Foreground);
            Assert.Same(Sung, runs[1].Foreground);
            Assert.Same(Pending, runs[2].Foreground);
        });
    }

    [Fact]
    public void BeforeTheLineStartsNothingIsSung()
    {
        RunOnSta(() =>
        {
            var block = new KaraokeTextBlock
            {
                Words = Words("One ", "two"),
                SungBrush = Sung,
                PendingBrush = Pending,
                SungWordCount = -1
            };

            var runs = block.Inlines.OfType<Run>().ToArray();
            Assert.All(runs, run => Assert.Same(Pending, run.Foreground));
        });
    }

    [Fact]
    public void DroppingTheWordsFallsBackToPlainText()
    {
        RunOnSta(() =>
        {
            var block = new KaraokeTextBlock
            {
                PlainText = "Plain again",
                Words = Words("Plain ", "again"),
                SungWordCount = 1
            };

            Assert.Equal(2, block.Inlines.OfType<Run>().Count());

            // This is what happens when the song moves on: the line that was
            // being sung becomes an ordinary line again.
            block.Words = null;

            Assert.Equal("Plain again", block.Text);
            var run = Assert.Single(block.Inlines.OfType<Run>());
            Assert.Equal("Plain again", run.Text);
        });
    }

    [Fact]
    public void EveryWordIsRenderedOnceEvenWithoutSpaces()
    {
        RunOnSta(() =>
        {
            var block = new KaraokeTextBlock
            {
                Words = Words("あ", "の", "歌"),
                SungBrush = Sung,
                PendingBrush = Pending,
                SungWordCount = 2
            };

            var runs = block.Inlines.OfType<Run>().ToArray();
            Assert.Equal(3, runs.Length);
            Assert.Equal("あの歌", string.Concat(runs.Select(run => run.Text)));
        });
    }

    private static IReadOnlyList<LyricWord> Words(params string[] texts)
    {
        var start = TimeSpan.Zero;
        var words = new List<LyricWord>();
        foreach (var text in texts)
        {
            words.Add(new LyricWord(start, text));
            start += TimeSpan.FromMilliseconds(500);
        }

        return words;
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
            throw new Xunit.Sdk.XunitException(failure.ToString());
    }
}
