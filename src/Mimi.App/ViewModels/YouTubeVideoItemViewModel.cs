using CommunityToolkit.Mvvm.ComponentModel;
using Mimi.YouTube;

namespace Mimi.ViewModels;

/// <summary>
/// One YouTube search result in the list. It is not a
/// <see cref="TrackItemViewModel"/>: nothing here is ever added to the local
/// library, queue, history, favorites, lyrics, or saved session.
/// </summary>
public sealed partial class YouTubeVideoItemViewModel : ObservableObject
{
    [ObservableProperty]
    private bool isCurrent;

    public YouTubeVideoItemViewModel(YouTubeVideo video)
    {
        Video = video;
    }

    public YouTubeVideo Video { get; }

    public string VideoId => Video.VideoId;

    public string Title => Video.Title;

    public string ChannelTitle =>
        Video.ChannelTitle.Length == 0 ? "YouTube" : Video.ChannelTitle;

    public Uri? ThumbnailUrl => Video.ThumbnailUrl;

    public bool HasThumbnail => ThumbnailUrl is not null;

    public string Initial =>
        Title.Length > 0 ? Title[..1].ToUpperInvariant() : "?";

    public void SetPlaybackState(bool current) => IsCurrent = current;
}
