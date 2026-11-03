namespace Mimi.Tests;

/// <summary>
/// Source-level guard for the YouTube slice. Mimi searches the official Data
/// API for metadata and plays the video in YouTube's own visible player; there
/// must never be a stream resolver, downloader, or hidden background player,
/// and closing the pane has to stop the sound.
/// </summary>
public sealed class YouTubeFeatureWiringTests
{
    private static readonly string[] ForbiddenTokens =
    [
        "yt-dlp",
        "youtube-dl",
        "ytmusicapi",
        "ytmusic-api",
        "YoutubeExplode",
        "googlevideo",
        "savefrom"
    ];

    [Fact]
    public void YouTubeUsesTheOfficialSearchApiAndTheVisibleEmbeddedPlayer()
    {
        var root = FindRepositoryRoot();

        var app = Read(root, "src", "Mimi.App", "App.xaml.cs");
        Assert.Contains(
            "services.AddSingleton<IYouTubeSearchService, YouTubeSearchService>();",
            app,
            StringComparison.Ordinal);

        var service = Read(root, "src", "Mimi.Library", "YouTubeSearchService.cs");
        Assert.Contains("https://www.googleapis.com/youtube/v3/search", service, StringComparison.Ordinal);
        Assert.Contains("MIMI_YOUTUBE_API_KEY", service, StringComparison.Ordinal);
        Assert.Contains("youtube-api-key.txt", service, StringComparison.Ordinal);
        // Metadata only: no stream URL, no download, no key in source.
        Assert.DoesNotContain("googlevideo", service, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Download", service, StringComparison.Ordinal);
        Assert.DoesNotContain("AIza", service, StringComparison.Ordinal);

        var videoModel = Read(root, "src", "Mimi.Core", "YouTube", "YouTubeVideo.cs");
        Assert.Contains("youtube.com/embed/", videoModel, StringComparison.Ordinal);
        Assert.Contains("youtube.com/watch?v=", videoModel, StringComparison.Ordinal);

        var xaml = Read(root, "src", "Mimi.App", "MainWindow.xaml");
        Assert.Contains("Microsoft.Web.WebView2.Wpf", xaml, StringComparison.Ordinal);
        Assert.Contains("YouTubeBrowser", xaml, StringComparison.Ordinal);
        Assert.Contains("IsYouTubePlaying", xaml, StringComparison.Ordinal);
        Assert.Contains("IsVisualizerSurfaceVisible", xaml, StringComparison.Ordinal);
        Assert.Contains("PlayYouTubeVideoCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("StopYouTubeVideoCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("SearchYouTubeCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("Nav_YouTube", xaml, StringComparison.Ordinal);

        // The visualizer and the video share one frame.
        Assert.Contains(
            "behaviors:ItemClickCommandBehavior.Command=\"{Binding PlayYouTubeVideoCommand}\"",
            xaml,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ClosingThePlayerStopsPlaybackAndThePaneReplacesTheVisualizer()
    {
        var root = FindRepositoryRoot();

        var codeBehind = Read(root, "src", "Mimi.App", "MainWindow.xaml.cs");
        Assert.Contains("SyncYouTubePlayerAsync", codeBehind, StringComparison.Ordinal);
        // Navigating away is what silences the embedded player.
        Assert.Contains("Navigate(\"about:blank\")", codeBehind, StringComparison.Ordinal);
        Assert.Contains("nameof(MainWindowViewModel.IsYouTubePlaying)", codeBehind, StringComparison.Ordinal);

        var viewModel = Read(
            root,
            "src",
            "Mimi.App",
            "ViewModels",
            "MainWindowViewModel.YouTube.cs");
        Assert.Contains(
            "public bool IsVisualizerSurfaceVisible => !IsYouTubePlaying;",
            viewModel,
            StringComparison.Ordinal);
        Assert.Contains("internal void DisableYouTube()", viewModel, StringComparison.Ordinal);
        Assert.Contains("private void ReleaseOtherPlaybackSurfaces()", viewModel, StringComparison.Ordinal);
        // Nothing from YouTube becomes a local library item.
        Assert.DoesNotContain("new TrackItemViewModel", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("playbackQueue.Enqueue", viewModel, StringComparison.Ordinal);

        var main = Read(root, "src", "Mimi.App", "ViewModels", "MainWindowViewModel.cs");
        Assert.Contains("if (IsYouTubePlaying)", main, StringComparison.Ordinal);
        Assert.Contains("StopYouTubeVideo();", main, StringComparison.Ordinal);

        var settings = Read(
            root,
            "src",
            "Mimi.App",
            "ViewModels",
            "MainWindowViewModel.Settings.cs");
        Assert.Contains("private bool youTubeSearchEnabled = true;", settings, StringComparison.Ordinal);
        Assert.Contains(
            "partial void OnYouTubeSearchEnabledChanged(bool value)",
            settings,
            StringComparison.Ordinal);
        Assert.Contains("DisableYouTube();", settings, StringComparison.Ordinal);
        Assert.Contains(
            "settings.YouTubeSearchEnabled = YouTubeSearchEnabled;",
            settings,
            StringComparison.Ordinal);

        // The key file is read lazily, so adding it while Mimi runs must not
        // require a restart just to reveal the nav entry.
        Assert.Contains(
            "public void RefreshYouTubeAvailability()",
            viewModel,
            StringComparison.Ordinal);
        Assert.Contains(
            "Activated += (_, _) => viewModel.RefreshYouTubeAvailability();",
            codeBehind,
            StringComparison.Ordinal);

        var settingsModel = Read(root, "src", "Mimi.Core", "Models", "AppSettings.cs");
        Assert.Contains(
            "public bool YouTubeSearchEnabled { get; set; } = true;",
            settingsModel,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheKeyIsConfiguredInSettingsInsteadOfByHuntingForTheDataFolder()
    {
        var root = FindRepositoryRoot();

        var xaml = Read(root, "src", "Mimi.App", "MainWindow.xaml");
        Assert.Contains("YouTubeApiKeyBox", xaml, StringComparison.Ordinal);
        Assert.Contains("SaveYouTubeApiKey_Click", xaml, StringComparison.Ordinal);
        Assert.Contains("ClearYouTubeApiKeyCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("YouTubeKeyFilePath", xaml, StringComparison.Ordinal);

        var viewModel = Read(
            root,
            "src",
            "Mimi.App",
            "ViewModels",
            "MainWindowViewModel.YouTube.cs");
        Assert.Contains(
            "private void SaveYouTubeApiKey(string? apiKey)",
            viewModel,
            StringComparison.Ordinal);
        Assert.Contains("youtubeSearchService.TrySaveApiKey", viewModel, StringComparison.Ordinal);
        Assert.Contains("youtubeSearchService.TryClearApiKey", viewModel, StringComparison.Ordinal);
        // The pasted key is written to disk and never kept as view model state;
        // only the feedback message lives there.
        Assert.DoesNotContain("private string youTubeApiKey =", viewModel, StringComparison.Ordinal);
        Assert.Contains(
            "private string youTubeApiKeyMessage = string.Empty;",
            viewModel,
            StringComparison.Ordinal);

        var codeBehind = Read(root, "src", "Mimi.App", "MainWindow.xaml.cs");
        Assert.Contains("YouTubeApiKeyBox.Clear();", codeBehind, StringComparison.Ordinal);

        var service = Read(root, "src", "Mimi.Library", "YouTubeSearchService.cs");
        Assert.Contains(
            "public bool TrySaveApiKey(string apiKey)",
            service,
            StringComparison.Ordinal);
    }

    [Fact]
    public void NoExtractionLibraryExistsInTheSource()
    {
        var root = FindRepositoryRoot();

        foreach (var file in Directory.EnumerateFiles(
                     Path.Combine(root, "src"),
                     "*.cs",
                     SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            foreach (var token in ForbiddenTokens)
            {
                Assert.DoesNotContain(token, text, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void YouTubeStringsExistInBothLanguages()
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
                     "Nav_YouTube",
                     "YouTube_Title",
                     "YouTube_Description",
                     "YouTube_SearchLabel",
                     "YouTube_Search",
                     "YouTube_SearchAutomation",
                     "YouTube_ResultListAutomation",
                     "YouTube_Loading",
                     "YouTube_ResultsAvailable",
                     "YouTube_NoResults",
                     "YouTube_LoadFailed",
                     "YouTube_QuotaExceeded",
                     "YouTube_NotConfigured",
                     "YouTube_Unavailable",
                     "YouTube_PlayAutomation",
                     "YouTube_OpenSource",
                     "YouTube_OpenFailed",
                     "YouTube_Stop",
                     "YouTube_NowPlaying",
                     "YouTube_Source",
                     "YouTube_ExternalTimeline",
                     "YouTube_ProviderAttribution",
                     "Settings_YouTubeSection",
                     "Settings_EnableYouTube",
                     "Settings_YouTubeDescription"
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
