namespace Mimi.Tests;

/// <summary>
/// Source-level guard for the opt-in Radio Browser slice. The HTTP client has
/// behavior tests; this test keeps the separate live-stream playback source,
/// direct Media Foundation loading, provider click, UI, and Windows media
/// controls connected without turning a station into a local library track.
/// </summary>
public sealed class RadioFeatureWiringTests
{
    [Fact]
    public void RadioFeatureIsWiredWithoutCollapsingStationsIntoLocalTracks()
    {
        var root = FindRepositoryRoot();

        var appCode = Read(root, "src", "Mimi.App", "App.xaml.cs");
        Assert.Contains(
            "services.AddSingleton<IRadioStationService, RadioBrowserService>();",
            appCode,
            StringComparison.Ordinal);

        var stationModel = Read(
            root,
            "src",
            "Mimi.Core",
            "Radio",
            "RadioStation.cs");
        Assert.Contains("public sealed record RadioStation", stationModel, StringComparison.Ordinal);
        Assert.Contains("Uri StreamUrl", stationModel, StringComparison.Ordinal);

        var radioClient = Read(root, "src", "Mimi.Library", "RadioBrowserService.cs");
        Assert.Contains("json/stations/topvote", radioClient, StringComparison.Ordinal);
        Assert.Contains("json/stations/search", radioClient, StringComparison.Ordinal);
        Assert.Contains("hidebroken=true", radioClient, StringComparison.Ordinal);
        Assert.Contains("json/url/", radioClient, StringComparison.Ordinal);
        Assert.Contains("new ProductInfoHeaderValue", radioClient, StringComparison.Ordinal);
        Assert.Contains("allowMirrorFailover: false", radioClient, StringComparison.Ordinal);

        var audioContract = Read(
            root,
            "src",
            "Mimi.Core",
            "Services",
            "IAudioPlayer.cs");
        Assert.Contains("bool IsLiveStream", audioContract, StringComparison.Ordinal);
        Assert.Contains("void LoadStream(Uri streamUrl)", audioContract, StringComparison.Ordinal);

        var audioService = Read(root, "src", "Mimi.Audio", "AudioService.cs");
        Assert.Contains(
            "new MediaFoundationReader(streamUrl.AbsoluteUri)",
            audioService,
            StringComparison.Ordinal);
        Assert.Contains(
            "return loadedIsLiveStream",
            audioService,
            StringComparison.Ordinal);
        Assert.Contains(
            "? TimeSpan.Zero",
            audioService,
            StringComparison.Ordinal);
        Assert.Contains(
            "reader is null || loadedIsLiveStream || disposed",
            audioService,
            StringComparison.Ordinal);
        Assert.Contains(
            "endedNaturally = !loadedIsLiveStream",
            audioService,
            StringComparison.Ordinal);
        Assert.Contains(
            "live stream host {streamUrl.Host}",
            audioService,
            StringComparison.Ordinal);

        var mainViewModel = Read(
            root,
            "src",
            "Mimi.App",
            "ViewModels",
            "MainWindowViewModel.cs");
        Assert.Contains(
            "IRadioStationService? radioStationService = null",
            mainViewModel,
            StringComparison.Ordinal);
        Assert.Contains(
            "this.radioStationService = radioStationService;",
            mainViewModel,
            StringComparison.Ordinal);
        Assert.Contains(
            "BeginLocalPlaybackAttempt();",
            mainViewModel,
            StringComparison.Ordinal);
        Assert.Contains(
            "TryHandleRadioStationNavigation",
            mainViewModel,
            StringComparison.Ordinal);
        Assert.Contains(
            "DurationLabel = Loc[\"Radio_Live\"]",
            mainViewModel,
            StringComparison.Ordinal);
        Assert.Contains("CancelRadioWork();", mainViewModel, StringComparison.Ordinal);

        var radioViewModel = Read(
            root,
            "src",
            "Mimi.App",
            "ViewModels",
            "MainWindowViewModel.Radio.cs");
        Assert.Contains(
            "audioPlayer.LoadStream(streamUrl)",
            radioViewModel,
            StringComparison.Ordinal);
        Assert.Contains(
            "RegisterStationClickAsync",
            radioViewModel,
            StringComparison.Ordinal);
        Assert.Contains(
            "CurrentTrack = null;",
            radioViewModel,
            StringComparison.Ordinal);
        Assert.Contains(
            "registerClick: false",
            radioViewModel,
            StringComparison.Ordinal);
        Assert.Contains(
            "CurrentCoverArtPath = null;",
            radioViewModel,
            StringComparison.Ordinal);
        Assert.Contains(
            "InternetRadioEnabled && radioStationService is not null",
            radioViewModel,
            StringComparison.Ordinal);
        Assert.Contains(
            "DisableInternetRadio",
            radioViewModel,
            StringComparison.Ordinal);

        var settingsViewModel = Read(
            root,
            "src",
            "Mimi.App",
            "ViewModels",
            "MainWindowViewModel.Settings.cs");
        Assert.Contains(
            "else if (CurrentRadioStation is null)",
            settingsViewModel,
            StringComparison.Ordinal);
        Assert.Contains(
            "!IsSettingsOpen && !IsRadioOpen",
            settingsViewModel,
            StringComparison.Ordinal);
        Assert.Contains(
            "InternetRadioEnabled = settings.InternetRadioEnabled",
            settingsViewModel,
            StringComparison.Ordinal);
        Assert.Contains(
            "settings.InternetRadioEnabled = InternetRadioEnabled",
            settingsViewModel,
            StringComparison.Ordinal);
        Assert.Contains(
            "DisableInternetRadio();",
            settingsViewModel,
            StringComparison.Ordinal);

        var mainWindow = Read(root, "src", "Mimi.App", "MainWindow.xaml");
        Assert.Contains("ShowRadioCommand", mainWindow, StringComparison.Ordinal);
        Assert.Contains("IsRadioContentVisible", mainWindow, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"RadioStationList\"", mainWindow, StringComparison.Ordinal);
        Assert.Contains("PlayRadioStationCommand", mainWindow, StringComparison.Ordinal);
        Assert.Contains("OpenRadioHomepageCommand", mainWindow, StringComparison.Ordinal);
        Assert.Contains("Radio_ProviderAttribution", mainWindow, StringComparison.Ordinal);
        Assert.Contains("IsRadioServiceInstalled", mainWindow, StringComparison.Ordinal);
        Assert.Contains("Binding InternetRadioEnabled", mainWindow, StringComparison.Ordinal);
        Assert.Contains("Settings_InternetRadioDescription", mainWindow, StringComparison.Ordinal);
        Assert.Contains(
            "Binding=\"{Binding IsLivePlayback}\"",
            mainWindow,
            StringComparison.Ordinal);

        var mediaControls = Read(
            root,
            "src",
            "Mimi.App",
            "Services",
            "WindowsMediaControlsService.cs");
        Assert.Contains("CurrentRadioStation", mediaControls, StringComparison.Ordinal);
        Assert.Contains("HasCurrentPlaybackSource", mediaControls, StringComparison.Ordinal);
        Assert.Contains("MaxSeekTime = livePosition", mediaControls, StringComparison.Ordinal);
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
