using System.Text.RegularExpressions;

namespace Mimi.Tests;

/// <summary>
/// Guards the bilingual UI against new hard-coded strings slipping in.
/// User-visible text must come from the Loc[...] resource system; the only
/// literals allowed are branding, keycap names, language self-names,
/// glyphs, separators, and unit-only diagnostics, all listed explicitly
/// below so additions are a deliberate review decision.
/// </summary>
public sealed class HardcodedUiStringTests
{
    private static readonly HashSet<string> AllowedXamlLiterals =
        new(StringComparer.Ordinal)
        {
            // Branding (not translated).
            "Mimi",
            "Mimi Stage",
            "MIMI",
            "MIMI BETA",
            "M",

            // Language self-names in the language picker.
            "English",
            "Ti\u1EBFng Vi\u1EC7t",

            // Keycap names in the shortcuts reference.
            "Space",
            "F11",
            "Esc",
            "Ctrl + F",
            "Ctrl + \u2190 / \u2192",
            "\u2190 / \u2192",
            "\u2191 / \u2193",

            // Unit-only labels/diagnostics (s = SI second symbol).
            "\u221210s",
            "+10s",
            "\u22120.1s",
            "+0.1s",
            "0 FPS \u00B7 0.00 ms"
        };

    private static readonly HashSet<string> AllowedViewModelFieldLiterals =
        new(StringComparer.Ordinal)
        {
            "0 FPS \u00B7 0.00 ms"
        };

    // \x22 is the double-quote character; spelling it out keeps these
    // patterns free of nested-quote escaping.
    private static readonly Regex XamlLiteralAttribute = new(
        "(?:Text|Content|Header|ToolTip|Title|AutomationProperties\\.Name)" +
        "\\s*=\\s*\\x22([^\\x22{][^\\x22]*)\\x22",
        RegexOptions.Compiled);

    private static readonly Regex ViewModelFieldInitializer = new(
        "private string [A-Za-z_][A-Za-z0-9_]* = \\x22([^\\x22]*)\\x22;",
        RegexOptions.Compiled);

    [Fact]
    public void XamlContainsNoHardcodedUiText()
    {
        var appDirectory = Path.Combine(FindRepositoryRoot(), "src", "Mimi.App");
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(
                     appDirectory,
                     "*.xaml",
                     SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            foreach (Match match in XamlLiteralAttribute.Matches(text))
            {
                var value = match.Groups[1].Value;
                if (!value.Any(char.IsLetter) ||
                    AllowedXamlLiterals.Contains(value))
                {
                    continue;
                }

                violations.Add(
                    $"{Path.GetFileName(file)}: \"{value}\"");
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void ViewModelLabelFieldsContainNoHardcodedUiText()
    {
        var viewModelsDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Mimi.App",
            "ViewModels");
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(
                     viewModelsDirectory,
                     "*.cs",
                     SearchOption.TopDirectoryOnly))
        {
            var text = File.ReadAllText(file);
            foreach (Match match in ViewModelFieldInitializer.Matches(text))
            {
                var value = match.Groups[1].Value;
                if (!value.Any(char.IsLetter) ||
                    AllowedViewModelFieldLiterals.Contains(value))
                {
                    continue;
                }

                violations.Add(
                    $"{Path.GetFileName(file)}: \"{value}\"");
            }
        }

        Assert.Empty(violations);
    }

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
