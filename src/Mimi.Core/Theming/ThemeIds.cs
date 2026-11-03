namespace Mimi.Theming;

/// <summary>
/// Shared theme-id spelling rules. Ids are stored as lowercase slugs, but
/// lookups accept friendly spellings ("Sakura Dream", "sakura_dream") by
/// comparing a compacted form that ignores separators and case.
/// </summary>
internal static class ThemeIds
{
    public static string Compact(string value) =>
        new(value.Trim().Where(character =>
            character is not ('-' or '_' or ' ')).ToArray());
}
