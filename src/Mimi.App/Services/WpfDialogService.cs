using Microsoft.Win32;
using Mimi.Localization;

namespace Mimi.Services;

/// <summary>WPF implementation of the dialogs used by the main window.</summary>
public sealed class WpfDialogService : IDialogService
{
    private readonly LocalizationService loc;

    public WpfDialogService(LocalizationService? localizationService = null)
    {
        loc = localizationService ?? new LocalizationService();
    }

    public IReadOnlyList<string> PickAudioFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = loc["Dialog_ChooseMusicFiles"],
            Filter = $"{loc["Dialog_AudioFiles"]}|{SupportedAudioFiles.FileDialogPattern}|{loc["Dialog_AllFiles"]}|*.*",
            Multiselect = true
        };

        return dialog.ShowDialog() == true ? dialog.FileNames : [];
    }

    public string? PickMusicFolder()
    {
        // Built-in WPF folder picker (.NET 8); no WinForms dependency needed.
        var dialog = new OpenFolderDialog
        {
            Title = loc["Dialog_ChooseMusicFolder"]
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public string? PromptText(
        string title,
        string prompt,
        string initialValue = "")
    {
        var owner = System.Windows.Application.Current?.MainWindow;
        var dialog = new TextPromptWindow(title, prompt, initialValue, loc)
        {
            Owner = owner is { IsLoaded: true } ? owner : null
        };

        return dialog.ShowDialog() == true ? dialog.Value : null;
    }

    public bool Confirm(string message, string title) =>
        System.Windows.MessageBox.Show(
            message,
            title,
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question,
            System.Windows.MessageBoxResult.No) == System.Windows.MessageBoxResult.Yes;

    public void ShowInformation(string message, string title = "Mimi") =>
        System.Windows.MessageBox.Show(
            message,
            title,
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);

    public void ShowWarning(string message, string title) =>
        System.Windows.MessageBox.Show(
            message,
            title,
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Warning);
}
