using System.Text.Json;

namespace Mimi.Theming;

/// <summary>
/// Outcome of loading one community theme. <see cref="Theme"/> is non-null
/// only when the file parsed and passed full validation; otherwise
/// <see cref="Errors"/> explains every problem that was found.
/// </summary>
public sealed record ThemeLoadResult(
    string Source,
    ThemeDefinition? Theme,
    IReadOnlyList<string> Errors)
{
    public bool Success => Theme is not null;
}

/// <summary>
/// Loads user-supplied JSON themes (schema version 1) from the per-user
/// themes folder. Loading never throws for malformed files: a broken theme
/// is skipped with errors so one bad download cannot stop Mimi from
/// starting. The expected shape is:
///
/// {
///   "schemaVersion": 1,
///   "id": "my-theme",
///   "name": "My Theme",
///   "colors": { "AppBackground": "#0B101B", ... }
/// }
/// </summary>
public static class CommunityThemeLoader
{
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// A complete theme is a few kilobytes. The cap simply keeps a stray
    /// multi-megabyte .json file in the folder from slowing startup.
    /// </summary>
    public const int MaximumFileSizeBytes = 256 * 1024;

    public static ThemeLoadResult Parse(string json, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(json);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            return Failure(sourceName, $"Invalid JSON: {ex.Message}");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return Failure(sourceName, "Theme file must contain a JSON object.");

            if (root.TryGetProperty("schemaVersion", out var schemaValue) &&
                (schemaValue.ValueKind != JsonValueKind.Number ||
                 !schemaValue.TryGetInt32(out var schemaVersion) ||
                 schemaVersion != CurrentSchemaVersion))
            {
                return Failure(
                    sourceName,
                    $"Unsupported schemaVersion '{schemaValue}'; this Mimi " +
                    $"build supports version {CurrentSchemaVersion}.");
            }

            var errors = new List<string>();
            var id = GetStringProperty(root, "id", errors);
            var name = GetStringProperty(root, "name", errors);
            var colors = GetColors(root, errors);
            if (errors.Count > 0)
                return new ThemeLoadResult(sourceName, null, errors);

            ThemeDefinition theme;
            try
            {
                theme = new ThemeDefinition(
                    id ?? string.Empty,
                    name ?? string.Empty,
                    colors);
            }
            catch (ArgumentException ex)
            {
                return Failure(
                    sourceName,
                    $"Duplicate color token: {ex.Message}");
            }

            var validationErrors = theme.Validate();
            return validationErrors.Count > 0
                ? new ThemeLoadResult(sourceName, null, validationErrors)
                : new ThemeLoadResult(sourceName, theme, []);
        }
    }

    public static ThemeLoadResult LoadFile(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
                return Failure(path, "Theme file was not found.");
            if (info.Length > MaximumFileSizeBytes)
            {
                return Failure(
                    path,
                    $"Theme file is {info.Length} bytes; the limit is " +
                    $"{MaximumFileSizeBytes} bytes.");
            }

            return Parse(File.ReadAllText(path), path);
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            return Failure(path, ex.Message);
        }
    }

    /// <summary>
    /// Loads every *.json file directly inside <paramref name="directory"/>
    /// in file-name order. A missing directory simply yields no results.
    /// </summary>
    public static IReadOnlyList<ThemeLoadResult> LoadDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (!Directory.Exists(directory)) return [];

        return Directory
            .EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(
                path => Path.GetFileName(path),
                StringComparer.OrdinalIgnoreCase)
            .Select(LoadFile)
            .ToList();
    }

    private static string? GetStringProperty(
        JsonElement root,
        string propertyName,
        List<string> errors)
    {
        if (!root.TryGetProperty(propertyName, out var value)) return null;
        if (value.ValueKind == JsonValueKind.String)
            return value.GetString();

        errors.Add($"'{propertyName}' must be a string.");
        return null;
    }

    private static Dictionary<string, string> GetColors(
        JsonElement root,
        List<string> errors)
    {
        var colors = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!root.TryGetProperty("colors", out var colorsElement))
        {
            errors.Add("Missing required 'colors' object.");
            return colors;
        }

        if (colorsElement.ValueKind != JsonValueKind.Object)
        {
            errors.Add("'colors' must be an object mapping token names to hex colors.");
            return colors;
        }

        foreach (var property in colorsElement.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String)
            {
                colors[property.Name] = property.Value.GetString() ?? string.Empty;
            }
            else
            {
                errors.Add(
                    $"Color token '{property.Name}' must be a string like \"#RRGGBB\".");
            }
        }

        return colors;
    }

    private static ThemeLoadResult Failure(string source, string error) =>
        new(source, null, [error]);
}
