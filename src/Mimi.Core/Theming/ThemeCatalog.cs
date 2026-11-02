namespace Mimi.Theming;

/// <summary>
/// The themes Mimi can actually apply: the four built-in themes first,
/// followed by valid community themes in load order. Community themes can
/// never shadow a built-in id (or an earlier community id); collisions and
/// invalid definitions are skipped and reported in <see cref="Warnings"/>
/// so the app layer can log them exactly once.
/// </summary>
public sealed class ThemeCatalog
{
    private readonly Dictionary<string, ThemeDefinition> byCompactId;

    public ThemeCatalog(IEnumerable<ThemeDefinition> communityThemes)
    {
        ArgumentNullException.ThrowIfNull(communityThemes);

        var all = new List<ThemeDefinition>(BuiltInThemes.All);
        byCompactId = BuiltInThemes.All.ToDictionary(
            theme => ThemeIds.Compact(theme.Id),
            StringComparer.OrdinalIgnoreCase);

        var community = new List<ThemeDefinition>();
        var warnings = new List<string>();
        foreach (var theme in communityThemes)
        {
            if (theme is null)
            {
                warnings.Add("Skipped a null community theme.");
                continue;
            }

            var errors = theme.Validate();
            if (errors.Count > 0)
            {
                warnings.Add(
                    $"Skipped invalid community theme '{theme.Id}': " +
                    string.Join("; ", errors));
                continue;
            }

            var key = ThemeIds.Compact(theme.Id);
            if (byCompactId.TryGetValue(key, out var existing))
            {
                warnings.Add(
                    $"Skipped community theme '{theme.Id}' because its id " +
                    $"is already used by '{existing.Id}'.");
                continue;
            }

            byCompactId.Add(key, theme);
            community.Add(theme);
            all.Add(theme);
        }

        All = all;
        CommunityThemes = community;
        Warnings = warnings;
    }

    /// <summary>Catalog containing only the built-in themes.</summary>
    public static ThemeCatalog BuiltInsOnly { get; } = new([]);

    /// <summary>Built-in themes first, then accepted community themes.</summary>
    public IReadOnlyList<ThemeDefinition> All { get; }

    public IReadOnlyList<ThemeDefinition> CommunityThemes { get; }

    /// <summary>Why each rejected community theme was skipped.</summary>
    public IReadOnlyList<string> Warnings { get; }

    public static bool IsBuiltIn(ThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        return BuiltInThemes.All.Any(
            builtIn => string.Equals(
                builtIn.Id,
                theme.Id,
                StringComparison.OrdinalIgnoreCase));
    }

    public bool TryGet(string? value, out ThemeDefinition theme)
    {
        if (!string.IsNullOrWhiteSpace(value) &&
            byCompactId.TryGetValue(ThemeIds.Compact(value), out var found))
        {
            theme = found;
            return true;
        }

        theme = BuiltInThemes.Default;
        return false;
    }

    public ThemeDefinition GetOrDefault(string? value) =>
        TryGet(value, out var theme) ? theme : BuiltInThemes.Default;

    public string NormalizeId(string? value) =>
        TryGet(value, out var theme) ? theme.Id : BuiltInThemes.DefaultId;
}
