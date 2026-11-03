using Mimi.Theming;

namespace Mimi.Tests;

public sealed class ThemeCatalogTests
{
    [Fact]
    public void BuiltInsOnly_ContainsFourThemesAndNormalizesFriendlySpellings()
    {
        var catalog = ThemeCatalog.BuiltInsOnly;

        Assert.Equal(4, catalog.All.Count);
        Assert.Empty(catalog.CommunityThemes);
        Assert.Empty(catalog.Warnings);
        Assert.Equal(
            BuiltInThemes.SakuraDreamId,
            catalog.NormalizeId("Sakura Dream"));
        Assert.Equal(
            BuiltInThemes.DefaultId,
            catalog.NormalizeId("no-such-theme"));
        Assert.Equal(
            BuiltInThemes.DefaultId,
            catalog.NormalizeId(null));
    }

    [Fact]
    public void CommunityTheme_IsAppendedAfterBuiltInsAndResolvable()
    {
        var catalog = new ThemeCatalog(
        [
            CreateTheme("alpha-one", "Alpha One")
        ]);

        Assert.Equal(5, catalog.All.Count);
        Assert.Equal("alpha-one", catalog.All[^1].Id);
        Assert.Single(catalog.CommunityThemes);
        Assert.Empty(catalog.Warnings);
        Assert.True(catalog.TryGet("AlphaOne", out var theme));
        Assert.Equal("alpha-one", theme.Id);
        Assert.Equal("alpha-one", catalog.NormalizeId("alpha_one"));
    }

    [Fact]
    public void CommunityTheme_CannotShadowBuiltInId()
    {
        var catalog = new ThemeCatalog(
        [
            CreateTheme(BuiltInThemes.NeonStageId, "Fake Neon", "#000000")
        ]);

        Assert.Equal(4, catalog.All.Count);
        Assert.Empty(catalog.CommunityThemes);
        Assert.NotEmpty(catalog.Warnings);
        Assert.Equal(
            "Neon Stage",
            catalog.GetOrDefault(BuiltInThemes.NeonStageId).Name);
    }

    [Fact]
    public void DuplicateCommunityIds_FirstOneWins()
    {
        var catalog = new ThemeCatalog(
        [
            CreateTheme("alpha-one", "First"),
            CreateTheme("alpha-one", "Second")
        ]);

        Assert.Equal(5, catalog.All.Count);
        var theme = Assert.Single(catalog.CommunityThemes);
        Assert.Equal("First", theme.Name);
        Assert.NotEmpty(catalog.Warnings);
    }

    [Fact]
    public void InvalidCommunityTheme_IsSkippedWithWarning()
    {
        var invalid = new ThemeDefinition(
            "broken-one",
            "Broken One",
            new Dictionary<string, string>
            {
                [ThemeTokens.Accent] = "#000000"
            });

        var catalog = new ThemeCatalog([invalid]);

        Assert.Equal(4, catalog.All.Count);
        Assert.Empty(catalog.CommunityThemes);
        Assert.NotEmpty(catalog.Warnings);
    }

    [Fact]
    public void IsBuiltIn_DistinguishesBuiltInFromCommunity()
    {
        Assert.True(ThemeCatalog.IsBuiltIn(BuiltInThemes.Default));
        Assert.False(ThemeCatalog.IsBuiltIn(
            CreateTheme("alpha-one", "Alpha One")));
    }

    private static ThemeDefinition CreateTheme(
        string id,
        string name,
        string accent = "#123456")
    {
        var colors = new Dictionary<string, string>(
            BuiltInThemes.Default.Colors)
        {
            [ThemeTokens.Accent] = accent
        };
        return new ThemeDefinition(id, name, colors);
    }
}
