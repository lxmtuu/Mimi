namespace Mimi.Theming;

/// <summary>
/// Checks the background layer stack of a theme: is every surface where it
/// belongs (inset below panel, panel below raised card) and is each step big
/// enough to be seen. Built-in themes are held to this by tests; community
/// themes are only warned about in the log, because a theme author's taste is
/// not a build failure - a card that disappears into its panel is.
/// </summary>
public static class ThemeLayerAudit
{
    /// <summary>
    /// Each step has its own floor, measured from the built-in themes. A panel
    /// on the window can be subtle - it always carries a 1 px border - while a
    /// card on a panel must be stronger, because cards are drawn borderless in
    /// a few compact places and a raised control must stay visible on a card.
    /// </summary>
    private static readonly (string Below, string Above, string Role, double Minimum)[] Steps =
    [
        (ThemeTokens.AppBackground, ThemeTokens.PanelBackground, "panel on window", 1.05),
        (ThemeTokens.PanelBackground, ThemeTokens.RaisedBackground, "card on panel", 1.08),
        (ThemeTokens.RaisedBackground, ThemeTokens.ControlBackground, "control on card", 1.10)
    ];

    /// <summary>
    /// Every problem found, as a short sentence. Empty means the stack reads
    /// correctly. Missing tokens are skipped: structure is
    /// <see cref="ThemeDefinition.Validate"/>'s job, not this one.
    /// </summary>
    public static IReadOnlyList<string> FindFlattenedLayers(ThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        var problems = new List<string>();

        if (theme.TryGetColor(ThemeTokens.SunkenBackground, out var sunken) &&
            theme.TryGetColor(ThemeTokens.PanelBackground, out var panel) &&
            sunken.RelativeLuminance >= panel.RelativeLuminance)
        {
            problems.Add(
                $"sunken area ({sunken.ToHexString()}) is not darker than the panel " +
                $"({panel.ToHexString()}), so inset fields look raised");
        }

        foreach (var (below, above, role, minimum) in Steps)
        {
            if (!theme.TryGetColor(below, out var lower) ||
                !theme.TryGetColor(above, out var upper))
            {
                continue;
            }

            var ratio = ThemeColor.GetContrastRatio(lower, upper);
            if (ratio < minimum)
            {
                problems.Add(
                    $"{role}: {below} and {above} differ by only {ratio:0.00}:1 " +
                    $"(needs {minimum:0.00}:1)");
            }
        }

        return problems;
    }
}
