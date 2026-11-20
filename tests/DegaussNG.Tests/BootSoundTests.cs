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
        int silent = (int)Math.Round(0.20 * SampleRate);

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
        int start = (int)Math.Round(0.20 * SampleRate);
        int end = (int)Math.Round(0.35 * SampleRate);

        double tone = Power(samples, start, end, 1000.0);
        double other = Power(samples, start, end, 1500.0);

        Assert.True(tone >= 10.0 * other, $"1000 Hz is {tone / other:F1} times 1500 Hz");
    }

    [Fact]
    public void BetweenTheBeepAndTheMotorNothingIsAboveTwo()
    {
        short[] samples = BootSound.Render();
        int start = (int)Math.Round(0.50 * SampleRate);
        int end = (int)Math.Round(3.15 * SampleRate);

        for (int i = start; i < end; i++)
            Assert.True(Math.Abs((double)samples[i]) <= 2.0, $"sample {i} is {samples[i]}");
    }

    [Fact]
    public void TheSeekIsClearlyLouderThanTheMotorAlone()
    {
        short[] samples = BootSound.Render();

        double seek = Rms(samples, (int)Math.Round(3.30 * SampleRate), (int)Math.Round(4.45 * SampleRate));
        double motor = Rms(samples, (int)Math.Round(4.50 * SampleRate), (int)Math.Round(4.85 * SampleRate));

        Assert.True(seek >= 2.0 * motor, $"seek rms {seek:F1} against motor rms {motor:F1}");
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

    private static byte[] Asset(string fileName)
    {
        string? directory = AppContext.BaseDirectory;

        while (directory != null && !File.Exists(Path.Combine(directory, "assets", "sounds", fileName)))
            directory = Path.GetDirectoryName(directory);

        Assert.NotNull(directory);

        return File.ReadAllBytes(Path.Combine(directory!, "assets", "sounds", fileName));
    }
}
