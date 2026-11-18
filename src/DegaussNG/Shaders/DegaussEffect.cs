using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace DegaussNG.Shaders;

/// <summary>
/// The WPF effect around the runtime-compiled shader: c0 amplitude, c1 rgb shift,
/// c2 brightness, c3 phase, sampler s0.
/// </summary>
internal sealed class DegaussEffect : ShaderEffect
{
    public static readonly DependencyProperty InputProperty =
        RegisterPixelShaderSamplerProperty("Input", typeof(DegaussEffect), 0);

    public static readonly DependencyProperty AmplitudeProperty =
        DependencyProperty.Register("Amplitude", typeof(double), typeof(DegaussEffect), new PropertyMetadata(0.0, PixelShaderConstantCallback(0)));

    public static readonly DependencyProperty RgbShiftProperty =
        DependencyProperty.Register("RgbShift", typeof(double), typeof(DegaussEffect), new PropertyMetadata(0.0, PixelShaderConstantCallback(1)));

    public static readonly DependencyProperty BrightnessProperty =
        DependencyProperty.Register("Brightness", typeof(double), typeof(DegaussEffect), new PropertyMetadata(0.0, PixelShaderConstantCallback(2)));

    public static readonly DependencyProperty PhaseProperty =
        DependencyProperty.Register("Phase", typeof(double), typeof(DegaussEffect), new PropertyMetadata(0.0, PixelShaderConstantCallback(3)));

    public DegaussEffect(PixelShader pixelShader)
    {
        PixelShader = pixelShader;

        UpdateShaderValue(InputProperty);
        UpdateShaderValue(AmplitudeProperty);
        UpdateShaderValue(RgbShiftProperty);
        UpdateShaderValue(BrightnessProperty);
        UpdateShaderValue(PhaseProperty);
    }

    public Brush Input
    {
        get => (Brush)GetValue(InputProperty);
        set => SetValue(InputProperty, value);
    }

    public double Amplitude
    {
        get => (double)GetValue(AmplitudeProperty);
        set => SetValue(AmplitudeProperty, value);
    }

    public double RgbShift
    {
        get => (double)GetValue(RgbShiftProperty);
        set => SetValue(RgbShiftProperty, value);
    }

    public double Brightness
    {
        get => (double)GetValue(BrightnessProperty);
        set => SetValue(BrightnessProperty, value);
    }

    public double Phase
    {
        get => (double)GetValue(PhaseProperty);
        set => SetValue(PhaseProperty, value);
    }
}
