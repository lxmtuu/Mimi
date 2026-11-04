using System.Windows;
using System.Windows.Input;
using Serilog;

namespace Mimi.Services;

/// <summary>
/// Recovers from the one WPF fault that has no user-facing answer.
///
/// <c>KeyboardNavigation.ShowFocusVisual</c> reads
/// <c>FrameworkElement.FocusVisualStyle</c> whenever keyboard focus moves - and
/// reactivating a window counts. If the value it finds is
/// <c>DependencyProperty.UnsetValue</c>, the read throws
/// <c>InvalidOperationException</c>, from inside the input pipeline, before any
/// of the app's own code runs.
///
/// Showing a dialog there cannot help: the user did nothing, and the dialog
/// offers nothing to do. What helps is (1) putting a real value on the element
/// that failed, so the read succeeds and the ring simply looks right, and
/// (2) logging *which* element it was, so the underlying source can be found.
///
/// The local value set here wins over whatever the style supplied, which is why
/// this works without knowing which style was at fault.
/// </summary>
public static class FocusVisualRepair
{
    /// <summary>The resource key of Mimi's focus ring.</summary>
    public const string FocusVisualStyleKey = "MimiFocusVisualStyle";

    private const string PropertyName = "FocusVisualStyle";

    /// <summary>
    /// True for the focus-ring fault: an <see cref="InvalidOperationException"/>
    /// complaining about that property, anywhere in the exception chain.
    ///
    /// The framework's message text is not a stable contract between locales, so
    /// the property name is matched and the stack is used as a second chance -
    /// either one is enough.
    /// </summary>
    public static bool IsUnsetFocusVisualStyle(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is not InvalidOperationException) continue;

            if (current.Message.Contains(PropertyName, StringComparison.Ordinal))
                return true;

            if (current.StackTrace?.Contains(
                    "KeyboardNavigation.ShowFocusVisual",
                    StringComparison.Ordinal) == true)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Names the element the framework was reading when it failed: its type, its
    /// name if it has one, and the style that was meant to paint it. This is the
    /// line that turns a mystery into a place in the XAML.
    /// </summary>
    public static string Identify(object? element)
    {
        if (element is not DependencyObject dependencyObject)
            return "no focused element";

        var type = dependencyObject.GetType().Name;
        var name = (dependencyObject as FrameworkElement)?.Name;
        var label = string.IsNullOrWhiteSpace(name) ? type : $"{type} '{name}'";

        if (dependencyObject is FrameworkElement { Style: { } style })
        {
            var target = style.TargetType?.Name ?? "any type";
            label += $" (style for {target})";
        }

        return label;
    }

    /// <summary>
    /// Gives the focused element a concrete focus-ring value so the next read
    /// cannot throw, and returns true when it did.
    ///
    /// The value is the app's own ring when the resource is available, and
    /// <c>null</c> when it is not: losing the ring is a cosmetic loss, losing the
    /// window is not.
    /// </summary>
    public static bool TryRepair(Exception? exception, out string element)
    {
        element = "no focused element";
        if (!IsUnsetFocusVisualStyle(exception)) return false;
        if (Keyboard.FocusedElement is not DependencyObject focused) return false;

        element = Identify(focused);
        if (!Repair(focused)) return false;

        Log.Warning(
            "Focus ring had no value on {Element}; replaced it with the app's own style. " +
            "The element's style is the place to look: {StyleKey}",
            element,
            FocusVisualStyleKey);

        return true;
    }

    /// <summary>
    /// Sets a real value for the ring on one element. Exposed separately so a
    /// test can exercise it without an input pipeline.
    /// </summary>
    public static bool Repair(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);

        var ring = Application.Current?.TryFindResource(FocusVisualStyleKey) as Style;

        try
        {
            element.SetValue(FrameworkElement.FocusVisualStyleProperty, ring);

            // Reading it back is the only proof: the point is that this read no
            // longer throws.
            _ = (element as FrameworkElement)?.FocusVisualStyle;
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
