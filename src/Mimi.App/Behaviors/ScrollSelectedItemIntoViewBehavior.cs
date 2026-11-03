using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Mimi.Behaviors;

/// <summary>
/// Keeps a ListBox selection visible without coupling view-model state to
/// WPF scrolling. When possible, the selected container is centered in the
/// viewport, which gives synchronized lyrics useful context on both sides.
/// </summary>
public static class ScrollSelectedItemIntoViewBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(ScrollSelectedItemIntoViewBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not ListBox listBox) return;

        listBox.SelectionChanged -= OnSelectionChanged;
        if (e.NewValue is true)
        {
            listBox.SelectionChanged += OnSelectionChanged;
            QueueScroll(listBox);
        }
    }

    private static void OnSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (sender is ListBox listBox)
            QueueScroll(listBox);
    }

    private static void QueueScroll(ListBox listBox)
    {
        if (listBox.SelectedItem is null) return;

        _ = listBox.Dispatcher.BeginInvoke(
            new Action(() => ScrollSelectedItemIntoView(listBox)),
            DispatcherPriority.ContextIdle);
    }

    private static void ScrollSelectedItemIntoView(ListBox listBox)
    {
        var selectedItem = listBox.SelectedItem;
        if (selectedItem is null) return;

        listBox.ScrollIntoView(selectedItem);
        listBox.UpdateLayout();

        if (listBox.ItemContainerGenerator.ContainerFromItem(selectedItem) is not
                FrameworkElement container ||
            FindDescendant<ScrollViewer>(listBox) is not { } scrollViewer)
        {
            return;
        }

        try
        {
            var itemTop = container
                .TransformToAncestor(scrollViewer)
                .Transform(new Point(0, 0)).Y;
            var centeredOffset =
                scrollViewer.VerticalOffset +
                itemTop -
                ((scrollViewer.ViewportHeight - container.ActualHeight) / 2);
            scrollViewer.ScrollToVerticalOffset(Math.Clamp(
                centeredOffset,
                0,
                scrollViewer.ScrollableHeight));
        }
        catch (InvalidOperationException)
        {
            // The container can detach between layout and this deferred
            // scroll while tracks change. ScrollIntoView above remains a
            // safe fallback for that frame.
        }
    }

    private static T? FindDescendant<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) return match;

            var descendant = FindDescendant<T>(child);
            if (descendant is not null) return descendant;
        }

        return null;
    }
}
