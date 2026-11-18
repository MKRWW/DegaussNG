using System.Text;
using DegaussNG.SoundSynth;
using Xunit;

namespace DegaussNG.Tests;

public sealed class DegaussSoundTests
{
    private const int SampleRate = 44100;
    private const int TotalSamples = 97020;
    private const int FadeSamples = 2205;   // 50 ms
    private const double MinusThreeDbfs = 23170.0; // 32767 * 0.70710678

    [Fact]
    public void RenderReturnsExactlyTwoPointTwoSeconds()
    {
        short[] samples = DegaussSound.Render();

        Assert.Equal(TotalSamples, samples.Length);
        Assert.Equal(2.2, DegaussSound.DurationSeconds);
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
    public void PeakStaysAtOrBelowMinusThreeDbfs()
    {
        short[] samples = DegaussSound.Render();

        double peak = 0.0;
        foreach (short sample in samples)
        {
            peak = Math.Max(peak, Math.Abs((double)sample));
        }

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
    public void TheQuietTailIsBelowOneTenthOfTheEarlyThump()
    {
        short[] samples = DegaussSound.Render();

        int fadeStart = TotalSamples - FadeSamples;
        int tailStart = fadeStart - 100 * SampleRate / 1000;

        double tail = Rms(samples, tailStart, fadeStart);
        double early = Rms(samples, 200 * SampleRate / 1000, 400 * SampleRate / 1000);

        Assert.True(early > 0.0, "the early thump is silent");
        Assert.True(tail < early / 10.0, $"tail rms {tail} is not below one tenth of the early rms {early}");
    }

    [Fact]
    public void TheSoundHasNoDcOffset()
    {
        short[] samples = DegaussSound.Render();

        double mean = samples.Average(sample => (double)sample);

        Assert.True(Math.Abs(mean) < 50.0, $"mean sample value is {mean}");
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
