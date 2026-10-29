using CommunityToolkit.Mvvm.ComponentModel;
using Mimi.Models;

namespace Mimi.ViewModels;

/// <summary>Presentation state for one playlist in the sidebar.</summary>
public sealed partial class PlaylistItemViewModel : ObservableObject
{
    public PlaylistItemViewModel(Playlist playlist)
    {
        Playlist = playlist;
        name = playlist.Name;
        trackCount = Math.Max(0, playlist.TrackCount);
    }

    public Playlist Playlist { get; }

    public long Id => Playlist.Id;

    [ObservableProperty]
    private string name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CountLabel))]
    private int trackCount;

    public string CountLabel => $"{TrackCount} song(s)";

    public void Update(Playlist updated)
    {
        if (updated.Id != Playlist.Id) return;

        Playlist.Name = updated.Name;
        Playlist.CreatedAtUtc = updated.CreatedAtUtc;
        Playlist.UpdatedAtUtc = updated.UpdatedAtUtc;
        Playlist.TrackCount = Math.Max(0, updated.TrackCount);
        Name = Playlist.Name;
        TrackCount = Playlist.TrackCount;
    }

    public void Rename(string newName)
    {
        Playlist.Name = newName.Trim();
        Playlist.UpdatedAtUtc = DateTimeOffset.UtcNow;
        Name = Playlist.Name;
    }

    public void UpdateTrackCount(int count)
    {
        Playlist.TrackCount = Math.Max(0, count);
        Playlist.UpdatedAtUtc = DateTimeOffset.UtcNow;
        TrackCount = Playlist.TrackCount;
    }
}
