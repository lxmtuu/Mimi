using Mimi.Services;
using Mimi.Theming;

namespace Mimi.Tests;

public sealed class ThemeServiceCommunityThemeTests
{
    [Fact]
    public void LoadsCommunityThemeFromUserThemesFolder()
    {
        RunOnSta(() =>
        {
            using var folders = new TestFolders();
            folders.WriteTheme(
                "alpha-one.json",
                CommunityThemeLoaderTests.BuildThemeJson(
                    "alpha-one",
                    "Alpha One",
                    colors => colors[ThemeTokens.Accent] = "#123456"));

            var service = new ThemeService(folders.UserData);

            Assert.Contains(
                service.AvailableThemes,
                theme => theme.Id == "alpha-one");
            Assert.Equal("alpha-one", service.Apply("alpha-one"));
            Assert.Equal("alpha-one", service.CurrentThemeId);
            Assert.Equal(
                ThemeColor.Parse("#123456"),
                service.CurrentTheme.GetColor(ThemeTokens.Accent));
        });
    }

    [Fact]
    public void CreatesThemesFolderAndSkipsInvalidFiles()
    {
        RunOnSta(() =>
        {
            using var folders = new TestFolders();
            folders.WriteTheme("broken.json", "{ not json");

            var service = new ThemeService(folders.UserData);

            Assert.True(Directory.Exists(service.ThemesDirectory));
            Assert.Equal(4, service.AvailableThemes.Count);
            Assert.Equal(
                BuiltInThemes.DefaultId,
                service.Apply("broken-one"));
        });
    }

    [Fact]
    public void Reload_PicksUpNewThemeAndFallsBackWhenCurrentIsRemoved()
    {
        RunOnSta(() =>
        {
            using var folders = new TestFolders();
            var alphaPath = folders.WriteTheme(
                "alpha-one.json",
                CommunityThemeLoaderTests.BuildThemeJson(
                    "alpha-one",
                    "Alpha One"));
            var service = new ThemeService(folders.UserData);
            service.Apply("alpha-one");
            Assert.Equal("alpha-one", service.CurrentThemeId);

            folders.WriteTheme(
                "beta-two.json",
                CommunityThemeLoaderTests.BuildThemeJson(
                    "beta-two",
                    "Beta Two"));
            service.ReloadCommunityThemes();

            Assert.Contains(
                service.AvailableThemes,
                theme => theme.Id == "beta-two");
            Assert.Equal("alpha-one", service.CurrentThemeId);

            File.Delete(alphaPath);
            service.ReloadCommunityThemes();

            Assert.DoesNotContain(
                service.AvailableThemes,
                theme => theme.Id == "alpha-one");
            Assert.Equal(BuiltInThemes.DefaultId, service.CurrentThemeId);
        });
    }

    [Fact]
    public void WithoutUserData_OnlyBuiltInsAreAvailable()
    {
        RunOnSta(() =>
        {
            var service = new ThemeService();

            Assert.Null(service.ThemesDirectory);
            Assert.Equal(4, service.AvailableThemes.Count);
            Assert.Equal(
                BuiltInThemes.DefaultId,
                service.NormalizeId("alpha-one"));
        });
    }

    private sealed class TestFolders : IDisposable
    {
        private readonly string root = Path.Combine(
            Path.GetTempPath(),
            "MimiThemeServiceTests-" + Guid.NewGuid().ToString("N"));

        public TestFolders()
        {
            DataDirectory = Path.Combine(root, "Mimi");
            var legacyDirectory = Path.Combine(root, "MikuVerseMusic");
            Directory.CreateDirectory(DataDirectory);
            Directory.CreateDirectory(legacyDirectory);
            UserData = new UserDataService(DataDirectory, legacyDirectory);
        }

        public string DataDirectory { get; }

        public UserDataService UserData { get; }

        public string WriteTheme(string fileName, string json)
        {
            var themesDirectory = Path.Combine(DataDirectory, "themes");
            Directory.CreateDirectory(themesDirectory);
            var path = Path.Combine(themesDirectory, fileName);
            File.WriteAllText(path, json);
            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (Exception ex) when (
                ex is IOException or UnauthorizedAccessException)
            {
                // Best-effort cleanup of the per-test temp folder.
            }
        }
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            throw new Xunit.Sdk.XunitException(
                $"STA/WPF theme service test failed: {failure}");
        }
    }
}
