namespace Mimi.Tests;

/// <summary>
/// Source-level guard for the visible Phase 6 lyrics slice. The parser and
/// service have behavior tests; this test keeps the app composition, track
/// change trigger, position updates, and Now Playing card connected.
/// </summary>
public sealed class LyricsFeatureWiringTests
{
    [Fact]
    public void LyricsFeatureIsWiredFromServicesThroughNowPlaying()
    {
        var root = FindRepositoryRoot();

        var appCode = Read(root, "src", "Mimi.App", "App.xaml.cs");
        Assert.Contains(
            "services.AddSingleton<ILyricsService, FileLyricsService>();",
            appCode,
            StringComparison.Ordinal);

        var mainViewModel = Read(
            root,
            "src",
            "Mimi.App",
            "ViewModels",
            "MainWindowViewModel.cs");
        Assert.Contains(
            "ILyricsService? lyricsService = null",
            mainViewModel,
            StringComparison.Ordinal);
        Assert.Contains(
            "this.lyricsService = lyricsService;",
            mainViewModel,
            StringComparison.Ordinal);
        Assert.Contains(
            "Interval = TimeSpan.FromMilliseconds(100)",
            mainViewModel,
            StringComparison.Ordinal);
        Assert.Contains(
            "UpdateCurrentLyricLine(position);",
            mainViewModel,
            StringComparison.Ordinal);
        Assert.Contains(
            "CancelLyricsLoad();",
            mainViewModel,
            StringComparison.Ordinal);

        var playbackQueue = Read(
            root,
            "src",
            "Mimi.App",
            "ViewModels",
            "MainWindowViewModel.PlaybackQueue.cs");
        Assert.Contains(
            "StartLyricsLoad(value);",
            playbackQueue,
            StringComparison.Ordinal);

        var lyricsViewModel = Read(
            root,
            "src",
            "Mimi.App",
            "ViewModels",
            "MainWindowViewModel.Lyrics.cs");
        Assert.Contains(
            "lyricsService!.LoadAsync",
            lyricsViewModel,
            StringComparison.Ordinal);
        Assert.Contains(
            "settings.LyricsOffsetsByPath",
            lyricsViewModel,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "partial void OnCurrentTrackChanged",
            lyricsViewModel,
            StringComparison.Ordinal);

        var mainWindow = Read(root, "src", "Mimi.App", "MainWindow.xaml");
        Assert.Contains(
            "{Binding Loc[Lyrics_Title]}",
            mainWindow,
            StringComparison.Ordinal);
        Assert.Contains(
            "ItemsSource=\"{Binding LyricLines}\"",
            mainWindow,
            StringComparison.Ordinal);
        Assert.Contains(
            "SelectedItem=\"{Binding CurrentLyricLine, Mode=OneWay}\"",
            mainWindow,
            StringComparison.Ordinal);
        Assert.Contains(
            "DecreaseLyricsOffsetCommand",
            mainWindow,
            StringComparison.Ordinal);
        Assert.Contains(
            "IncreaseLyricsOffsetCommand",
            mainWindow,
            StringComparison.Ordinal);
        Assert.Contains(
            "ScrollSelectedItemIntoViewBehavior.IsEnabled=\"True\"",
            mainWindow,
            StringComparison.Ordinal);
        Assert.Contains(
            "VirtualizingPanel.ScrollUnit=\"Pixel\"",
            mainWindow,
            StringComparison.Ordinal);

        var scrollBehavior = Read(
            root,
            "src",
            "Mimi.App",
            "Behaviors",
            "ScrollSelectedItemIntoViewBehavior.cs");
        Assert.Contains(
            "scrollViewer.CanContentScroll",
            scrollBehavior,
            StringComparison.Ordinal);
        Assert.Contains(
            "ScrollUnit.Item",
            scrollBehavior,
            StringComparison.Ordinal);

        Assert.Contains(
            "PreviousLyricLine = synchronizedDocument is not null",
            lyricsViewModel,
            StringComparison.Ordinal);
        Assert.Contains(
            "NextLyricLine = synchronizedDocument is not null",
            lyricsViewModel,
            StringComparison.Ordinal);

        var mainWindowCode = Read(root, "src", "Mimi.App", "MainWindow.xaml.cs");
        Assert.Contains(
            "nameof(MainWindowViewModel.CurrentLyricLine)",
            mainWindowCode,
            StringComparison.Ordinal);
        Assert.Contains(
            "stageWindow?.SetLyrics(",
            mainWindowCode,
            StringComparison.Ordinal);

        var stageWindow = Read(root, "src", "Mimi.App", "StageWindow.xaml");
        Assert.Contains(
            "x:Name=\"StageLyricsPanel\"",
            stageWindow,
            StringComparison.Ordinal);
        Assert.Contains(
            "x:Name=\"StageLyricCurrent\"",
            stageWindow,
            StringComparison.Ordinal);

        var stageCode = Read(root, "src", "Mimi.App", "StageWindow.xaml.cs");
        Assert.Contains(
            "public void SetLyrics",
            stageCode,
            StringComparison.Ordinal);

        var settingsModel = Read(
            root,
            "src",
            "Mimi.Core",
            "Models",
            "AppSettings.cs");
        var expectedSchemaVersion =
            global::Mimi.Models.AppSettings.CurrentVersion;
        Assert.Contains(
            $"CurrentVersion = {expectedSchemaVersion}",
            settingsModel,
            StringComparison.Ordinal);
        Assert.Contains(
            "LyricsOffsetsByPath",
            settingsModel,
            StringComparison.Ordinal);
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
