using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using Mimi.Theming;
using Mimi.Services;

namespace Mimi.Tests;

/// <summary>
/// Guards the brushes themselves, not just the colour keys. A theme that
/// publishes correctly but leaves brushes painting the previous palette is
/// exactly the "half themed window" this covers: pink cards inside navy panels,
/// with the Windows caption already showing the new theme.
/// </summary>
public sealed class ThemeBrushSyncTests
{
    [Fact]
    public void EveryBrushInTheThemeFileIsInTheTable()
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(root, "src", "Mimi.App", "Themes", "ThemeBrushes.xaml");
        var xaml = File.ReadAllText(path);

        var declared = Regex
            .Matches(xaml, "x:Key=\"([A-Za-z]+Brush)\"", RegexOptions.CultureInvariant)
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(declared);
        Assert.Equal(
            declared.OrderBy(key => key, StringComparer.Ordinal),
            ThemeBrushSync.Map.Keys.OrderBy(key => key, StringComparer.Ordinal));
    }

    [Fact]
    public void TheTableMatchesTheStopsTheFileDeclares()
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(root, "src", "Mimi.App", "Themes", "ThemeBrushes.xaml");
        var dictionary = (ResourceDictionary)XamlReader.Parse(File.ReadAllText(path));

        foreach (var (brushKey, colorKeys) in ThemeBrushSync.Map)
        {
            var brush = Assert.IsAssignableFrom<Brush>(dictionary[brushKey]);

            if (brush is SolidColorBrush)
            {
                Assert.Single(colorKeys);
                continue;
            }

            var gradient = Assert.IsAssignableFrom<GradientBrush>(brush);
            Assert.Equal(gradient.GradientStops.Count, colorKeys.Length);
        }
    }

    [Fact]
    public void EveryMappedColourIsActuallyPublished()
    {
        RunOnSta(() =>
        {
            var dictionary = LoadBrushes();
            ThemeService.Apply(BuiltInThemes.Default, dictionary);

            foreach (var (brushKey, colorKeys) in ThemeBrushSync.Map)
            {
                foreach (var colorKey in colorKeys)
                {
                    Assert.True(
                        dictionary.Contains(colorKey),
                        $"{brushKey} needs {colorKey}, which the theme does not publish.");
                }
            }
        });
    }

    [Fact]
    public void BrushesFollowTheThemeAcrossSwitches()
    {
        RunOnSta(() =>
        {
            var dictionary = LoadBrushes();

            foreach (var theme in new[]
                     {
                         BuiltInThemes.Default,
                         BuiltInThemes.GetOrDefault(BuiltInThemes.SakuraDreamId),
                         BuiltInThemes.GetOrDefault(BuiltInThemes.MidnightLiveId),
                         BuiltInThemes.GetOrDefault(BuiltInThemes.MonoFocusId),
                         BuiltInThemes.Default
                     })
            {
                ThemeService.Apply(theme, dictionary);

                foreach (var (brushKey, colorKeys) in ThemeBrushSync.Map)
                {
                    var brush = Assert.IsAssignableFrom<Brush>(dictionary[brushKey]);

                    if (brush is SolidColorBrush solid)
                    {
                        Assert.Equal(
                            Assert.IsType<Color>(dictionary[colorKeys[0]]),
                            solid.Color);
                        continue;
                    }

                    var gradient = Assert.IsAssignableFrom<GradientBrush>(brush);
                    for (var i = 0; i < colorKeys.Length; i++)
                    {
                        Assert.Equal(
                            Assert.IsType<Color>(dictionary[colorKeys[i]]),
                            gradient.GradientStops[i].Color);
                    }
                }
            }
        });
    }

    [Fact]
    public void BrushesAreNeverFrozenSoTheyCanBeRepainted()
    {
        RunOnSta(() =>
        {
            var dictionary = LoadBrushes();
            ThemeService.Apply(BuiltInThemes.Default, dictionary);

            foreach (var brushKey in ThemeBrushSync.Map.Keys)
            {
                var brush = Assert.IsAssignableFrom<Brush>(dictionary[brushKey]);
                Assert.False(
                    brush.IsFrozen,
                    $"{brushKey} is frozen, so a live theme change cannot reach it.");
            }
        });
    }

    [Fact]
    public void ATintedThemeReachesTheGradientsToo()
    {
        RunOnSta(() =>
        {
            var dictionary = LoadBrushes();
            ThemeSurfaceCustomizer.TryParse("#FF8FCB", out var pink);
            var tinted = ThemeSurfaceCustomizer.Apply(BuiltInThemes.Default, pink);

            ThemeService.Apply(tinted, dictionary);

            // The hero is the largest block of colour on the screen; if it keeps
            // the old hue the app looks like two themes stitched together.
            var hero = Assert.IsAssignableFrom<GradientBrush>(dictionary["HeroGradientBrush"]);
            foreach (var stop in hero.GradientStops)
            {
                Assert.True(
                    stop.Color.R > stop.Color.B,
                    $"Hero stop {stop.Color} is not pink after a pink background.");
            }

            var cover = Assert.IsAssignableFrom<GradientBrush>(dictionary["CoverGradientBrush"]);
            foreach (var stop in cover.GradientStops)
            {
                Assert.True(
                    stop.Color.R > stop.Color.B,
                    $"Cover stop {stop.Color} is not pink after a pink background.");
            }
        });
    }

    private static ResourceDictionary LoadBrushes()
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(root, "src", "Mimi.App", "Themes", "ThemeBrushes.xaml");
        return (ResourceDictionary)XamlReader.Parse(File.ReadAllText(path));
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
            throw new Xunit.Sdk.XunitException(failure.ToString());
    }

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
