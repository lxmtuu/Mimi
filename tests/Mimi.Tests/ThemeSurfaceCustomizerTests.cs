using System.IO;
using Mimi.Models;
using Mimi.Theming;

namespace Mimi.Tests;

/// <summary>
/// Guards the custom background colour. The promise made in Settings is narrow
/// and has to hold for every theme: the tint changes the hue of the nine
/// background layers and nothing else - not their depth, not the contrast
/// against text, not the accents. These checks are what make that true by
/// construction rather than by eye.
/// </summary>
public sealed class ThemeSurfaceCustomizerTests
{
    private static readonly string[] Tints =
    [
        "#3E8BFF", "#FF8FCB", "#B07CFF", "#4FD1A5", "#FFB020", "#4FE3FF"
    ];

    [Fact]
    public void ATintKeepsEveryLayerAtItsOwnDepth()
    {
        foreach (var theme in BuiltInThemes.All)
        {
            foreach (var tint in Tints)
            {
                Assert.True(ThemeSurfaceCustomizer.TryParse(tint, out var color));
                var tinted = ThemeSurfaceCustomizer.Apply(theme, color);

                foreach (var token in ThemeSurfaceCustomizer.SurfaceTokens)
                {
                    var before = theme.GetColor(token).RelativeLuminance;
                    var after = tinted.GetColor(token).RelativeLuminance;

                    // The layer stays where it was. The allowance is a couple of
                    // 8-bit steps - at these luminances one step in a channel is
                    // several percent of the value - and the visible steps are
                    // asserted properly in the check below.
                    var allowance = Math.Max(before * 0.20, 0.002);
                    Assert.True(
                        Math.Abs(after - before) <= allowance,
                        $"{theme.Id} + {tint}: {token} moved from luminance " +
                        $"{before:0.0000} to {after:0.0000}.");
                }

                // Whatever rounding does, the stack still reads as a stack.
                Assert.Empty(ThemeLayerAudit.FindFlattenedLayers(tinted));
            }
        }
    }

    [Fact]
    public void ATintNeverBreaksTextContrast()
    {
        var texts = new[]
        {
            ThemeTokens.TextPrimary,
            ThemeTokens.TextStrong,
            ThemeTokens.TextSecondary,
            ThemeTokens.TextMuted
        };

        foreach (var theme in BuiltInThemes.All)
        {
            foreach (var tint in Tints)
            {
                ThemeSurfaceCustomizer.TryParse(tint, out var color);
                var tinted = ThemeSurfaceCustomizer.Apply(theme, color);

                foreach (var surface in ThemeSurfaceCustomizer.SurfaceTokens)
                {
                    foreach (var text in texts)
                    {
                        var ratio = ThemeColor.GetContrastRatio(
                            tinted.GetColor(text),
                            tinted.GetColor(surface));

                        Assert.True(
                            ratio >= 4.5,
                            $"{theme.Id} + {tint}: {text} on {surface} is only {ratio:0.00}:1.");
                    }
                }
            }
        }
    }

    [Fact]
    public void ATintKeepsTheThemeValidAndKeepsItsIdentity()
    {
        foreach (var theme in BuiltInThemes.All)
        {
            foreach (var tint in Tints)
            {
                ThemeSurfaceCustomizer.TryParse(tint, out var color);
                var tinted = ThemeSurfaceCustomizer.Apply(theme, color);

                Assert.Empty(tinted.Validate());
                Assert.Equal(theme.Id, tinted.Id);
                Assert.Equal(theme.Name, tinted.Name);

                // Accents are the theme's own: a background choice is not a
                // licence to repaint the whole palette.
                Assert.Equal(
                    theme.GetColor(ThemeTokens.Accent),
                    tinted.GetColor(ThemeTokens.Accent));
                Assert.Equal(
                    theme.GetColor(ThemeTokens.TextPrimary),
                    tinted.GetColor(ThemeTokens.TextPrimary));
            }
        }
    }

    [Fact]
    public void TheChosenHueActuallyReachesTheSurfaces()
    {
        var pink = ThemeColor.Parse("#FF8FCB");
        var theme = BuiltInThemes.Default;
        var tinted = ThemeSurfaceCustomizer.Apply(theme, pink);

        var panel = tinted.GetColor(ThemeTokens.PanelBackground);

        // Not the shipped navy any more...
        Assert.NotEqual(
            theme.GetColor(ThemeTokens.PanelBackground).ToHexString(),
            panel.ToHexString());

        // ...and it reads as the hue that was picked rather than as grey: red
        // must be the strongest channel for this pink.
        Assert.True(
            panel.R > panel.B && panel.R > panel.G,
            $"Expected a pink-leaning panel, got {panel.ToHexString()}.");

        // The stack order survives the recolour.
        Assert.True(
            tinted.GetColor(ThemeTokens.SunkenBackground).RelativeLuminance <
            panel.RelativeLuminance);
        Assert.True(
            tinted.GetColor(ThemeTokens.RaisedBackground).RelativeLuminance >
            panel.RelativeLuminance);
    }

    [Fact]
    public void AStrongTintIsMutedEnoughToLeaveRoomForTheAccents()
    {
        // Full saturation at these luminances produces a neon-black that fights
        // the accent colour, so the customiser caps it.
        ThemeSurfaceCustomizer.TryParse("#FF00AA", out var parsed);
        var tinted = ThemeSurfaceCustomizer.Apply(BuiltInThemes.Default, parsed);

        var panel = tinted.GetColor(ThemeTokens.PanelBackground);
        var max = Math.Max(panel.R, Math.Max(panel.G, panel.B));
        var min = Math.Min(panel.R, Math.Min(panel.G, panel.B));

        Assert.True(
            max - min < 0x40,
            $"The tint is louder than the cap allows: {panel.ToHexString()}.");
    }

    [Theory]
    [InlineData("#FF8FCB", "#FF8FCB")]
    [InlineData("ff8fcb", "#FF8FCB")]
    [InlineData("  #ff8fCB ", "#FF8FCB")]
    // Eight digits are #AARRGGBB - the alpha is dropped, the colour stays.
    [InlineData("#80FF8FCB", "#FF8FCB")]
    public void ParsingAcceptsWhatAPersonWouldType(string input, string expected)
    {
        Assert.True(ThemeSurfaceCustomizer.TryParse(input, out var color));
        Assert.Equal(expected, ThemeSurfaceCustomizer.ToDisplayString(color));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("pink")]
    [InlineData("#FF8FC")]
    [InlineData("#GGGGGG")]
    public void ParsingRejectsAnythingElse(string input) =>
        Assert.False(ThemeSurfaceCustomizer.TryParse(input, out _));

    [Fact]
    public void SettingsKeepTheTintOnlyWhenItIsReadable()
    {
        var valid = new AppSettings
        {
            CustomBackgroundEnabled = true,
            CustomBackground = "ff8fcb"
        };
        valid.Normalize();

        Assert.True(valid.CustomBackgroundEnabled);
        Assert.Equal("#FF8FCB", valid.CustomBackground);

        var garbage = new AppSettings
        {
            CustomBackgroundEnabled = true,
            CustomBackground = "not-a-colour"
        };
        garbage.Normalize();

        // An unreadable value turns the feature off instead of leaving a
        // half-applied palette behind.
        Assert.False(garbage.CustomBackgroundEnabled);
        Assert.Equal(string.Empty, garbage.CustomBackground);
    }

    [Fact]
    public void TheSettingsScreenExposesTheTintEndToEnd()
    {
        var root = FindRepositoryRoot();
        var shell = File.ReadAllText(Path.Combine(root, "src", "Mimi.App", "MainWindow.xaml"));
        var settings = File.ReadAllText(Path.Combine(
            root, "src", "Mimi.App", "ViewModels", "MainWindowViewModel.Settings.cs"));
        var service = File.ReadAllText(Path.Combine(
            root, "src", "Mimi.App", "Services", "ThemeService.cs"));

        Assert.Contains("IsCustomBackgroundEnabled", shell, StringComparison.Ordinal);
        Assert.Contains("CustomBackgroundHex", shell, StringComparison.Ordinal);
        Assert.Contains("SelectCustomBackgroundCommand", shell, StringComparison.Ordinal);
        Assert.Contains("ClearCustomBackgroundCommand", shell, StringComparison.Ordinal);

        // Persisted, restored before the first theme is applied, and handed to
        // the service that paints the palette.
        Assert.Contains("settings.CustomBackgroundEnabled", settings, StringComparison.Ordinal);
        Assert.Contains("settings.CustomBackground =", settings, StringComparison.Ordinal);
        Assert.Contains("themeService.CustomBackground =", settings, StringComparison.Ordinal);
        Assert.Contains("public ThemeColor? CustomBackground { get; set; }", service, StringComparison.Ordinal);
        Assert.Contains("ThemeSurfaceCustomizer.Apply(theme, background)", service, StringComparison.Ordinal);
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
