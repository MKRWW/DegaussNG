using System.Text;
using DegaussNG.SoundSynth;
using Xunit;

namespace DegaussNG.Tests;

public sealed class DegaussSoundTests
{
    private const int SampleRate = 44100;
    private const int TotalSamples = 114660;
    private const double MinusThreeDbfs = 23170.0; // 32767 * 0.70710678

    [Fact]
    public void RenderReturnsExactlyTwoPointSixSeconds()
    {
        short[] samples = DegaussSound.Render();

        Assert.Equal(TotalSamples, samples.Length);
        Assert.Equal(2.6, DegaussSound.DurationSeconds);
        Assert.Equal(SampleRate, DegaussSound.SampleRate);
    }

    [Fact]
    public void OutputIsIdenticalAcrossTwoCalls()
    {
        Assert.Equal(DegaussSound.Render(), DegaussSound.Render());
    }

    [Fact]
    public void RenderedSamplesMatchTheCommittedWav()
    {
        string path = CommittedWavPath();
        short[] committed = ReadDataSamples(path);

        Assert.Equal(TotalSamples, committed.Length);
        Assert.Equal(DegaussSound.Render(), committed);
    }

    [Fact]
    public void CommittedWavIsPcmMonoAtFortyFourThousandOneHundred()
    {
        byte[] bytes = File.ReadAllBytes(CommittedWavPath());

        Assert.Equal("RIFF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal("WAVE", Encoding.ASCII.GetString(bytes, 8, 4));
        Assert.Equal("fmt ", Encoding.ASCII.GetString(bytes, 12, 4));

        Assert.Equal(16, BitConverter.ToInt32(bytes, 16));
        Assert.Equal(1, BitConverter.ToInt16(bytes, 20));
        Assert.Equal(1, BitConverter.ToInt16(bytes, 22));
        Assert.Equal(SampleRate, BitConverter.ToInt32(bytes, 24));
        Assert.Equal(16, BitConverter.ToInt16(bytes, 34));
    }

    [Fact]
    public void EverythingBeforeTheRelayIsDigitalSilence()
    {
        short[] samples = DegaussSound.Render();

        for (int i = 0; i < Sample(0.20); i++)
        {
            Assert.Equal((short)0, samples[i]);
        }
    }

    [Fact]
    public void RelayClickIsLoudAgainstTheThump()
    {
        short[] samples = DegaussSound.Render();

        double relay = Peak(samples, Sample(0.20), Sample(0.25));
        double thump = Peak(samples, Sample(0.29), Sample(0.60));

        Assert.True(thump > 0.0, "the thump is silent");
        Assert.True(relay >= 0.70 * thump, $"relay peak {relay} is below 0.70 of the thump peak {thump}");
    }

    [Fact]
    public void RelayStandsAloneBeforeTheThumpStarts()
    {
        short[] samples = DegaussSound.Render();

        double beforeThump = Rms(samples, Sample(0.265), Sample(0.288));
        double atRelay = Rms(samples, Sample(0.20), Sample(0.215));

        Assert.True(atRelay > 0.0, "the relay is silent");
        Assert.True(beforeThump <= 0.25 * atRelay, $"rms {beforeThump} is more than 0.25 of the relay rms {atRelay}");
    }

    [Fact]
    public void RelayIsAClickAndNotAThud()
    {
        short[] samples = DegaussSound.Render();

        int first = Sample(0.200);
        int last = Sample(0.206);

        double crossings = 0.0;
        int previous = 0;

        for (int i = first; i < last; i++)
        {
            int sign = Math.Sign((double)samples[i]);
            if (sign == 0)
            {
                continue;
            }

            if (previous != 0 && sign != previous)
            {
                crossings += 1.0;
            }

            previous = sign;
        }

        double perSecond = crossings / (last - first) * SampleRate;

        Assert.True(perSecond >= 3000.0, $"{perSecond} zero crossings per second, expected at least 3000");
    }

    [Fact]
    public void BuzzHasNoPitchSweep()
    {
        short[] samples = DegaussSound.Render();

        AssertPeakAt100Hz(samples, 0.40, 0.60);
        AssertPeakAt100Hz(samples, 0.80, 1.00);
        AssertPeakAt100Hz(samples, 1.20, 1.40);
    }

    private static void AssertPeakAt100Hz(short[] samples, double from, double to)
    {
        double strongest = StrongestFrequency(samples, from, to);

        Assert.True(Math.Abs(strongest - 100.0) <= 3.0, $"strongest frequency in [{from}, {to}) is {strongest} Hz");
    }

    [Fact]
    public void SoundStartsHarshAndSmoothsOut()
    {
        short[] samples = DegaussSound.Render();

        double early = Roughness(samples, 0.30, 0.45);
        double late = Roughness(samples, 1.20, 1.50);

        Assert.True(late > 0.0, "the late part is silent");
        Assert.True(early >= 1.3 * late, $"early roughness {early} is below 1.3 times the late roughness {late}");
    }

    [Fact]
    public void QuietTailIsBelowOneTenthOfTheEarlyBuzz()
    {
        short[] samples = DegaussSound.Render();

        double tail = Rms(samples, Sample(1.90), Sample(2.10));
        double early = Rms(samples, Sample(0.30), Sample(0.50));

        Assert.True(early > 0.0, "the early buzz is silent");
        Assert.True(tail <= 0.1 * early, $"tail rms {tail} is more than one tenth of the early rms {early}");
    }

    [Fact]
    public void RelayOpeningIsQuieterThanTheThumpButStandsOut()
    {
        short[] samples = DegaussSound.Render();

        double click = Peak(samples, Sample(2.30), Sample(2.34));
        double before = Peak(samples, Sample(2.26), Sample(2.295));
        double overall = Peak(samples, 0, TotalSamples);

        Assert.True(click >= 3.0 * before, $"click peak {click} is below 3 times the peak {before} before it");
        Assert.True(click >= 0.2 * overall, $"click peak {click} is below 0.2 of the overall peak {overall}");
    }

    [Fact]
    public void PeakStaysAtOrBelowMinusThreeDbfs()
    {
        short[] samples = DegaussSound.Render();

        double peak = Peak(samples, 0, TotalSamples);

        Assert.True(peak > 0.0, "the sound is silent");
        Assert.True(peak <= MinusThreeDbfs, $"peak is {peak} full scale, expected at most {MinusThreeDbfs}");
    }

    [Fact]
    public void FadeOutMakesTheLastSampleExactlyZero()
    {
        short[] samples = DegaussSound.Render();

        Assert.Equal((short)0, samples[samples.Length - 1]);
    }

    [Fact]
    public void TheSoundHasNoDcOffset()
    {
        short[] samples = DegaussSound.Render();

        double mean = samples.Average(sample => (double)sample);

        Assert.True(Math.Abs(mean) < 50.0, $"mean sample value is {mean}");
    }

    [Fact]
    public void BuzzSustainsInsteadOfDyingLikeADrumHit()
    {
        short[] samples = DegaussSound.Render();

        double sustain = Rms(samples, Sample(0.80), Sample(1.10));
        double early = Rms(samples, Sample(0.30), Sample(0.50));

        Assert.True(sustain >= 0.2 * early, $"sustain rms {sustain} is below 0.2 of the early rms {early}");
    }

    [Fact]
    public void BuzzStaysRoughInsteadOfTurningIntoACleanTone()
    {
        short[] samples = DegaussSound.Render();

        double roughness = Roughness(samples, 0.80, 1.10);

        Assert.True(roughness >= 0.06, $"roughness is {roughness}, a clean 100 Hz tone measures 0.014");
    }

    private static int Sample(double seconds)
    {
        return (int)Math.Round(seconds * SampleRate);
    }

    private static double Peak(short[] samples, int first, int last)
    {
        double peak = 0.0;

        for (int i = first; i < last; i++)
        {
            peak = Math.Max(peak, Math.Abs((double)samples[i]));
        }

        return peak;
    }

    private static double Rms(short[] samples, int first, int last)
    {
        double sum = 0.0;

        for (int i = first; i < last; i++)
        {
            sum += (double)samples[i] * samples[i];
        }

        return Math.Sqrt(sum / (last - first));
    }

    private static double Roughness(short[] samples, double from, double to)
    {
        int first = Sample(from);
        int last = Sample(to);

        double differences = 0.0;
        int count = 0;

        for (int i = first + 1; i < last; i++)
        {
            double difference = (double)samples[i] - samples[i - 1];
            differences += difference * difference;
            count += 1;
        }

        return Math.Sqrt(differences / count) / Rms(samples, first, last);
    }

    private static double StrongestFrequency(short[] samples, double from, double to)
    {
        int first = Sample(from);
        int last = Sample(to);

        double strongest = 0.0;
        double frequency = 0.0;

        for (int hz = 60; hz <= 140; hz++)
        {
            double power = Goertzel(samples, first, last, hz);

            if (power > strongest)
            {
                strongest = power;
                frequency = hz;
            }
        }

        return frequency;
    }

    private static double Goertzel(short[] samples, int first, int last, double hz)
    {
        double cosine = 2.0 * Math.Cos(2.0 * Math.PI * hz / SampleRate);
        double previous = 0.0;
        double current = 0.0;

        for (int i = first; i < last; i++)
        {
            double next = cosine * current - previous + samples[i];
            previous = current;
            current = next;
        }

        return Math.Sqrt(current * current + previous * previous - cosine * current * previous);
    }

    private static string CommittedWavPath()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            string candidate = Path.Combine(directory.FullName, "assets", "sounds", "degauss.wav");

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("assets/sounds/degauss.wav was not found");
    }

    private static short[] ReadDataSamples(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);

        int offset = 12;

        while (offset + 8 <= bytes.Length)
        {
            string chunk = Encoding.ASCII.GetString(bytes, offset, 4);
            int size = BitConverter.ToInt32(bytes, offset + 4);

            if (chunk == "data")
            {
                short[] samples = new short[size / 2];

                for (int i = 0; i < samples.Length; i++)
                {
                    samples[i] = BitConverter.ToInt16(bytes, offset + 8 + i * 2);
                }

                return samples;
            }

            offset += 8 + size;
        }

        throw new InvalidDataException("the wav file has no data chunk");
    }
}
