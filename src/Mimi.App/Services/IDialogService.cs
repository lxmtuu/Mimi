namespace Mimi.Services;

/// <summary>Native dialogs used by view models, isolated for testability.</summary>
public interface IDialogService
{
    IReadOnlyList<string> PickAudioFiles();

    string? PickMusicFolder();

    void ShowInformation(string message, string title = "Mimi");

    void ShowWarning(string message, string title);
}
