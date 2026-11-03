namespace Mimi.Tests;

/// <summary>
/// Source-level guard for the in-app online catalog. Mimi searches and streams
/// a permitted provider inside its own engine; it must never grow a stream
/// extractor, download path, proxy, or persistent cache, and online items must
/// stay out of the local library, queue, history, favorites, and session.
/// </summary>
public sealed class OnlineCatalogFeatureWiringTests
{
    private static readonly string[] ForbiddenExtractionTokens =
    [
        "yt-dlp",
        "youtube-dl",
        "YoutubeExplode",
        "googlevideo",
        "savefrom",
        "SaveFrom",
        "music.youtube.com"
    ];

    [Fact]
    public void OnlineCatalogStreamsThroughTheMimiEngine()
    {
        var root = FindRepositoryRoot();

        var app = Read(root, "src", "Mimi.App", "App.xaml.cs");
        Assert.Contains(
            "services.AddSingleton<IOnlineCatalogService, AudiusCatalogService>();",
            app,
            StringComparison.Ordinal);

        var audioContract = Read(
            root,
            "src",
            "Mimi.Core",
            "Services",
            "IAudioPlayer.cs");
        Assert.Contains("void LoadRemoteAudio(Uri audioUrl);", audioContract, StringComparison.Ordinal);

        var audioService = Read(root, "src", "Mimi.Audio", "AudioService.cs");
        Assert.Contains("public void LoadRemoteAudio(Uri audioUrl)", audioService, StringComparison.Ordinal);
        Assert.Contains("isLiveStream: false", audioService, StringComparison.Ordinal);
        Assert.Contains("online audio host", audioService, StringComparison.Ordinal);

        var catalog = Read(root, "src", "Mimi.Library", "AudiusCatalogService.cs");
        Assert.Contains("/tracks/search", catalog, StringComparison.Ordinal);
        Assert.Contains("app_name", catalog, StringComparison.Ordinal);
        Assert.Contains("is_streamable", catalog, StringComparison.Ordinal);
        Assert.Contains("is_stream_gated", catalog, StringComparison.Ordinal);
        Assert.Contains("allowed_api_keys", catalog, StringComparison.Ordinal);
        Assert.Contains("MaximumResponseBytes", catalog, StringComparison.Ordinal);

        // No credential may ship inside a distributed build.
        foreach (var token in new[] { "Bearer ", "Authorization", "api_secret", "ApiSecret" })
        {
            Assert.DoesNotContain(token, catalog, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void OnlineItemsStayOutOfTheLocalLibraryAndTheKillSwitchWorks()
    {
        var root = FindRepositoryRoot();

        var viewModel = Read(
            root,
            "src",
            "Mimi.App",
            "ViewModels",
            "MainWindowViewModel.Online.cs");
        Assert.Contains("audioPlayer.LoadRemoteAudio(streamUrl)", viewModel, StringComparison.Ordinal);
        Assert.Contains("BeginLocalPlaybackAttempt()", viewModel, StringComparison.Ordinal);
        Assert.Contains("IsCurrentPlaybackVersion(version)", viewModel, StringComparison.Ordinal);
        Assert.Contains("internal void DisableOnlineCatalog()", viewModel, StringComparison.Ordinal);
        Assert.Contains("public bool IsOnlineAvailable =>", viewModel, StringComparison.Ordinal);

        // Online playback never creates a local track or a queue/history entry.
        Assert.DoesNotContain("new TrackItemViewModel", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("playbackQueue.Enqueue", viewModel, StringComparison.Ordinal);

        var settings = Read(
            root,
            "src",
            "Mimi.App",
            "ViewModels",
            "MainWindowViewModel.Settings.cs");
        Assert.Contains("private bool onlineCatalogEnabled = true;", settings, StringComparison.Ordinal);
        Assert.Contains(
            "partial void OnOnlineCatalogEnabledChanged(bool value)",
            settings,
            StringComparison.Ordinal);
        Assert.Contains("DisableOnlineCatalog();", settings, StringComparison.Ordinal);
        Assert.Contains(
            "settings.OnlineCatalogEnabled = OnlineCatalogEnabled;",
            settings,
            StringComparison.Ordinal);

        var settingsModel = Read(root, "src", "Mimi.Core", "Models", "AppSettings.cs");
        Assert.Contains(
            "public bool OnlineCatalogEnabled { get; set; } = true;",
            settingsModel,
            StringComparison.Ordinal);

        var mainWindow = Read(root, "src", "Mimi.App", "MainWindow.xaml");
        Assert.Contains("ShowOnlineCommand", mainWindow, StringComparison.Ordinal);
        Assert.Contains("PlayOnlineTrackCommand", mainWindow, StringComparison.Ordinal);
        Assert.Contains("OnlineSearchText", mainWindow, StringComparison.Ordinal);
        Assert.Contains("IsOnlineContentVisible", mainWindow, StringComparison.Ordinal);
        Assert.Contains("Binding OnlineCatalogEnabled", mainWindow, StringComparison.Ordinal);
        Assert.Contains("Settings_OnlineDescription", mainWindow, StringComparison.Ordinal);
        Assert.Contains(
            "behaviors:ItemClickCommandBehavior.Command=\"{Binding PlayOnlineTrackCommand}\"",
            mainWindow,
            StringComparison.Ordinal);

        var mediaControls = Read(
            root,
            "src",
            "Mimi.App",
            "Services",
            "WindowsMediaControlsService.cs");
        Assert.Contains("CurrentOnlineTrack", mediaControls, StringComparison.Ordinal);
    }

    [Fact]
    public void FinishedOnlineTrackNeverFallsBackToALocalSong()
    {
        var root = FindRepositoryRoot();

        var viewModel = Read(
            root,
            "src",
            "Mimi.App",
            "ViewModels",
            "MainWindowViewModel.Online.cs");
        Assert.Contains("private bool hasOnlineSession;", viewModel, StringComparison.Ordinal);
        Assert.Contains("hasOnlineSession = true;", viewModel, StringComparison.Ordinal);
        Assert.Contains("private void ShowOnlineSessionEnded()", viewModel, StringComparison.Ordinal);
        Assert.Contains("private bool TryToggleOnlinePlayback()", viewModel, StringComparison.Ordinal);
        Assert.Contains("if (!hasOnlineSession) return false;", viewModel, StringComparison.Ordinal);

        var main = Read(root, "src", "Mimi.App", "ViewModels", "MainWindowViewModel.cs");
        Assert.Contains("if (TryToggleOnlinePlayback())", main, StringComparison.Ordinal);
        Assert.Contains("if (endedTrack is null && hasOnlineSession)", main, StringComparison.Ordinal);
        Assert.Contains("ShowOnlineSessionEnded();", main, StringComparison.Ordinal);

        var queue = Read(
            root,
            "src",
            "Mimi.App",
            "ViewModels",
            "MainWindowViewModel.PlaybackQueue.cs");
        Assert.Contains(
            "if (CurrentTrack is null && !hasOnlineSession)",
            queue,
            StringComparison.Ordinal);
    }

    [Fact]
    public void StreamedOnlineTracksReachANaturalEndWithoutAReportedLength()
    {
        var root = FindRepositoryRoot();

        var audioService = Read(root, "src", "Mimi.Audio", "AudioService.cs");
        Assert.Contains("loadedIsFiniteRemote", audioService, StringComparison.Ordinal);
        Assert.Contains("reachedReportedEnd", audioService, StringComparison.Ordinal);
        Assert.Contains("reader.CurrentTime >=", audioService, StringComparison.Ordinal);
        Assert.Contains(
            "TimeSpan.FromMilliseconds(500)",
            audioService,
            StringComparison.Ordinal);

        var classifier = Read(
            root,
            "src",
            "Mimi.Core",
            "Playback",
            "PlaybackStopClassifier.cs");
        Assert.Contains("bool reachedReportedEnd = false", classifier, StringComparison.Ordinal);
    }

    [Fact]
    public void NoStreamExtractionDependencyExistsAnywhereInTheSource()
    {
        var root = FindRepositoryRoot();

        foreach (var file in Directory.EnumerateFiles(
                     Path.Combine(root, "src"),
                     "*.cs",
                     SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            foreach (var token in ForbiddenExtractionTokens)
            {
                Assert.DoesNotContain(token, text, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void OnlineStringsExistInBothLanguages()
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
                     "Nav_Online",
                     "Online_Title",
                     "Online_Description",
                     "Online_SearchLabel",
                     "Online_Search",
                     "Online_SearchAutomation",
                     "Online_ResultListAutomation",
                     "Online_Loading",
                     "Online_ResultsAvailable",
                     "Online_NoResults",
                     "Online_LoadFailed",
                     "Online_Unavailable",
                     "Online_PlayFailed",
                     "Online_PlayAutomation",
                     "Online_OpenSource",
                     "Online_OpenSourceFailed",
                     "Online_Source",
                     "Online_NowPlaying",
                     "Online_Connecting",
                     "Online_LicensePrefix",
                     "Online_ProviderAttribution",
                     "Settings_OnlineSection",
                     "Settings_EnableOnline",
                     "Settings_OnlineDescription"
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
