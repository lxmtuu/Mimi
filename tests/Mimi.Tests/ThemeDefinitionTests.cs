using Mimi.Theming;

namespace Mimi.Tests;

public sealed class ThemeDefinitionTests
{
    [Fact]
    public void BuiltInThemesExposeFourStableThemesWithNeonStageDefault()
    {
        Assert.Equal(
            [
                BuiltInThemes.NeonStageId,
                BuiltInThemes.SakuraDreamId,
                BuiltInThemes.MidnightLiveId,
                BuiltInThemes.MonoFocusId
            ],
            BuiltInThemes.All.Select(theme => theme.Id));
        Assert.Equal(BuiltInThemes.NeonStageId, BuiltInThemes.DefaultId);
        Assert.Equal(BuiltInThemes.NeonStageId, BuiltInThemes.Default.Id);
        Assert.Equal(8, global::Mimi.Models.AppSettings.CurrentVersion);
    }

    [Fact]
    public void EveryBuiltInThemeDefinesExactlyTheRequiredTokens()
    {
        var expected = ThemeTokens.RequiredColorTokens
            .OrderBy(token => token, StringComparer.Ordinal)
            .ToArray();

        foreach (var theme in BuiltInThemes.All)
        {
            Assert.True(
                theme.IsValid,
                $"{theme.Id}: {string.Join("; ", theme.Validate())}");
            Assert.Equal(
                expected,
                theme.Colors.Keys
                    .OrderBy(token => token, StringComparer.Ordinal)
                    .ToArray());
            Assert.Equal(0x59, theme.GetColor(ThemeTokens.StageOverlay).A);
        }
    }

    [Theory]
    [InlineData(null, BuiltInThemes.NeonStageId)]
    [InlineData("", BuiltInThemes.NeonStageId)]
    [InlineData("unknown-theme", BuiltInThemes.NeonStageId)]
    [InlineData(" neon-stage ", BuiltInThemes.NeonStageId)]
    [InlineData(" Sakura_Dream ", BuiltInThemes.SakuraDreamId)]
    [InlineData("MIDNIGHT LIVE", BuiltInThemes.MidnightLiveId)]
    [InlineData("MonoFocus", BuiltInThemes.MonoFocusId)]
    public void NormalizeIdFallsBackAndAcceptsFriendlySpellings(
        string? value,
        string expected)
    {
        Assert.Equal(expected, BuiltInThemes.NormalizeId(value));
        Assert.Equal(expected, BuiltInThemes.GetOrDefault(value).Id);
    }

    [Theory]
    [InlineData("#47E6D0", 255, 0x47, 0xE6, 0xD0)]
    [InlineData("#590B101B", 0x59, 0x0B, 0x10, 0x1B)]
    [InlineData(" #ff8fcb ", 255, 0xFF, 0x8F, 0xCB)]
    public void ThemeColorParsesRgbAndArgb(
        string value,
        int alpha,
        int red,
        int green,
        int blue)
    {
        var color = ThemeColor.Parse(value);

        Assert.Equal((byte)alpha, color.A);
        Assert.Equal((byte)red, color.R);
        Assert.Equal((byte)green, color.G);
        Assert.Equal((byte)blue, color.B);
        Assert.Equal(color, ThemeColor.Parse(color.ToHexString()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("47E6D0")]
    [InlineData("#47E6D")]
    [InlineData("#47E6D00")]
    [InlineData("#GGHHII")]
    public void ThemeColorRejectsMalformedValues(string? value)
    {
        Assert.False(ThemeColor.TryParse(value, out _));
        Assert.Throws<FormatException>(() => ThemeColor.Parse(value!));
    }

    [Fact]
    public void ValidationRejectsMissingUnknownAndMalformedTokens()
    {
        var colors = CopyColors(BuiltInThemes.Default);
        colors.Remove(ThemeTokens.Accent);
        colors[ThemeTokens.TextPrimary] = "white";
        colors["InventedToken"] = "#FFFFFF";

        var theme = new ThemeDefinition(
            "Bad Theme",
            "Bad theme",
            colors);
        var errors = theme.Validate();

        Assert.False(theme.IsValid);
        Assert.Contains(errors, error => error.Contains("theme id", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains(ThemeTokens.Accent, StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains(ThemeTokens.TextPrimary, StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("InventedToken", StringComparison.Ordinal));
        Assert.Throws<KeyNotFoundException>(
            () => theme.GetColor(ThemeTokens.Accent));
    }

    [Fact]
    public void BuiltInThemesMeetAaContrastForKeyTextAndSurfacePairs()
    {
        string[] textTokens =
        [
            ThemeTokens.TextPrimary,
            ThemeTokens.TextStrong,
            ThemeTokens.TextSecondary,
            ThemeTokens.TextMuted,
            ThemeTokens.TextFaint,
            ThemeTokens.TextDisabled,
            ThemeTokens.Accent,
            ThemeTokens.AccentSecondary
        ];
        string[] mainSurfaces =
        [
            ThemeTokens.AppBackground,
            ThemeTokens.PanelBackground,
            ThemeTokens.RaisedBackground
        ];
        string[] primaryTextSurfaces =
        [
            ThemeTokens.SunkenBackground,
            ThemeTokens.ControlBackground,
            ThemeTokens.SelectedBackground,
            ThemeTokens.HeaderBackground
        ];

        foreach (var theme in BuiltInThemes.All)
        {
            foreach (var foreground in textTokens)
            {
                foreach (var background in mainSurfaces)
                    AssertContrast(theme, foreground, background);
            }

            foreach (var background in primaryTextSurfaces)
                AssertContrast(theme, ThemeTokens.TextPrimary, background);

            AssertContrast(theme, ThemeTokens.OnAccent, ThemeTokens.Accent);
            AssertContrast(
                theme,
                ThemeTokens.WarningText,
                ThemeTokens.WarningBackground);
        }
    }

    private static Dictionary<string, string> CopyColors(
        ThemeDefinition theme) =>
        theme.Colors.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.OrdinalIgnoreCase);

    private static void AssertContrast(
        ThemeDefinition theme,
        string foregroundToken,
        string backgroundToken)
    {
        var ratio = ThemeColor.GetContrastRatio(
            theme.GetColor(foregroundToken),
            theme.GetColor(backgroundToken));

        Assert.True(
            ratio >= 4.5,
            $"{theme.Id}: {foregroundToken} on {backgroundToken} is " +
            $"{ratio:0.00}:1, below WCAG AA 4.5:1.");
    }
}
