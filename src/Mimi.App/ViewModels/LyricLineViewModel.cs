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

    [ObservableProperty]
    private bool isCurrent;

    [ObservableProperty]
    private bool isPast;
}
