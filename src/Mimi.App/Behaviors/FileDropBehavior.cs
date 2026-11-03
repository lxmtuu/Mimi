using System.Windows;
using System.Windows.Input;

namespace Mimi.Behaviors;

/// <summary>
/// Attached behavior that sends Explorer file/folder drops to a command.
/// This keeps drag-and-drop view plumbing declarative and out of
/// MainWindow's code-behind.
/// </summary>
public static class FileDropBehavior
{
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached(
            "Command",
            typeof(ICommand),
            typeof(FileDropBehavior),
            new PropertyMetadata(null, OnCommandChanged));

    public static ICommand? GetCommand(DependencyObject element) =>
        (ICommand?)element.GetValue(CommandProperty);

    public static void SetCommand(DependencyObject element, ICommand? value) =>
        element.SetValue(CommandProperty, value);

    private static void OnCommandChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not FrameworkElement element) return;

        element.DragOver -= OnDragOver;
        element.Drop -= OnDrop;

        if (e.NewValue is ICommand)
        {
            element.AllowDrop = true;
            element.DragOver += OnDragOver;
            element.Drop += OnDrop;
        }
        else
        {
            element.AllowDrop = false;
        }
    }

    private static void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private static void OnDrop(object sender, DragEventArgs e)
    {
        if (sender is DependencyObject element &&
            e.Data.GetDataPresent(DataFormats.FileDrop) &&
            e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            var command = GetCommand(element);
            if (command?.CanExecute(paths) == true)
                command.Execute(paths);
        }

        e.Handled = true;
    }
}
