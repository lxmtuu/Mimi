using System.Text.Json.Serialization;
using Mimi.Localization;
using Mimi.Theming;
using Mimi.YouTube;

namespace Mimi.Models;

/// <summary>
/// User preferences and the last paused playback session persisted to
/// settings.json. Version 1 contained only the volume; version 2 adds the
/// visualizer mode, startup-restore preferences, library-view state, window
/// placement, watched library folders, and the paused session. Version 3
/// adds mute/shuffle/repeat preferences and the paused playback queue.
/// Version 4 adds the interface-language preference. Version 5 adds the
/// interface-theme preference. Version 6 adds per-track lyrics display
/// offsets. Version 7 adds the Internet Radio enable/disable preference.
/// Version 8 adds the online catalog (in-app search and streaming)
/// enable/disable preference. Version 9 adds the YouTube search
/// enable/disable preference. Version 10 adds the saved YouTube list.
/// Version 11 adds the optional custom background tint applied on top of
/// whichever theme is selected.
/// Unknown string values are tolerated here and interpreted defensively by
/// the app layer.
/// </summary>
public sealed class AppSettings
{
    public const int CurrentVersion = 11;

    public const int MinimumLyricsOffsetMilliseconds = -30000;

    public const int MaximumLyricsOffsetMilliseconds = 30000;

    public int Version { get; set; } = CurrentVersion;

    public double Volume { get; set; } = 0.7;

    public bool IsMuted { get; set; }

    public bool ShuffleEnabled { get; set; }

    public string RepeatMode { get; set; } = "Off";

    public string VisualizerMode { get; set; } = "NeonSpectrum";

    public string UiLanguage { get; set; } = UiLanguages.Default;

    public string UiTheme { get; set; } = BuiltInThemes.DefaultId;

    /// <summary>
    /// When set, the nine background layers of the selected theme are tinted
    /// from <see cref="CustomBackground"/>. Off by default: the shipped palette
    /// is the one that was reviewed.
    /// </summary>
    public bool CustomBackgroundEnabled { get; set; }

    /// <summary>
    /// The user's chosen background colour as <c>#RRGGBB</c>. Only its hue and
    /// chroma are used; the theme keeps its own layer luminance, so this can
    /// never break the contrast the theme was checked for.
    /// </summary>
    public string CustomBackground { get; set; } = string.Empty;

    public bool RestorePlaybackSession { get; set; } = true;

    public bool RestoreLibraryView { get; set; } = true;

    public bool RestoreWindowPlacement { get; set; } = true;

    public bool AutoUpgradeLegacyCoverArt { get; set; } = true;

    public bool WatchLibraryFolders { get; set; } = true;

    public bool InternetRadioEnabled { get; set; } = true;

    /// <summary>
    /// Controls whether the in-app online catalog search is available. The
    /// catalog is streaming-only: Mimi never downloads, proxies, or caches
    /// provider audio, and online items never join the local library.
    /// </summary>
    public bool OnlineCatalogEnabled { get; set; } = true;

    /// <summary>
    /// Controls whether in-app YouTube search is available. Search uses the
    /// user's own Data API key; playback happens only in YouTube's embedded
    /// player, which stays visible, and Mimi never downloads the media.
    /// </summary>
    public bool YouTubeSearchEnabled { get; set; } = true;

    /// <summary>
    /// YouTube items the user kept. Only ids and display metadata are stored;
    /// nothing here is media, and none of it joins the local library.
    /// </summary>
    public List<SavedYouTubeVideo> YouTubeFavorites { get; set; } = [];

    /// <summary>Upper bound so a mis-edited file cannot grow without limit.</summary>
    public const int MaximumYouTubeFavorites = 500;

    public List<string> WatchedFolders { get; set; } = [];

    /// <summary>
    /// Per-track manual lyrics delay in milliseconds, keyed by audio file
    /// path. Positive values display synchronized lines later; zero entries
    /// are removed during normalization.
    /// </summary>
    public Dictionary<string, int> LyricsOffsetsByPath { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public LibraryViewSettings LibraryView { get; set; } = new();

    public WindowPlacementSettings WindowPlacement { get; set; } = new();

    public PlaybackSessionSettings? PlaybackSession { get; set; }

    /// <summary>
    /// Repairs values that are absent, out of range, or unsafe to consume.
    /// settings.json is user-editable, so loading must never trust it blindly.
    /// </summary>
    private static string? CleanNullableUrl(string? value)
    {
        var cleaned = Clean(value, string.Empty, 512);
        return cleaned.Length == 0 ? null : cleaned;
    }

    public void Normalize()
    {
        Version = CurrentVersion;
        Volume = double.IsFinite(Volume)
            ? Math.Clamp(Volume, 0, 1)
            : 0.7;
        VisualizerMode = Clean(VisualizerMode, "NeonSpectrum", 64);
        UiLanguage = UiLanguages.Normalize(UiLanguage);

        // Friendly spellings of built-in ids canonicalize immediately. Any
        // other well-formed slug is preserved because it may name a
        // community theme; the app layer falls back to the default theme
        // when no loaded theme actually has that id.
        var themeId = UiTheme?.Trim() ?? string.Empty;
        if (BuiltInThemes.TryGet(themeId, out var builtInTheme))
            UiTheme = builtInTheme.Id;
        else if (!ThemeDefinition.IsValidId(themeId))
            UiTheme = BuiltInThemes.DefaultId;
        else
            UiTheme = themeId;
        // An unreadable tint is simply off; the key is never trusted as colour.
        var tint = Clean(CustomBackground, string.Empty, 9);
        CustomBackground = ThemeSurfaceCustomizer.TryParse(tint, out var parsedTint)
            ? ThemeSurfaceCustomizer.ToDisplayString(parsedTint)
            : string.Empty;
        if (CustomBackground.Length == 0)
            CustomBackgroundEnabled = false;

        RepeatMode = Clean(RepeatMode, "Off", 32);
        WatchedFolders = (WatchedFolders ?? [])
            .Select(CleanNullablePath)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(128)
            .ToList();

        var normalizedLyricsOffsets =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (LyricsOffsetsByPath is not null)
        {
            foreach (var pair in LyricsOffsetsByPath)
            {
                if (normalizedLyricsOffsets.Count >= 10000) break;

                var path = CleanNullablePath(pair.Key);
                var offset = Math.Clamp(
                    pair.Value,
                    MinimumLyricsOffsetMilliseconds,
                    MaximumLyricsOffsetMilliseconds);
                if (path is not null && offset != 0)
                    normalizedLyricsOffsets[path] = offset;
            }
        }

        LyricsOffsetsByPath = normalizedLyricsOffsets;

        // Saved YouTube items: keep only what can be played again, drop
        // duplicates, and cap the list so an edited file cannot grow forever.
        var normalizedFavorites = new List<SavedYouTubeVideo>();
        var seenVideoIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var favorite in YouTubeFavorites ?? [])
        {
            if (favorite is null) continue;

            var id = Clean(favorite.VideoId, string.Empty, 32);
            if (id.Length != 11) continue;
            if (!seenVideoIds.Add(id)) continue;

            normalizedFavorites.Add(new SavedYouTubeVideo(
                id,
                Clean(favorite.Title, "YouTube", 200),
                Clean(favorite.ChannelTitle, "YouTube", 120),
                CleanNullableUrl(favorite.ThumbnailUrl)));

            if (normalizedFavorites.Count >= MaximumYouTubeFavorites) break;
        }

        YouTubeFavorites = normalizedFavorites;

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

    public List<string> QueueTrackPaths { get; set; } = [];

    public List<string> HistoryTrackPaths { get; set; } = [];

    public List<string> UpcomingTrackPaths { get; set; } = [];

    internal void Normalize()
    {
        CurrentTrackPath = AppSettings.CleanNullablePath(CurrentTrackPath);
        PositionSeconds = double.IsFinite(PositionSeconds)
            ? Math.Clamp(PositionSeconds, 0, MaximumPositionSeconds)
            : 0;
        QueueTrackPaths = CleanPathList(QueueTrackPaths);
        HistoryTrackPaths = CleanPathList(HistoryTrackPaths);
        UpcomingTrackPaths = CleanPathList(UpcomingTrackPaths);
    }

    private static List<string> CleanPathList(List<string>? paths) =>
        (paths ?? [])
            .Select(AppSettings.CleanNullablePath)
            .OfType<string>()
            .Take(10000)
            .ToList();
}
