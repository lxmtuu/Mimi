using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Mimi.Tests;

/// <summary>
/// The README is the front door, and the pictures on it are files like any
/// other: rename one, forget to commit one, or bump the version and forget the
/// pill, and the page breaks without anything failing. These checks fail the
/// build instead.
/// </summary>
public sealed class ReadmeArtworkTests
{
    private const string Readme = "README.md";

    [Fact]
    public void EveryImageTheReadmeShowsIsCommittedAtThePathItNames()
    {
        var readme = Read(Readme);
        var root = RepositoryRoot();
        var missing = new System.Collections.Generic.List<string>();

        // HTML images, because the header needs them centred and clickable.
        foreach (Match match in Regex.Matches(readme, "<img[^>]+src=\"([^\"]+)\""))
        {
            var target = match.Groups[1].Value;
            if (target.StartsWith("http", StringComparison.OrdinalIgnoreCase)) continue;
            if (!File.Exists(Path.Combine(root, target))) missing.Add(target);
        }

        // And the markdown ones, in case the header ever goes back to plain markdown.
        foreach (Match match in Regex.Matches(readme, @"!\[[^\]]*\]\(([^)\s]+)\)"))
        {
            var target = match.Groups[1].Value;
            if (target.StartsWith("http", StringComparison.OrdinalIgnoreCase)) continue;
            if (!File.Exists(Path.Combine(root, target))) missing.Add(target);
        }

        Assert.True(missing.Count == 0, "Missing README artwork: " + string.Join(", ", missing));
    }

    [Fact]
    public void TheDownloadButtonsLeadSomewhereThatExists()
    {
        var readme = Read(Readme);

        // Both buttons wrap an image in a link; the link has to be the release
        // page, not a guessed asset URL for a tag that may not exist yet.
        foreach (Match match in Regex.Matches(readme, "<a href=\"([^\"]+)\">"))
        {
            var target = match.Groups[1].Value;
            Assert.True(
                target.Contains("releases", StringComparison.Ordinal) ||
                target.StartsWith("#", StringComparison.Ordinal) ||
                target.StartsWith("https://github.com/lxmtuu/Mimi", StringComparison.Ordinal),
                $"Unexpected README button target: {target}");
        }

        Assert.Contains("btn-download-windows.png", readme, StringComparison.Ordinal);
        Assert.Contains("btn-download-portable.png", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void TheVersionPillCannotDriftAwayFromTheProject()
    {
        // The pill is a picture, so the number is drawn in at generation time.
        // The generator reads <Version> from the project file; it must never keep
        // its own copy, and the README must not restate a version in prose either.
        var generator = Read(Path.Combine("tools", "release-art", "readme_badges.py"));

        Assert.Contains("Mimi.App.csproj", generator, StringComparison.Ordinal);
        Assert.Contains("<Version>", generator, StringComparison.Ordinal);
        Assert.DoesNotContain("version = \"0.", generator, StringComparison.Ordinal);

        // The download table therefore talks about "<phiên bản>" and "<tag>",
        // which stay true whatever the next release is called.
        var readme = Read(Readme);
        Assert.Contains("Mimi-<phiên bản>-Setup.exe", readme, StringComparison.Ordinal);
        Assert.Contains("Mimi-<tag>-win-x64.zip", readme, StringComparison.Ordinal);

        // Historical version numbers in the acceptance notes are fine; a current
        // one typed by hand is not.
        var project = Read(Path.Combine("src", "Mimi.App", "Mimi.App.csproj"));
        var version = Regex.Match(project, @"<Version>([^<]+)</Version>").Groups[1].Value.Trim();
        Assert.DoesNotContain($"Mimi-{version}-Setup.exe", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePreviewsSayTheyAreDrawingsAndNotScreenshots()
    {
        var readme = Read(Readme);

        // Claiming a drawing is a screenshot is the kind of small lie that costs
        // a project its credibility. The note has to stay.
        Assert.Contains("không phải ảnh chụp màn hình", readme, StringComparison.Ordinal);
        Assert.Contains("BuiltInThemes.cs", readme, StringComparison.Ordinal);
        Assert.Contains("MainWindow.xaml", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePreviewSceneStaysInsideTheGapTheRealPanelHas()
    {
        // The Now Playing column is 270 px wide in MainWindow.xaml, and the rows
        // it draws are laid out in that space. A drawing that overflows it would
        // advertise a layout the app cannot produce.
        var window = Read(Path.Combine("src", "Mimi.App", "MainWindow.xaml"));
        var nowPlayingWidth = Regex.Match(
            window,
            @"x:Name=""NowPlayingColumn"" Width=""(\d+)""").Groups[1].Value;

        Assert.Equal("270", nowPlayingWidth);

        var previews = Read(Path.Combine("tools", "release-art", "ui_previews.py"));
        Assert.Contains("1052, 70, 1312, 716", previews, StringComparison.Ordinal);
    }

    [Fact]
    public void TheArtworkScriptsNeedNothingButThisRepository()
    {
        foreach (var tool in new[] { "ui_previews.py", "readme_badges.py", "ui_kit.py", "ui_icons.py" })
        {
            var path = Path.Combine(RepositoryRoot(), "tools", "release-art", tool);
            Assert.True(File.Exists(path), $"{tool} is missing");

            var source = File.ReadAllText(path);
            Assert.DoesNotContain("http://", source, StringComparison.Ordinal);
            Assert.DoesNotContain("https://", source, StringComparison.Ordinal);
            Assert.DoesNotContain("urllib", source, StringComparison.Ordinal);
            Assert.DoesNotContain("requests.", source, StringComparison.Ordinal);
        }
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath));

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Mimi.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new Xunit.Sdk.XunitException(
            "Could not locate the repository root (Mimi.sln) above " + AppContext.BaseDirectory);
    }
}
