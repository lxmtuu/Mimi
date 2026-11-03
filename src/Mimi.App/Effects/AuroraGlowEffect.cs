using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Mimi.Effects;

/// <summary>
/// Limited aurora treatment for the Home hero accent layer. The companion
/// HLSL source is AuroraGlowEffect.fx; the compiled AuroraGlowEffect.ps file
/// is what ships as a WPF resource, so normal developer builds do not need
/// the Windows SDK shader compiler.
/// </summary>
public sealed class AuroraGlowEffect : ShaderEffect
{
    private static readonly Uri ShaderUri = new(
        "pack://application:,,,/Mimi;component/Effects/AuroraGlowEffect.ps",
        UriKind.Absolute);

    public static readonly DependencyProperty InputProperty =
        RegisterPixelShaderSamplerProperty(
            nameof(Input),
            typeof(AuroraGlowEffect),
            0);

    public static readonly DependencyProperty AccentColorProperty =
        DependencyProperty.Register(
            nameof(AccentColor),
            typeof(Color),
            typeof(AuroraGlowEffect),
            new UIPropertyMetadata(
                Color.FromRgb(0x47, 0xE6, 0xD0),
                PixelShaderConstantCallback(0)));

    public static readonly DependencyProperty SecondaryColorProperty =
        DependencyProperty.Register(
            nameof(SecondaryColor),
            typeof(Color),
            typeof(AuroraGlowEffect),
            new UIPropertyMetadata(
                Color.FromRgb(0xFF, 0x8F, 0xCB),
                PixelShaderConstantCallback(1)));

    public static readonly DependencyProperty StrengthProperty =
        DependencyProperty.Register(
            nameof(Strength),
            typeof(double),
            typeof(AuroraGlowEffect),
            new UIPropertyMetadata(0.42, PixelShaderConstantCallback(2)));

    public static readonly DependencyProperty PhaseProperty =
        DependencyProperty.Register(
            nameof(Phase),
            typeof(double),
            typeof(AuroraGlowEffect),
            new UIPropertyMetadata(0.18, PixelShaderConstantCallback(3)));

    public AuroraGlowEffect()
    {
        PixelShader = new PixelShader { UriSource = ShaderUri };

        UpdateShaderValue(InputProperty);
        UpdateShaderValue(AccentColorProperty);
        UpdateShaderValue(SecondaryColorProperty);
        UpdateShaderValue(StrengthProperty);
        UpdateShaderValue(PhaseProperty);
    }

    public Brush? Input
    {
        get => (Brush?)GetValue(InputProperty);
        set => SetValue(InputProperty, value);
    }

    public Color AccentColor
    {
        get => (Color)GetValue(AccentColorProperty);
        set => SetValue(AccentColorProperty, value);
    }

    public Color SecondaryColor
    {
        get => (Color)GetValue(SecondaryColorProperty);
        set => SetValue(SecondaryColorProperty, value);
    }

    /// <summary>How strongly the aurora tint replaces the source color.</summary>
    public double Strength
    {
        get => (double)GetValue(StrengthProperty);
        set => SetValue(StrengthProperty, value);
    }

    /// <summary>Static wave phase in cycles; 0 and 1 are the same point.</summary>
    public double Phase
    {
        get => (double)GetValue(PhaseProperty);
        set => SetValue(PhaseProperty, value);
    }
}
