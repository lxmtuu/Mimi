using System.Text.Json;
using Mimi.Theming;

namespace Mimi.Tests;

public sealed class CommunityThemeLoaderTests
{
    [Fact]
    public void Parse_ValidTheme_Succeeds()
    {
        var json = BuildThemeJson(
            "alpha-one",
            "Alpha One",
            colors => colors[ThemeTokens.Accent] = "#123456");

        var result = CommunityThemeLoader.Parse(json, "alpha-one.json");

        Assert.True(result.Success);
        Assert.Empty(result.Errors);
        var theme = result.Theme!;
        Assert.Equal("alpha-one", theme.Id);
        Assert.Equal("Alpha One", theme.Name);
        Assert.Equal(
            ThemeColor.Parse("#123456"),
            theme.GetColor(ThemeTokens.Accent));
    }

    [Fact]
    public void Parse_MissingSchemaVersion_IsTreatedAsVersionOne()
    {
        var json = BuildThemeJson(
            "alpha-one",
            "Alpha One",
            schemaVersion: null);

        var result = CommunityThemeLoader.Parse(json, "alpha-one.json");

        Assert.True(result.Success);
    }

    [Fact]
    public void Parse_MalformedJson_FailsWithoutThrowing()
    {
        var result = CommunityThemeLoader.Parse("{ not json", "broken.json");

        Assert.False(result.Success);
        Assert.Null(result.Theme);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Parse_UnsupportedSchemaVersion_Fails()
    {
        var json = BuildThemeJson(
            "alpha-one",
            "Alpha One",
            schemaVersion: CommunityThemeLoader.CurrentSchemaVersion + 1);

        var result = CommunityThemeLoader.Parse(json, "alpha-one.json");

        Assert.False(result.Success);
        Assert.Contains(
            result.Errors,
            error => error.Contains("schemaVersion", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_MissingToken_Fails()
    {
        var json = BuildThemeJson(
            "alpha-one",
            "Alpha One",
            colors => colors.Remove(ThemeTokens.Accent));

        var result = CommunityThemeLoader.Parse(json, "alpha-one.json");

        Assert.False(result.Success);
        Assert.Contains(
            result.Errors,
            error => error.Contains(ThemeTokens.Accent, StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_UnknownToken_Fails()
    {
        var json = BuildThemeJson(
            "alpha-one",
            "Alpha One",
            colors => colors["Banana"] = "#000000");

        var result = CommunityThemeLoader.Parse(json, "alpha-one.json");

        Assert.False(result.Success);
        Assert.Contains(
            result.Errors,
            error => error.Contains("Banana", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_InvalidColor_Fails()
    {
        var json = BuildThemeJson(
            "alpha-one",
            "Alpha One",
            colors => colors[ThemeTokens.Accent] = "red");

        var result = CommunityThemeLoader.Parse(json, "alpha-one.json");

        Assert.False(result.Success);
        Assert.Contains(
            result.Errors,
            error => error.Contains(ThemeTokens.Accent, StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_NonStringColor_Fails()
    {
        var colors = new Dictionary<string, object?>();
        foreach (var pair in BuiltInThemes.Default.Colors)
            colors[pair.Key] = pair.Value;
        colors[ThemeTokens.Accent] = 42;
        var json = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["schemaVersion"] = 1,
            ["id"] = "alpha-one",
            ["name"] = "Alpha One",
            ["colors"] = colors
        });

        var result = CommunityThemeLoader.Parse(json, "alpha-one.json");

        Assert.False(result.Success);
        Assert.Contains(
            result.Errors,
            error => error.Contains(ThemeTokens.Accent, StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_InvalidId_Fails()
    {
        var json = BuildThemeJson("My Theme", "Alpha One");

        var result = CommunityThemeLoader.Parse(json, "alpha-one.json");

        Assert.False(result.Success);
        Assert.Contains(
            result.Errors,
            error => error.Contains("id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LoadDirectory_LoadsJsonFilesInNameOrderAndSkipsOthers()
    {
        var directory = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(directory, "b.json"),
                BuildThemeJson("beta-two", "Beta Two"));
            File.WriteAllText(
                Path.Combine(directory, "a.json"),
                BuildThemeJson("alpha-one", "Alpha One"));
            File.WriteAllText(
                Path.Combine(directory, "broken.json"),
                "{ not json");
            File.WriteAllText(
                Path.Combine(directory, "notes.txt"),
                BuildThemeJson("gamma-three", "Gamma Three"));

            var results = CommunityThemeLoader.LoadDirectory(directory);

            Assert.Equal(3, results.Count);
            Assert.Equal(
                ["alpha-one", "beta-two"],
                results.Where(result => result.Success)
                    .Select(result => result.Theme!.Id)
                    .ToArray());
            Assert.Contains(results, result => !result.Success);
        }
        finally
        {
            TryDelete(directory);
        }
    }

    [Fact]
    public void LoadDirectory_MissingDirectory_ReturnsEmpty()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "MimiThemeLoaderTests-" + Guid.NewGuid().ToString("N"));

        var results = CommunityThemeLoader.LoadDirectory(directory);

        Assert.Empty(results);
    }

    [Fact]
    public void LoadFile_OversizedFile_Fails()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "huge.json");
            File.WriteAllText(
                path,
                new string(
                    ' ',
                    CommunityThemeLoader.MaximumFileSizeBytes + 1));

            var result = CommunityThemeLoader.LoadFile(path);

            Assert.False(result.Success);
            Assert.Contains(
                result.Errors,
                error => error.Contains("limit", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            TryDelete(directory);
        }
    }

    internal static string BuildThemeJson(
        string id,
        string name,
        Action<Dictionary<string, string>>? mutateColors = null,
        int? schemaVersion = 1)
    {
        var colors = new Dictionary<string, string>(BuiltInThemes.Default.Colors);
        mutateColors?.Invoke(colors);

        var payload = new Dictionary<string, object?>
        {
            ["id"] = id,
            ["name"] = name,
            ["colors"] = colors
        };
        if (schemaVersion is { } version)
            payload["schemaVersion"] = version;

        return JsonSerializer.Serialize(payload);
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "MimiThemeLoaderTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void TryDelete(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort cleanup of the per-test temp folder.
        }
    }
}
