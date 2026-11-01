using System.Globalization;
using Mimi.Localization;

namespace Mimi.Tests;

public sealed class UiLanguagesTests
{
    [Theory]
    [InlineData(null, UiLanguages.System)]
    [InlineData("", UiLanguages.System)]
    [InlineData(" system ", UiLanguages.System)]
    [InlineData("auto", UiLanguages.System)]
    [InlineData("default", UiLanguages.System)]
    [InlineData("en", UiLanguages.English)]
    [InlineData("EN_gb", UiLanguages.English)]
    [InlineData("en-US", UiLanguages.English)]
    [InlineData("vi", UiLanguages.Vietnamese)]
    [InlineData("vi-VN", UiLanguages.Vietnamese)]
    [InlineData("fr-FR", UiLanguages.System)]
    public void NormalizeAcceptsOnlySupportedPreferences(
        string? value,
        string expected) =>
        Assert.Equal(expected, UiLanguages.Normalize(value));

    [Theory]
    [InlineData(UiLanguages.English, "vi-VN", UiLanguages.English)]
    [InlineData(UiLanguages.Vietnamese, "en-US", UiLanguages.Vietnamese)]
    [InlineData(UiLanguages.System, "vi-VN", UiLanguages.Vietnamese)]
    [InlineData(UiLanguages.System, "fr-FR", UiLanguages.English)]
    [InlineData("unsupported", "vi-VN", UiLanguages.Vietnamese)]
    public void ResolveCultureNameUsesPreferenceThenSystemLanguage(
        string preference,
        string systemCultureName,
        string expected)
    {
        var systemCulture = CultureInfo.GetCultureInfo(systemCultureName);

        Assert.Equal(
            expected,
            UiLanguages.ResolveCultureName(preference, systemCulture));
    }
}
