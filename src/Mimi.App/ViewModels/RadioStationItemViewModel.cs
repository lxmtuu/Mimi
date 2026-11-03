using CommunityToolkit.Mvvm.ComponentModel;
using Mimi.Models;

namespace Mimi.ViewModels;

/// <summary>
/// Presentation wrapper for one live radio station. It intentionally stays
/// separate from <see cref="TrackItemViewModel"/> so a network stream never
/// leaks into the local library, playback queue, history, or saved session.
/// </summary>
public sealed partial class RadioStationItemViewModel : ObservableObject
{
    public RadioStationItemViewModel(RadioStation station)
    {
        Station = station ?? throw new ArgumentNullException(nameof(station));
    }

    public RadioStation Station { get; }

    public Guid StationUuid => Station.StationUuid;

    public string Name => Station.Name;

    public Uri StreamUrl => Station.StreamUrl;

    public Uri? HomepageUrl => Station.HomepageUrl;

    public bool HasHomepage => HomepageUrl is not null;

    public string Initial
    {
        get
        {
            foreach (var character in Name)
            {
                if (char.IsLetterOrDigit(character))
                    return char.ToUpperInvariant(character).ToString();
            }

            return "♪";
        }
    }

    public string MetadataLabel
    {
        get
        {
            var parts = new List<string>();
            if (Station.Country.Length > 0)
                parts.Add(Station.Country);
            if (Station.Language.Length > 0)
                parts.Add(Station.Language);

            if (Station.Codec.Length > 0 && Station.BitrateKbps > 0)
                parts.Add($"{Station.Codec} {Station.BitrateKbps} kbps");
            else if (Station.Codec.Length > 0)
                parts.Add(Station.Codec);
            else if (Station.BitrateKbps > 0)
                parts.Add($"{Station.BitrateKbps} kbps");

            return string.Join(" · ", parts);
        }
    }

    public string TagsLabel => string.Join(" · ", Station.Tags.Take(4));

    public bool HasTags => Station.Tags.Count > 0;

    [ObservableProperty]
    private bool isCurrent;

    [ObservableProperty]
    private bool isPlaying;

    [ObservableProperty]
    private bool isConnecting;

    public void SetPlaybackState(bool current, bool playing, bool connecting)
    {
        IsCurrent = current;
        IsPlaying = current && playing;
        IsConnecting = connecting;
    }
}
