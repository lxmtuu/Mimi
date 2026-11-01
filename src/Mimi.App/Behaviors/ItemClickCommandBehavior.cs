using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Mimi.Behaviors;

/// <summary>
/// Executes a command with the clicked item when the user completes a plain
/// left-click on an item container (single-click activation). The command
/// parameter is the container's data context. Clicks combined with Ctrl or
/// Shift stay selection-only so multi-select batch actions keep working,
/// movement beyond the system drag threshold cancels activation so drag
/// reordering never plays a track, and clicks landing on buttons inside a
/// row (favorite, queue actions) are ignored.
/// </summary>
public static class ItemClickCommandBehavior
{
    private static ItemsControl? pressControl;

    // Track the item view model rather than the visual container. During
    // startup the track view can refresh between mouse-down and mouse-up,
    // which may recycle or recreate the ListViewItem even though the user
    // never left the row. The item identity remains stable across that
    // container churn, while still preventing activation when the pointer
    // is released over a different row.
    private static object? pressItem;
    private static Point pressPoint;

    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached(
            "Command",
            typeof(ICommand),
            typeof(ItemClickCommandBehavior),
            new PropertyMetadata(null, OnCommandChanged));

    public static ICommand? GetCommand(DependencyObject element) =>
        (ICommand?)element.GetValue(CommandProperty);

    public static void SetCommand(DependencyObject element, ICommand? value) =>
        element.SetValue(CommandProperty, value);

    private static void OnCommandChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not ItemsControl itemsControl) return;

        itemsControl.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
        itemsControl.PreviewMouseMove -= OnPreviewMouseMove;
        itemsControl.MouseLeftButtonUp -= OnMouseLeftButtonUp;

        if (e.NewValue is ICommand)
        {
            itemsControl.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            itemsControl.PreviewMouseMove += OnPreviewMouseMove;
            itemsControl.MouseLeftButtonUp += OnMouseLeftButtonUp;
        }
        else if (ReferenceEquals(pressControl, itemsControl))
        {
            pressControl = null;
            pressItem = null;
        }
    }

    private static void OnPreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        pressControl = null;
        pressItem = null;

        if (sender is not ItemsControl itemsControl) return;
        if (Keyboard.Modifiers != ModifierKeys.None) return;
        if (e.OriginalSource is not DependencyObject source) return;
        if (FindAncestor<ButtonBase>(source) is not null) return;

        var container = ItemsControl.ContainerFromElement(itemsControl, source);
        if (container is not FrameworkElement { DataContext: { } item }) return;

        pressControl = itemsControl;
        pressItem = item;
        pressPoint = e.GetPosition(null);
    }

    private static void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (pressItem is null ||
            !ReferenceEquals(sender, pressControl) ||
            e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        if (IsBeyondDragThreshold(e.GetPosition(null)))
        {
            pressControl = null;
            pressItem = null;
        }
    }

    private static void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var item = pressItem;
        var belongsToPress = ReferenceEquals(sender, pressControl);
        pressControl = null;
        pressItem = null;

        if (!belongsToPress || item is null) return;
        if (sender is not ItemsControl itemsControl) return;
        if (Keyboard.Modifiers != ModifierKeys.None) return;
        if (IsBeyondDragThreshold(e.GetPosition(null))) return;
        if (e.OriginalSource is not DependencyObject source) return;
        if (FindAncestor<ButtonBase>(source) is not null) return;

        var releaseContainer = ItemsControl.ContainerFromElement(
            itemsControl,
            source);
        var releaseItem =
            (releaseContainer as FrameworkElement)?.DataContext;
        if (!ReferenceEquals(releaseItem, item)) return;

        var command = GetCommand(itemsControl);
        if (command?.CanExecute(item) == true)
            command.Execute(item);
    }

    private static bool IsBeyondDragThreshold(Point position) =>
        Math.Abs(position.X - pressPoint.X) >
            SystemParameters.MinimumHorizontalDragDistance ||
        Math.Abs(position.Y - pressPoint.Y) >
            SystemParameters.MinimumVerticalDragDistance;

    private static T? FindAncestor<T>(DependencyObject source)
        where T : DependencyObject
    {
        for (var current = source; current is not null;)
        {
            if (current is T match) return match;

            current = current is Visual
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return null;
    }
}
