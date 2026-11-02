using CommunityToolkit.Mvvm.ComponentModel;

namespace Mimi.ViewModels;

/// <summary>
/// One selectable entry in the Settings theme picker. The id is the stable
/// value persisted in settings; the name is display-only and refreshes when
/// the UI language changes (built-in themes use localized names, community
/// themes keep the name from their JSON file).
/// </summary>
public sealed partial class ThemeOptionViewModel : ObservableObject
{
    public ThemeOptionViewModel(string id, string displayName, bool isBuiltIn)
    {
        Id = id;
        name = displayName;
        IsBuiltIn = isBuiltIn;
    }

    public string Id { get; }

    public bool IsBuiltIn { get; }

    [ObservableProperty]
    private string name;
}
