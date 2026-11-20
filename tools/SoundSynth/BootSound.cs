using System;

namespace DegaussNG.SoundSynth;

/// <summary>
/// The power-on sound of the fake BIOS: the beep, the floppy motor and the head seek.
/// Every sample is produced by hand from an own xorshift32 generator with a fixed seed, so the
/// output is byte-for-byte reproducible. 22050 Hz, 16 bit PCM, mono.
/// </summary>
public static class BootSound
{
    public const int SampleRate = 22050;
    public const double DurationSeconds = 5.2;
    public const int TotalSamples = 114660;

    private const double PeakLevel = 0.5011872336272722; // -6 dBFS
    private const int MasterFadeSamples = 441;           // 20 ms
    private const int EdgeFadeSamples = 1103;            // 50 ms
    private const uint Seed = 0x9E3779B9;

    private const double BeepStartSeconds = 0.20;
    private const double BeepEndSeconds = 0.35;
    private const double BeepFrequency = 1000.0;
    private const double BeepGain = 0.5;
    private const double OnePoleCoefficient = 0.3;

    private const double MotorStartSeconds = 3.20;
    private const double MotorEndSeconds = 5.00;
    private const double MotorGain = 0.25;
    private const double MotorFrequency = 120.0;
    private const double MotorQuality = 2.0;

    private const double SeekStartSeconds = 3.30;
    private const int Passes = 5;
    private const int StepsPerPass = 16;
    private const double PassStartSeconds = 0.253;
    private const double StepSeconds = 1.0 / 120.0;
    private const double StepLengthSeconds = 0.015;
    private const double FirstFrequency = 700.0;
    private const double FrequencyRise = 20.0;

    private const double ClunkStartSeconds = 4.90;
    private const double ClunkEndSeconds = 5.00;
    private const double ClunkFrequency = 180.0;

    public static short[] Render()
    {
        Xorshift32 noise = new Xorshift32(Seed);

        double[] signal = new double[TotalSamples];

        RenderBeep(signal);
        RenderMotor(signal, noise);
        RenderSeek(signal, noise);
        RenderClunk(signal, noise);

        for (int i = 0; i < signal.Length; i++)
            signal[i] = Math.Tanh(1.2 * signal[i]);

        double peak = Peak(signal);
        double scale = peak <= double.Epsilon ? 0.0 : PeakLevel / peak;

        short[] samples = new short[signal.Length];
        int fadeStart = signal.Length - MasterFadeSamples;

        for (int i = 0; i < signal.Length; i++)
        {
            double gain = i < fadeStart
                ? 1.0
                : (MasterFadeSamples - 1 - (i - fadeStart)) / (double)(MasterFadeSamples - 1);

            int rounded = (int)Math.Round(signal[i] * scale * gain * 32767.0);
            samples[i] = (short)Math.Clamp(rounded, short.MinValue, short.MaxValue);
        }

        return samples;
    }

    /// <summary>
    /// The power-on beep: a square wave through a one-pole low-pass with coefficient 0.3. The
    /// filter runs from the first sample, so the silent start stays exactly silent and the tail
    /// dies away after the beep.
    /// </summary>
    private static void RenderBeep(double[] signal)
    {
        double state = 0.0;

        for (int i = 0; i < TotalSamples; i++)
        {
            double seconds = i / (double)SampleRate;

            double input = seconds >= BeepStartSeconds && seconds < BeepEndSeconds
                ? BeepGain * Math.Sign(Math.Sin(2.0 * Math.PI * BeepFrequency * seconds))
                : 0.0;

            state += OnePoleCoefficient * (input - state);
            signal[i] += state;
        }
    }

    /// <summary>
    /// White noise through a band-pass at 120 Hz with Q 2, gain 0.25, with a 50 ms linear fade at
    /// both edges. The filter keeps running after the motor stops, so its tail is what is left in
    /// the last part of the file.
    /// </summary>
    private static void RenderMotor(double[] signal, Xorshift32 noise)
    {
        Biquad bandPass = new Biquad(BandPass(MotorFrequency, MotorQuality));

        int start = Index(MotorStartSeconds);
        int end = Index(MotorEndSeconds);

        for (int i = 0; i < TotalSamples; i++)
        {
            double input = i >= start && i < end
                ? MotorGain * FadeGain(i, start, end) * noise.NextSample()
                : 0.0;

            signal[i] += bandPass.Process(input);
        }
    }

    /// <summary>
    /// Five passes of sixteen steps, one step every 1/120 s and each step 15 ms long. The
    /// frequencies start at 700 Hz and rise 20 Hz per step; an odd pass runs the same tones
    /// downwards.
    /// </summary>
    private static void RenderSeek(double[] signal, Xorshift32 noise)
    {
        int stepLength = (int)Math.Round(StepLengthSeconds * SampleRate);

        for (int pass = 0; pass < Passes; pass++)
        {
            double passStart = SeekStartSeconds + PassStartSeconds * pass;

            for (int step = 0; step < StepsPerPass; step++)
            {
                int stepStart = Index(passStart + step * StepSeconds);

                double frequency = pass % 2 == 0
                    ? FirstFrequency + FrequencyRise * step
                    : FirstFrequency + FrequencyRise * (StepsPerPass - 1 - step);

                for (int i = stepStart; i < stepStart + stepLength && i < TotalSamples; i++)
                {
                    double t = (i - stepStart) / (double)SampleRate;

                    signal[i] += noise.NextSample() * 0.6 * Math.Exp(-t / 0.002)
                        + 0.5 * Math.Sin(2.0 * Math.PI * frequency * t) * Math.Exp(-t / 0.01);
                }
            }
        }
    }

    private static void RenderClunk(double[] signal, Xorshift32 noise)
    {
        int start = Index(ClunkStartSeconds);
        int end = Index(ClunkEndSeconds);

        for (int i = start; i < end; i++)
        {
            double t = (i - start) / (double)SampleRate;

            signal[i] += 0.8 * Math.Sin(2.0 * Math.PI * ClunkFrequency * t) * Math.Exp(-t / 0.03)
                + noise.NextSample() * Math.Exp(-t / 0.004);
        }
    }

    private static double FadeGain(int index, int start, int end)
    {
        if (index < start + EdgeFadeSamples)
            return (index - start) / (double)(EdgeFadeSamples - 1);

        if (index >= end - EdgeFadeSamples)
            return (EdgeFadeSamples - 1 - (index - (end - EdgeFadeSamples))) / (double)(EdgeFadeSamples - 1);

        return 1.0;
    }

    private static double[] BandPass(double frequency, double quality)
    {
        double w0 = 2.0 * Math.PI * frequency / SampleRate;
        double alpha = Math.Sin(w0) / (2.0 * quality);
        double a0 = 1.0 + alpha;

        return new double[]
        {
            alpha / a0,
            0.0,
            -alpha / a0,
            -2.0 * Math.Cos(w0) / a0,
            (1.0 - alpha) / a0,
        };
    }

    private sealed class Biquad
    {
        private readonly double _b0;
        private readonly double _b1;
        private readonly double _b2;
        private readonly double _a1;
        private readonly double _a2;

        private double _x1;
        private double _x2;
        private double _y1;
        private double _y2;

        public Biquad(double[] coefficients)
        {
            _b0 = coefficients[0];
            _b1 = coefficients[1];
            _b2 = coefficients[2];
            _a1 = coefficients[3];
            _a2 = coefficients[4];
        }

        public double Process(double sample)
        {
            double output = _b0 * sample + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;

            _x2 = _x1;
            _x1 = sample;
            _y2 = _y1;
            _y1 = output;

            return output;
        }
    }

    /// <summary>
    /// Own xorshift32 generator with a fixed seed, so two runs produce the same bytes.
    /// </summary>
    private sealed class Xorshift32
    {
        private uint _state;

        public Xorshift32(uint seed)
        {
            _state = seed == 0 ? 1u : seed;
        }

        public uint Next()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;

            return x;
        }

        public double NextSample()
        {
            return Next() / 2147483648.0 - 1.0;
        }
    }

    private static int Index(double seconds)
    {
        return (int)Math.Round(seconds * SampleRate);
    }

    private static double Peak(double[] signal)
    {
        double peak = 0.0;

        for (int i = 0; i < signal.Length; i++)
        {
            double magnitude = Math.Abs(signal[i]);
            if (magnitude > peak)
                peak = magnitude;
        }

        return peak;
    }
}
