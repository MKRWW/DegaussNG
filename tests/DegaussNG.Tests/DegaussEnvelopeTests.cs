using DegaussNG.Core;
using Xunit;

namespace DegaussNG.Tests;

public sealed class DegaussEnvelopeTests
{
    private const double Tolerance = 1e-12;

    private static int DelayMillisecond => (int)Math.Round(DegaussEnvelope.DelaySeconds * 1000.0);

    private static int LastMillisecond => (int)Math.Round(DegaussEnvelope.TotalSeconds * 1000.0);

    [Fact]
    public void EverythingButPhaseIsZeroBeforeTheDelay()
    {
        DegaussFrame frame = DegaussEnvelope.At(0.0, false);

        Assert.Equal(0.0, frame.Amplitude);
        Assert.Equal(0.0, frame.RgbShift);
        Assert.Equal(0.0, frame.Brightness);
        Assert.Equal(0.0, frame.Phase);
    }

    [Fact]
    public void EverythingButPhaseIsZeroFromTotalSecondsOn()
    {
        DegaussFrame frame = DegaussEnvelope.At(DegaussEnvelope.TotalSeconds, false);

        Assert.Equal(0.0, frame.Amplitude);
        Assert.Equal(0.0, frame.RgbShift);
        Assert.Equal(0.0, frame.Brightness);
        Assert.Equal(DegaussEnvelope.TotalSeconds * 38.0, frame.Phase);
    }

    [Fact]
    public void AmplitudeNeverRisesAfterItsPeak()
    {
        List<double> samples = new();

        for (int i = 0; i <= LastMillisecond; i++)
        {
            samples.Add(DegaussEnvelope.At(i / 1000.0, false).Amplitude);
        }

        int peakIndex = 0;
        for (int i = 1; i < samples.Count; i++)
        {
            if (samples[i] > samples[peakIndex])
            {
                peakIndex = i;
            }
        }

        Assert.True(peakIndex == DelayMillisecond, $"amplitude peaks at t={peakIndex / 1000.0}");
        Assert.True(samples[peakIndex] > 0.0, "the envelope never rises above zero");

        for (int i = peakIndex; i < samples.Count; i++)
        {
            Assert.True(samples[i] <= samples[peakIndex] + Tolerance, $"amplitude rose at t={i / 1000.0}");
        }
    }

    [Fact]
    public void BrightnessIsOneSinglePulseThatNeverRisesAgain()
    {
        double previous = DegaussEnvelope.At(DegaussEnvelope.DelaySeconds, false).Brightness;

        Assert.True(previous > 0.0);

        for (int i = DelayMillisecond + 1; i <= LastMillisecond; i++)
        {
            double brightness = DegaussEnvelope.At(i / 1000.0, false).Brightness;

            Assert.True(brightness <= previous + Tolerance, $"brightness rose at t={i / 1000.0}");
            previous = brightness;
        }
    }

    [Fact]
    public void ReducedMotionKeepsBrightnessAtZeroAtEverySampledTime()
    {
        for (int i = 0; i <= LastMillisecond; i++)
        {
            DegaussFrame frame = DegaussEnvelope.At(i / 1000.0, true);

            Assert.Equal(0.0, frame.Brightness);
        }
    }

    [Fact]
    public void ReducedMotionLimitsAmplitudeAndRgbShiftToThirtyFivePercent()
    {
        for (int i = 0; i <= LastMillisecond; i++)
        {
            double seconds = i / 1000.0;

            DegaussFrame normal = DegaussEnvelope.At(seconds, false);
            DegaussFrame reduced = DegaussEnvelope.At(seconds, true);

            Assert.True(reduced.Amplitude <= normal.Amplitude * 0.35 + Tolerance, $"amplitude too high at t={seconds}");
            Assert.True(reduced.RgbShift <= normal.RgbShift * 0.35 + Tolerance, $"rgb shift too high at t={seconds}");
        }

        DegaussFrame peak = DegaussEnvelope.At(DegaussEnvelope.DelaySeconds, true);
        Assert.True(peak.Amplitude > 0.0);
    }
}
