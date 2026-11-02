using System.Collections.ObjectModel;

namespace Mimi.Theming;

/// <summary>
/// An immutable, validated color theme. WPF brushes are derived from these
/// tokens in the application layer, keeping theme data portable and ready
/// for a future JSON community-theme loader.
/// </summary>
public sealed record ThemeDefinition
{
    public ThemeDefinition(
        string id,
        string name,
        IReadOnlyDictionary<string, string> colors)
    {
        ArgumentNullException.ThrowIfNull(colors);

        Id = id?.Trim() ?? string.Empty;
        Name = name?.Trim() ?? string.Empty;
        Colors = new ReadOnlyDictionary<string, string>(
            colors.ToDictionary(
                pair => pair.Key.Trim(),
                pair => pair.Value?.Trim() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase));
    }

    public string Id { get; }

    public string Name { get; }

    public IReadOnlyDictionary<string, string> Colors { get; }

    public bool IsValid => Validate().Count == 0;

    public ThemeColor GetColor(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return Colors.TryGetValue(token, out var value)
            ? ThemeColor.Parse(value)
            : throw new KeyNotFoundException(
                $"Theme '{Id}' does not define color token '{token}'.");
    }

    public bool TryGetColor(string token, out ThemeColor color)
    {
        color = default;
        return !string.IsNullOrWhiteSpace(token) &&
               Colors.TryGetValue(token, out var value) &&
               ThemeColor.TryParse(value, out color);
    }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (!IsValidId(Id))
        {
            errors.Add(
                "Theme id must be a lowercase slug containing letters, " +
                "digits, and single hyphens.");
        }

        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 80)
            errors.Add("Theme name must contain between 1 and 80 characters.");

        var required = new HashSet<string>(
            ThemeTokens.RequiredColorTokens,
            StringComparer.OrdinalIgnoreCase);
        foreach (var token in ThemeTokens.RequiredColorTokens)
        {
            if (!Colors.TryGetValue(token, out var value))
            {
                errors.Add($"Missing required color token '{token}'.");
            }
            else if (!ThemeColor.TryParse(value, out _))
            {
                errors.Add(
                    $"Color token '{token}' must use #RRGGBB or #AARRGGBB.");
            }
        }

        foreach (var token in Colors.Keys)
        {
            if (!required.Contains(token))
                errors.Add($"Unknown color token '{token}'.");
        }

        return errors;
    }

    /// <summary>
    /// Theme ids are lowercase slugs so they stay safe in settings files,
    /// URLs, and community-theme file names across languages.
    /// </summary>
    public static bool IsValidId(string? value)
    {
        if (value is null ||
            value.Length is < 1 or > 64 ||
            value.StartsWith('-') ||
            value.EndsWith('-') ||
            value.Contains("--", StringComparison.Ordinal))
        {
            return false;
        }

        return value.All(character =>
            character is >= 'a' and <= 'z' or
            >= '0' and <= '9' or '-');
    }
}
