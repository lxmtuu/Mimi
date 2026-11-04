using System.Windows;
using System.Windows.Media;
using Serilog;

namespace Mimi.Services;

/// <summary>
/// Makes the shared brushes carry the theme that was just published.
///
/// Two things had to be true at once, and only one of them was:
///
/// 1. The colour keys must be updated - they were, which is why the Windows
///    caption (it reads the key directly) always showed the new theme.
/// 2. The brushes must actually be repainted. A brush that an element picked up
///    with <c>StaticResource</c> is fixed at that moment: it stays the very
///    instance it was handed, and if anything along the way copied or froze it,
///    writing to the dictionary cannot reach it. That is how one window ends up
///    showing two themes - pink cards inside navy panels.
///
/// So this does both halves of the fix:
///
/// * the brush the window loaded with is repainted in place, forever - anyone
///   still holding that instance sees the new colour;
/// * the dictionary entry is replaced with a fresh instance on every publish,
///   which is what makes a <c>DynamicResource</c> reference re-resolve, and
///   what rescues anything that ended up with a copy of the brush.
///
/// Every XAML reference to a theme brush is dynamic for that second reason; a
/// test keeps it that way.
/// </summary>
public static class ThemeBrushSync
{
    /// <summary>
    /// Brush key to the colour keys it paints, in gradient-stop order. One
    /// entry per brush declared in <c>ThemeBrushes.xaml</c>; the test that
    /// guards this table fails if a brush is added there and not here.
    /// </summary>
    public static IReadOnlyDictionary<string, string[]> Map { get; } =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            // Layer stack.
            ["AppBackgroundBrush"] = ["AppBackgroundColor"],
            ["PanelBackgroundBrush"] = ["PanelBackgroundColor"],
            ["RaisedBackgroundBrush"] = ["RaisedBackgroundColor"],
            ["SunkenBackgroundBrush"] = ["SunkenBackgroundColor"],
            ["ControlBackgroundBrush"] = ["ControlBackgroundColor"],
            ["SelectedBackgroundBrush"] = ["SelectedBackgroundColor"],
            ["HeaderBackgroundBrush"] = ["HeaderBackgroundColor"],
            ["BorderBrush"] = ["BorderColor"],
            ["DividerBrush"] = ["DividerColor"],
            ["ScrimBrush"] = ["AppBackgroundColor"],

            // Accents.
            ["AccentBrush"] = ["AccentColor"],
            ["AccentSecondaryBrush"] = ["AccentSecondaryColor"],
            ["AccentHotBrush"] = ["AccentHotColor"],
            ["OnAccentBrush"] = ["OnAccentColor"],
            ["AccentDecorSoftBrush"] = ["AccentDecorSoftColor"],
            ["AccentDecorMediumBrush"] = ["AccentDecorMediumColor"],
            ["AccentDecorStrongBrush"] = ["AccentDecorStrongColor"],
            ["AccentDecorVividBrush"] = ["AccentDecorVividColor"],
            ["AccentSecondaryDecorSoftBrush"] = ["AccentSecondaryDecorSoftColor"],
            ["AccentSecondaryDecorMediumBrush"] = ["AccentSecondaryDecorMediumColor"],
            ["AccentHotDecorSoftBrush"] = ["AccentHotDecorSoftColor"],
            ["AccentHotDecorMediumBrush"] = ["AccentHotDecorMediumColor"],

            // Interaction surfaces.
            ["HoverSurfaceBrush"] = ["HoverSurfaceColor"],
            ["HoverSurfaceStrongBrush"] = ["HoverSurfaceStrongColor"],
            ["HoverBorderBrush"] = ["HoverBorderColor"],
            ["SelectedBorderBrush"] = ["SelectedBorderColor"],
            ["FieldBorderBrush"] = ["FieldBorderColor"],
            ["MenuSurfaceBrush"] = ["MenuSurfaceColor"],
            ["ToolTipSurfaceBrush"] = ["ToolTipSurfaceColor"],

            // Text.
            ["TextPrimaryBrush"] = ["TextPrimaryColor"],
            ["TextStrongBrush"] = ["TextStrongColor"],
            ["TextSecondaryBrush"] = ["TextSecondaryColor"],
            ["TextMutedBrush"] = ["TextMutedColor"],
            ["TextFaintBrush"] = ["TextFaintColor"],
            ["TextDisabledBrush"] = ["TextDisabledColor"],

            // Plates.
            ["WarningBrush"] = ["WarningColor"],
            ["WarningTextBrush"] = ["WarningTextColor"],
            ["WarningBackgroundBrush"] = ["WarningBackgroundColor"],
            ["WarningBorderBrush"] = ["WarningBorderColor"],
            ["DangerBackgroundBrush"] = ["DangerBackgroundColor"],
            ["StageOverlayBrush"] = ["StageOverlayColor"],

            // Gradients: the hero, the stage, the cover and the light that
            // falls on them. Leaving these behind is what made a themed app
            // keep one blue block in the middle of it.
            ["HeroGradientBrush"] =
            [
                "HeroGradientStartColor",
                "HeroGradientMiddleColor",
                "HeroGradientEndColor"
            ],
            ["StageGradientBrush"] =
            [
                "StageGradientStartColor",
                "StageGradientMiddleColor",
                "StageGradientEndColor"
            ],
            ["CoverGradientBrush"] =
            [
                "CoverGradientStartColor",
                "CoverGradientEndColor"
            ],
            ["NowPlayingPanelBrush"] =
            [
                "RaisedBackgroundColor",
                "PanelBackgroundColor",
                "SunkenBackgroundColor"
            ],
            ["HeroAccentGlowBrush"] =
            [
                "AccentDecorMediumColor",
                "AccentDecorSoftColor",
                "AccentTransparentColor"
            ],
            ["HeroSecondaryGlowBrush"] =
            [
                "AccentSecondaryDecorMediumColor",
                "AccentSecondaryDecorSoftColor",
                "AccentSecondaryTransparentColor"
            ],
            ["HeroTopHighlightBrush"] =
            [
                "TextPrimaryDecorSoftColor",
                "TextPrimaryTransparentColor"
            ],
            ["NowPlayingGlowBrush"] =
            [
                "AccentDecorMediumColor",
                "AccentSecondaryDecorSoftColor",
                "AccentSecondaryTransparentColor"
            ],
            ["CoverSheenBrush"] =
            [
                "TextPrimaryDecorSoftColor",
                "TextPrimaryTransparentColor",
                "TextPrimaryTransparentColor"
            ],
            ["StageAccentGlowBrush"] =
            [
                "AccentDecorMediumColor",
                "AccentDecorSoftColor",
                "AccentTransparentColor"
            ],
            ["StageHotGlowBrush"] =
            [
                "AccentHotDecorMediumColor",
                "AccentHotDecorSoftColor",
                "AccentHotTransparentColor"
            ],
            ["StageVignetteBrush"] =
            [
                "AppBackgroundTransparentColor",
                "AppBackgroundTransparentColor",
                "AppBackgroundColor"
            ]
        };

    /// <summary>
    /// The brush each key originally had, per dictionary. That first instance
    /// is the one the visual tree picked up when the window was loaded, so it
    /// has to keep being repainted for as long as the app runs - even though
    /// the dictionary entry is replaced on every theme change.
    /// </summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<
        ResourceDictionary,
        Dictionary<string, Brush>> Originals = new();

    /// <summary>
    /// Writes <paramref name="colors"/> into every brush the table knows about
    /// and leaves a fresh instance behind in the dictionary. Returns how many
    /// brushes were painted.
    /// </summary>
    public static int Sync(
        ResourceDictionary owner,
        IReadOnlyDictionary<string, Color> colors)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(colors);

        var originals = Originals.GetValue(
            owner,
            _ => new Dictionary<string, Brush>(StringComparer.Ordinal));

        var updated = 0;
        foreach (var (brushKey, colorKeys) in Map)
        {
            if (!owner.Contains(brushKey)) continue;
            if (owner[brushKey] is not Brush current) continue;

            // The instance a window is already painting with. Repainting it is
            // what makes a theme change reach elements that took the brush at
            // load time; that is the case a dictionary write alone cannot fix.
            if (!originals.TryGetValue(brushKey, out var original))
            {
                original = current;
                originals[brushKey] = original;
            }

            Paint(original, brushKey, colorKeys, colors);
            if (!ReferenceEquals(original, current))
                Paint(current, brushKey, colorKeys, colors);

            // Hand out a fresh instance as well. A DynamicResource reference
            // only re-resolves when the dictionary value itself changes, which
            // is what rescues anything that ended up with a copy.
            var fresh = CreateLike(original, brushKey);
            if (fresh is null) continue;

            Paint(fresh, brushKey, colorKeys, colors);
            owner[brushKey] = fresh;
            updated++;
        }

        return updated;
    }

    /// <summary>
    /// A brand new, definitely modifiable brush with the same shape as
    /// <paramref name="brush"/>: same kind, same gradient geometry, same
    /// opacity. Built explicitly rather than cloned, because a clone of a
    /// frozen brush can come back frozen - and a frozen brush is exactly the
    /// case that used to leave a window half themed.
    /// </summary>
    private static Brush? CreateLike(Brush brush, string brushKey)
    {
        switch (brush)
        {
            case SolidColorBrush solid:
                return new SolidColorBrush(solid.Color)
                {
                    Opacity = solid.Opacity
                };

            case LinearGradientBrush linear:
            {
                var copy = new LinearGradientBrush
                {
                    StartPoint = linear.StartPoint,
                    EndPoint = linear.EndPoint,
                    MappingMode = linear.MappingMode,
                    SpreadMethod = linear.SpreadMethod,
                    ColorInterpolationMode = linear.ColorInterpolationMode,
                    Opacity = linear.Opacity
                };

                foreach (var stop in linear.GradientStops)
                    copy.GradientStops.Add(new GradientStop(stop.Color, stop.Offset));

                return copy;
            }

            case RadialGradientBrush radial:
            {
                var copy = new RadialGradientBrush
                {
                    Center = radial.Center,
                    GradientOrigin = radial.GradientOrigin,
                    RadiusX = radial.RadiusX,
                    RadiusY = radial.RadiusY,
                    MappingMode = radial.MappingMode,
                    SpreadMethod = radial.SpreadMethod,
                    ColorInterpolationMode = radial.ColorInterpolationMode,
                    Opacity = radial.Opacity
                };

                foreach (var stop in radial.GradientStops)
                    copy.GradientStops.Add(new GradientStop(stop.Color, stop.Offset));

                return copy;
            }

            default:
                Log.Warning(
                    "Brush {BrushKey} is a {BrushType}, which this repaint does not know how to rebuild",
                    brushKey,
                    brush.GetType().Name);
                return null;
        }
    }

    private static void Paint(
        Brush brush,
        string brushKey,
        string[] colorKeys,
        IReadOnlyDictionary<string, Color> colors)
    {
        // Nothing can be written to a frozen brush. The replacement instance
        // covers that case, so there is nothing to do here.
        if (brush.IsFrozen) return;

        if (brush is SolidColorBrush solid)
        {
            if (colors.TryGetValue(colorKeys[0], out var color))
                solid.Color = color;

            return;
        }

        if (brush is not GradientBrush gradient) return;

        var stops = gradient.GradientStops;
        if (stops.Count != colorKeys.Length)
        {
            Log.Warning(
                "Brush {BrushKey} has {StopCount} gradient stop(s) but {KeyCount} colour(s) are mapped",
                brushKey,
                stops.Count,
                colorKeys.Length);
        }

        var count = Math.Min(stops.Count, colorKeys.Length);
        for (var i = 0; i < count; i++)
        {
            if (colors.TryGetValue(colorKeys[i], out var color))
                stops[i].Color = color;
        }
    }
}
