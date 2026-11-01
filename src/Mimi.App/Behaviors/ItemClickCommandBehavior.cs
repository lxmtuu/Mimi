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

    private static readonly MouseButtonEventHandler PreviewMouseDownHandler =
        OnPreviewMouseDown;

    private static readonly MouseEventHandler PreviewMouseMoveHandler =
        OnPreviewMouseMove;

    private static readonly MouseButtonEventHandler MouseUpHandler =
        OnMouseUp;

    // One-time diagnostics for the "first click after startup does nothing"
    // report: each distinct rejection reason is logged once per run so a
    // user-supplied log can pinpoint which half of the click was lost
    // without flooding the file on every drag or outside release.
    private static bool firstPressLogged;
    private static bool noRecordedPressLogged;
    private static bool dragThresholdLogged;

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

        itemsControl.RemoveHandler(
            Mouse.PreviewMouseDownEvent,
            PreviewMouseDownHandler);
        itemsControl.RemoveHandler(
            Mouse.PreviewMouseMoveEvent,
            PreviewMouseMoveHandler);
        itemsControl.RemoveHandler(Mouse.MouseUpEvent, MouseUpHandler);

        if (e.NewValue is ICommand)
        {
            // The button-specific PreviewMouseLeftButtonDown and
            // MouseLeftButtonUp events are Direct events that WPF re-raises
            // while the underlying tunneling PreviewMouseDown and bubbling
            // MouseUp events travel through the ItemsControl. If another
            // element marks one of those underlying events handled before
            // it reaches this control, an ordinary handler for the Direct
            // event can be skipped, losing one half of an otherwise
            // ordinary click (observed for the very first click after the
            // window opens). Listening to the underlying events with
            // handledEventsToo makes both halves of the click observable
            // while preserving the normal route order.
            itemsControl.AddHandler(
                Mouse.PreviewMouseDownEvent,
                PreviewMouseDownHandler,
                handledEventsToo: true);
            itemsControl.AddHandler(
                Mouse.PreviewMouseMoveEvent,
                PreviewMouseMoveHandler,
                handledEventsToo: true);
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

    private static void OnPreviewMouseDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;

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

        if (!firstPressLogged)
        {
            firstPressLogged = true;
            Log.Information(
                "First item press recorded after startup: itemType={ItemType}",
                item.GetType().Name);
        }
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

            if (!dragThresholdLogged)
            {
                dragThresholdLogged = true;
                Log.Information(
                    "Item click was not activated because pointer movement " +
                    "exceeded the drag threshold (first occurrence)");
            }
        }
    }

    private static void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;

        var item = pressItem;
        var belongsToPress = ReferenceEquals(sender, pressControl);
        pressControl = null;
        pressItem = null;

        if (sender is not ItemsControl itemsControl) return;
        if (!belongsToPress || item is null)
        {
            // A release over a row without a recorded press means the press
            // half of the click never reached this behavior (or began
            // outside the list). Log the first occurrence so startup-first-
            // click reports can distinguish a lost press from a lost
            // release or a rejected command.
            if (!noRecordedPressLogged &&
                e.OriginalSource is DependencyObject unpressedSource &&
                GetItemFromSource(itemsControl, unpressedSource) is not null)
            {
                noRecordedPressLogged = true;
                Log.Information(
                    "Item click was not activated because no matching press " +
                    "was recorded for this control (first occurrence)");
            }

            return;
        }
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
