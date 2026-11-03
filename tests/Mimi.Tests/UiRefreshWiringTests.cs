using System.Xml.Linq;
using Mimi.Theming;

namespace Mimi.Tests;

/// <summary>
/// Guards the UI refresh: list rows, editors and menus must draw their own
/// states from theme brushes, because the stock WPF containers paint hover
/// with a light-theme brush and turned every dark list white under the
/// pointer. The default theme must stay dark with the original azure accent.
/// </summary>
public sealed class UiRefreshWiringTests
{
    [Fact]
    public void EveryListRowDrawsItsOwnHoverAndSelectionStates()
    {
        var root = FindRepositoryRoot();
        var styles = Read(root, "src", "Mimi.App", "Themes", "ControlStyles.xaml");

        // One themed container per list shape: plain items, data-templates,
        // and the library table that keeps GridView columns.
        Assert.Contains("x:Key=\"MimiRowItemStyle\"", styles, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"MimiListViewItemStyle\"", styles, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"MimiGridViewItemStyle\"", styles, StringComparison.Ordinal);
        Assert.Contains(
            "<GridViewRowPresenter",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "Columns=\"{TemplateBinding GridView.ColumnCollection}\"",
            styles,
            StringComparison.Ordinal);

        // Implicit styles make every list follow automatically.
        Assert.Contains(
            "<Style TargetType=\"{x:Type ListBoxItem}\" BasedOn=\"{StaticResource MimiRowItemStyle}\"/>",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "<Style TargetType=\"{x:Type ListViewItem}\" BasedOn=\"{StaticResource MimiListViewItemStyle}\"/>",
            styles,
            StringComparison.Ordinal);

        // Hover uses a theme-derived plate, never a light system brush.
        var hoverBlock = styles[styles.IndexOf("MimiRowItemStyle", StringComparison.Ordinal)..];
        Assert.Contains("HoverSurfaceBrush", hoverBlock, StringComparison.Ordinal);
        Assert.Contains("HoverBorderBrush", hoverBlock, StringComparison.Ordinal);
        Assert.Contains("SelectedBorderBrush", hoverBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void EditorsAndMenusAreTemplatedFromThemeBrushes()
    {
        var root = FindRepositoryRoot();
        var styles = Read(root, "src", "Mimi.App", "Themes", "ControlStyles.xaml");

        // TextBox / CheckBox / menus / tooltips were the remaining stock
        // light controls inside the dark shell.
        foreach (var snippet in new[]
                 {
                     "<Style TargetType=\"{x:Type TextBox}\">",
                     "PART_ContentHost",
                     "<Style TargetType=\"{x:Type CheckBox}\">",
                     "<Style TargetType=\"{x:Type ContextMenu}\">",
                     "<Style TargetType=\"{x:Type MenuItem}\">",
                     "<Style TargetType=\"{x:Type ToolTip}\">"
                 })
        {
            Assert.Contains(snippet, styles, StringComparison.Ordinal);
        }

        var brushes = Read(root, "src", "Mimi.App", "Themes", "ThemeBrushes.xaml");
        foreach (var brush in new[]
                 {
                     "HoverSurfaceBrush",
                     "HoverSurfaceStrongBrush",
                     "HoverBorderBrush",
                     "SelectedBorderBrush",
                     "FieldBorderBrush",
                     "MenuSurfaceBrush",
                     "ToolTipSurfaceBrush"
                 })
        {
            Assert.Contains($"x:Key=\"{brush}\"", brushes, StringComparison.Ordinal);
        }

        // The application dictionary must not shadow the templated styles it
        // merges: a plain implicit TextBox/CheckBox style there would win.
        var app = Read(root, "src", "Mimi.App", "App.xaml");
        Assert.DoesNotContain("<Style TargetType=\"TextBox\">", app, StringComparison.Ordinal);
        Assert.DoesNotContain("<Style TargetType=\"CheckBox\">", app, StringComparison.Ordinal);
        Assert.DoesNotContain("<Style TargetType=\"ContextMenu\">", app, StringComparison.Ordinal);
        Assert.DoesNotContain("<Style TargetType=\"MenuItem\">", app, StringComparison.Ordinal);
        // Buttons keep the accent wash under the label, not over it.
        Assert.Contains("HoverWash", app, StringComparison.Ordinal);
        Assert.Contains("PressWash", app, StringComparison.Ordinal);
    }

    [Fact]
    public void EachOnlineListReusesTheThemedRowInsteadOfTheStockTemplate()
    {
        var root = FindRepositoryRoot();
        var xaml = Read(root, "src", "Mimi.App", "MainWindow.xaml");

        Assert.Contains(
            "<Style TargetType=\"{x:Type ListViewItem}\" BasedOn=\"{StaticResource MimiGridViewItemStyle}\">",
            xaml,
            StringComparison.Ordinal);
        Assert.Contains(
            "<Style TargetType=\"{x:Type ListViewItem}\" BasedOn=\"{StaticResource MimiListViewItemStyle}\">",
            xaml,
            StringComparison.Ordinal);
        // Library table, queue, playlists, radio, online, YouTube, watched
        // folders and lyrics all answer a pointer now.
        Assert.Equal(
            8,
            CountOccurrences(
                xaml,
                "BasedOn=\"{StaticResource MimiRowItemStyle}\"") +
            CountOccurrences(
                xaml,
                "BasedOn=\"{StaticResource MimiListViewItemStyle}\"") +
            CountOccurrences(
                xaml,
                "BasedOn=\"{StaticResource MimiGridViewItemStyle}\""));
    }

    [Fact]
    public void TheShellAndEveryScreenStillParseAsXml()
    {
        var root = FindRepositoryRoot();

        string[][] files =
        [
            ["src", "Mimi.App", "App.xaml"],
            ["src", "Mimi.App", "MainWindow.xaml"],
            ["src", "Mimi.App", "StageWindow.xaml"],
            ["src", "Mimi.App", "TextPromptWindow.xaml"],
            ["src", "Mimi.App", "Themes", "ControlStyles.xaml"],
            ["src", "Mimi.App", "Themes", "ThemeBrushes.xaml"]
        ];

        foreach (var file in files)
        {
            var document = XDocument.Parse(Read(root, file));
            Assert.NotNull(document.Root);
        }
    }

    [Fact]
    public void TheDefaultThemeIsDarkNavyWithTheOriginalAzureAccent()
    {
        var theme = BuiltInThemes.Default;

        Assert.Equal(BuiltInThemes.NeonStageId, theme.Id);
        Assert.Equal("#3E8BFF", theme.Colors[ThemeTokens.Accent]);
        Assert.Equal("#4FE3FF", theme.Colors[ThemeTokens.AccentSecondary]);

        // Dark: the page background must stay near-black navy so the blue
        // accent and the white text both have contrast to work with.
        var background = theme.GetColor(ThemeTokens.AppBackground);
        Assert.True(background.R < 0x20 && background.G < 0x20 && background.B < 0x30);

        // The accent must read as blue, not teal: blue clearly dominant.
        var accent = theme.GetColor(ThemeTokens.Accent);
        Assert.True(accent.B > accent.R + 0x40);
        Assert.True(accent.B > accent.G + 0x20);
    }

    [Fact]
    public void NoSurfaceKeepsAHardCodedColour()
    {
        var root = FindRepositoryRoot();
        var appRoot = Path.Combine(root, "src", "Mimi.App");
        var hex = new System.Text.RegularExpressions.Regex(
            "#[0-9A-Fa-f]{6}",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        // ThemeBrushes.xaml is the one place a colour literal is allowed: it is
        // the layer that turns theme tokens into brushes. Any other screen or
        // style that hard-codes a colour stops following the theme, which is
        // exactly the "only some places change" complaint this guards.
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(
                     appRoot,
                     "*.xaml",
                     SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(file);
            if (string.Equals(name, "ThemeBrushes.xaml", StringComparison.OrdinalIgnoreCase))
                continue;

            if (hex.IsMatch(File.ReadAllText(file)))
                offenders.Add(Path.GetRelativePath(root, file));
        }

        Assert.Empty(offenders);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
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
