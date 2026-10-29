using System.Text.Json.Serialization;

namespace Mimi.Models;

/// <summary>
/// User preferences and the last paused playback session persisted to
/// settings.json. Version 1 contained only the volume; version 2 adds the
/// visualizer mode, startup-restore preferences, library-view state, window
/// placement, and the paused session. Unknown string values are tolerated
/// here and interpreted defensively by the app layer.
/// </summary>
public sealed class AppSettings
{
    public const int CurrentVersion = 2;

    public int Version { get; set; } = CurrentVersion;

    public double Volume { get; set; } = 0.7;

    public string VisualizerMode { get; set; } = "NeonSpectrum";

    public bool RestorePlaybackSession { get; set; } = true;

    public bool RestoreLibraryView { get; set; } = true;

    public bool RestoreWindowPlacement { get; set; } = true;

    public bool AutoUpgradeLegacyCoverArt { get; set; } = true;

    public LibraryViewSettings LibraryView { get; set; } = new();

    public WindowPlacementSettings WindowPlacement { get; set; } = new();

    public PlaybackSessionSettings? PlaybackSession { get; set; }

    /// <summary>
    /// Repairs values that are absent, out of range, or unsafe to consume.
    /// settings.json is user-editable, so loading must never trust it blindly.
    /// </summary>
    public void Normalize()
    {
        Version = CurrentVersion;
        Volume = double.IsFinite(Volume)
            ? Math.Clamp(Volume, 0, 1)
            : 0.7;
        VisualizerMode = Clean(VisualizerMode, "NeonSpectrum", 64);

        LibraryView ??= new LibraryViewSettings();
        LibraryView.Normalize();

        WindowPlacement ??= new WindowPlacementSettings();
        WindowPlacement.Normalize();

        if (PlaybackSession is not null)
        {
            PlaybackSession.Normalize();
            if (PlaybackSession.CurrentTrackPath is null)
                PlaybackSession = null;
        }
    }

    internal static string Clean(
        string? value,
        string fallback,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;

        var cleaned = value.Trim();
        return cleaned.Length <= maximumLength
            ? cleaned
            : cleaned[..maximumLength];
    }

    internal static string? CleanNullablePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var cleaned = value.Trim();
        const int maximumWindowsPathLength = 32767;
        return cleaned.Length <= maximumWindowsPathLength
            ? cleaned
            : cleaned[..maximumWindowsPathLength];
    }
}

/// <summary>
/// The library context needed to rebuild the effective playback queue on
/// the next launch: view, playlist, search, filters, sort, and selection.
/// </summary>
public sealed class LibraryViewSettings
{
    public string ActiveView { get; set; } = "Library";

    public long? SelectedPlaylistId { get; set; }

    public string? SelectedTrackPath { get; set; }

    public string SearchText { get; set; } = string.Empty;

    public string ArtistFilter { get; set; } = "All artists";

    public string AlbumFilter { get; set; } = "All albums";

    public string GenreFilter { get; set; } = "All genres";

    public string FormatFilter { get; set; } = "All formats";

    public string PlaybackStateFilter { get; set; } = "All states";

    public string SortField { get; set; } = "None";

    public string SortDirection { get; set; } = "Ascending";

    internal void Normalize()
    {
        ActiveView = AppSettings.Clean(ActiveView, "Library", 64);
        SelectedPlaylistId = SelectedPlaylistId > 0
            ? SelectedPlaylistId
            : null;
        SelectedTrackPath = AppSettings.CleanNullablePath(SelectedTrackPath);
        SearchText = AppSettings.Clean(SearchText, string.Empty, 512);
        ArtistFilter = AppSettings.Clean(ArtistFilter, "All artists", 512);
        AlbumFilter = AppSettings.Clean(AlbumFilter, "All albums", 512);
        GenreFilter = AppSettings.Clean(GenreFilter, "All genres", 512);
        FormatFilter = AppSettings.Clean(FormatFilter, "All formats", 128);
        PlaybackStateFilter = AppSettings.Clean(
            PlaybackStateFilter,
            "All states",
            128);
        SortField = AppSettings.Clean(SortField, "None", 64);
        SortDirection = AppSettings.Clean(
            SortDirection,
            "Ascending",
            64);
    }
}

/// <summary>Main-window bounds in device-independent WPF units.</summary>
public sealed class WindowPlacementSettings
{
    public double? Left { get; set; }

    public double? Top { get; set; }

    public double? Width { get; set; }

    public double? Height { get; set; }

    public bool IsMaximized { get; set; }

    [JsonIgnore]
    public bool HasBounds =>
        Left.HasValue && Top.HasValue && Width.HasValue && Height.HasValue;

    internal void Normalize()
    {
        Left = FiniteOrNull(Left);
        Top = FiniteOrNull(Top);
        Width = PositiveOrNull(Width);
        Height = PositiveOrNull(Height);
    }

    private static double? FiniteOrNull(double? value) =>
        value.HasValue && double.IsFinite(value.Value) ? value : null;

    private static double? PositiveOrNull(double? value) =>
        value.HasValue && double.IsFinite(value.Value) && value.Value > 0
            ? value
            : null;
}

/// <summary>
/// The last loaded track and its position. Restoring this session must
/// always leave playback paused; Mimi never starts audio unexpectedly.
/// </summary>
public sealed class PlaybackSessionSettings
{
    private const double MaximumPositionSeconds = 365d * 24 * 60 * 60;

    public string? CurrentTrackPath { get; set; }

    public double PositionSeconds { get; set; }

    internal void Normalize()
    {
        CurrentTrackPath = AppSettings.CleanNullablePath(CurrentTrackPath);
        PositionSeconds = double.IsFinite(PositionSeconds)
            ? Math.Clamp(PositionSeconds, 0, MaximumPositionSeconds)
            : 0;
    }
}
