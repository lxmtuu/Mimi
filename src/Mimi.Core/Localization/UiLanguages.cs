using System.Globalization;

namespace Mimi.Localization;

/// <summary>
/// Supported interface-language preferences persisted in settings.json.
/// Preferences are stored as stable culture names (or <see cref="System"/>)
/// so older settings files can be repaired without trusting arbitrary text.
/// </summary>
public static class UiLanguages
{
    public const string System = "system";
    public const string English = "en-US";
    public const string Vietnamese = "vi-VN";
    public const string Default = System;

    private static readonly string[] SupportedPreferenceValues =
        [System, English, Vietnamese];

    public static IReadOnlyList<string> SupportedPreferences =>
        SupportedPreferenceValues;

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Default;

        var cleaned = value.Trim().Replace('_', '-');
        if (cleaned.Equals(System, StringComparison.OrdinalIgnoreCase) ||
            cleaned.Equals("auto", StringComparison.OrdinalIgnoreCase) ||
            cleaned.Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            return System;
        }

        var language = cleaned.Split('-')[0];
        if (language.Equals("vi", StringComparison.OrdinalIgnoreCase))
            return Vietnamese;
        if (language.Equals("en", StringComparison.OrdinalIgnoreCase))
            return English;

        return System;
    }

    public static string ResolveCultureName(
        string? preference,
        CultureInfo? systemCulture = null)
    {
        var normalized = Normalize(preference);
        if (!string.Equals(normalized, System, StringComparison.Ordinal))
            return normalized;

        var culture = systemCulture ?? CultureInfo.CurrentUICulture;
        return culture.TwoLetterISOLanguageName.Equals(
            "vi",
            StringComparison.OrdinalIgnoreCase)
            ? Vietnamese
            : English;
    }

    public static CultureInfo ResolveCulture(
        string? preference,
        CultureInfo? systemCulture = null) =>
        CultureInfo.GetCultureInfo(ResolveCultureName(preference, systemCulture));
}
