namespace Mimi.Services;

/// <summary>
/// The single source of truth for audio extensions Mimi scans, offers in
/// file pickers, watches, and can register with Windows Explorer.
/// </summary>
public static class SupportedAudioFiles
{
    private static readonly string[] ExtensionArray =
    [
        ".mp3",
        ".wav",
        ".aiff",
        ".aif",
        ".wma",
        ".m4a",
        ".aac",
        ".flac"
    ];

    private static readonly HashSet<string> ExtensionSet =
        new(ExtensionArray, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> Extensions { get; } =
        Array.AsReadOnly(ExtensionArray);

    public static string FileDialogPattern { get; } = string.Join(
        ";",
        ExtensionArray.Select(extension => $"*{extension}"));

    public static bool IsSupported(string? filePath) =>
        !string.IsNullOrWhiteSpace(filePath) &&
        ExtensionSet.Contains(Path.GetExtension(filePath));
}
