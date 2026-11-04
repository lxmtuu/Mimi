using System.IO;
using System.Text.RegularExpressions;
using Mimi.Theming;

namespace Mimi.Tests;

/// <summary>
/// Guards the background layer stack. Every screen is built from the same nine
/// surface tokens, so the stack has to behave the same way in every theme: the
/// window darkest, inset areas below the panel they cut into, panels above the
/// window, raised cards above panels. When that order breaks - or a step
/// becomes so small that two layers are effectively the same colour - the UI
/// looks flat or, worse, like it is wearing two palettes at once.
/// </summary>
public sealed class ThemeLayerTests
{
    /// <summary>
    /// The stack as the layout actually nests it, darkest first. The header bar
    /// is not part of this ladder on purpose: it belongs to whatever list it
    /// labels, so it only has to beat the panel underneath it (checked below).
    /// </summary>
    private static readonly string[] LayersBottomToTop =
    [
        ThemeTokens.AppBackground,
        ThemeTokens.SunkenBackground,
        ThemeTokens.PanelBackground,
        ThemeTokens.RaisedBackground
    ];

    /// <summary>
    /// Controls and selections sit above the card they are drawn on.
    /// </summary>
    private static readonly string[] LayersAboveCards =
    [
        ThemeTokens.RaisedBackground,
        ThemeTokens.ControlBackground,
        ThemeTokens.SelectedBackground,
        ThemeTokens.Border
    ];

    [Fact]
    public void EveryThemeStacksItsSurfacesInTheSameOrder()
    {
        foreach (var theme in BuiltInThemes.All)
        {
            for (var i = 1; i < LayersBottomToTop.Length; i++)
            {
                var below = theme.GetColor(LayersBottomToTop[i - 1]);
                var above = theme.GetColor(LayersBottomToTop[i]);

                Assert.True(
                    above.RelativeLuminance > below.RelativeLuminance,
                    $"{theme.Id}: {LayersBottomToTop[i]} ({above.ToHexString()}) is not " +
                    $"lighter than {LayersBottomToTop[i - 1]} ({below.ToHexString()}).");
            }

            // A list header labels the panel it sits on, so it must read as a
            // band above that panel even when a raised card is lighter than it.
            Assert.True(
                theme.GetColor(ThemeTokens.HeaderBackground).RelativeLuminance >
                theme.GetColor(ThemeTokens.PanelBackground).RelativeLuminance,
                $"{theme.Id}: the header bar is not lighter than the panel it labels.");

            for (var i = 1; i < LayersAboveCards.Length; i++)
            {
                var below = theme.GetColor(LayersAboveCards[i - 1]);
                var above = theme.GetColor(LayersAboveCards[i]);

                Assert.True(
                    above.RelativeLuminance > below.RelativeLuminance,
                    $"{theme.Id}: {LayersAboveCards[i]} ({above.ToHexString()}) is not " +
                    $"lighter than {LayersAboveCards[i - 1]} ({below.ToHexString()}).");
            }
        }
    }

    [Fact]
    public void NoLayerStepIsSoSmallThatTwoSurfacesReadAsOne()
    {
        // Measured reality: the smallest real step in the built-in themes is
        // panel-on-window at 1.06:1. These floors are deliberately just under
        // that, so a palette edit that flattens a layer - the mistake that made
        // cards invisible - fails here instead of on the owner's screen.
        var minimumSteps = new (string Below, string Above, double Ratio)[]
        {
            (ThemeTokens.AppBackground, ThemeTokens.PanelBackground, 1.05),
            (ThemeTokens.PanelBackground, ThemeTokens.RaisedBackground, 1.08),
            (ThemeTokens.RaisedBackground, ThemeTokens.ControlBackground, 1.10),
            (ThemeTokens.ControlBackground, ThemeTokens.SelectedBackground, 1.05)
        };

        foreach (var theme in BuiltInThemes.All)
        {
            foreach (var (below, above, ratio) in minimumSteps)
            {
                var actual = ThemeColor.GetContrastRatio(
                    theme.GetColor(below),
                    theme.GetColor(above));

                Assert.True(
                    actual >= ratio,
                    $"{theme.Id}: {above} only differs from {below} by " +
                    $"{actual:0.00}:1 (needs {ratio:0.00}:1).");
            }
        }
    }

    [Fact]
    public void SunkenSurfacesAreDarkerThanThePanelTheyCutInto()
    {
        foreach (var theme in BuiltInThemes.All)
        {
            var panel = theme.GetColor(ThemeTokens.PanelBackground);
            var sunken = theme.GetColor(ThemeTokens.SunkenBackground);

            Assert.True(
                sunken.RelativeLuminance < panel.RelativeLuminance,
                $"{theme.Id}: a sunken area must be darker than the panel, " +
                $"but {sunken.ToHexString()} is not darker than {panel.ToHexString()}.");
        }
    }

    [Fact]
    public void TextStaysReadableOnEverySurfaceOfEveryTheme()
    {
        var surfaces = new[]
        {
            ThemeTokens.AppBackground,
            ThemeTokens.PanelBackground,
            ThemeTokens.RaisedBackground,
            ThemeTokens.SunkenBackground,
            ThemeTokens.ControlBackground,
            ThemeTokens.SelectedBackground,
            ThemeTokens.HeaderBackground
        };

        var texts = new[]
        {
            ThemeTokens.TextPrimary,
            ThemeTokens.TextStrong,
            ThemeTokens.TextSecondary,
            ThemeTokens.TextMuted
        };

        foreach (var theme in BuiltInThemes.All)
        {
            foreach (var surface in surfaces)
            {
                foreach (var text in texts)
                {
                    var ratio = ThemeColor.GetContrastRatio(
                        theme.GetColor(text),
                        theme.GetColor(surface));

                    // 4.5:1 is the AA floor for body text; muted captions sit
                    // at 11 px, which WCAG still treats as normal text.
                    Assert.True(
                        ratio >= 4.5,
                        $"{theme.Id}: {text} on {surface} is only {ratio:0.00}:1.");
                }
            }
        }
    }

    [Fact]
    public void TheLayerMapMatchesTheScreensItDescribes()
    {
        var root = FindRepositoryRoot();
        var shell = Read(root, "src", "Mimi.App", "MainWindow.xaml");

        // The window paints the darkest layer. The reference is dynamic on
        // purpose: a theme change has to reach the elements that are already
        // on screen, not only the ones created after it.
        Assert.Contains(
            "Background=\"{DynamicResource AppBackgroundBrush}\"",
            shell,
            StringComparison.Ordinal);

        // ...the navigation rail and every screen panel paint the panel layer...
        Assert.Contains(
            "<Border x:Name=\"NavRail\" Grid.Column=\"0\" Background=\"{DynamicResource PanelBackgroundBrush}\"",
            shell,
            StringComparison.Ordinal);

        // ...and raised content uses the card style, never a bare fill.
        Assert.Contains("MimiCardStyle", shell, StringComparison.Ordinal);
    }

    [Fact]
    public void EverySurfaceTokenIsActuallyPaintedSomewhere()
    {
        var root = FindRepositoryRoot();
        var app = Path.Combine(root, "src", "Mimi.App");
        var text = string.Join(
            "\n",
            Directory.EnumerateFiles(app, "*.xaml", SearchOption.AllDirectories)
                .Where(file => !file.EndsWith("ThemeBrushes.xaml", StringComparison.OrdinalIgnoreCase))
                .Select(File.ReadAllText));

        // A token nobody paints is a layer the theme promises and never shows,
        // which is how "theme only changes some places" starts.
        foreach (var token in new[]
                 {
                     ThemeTokens.AppBackground,
                     ThemeTokens.PanelBackground,
                     ThemeTokens.RaisedBackground,
                     ThemeTokens.SunkenBackground,
                     ThemeTokens.ControlBackground,
                     ThemeTokens.SelectedBackground,
                     ThemeTokens.HeaderBackground
                 })
        {
            Assert.Contains($"{token}Brush", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RaisedCardsAlwaysGetAnEdgeSoTheyDoNotReadAsFlat()
    {
        var root = FindRepositoryRoot();
        var styles = Read(root, "src", "Mimi.App", "Themes", "ControlStyles.xaml");
        var shell = Read(root, "src", "Mimi.App", "MainWindow.xaml");

        // A raised card is only ~1.09:1 away from the panel behind it, so the
        // card style must draw an edge; without one a card is invisible shape.
        var style = styles[styles.IndexOf("MimiCardStyle", StringComparison.Ordinal)..];
        Assert.Contains("TargetType=\"{x:Type Border}\"", style, StringComparison.Ordinal);
        Assert.Contains("RaisedBackgroundBrush", style, StringComparison.Ordinal);
        Assert.Contains("FieldBorderBrush", style, StringComparison.Ordinal);
        Assert.Contains("Property=\"BorderThickness\" Value=\"1\"", style, StringComparison.Ordinal);

        // And no section card may go back to a bare fill.
        var bareSectionCard = new Regex(
            "Background=\"\\{(?:Static|Dynamic)Resource RaisedBackgroundBrush\\}\"\\s+CornerRadius=\"12\"\\s+Padding=\"14\"",
            RegexOptions.CultureInvariant);
        Assert.DoesNotMatch(bareSectionCard, shell);
    }

    [Fact]
    public void NoBuiltInThemeIsReportedAsFlat()
    {
        foreach (var theme in BuiltInThemes.All)
        {
            var problems = ThemeLayerAudit.FindFlattenedLayers(theme);
            Assert.True(
                problems.Count == 0,
                $"{theme.Id}: {string.Join("; ", problems)}");
        }
    }

    [Fact]
    public void TheAuditCatchesTheTwoMistakesThatFlattenAUi()
    {
        // Every surface painted with the same colour: the failure mode this
        // whole file exists for.
        var flat = new ThemeDefinition(
            "flat-theme",
            "Flat",
            BuiltInThemes.Default.Colors.ToDictionary(
                pair => pair.Key,
                _ => "#101010",
                StringComparer.OrdinalIgnoreCase));

        var problems = ThemeLayerAudit.FindFlattenedLayers(flat);
        Assert.NotEmpty(problems);
        Assert.Contains(problems, problem => problem.Contains("card on panel", StringComparison.Ordinal));

        // A "sunken" surface that is lighter than its panel reads as raised.
        var colors = new Dictionary<string, string>(
            BuiltInThemes.Default.Colors,
            StringComparer.OrdinalIgnoreCase)
        {
            [ThemeTokens.SunkenBackground] = "#404040"
        };
        var upsideDown = new ThemeDefinition("upside-down", "Upside down", colors);

        Assert.Contains(
            ThemeLayerAudit.FindFlattenedLayers(upsideDown),
            problem => problem.Contains("not darker than the panel", StringComparison.Ordinal));
    }

    [Fact]
    public void CommunityThemesAreWarnedButNeverRejected()
    {
        var root = FindRepositoryRoot();
        var service = Read(root, "src", "Mimi.App", "Services", "ThemeService.cs");

        // The audit is advisory for community themes: a log line, not an
        // exception and not a fallback to the default palette.
        Assert.Contains("ThemeLayerAudit.FindFlattenedLayers", service, StringComparison.Ordinal);
        Assert.Contains("if (!ThemeCatalog.IsBuiltIn(effectiveTheme))", service, StringComparison.Ordinal);
        Assert.Contains("Log.Warning(", service, StringComparison.Ordinal);
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
