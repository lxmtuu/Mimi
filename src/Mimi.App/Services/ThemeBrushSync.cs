using System.Windows;
using System.Windows.Media;
using Serilog;

namespace Mimi.Services;

/// <summary>
/// Pushes the published theme colours straight into the shared brushes.
///
/// Relying on the brushes' own <c>DynamicResource</c> expressions is not
/// enough to keep a live theme change whole: the colours in the dictionary do
/// update - the Windows caption, which reads the key directly, always shows the
/// new theme - but a brush that was handed to the visual tree (or cloned on the
/// way into a sealed template) can keep rendering the palette it was first
/// painted with. The visible result is one window showing two themes at once:
/// pink cards around navy panels.
///
/// So every brush is written to directly, by the same owner that writes the
/// colour keys. No element can hold a stale colour afterwards, because the
/// brush objects in the tree are the objects being written.
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
    /// Writes <paramref name="colors"/> into every brush the table knows about.
    /// Returns how many brushes were painted.
    /// </summary>
    public static int Push(
        ResourceDictionary owner,
        IReadOnlyDictionary<string, Color> colors)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(colors);

        var updated = 0;
        foreach (var (brushKey, colorKeys) in Map)
        {
            if (!owner.Contains(brushKey)) continue;
            if (owner[brushKey] is not Brush brush) continue;

            // A frozen brush can never be repainted, and a frozen brush cannot
            // exist here: binding a colour through DynamicResource makes it
            // unfreezable. If one turns up anyway, say so by name instead of
            // leaving half a window in the previous theme.
            if (brush.IsFrozen)
            {
                Log.Error(
                    "Brush {BrushKey} is frozen, so the theme cannot repaint it",
                    brushKey);
                continue;
            }

            if (brush is SolidColorBrush solid)
            {
                if (colors.TryGetValue(colorKeys[0], out var color))
                {
                    solid.Color = color;
                    updated++;
                }

                continue;
            }

            if (brush is GradientBrush gradient)
            {
                var stops = gradient.GradientStops;
                var count = Math.Min(stops.Count, colorKeys.Length);
                if (stops.Count != colorKeys.Length)
                {
                    Log.Warning(
                        "Brush {BrushKey} has {StopCount} gradient stop(s) but {KeyCount} colour(s) are mapped",
                        brushKey,
                        stops.Count,
                        colorKeys.Length);
                }

                for (var i = 0; i < count; i++)
                {
                    if (colors.TryGetValue(colorKeys[i], out var color))
                        stops[i].Color = color;
                }

                if (count > 0) updated++;
            }
        }

        return updated;
    }
}
