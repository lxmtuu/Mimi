using System.Windows;
using System.Windows.Media;
using Mimi.Theming;
using Serilog;

namespace Mimi.Services;

/// <summary>
/// Publishes platform-neutral theme colors into the WPF application
/// resource dictionary. Shared brushes in ThemeBrushes.xaml bind to these
/// colors dynamically, so changing a theme updates open windows without a
/// restart.
/// </summary>
public sealed class ThemeService
{
    public event EventHandler? ThemeChanged;

    public string CurrentThemeId { get; private set; } =
        BuiltInThemes.DefaultId;

    public ThemeDefinition CurrentTheme =>
        BuiltInThemes.GetOrDefault(CurrentThemeId);

    public string Apply(string? themeId)
    {
        var theme = BuiltInThemes.GetOrDefault(themeId);
        CurrentThemeId = theme.Id;

        if (Application.Current?.Resources is { } resources)
            Apply(theme, resources);

        ThemeChanged?.Invoke(this, EventArgs.Empty);
        return theme.Id;
    }

    public void ApplyDefault() => Apply(BuiltInThemes.DefaultId);

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
