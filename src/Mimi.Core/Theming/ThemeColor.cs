namespace Mimi.Theming;

/// <summary>
/// A platform-neutral ARGB color used by theme definitions. Keeping this in
/// Core lets built-in and future community themes be validated and measured
/// for contrast without taking a dependency on WPF.
/// </summary>
public readonly record struct ThemeColor(byte A, byte R, byte G, byte B)
{
    public static ThemeColor Parse(string value)
    {
        if (!TryParse(value, out var color))
        {
            throw new FormatException(
                $"'{value}' is not a supported #RRGGBB or #AARRGGBB color.");
        }

        return color;
    }

    public static bool TryParse(string? value, out ThemeColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var text = value.Trim();
        if (text.Length is not (7 or 9) || text[0] != '#') return false;

        var offset = 1;
        byte alpha = 255;
        if (text.Length == 9)
        {
            if (!TryParseByte(text, offset, out alpha)) return false;
            offset += 2;
        }

        if (!TryParseByte(text, offset, out var red) ||
            !TryParseByte(text, offset + 2, out var green) ||
            !TryParseByte(text, offset + 4, out var blue))
        {
            return false;
        }

        color = new ThemeColor(alpha, red, green, blue);
        return true;
    }

    public ThemeColor WithAlpha(byte alpha) => this with { A = alpha };

    public string ToHexString(bool includeAlpha = false) =>
        includeAlpha || A != 255
            ? $"#{A:X2}{R:X2}{G:X2}{B:X2}"
            : $"#{R:X2}{G:X2}{B:X2}";

    /// <summary>WCAG relative luminance after compositing over black.</summary>
    public double RelativeLuminance => CalculateRelativeLuminance(
        CompositeOver(new ThemeColor(255, 0, 0, 0)));

    public static double GetContrastRatio(
        ThemeColor foreground,
        ThemeColor background)
    {
        var effectiveBackground = background.CompositeOver(
            new ThemeColor(255, 0, 0, 0));
        var effectiveForeground = foreground.CompositeOver(
            effectiveBackground);
        var foregroundLuminance = CalculateRelativeLuminance(
            effectiveForeground);
        var backgroundLuminance = CalculateRelativeLuminance(
            effectiveBackground);
        var lighter = Math.Max(foregroundLuminance, backgroundLuminance);
        var darker = Math.Min(foregroundLuminance, backgroundLuminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private ThemeColor CompositeOver(ThemeColor background)
    {
        if (A == 255) return this;
        if (A == 0) return background with { A = 255 };

        var alpha = A / 255.0;
        return new ThemeColor(
            255,
            CompositeChannel(R, background.R, alpha),
            CompositeChannel(G, background.G, alpha),
            CompositeChannel(B, background.B, alpha));
    }

    private static byte CompositeChannel(
        byte foreground,
        byte background,
        double alpha) =>
        (byte)Math.Clamp(
            Math.Round(foreground * alpha + background * (1 - alpha)),
            0,
            255);

    private static double CalculateRelativeLuminance(ThemeColor color) =>
        0.2126 * Linearize(color.R) +
        0.7152 * Linearize(color.G) +
        0.0722 * Linearize(color.B);

    private static double Linearize(byte channel)
    {
        var value = channel / 255.0;
        return value <= 0.04045
            ? value / 12.92
            : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    private static bool TryParseByte(
        string text,
        int offset,
        out byte value)
    {
        value = 0;
        if (!TryParseHexDigit(text[offset], out var high) ||
            !TryParseHexDigit(text[offset + 1], out var low))
        {
            return false;
        }

        value = (byte)((high << 4) | low);
        return true;
    }

    private static bool TryParseHexDigit(char value, out int digit)
    {
        digit = value switch
        {
            >= '0' and <= '9' => value - '0',
            >= 'a' and <= 'f' => value - 'a' + 10,
            >= 'A' and <= 'F' => value - 'A' + 10,
            _ => -1
        };
        return digit >= 0;
    }
}
