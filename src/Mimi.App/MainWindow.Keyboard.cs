using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Mimi;

/// <summary>
/// Main-window keyboard shortcuts. Focus inspection is view responsibility;
/// every action delegates to an existing view-model command. Playback keys
/// are ignored while the user is typing or operating a control that already
/// gives arrows/Space a local meaning.
/// </summary>
public partial class MainWindow
{
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            ToggleStage();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && viewModel.IsSettingsOpen)
        {
            ExecuteCommand(viewModel.CloseSettingsCommand);
            e.Handled = true;
            return;
        }

        var modifiers = Keyboard.Modifiers;
        if (e.Key == Key.F &&
            modifiers.HasFlag(ModifierKeys.Control) &&
            !modifiers.HasFlag(ModifierKeys.Alt))
        {
            FocusSearch();
            e.Handled = true;
            return;
        }

        if (IsMediaKey(e.Key))
        {
            // Windows delivers the same hardware key through SMTC. When
            // SMTC is active it owns that copy; executing here too would
            // toggle or skip twice.
            if (mediaControls?.IsAvailable == true)
            {
                base.OnKeyDown(e);
                return;
            }

            ExecuteMediaKey(e.Key);
            e.Handled = true;
            return;
        }

        var focusSource =
            Keyboard.FocusedElement as DependencyObject ??
            e.OriginalSource as DependencyObject;
        var isTextInput = IsWithin<TextBoxBase>(focusSource) ||
                          IsWithin<ComboBox>(focusSource);

        if (modifiers.HasFlag(ModifierKeys.Control) &&
            !modifiers.HasFlag(ModifierKeys.Alt) &&
            !isTextInput)
        {
            if (e.Key == Key.Left)
            {
                ExecuteCommand(viewModel.PreviousCommand);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Right)
            {
                ExecuteCommand(viewModel.NextCommand);
                e.Handled = true;
                return;
            }
        }

        var isInteractiveControl = isTextInput ||
                                   IsWithin<ButtonBase>(focusSource) ||
                                   IsWithin<Slider>(focusSource) ||
                                   IsWithin<Selector>(focusSource);
        if (modifiers == ModifierKeys.None && !isInteractiveControl)
        {
            switch (e.Key)
            {
                case Key.Space:
                    ExecuteCommand(viewModel.PlayPauseCommand);
                    e.Handled = true;
                    return;
                case Key.Left:
                    ExecuteCommand(viewModel.SeekBackward5Command);
                    e.Handled = true;
                    return;
                case Key.Right:
                    ExecuteCommand(viewModel.SeekForward5Command);
                    e.Handled = true;
                    return;
                case Key.Up:
                    ExecuteCommand(viewModel.IncreaseVolumeCommand);
                    e.Handled = true;
                    return;
                case Key.Down:
                    ExecuteCommand(viewModel.DecreaseVolumeCommand);
                    e.Handled = true;
                    return;
            }
        }

        base.OnKeyDown(e);
    }

    private static bool IsMediaKey(Key key) => key is
        Key.MediaPlayPause or
        Key.MediaNextTrack or
        Key.MediaPreviousTrack or
        Key.MediaStop or
        Key.Play or
        Key.Pause;

    private void ExecuteMediaKey(Key key)
    {
        switch (key)
        {
            case Key.MediaPlayPause:
                ExecuteCommand(viewModel.PlayPauseCommand);
                break;
            case Key.Play:
                ExecuteCommand(viewModel.ResumePlaybackCommand);
                break;
            case Key.Pause:
                ExecuteCommand(viewModel.PausePlaybackCommand);
                break;
            case Key.MediaNextTrack:
                ExecuteCommand(viewModel.NextCommand);
                break;
            case Key.MediaPreviousTrack:
                ExecuteCommand(viewModel.PreviousCommand);
                break;
            case Key.MediaStop:
                ExecuteCommand(viewModel.PausePlaybackCommand);
                viewModel.SeekToPosition(TimeSpan.Zero);
                break;
        }
    }

    private void FocusSearch()
    {
        if (viewModel.IsSettingsOpen)
            ExecuteCommand(viewModel.CloseSettingsCommand);

        Dispatcher.BeginInvoke(
            new Action(() =>
            {
                SearchTextBox.Focus();
                SearchTextBox.SelectAll();
            }),
            DispatcherPriority.Input);
    }

    private static void ExecuteCommand(ICommand command)
    {
        if (command.CanExecute(null))
            command.Execute(null);
    }

    private static bool IsWithin<T>(DependencyObject? source)
        where T : DependencyObject
    {
        for (var current = source; current is not null;)
        {
            if (current is T) return true;

            current = current is Visual or Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return false;
    }
}
