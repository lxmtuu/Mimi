using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Serilog;

namespace Mimi.Behaviors;

/// <summary>
/// Executes a command with the clicked item when the user completes a plain
/// left-click on an item container (single-click activation). The command
/// parameter is the data item presented by that container. Clicks combined with Ctrl or
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

    private static readonly MouseButtonEventHandler MouseUpHandler =
        OnMouseUp;

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
        itemsControl.RemoveHandler(Mouse.MouseUpEvent, MouseUpHandler);

        if (e.NewValue is ICommand)
        {
            itemsControl.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            itemsControl.PreviewMouseMove += OnPreviewMouseMove;

            // MouseLeftButtonUp is a Direct event that WPF re-raises while
            // the underlying bubbling Mouse.MouseUp event travels from the
            // row to the ItemsControl. If an element in the row/template
            // marks Mouse.MouseUp handled, an ordinary MouseLeftButtonUp
            // handler on the parent can be skipped even though the row was
            // selected. Listening to the underlying bubbling event with
            // handledEventsToo makes click completion observable in that
            // case as well, while preserving the normal bubble order.
            itemsControl.AddHandler(
                Mouse.MouseUpEvent,
                MouseUpHandler,
                handledEventsToo: true);
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

        var item = GetItemFromSource(itemsControl, source);
        if (item is null) return;

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

    private static void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;

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

        var releaseItem = GetItemFromSource(itemsControl, source);
        if (releaseItem is null)
        {
            // A container can be in the middle of being recycled when the
            // OriginalSource is resolved. Re-run hit testing at the actual
            // release position before giving up on an otherwise ordinary
            // click.
            var hitResult = VisualTreeHelper.HitTest(
                itemsControl,
                e.GetPosition(itemsControl));
            if (hitResult?.VisualHit is DependencyObject hitSource &&
                FindAncestor<ButtonBase>(hitSource) is null)
            {
                releaseItem = GetItemFromSource(itemsControl, hitSource);
            }
        }

        if (releaseItem is null &&
            itemsControl is Selector selector &&
            ReferenceEquals(selector.SelectedItem, item))
        {
            // Selection is applied on mouse-down. If container lookup is
            // unavailable at release because of recycling, the selector's
            // selection is still authoritative evidence that this completed
            // press targeted the recorded item.
            releaseItem = item;
        }

        if (!ReferenceEquals(releaseItem, item))
        {
            Log.Information(
                "Item click was not activated because the release item " +
                "did not match the pressed item: pressedType={PressedType} " +
                "releaseType={ReleaseType}",
                item.GetType().Name,
                releaseItem?.GetType().Name ?? "<null>");
            return;
        }

        var command = GetCommand(itemsControl);
        if (command?.CanExecute(item) == true)
        {
            command.Execute(item);
            return;
        }

        Log.Information(
            "Item click was not activated because its command could not " +
            "execute: commandPresent={CommandPresent} itemType={ItemType}",
            command is not null,
            item.GetType().FullName);
    }

    private static object? GetItemFromSource(
        ItemsControl itemsControl,
        DependencyObject source)
    {
        var container = ItemsControl.ContainerFromElement(
            itemsControl,
            source);
        if (container is null) return null;

        // ItemFromContainer is the canonical container-to-data mapping. Do
        // not rely on FrameworkElement.DataContext: templates/styles can
        // override it, and WPF replaces it with a disconnected sentinel
        // while containers are being recycled.
        var item = itemsControl.ItemContainerGenerator
            .ItemFromContainer(container);
        return item == DependencyProperty.UnsetValue ||
               ReferenceEquals(item, BindingOperations.DisconnectedSource)
            ? null
            : item;
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
