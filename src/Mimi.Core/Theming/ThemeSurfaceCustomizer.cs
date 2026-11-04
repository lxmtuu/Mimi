using System.Globalization;

namespace Mimi.Theming;

/// <summary>
/// Recolours a theme's background family from one colour the user picked.
///
/// The rule that makes this safe: a custom background changes the <em>hue</em>
/// of the backgrounds, not their <em>depth</em>. Each surface keeps exactly the
/// relative luminance it has in the theme, so the stack order, the step between
/// layers and the contrast against every text token stay identical to the
/// palette the theme was designed and tested with. Only the hue moves, which is
/// what "I want this app pink / green / navy" actually means.
///
/// The family is wider than the nine layers: the hero aurora, the video stage
/// and the cover fallback are backgrounds too. Leaving them on the theme's own
/// blue while the shell turns pink is exactly the patchwork look a background
/// colour is supposed to remove, so they move with it - by the same hue angle,
/// which keeps the relationships their author designed instead of flattening
/// them into one colour.
/// </summary>
public static class ThemeSurfaceCustomizer
{
    /// <summary>
    /// How much of the chosen colour's saturation the surfaces keep.
    /// </summary>
    public const double MaximumSaturation = 0.5;

    /// <summary>
    /// The steps that must stay visible after rounding. Same floors as
    /// <see cref="ThemeLayerAudit"/>, expressed for the derived palette.
    /// </summary>
    private static readonly (string Below, string Above, double Minimum)[] Steps =
    [
        (ThemeTokens.AppBackground, ThemeTokens.PanelBackground, 1.05),
        (ThemeTokens.PanelBackground, ThemeTokens.RaisedBackground, 1.08),
        (ThemeTokens.RaisedBackground, ThemeTokens.ControlBackground, 1.10)
    ];

    /// <summary>
    /// The background layers, in the order the layout stacks them. Accents and
    /// text are deliberately excluded: the user asked for a background colour,
    /// not a new theme.
    /// </summary>
    public static IReadOnlyList<string> SurfaceTokens { get; } =
    [
        ThemeTokens.AppBackground,
        ThemeTokens.SunkenBackground,
        ThemeTokens.PanelBackground,
        ThemeTokens.HeaderBackground,
        ThemeTokens.RaisedBackground,
        ThemeTokens.ControlBackground,
        ThemeTokens.SelectedBackground,
        ThemeTokens.Border,
        ThemeTokens.Divider
    ];

    /// <summary>
    /// The hued backgrounds that are not layers: the hero aurora, the video
    /// stage and the cover fallback. They belong to the same family as the
    /// panels - a hero that keeps its old blue while the shell turns pink is
    /// the "colours do not line up" look this exists to avoid.
    /// </summary>
    public static IReadOnlyList<string> GradientTokens { get; } =
    [
        ThemeTokens.HeroGradientStart,
        ThemeTokens.HeroGradientMiddle,
        ThemeTokens.HeroGradientEnd,
        ThemeTokens.StageGradientStart,
        ThemeTokens.StageGradientMiddle,
        ThemeTokens.StageGradientEnd,
        ThemeTokens.CoverGradientStart,
        ThemeTokens.CoverGradientEnd
    ];

    /// <summary>
    /// Everything the tint repaints: the layer stack plus the gradients. Text,
    /// accents and the warning/danger plates are deliberately not here.
    /// </summary>
    public static IReadOnlyList<string> BackgroundTokens { get; } =
        [.. SurfaceTokens, .. GradientTokens];

    /// <summary>
    /// Below this saturation a theme has no hue worth keeping - a grey
    /// community theme - so the chosen colour is painted straight on instead of
    /// being rotated.
    /// </summary>
    private const double AchromaticBelow = 0.08;

    /// <summary>
    /// Returns <paramref name="theme"/> with its background family - the nine
    /// layers and the eight gradient stops - recoloured from
    /// <paramref name="background"/>. The result keeps the theme's id and name
    /// so the picker still shows which theme this is.
    /// </summary>
    public static ThemeDefinition Apply(
        ThemeDefinition theme,
        ThemeColor background)
    {
        ArgumentNullException.ThrowIfNull(theme);

        var baseColor = theme.TryGetColor(ThemeTokens.AppBackground, out var appBackground)
            ? appBackground
            : BuiltInThemes.Default.GetColor(ThemeTokens.AppBackground);

        ToHsl(baseColor, out var baseHue, out var baseSaturation, out _);
        ToHsl(background, out var chosenHue, out var chosenSaturation, out _);

        var cappedSaturation = Math.Min(chosenSaturation, MaximumSaturation);
        var hasOwnHue = baseSaturation >= AchromaticBelow;

        // Rotate rather than repaint. The aurora goes from a cooler start to a
        // warmer end on purpose, and that relationship is what makes it read as
        // light falling on one place. Moving every stop by the same angle keeps
        // that relationship while putting the whole family in the chosen hue.
        var rotation = chosenHue - baseHue;

        // The chosen colour also sets how colourful the shell is. Without this,
        // picking grey would leave one fully saturated blue gradient glowing in
        // the middle of a monochrome app.
        var saturationScale = hasOwnHue
            ? cappedSaturation / baseSaturation
            : 1;

        // A hue-only rotation is the whole point for a theme that has its own
        // colour; a theme without one simply takes the chosen colour.
        (double Hue, double Saturation) TintOf(ThemeColor color)
        {
            if (!hasOwnHue) return (chosenHue, cappedSaturation);

            ToHsl(color, out var hue, out var saturation, out _);
            return (hue + rotation, Math.Min(saturation * saturationScale, MaximumSaturation));
        }

        var colors = new Dictionary<string, string>(
            theme.Colors,
            StringComparer.OrdinalIgnoreCase);

        // The layer stack: relit to the depth it has in the theme, then nudged
        // until the ladder survives 8-bit rounding. Rounding alone is enough to
        // turn a 1.05:1 step into 1.04:1, which is the difference between a
        // panel you can see and a wall of one colour.
        var layers = new Dictionary<string, (double Hue, double Saturation, double Luminance)>(
            StringComparer.Ordinal);
        foreach (var token in SurfaceTokens)
        {
            if (!theme.TryGetColor(token, out var color)) continue;

            var (hue, saturation) = TintOf(color);
            layers[token] = (hue, saturation, color.RelativeLuminance);
        }

        for (var pass = 0; pass < 64; pass++)
        {
            foreach (var (token, layer) in layers)
            {
                colors[token] = Relight(layer.Hue, layer.Saturation, layer.Luminance)
                    .ToHexString();
            }

            var weak = FindWeakStep(colors);
            if (weak is null) break;

            // Raise the layer that is too close to the one below it; the loop
            // re-checks everything, so pushing one layer up cannot leave the
            // next step broken.
            if (!layers.TryGetValue(weak, out var broken)) break;
            layers[weak] = (broken.Hue, broken.Saturation, broken.Luminance * 1.01);
        }

        // Gradients keep their own stops; they are light, not layers, so no
        // ladder applies to them.
        foreach (var token in GradientTokens)
        {
            if (!theme.TryGetColor(token, out var color)) continue;

            var (hue, saturation) = TintOf(color);
            colors[token] = Relight(hue, saturation, color.RelativeLuminance)
                .ToHexString();
        }

        // The stage scrim is the window colour with an alpha on top of the
        // video; it has to move with the window it is drawn over.
        if (theme.TryGetColor(ThemeTokens.StageOverlay, out var overlay) &&
            colors.TryGetValue(ThemeTokens.AppBackground, out var tintedApp) &&
            ThemeColor.TryParse(tintedApp, out var tintedAppColor))
        {
            colors[ThemeTokens.StageOverlay] =
                (tintedAppColor with { A = overlay.A }).ToHexString(includeAlpha: true);
        }

        return new ThemeDefinition(theme.Id, theme.Name, colors);
    }

    /// <summary>
    /// The first layer whose step from the layer below it is too small, or
    /// null when the stack reads correctly.
    /// </summary>
    private static string? FindWeakStep(IReadOnlyDictionary<string, string> colors)
    {
        foreach (var (below, above, minimum) in Steps)
        {
            if (!colors.TryGetValue(below, out var lower) ||
                !colors.TryGetValue(above, out var upper))
            {
                continue;
            }

            if (!ThemeColor.TryParse(lower, out var lowerColor) ||
                !ThemeColor.TryParse(upper, out var upperColor))
            {
                continue;
            }

            if (ThemeColor.GetContrastRatio(lowerColor, upperColor) < minimum)
                return above;
        }

        return null;
    }

    /// <summary>
    /// Writes the tint as the conventional <c>#RRGGBB</c> string, or reports
    /// that the text is not a colour at all.
    /// </summary>
    public static bool TryParse(string? text, out ThemeColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var value = text.Trim();
        if (!value.StartsWith('#')) value = "#" + value;

        // 8-digit input is accepted but its alpha is dropped: a translucent
        // background would let the window behind Mimi show through.
        if (!ThemeColor.TryParse(value, out color)) return false;

        color = color with { A = 255 };
        return true;
    }

    /// <summary>
    /// A colour with this hue and saturation whose relative luminance is
    /// <paramref name="luminance"/>. Luminance rises monotonically with HSL
    /// lightness, so a bounded search lands on the answer without a formula that
    /// has to be maintained.
    /// </summary>
    private static ThemeColor Relight(double hue, double saturation, double luminance)
    {
        var low = 0d;
        var high = 1d;

        for (var i = 0; i < 32; i++)
        {
            var middle = (low + high) / 2;
            var candidate = FromHsl(hue, saturation, middle);
            if (candidate.RelativeLuminance < luminance)
                low = middle;
            else
                high = middle;
        }

        return FromHsl(hue, saturation, (low + high) / 2);
    }

    private static ThemeColor FromHsl(double hue, double saturation, double lightness)
    {
        var chroma = (1 - Math.Abs((2 * lightness) - 1)) * saturation;
        var secondary = chroma * (1 - Math.Abs(((hue / 60) % 2) - 1));
        var offset = lightness - (chroma / 2);

        var (r, g, b) = (int)Math.Floor(hue / 60) switch
        {
            0 => (chroma, secondary, 0d),
            1 => (secondary, chroma, 0d),
            2 => (0d, chroma, secondary),
            3 => (0d, secondary, chroma),
            4 => (secondary, 0d, chroma),
            _ => (chroma, 0d, secondary)
        };

        return new ThemeColor(
            255,
            ToByte(r + offset),
            ToByte(g + offset),
            ToByte(b + offset));
    }

    private static void ToHsl(
        ThemeColor color,
        out double hue,
        out double saturation,
        out double lightness)
    {
        var r = color.R / 255d;
        var g = color.G / 255d;
        var b = color.B / 255d;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        lightness = (max + min) / 2;
        if (delta <= 0)
        {
            // Grey: there is no hue to keep, and nothing to tint with.
            hue = 0;
            saturation = 0;
            return;
        }

        var denominator = 1 - Math.Abs((2 * lightness) - 1);
        saturation = denominator <= 0 ? 0 : delta / denominator;

        hue = max switch
        {
            _ when max == r => 60 * (((g - b) / delta) % 6),
            _ when max == g => 60 * (((b - r) / delta) + 2),
            _ => 60 * (((r - g) / delta) + 4)
        };

        if (hue < 0) hue += 360;
    }

    private static byte ToByte(double value) =>
        (byte)Math.Clamp(Math.Round(value * 255), 0, 255);

    /// <summary>
    /// Formats a colour for display in Settings, always as <c>#RRGGBB</c>.
    /// </summary>
    public static string ToDisplayString(ThemeColor color) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"#{color.R:X2}{color.G:X2}{color.B:X2}");
}
