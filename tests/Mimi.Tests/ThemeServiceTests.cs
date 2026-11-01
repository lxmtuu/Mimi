using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using Mimi.Controls;
using Mimi.Services;
using Mimi.Theming;

namespace Mimi.Tests;

public sealed class ThemeServiceTests
{
    [Fact]
    public void ApplyPublishesEveryThemeColorAndDerivedAccentAlpha()
    {
        RunOnSta(() =>
        {
            var resources = new ResourceDictionary();
            var theme = BuiltInThemes.GetOrDefault(
                BuiltInThemes.SakuraDreamId);

            ThemeService.Apply(theme, resources);

            foreach (var token in ThemeTokens.RequiredColorTokens)
            {
                var resource = Assert.IsType<Color>(
                    resources[ThemeService.ColorResourceKey(token)]);
                Assert.Equal(ToColor(theme.GetColor(token)), resource);
            }

            var accent = theme.GetColor(ThemeTokens.Accent);
            Assert.Equal(
                ToColor(accent.WithAlpha(0x22)),
                Assert.IsType<Color>(resources["AccentDecorSoftColor"]));
            Assert.Equal(
                ToColor(accent.WithAlpha(0x66)),
                Assert.IsType<Color>(resources["AccentDecorMediumColor"]));
            Assert.Equal(
                ToColor(accent.WithAlpha(0x88)),
                Assert.IsType<Color>(resources["AccentDecorStrongColor"]));
            Assert.Equal(
                ToColor(accent.WithAlpha(0xAA)),
                Assert.IsType<Color>(resources["AccentDecorVividColor"]));
            Assert.Equal(theme.Id, Assert.IsType<string>(resources["MimiThemeId"]));
            Assert.Equal(theme.Name, Assert.IsType<string>(resources["MimiThemeName"]));
        });
    }

    [Fact]
    public void SharedBrushBoundToThemeColorUpdatesWhenThemeChanges()
    {
        RunOnSta(() =>
        {
            var resources = new ResourceDictionary();
            var neon = BuiltInThemes.Default;
            var mono = BuiltInThemes.GetOrDefault(BuiltInThemes.MonoFocusId);
            ThemeService.Apply(neon, resources);

            var brush = new SolidColorBrush();
            brush.SetResourceReference(
                SolidColorBrush.ColorProperty,
                ThemeService.ColorResourceKey(ThemeTokens.Accent));
            resources.Add(ThemeService.BrushResourceKey(ThemeTokens.Accent), brush);

            Assert.Equal(
                ToColor(neon.GetColor(ThemeTokens.Accent)),
                brush.Color);

            ThemeService.Apply(mono, resources);

            Assert.Equal(
                ToColor(mono.GetColor(ThemeTokens.Accent)),
                brush.Color);
        });
    }

    [Fact]
    public void SharedBrushInMergedDictionaryUpdatesWhenThemeChanges()
    {
        RunOnSta(() =>
        {
            var resources = new ResourceDictionary();
            var brushes = new ResourceDictionary();
            var brush = new SolidColorBrush();
            brush.SetResourceReference(
                SolidColorBrush.ColorProperty,
                ThemeService.ColorResourceKey(ThemeTokens.Accent));
            brushes.Add(
                ThemeService.BrushResourceKey(ThemeTokens.Accent),
                brush);
            resources.MergedDictionaries.Add(brushes);

            ThemeService.Apply(BuiltInThemes.Default, resources);
            Assert.Equal(
                ToColor(BuiltInThemes.Default.GetColor(ThemeTokens.Accent)),
                brush.Color);

            var sakura = BuiltInThemes.GetOrDefault(
                BuiltInThemes.SakuraDreamId);
            ThemeService.Apply(sakura, resources);
            Assert.Equal(
                ToColor(sakura.GetColor(ThemeTokens.Accent)),
                brush.Color);
        });
    }

    [Fact]
    public void InvalidThemeFallsBackToDefaultPalette()
    {
        RunOnSta(() =>
        {
            var resources = new ResourceDictionary();
            var invalid = new ThemeDefinition(
                "invalid-theme",
                "Invalid theme",
                new Dictionary<string, string>());

            ThemeService.Apply(invalid, resources);

            Assert.Equal(
                BuiltInThemes.DefaultId,
                Assert.IsType<string>(resources["MimiThemeId"]));
            Assert.Equal(
                ToColor(BuiltInThemes.Default.GetColor(
                    ThemeTokens.AppBackground)),
                Assert.IsType<Color>(resources[
                    ThemeService.ColorResourceKey(
                        ThemeTokens.AppBackground)]));
        });
    }

    [Fact]
    public void InstanceApplyNormalizesThemeAndRaisesChange()
    {
        RunOnSta(() =>
        {
            var service = new ThemeService();
            var changes = 0;
            service.ThemeChanged += (_, _) => changes++;

            var applied = service.Apply(" Sakura_Dream ");

            Assert.Equal(BuiltInThemes.SakuraDreamId, applied);
            Assert.Equal(BuiltInThemes.SakuraDreamId, service.CurrentThemeId);
            Assert.Equal(BuiltInThemes.SakuraDreamId, service.CurrentTheme.Id);
            Assert.Equal(1, changes);

            Assert.Equal(
                BuiltInThemes.DefaultId,
                service.Apply("theme-that-does-not-exist"));
            Assert.Equal(2, changes);
        });
    }

    [Fact]
    public void VisualizerRebuildsFrozenPaletteWhenThemeColorsChange()
    {
        RunOnSta(() =>
        {
            var view = new VisualizerView();
            var initialSpectrum = GetField<LinearGradientBrush>(
                view,
                "spectrumBrush");
            Assert.True(initialSpectrum.IsFrozen);
            Assert.Equal(
                Color.FromRgb(0x47, 0xE6, 0xD0),
                initialSpectrum.GradientStops[0].Color);

            view.PrimaryColor = Color.FromRgb(0x12, 0x34, 0x56);
            view.SecondaryColor = Color.FromRgb(0xAB, 0xCD, 0xEF);
            view.PeakColor = Color.FromRgb(0xFE, 0xDC, 0xBA);
            view.BaselineColor = Color.FromRgb(0x89, 0x9A, 0xBC);

            var updatedSpectrum = GetField<LinearGradientBrush>(
                view,
                "spectrumBrush");
            Assert.NotSame(initialSpectrum, updatedSpectrum);
            Assert.True(updatedSpectrum.IsFrozen);
            Assert.Equal(view.PrimaryColor, updatedSpectrum.GradientStops[0].Color);
            Assert.Equal(view.PeakColor, updatedSpectrum.GradientStops[2].Color);

            var peakPen = GetField<Pen>(view, "peakPen");
            Assert.True(peakPen.IsFrozen);
            Assert.Equal(
                view.SecondaryColor,
                Assert.IsType<SolidColorBrush>(peakPen.Brush).Color);

            var baselinePen = GetField<Pen>(view, "baselinePen");
            var baseline = Assert.IsType<SolidColorBrush>(
                baselinePen.Brush).Color;
            Assert.Equal(90, baseline.A);
            Assert.Equal(view.BaselineColor.R, baseline.R);
            Assert.Equal(view.BaselineColor.G, baseline.G);
            Assert.Equal(view.BaselineColor.B, baseline.B);
        });
    }

    private static T GetField<T>(object instance, string fieldName)
        where T : class
    {
        var field = instance.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<T>(field!.GetValue(instance));
    }

    private static Color ToColor(ThemeColor color) =>
        Color.FromArgb(color.A, color.R, color.G, color.B);

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
        {
            throw new Xunit.Sdk.XunitException(
                $"STA/WPF theme test failed: {failure}");
        }
    }
}
