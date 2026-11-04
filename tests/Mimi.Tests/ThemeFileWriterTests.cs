using System.IO;
using Mimi.Theming;

namespace Mimi.Tests;

/// <summary>
/// Guards "duplicate for editing": the copy written to the themes folder has to
/// be a valid community theme the loader accepts, otherwise the user edits a
/// file Mimi silently ignores. This is how the pink theme becomes theirs to
/// change instead of a fixed built-in.
/// </summary>
public sealed class ThemeFileWriterTests
{
    [Theory]
    [InlineData(BuiltInThemes.NeonStageId)]
    [InlineData(BuiltInThemes.SakuraDreamId)]
    [InlineData(BuiltInThemes.MidnightLiveId)]
    [InlineData(BuiltInThemes.MonoFocusId)]
    public void EveryBuiltInThemeRoundTripsThroughTheWriter(string themeId)
    {
        var theme = BuiltInThemes.GetOrDefault(themeId);
        var catalog = ThemeCatalog.BuiltInsOnly;
        var id = ThemeFileWriter.UniqueId(theme, catalog);

        var json = ThemeFileWriter.ToJson(theme, id, "Copy");
        var result = CommunityThemeLoader.Parse(json, $"{id}.json");

        Assert.True(result.Success, string.Join("; ", result.Errors));
        var copy = Assert.IsType<ThemeDefinition>(result.Theme);
        Assert.Equal(id, copy.Id);
        Assert.Equal("Copy", copy.Name);

        // Every colour survives the round trip byte for byte.
        foreach (var token in ThemeTokens.RequiredColorTokens)
            Assert.Equal(theme.GetColor(token), copy.GetColor(token));

        Assert.Empty(copy.Validate());
    }

    [Fact]
    public void TheCopyGetsItsOwnIdSoTheBuiltInStaysUntouched()
    {
        var sakura = BuiltInThemes.GetOrDefault(BuiltInThemes.SakuraDreamId);

        var first = ThemeFileWriter.UniqueId(sakura, ThemeCatalog.BuiltInsOnly);
        Assert.Equal("sakura-dream-custom", first);

        // A second copy must not overwrite the first one.
        var withCopy = new ThemeCatalog(
        [
            new ThemeDefinition(
                first,
                "Copy",
                BuiltInThemes.Default.Colors.ToDictionary(pair => pair.Key, pair => pair.Value))
        ]);
        Assert.Equal("sakura-dream-custom-2", ThemeFileWriter.UniqueId(sakura, withCopy));
    }

    [Fact]
    public void AnAwkwardIdStillProducesAValidSlug()
    {
        var weird = new ThemeDefinition(
            "Pink!",
            "Pink theme",
            BuiltInThemes.Default.Colors.ToDictionary(pair => pair.Key, pair => pair.Value));

        var id = ThemeFileWriter.UniqueId(weird, ThemeCatalog.BuiltInsOnly);

        Assert.True(ThemeDefinition.IsValidId(id), id);
        Assert.Equal("pink-custom", id);
    }

    [Fact]
    public void TheWrittenFileIsReadableAsAJsonThemeFile()
    {
        var theme = BuiltInThemes.GetOrDefault(BuiltInThemes.SakuraDreamId);
        var json = ThemeFileWriter.ToJson(theme, "sakura-dream-custom", "Sakura · custom");

        using var document = System.Text.Json.JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("sakura-dream-custom", root.GetProperty("id").GetString());
        Assert.Equal(
            ThemeTokens.RequiredColorTokens.Count,
            root.GetProperty("colors").EnumerateObject().Count());
    }

    [Fact]
    public void BuiltInThemesKeepTheirOwnPaletteWhenOthersAreApplied()
    {
        // The owner's report: switching to the blue theme still showed pink,
        // because two palettes were live at once. Every built-in must publish
        // its whole palette, not just its accent.
        foreach (var theme in BuiltInThemes.All)
        {
            var resources = new System.Windows.ResourceDictionary();
            Mimi.Services.ThemeService.Apply(theme, resources);

            foreach (var token in new[]
                     {
                         ThemeTokens.AppBackground,
                         ThemeTokens.PanelBackground,
                         ThemeTokens.RaisedBackground
                     })
            {
                var expected = theme.GetColor(token);
                var actual = Assert.IsType<System.Windows.Media.Color>(
                    resources[Mimi.Services.ThemeService.ColorResourceKey(token)]);
                Assert.Equal(expected.R, actual.R);
                Assert.Equal(expected.G, actual.G);
                Assert.Equal(expected.B, actual.B);
            }
        }
    }

    [Fact]
    public void TheThemesFolderCopyIsWrittenWhereTheAppLooksForIt()
    {
        var root = FindRepositoryRoot();
        var settings = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Mimi.App",
            "ViewModels",
            "MainWindowViewModel.Settings.cs"));
        var shell = File.ReadAllText(Path.Combine(root, "src", "Mimi.App", "MainWindow.xaml"));

        // The command must write into the scanned folder and reload the list.
        Assert.Contains("Path.Combine(ThemesDirectory, $\"{id}.json\")", settings, StringComparison.Ordinal);
        Assert.Contains("ThemeFileWriter.UniqueId(source, themeService.Catalog)", settings, StringComparison.Ordinal);
        Assert.Contains("themeService.ReloadCommunityThemes();", settings, StringComparison.Ordinal);
        Assert.Contains("DuplicateThemeForEditingCommand", shell, StringComparison.Ordinal);
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
