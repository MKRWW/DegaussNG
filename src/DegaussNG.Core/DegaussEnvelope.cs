namespace DegaussNG.Core;

/// <summary>
/// One frame of the effect: how much the picture is displaced, split into RGB and
/// lifted in brightness, plus the phase of the wobble.
/// </summary>
public readonly record struct DegaussFrame(
    double Amplitude,
    double RgbShift,
    double Brightness,
    double Phase);

/// <summary>
/// The decay curve of a degauss run. It is quiet before the delay and from the
/// total length on, and brightness is a single pulse that never rises again.
/// The delay covers the silent start of the sound and the relay, so the picture begins with the thump.
/// </summary>
public static class DegaussEnvelope
{
    public const double DelaySeconds = 0.29;
    public const double DecaySeconds = 1.78;
    public const double TotalSeconds = DelaySeconds + DecaySeconds;

    private const double DecayRate = 3.2;
    private const double BrightnessDecayRate = 9.0;
    private const double NormalAmplitude = 0.018;
    private const double NormalRgbShift = 0.006;
    private const double NormalBrightness = 0.22;
    private const double ReducedMotionFactor = 0.35;
    private const double PhaseRate = 38.0;

    public static DegaussFrame At(double seconds, bool reducedMotion)
    {
        double phase = seconds * PhaseRate;

        if (seconds < DelaySeconds || seconds >= TotalSeconds)
        {
            return new DegaussFrame(0.0, 0.0, 0.0, phase);
        }

        double p = (seconds - DelaySeconds) / DecaySeconds;
        double decay = Math.Exp(-DecayRate * p) * (1.0 - p);

        double amplitude = NormalAmplitude * decay;
        double rgbShift = NormalRgbShift * decay;
        double brightness = reducedMotion ? 0.0 : NormalBrightness * Math.Exp(-BrightnessDecayRate * p);

        if (reducedMotion)
        {
            amplitude *= ReducedMotionFactor;
            rgbShift *= ReducedMotionFactor;
        }

        return new DegaussFrame(amplitude, rgbShift, brightness, phase);
    }
}
