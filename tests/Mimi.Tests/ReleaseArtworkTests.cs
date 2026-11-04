using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Mimi.Tests;

/// <summary>
/// A release page is the only thing most people will read before they decide to
/// download, and nothing about it is compiled: a wrong file name, a picture that
/// was never committed, or a claim the build does not keep all ship silently.
/// These checks fail the build instead.
/// </summary>
public sealed class ReleaseArtworkTests
{
    private const string BodyPath = "docs/release/RELEASE-BODY.md";
    private const string NotesPath = "docs/release/0.7.0.md";

    [Fact]
    public void EveryPictureTheReleaseBodyShowsIsInTheRepository()
    {
        var body = Read(BodyPath);

        // Images are referenced by repository path; the render script turns those
        // into raw.githubusercontent URLs at publish time. A path that does not
        // exist renders as a broken image on the release page.
        var missing = new List<string>();
        foreach (Match match in Regex.Matches(body, @"!\[[^\]]*\]\(([^)\s]+)\)"))
        {
            var target = match.Groups[1].Value;
            if (target.StartsWith("http", StringComparison.OrdinalIgnoreCase)) continue;

            if (!File.Exists(Path.Combine(RepositoryRoot(), target)))
                missing.Add(target);
        }

        Assert.True(missing.Count == 0, "Missing artwork: " + string.Join(", ", missing));

        // And there are at least two: a page with no picture is a wall of text.
        Assert.True(Regex.Matches(body, @"!\[").Count >= 2);
    }

    [Fact]
    public void ThePicturesAreTheSizeTheyClaimToBe()
    {
        // A 0-byte or truncated PNG can be committed by accident and still open in
        // an editor. Reading the IHDR header needs no image library.
        Assert.Equal((1280, 640), PngSize("docs/assets/release/banner-vi.png"));
        Assert.Equal((1280, 640), PngSize("docs/assets/release/banner-en.png"));
        Assert.Equal((1280, 560), PngSize("docs/assets/release/whats-new-0.7.0.png"));

        foreach (var file in Directory.EnumerateFiles(
                     Path.Combine(RepositoryRoot(), "docs", "assets", "release"), "*.png"))
        {
            // Anything smaller than this is a placeholder, not artwork.
            Assert.True(
                new FileInfo(file).Length > 20_000,
                $"{Path.GetFileName(file)} looks empty ({new FileInfo(file).Length} bytes)");
        }
    }

    [Fact]
    public void TheReleaseBodyNamesTheFileNamesTheWorkflowProduces()
    {
        var body = Read(BodyPath);
        var version = ReadProjectVersion();

        // The zip is named after the tag; the installer after the version. Both
        // were renamed once already, so neither name may be typed twice.
        Assert.Contains($"Mimi-{version}-Setup.exe", body, StringComparison.Ordinal);
        Assert.Contains($"Mimi-v{version}-win-x64.zip", body, StringComparison.Ordinal);
        Assert.Contains($"releases/download/v{version}/", body, StringComparison.Ordinal);
        Assert.Contains("SHA256SUMS.txt", body, StringComparison.Ordinal);

        var release = Read(Path.Combine(".github", "workflows", "release.yml"));
        Assert.Contains("Mimi-$tag-win-x64", release, StringComparison.Ordinal);
    }

    [Fact]
    public void TheReleaseBodySaysWhatIsNotDone()
    {
        var body = Read(BodyPath);

        // Two promises the project cannot keep yet. If either line is edited away,
        // the page starts implying more than the build delivers.
        Assert.Contains("chưa được ký số", body, StringComparison.Ordinal);
        Assert.Contains("chưa qua lượt cài thật", body, StringComparison.Ordinal);
        Assert.Contains("SmartScreen", body, StringComparison.Ordinal);

        // Privacy is a claim, so the place holding the evidence is linked.
        Assert.Contains("SECURITY.md", body, StringComparison.Ordinal);
    }

    [Fact]
    public void TheReleasePublishesTheWrittenBodyAndNotGeneratedNotes()
    {
        var release = Read(Path.Combine(".github", "workflows", "release.yml"));

        // --generate-notes would silently replace the download table and the
        // honesty section with a list of commit subjects.
        Assert.DoesNotContain("--generate-notes", release, StringComparison.Ordinal);
        Assert.Contains("--notes-file", release, StringComparison.Ordinal);
        Assert.Contains("render_release_notes.ps1", release, StringComparison.Ordinal);

        // Re-running a workflow must update the body, not keep the old one.
        Assert.Contains("gh release edit $tag --notes-file $notes", release, StringComparison.Ordinal);
    }

    [Fact]
    public void TheRenderScriptRewritesWhatBreaksOnAReleasePage()
    {
        var script = Read(Path.Combine("tools", "release-art", "render_release_notes.ps1"));

        // Relative links work in the repository and 404 on a release page, so the
        // script has to lift every one of them to an absolute URL.
        Assert.Contains("raw.githubusercontent.com", script, StringComparison.Ordinal);
        Assert.Contains("blob/main", script, StringComparison.Ordinal);
        Assert.Contains("releases/download", script, StringComparison.Ordinal);

        // UTF-8 without BOM: a BOM on a release body shows as a stray character.
        Assert.Contains("UTF8Encoding]::new($false)", script, StringComparison.Ordinal);

        // It refuses to publish a half-substituted body.
        Assert.Contains("Unsubstituted placeholder", script, StringComparison.Ordinal);
    }

    [Fact]
    public void TheRenderScriptIsSyntaxCheckedOnEveryBuild()
    {
        var build = Read(Path.Combine(".github", "workflows", "windows-build.yml"));
        var script = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "tools", "release-art", "render_release_notes.ps1"));

        // A syntax error in a release-only script is invisible until a tag is
        // pushed, which is the worst moment to discover it. The build parses
        // every .ps1 under tools/ recursively.
        Assert.Contains("-Filter *.ps1 -Recurse", build, StringComparison.Ordinal);
        Assert.Contains("Language.Parser]::ParseFile", build, StringComparison.Ordinal);

        // It must not depend on anything outside the repository.
        Assert.DoesNotContain("Install-Module", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Invoke-WebRequest", script, StringComparison.Ordinal);
    }

    [Fact]
    public void TheArtworkCanBeRebuiltFromThisRepositoryAlone()
    {
        foreach (var tool in new[] { "banner.py", "whats_new.py", "cutout_mascot.py" })
        {
            var path = Path.Combine(RepositoryRoot(), "tools", "release-art", tool);
            Assert.True(File.Exists(path), $"{tool} is missing");

            var source = File.ReadAllText(path);

            // No stock art, no remote fetch: a release picture that depends on
            // something outside the repository cannot be reproduced later.
            Assert.DoesNotContain("http://", source, StringComparison.Ordinal);
            Assert.DoesNotContain("https://", source, StringComparison.Ordinal);
            Assert.DoesNotContain("urllib", source, StringComparison.Ordinal);
            Assert.DoesNotContain("requests.", source, StringComparison.Ordinal);
        }

        // The pieces the scripts draw from have to be here too.
        Assert.True(File.Exists(Path.Combine(RepositoryRoot(), "src", "Mimi.App", "Assets", "mimi-logo.png")));
        Assert.True(File.Exists(Path.Combine(
            RepositoryRoot(), "src", "Mimi.App", "Assets", "Mascot", "mimi-mascot-v1.png")));
        Assert.True(File.Exists(Path.Combine(
            RepositoryRoot(), "src", "Mimi.App", "Assets", "Fonts", "BeVietnamPro-ExtraBold.ttf")));
    }

    [Fact]
    public void TheRepositoryCopyAndThePublishedBodyStayInStep()
    {
        var source = Read(BodyPath);
        var copy = Read(NotesPath);

        var version = ReadProjectVersion();

        // The banner and the panel are inserted by the repository copy, so it
        // differs from the source by exactly those two lines plus their captions.
        var added = copy
            .Split('\n')
            .Where(line => !source.Contains(line, StringComparison.Ordinal))
            .Where(line => line.Trim().Length > 0)
            .Where(line => !line.StartsWith("![", StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            added.Length == 0,
            "docs/release/0.7.0.md has text the source does not:\n" + string.Join("\n", added));

        Assert.Contains("banner-vi.png", copy, StringComparison.Ordinal);
        Assert.Contains($"whats-new-{version}.png", copy, StringComparison.Ordinal);
    }

    private static (int Width, int Height) PngSize(string relativePath)
    {
        var bytes = File.ReadAllBytes(Path.Combine(RepositoryRoot(), relativePath));

        // 8-byte signature, 4-byte length, "IHDR", then width and height.
        Assert.True(bytes.Length > 24, $"{relativePath} is too short to be a PNG");
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal("PNG", System.Text.Encoding.ASCII.GetString(bytes, 1, 3));

        var width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
        var height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
        return (width, height);
    }

    private static string ReadProjectVersion()
    {
        var project = Read(Path.Combine("src", "Mimi.App", "Mimi.App.csproj"));
        var match = Regex.Match(project, @"<Version>([^<]+)</Version>");
        Assert.True(match.Success, "Mimi.App.csproj has no <Version>");
        return match.Groups[1].Value.Trim();
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
