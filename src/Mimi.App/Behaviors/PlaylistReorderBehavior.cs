using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Mimi.Behaviors;

/// <summary>The item and final zero-based position requested by a drag.</summary>
public sealed record PlaylistMoveRequest(object Item, int TargetPosition);

/// <summary>
/// Attached drag-and-drop reordering for the main track ListView. It keeps
/// WPF pointer/drag plumbing out of MainWindow code-behind and reports only
/// the requested final position to the view model.
/// </summary>
public static class PlaylistReorderBehavior
{
    private const string DragDataFormat = "Mimi.PlaylistTrack";

    private static Point dragStartPoint;
    private static ListViewItem? dragSourceItem;

    public static readonly DependencyProperty MoveCommandProperty =
        DependencyProperty.RegisterAttached(
            "MoveCommand",
            typeof(ICommand),
            typeof(PlaylistReorderBehavior),
            new PropertyMetadata(null, OnBehaviorChanged));

    public static readonly DependencyProperty IsReorderEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsReorderEnabled",
            typeof(bool),
            typeof(PlaylistReorderBehavior),
            new PropertyMetadata(false, OnBehaviorChanged));

    public static ICommand? GetMoveCommand(DependencyObject element) =>
        (ICommand?)element.GetValue(MoveCommandProperty);

    public static void SetMoveCommand(DependencyObject element, ICommand? value) =>
        element.SetValue(MoveCommandProperty, value);

    public static bool GetIsReorderEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsReorderEnabledProperty);

    public static void SetIsReorderEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsReorderEnabledProperty, value);

    private static void OnBehaviorChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not ListView listView) return;

        listView.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
        listView.PreviewMouseMove -= OnPreviewMouseMove;
        listView.DragOver -= OnDragOver;
        listView.Drop -= OnDrop;

        if (GetMoveCommand(listView) is not null && GetIsReorderEnabled(listView))
        {
            listView.AllowDrop = true;
            listView.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            listView.PreviewMouseMove += OnPreviewMouseMove;
            listView.DragOver += OnDragOver;
            listView.Drop += OnDrop;
        }
        else
        {
            listView.AllowDrop = false;
            dragSourceItem = null;
        }
    }

    private static void OnPreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        dragStartPoint = e.GetPosition(null);
        dragSourceItem =
            e.OriginalSource is DependencyObject source &&
            FindAncestor<ButtonBase>(source) is null
                ? FindAncestor<ListViewItem>(source)
                : null;
    }

    private static void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || dragSourceItem is null)
            return;

        var currentPoint = e.GetPosition(null);
        if (Math.Abs(currentPoint.X - dragStartPoint.X) <
                SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(currentPoint.Y - dragStartPoint.Y) <
                SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (sender is not ListView listView) return;

        var sourceItem = dragSourceItem;
        dragSourceItem = null;
        listView.SelectedItem = sourceItem.DataContext;

        var data = new DataObject(DragDataFormat, sourceItem.DataContext);
        DragDrop.DoDragDrop(listView, data, DragDropEffects.Move);
    }

    private static void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DragDataFormat)
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private static void OnDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (sender is not ListView listView ||
            !e.Data.GetDataPresent(DragDataFormat) ||
            e.Data.GetData(DragDataFormat) is not { } draggedItem)
        {
            return;
        }

        var sourceIndex = listView.Items.IndexOf(draggedItem);
        if (sourceIndex < 0 || listView.Items.Count == 0) return;

        int insertionIndex;
        var targetItem = e.OriginalSource is DependencyObject source
            ? FindAncestor<ListViewItem>(source)
            : null;
        if (targetItem is null)
        {
            insertionIndex = listView.Items.Count;
        }
        else
        {
            insertionIndex = listView.ItemContainerGenerator
                .IndexFromContainer(targetItem);
            if (insertionIndex < 0) return;

            var point = e.GetPosition(targetItem);
            if (point.Y > targetItem.ActualHeight / 2)
                insertionIndex++;
        }

        var targetIndex = insertionIndex;
        if (sourceIndex < insertionIndex)
            targetIndex--;

        targetIndex = Math.Clamp(targetIndex, 0, listView.Items.Count - 1);
        if (targetIndex == sourceIndex) return;

        var request = new PlaylistMoveRequest(draggedItem, targetIndex);
        var command = GetMoveCommand(listView);
        if (command?.CanExecute(request) == true)
            command.Execute(request);
    }

    private static T? FindAncestor<T>(DependencyObject? current)
        where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match) return match;
            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
