using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Mimi.Tests;

/// <summary>
/// The focus ring has one failure mode, and it is nasty because it only shows
/// up for keyboard users:
///
/// WPF reads <c>FocusVisualStyle</c> when focus moves *by keyboard* - a mouse
/// click never asks for it. If the setter's value came from a
/// <c>StaticResource</c> that was not visible when the style was materialised,
/// the value is <c>DependencyProperty.UnsetValue</c>, and reading it throws
/// "'{DependencyProperty.UnsetValue}' is not a valid value for property
/// 'FocusVisualStyle'". That is why the owner only met it after pressing F11
/// and closing the Stage: closing a window reactivates the other one, which
/// counts as a keyboard focus change.
///
/// The cure is not "catch it": it is to make the value unable to be unset -
/// define the key before anything that uses it, and reference it with a
/// DynamicResource so the worst case is a missing ring, never a crash.
/// </summary>
public sealed class FocusVisualStyleTests
{
    [Fact]
    public void EveryFocusVisualStyleReferenceIsResolvedAtRuntime()
    {
        var offenders = new List<string>();
        foreach (var file in EnumerateXaml())
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (!lines[i].Contains("FocusVisualStyle", StringComparison.Ordinal))
                    continue;
                if (lines[i].Contains("{StaticResource", StringComparison.Ordinal))
                    offenders.Add($"{Relative(file)}:{i + 1}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "A StaticResource can resolve to DependencyProperty.UnsetValue and " +
            "throw on the first keyboard focus. Use DynamicResource " +
            "(or {x:Null}) for FocusVisualStyle. Offenders:\n" +
            string.Join("\n", offenders));
    }

    [Fact]
    public void TheFocusVisualStyleIsDefinedExactlyOnce()
    {
        var definitions = EnumerateXaml()
            .Where(file => File.ReadAllText(file)
                .Contains("x:Key=\"MimiFocusVisualStyle\"", StringComparison.Ordinal))
            .ToArray();

        Assert.Single(definitions);
        Assert.EndsWith(
            Path.Combine("Themes", "FocusVisual.xaml"),
            definitions[0],
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheFocusVisualDictionaryIsMergedBeforeEverythingThatUsesIt()
    {
        var app = File.ReadAllText(Path.Combine(
            AppRoot(),
            "App.xaml"));

        var focus = app.IndexOf("Themes/FocusVisual.xaml", StringComparison.Ordinal);
        var brushes = app.IndexOf("Themes/ThemeBrushes.xaml", StringComparison.Ordinal);
        var controls = app.IndexOf("Themes/ControlStyles.xaml", StringComparison.Ordinal);

        Assert.True(focus > 0, "App.xaml must merge Themes/FocusVisual.xaml.");
        Assert.True(
            focus < brushes && focus < controls,
            "The focus ring dictionary has to be merged before the dictionaries " +
            "whose styles reference the key.");
    }

    [Fact]
    public void TheFocusRingTakesItsColourFromTheTheme()
    {
        var text = File.ReadAllText(Path.Combine(
            AppRoot(),
            "Themes",
            "FocusVisual.xaml"));

        // No literal colour: the ring belongs to whichever palette is active.
        Assert.DoesNotContain("#", text, StringComparison.Ordinal);
        Assert.Contains("{DynamicResource AccentBrush}", text, StringComparison.Ordinal);
        Assert.Contains("SnapsToDevicePixels=\"True\"", text, StringComparison.Ordinal);
    }

    private static IEnumerable<string> EnumerateXaml() =>
        Directory.EnumerateFiles(AppRoot(), "*.xaml", SearchOption.AllDirectories);

    private static string AppRoot() =>
        Path.Combine(FindRepositoryRoot(), "src", "Mimi.App");

    private static string Relative(string file) =>
        Path.GetRelativePath(FindRepositoryRoot(), file);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Mimi.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new Xunit.Sdk.XunitException(
            "Could not locate the repository root (Mimi.sln) above " +
            AppContext.BaseDirectory);
    }
}
