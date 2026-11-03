using CommunityToolkit.Mvvm.ComponentModel;
using Mimi.Online;

namespace Mimi.ViewModels;

/// <summary>
/// Presentation wrapper for one search result from a permitted online catalog.
/// It deliberately is not a <see cref="TrackItemViewModel"/>: online items have
/// no local file, no library identity, and no persistence.
/// </summary>
public sealed partial class OnlineTrackItemViewModel : ObservableObject
{
    [ObservableProperty]
    private bool isCurrent;

    [ObservableProperty]
    private bool isPlaying;

    [ObservableProperty]
    private bool isConnecting;

    public OnlineTrackItemViewModel(OnlineTrack track)
    {
        Track = track;
    }

    public OnlineTrack Track { get; }

    public string Id => Track.Id;

    public string Title => Track.Title;

    public string Artist => Track.Artist;

    public string DurationLabel => Track.DurationLabel;

    public Uri StreamUrl => Track.StreamUrl;

    public Uri Permalink => Track.Permalink;

    public bool HasArtwork => Track.ArtworkUrl is not null;

    public Uri? ArtworkUrl => Track.ArtworkUrl;

    public string LicenseLabel => Track.License.Trim();

    public bool HasLicense => LicenseLabel.Length > 0;

    /// <summary>
    /// Single-letter badge used when the artwork cannot be loaded, so a result
    /// never renders as an empty row.
    /// </summary>
    public string Initial =>
        Title.Length > 0 ? Title[..1].ToUpperInvariant() : "?";

    public string MetadataLabel =>
        $"{Artist} · {DurationLabel}";

    public void SetPlaybackState(bool current, bool playing, bool connecting)
    {
        IsCurrent = current;
        IsPlaying = playing;
        IsConnecting = connecting;
    }
}
