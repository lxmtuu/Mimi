using CommunityToolkit.Mvvm.ComponentModel;
using Mimi.Lyrics;

namespace Mimi.ViewModels;

/// <summary>Presentation state for one line in the Now Playing lyrics card.</summary>
public sealed partial class LyricLineViewModel : ObservableObject
{
    public LyricLineViewModel(LyricLine line) =>
        Line = line ?? throw new ArgumentNullException(nameof(line));

    public LyricLine Line { get; }

    public string Text => Line.Text;

    public TimeSpan? Timestamp => Line.Timestamp;

    /// <summary>Word timings, empty for the usual line-only lyric file.</summary>
    public IReadOnlyList<LyricWord> Words => Line.WordTimings;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KaraokeWords))]
    private bool isCurrent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KaraokeWords))]
    private bool isPast;

    /// <summary>
    /// How many words of this line have been sung, or -1 before it starts.
    /// Only the current line ever carries progress.
    /// </summary>
    [ObservableProperty]
    private int sungWordCount = -1;

    /// <summary>
    /// The words to highlight, or null when this line is not the one being sung.
    /// Highlighting a past or coming line would just be noise: the eye is on the
    /// line under the cursor.
    /// </summary>
    public IReadOnlyList<LyricWord>? KaraokeWords =>
        IsCurrent && Words.Count > 0 ? Words : null;
}
