namespace Mimi.ViewModels;

/// <summary>
/// One filter that is currently narrowing the library, drawn as a removable
/// chip next to the filter button. <see cref="Kind"/> is the token the clear
/// command switches on, so the chip never has to know about the view model.
/// </summary>
public sealed record LibraryFilterChip(string Kind, string Label, string Value)
{
    public string Text => $"{Label}: {Value}";
}
