using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Mimi.Theming;

namespace Mimi.ViewModels;

/// <summary>
/// One selectable entry in the Settings theme picker. The id is the stable
/// value persisted in settings; the name is display-only and refreshes when
/// the UI language changes (built-in themes use localized names, community
/// themes keep the name from their JSON file).
///
/// Each option also carries a three-colour sample, so the picker shows what a
/// theme looks like instead of only its name: choosing a theme used to mean
/// applying it and looking around the app.
/// </summary>
public sealed partial class ThemeOptionViewModel : ObservableObject
{
    public ThemeOptionViewModel(string id, string displayName, bool isBuiltIn)
        : this(id, displayName, isBuiltIn, theme: null)
    {
    }

    public ThemeOptionViewModel(
        ThemeDefinition theme,
        string displayName)
        : this(
            theme.Id,
            displayName,
            ThemeCatalog.IsBuiltIn(theme),
            theme)
    {
    }

    private ThemeOptionViewModel(
        string id,
        string displayName,
        bool isBuiltIn,
        ThemeDefinition? theme)
    {
        Id = id;
        name = displayName;
        IsBuiltIn = isBuiltIn;

        background = Sample(theme, ThemeTokens.AppBackground, "#070B14");
        accent = Sample(theme, ThemeTokens.Accent, "#3E8BFF");
        accentSecondary = Sample(theme, ThemeTokens.AccentSecondary, "#4FE3FF");
    }

    public string Id { get; }

    public bool IsBuiltIn { get; }

    [ObservableProperty]
    private string name;

    [ObservableProperty]
    private Brush background;

    [ObservableProperty]
    private Brush accent;

    [ObservableProperty]
    private Brush accentSecondary;

    /// <summary>
    /// Re-reads the colours after the theme file changed on disk. The option
    /// object is kept so the ComboBox selection survives a reload.
    /// </summary>
    public void Apply(ThemeDefinition theme, string displayName)
    {
        Name = displayName;
        Background = Sample(theme, ThemeTokens.AppBackground, "#070B14");
        Accent = Sample(theme, ThemeTokens.Accent, "#3E8BFF");
        AccentSecondary = Sample(theme, ThemeTokens.AccentSecondary, "#4FE3FF");
    }

    private static Brush Sample(
        ThemeDefinition? theme,
        string token,
        string fallback)
    {
        var color = theme is not null && theme.TryGetColor(token, out var found)
            ? found
            : ThemeColor.Parse(fallback);

        var brush = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }
}
