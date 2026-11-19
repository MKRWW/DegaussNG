using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DegaussNG.SoundSynth;
using Xunit;

namespace DegaussNG.Tests;

public sealed class ModemSoundTests
{
    private const int SampleRate = ModemSound.SampleRate;

    private static readonly double[] Rows = { 697.0, 770.0, 852.0, 941.0 };
    private static readonly double[] Columns = { 1209.0, 1336.0, 1477.0 };

    [Fact]
    public void ShortLastsBetweenFiveAndFiveAndAHalfSeconds()
    {
        double seconds = ModemSound.RenderShort().Length / (double)SampleRate;

        Assert.True(seconds >= 5.0 && seconds <= 5.5, $"short is {seconds:F3} s");
    }

    [Fact]
    public void FullLastsBetweenTwentyFourAndAHalfAndTwentySixSeconds()
    {
        double seconds = ModemSound.RenderFull().Length / (double)SampleRate;

        Assert.True(seconds >= 24.5 && seconds <= 26.0, $"full is {seconds:F3} s");
    }

    [Fact]
    public void BothModesAreIdenticalAcrossTwoCalls()
    {
        Assert.Equal(ModemSound.RenderShort(), ModemSound.RenderShort());
        Assert.Equal(ModemSound.RenderFull(), ModemSound.RenderFull());
    }

    [Fact]
    public void TheCommittedWavFilesArePcmMonoAt22050AndMatchTheRender()
    {
        byte[] shortFile = Asset("modem-short.wav");
        byte[] fullFile = Asset("modem-full.wav");

        Assert.Equal(WavWriter.Build(ModemSound.RenderShort(), SampleRate), shortFile);
        Assert.Equal(WavWriter.Build(ModemSound.RenderFull(), SampleRate), fullFile);

        foreach (byte[] wav in new[] { shortFile, fullFile })
        {
            Assert.Equal("RIFF", Encoding.ASCII.GetString(wav, 0, 4));
            Assert.Equal("WAVE", Encoding.ASCII.GetString(wav, 8, 4));
            Assert.Equal("fmt ", Encoding.ASCII.GetString(wav, 12, 4));
            Assert.Equal("data", Encoding.ASCII.GetString(wav, 36, 4));
            Assert.Equal(1, BitConverter.ToInt16(wav, 20));
            Assert.Equal(1, BitConverter.ToInt16(wav, 22));
            Assert.Equal(SampleRate, BitConverter.ToInt32(wav, 24));
            Assert.Equal(16, BitConverter.ToInt16(wav, 34));
        }
    }

    [Fact]
    public void TheFirstTwoHundredMillisecondsAreSilent()
    {
        AssertSilent(ModemSound.RenderShort(), 0.20);
        AssertSilent(ModemSound.RenderFull(), 0.20);
    }

    [Fact]
    public void ThePeakIsAtMostMinusSixDecibfsAndTheLastSampleIsZero()
    {
        AssertPeak(ModemSound.RenderShort());
        AssertPeak(ModemSound.RenderFull());
    }

    [Fact]
    public void DialtoneHoldsBothTonesInBothModes()
    {
        foreach (bool full in new[] { false, true })
        {
            IReadOnlyList<ModemSection> sections = full ? ModemSound.FullSections : ModemSound.ShortSections;
            short[] samples = full ? ModemSound.RenderFull() : ModemSound.RenderShort();

            (int start, int length) = Window(sections, "dialtone");

            double low = Power(samples, start, start + length, 350.0);
            double high = Power(samples, start, start + length, 440.0);
            double other = Power(samples, start, start + length, 1000.0);

            Assert.True(low >= 5.0 * other, $"350 Hz: {low / other:F1} times 1000 Hz");
            Assert.True(high >= 5.0 * other, $"440 Hz: {high / other:F1} times 1000 Hz");
        }
    }

    [Fact]
    public void DtmfDecodesTo5550199InBothModes()
    {
        Assert.Equal("5550199", DecodeDigits(ModemSound.ShortSections, ModemSound.RenderShort(), 0.07, 0.03));
        Assert.Equal("5550199", DecodeDigits(ModemSound.FullSections, ModemSound.RenderFull(), 0.12, 0.08));
    }

    [Fact]
    public void DtmfDigitsAreSeparatedByPausesInBothModes()
    {
        AssertDtmfPauses(ModemSound.ShortSections, ModemSound.RenderShort(), 0.07, 0.03);
        AssertDtmfPauses(ModemSound.FullSections, ModemSound.RenderFull(), 0.12, 0.08);
    }

    private static void AssertDtmfPauses(IReadOnlyList<ModemSection> sections, short[] samples, double onSeconds, double offSeconds)
    {
        (int start, int _) = Window(sections, "dtmf");

        for (int digit = 0; digit < 6; digit++)
        {
            double digitStart = digit * (onSeconds + offSeconds);

            int toneFrom = start + (int)Math.Round(digitStart * SampleRate);
            int toneTo = start + (int)Math.Round((digitStart + onSeconds) * SampleRate);
            int pauseFrom = start + (int)Math.Round((digitStart + onSeconds + 0.01) * SampleRate);
            int pauseTo = start + (int)Math.Round((digitStart + onSeconds + offSeconds) * SampleRate);

            double tone = Rms(samples, toneFrom, toneTo);
            double pause = Rms(samples, pauseFrom, pauseTo);

            Assert.True(pause <= 0.1 * tone, $"digit {digit}: pause rms {pause:F1}, tone rms {tone:F1}");
        }
    }

    private static double Rms(short[] samples, int start, int end)
    {
        double sum = 0.0;

        for (int i = start; i < end; i++)
            sum += (double)samples[i] * samples[i];

        return Math.Sqrt(sum / (end - start));
    }

    [Fact]
    public void AnswerIsTheCarrierInBothModes()
    {
        foreach (bool full in new[] { false, true })
        {
            IReadOnlyList<ModemSection> sections = full ? ModemSound.FullSections : ModemSound.ShortSections;
            short[] samples = full ? ModemSound.RenderFull() : ModemSound.RenderShort();

            (int start, int length) = Window(sections, "answer");

            double carrier = Power(samples, start, start + length, 2100.0);

            Assert.True(carrier >= 20.0 * Power(samples, start, start + length, 1000.0), "2100 Hz against 1000 Hz");
            Assert.True(carrier >= 20.0 * Power(samples, start, start + length, 3000.0), "2100 Hz against 3000 Hz");
        }
    }

    [Fact]
    public void AnswerReversesItsPhaseAfterTheFirstReversal()
    {
        short[] samples = ModemSound.RenderFull();
        (int start, int length) = Window(ModemSound.FullSections, "answer");

        int reversal = (int)Math.Round(0.45 * SampleRate);
        int window = (int)Math.Round(0.05 * SampleRate);

        double before = Phase(samples, start + reversal - window, start + reversal, 2100.0);
        double after = Phase(samples, start + reversal, start + reversal + window, 2100.0);

        double difference = Math.Abs(Normalize(after - before));

        Assert.True(Math.Abs(difference - Math.PI) <= 0.5, $"phase difference {difference:F3} rad");
    }

    [Fact]
    public void BongStartsWithTheLowerToneAndThenAlternates()
    {
        foreach (bool full in new[] { false, true })
        {
            IReadOnlyList<ModemSection> sections = full ? ModemSound.FullSections : ModemSound.ShortSections;
            short[] samples = full ? ModemSound.RenderFull() : ModemSound.RenderShort();

            (int start, int length) = Window(sections, "bong");
            int slot = (int)Math.Round(0.15 * SampleRate);

            double firstLow = Power(samples, start, start + slot, 1200.0);
            double firstHigh = Power(samples, start, start + slot, 2400.0);

            double secondLow = Power(samples, start + slot, start + 2 * slot, 1200.0);
            double secondHigh = Power(samples, start + slot, start + 2 * slot, 2400.0);

            Assert.True(firstLow > firstHigh, $"first slot: 1200 Hz {firstLow / firstHigh:F1} times 2400 Hz");
            Assert.True(secondHigh > secondLow, $"second slot: 2400 Hz {secondHigh / secondLow:F1} times 1200 Hz");
        }
    }

    [Fact]
    public void TrainingHasNoSingleDominantFrequency()
    {
        short[] samples = ModemSound.RenderFull();
        (int start, int length) = Window(ModemSound.FullSections, "training");

        double total = 0.0;
        double maximum = 0.0;

        for (double frequency = 300.0; frequency <= 3400.0; frequency += 50.0)
        {
            double power = Power(samples, start, start + length, frequency);
            total += power;

            if (power > maximum)
                maximum = power;
        }

        Assert.True(maximum / total <= 0.20, $"largest bin is {maximum / total:F3} of the sum");

        double above = Power(samples, start, start + length, 5000.0);
        double average = total / 63.0;

        Assert.True(above < 0.1 * average, $"5000 Hz is {above / average:F5} of the average bin");
    }

    private static void AssertSilent(short[] samples, double seconds)
    {
        int length = (int)Math.Round(seconds * SampleRate);

        for (int i = 0; i < length; i++)
            Assert.Equal(0, samples[i]);
    }

    private static void AssertPeak(short[] samples)
    {
        double peak = 0.0;

        foreach (short sample in samples)
            peak = Math.Max(peak, Math.Abs((double)sample));

        double reference = 32767.0 * Math.Pow(10.0, -6.0 / 20.0);

        Assert.True(peak <= reference + 0.5, $"peak {peak:F1} is above -6 dBFS");
        Assert.Equal(0, samples[^1]);
    }

    private static string DecodeDigits(IReadOnlyList<ModemSection> sections, short[] samples, double onSeconds, double offSeconds)
    {
        (int start, int _) = Window(sections, "dtmf");

        StringBuilder digits = new StringBuilder();

        for (int digit = 0; digit < 7; digit++)
        {
            int from = start + (int)Math.Round(digit * (onSeconds + offSeconds) * SampleRate);
            int to = start + (int)Math.Round((digit * (onSeconds + offSeconds) + onSeconds) * SampleRate);

            int row = Strongest(samples, from, to, Rows);
            int column = Strongest(samples, from, to, Columns);

            digits.Append(row == 3 ? '0' : (char)('1' + row * 3 + column));
        }

        return digits.ToString();
    }

    private static int Strongest(short[] samples, int start, int end, double[] candidates)
    {
        int best = 0;
        double bestPower = -1.0;

        for (int i = 0; i < candidates.Length; i++)
        {
            double power = Power(samples, start, end, candidates[i]);
            if (power > bestPower)
            {
                bestPower = power;
                best = i;
            }
        }

        return best;
    }

    private static (int Start, int Length) Window(IReadOnlyList<ModemSection> sections, string name)
    {
        foreach (ModemSection section in sections)
        {
            if (section.Name == name)
                return ((int)Math.Round(section.StartSeconds * SampleRate), (int)Math.Round(section.DurationSeconds * SampleRate));
        }

        throw new InvalidOperationException("no section named " + name);
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

    private static double Phase(short[] samples, int start, int end, double frequency)
    {
        double omega = 2.0 * Math.PI * frequency / SampleRate;
        double real = 0.0;
        double imaginary = 0.0;

        for (int n = start; n < end; n++)
        {
            real += samples[n] * Math.Cos(omega * n);
            imaginary += samples[n] * Math.Sin(omega * n);
        }

        return Math.Atan2(imaginary, real);
    }

    private static double Normalize(double value)
    {
        double result = value % (2.0 * Math.PI);

        if (result < -Math.PI)
            result += 2.0 * Math.PI;
        if (result > Math.PI)
            result -= 2.0 * Math.PI;

        return result;
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
