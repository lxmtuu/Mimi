using System.Windows.Media;
using Mimi.Theming;

namespace Mimi.ViewModels;

/// <summary>
/// One colour in the palette strip in Settings. The token name is kept as the
/// tooltip so a photographer of themes can tell which colour is which.
/// </summary>
public sealed class ThemeSwatch
{
    /// <summary>
    /// Surfaces first, then accents: the strip reads left to right as the
    /// layers a user actually sees, from the window background to the
    /// highlight colour.
    /// </summary>
    public static IReadOnlyList<string> PreviewTokens { get; } =
    [
        ThemeTokens.AppBackground,
        ThemeTokens.PanelBackground,
        ThemeTokens.RaisedBackground,
        ThemeTokens.SelectedBackground,
        ThemeTokens.Border,
        ThemeTokens.Accent,
        ThemeTokens.AccentSecondary,
        ThemeTokens.AccentHot,
        ThemeTokens.TextPrimary
    ];

    public ThemeSwatch(string token, ThemeColor color)
    {
        Token = token;

        var brush = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
        brush.Freeze();
        Brush = brush;
    }

    public string Token { get; }

    public Brush Brush { get; }
}
