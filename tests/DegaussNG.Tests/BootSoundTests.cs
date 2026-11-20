using System;
using System.Text;
using DegaussNG.SoundSynth;
using Xunit;

namespace DegaussNG.Tests;

public sealed class BootSoundTests
{
    private const int SampleRate = BootSound.SampleRate;

    [Fact]
    public void RenderHasExactlyTheDeclaredLength()
    {
        short[] samples = BootSound.Render();

        Assert.Equal(114660, samples.Length);
        Assert.Equal(BootSound.DurationSeconds, samples.Length / (double)SampleRate);
    }

    [Fact]
    public void TwoCallsAreIdentical()
    {
        Assert.Equal(BootSound.Render(), BootSound.Render());
    }

    [Fact]
    public void TheCommittedWavFileIsPcmMonoAt22050AndMatchesTheRender()
    {
        byte[] wav = Asset("boot.wav");

        Assert.Equal(WavWriter.Build(BootSound.Render(), SampleRate), wav);

        Assert.Equal("RIFF", Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal("WAVE", Encoding.ASCII.GetString(wav, 8, 4));
        Assert.Equal("fmt ", Encoding.ASCII.GetString(wav, 12, 4));
        Assert.Equal("data", Encoding.ASCII.GetString(wav, 36, 4));
        Assert.Equal(1, BitConverter.ToInt16(wav, 20));
        Assert.Equal(1, BitConverter.ToInt16(wav, 22));
        Assert.Equal(SampleRate, BitConverter.ToInt32(wav, 24));
        Assert.Equal(16, BitConverter.ToInt16(wav, 34));
    }

    [Fact]
    public void TheSilentStartStaysSilentAndTheFadeEndsOnZero()
    {
        short[] samples = BootSound.Render();
        int silent = Index(0.20);

        for (int i = 0; i < silent; i++)
            Assert.Equal(0, samples[i]);

        double peak = 0.0;

        foreach (short sample in samples)
            peak = Math.Max(peak, Math.Abs((double)sample));

        double reference = 32767.0 * Math.Pow(10.0, -6.0 / 20.0);

        Assert.True(peak <= reference + 0.5, $"peak {peak:F1} is above -6 dBFS");
        Assert.Equal(0, samples[^1]);
    }

    [Fact]
    public void BeepHoldsThePowerOnTone()
    {
        short[] samples = BootSound.Render();
        int start = Index(0.20);
        int end = Index(0.35);

        double tone = Power(samples, start, end, 1000.0);
        double other = Power(samples, start, end, 1500.0);

        Assert.True(tone >= 10.0 * other, $"1000 Hz is {tone / other:F1} times 1500 Hz");
    }

    [Fact]
    public void BetweenTheBeepAndTheMotorNothingIsAboveTwo()
    {
        short[] samples = BootSound.Render();
        int start = Index(0.50);
        int end = Index(3.15);

        for (int i = start; i < end; i++)
            Assert.True(Math.Abs((double)samples[i]) <= 2.0, $"sample {i} is {samples[i]}");
    }

    // AC13
    [Fact]
    public void TheSeekIsAtLeastTwiceAsLoudAsTheMotorAlone()
    {
        short[] samples = BootSound.Render();

        double seek = Rms(samples, Index(3.30), Index(4.05));
        double motor = Rms(samples, Index(4.10), Index(4.30));

        Assert.True(seek >= 2.0 * motor, $"seek rms {seek:F1} against motor rms {motor:F1}");
    }

    // AC16
    [Fact]
    public void NoFrequencyOfTheSeekHoldsAFifthOfThePower()
    {
        short[] samples = BootSound.Render();
        int start = Index(3.30);
        int end = Index(4.05);

        double[] powers = new double[77];
        double sum = 0.0;

        for (int i = 0; i < powers.Length; i++)
        {
            powers[i] = Goertzel(samples, start, end, 200.0 + 50.0 * i);
            sum += powers[i];
        }

        for (int i = 0; i < powers.Length; i++)
        {
            double frequency = 200.0 + 50.0 * i;
            Assert.True(powers[i] <= 0.20 * sum, $"{frequency:F0} Hz holds {100.0 * powers[i] / sum:F1} percent");
        }
    }

    // AC17
    [Fact]
    public void EveryStepOfPassAIsASeparateClick()
    {
        short[] samples = BootSound.Render();

        for (int step = 0; step < 12; step++)
        {
            int start = Index(3.30 + step * 0.020 - 0.001);
            int end = start + Index(0.020);

            double peak = 0.0;
            int peakAt = start;

            for (int i = start; i < end; i++)
            {
                double magnitude = Math.Abs((double)samples[i]);

                if (magnitude > peak)
                {
                    peak = magnitude;
                    peakAt = i;
                }
            }

            Assert.True(peakAt < start + Index(0.004), $"step {step}: peak is at {(peakAt - start) / SampleRate * 1000.0:F1} ms");

            double late = 0.0;

            for (int i = start + Index(0.013); i < start + Index(0.019); i++)
                late = Math.Max(late, Math.Abs((double)samples[i]));

            Assert.True(late <= peak / 5.0, $"step {step}: late {late:F1} against peak {peak:F1}");
        }
    }

    private static double Rms(short[] samples, int start, int end)
    {
        double sum = 0.0;

        for (int i = start; i < end; i++)
            sum += (double)samples[i] * samples[i];

        return Math.Sqrt(sum / (end - start));
    }

    private static double Power(short[] samples, int start, int end, double frequency)
    {
        double omega = 2.0 * Math.PI * frequency / SampleRate;
        int length = end - start;

        double real = 0.0;
        double imaginary = 0.0;

        for (int n = start; n < end; n++)
        {
            double weight = 0.5 - 0.5 * Math.Cos(2.0 * Math.PI * (n - start) / length);
            real += samples[n] * weight * Math.Cos(omega * n);
            imaginary += samples[n] * weight * Math.Sin(omega * n);
        }

        return real * real + imaginary * imaginary;
    }

    private static double Goertzel(short[] samples, int start, int end, double frequency)
    {
        double omega = 2.0 * Math.PI * frequency / SampleRate;
        double coefficient = 2.0 * Math.Cos(omega);

        double previous = 0.0;
        double before = 0.0;

        for (int n = start; n < end; n++)
        {
            double current = samples[n] + coefficient * previous - before;
            before = previous;
            previous = current;
        }

        return previous * previous + before * before - coefficient * previous * before;
    }

    private static int Index(double seconds)
    {
        return (int)Math.Round(seconds * SampleRate);
    }

    private static byte[] Asset(string fileName)
    {
        string? directory = AppContext.BaseDirectory;

        while (directory != null && !File.Exists(Path.Combine(directory, "assets", "sounds", fileName)))
            directory = Path.GetDirectoryName(directory);

        Assert.NotNull(directory);

        return File.ReadAllBytes(Path.Combine(directory!, "assets", "sounds", fileName));
    }
}
