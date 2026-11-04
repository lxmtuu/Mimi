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

        // A community theme is not rejected for a flat layer - the author's
        // taste is theirs - but the log should say why a card vanished into
        // its panel, because that is impossible to see in a colour picker.
        if (!ThemeCatalog.IsBuiltIn(effectiveTheme))
        {
            foreach (var problem in ThemeLayerAudit.FindFlattenedLayers(effectiveTheme))
            {
                Log.Warning(
                    "Theme {ThemeId} has a flat background layer: {Problem}",
                    effectiveTheme.Id,
                    problem);
            }
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

        // Interaction surfaces — row hover, row selection outline, field
        // borders, menu and tooltip plates — are mixed from the theme's own
        // panel colour and accent instead of relying on the WPF system
        // highlight, which is a light-theme brush. Without this, lists and
        // editors flashed white on hover and ignored the selected theme.
        var panelBackground = effectiveTheme.GetColor(
            ThemeTokens.PanelBackground);
        var selectedBackground = effectiveTheme.GetColor(
            ThemeTokens.SelectedBackground);
        publishedColors["HoverSurfaceColor"] = ToColor(
            Mix(panelBackground, accent, 0.14));
        publishedColors["HoverSurfaceStrongColor"] = ToColor(
            Mix(panelBackground, accent, 0.26));
        publishedColors["HoverBorderColor"] = ToColor(
            Mix(panelBackground, accent, 0.38));
        publishedColors["SelectedBorderColor"] = ToColor(
            Mix(selectedBackground, accent, 0.42));
        publishedColors["FieldBorderColor"] = ToColor(
            Mix(panelBackground, accent, 0.18));
        publishedColors["MenuSurfaceColor"] = ToColor(
            Mix(panelBackground, accent, 0.04));
        publishedColors["ToolTipSurfaceColor"] = ToColor(
            Mix(panelBackground, accent, 0.10));

        var textPrimary = effectiveTheme.GetColor(
            ThemeTokens.TextPrimary);
        publishedColors["TextPrimaryDecorSoftColor"] = ToColor(
            textPrimary.WithAlpha(0x2A));
        publishedColors["TextPrimaryTransparentColor"] = ToColor(
            textPrimary.WithAlpha(0x00));

        // Exactly one dictionary owns the colours. Publishing the same keys
        // into several dictionaries (the application root *and* the merged
        // brush dictionary) looked harmless, but WPF resolves a colour in the
        // scope of whoever asks: brushes declared in ThemeBrushes read the
        // merged copy, while elements in a window read the root copy. When the
        // two drifted apart - which they do whenever one of the copies is a
        // still-deferred dictionary that loads after the write - the window
        // showed two themes at once: a pink rail around a navy toolbar.
        var target = FindBrushOwner(resources) ?? resources;
        foreach (var (key, color) in publishedColors)
            target[key] = color;

        target["MimiThemeId"] = effectiveTheme.Id;
        target["MimiThemeName"] = effectiveTheme.Name;

        // One line per apply: when a window looks half-themed, the log says
        // which theme was actually published and how often.
        Log.Information("Published theme {ThemeId}", effectiveTheme.Id);

        // Anything left over in another dictionary is a previous theme that a
        // scope could still resolve; drop it so the theme cannot half-apply.
        RemoveFromOtherDictionaries(
            resources,
            target,
            publishedColors.Keys.Append("MimiThemeId").Append("MimiThemeName"));

        VerifyPublishedTheme(resources, target, effectiveTheme);
    }

    /// <summary>
    /// Confirms the palette really has a single owner after publishing, and
    /// records the surface colour the shared brush ended up with. A half-themed
    /// window is hard to explain from a screenshot; this makes the log state it.
    /// </summary>
    private static void VerifyPublishedTheme(
        ResourceDictionary resources,
        ResourceDictionary target,
        ThemeDefinition theme)
    {
        var duplicates = EnumerateDictionaries(resources)
            .Where(dictionary =>
                !ReferenceEquals(dictionary, target) &&
                dictionary.Contains(ColorResourceKey(ThemeTokens.PanelBackground)))
            .Count();

        if (duplicates > 0)
        {
            Log.Warning(
                "Theme {ThemeId} still resolves from {DictionaryCount} extra dictionary slot(s)",
                theme.Id,
                duplicates);
        }

        if (target[BrushResourceKey(ThemeTokens.PanelBackground)] is SolidColorBrush brush)
        {
            Log.Debug(
                "Theme {ThemeId} panel brush resolves to {BrushColor}",
                theme.Id,
                brush.Color);
        }
    }

    /// <summary>
    /// The merged dictionary that declares the shared brushes
    /// (<c>ThemeBrushes.xaml</c>). Its brushes resolve their DynamicResource
    /// colours in that dictionary's own scope, so the colours have to live
    /// there rather than in the application root.
    /// </summary>
    private static ResourceDictionary? FindBrushOwner(ResourceDictionary resources)
    {
        foreach (var dictionary in EnumerateDictionaries(resources))
        {
            if (ReferenceEquals(dictionary, resources)) continue;
            if (dictionary.Contains(BrushResourceKey(ThemeTokens.Accent)) ||
                dictionary.Contains("PanelBackgroundBrush"))
            {
                return dictionary;
            }
        }

        return null;
    }

    private static void RemoveFromOtherDictionaries(
        ResourceDictionary resources,
        ResourceDictionary owner,
        IEnumerable<string> keys)
    {
        var stale = keys.ToArray();
        foreach (var dictionary in EnumerateDictionaries(resources))
        {
            if (ReferenceEquals(dictionary, owner) || dictionary.IsReadOnly) continue;

            foreach (var key in stale)
            {
                if (dictionary.Contains(key)) dictionary.Remove(key);
            }
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

    /// <summary>
    /// Linear color mix, used to derive interaction surfaces that belong to
    /// the same family as the theme instead of a fixed grey.
    /// </summary>
    private static ThemeColor Mix(
        ThemeColor from,
        ThemeColor to,
        double amount)
    {
        var t = Math.Clamp(amount, 0d, 1d);
        return new ThemeColor(
            255,
            Blend(from.R, to.R, t),
            Blend(from.G, to.G, t),
            Blend(from.B, to.B, t));
    }

    private static byte Blend(byte from, byte to, double t) =>
        (byte)Math.Round(from + ((to - from) * t));

    private static Color ToColor(ThemeColor color) =>
        Color.FromArgb(color.A, color.R, color.G, color.B);
}
