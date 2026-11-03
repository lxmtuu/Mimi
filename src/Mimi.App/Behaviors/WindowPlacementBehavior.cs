using System.Runtime.CompilerServices;
using System.Windows;
using Mimi.Models;

namespace Mimi.Behaviors;

/// <summary>
/// Applies and tracks a window's persisted bounds without putting window
/// plumbing in either the view model or the window code-behind. Saved
/// bounds are clamped back into the current virtual screen so a disconnected
/// monitor cannot make Mimi open off-screen.
/// </summary>
public static class WindowPlacementBehavior
{
    private static readonly ConditionalWeakTable<Window, WindowPlacementState>
        States = new();

    public static readonly DependencyProperty PlacementProperty =
        DependencyProperty.RegisterAttached(
            "Placement",
            typeof(WindowPlacementSettings),
            typeof(WindowPlacementBehavior),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnPlacementChanged));

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(WindowPlacementBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static WindowPlacementSettings? GetPlacement(
        DependencyObject dependencyObject) =>
        (WindowPlacementSettings?)dependencyObject.GetValue(PlacementProperty);

    public static void SetPlacement(
        DependencyObject dependencyObject,
        WindowPlacementSettings? value) =>
        dependencyObject.SetValue(PlacementProperty, value);

    public static bool GetIsEnabled(DependencyObject dependencyObject) =>
        (bool)dependencyObject.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(
        DependencyObject dependencyObject,
        bool value) =>
        dependencyObject.SetValue(IsEnabledProperty, value);

    private static void OnPlacementChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not Window window) return;

        var state = GetOrCreateState(window);
        if (!state.IsUpdatingPlacement)
            state.TryApplyPlacement();
    }

    private static void OnIsEnabledChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not Window window) return;

        var state = GetOrCreateState(window);
        if (e.NewValue is true)
        {
            // Enabling the preference during a running session should start
            // remembering the current window, not jump it to an older spot.
            if (window.IsLoaded && state.HasSourceInitialized)
            {
                state.HasAppliedPlacement = true;
                state.UpdatePlacement();
            }
            else
            {
                state.TryApplyPlacement();
            }
        }
    }

    private static WindowPlacementState GetOrCreateState(Window window)
    {
        if (!States.TryGetValue(window, out var state))
        {
            state = new WindowPlacementState(window);
            States.Add(window, state);
            state.Attach();
        }

        return state;
    }

    private sealed class WindowPlacementState
    {
        public WindowPlacementState(Window window) => Window = window;

        private Window Window { get; }

        public bool HasAppliedPlacement { get; set; }

        public bool HasSourceInitialized { get; private set; }

        public bool IsUpdatingPlacement { get; private set; }

        private bool IsApplyingPlacement { get; set; }

        private bool IsAttached { get; set; }

        public void Attach()
        {
            if (IsAttached) return;
            IsAttached = true;

            Window.SourceInitialized += Window_SourceInitialized;
            Window.Loaded += Window_Loaded;
            Window.LocationChanged += Window_LocationChanged;
            Window.SizeChanged += Window_SizeChanged;
            Window.StateChanged += Window_StateChanged;
            Window.Closing += Window_Closing;
            Window.Closed += Window_Closed;
        }

        public void TryApplyPlacement()
        {
            if (HasAppliedPlacement || IsApplyingPlacement ||
                !GetIsEnabled(Window))
            {
                return;
            }

            var placement = GetPlacement(Window);
            if (placement?.HasBounds != true)
            {
                if (Window.IsLoaded)
                    HasAppliedPlacement = true;
                return;
            }

            IsApplyingPlacement = true;
            try
            {
                Window.WindowStartupLocation = WindowStartupLocation.Manual;
                if (Window.WindowState != WindowState.Normal)
                    Window.WindowState = WindowState.Normal;

                var virtualLeft = SystemParameters.VirtualScreenLeft;
                var virtualTop = SystemParameters.VirtualScreenTop;
                var virtualWidth = Math.Max(
                    1,
                    SystemParameters.VirtualScreenWidth);
                var virtualHeight = Math.Max(
                    1,
                    SystemParameters.VirtualScreenHeight);

                var width = Math.Clamp(
                    placement.Width!.Value,
                    Math.Max(1, Window.MinWidth),
                    Math.Max(Math.Max(1, Window.MinWidth), virtualWidth));
                var height = Math.Clamp(
                    placement.Height!.Value,
                    Math.Max(1, Window.MinHeight),
                    Math.Max(Math.Max(1, Window.MinHeight), virtualHeight));

                var minimumVisible = Math.Min(100, Math.Min(width, height));
                var left = Math.Clamp(
                    placement.Left!.Value,
                    virtualLeft - width + minimumVisible,
                    virtualLeft + Math.Max(0, virtualWidth - minimumVisible));
                var top = Math.Clamp(
                    placement.Top!.Value,
                    virtualTop - height + minimumVisible,
                    virtualTop + Math.Max(0, virtualHeight - minimumVisible));

                Window.Width = width;
                Window.Height = height;
                Window.Left = left;
                Window.Top = top;

                if (placement.IsMaximized)
                    Window.WindowState = WindowState.Maximized;

                HasAppliedPlacement = true;
            }
            finally
            {
                IsApplyingPlacement = false;
            }
        }

        public void UpdatePlacement()
        {
            if (!GetIsEnabled(Window) || !HasAppliedPlacement ||
                IsApplyingPlacement || IsUpdatingPlacement ||
                Window.WindowState == WindowState.Minimized)
            {
                return;
            }

            var bounds = Window.WindowState == WindowState.Normal
                ? new Rect(Window.Left, Window.Top, Window.Width, Window.Height)
                : Window.RestoreBounds;
            if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
                return;

            var isMaximized = Window.WindowState == WindowState.Maximized;
            var existing = GetPlacement(Window);
            if (existing?.HasBounds == true &&
                existing.IsMaximized == isMaximized &&
                AreClose(existing.Left!.Value, bounds.Left) &&
                AreClose(existing.Top!.Value, bounds.Top) &&
                AreClose(existing.Width!.Value, bounds.Width) &&
                AreClose(existing.Height!.Value, bounds.Height))
            {
                return;
            }

            IsUpdatingPlacement = true;
            try
            {
                Window.SetCurrentValue(
                    PlacementProperty,
                    new WindowPlacementSettings
                    {
                        Left = bounds.Left,
                        Top = bounds.Top,
                        Width = bounds.Width,
                        Height = bounds.Height,
                        IsMaximized = isMaximized
                    });
            }
            finally
            {
                IsUpdatingPlacement = false;
            }
        }

        private static bool AreClose(double left, double right) =>
            Math.Abs(left - right) < 0.5;

        private void Window_SourceInitialized(object? sender, EventArgs e)
        {
            HasSourceInitialized = true;
            TryApplyPlacement();
            if (GetIsEnabled(Window) && !HasAppliedPlacement &&
                GetPlacement(Window)?.HasBounds != true)
            {
                HasAppliedPlacement = true;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            TryApplyPlacement();
            if (GetIsEnabled(Window))
                HasAppliedPlacement = true;
        }

        private void Window_LocationChanged(object? sender, EventArgs e) =>
            UpdatePlacement();

        private void Window_SizeChanged(
            object sender,
            SizeChangedEventArgs e) =>
            UpdatePlacement();

        private void Window_StateChanged(object? sender, EventArgs e) =>
            UpdatePlacement();

        private void Window_Closing(
            object? sender,
            System.ComponentModel.CancelEventArgs e) =>
            UpdatePlacement();

        private void Window_Closed(object? sender, EventArgs e)
        {
            Window.SourceInitialized -= Window_SourceInitialized;
            Window.Loaded -= Window_Loaded;
            Window.LocationChanged -= Window_LocationChanged;
            Window.SizeChanged -= Window_SizeChanged;
            Window.StateChanged -= Window_StateChanged;
            Window.Closing -= Window_Closing;
            Window.Closed -= Window_Closed;
            IsAttached = false;
            States.Remove(Window);
        }
    }
}
