using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Mimi.Lyrics;

namespace Mimi.Controls;

/// <summary>
/// A lyric line that can be highlighted word by word.
///
/// The colouring is the whole point: singing along only works when the eye can
/// see how far into the line the track is, so the words already sung are drawn
/// in <see cref="SungBrush"/> and the rest in <see cref="PendingBrush"/>. When a
/// source has no word timings the control behaves like a plain
/// <see cref="TextBlock"/> showing <see cref="PlainText"/>, which is what most
/// LRC files need.
///
/// Only the ranges are rebuilt when the line changes, and only the brushes are
/// touched when the song moves to the next word, so a 10 Hz lyric tick does not
/// churn the visual tree.
/// </summary>
public sealed class KaraokeTextBlock : TextBlock
{
    public static readonly DependencyProperty PlainTextProperty =
        DependencyProperty.Register(
            nameof(PlainText),
            typeof(string),
            typeof(KaraokeTextBlock),
            new PropertyMetadata(string.Empty, OnContentChanged));

    /// <summary>
    /// Word timings for this line. Empty means the line is not word-timed and
    /// <see cref="PlainText"/> is shown instead.
    /// </summary>
    public static readonly DependencyProperty WordsProperty =
        DependencyProperty.Register(
            nameof(Words),
            typeof(IReadOnlyList<LyricWord>),
            typeof(KaraokeTextBlock),
            new PropertyMetadata(null, OnContentChanged));

    /// <summary>How many words have been sung, or -1 before the line starts.</summary>
    public static readonly DependencyProperty SungWordCountProperty =
        DependencyProperty.Register(
            nameof(SungWordCount),
            typeof(int),
            typeof(KaraokeTextBlock),
            new PropertyMetadata(-1, OnProgressChanged));

    /// <summary>Colour of the words already sung.</summary>
    public static readonly DependencyProperty SungBrushProperty =
        DependencyProperty.Register(
            nameof(SungBrush),
            typeof(Brush),
            typeof(KaraokeTextBlock),
            new PropertyMetadata(null, OnProgressChanged));

    /// <summary>Colour of the words still to come.</summary>
    public static readonly DependencyProperty PendingBrushProperty =
        DependencyProperty.Register(
            nameof(PendingBrush),
            typeof(Brush),
            typeof(KaraokeTextBlock),
            new PropertyMetadata(null, OnProgressChanged));

    public KaraokeTextBlock()
    {
        // A derived TextBlock does not pick the app's implicit TextBlock style
        // (implicit styles match the exact type), so the bundled font has to be
        // requested here or lyrics would render in the system face.
        SetResourceReference(FontFamilyProperty, "MimiUiFontFamily");
    }

    public string PlainText
    {
        get => (string)GetValue(PlainTextProperty);
        set => SetValue(PlainTextProperty, value);
    }

    public IReadOnlyList<LyricWord>? Words
    {
        get => (IReadOnlyList<LyricWord>?)GetValue(WordsProperty);
        set => SetValue(WordsProperty, value);
    }

    public int SungWordCount
    {
        get => (int)GetValue(SungWordCountProperty);
        set => SetValue(SungWordCountProperty, value);
    }

    public Brush? SungBrush
    {
        get => (Brush?)GetValue(SungBrushProperty);
        set => SetValue(SungBrushProperty, value);
    }

    public Brush? PendingBrush
    {
        get => (Brush?)GetValue(PendingBrushProperty);
        set => SetValue(PendingBrushProperty, value);
    }

    private static void OnContentChanged(
        DependencyObject sender,
        DependencyPropertyChangedEventArgs args) =>
        ((KaraokeTextBlock)sender).Rebuild();

    private static void OnProgressChanged(
        DependencyObject sender,
        DependencyPropertyChangedEventArgs args) =>
        ((KaraokeTextBlock)sender).ApplyProgress();

    private void Rebuild()
    {
        Inlines.Clear();

        var words = Words;
        if (words is null || words.Count == 0)
        {
            // Not word-timed: behave like the TextBlock this class is.
            Text = PlainText ?? string.Empty;
            return;
        }

        Text = string.Empty;
        foreach (var word in words)
            Inlines.Add(new Run(word.Text));

        ApplyProgress();
    }

    private void ApplyProgress()
    {
        var words = Words;
        if (words is null || words.Count == 0) return;

        var sung = Math.Clamp(SungWordCount, -1, Inlines.Count);
        var index = 0;
        foreach (var inline in Inlines)
        {
            if (inline is not Run run) continue;

            run.Foreground = index <= sung
                ? SungBrush ?? Foreground
                : PendingBrush ?? Foreground;
            index++;
        }
    }
}
