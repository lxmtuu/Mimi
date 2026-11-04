using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Mimi.Tests;

/// <summary>
/// Release metadata drifts silently: a version bumped in one file out of two, a
/// changelog that stops at the previous release, a package added without a credit
/// line. None of that breaks a build, and all of it breaks a release. These
/// checks fail the build instead.
/// </summary>
public sealed class ReleaseMetadataTests
{
    [Fact]
    public void TheProjectVersionAndTheManifestVersionAgree()
    {
        var project = ReadProjectVersion();
        var manifest = ReadManifestVersion();

        // The manifest carries a four-part version; the project carries three.
        Assert.Equal(
            $"{project}.0",
            manifest);
    }

    [Fact]
    public void TheChangelogDocumentsTheVersionBeingShipped()
    {
        var version = ReadProjectVersion();
        var changelog = Read("CHANGELOG.md");

        Assert.Contains($"## [{version}]", changelog, StringComparison.Ordinal);

        // Every released version appears once, in descending order, or a reader
        // cannot tell which entry is the newest.
        var headings = Regex
            .Matches(changelog, @"^## \[(\d+\.\d+\.\d+)\]", RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value)
            .ToArray();

        Assert.Equal(headings.Distinct(StringComparer.Ordinal).Count(), headings.Length);
        Assert.Equal(version, headings[0]);
    }

    [Fact]
    public void TheLicenceIsMitAndNamesAHolder()
    {
        var licence = Read("LICENSE");

        Assert.StartsWith("MIT License", licence, StringComparison.Ordinal);
        Assert.Contains("Copyright (c)", licence, StringComparison.Ordinal);
        Assert.Contains("WITHOUT WARRANTY OF ANY KIND", licence, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryShippedPackageIsCredited()
    {
        var notices = Read("THIRD-PARTY-NOTICES.md");
        var missing = new List<string>();

        foreach (var project in EnumerateProjects("src"))
        {
            foreach (var id in ReadPackageIds(project))
            {
                // Ids are matched as words so "NAudio" does not satisfy "NAudio.Core".
                if (!Regex.IsMatch(notices, $@"\b{Regex.Escape(id)}\b"))
                    missing.Add($"{id} ({Path.GetFileName(project)})");
            }
        }

        Assert.True(
            missing.Count == 0,
            "These packages are referenced but not credited in " +
            "THIRD-PARTY-NOTICES.md:\n" + string.Join("\n", missing));
    }

    [Fact]
    public void TheLgplDependencyComesWithItsObligationWrittenDown()
    {
        // TagLibSharp is LGPL-2.1. The obligation that is easy to break by
        // accident is "keep it replaceable", which forbids single-file publishing.
        var notices = Read("THIRD-PARTY-NOTICES.md");
        Assert.Contains("TagLibSharp", notices, StringComparison.Ordinal);
        Assert.Contains("LGPL-2.1", notices, StringComparison.Ordinal);
        Assert.Contains("PublishSingleFile", notices, StringComparison.Ordinal);

        var release = Read(Path.Combine(".github", "workflows", "release.yml"));
        Assert.Contains("-p:PublishSingleFile=false", release, StringComparison.Ordinal);
        Assert.DoesNotContain("-p:PublishSingleFile=true", release, StringComparison.Ordinal);
    }

    [Fact]
    public void TheReleaseWorkflowRefusesATagThatDisagreesWithTheBuild()
    {
        var release = Read(Path.Combine(".github", "workflows", "release.yml"));

        Assert.Contains("Mimi.App.csproj", release, StringComparison.Ordinal);
        Assert.Contains("does not match the project version", release, StringComparison.Ordinal);

        // A self-contained build is the whole point: no .NET install required.
        Assert.Contains("--self-contained true", release, StringComparison.Ordinal);
    }

    [Fact]
    public void TheReadmePointsAtTheDocumentsAUserNeeds()
    {
        var readme = Read("README.md");

        foreach (var document in new[]
                 {
                     "LICENSE",
                     "CHANGELOG.md",
                     "CONTRIBUTING.md",
                     "SECURITY.md",
                     "THIRD-PARTY-NOTICES.md"
                 })
        {
            Assert.Contains(document, readme, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void EveryDocumentTheReadmeLinksExists()
    {
        var readme = Read("README.md");
        var root = RepositoryRoot();
        var missing = new List<string>();

        foreach (Match match in Regex.Matches(readme, @"\]\((?!https?:)([^)#]+)"))
        {
            var target = match.Groups[1].Value.Trim();
            if (target.Length == 0) continue;
            if (!File.Exists(Path.Combine(root, target)))
                missing.Add(target);
        }

        Assert.True(missing.Count == 0, "Broken links in README.md:\n" + string.Join("\n", missing));
    }

    private static string ReadProjectVersion()
    {
        var project = XDocument.Load(Path.Combine(
            RepositoryRoot(), "src", "Mimi.App", "Mimi.App.csproj"));

        // Only the top-level <Version> in a PropertyGroup: a PackageReference may
        // carry a child element of the same name.
        var version = project.Root!
            .Elements("PropertyGroup")
            .Elements("Version")
            .Select(element => element.Value.Trim())
            .FirstOrDefault(value => value.Length > 0);

        Assert.False(string.IsNullOrWhiteSpace(version), "Mimi.App.csproj has no <Version>.");
        return version!;
    }

    private static string ReadManifestVersion()
    {
        var manifest = XDocument.Load(Path.Combine(
            RepositoryRoot(), "src", "Mimi.App", "app.manifest"));

        // app.manifest declares urn:schemas-microsoft-com:asm.v1 as its default
        // namespace, so the element cannot be found by its bare name.
        var identity = manifest
            .Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "assemblyIdentity");
        Assert.NotNull(identity);

        var version = identity!.Attribute("version")?.Value;
        Assert.False(string.IsNullOrWhiteSpace(version), "app.manifest has no version.");
        return version!;
    }

    private static IEnumerable<string> EnumerateProjects(string folder) =>
        Directory.EnumerateFiles(
            Path.Combine(RepositoryRoot(), folder),
            "*.csproj",
            SearchOption.AllDirectories);

    private static IEnumerable<string> ReadPackageIds(string projectPath) =>
        XDocument.Load(projectPath)
            .Descendants("PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!);

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath));

    private static string RepositoryRoot()
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
