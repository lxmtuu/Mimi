using Mimi.Models;
using Mimi.Services;

namespace Mimi.Tests;

public sealed class UserDataServiceTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        "MimiUserDataTests-" + Guid.NewGuid().ToString("N"));
    private readonly string dataDirectory;
    private readonly string legacyDirectory;
    private readonly UserDataService service;

    public UserDataServiceTests()
    {
        dataDirectory = Path.Combine(root, "Mimi");
        legacyDirectory = Path.Combine(root, "MikuVerseMusic");
        Directory.CreateDirectory(dataDirectory);
        Directory.CreateDirectory(legacyDirectory);
        service = new UserDataService(dataDirectory, legacyDirectory);
    }

    [Fact]
    public void MigrationCopiesLegacyFilesWithoutOverwritingCurrentData()
    {
        File.WriteAllText(
            Path.Combine(legacyDirectory, "library.json"),
            "[\"legacy.mp3\"]");
        File.WriteAllText(
            Path.Combine(legacyDirectory, "favorites.json"),
            "[\"legacy.mp3\"]");
        File.WriteAllText(
            Path.Combine(dataDirectory, "library.json"),
            "[\"current.mp3\"]");

        service.MigrateLegacyDataIfNeeded();

        Assert.Equal(
            ["current.mp3"],
            service.LoadLibraryPaths());
        Assert.Contains("legacy.mp3", service.LoadFavorites());
        Assert.True(File.Exists(Path.Combine(
            legacyDirectory,
            "library.json")));
    }

    [Fact]
    public void LibraryAndFavoritesRoundTrip()
    {
        service.SaveLibraryPaths(["C:\\Music\\One.mp3", "C:\\Music\\Two.flac"]);
        service.SaveFavorites(["C:\\Music\\One.mp3"]);

        Assert.Equal(
            ["C:\\Music\\One.mp3", "C:\\Music\\Two.flac"],
            service.LoadLibraryPaths());
        var loadedFavorites = service.LoadFavorites();
        Assert.Contains(
            "c:\\music\\one.mp3",
            loadedFavorites,
            loadedFavorites.Comparer);
        Assert.False(File.Exists(Path.Combine(
            dataDirectory,
            "library.json.tmp")));
    }

    [Fact]
    public void ArchiveImportedLibraryDataRenamesLibraryAndFavoritesAsBackups()
    {
        var libraryFile = Path.Combine(dataDirectory, "library.json");
        var favoritesFile = Path.Combine(dataDirectory, "favorites.json");
        File.WriteAllText(libraryFile, "[\"one.mp3\"]");
        File.WriteAllText(favoritesFile, "[\"one.mp3\"]");

        service.ArchiveImportedLibraryData();

        Assert.False(File.Exists(libraryFile));
        Assert.False(File.Exists(favoritesFile));
        Assert.Equal(
            "[\"one.mp3\"]",
            File.ReadAllText(libraryFile + ".bak"));
        Assert.Equal(
            "[\"one.mp3\"]",
            File.ReadAllText(favoritesFile + ".bak"));
    }

    [Fact]
    public void SettingsRoundTripAndClampVolume()
    {
        service.SaveSettings(new AppSettings { Volume = 0.25 });
        Assert.Equal(0.25, service.LoadSettings().Volume);

        File.WriteAllText(
            Path.Combine(dataDirectory, "settings.json"),
            "{\"Version\":1,\"Volume\":42}");
        Assert.Equal(1, service.LoadSettings().Volume);
    }

    [Fact]
    public void Version2SettingsRoundTrip()
    {
        var expected = new AppSettings
        {
            Volume = 0.4,
            VisualizerMode = "TwinWave",
            RestorePlaybackSession = false,
            RestoreLibraryView = false,
            RestoreWindowPlacement = false,
            AutoUpgradeLegacyCoverArt = false,
            LibraryView = new LibraryViewSettings
            {
                ActiveView = "Playlist",
                SelectedPlaylistId = 42,
                SelectedTrackPath = @"C:\Music\Selected.flac",
                SearchText = "neon",
                ArtistFilter = "Mimi Artist",
                AlbumFilter = "Neon Album",
                GenreFilter = "Synth",
                FormatFilter = "FLAC",
                PlaybackStateFilter = "Ready",
                SortField = "Title",
                SortDirection = "Descending"
            },
            WindowPlacement = new WindowPlacementSettings
            {
                Left = 120,
                Top = 80,
                Width = 1280,
                Height = 800,
                IsMaximized = true
            },
            PlaybackSession = new PlaybackSessionSettings
            {
                CurrentTrackPath = @"C:\Music\Selected.flac",
                PositionSeconds = 93.5
            }
        };

        service.SaveSettings(expected);
        var loaded = service.LoadSettings();

        Assert.Equal(AppSettings.CurrentVersion, loaded.Version);
        Assert.Equal(0.4, loaded.Volume);
        Assert.Equal("TwinWave", loaded.VisualizerMode);
        Assert.False(loaded.RestorePlaybackSession);
        Assert.False(loaded.RestoreLibraryView);
        Assert.False(loaded.RestoreWindowPlacement);
        Assert.False(loaded.AutoUpgradeLegacyCoverArt);
        Assert.Equal("Playlist", loaded.LibraryView.ActiveView);
        Assert.Equal(42, loaded.LibraryView.SelectedPlaylistId);
        Assert.Equal(
            @"C:\Music\Selected.flac",
            loaded.LibraryView.SelectedTrackPath);
        Assert.Equal("neon", loaded.LibraryView.SearchText);
        Assert.Equal("Mimi Artist", loaded.LibraryView.ArtistFilter);
        Assert.Equal("Descending", loaded.LibraryView.SortDirection);
        Assert.True(loaded.WindowPlacement.HasBounds);
        Assert.Equal(1280, loaded.WindowPlacement.Width);
        Assert.True(loaded.WindowPlacement.IsMaximized);
        Assert.NotNull(loaded.PlaybackSession);
        Assert.Equal(93.5, loaded.PlaybackSession.PositionSeconds);
    }

    [Fact]
    public void Version1SettingsReceiveVersion2Defaults()
    {
        File.WriteAllText(
            Path.Combine(dataDirectory, "settings.json"),
            "{\"Version\":1,\"Volume\":0.25}");

        var loaded = service.LoadSettings();

        Assert.Equal(AppSettings.CurrentVersion, loaded.Version);
        Assert.Equal(0.25, loaded.Volume);
        Assert.Equal("NeonSpectrum", loaded.VisualizerMode);
        Assert.True(loaded.RestorePlaybackSession);
        Assert.True(loaded.RestoreLibraryView);
        Assert.True(loaded.RestoreWindowPlacement);
        Assert.True(loaded.AutoUpgradeLegacyCoverArt);
        Assert.Equal("Library", loaded.LibraryView.ActiveView);
        Assert.False(loaded.WindowPlacement.HasBounds);
        Assert.Null(loaded.PlaybackSession);
    }

    [Fact]
    public void InvalidVersion2SettingsAreNormalized()
    {
        File.WriteAllText(
            Path.Combine(dataDirectory, "settings.json"),
            """
            {
              "Version": 99,
              "Volume": -4,
              "VisualizerMode": " ",
              "LibraryView": {
                "ActiveView": null,
                "SelectedPlaylistId": -9,
                "SelectedTrackPath": " ",
                "SearchText": null,
                "ArtistFilter": null,
                "AlbumFilter": null,
                "GenreFilter": null,
                "FormatFilter": null,
                "PlaybackStateFilter": null,
                "SortField": null,
                "SortDirection": null
              },
              "WindowPlacement": {
                "Left": 10,
                "Top": 20,
                "Width": 0,
                "Height": -1
              },
              "PlaybackSession": {
                "CurrentTrackPath": " ",
                "PositionSeconds": -30
              }
            }
            """);

        var loaded = service.LoadSettings();

        Assert.Equal(AppSettings.CurrentVersion, loaded.Version);
        Assert.Equal(0, loaded.Volume);
        Assert.Equal("NeonSpectrum", loaded.VisualizerMode);
        Assert.Equal("Library", loaded.LibraryView.ActiveView);
        Assert.Null(loaded.LibraryView.SelectedPlaylistId);
        Assert.Null(loaded.LibraryView.SelectedTrackPath);
        Assert.Equal(string.Empty, loaded.LibraryView.SearchText);
        Assert.Equal("All artists", loaded.LibraryView.ArtistFilter);
        Assert.False(loaded.WindowPlacement.HasBounds);
        Assert.Null(loaded.WindowPlacement.Width);
        Assert.Null(loaded.WindowPlacement.Height);
        Assert.Null(loaded.PlaybackSession);
    }

    [Fact]
    public void MalformedSettingsFallBackToDefaults()
    {
        File.WriteAllText(
            Path.Combine(dataDirectory, "settings.json"),
            "not json");

        Assert.Equal(0.7, service.LoadSettings().Volume);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
