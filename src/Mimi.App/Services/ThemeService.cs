using System.IO;
using System.Windows;
using System.Windows.Media;
using Mimi.Theming;
using Serilog;

namespace Mimi.Services;

/// <summary>
/// Publishes platform-neutral theme colors into the WPF application
/// resource dictionary. Shared brushes in ThemeBrushes.xaml bind to these
/// colors dynamically, so changing a theme updates open windows without a
/// restart. In addition to the built-in themes, the service loads
/// community *.json themes from the per-user themes folder; invalid files
/// are skipped and logged, never fatal.
/// </summary>
public sealed class ThemeService
{
    public ThemeService(IUserDataService? userDataService = null)
    {
        ThemesDirectory = userDataService is null
            ? null
            : Path.Combine(userDataService.DataDirectory, "themes");
        Catalog = LoadCatalog();
    }

    public event EventHandler? ThemeChanged;

    /// <summary>
    /// Per-user folder scanned for community themes, or null when the
    /// service was created without user-data access (design-time/tests).
    /// </summary>
    public string? ThemesDirectory { get; }

    public ThemeCatalog Catalog { get; private set; }

    public IReadOnlyList<ThemeDefinition> AvailableThemes => Catalog.All;

    public string CurrentThemeId { get; private set; } =
        BuiltInThemes.DefaultId;

    public ThemeDefinition CurrentTheme =>
        Catalog.GetOrDefault(CurrentThemeId);

    public string Apply(string? themeId)
    {
        var theme = Catalog.GetOrDefault(themeId);
        CurrentThemeId = theme.Id;

        if (Application.Current?.Resources is { } resources)
            Apply(theme, resources);

        ThemeChanged?.Invoke(this, EventArgs.Empty);
        return theme.Id;
    }

    public void ApplyDefault() => Apply(BuiltInThemes.DefaultId);

    public string NormalizeId(string? themeId) => Catalog.NormalizeId(themeId);

    /// <summary>
    /// Re-scans the themes folder, then re-applies the current theme so an
    /// edited community theme refreshes live and a removed one falls back
    /// to the default theme.
    /// </summary>
    public void ReloadCommunityThemes()
    {
        Catalog = LoadCatalog();
        Apply(CurrentThemeId);
    }

    private ThemeCatalog LoadCatalog()
    {
        if (ThemesDirectory is null)
            return ThemeCatalog.BuiltInsOnly;

        try
        {
            // Create the folder eagerly so "Open themes folder" always has
            // somewhere real to show, even before the first custom theme.
            Directory.CreateDirectory(ThemesDirectory);
            var results = CommunityThemeLoader.LoadDirectory(ThemesDirectory);

            var themes = new List<ThemeDefinition>();
            foreach (var result in results)
            {
                if (result.Theme is { } theme)
                {
                    themes.Add(theme);
                }
                else
                {
                    Log.Warning(
                        "Skipped community theme file {ThemeFile}: {Errors}",
                        result.Source,
                        string.Join("; ", result.Errors));
                }
            }

            var catalog = new ThemeCatalog(themes);
            foreach (var warning in catalog.Warnings)
                Log.Warning("Community themes: {Warning}", warning);

            if (catalog.CommunityThemes.Count > 0)
            {
                Log.Information(
                    "Loaded {CommunityThemeCount} community theme(s) from {ThemesDirectory}",
                    catalog.CommunityThemes.Count,
                    ThemesDirectory);
            }

            return catalog;
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            Log.Warning(
                ex,
                "Could not load community themes from {ThemesDirectory}; using built-in themes only",
                ThemesDirectory);
            return ThemeCatalog.BuiltInsOnly;
        }
    }

    public static void Apply(
        ThemeDefinition theme,
        ResourceDictionary resources)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(resources);

        var effectiveTheme = theme;
        var errors = theme.Validate();
        if (errors.Count > 0)
        {
            Log.Warning(
                "Theme {ThemeId} is invalid ({Errors}); using {DefaultThemeId}",
                theme.Id,
                string.Join("; ", errors),
                BuiltInThemes.DefaultId);
            effectiveTheme = BuiltInThemes.Default;
        }

        var publishedColors =
            new Dictionary<string, Color>(StringComparer.Ordinal);
        foreach (var token in ThemeTokens.RequiredColorTokens)
        {
            publishedColors[ColorResourceKey(token)] = ToColor(
                effectiveTheme.GetColor(token));
        }

        var accent = effectiveTheme.GetColor(ThemeTokens.Accent);
        publishedColors["AccentDecorSoftColor"] = ToColor(
            accent.WithAlpha(0x22));
        publishedColors["AccentDecorMediumColor"] = ToColor(
            accent.WithAlpha(0x66));
        publishedColors["AccentDecorStrongColor"] = ToColor(
            accent.WithAlpha(0x88));
        publishedColors["AccentDecorVividColor"] = ToColor(
            accent.WithAlpha(0xAA));
        publishedColors["AccentTransparentColor"] = ToColor(
            accent.WithAlpha(0x00));

        // Lighting gradients use the secondary/hot accents as colored
        // light sources. Publishing their alpha variants (and matching
        // transparent hues) keeps those gradients theme-derived instead
        // of hard-coding cyan/pink into XAML, so community themes light
        // up in their own palette too.
        var accentSecondary = effectiveTheme.GetColor(
            ThemeTokens.AccentSecondary);
        publishedColors["AccentSecondaryDecorSoftColor"] = ToColor(
            accentSecondary.WithAlpha(0x22));
        publishedColors["AccentSecondaryDecorMediumColor"] = ToColor(
            accentSecondary.WithAlpha(0x66));
        publishedColors["AccentSecondaryTransparentColor"] = ToColor(
            accentSecondary.WithAlpha(0x00));

        var accentHot = effectiveTheme.GetColor(ThemeTokens.AccentHot);
        publishedColors["AccentHotDecorSoftColor"] = ToColor(
            accentHot.WithAlpha(0x22));
        publishedColors["AccentHotDecorMediumColor"] = ToColor(
            accentHot.WithAlpha(0x66));
        publishedColors["AccentHotTransparentColor"] = ToColor(
            accentHot.WithAlpha(0x00));

        var appBackground = effectiveTheme.GetColor(
            ThemeTokens.AppBackground);
        publishedColors["AppBackgroundTransparentColor"] = ToColor(
            appBackground.WithAlpha(0x00));

        var textPrimary = effectiveTheme.GetColor(
            ThemeTokens.TextPrimary);
        publishedColors["TextPrimaryDecorSoftColor"] = ToColor(
            textPrimary.WithAlpha(0x2A));
        publishedColors["TextPrimaryTransparentColor"] = ToColor(
            textPrimary.WithAlpha(0x00));

        // Publish into merged dictionaries as well as the root. WPF
        // resolves DynamicResource values owned by a merged dictionary in
        // that dictionary's context, while element-level references start
        // from the application root; keeping both in sync makes the live
        // brush update independent of that lookup detail.
        foreach (var dictionary in EnumerateDictionaries(resources))
        {
            // Sealed style/template dictionaries cannot be written, but
            // the application dictionary and ThemeBrushes are writable.
            if (dictionary.IsReadOnly) continue;

            foreach (var (key, color) in publishedColors)
                dictionary[key] = color;

            dictionary["MimiThemeId"] = effectiveTheme.Id;
            dictionary["MimiThemeName"] = effectiveTheme.Name;
        }
    }

    public static string ColorResourceKey(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return $"{token}Color";
    }

    public static string BrushResourceKey(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return $"{token}Brush";
    }

    private static IEnumerable<ResourceDictionary> EnumerateDictionaries(
        ResourceDictionary root)
    {
        var visited = new HashSet<ResourceDictionary>(
            ReferenceEqualityComparer.Instance);
        var pending = new Stack<ResourceDictionary>();
        pending.Push(root);

        while (pending.TryPop(out var dictionary))
        {
            if (!visited.Add(dictionary)) continue;
            yield return dictionary;

            foreach (var merged in dictionary.MergedDictionaries)
                pending.Push(merged);
        }
    }

    private static Color ToColor(ThemeColor color) =>
        Color.FromArgb(color.A, color.R, color.G, color.B);
}
