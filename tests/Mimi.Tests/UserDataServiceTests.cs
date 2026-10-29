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
        Assert.Contains(
            "c:\\music\\one.mp3",
            service.LoadFavorites());
        Assert.False(File.Exists(Path.Combine(
            dataDirectory,
            "library.json.tmp")));
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
