using Microsoft.Win32;

namespace Mimi.Services;

/// <summary>WPF implementation of the dialogs used by the main window.</summary>
public sealed class WpfDialogService : IDialogService
{
    public IReadOnlyList<string> PickAudioFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose music files",
            Filter = "Audio files|*.mp3;*.wav;*.aiff;*.aif;*.wma;*.m4a;*.aac;*.flac|All files|*.*",
            Multiselect = true
        };

        return dialog.ShowDialog() == true ? dialog.FileNames : [];
    }

    public string? PickMusicFolder()
    {
        // Built-in WPF folder picker (.NET 8); no WinForms dependency needed.
        var dialog = new OpenFolderDialog
        {
            Title = "Choose a folder containing your music"
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

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
