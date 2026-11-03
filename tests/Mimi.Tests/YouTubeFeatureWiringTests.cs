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
        Assert.Contains("youtube.com/watch?v=", videoModel, StringComparison.Ordinal);
        Assert.Contains("HasValidVideoId", videoModel, StringComparison.Ordinal);

        // The player is created by the official IFrame API inside a page Mimi
        // serves from a virtual host, which is what gives the request an origin
        // and a referrer (without them YouTube answers error 153).
        var codeBehind = Read(root, "src", "Mimi.App", "MainWindow.xaml.cs");
        Assert.Contains("SetVirtualHostNameToFolderMapping", codeBehind, StringComparison.Ordinal);
        Assert.Contains("https://www.youtube.com/iframe_api", codeBehind, StringComparison.Ordinal);
        Assert.Contains("onYouTubeIframeAPIReady", codeBehind, StringComparison.Ordinal);
        Assert.Contains(
            "<meta name=\"referrer\" content=\"strict-origin-when-cross-origin\">",
            codeBehind,
            StringComparison.Ordinal);

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
        // The transport hands playback to the video; the Stop button and the
        // row toggle are what tear the player down.
        // The page flips its own state, so a stale report cannot turn the
        // Play button into a no-op.
        Assert.Contains(
            "RequestEmbeddedPlayer(EmbeddedPlayerCommandKind.Toggle)",
            main,
            StringComparison.Ordinal);
        Assert.Contains(
            "private void StopYouTubeVideo()",
            viewModel,
            StringComparison.Ordinal);

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
    public void TheTransportDrivesTheEmbeddedPlayerThroughItsOfficialBridge()
    {
        var root = FindRepositoryRoot();

        var viewModel = Read(
            root,
            "src",
            "Mimi.App",
            "ViewModels",
            "MainWindowViewModel.YouTube.cs");
        // Position and duration come from the player; Mimi never guesses them.
        Assert.Contains(
            "public void ApplyEmbeddedPlayerState(",
            viewModel,
            StringComparison.Ordinal);
        Assert.Contains(
            "public event Action<EmbeddedPlayerCommand>? EmbeddedPlayerCommandRequested;",
            viewModel,
            StringComparison.Ordinal);
        Assert.Contains(
            "public enum EmbeddedPlayerCommandKind",
            viewModel,
            StringComparison.Ordinal);

        var main = Read(root, "src", "Mimi.App", "ViewModels", "MainWindowViewModel.cs");
        // The play/pause button toggles the video instead of stopping it, and
        // the NAudio timeline stops writing over the video's timeline.
        // The page flips its own state, so a stale report cannot turn the
        // Play button into a no-op.
        Assert.Contains(
            "RequestEmbeddedPlayer(EmbeddedPlayerCommandKind.Toggle)",
            main,
            StringComparison.Ordinal);
        // Slider input seeks the video rather than a silent local player.
        Assert.Contains(
            "EmbeddedPlayerCommandKind.Seek",
            main,
            StringComparison.Ordinal);
        Assert.Contains("if (IsYouTubePlaying)", main, StringComparison.Ordinal);

        var queue = Read(
            root,
            "src",
            "Mimi.App",
            "ViewModels",
            "MainWindowViewModel.PlaybackQueue.cs");
        Assert.Contains("RequestEmbeddedPlayer(EmbeddedPlayerCommandKind.Seek", queue, StringComparison.Ordinal);

        var codeBehind = Read(root, "src", "Mimi.App", "MainWindow.xaml.cs");
        // One synchronous bridge: the host asks the page for a state object and
        // sends commands as script calls. No message channel to configure.
        Assert.Contains("ExecuteScriptAsync", codeBehind, StringComparison.Ordinal);
        Assert.Contains("window.mimi.report", codeBehind, StringComparison.Ordinal);
        Assert.Contains("window.mimi.command", codeBehind, StringComparison.Ordinal);
        // The player must be big enough for YouTube's own controls, and Mimi may
        // not cover them.
        var xaml = Read(root, "src", "Mimi.App", "MainWindow.xaml");
        Assert.Contains("MinHeight=\"200\"", xaml, StringComparison.Ordinal);

        // A silent bridge must be diagnosable from the UI, not only the log.
        Assert.Contains("DiagnoseYouTubePlayerCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("EmbeddedPlayerDiagnosticRequested", viewModel, StringComparison.Ordinal);
        Assert.Contains("ApplyEmbeddedPlayerDiagnostic", codeBehind, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTransportCoverageMatchesTheLocalPlayer()
    {
        var root = FindRepositoryRoot();

        var viewModel = Read(
            root,
            "src",
            "Mimi.App",
            "ViewModels",
            "MainWindowViewModel.YouTube.cs");
        // Next/previous, repeat, shuffle and play-all all act on the results
        // list, which is the play order for YouTube.
        Assert.Contains("private bool TryHandleYouTubeNavigation(", viewModel, StringComparison.Ordinal);
        Assert.Contains("private bool PlayAdjacentYouTubeVideo(", viewModel, StringComparison.Ordinal);
        Assert.Contains("private void HandleYouTubeVideoEnded()", viewModel, StringComparison.Ordinal);
        Assert.Contains("private void PlayAllYouTubeVideos()", viewModel, StringComparison.Ordinal);
        Assert.Contains("EmbeddedPlayerCommandKind.SetVolume", viewModel, StringComparison.Ordinal);
        Assert.Contains("public int YouTubeVolumePercent", viewModel, StringComparison.Ordinal);

        var main = Read(root, "src", "Mimi.App", "ViewModels", "MainWindowViewModel.cs");
        Assert.Contains("TryHandleYouTubeNavigation(-1)", main, StringComparison.Ordinal);
        Assert.Contains("TryHandleYouTubeNavigation(1)", main, StringComparison.Ordinal);

        var queue = Read(
            root,
            "src",
            "Mimi.App",
            "ViewModels",
            "MainWindowViewModel.PlaybackQueue.cs");
        Assert.Contains("PushYouTubeVolume();", queue, StringComparison.Ordinal);

        var codeBehind = Read(root, "src", "Mimi.App", "MainWindow.xaml.cs");
        Assert.Contains("player.setVolume(", codeBehind, StringComparison.Ordinal);
        Assert.Contains("player.seekTo(0, true)", codeBehind, StringComparison.Ordinal);

        var xaml = Read(root, "src", "Mimi.App", "MainWindow.xaml");
        Assert.Contains("PlayAllYouTubeVideosCommand", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void TheResultListHasTheSameRightClickActionsAndASavedList()
    {
        var root = FindRepositoryRoot();

        var xaml = Read(root, "src", "Mimi.App", "MainWindow.xaml");
        Assert.Contains("ToggleYouTubeFavoriteCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("CopyYouTubeLinkCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("ToggleYouTubeSavedViewCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding YouTubeDisplayedVideos}\"", xaml, StringComparison.Ordinal);
        // Radio and the online catalog answer a right click too.
        Assert.Contains("PlayRadioStationCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("OpenOnlineTrackPageCommand", xaml, StringComparison.Ordinal);

        var viewModel = Read(
            root,
            "src",
            "Mimi.App",
            "ViewModels",
            "MainWindowViewModel.YouTube.cs");
        Assert.Contains("public ObservableCollection<YouTubeVideoItemViewModel> SavedYouTubeVideos", viewModel, StringComparison.Ordinal);
        Assert.Contains("private void ToggleYouTubeFavorite(", viewModel, StringComparison.Ordinal);
        Assert.Contains("private void CopyYouTubeLink(", viewModel, StringComparison.Ordinal);
        Assert.Contains("public bool IsYouTubeFavorite(string videoId)", viewModel, StringComparison.Ordinal);

        // Saved items are ids and labels only, in their own settings list.
        var settingsModel = Read(root, "src", "Mimi.Core", "Models", "AppSettings.cs");
        Assert.Contains("public List<SavedYouTubeVideo> YouTubeFavorites", settingsModel, StringComparison.Ordinal);

        // Play inside the YouTube panel no longer falls through to the library.
        var main = Read(root, "src", "Mimi.App", "ViewModels", "MainWindowViewModel.cs");
        Assert.Contains("if (IsYouTubeOpen && SelectedYouTubeVideo is { } youTubeSelection)", main, StringComparison.Ordinal);
    }

    [Fact]
    public void TheEmbedIdentifiesItselfSoTheVideoPlaysInsideMimi()
    {
        var root = FindRepositoryRoot();
        var codeBehind = Read(root, "src", "Mimi.App", "MainWindow.xaml.cs");

        // A page served from a virtual https host gives the iframe an origin
        // and a referrer. Without one YouTube answers error 153 and the owner
        // is pushed out to youtube.com, which is exactly what must not happen.
        Assert.Contains("SetVirtualHostNameToFolderMapping(", codeBehind, StringComparison.Ordinal);
        Assert.Contains("mimi-player.local", codeBehind, StringComparison.Ordinal);
        Assert.Contains("strict-origin-when-cross-origin", codeBehind, StringComparison.Ordinal);

        // WebView2 does not always send Referer itself, so Mimi fills it in
        // rather than leaving the player unattributable.
        Assert.Contains("AddWebResourceRequestedFilter(", codeBehind, StringComparison.Ordinal);
        Assert.Contains("\"Referer\"", codeBehind, StringComparison.Ordinal);
        Assert.Contains("SetHeader(", codeBehind, StringComparison.Ordinal);

        // The page also names itself to the player.
        Assert.Contains("origin: window.location.origin", codeBehind, StringComparison.Ordinal);
        Assert.Contains("widget_referrer:", codeBehind, StringComparison.Ordinal);

        // A refused embed is reported instead of leaving a dead progress bar.
        Assert.Contains("ReportEmbeddedPlayerError(", codeBehind, StringComparison.Ordinal);
        Assert.Contains(
            "YouTube_ReferrerRejected",
            Read(root, "src", "Mimi.App", "ViewModels", "MainWindowViewModel.YouTube.cs"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void ThePlayerPollCannotGetStuckAndSaysWhyItIsEmpty()
    {
        var root = FindRepositoryRoot();
        var codeBehind = Read(root, "src", "Mimi.App", "MainWindow.xaml.cs");

        // A script call issued while the page navigates can stay pending. If
        // the in-flight flag is never cleared, the timeline looks dead while
        // the video keeps playing.
        Assert.Contains("Task.WhenAny(", codeBehind, StringComparison.Ordinal);
        Assert.Contains("NavigationCompleted", codeBehind, StringComparison.Ordinal);
        Assert.Contains("youTubePollInFlight = false;", codeBehind, StringComparison.Ordinal);

        // The page reports why it has no state instead of returning null, so a
        // missing player can be told apart from a slow one.
        Assert.Contains("problem: 'api-missing'", codeBehind, StringComparison.Ordinal);
        Assert.Contains("problem: 'player-missing'", codeBehind, StringComparison.Ordinal);
        Assert.Contains("problem.GetString()", codeBehind, StringComparison.Ordinal);

        // Play/Pause is decided inside the page, where the state is real.
        Assert.Contains("command.command === 'toggle'", codeBehind, StringComparison.Ordinal);
        Assert.Contains(
            "EmbeddedPlayerCommandKind.Toggle => ",
            codeBehind,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheKeyLivesInATerminalCommandAndNeverInTheSettingsPanel()
    {
        var root = FindRepositoryRoot();

        // Standing owner rule: no key field anywhere in Settings. The key is
        // typed into a terminal (tools\Set-YouTubeKey.ps1) or set as an
        // environment variable, and Mimi only ever reads it back.
        var xaml = Read(root, "src", "Mimi.App", "MainWindow.xaml");
        Assert.DoesNotContain("PasswordBox", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("YouTubeApiKeyBox", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("YouTubeKeyBox", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("youtube-api-key", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Settings_YouTubeSection", xaml, StringComparison.Ordinal);
        // The panel may explain where the key comes from; it must not offer a
        // field to paste one.
        Assert.Contains("Settings_YouTubeKeyHint", xaml, StringComparison.Ordinal);

        // The old guidance pointed at Settings; the key is stored by script.
        foreach (var file in new[] { "UiStrings.resx", "UiStrings.vi.resx" })
        {
            var resx = Read(root, "src", "Mimi.App", "Localization", file);
            var line = resx
                .Split('\n')
                .FirstOrDefault(candidate =>
                    candidate.Contains(
                        "name=\"YouTube_InvalidKey\"",
                        StringComparison.Ordinal));
            Assert.NotNull(line);
            Assert.Contains("Set-YouTubeKey.ps1", line!, StringComparison.Ordinal);
            Assert.DoesNotContain("Settings", line!, StringComparison.Ordinal);
        }

        // The terminal path is real: the script exists and has a status check.
        var script = Read(root, "tools", "Set-YouTubeKey.ps1");
        Assert.Contains("youtube-api-key.txt", script, StringComparison.Ordinal);
        Assert.Contains("Read-Host -AsSecureString", script, StringComparison.Ordinal);
        Assert.Contains("[switch]$Remove", script, StringComparison.Ordinal);
    }

    [Fact]
    public void TheKeyIsConfiguredFromATerminalAndNeverInTheUi()
    {
        var root = FindRepositoryRoot();

        // Owner requirement: no API key field anywhere in the interface.
        var xaml = Read(root, "src", "Mimi.App", "MainWindow.xaml");
        Assert.DoesNotContain("PasswordBox", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("YouTubeApiKeyBox", xaml, StringComparison.Ordinal);
        // A hint that names the terminal script is allowed; a field that takes
        // a key is what this rule forbids, so the only key reference in the
        // window is that hint.
        Assert.Contains("Settings_YouTubeKeyHint", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Settings_YouTubeKeyField", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Loc[Settings_YouTubeKey]", xaml, StringComparison.Ordinal);

        var viewModel = Read(
            root,
            "src",
            "Mimi.App",
            "ViewModels",
            "MainWindowViewModel.YouTube.cs");
        Assert.DoesNotContain("SaveYouTubeApiKey", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("TrySaveApiKey", viewModel, StringComparison.Ordinal);

        // The terminal script is the supported way to set the key.
        var script = Read(root, "tools", "Set-YouTubeKey.ps1");
        Assert.Contains("youtube-api-key.txt", script, StringComparison.Ordinal);
        Assert.Contains("MIMI_YOUTUBE_API_KEY", script, StringComparison.Ordinal);
        Assert.Contains("AsSecureString", script, StringComparison.Ordinal);
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
                     "YouTube_EmbeddingBlocked",
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

    [Fact]
    public void TheVideoStageReusesTheOnePlayerInsteadOfMovingIt()
    {
        var root = FindRepositoryRoot();
        var stage = Read(root, "src", "Mimi.App", "MainWindow.VideoStage.cs");
        var shell = Read(root, "src", "Mimi.App", "MainWindow.xaml");
        var main = Read(root, "src", "Mimi.App", "MainWindow.xaml.cs");
        var keyboard = Read(root, "src", "Mimi.App", "MainWindow.Keyboard.cs");

        // The shell has to be addressable: the stage collapses it around the
        // player rather than building a second window with a second player.
        foreach (var name in new[]
                 {
                     "ShellGrid",
                     "AppHeader",
                     "AppTransport",
                     "NavRail",
                     "MainContent",
                     "NowPlayingPanel",
                     "PlayerCard",
                     "NavColumn",
                     "MainColumn",
                     "NowPlayingColumn",
                     "AppHeaderRow",
                     "AppTransportRow"
                 })
        {
            Assert.Contains("x:Name=\"" + name + "\"", shell, StringComparison.Ordinal);
        }

        Assert.Contains("private void EnterVideoStage()", stage, StringComparison.Ordinal);
        Assert.Contains("private void ExitVideoStage()", stage, StringComparison.Ordinal);

        // Leaving the stage must restore the shell and the card's own height,
        // otherwise every later layout change stays full-screen.
        Assert.Contains(
            "PlayerCard.ClearValue(FrameworkElement.HeightProperty)",
            stage,
            StringComparison.Ordinal);
        Assert.Contains("WindowState = videoStageRestoreState;", stage, StringComparison.Ordinal);
        Assert.Contains("NowPlayingColumn.Width = new GridLength(270);", stage, StringComparison.Ordinal);

        // The Stage shortcut and the Stage button both go through it.
        Assert.Contains("ToggleVideoStage();", main, StringComparison.Ordinal);
        Assert.Contains("if (isVideoStageOpen || viewModel.IsYouTubePlaying)", main, StringComparison.Ordinal);

        // Esc leaves the video before it means anything else.
        Assert.Contains("Key.Escape && isVideoStageOpen", keyboard, StringComparison.Ordinal);
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
