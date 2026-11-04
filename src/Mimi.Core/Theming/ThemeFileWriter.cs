using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mimi.Theming;

/// <summary>
/// Writes a theme definition back out as a community theme file
/// (schema version 1). This is what "duplicate for editing" uses: the user
/// gets a real, valid file with every token already filled in, instead of a
/// documentation example they have to complete by hand.
/// </summary>
public static class ThemeFileWriter
{
    /// <summary>
    /// Serializes <paramref name="theme"/> under a new id and name. Tokens are
    /// written in the canonical order so two flavours of the same theme are
    /// easy to compare (and diff) as text.
    /// </summary>
    public static string ToJson(
        ThemeDefinition theme,
        string id,
        string name)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var colors = new JsonObject();
        foreach (var token in ThemeTokens.RequiredColorTokens)
        {
            // A theme must be complete; an incomplete source theme would
            // produce a file the loader rejects, so missing tokens fall back
            // to the default palette instead.
            var value = theme.Colors.TryGetValue(token, out var own)
                ? own
                : BuiltInThemes.Default.Colors[token];

            colors[token] = ThemeColor.TryParse(value, out var color)
                ? Format(color)
                : Format(
                    BuiltInThemes.Default.GetColor(token));
        }

        var document = new JsonObject
        {
            ["schemaVersion"] = CommunityThemeLoader.CurrentSchemaVersion,
            ["id"] = id,
            ["name"] = name,
            ["colors"] = colors
        };

        return document.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    /// <summary>
    /// Picks an id that is a valid slug and that the catalog does not use yet:
    /// <c>sakura-dream</c> becomes <c>sakura-dream-custom</c>, then
    /// <c>sakura-dream-custom-2</c>, and so on.
    /// </summary>
    public static string UniqueId(ThemeDefinition source, ThemeCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(catalog);

        var stem = Slug(source.Id);
        if (stem.Length == 0) stem = "theme";

        var candidate = $"{stem}-custom";
        for (var suffix = 2; ; suffix++)
        {
            if (!IsTaken(candidate, catalog))
                return candidate;

            candidate = $"{stem}-custom-{suffix}";
        }
    }

    private static bool IsTaken(string id, ThemeCatalog catalog) =>
        catalog.All.Any(theme =>
            string.Equals(theme.Id, id, StringComparison.OrdinalIgnoreCase));

    private static string Slug(string value)
    {
        var text = new string(
            value
                .Trim()
                .ToLowerInvariant()
                .Select(character =>
                    character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-'
                        ? character
                        : '-')
                .ToArray());

        // Collapse runs so the id rule "no -- and no edge hyphens" holds even
        // for a community theme with an unusual name.
        while (text.Contains("--", StringComparison.Ordinal))
            text = text.Replace("--", "-", StringComparison.Ordinal);

        return text.Trim('-');
    }

    private static string Format(ThemeColor color) =>
        color.A == 255
            ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
            : $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
}
