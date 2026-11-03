namespace Mimi.YouTube;

/// <summary>
/// Shape rules for the user's own YouTube Data API key, shared by the Settings
/// box and the file reader so both accept exactly the same thing.
///
/// The rules are deliberately only about shape, not about validity: Mimi cannot
/// know whether Google still accepts a key. Catching copy/paste damage here
/// (a trailing space, a curly quote, a line break) turns the most common
/// mistake into an immediate, understandable message instead of a failed
/// search later.
/// </summary>
public static class YouTubeApiKey
{
    /// <summary>Shortest plausible key. Google's current keys are 39 characters.</summary>
    public const int MinimumLength = 20;

    /// <summary>Longest value Mimi will store, to catch pasted junk.</summary>
    public const int MaximumLength = 100;

    /// <summary>
    /// Returns the trimmed key, or null when the text cannot be a key. Only
    /// printable ASCII without spaces is accepted: that rejects newlines,
    /// non-breaking spaces, and the typographic quotes some pages copy.
    /// </summary>
    public static string? Normalize(string? raw)
    {
        var trimmed = raw?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        if (trimmed.Length is < MinimumLength or > MaximumLength) return null;

        foreach (var character in trimmed)
        {
            if (character is < (char)0x21 or > (char)0x7E) return null;
        }

        return trimmed;
    }
}
