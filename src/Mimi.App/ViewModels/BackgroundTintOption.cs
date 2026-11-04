using System.Windows.Media;

namespace Mimi.ViewModels;

/// <summary>
/// One background tint offered in Settings. The hex string is what gets
/// persisted and parsed; the brush is only for the swatch, so the swatch does
/// not depend on WPF converting a string into a colour at binding time.
/// </summary>
public sealed class BackgroundTintOption
{
    public BackgroundTintOption(string hex, Color color)
    {
        Hex = hex;

        var brush = new SolidColorBrush(color);
        brush.Freeze();
        Swatch = brush;
    }

    public string Hex { get; }

    public Brush Swatch { get; }
}
