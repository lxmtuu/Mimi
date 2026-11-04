using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Mimi.Tests;

/// <summary>
/// Documentation rots in a specific way: a file drifts to the repository root,
/// nobody indexes it, a link points at a path that moved, and six months later
/// "the docs" means thirty files nobody can find. These checks keep the map
/// honest and every relative link resolvable.
/// </summary>
public sealed class DocumentationLayoutTests
{
    private const string Map = "docs/README.md";

    // The only markdown allowed at the repository root: GitHub reads some of
    // these from there, and the release zip ships the rest inside the install.
    private static readonly HashSet<string> RootAllowList = new(StringComparer.Ordinal)
    {
        "README.md",
        "CONTRIBUTING.md",
        "SECURITY.md",
        "CHANGELOG.md",
        "THIRD-PARTY-NOTICES.md",
    };

    [Fact]
    public void EveryDocumentUnderDocsIsListedOnTheMap()
    {
        var map = File.ReadAllText(Path.Combine(RepositoryRoot(), Map));
        var unlisted = new List<string>();

        foreach (var document in DocumentsUnderDocs())
        {
            // The map lists siblings as bare relative paths, e.g. (ROADMAP.md)
            // or (acceptance/README.md).
            var relative = Path.GetRelativePath(
                Path.Combine(RepositoryRoot(), "docs"),
                document).Replace('\\', '/');

            if (!map.Contains($"]({relative})", StringComparison.Ordinal))
                unlisted.Add(relative);
        }

        Assert.True(
            unlisted.Count == 0,
            "These documents are not on docs/README.md: " + string.Join(", ", unlisted));
    }

    [Fact]
    public void TheRepositoryRootOnlyHoldsDocumentsThatHaveToBeThere()
    {
        var root = RepositoryRoot();
        var strays = Directory
            .EnumerateFiles(root, "*.md", SearchOption.TopDirectoryOnly)
            .Select(path => Path.GetFileName(path) ?? string.Empty)
            .Where(name => !RootAllowList.Contains(name))
            .ToArray();

        Assert.True(
            strays.Length == 0,
            "Move these under docs/ and add them to docs/README.md: " +
            string.Join(", ", strays));
    }

    [Fact]
    public void NoDocumentLinksToAPathThatDoesNotExist()
    {
        var root = RepositoryRoot();
        var broken = new List<string>();

        foreach (var document in DocumentsInRepository(root))
        {
            foreach (var target in RelativeLinkTargets(document))
            {
                // A link may be written relative to the file it sits in (a doc
                // under docs/) or relative to the repository root (the release
                // body is pasted into GitHub's editor, where root-relative is
                // the only thing that resolves). Either is fine; neither is a
                // broken link.
                var fromFile = Path.GetFullPath(
                    Path.Combine(Path.GetDirectoryName(document)!, target));
                var fromRoot = Path.Combine(root, target);

                if (!File.Exists(fromFile) && !File.Exists(fromRoot))
                    broken.Add($"{Relative(root, document)} -> {target}");
            }
        }

        Assert.True(broken.Count == 0, "Broken relative links:\n" + string.Join("\n", broken));
    }

    private static IEnumerable<string> DocumentsUnderDocs()
    {
        var docs = Path.Combine(RepositoryRoot(), "docs");
        return Directory
            .EnumerateFiles(docs, "*.md", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith(Path.Combine("docs", "README.md"), StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal);
    }

    private static IEnumerable<string> DocumentsInRepository(string root)
    {
        foreach (var document in Directory.EnumerateFiles(root, "*.md", SearchOption.TopDirectoryOnly))
            yield return document;

        foreach (var document in Directory.EnumerateFiles(
                     Path.Combine(root, "docs"), "*.md", SearchOption.AllDirectories))
            yield return document;
    }

    private static IEnumerable<string> RelativeLinkTargets(string document)
    {
        var text = StripFencedCode(File.ReadAllText(document));

        // Inline images count too: a preview that points at a moved PNG is the
        // same broken page as a dead link. The header uses <img> tags so it can
        // be centred and linked, so read those as well.
        var targets = Regex.Matches(text, @"\]\(([^)\s]+)\)")
            .Select(match => match.Groups[1].Value)
            .Concat(Regex.Matches(text, "<img[^>]+src=\"([^\"]+)\"").Select(match => match.Groups[1].Value));

        foreach (var candidate in targets)
        {
            if (candidate.StartsWith("#", StringComparison.Ordinal)) continue;
            if (candidate.Contains("://", StringComparison.Ordinal)) continue;
            if (candidate.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) continue;

            // A link may carry an anchor; the file is what has to exist.
            var target = candidate;
            var hash = target.IndexOf('#');
            if (hash >= 0) target = target[..hash];
            if (target.Length == 0) continue;

            yield return Uri.UnescapeDataString(target);
        }
    }

    /// <summary>Links shown inside a code block are examples, not links.</summary>
    private static string StripFencedCode(string text)
    {
        var kept = new List<string>();
        var inside = false;

        foreach (var line in text.Split('\n'))
        {
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                inside = !inside;
                continue;
            }

            if (!inside) kept.Add(line);
        }

        return string.Join('\n', kept);
    }

    private static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');

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
