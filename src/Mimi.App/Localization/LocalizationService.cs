using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;

namespace Mimi.Localization;

/// <summary>
/// Runtime access to Mimi's UI strings. WPF binds to this object through
/// its string indexer; changing the language raises the indexer change
/// notification so already-open views refresh without a restart.
/// </summary>
public sealed class LocalizationService : INotifyPropertyChanged
{
    private const string ResourceBaseName = "Mimi.Localization.UiStrings";

    private static readonly ResourceManager Resources = new(
        ResourceBaseName,
        typeof(LocalizationService).Assembly);

    private CultureInfo currentCulture;

    public LocalizationService()
    {
        currentCulture = UiLanguages.ResolveCulture(UiLanguages.System);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public CultureInfo CurrentCulture => currentCulture;

    public string CurrentCultureName => currentCulture.Name;

    public string this[string key] => GetString(key);

    public string GetString(string key) => GetString(key, currentCulture);

    public string GetString(string key, string cultureName) =>
        GetString(key, CultureInfo.GetCultureInfo(cultureName));

    public string Format(string key, params object?[] args) =>
        string.Format(currentCulture, GetString(key), args);

    public IReadOnlyList<string> GetKeys(string? cultureName)
    {
        var culture = string.IsNullOrWhiteSpace(cultureName)
            ? CultureInfo.InvariantCulture
            : CultureInfo.GetCultureInfo(cultureName);

        // ResourceManager owns and caches the returned set. Disposing it
        // here would poison later GetString calls with a closed set.
        var resourceSet = Resources.GetResourceSet(
            culture,
            createIfNotExists: true,
            tryParents: false);
        if (resourceSet is null) return [];

        return resourceSet
            .Cast<DictionaryEntry>()
            .Select(entry => entry.Key?.ToString())
            .OfType<string>()
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
    }

    public void SetLanguage(
        string? preference,
        CultureInfo? systemCulture = null)
    {
        var culture = UiLanguages.ResolveCulture(preference, systemCulture);
        var changed = !string.Equals(
            currentCulture.Name,
            culture.Name,
            StringComparison.OrdinalIgnoreCase);

        currentCulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        if (!changed) return;

        OnPropertyChanged(nameof(CurrentCulture));
        OnPropertyChanged(nameof(CurrentCultureName));
        OnPropertyChanged("Item[]");
        OnPropertyChanged(string.Empty);
    }

    private static string GetString(string key, CultureInfo culture)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;

        try
        {
            return Resources.GetString(key, culture) ?? key;
        }
        catch (MissingManifestResourceException)
        {
            return key;
        }
        catch (MissingSatelliteAssemblyException)
        {
            return key;
        }
    }

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
}
