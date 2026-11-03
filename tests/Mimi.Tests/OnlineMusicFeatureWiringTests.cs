namespace Mimi.Tests;

/// <summary>
/// Source-level guard for the opt-in online music panel. Mimi embeds the
/// official YouTube Music web player in a visible window; it must never grow
/// a stream extractor, download path, ad blocker, or player modification, and
/// the Settings kill switch has to keep closing the panel.
/// </summary>
public sealed class OnlineMusicFeatureWiringTests
{
    private static readonly string[] ForbiddenExtractionTokens =
    [
        "yt-dlp",
        "youtube-dl",
        "YoutubeExplode",
        "googlevideo",
        "savefrom",
        "SaveFrom"
    ];

    [Fact]
    public void OnlineMusicPanelHostsTheOfficialSiteWithoutExtractingAudio()
    {
        var root = FindRepositoryRoot();

        var project = Read(root, "src", "Mimi.App", "Mimi.App.csproj");
        Assert.Contains(
            "Microsoft.Web.WebView2",
            project,
            StringComparison.Ordinal);

        var window = Read(root, "src", "Mimi.App", "OnlineMusicWindow.xaml.cs");
        Assert.Contains(
            "https://music.youtube.com/",
            window,
            StringComparison.Ordinal);
        Assert.Contains(
            "CoreWebView2Environment.CreateAsync",
            window,
            StringComparison.Ordinal);
        Assert.Contains("EnsureCoreWebView2Async", window, StringComparison.Ordinal);

        // A download request is declined, not stored somewhere invisible.
        Assert.Contains("Core_DownloadStarting", window, StringComparison.Ordinal);
        Assert.Contains("e.Cancel = true;", window, StringComparison.Ordinal);

        var xaml = Read(root, "src", "Mimi.App", "OnlineMusicWindow.xaml");
        Assert.Contains(
            "Microsoft.Web.WebView2.Wpf",
            xaml,
            StringComparison.Ordinal);

        foreach (var file in Directory.EnumerateFiles(
                     Path.Combine(root, "src"),
                     "*.cs",
                     SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            foreach (var token in ForbiddenExtractionTokens)
            {
                Assert.DoesNotContain(
                    token,
                    text,
                    StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void OnlineMusicKillSwitchClosesThePanelAndHidesNavigation()
    {
        var root = FindRepositoryRoot();

        var settingsModel = Read(root, "src", "Mimi.Core", "Models", "AppSettings.cs");
        Assert.Contains(
            "public bool OnlineMusicEnabled { get; set; } = true;",
            settingsModel,
            StringComparison.Ordinal);

        var settingsViewModel = Read(
            root,
            "src",
            "Mimi.App",
            "ViewModels",
            "MainWindowViewModel.Settings.cs");
        Assert.Contains(
            "private bool onlineMusicEnabled = true;",
            settingsViewModel,
            StringComparison.Ordinal);
        Assert.Contains(
            "settings.OnlineMusicEnabled = OnlineMusicEnabled;",
            settingsViewModel,
            StringComparison.Ordinal);
        Assert.Contains(
            "OnlineMusicEnabled = settings.OnlineMusicEnabled;",
            settingsViewModel,
            StringComparison.Ordinal);
        Assert.Contains(
            "partial void OnOnlineMusicEnabledChanged(bool value)",
            settingsViewModel,
            StringComparison.Ordinal);
        Assert.Contains("DisableOnlineMusic();", settingsViewModel, StringComparison.Ordinal);

        var onlineViewModel = Read(
            root,
            "src",
            "Mimi.App",
            "ViewModels",
            "MainWindowViewModel.OnlineMusic.cs");
        Assert.Contains(
            "public bool IsOnlineMusicAvailable => OnlineMusicEnabled;",
            onlineViewModel,
            StringComparison.Ordinal);
        Assert.Contains(
            "internal void DisableOnlineMusic() => IsOnlineMusicOpen = false;",
            onlineViewModel,
            StringComparison.Ordinal);
        // The panel owns audio while it is open.
        Assert.Contains("DisableInternetRadio();", onlineViewModel, StringComparison.Ordinal);
        Assert.Contains("audioPlayer.Pause();", onlineViewModel, StringComparison.Ordinal);

        var mainWindow = Read(root, "src", "Mimi.App", "MainWindow.xaml");
        Assert.Contains("ToggleOnlineMusicCommand", mainWindow, StringComparison.Ordinal);
        Assert.Contains("NavOnlineMusicButtonStyle", mainWindow, StringComparison.Ordinal);
        Assert.Contains("IsOnlineMusicAvailable", mainWindow, StringComparison.Ordinal);
        Assert.Contains(
            "Binding OnlineMusicEnabled",
            mainWindow,
            StringComparison.Ordinal);
        Assert.Contains(
            "Settings_OnlineMusicDescription",
            mainWindow,
            StringComparison.Ordinal);
        Assert.Contains("Nav_OnlineMusic", mainWindow, StringComparison.Ordinal);

        var windowHost = Read(root, "src", "Mimi.App", "MainWindow.xaml.cs");
        Assert.Contains("new OnlineMusicWindow(", windowHost, StringComparison.Ordinal);
        Assert.Contains(
            "nameof(MainWindowViewModel.IsOnlineMusicOpen)",
            windowHost,
            StringComparison.Ordinal);
    }

    [Fact]
    public void OnlineMusicStringsExistInBothLanguages()
    {
        var root = FindRepositoryRoot();

        var neutral = Read(root, "src", "Mimi.App", "Localization", "UiStrings.resx");
        var vietnamese = Read(
            root,
            "src",
            "Mimi.App",
            "Localization",
            "UiStrings.vi.resx");

        foreach (var key in new[]
                 {
                     "Nav_OnlineMusic",
                     "Online_Loading",
                     "Online_LoadFailed",
                     "Online_RuntimeMissing",
                     "Online_RuntimeMissingDetail",
                     "Online_InstallRuntime",
                     "Online_OpenInBrowser",
                     "Online_LegalNotice",
                     "Online_ProviderAttribution",
                     "Online_Title",
                     "Online_WindowTitle",
                     "Settings_EnableOnlineMusic",
                     "Settings_OnlineMusicDescription",
                     "Settings_OnlineMusicSection"
                 })
        {
            Assert.Contains($"name=\"{key}\"", neutral, StringComparison.Ordinal);
            Assert.Contains($"name=\"{key}\"", vietnamese, StringComparison.Ordinal);
        }
    }

    private static string Read(string root, params string[] pathParts) =>
        File.ReadAllText(Path.Combine([root, .. pathParts]));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Mimi.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new Xunit.Sdk.XunitException(
            "Could not locate the repository root (Mimi.sln) above " +
            AppContext.BaseDirectory);
    }
}
