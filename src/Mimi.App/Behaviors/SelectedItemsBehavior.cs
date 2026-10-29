using System.Collections;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace Mimi.Behaviors;

/// <summary>
/// Two-way synchronization for <see cref="ListBox.SelectedItems"/>, which is
/// read-only in WPF and therefore cannot be bound directly to a view model.
/// </summary>
public static class SelectedItemsBehavior
{
    private static readonly ConditionalWeakTable<ListBox, SyncState> States = new();

    public static readonly DependencyProperty SelectedItemsProperty =
        DependencyProperty.RegisterAttached(
            "SelectedItems",
            typeof(IList),
            typeof(SelectedItemsBehavior),
            new PropertyMetadata(null, OnSelectedItemsChanged));

    public static IList? GetSelectedItems(DependencyObject element) =>
        (IList?)element.GetValue(SelectedItemsProperty);

    public static void SetSelectedItems(DependencyObject element, IList? value) =>
        element.SetValue(SelectedItemsProperty, value);

    private static void OnSelectedItemsChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not ListBox listBox) return;

        var state = States.GetValue(
            listBox,
            key => new SyncState(key));

        listBox.SelectionChanged -= OnSelectionChanged;
        if (state.BoundNotifier is not null &&
            state.CollectionChangedHandler is not null)
        {
            state.BoundNotifier.CollectionChanged -=
                state.CollectionChangedHandler;
        }

        state.BoundItems = e.NewValue as IList;
        state.BoundNotifier = e.NewValue as INotifyCollectionChanged;
        state.CollectionChangedHandler ??=
            (_, _) => SyncBoundItemsToList(state);

        if (state.BoundItems is null) return;

        listBox.SelectionChanged += OnSelectionChanged;
        if (state.BoundNotifier is not null)
        {
            state.BoundNotifier.CollectionChanged +=
                state.CollectionChangedHandler;
        }

        SyncListToBoundItems(state);
    }

    private static void OnSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (sender is ListBox listBox &&
            States.TryGetValue(listBox, out var state))
        {
            SyncListToBoundItems(state);
        }
    }

    private static void SyncListToBoundItems(SyncState state)
    {
        if (state.IsSyncing || state.BoundItems is null) return;

        state.IsSyncing = true;
        try
        {
            state.BoundItems.Clear();
            foreach (var item in state.ListBox.SelectedItems)
                state.BoundItems.Add(item);
        }
        finally
        {
            state.IsSyncing = false;
        }
    }

    private static void SyncBoundItemsToList(SyncState state)
    {
        if (state.IsSyncing || state.BoundItems is null) return;

        state.IsSyncing = true;
        try
        {
            state.ListBox.SelectedItems.Clear();
            foreach (var item in state.BoundItems)
                state.ListBox.SelectedItems.Add(item);
        }
        finally
        {
            state.IsSyncing = false;
        }
    }

    private sealed class SyncState(ListBox listBox)
    {
        public ListBox ListBox { get; } = listBox;

        public IList? BoundItems { get; set; }

        public INotifyCollectionChanged? BoundNotifier { get; set; }

        public NotifyCollectionChangedEventHandler? CollectionChangedHandler
        {
            get;
            set;
        }

        public bool IsSyncing { get; set; }
    }
}
