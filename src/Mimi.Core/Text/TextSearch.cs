using System.Globalization;
using System.Text;

namespace Mimi.Core.Text;

/// <summary>Helpers for case- and Vietnamese-diacritic-insensitive search.</summary>
public static class TextSearch
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        // Vietnamese đ/Đ do not decompose into a base letter plus a combining
        // mark, so they require this explicit mapping before normalization.
        var prepared = value.Trim().Replace('đ', 'd').Replace('Đ', 'D');
        var decomposed = prepared.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var previousWasSpace = false;

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsWhiteSpace(character))
            {
                if (!previousWasSpace && builder.Length > 0) builder.Append(' ');
                previousWasSpace = true;
                continue;
            }

            builder.Append(character);
            previousWasSpace = false;
        }

        return builder.ToString().Trim().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    public static bool ContainsNormalized(string? haystack, string? normalizedQuery)
    {
        if (string.IsNullOrWhiteSpace(normalizedQuery)) return true;
        return Normalize(haystack).Contains(normalizedQuery, StringComparison.Ordinal);
    }
}
