using System;

namespace DegaussNG.SoundSynth;

/// <summary>
/// The power-on sound of the fake BIOS: the beep, the floppy motor and the head seek. The seek is a
/// row of separate mechanical clicks, never a tone. Every sample is produced by hand from an own
/// xorshift32 generator with a fixed seed, so the output is byte-for-byte reproducible.
/// 22050 Hz, 16 bit PCM, mono.
/// </summary>
public static class BootSound
{
    public const int SampleRate = 22050;
    public const double DurationSeconds = 5.2;
    public const int TotalSamples = 114660;

    private const double PeakLevel = 0.5011872336272722; // -6 dBFS
    private const int MasterFadeSamples = 441;           // 20 ms
    private const int MotorFadeSamples = 1764;           // 80 ms
    private const uint Seed = 0x9E3779B9;

    private const double BeepStartSeconds = 0.20;
    private const double BeepEndSeconds = 0.35;
    private const double BeepFrequency = 1000.0;
    private const double BeepGain = 0.5;
    private const double OnePoleCoefficient = 0.3;

    private const double MotorStartSeconds = 3.20;
    private const double MotorEndSeconds = 5.00;
    private const double MotorGain = 0.12;
    private const double MotorFrequency = 400.0;
    private const double MotorQuality = 0.7;
    private const double MotorWobbleLevel = 0.1;
    private const double MotorWobbleFrequency = 5.0;

    // One click: the transient, the frame modes and the body.
    private const int LightClickSamples = 265;  // 12 ms
    private const int HeavyClickSamples = 882;  // 40 ms
    private const double ClickLowPassCoefficient = 0.3;
    private const double ClickNoiseTau = 0.0008;

    private static readonly (double Level, double Frequency, double Tau)[] FrameModes =
    {
        (0.5, 2350.0, 0.003),
        (0.35, 3710.0, 0.002),
        (0.25, 5230.0, 0.0015),
    };

    private const double BodyLevel = 0.25;
    private const double BodyFrequency = 290.0;
    private const double BodySpreadBase = 0.9;
    private const double BodySpread = 0.2;
    private const double LightBodyTau = 0.003;
    private const double HeavyBodyTau = 0.008;

    private const double HeavyLowLevel = 0.5;
    private const double HeavyLowFrequency = 140.0;
    private const double HeavyLowTau = 0.012;

    // A light step: L = 0.6 (0.85 + 0.3 u), and it starts (u2 - 0.5) * 0.002 s off its nominal time.
    private const double LightLevelBase = 0.85;
    private const double LightLevelSwing = 0.3;
    private const double LightLevel = 0.6;
    private const double HeavyLevel = 1.0;
    private const double StepJitterSeconds = 0.002;

    // Seek passes: first second of the pass, number of steps, seconds between two steps.
    private static readonly (double StartSeconds, int Steps, double StepSeconds)[] Passes =
    {
        (3.30, 12, 0.020),
        (3.62, 12, 0.020),
        (3.95, 6, 0.012),
    };

    private static readonly double[] HeavyClackSeconds = { 4.35, 4.60, 4.90 };

    public static short[] Render()
    {
        Xorshift32 noise = new Xorshift32(Seed);

        double[] signal = new double[TotalSamples];

        RenderBeep(signal);
        RenderMotor(signal, noise);
        RenderSeek(signal, noise);
        RenderClacks(signal, noise);

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
    /// White noise through a band-pass at 400 Hz with Q 0.7 and gain 0.12, wobbling with
    /// 1 + 0.1 sin(2 pi 5 t), with an 80 ms linear fade at both edges. The filter keeps running
    /// after the motor stops, so its tail is what is left in the last part of the file.
    /// </summary>
    private static void RenderMotor(double[] signal, Xorshift32 noise)
    {
        Biquad bandPass = new Biquad(BandPass(MotorFrequency, MotorQuality));

        int start = Index(MotorStartSeconds);
        int end = Index(MotorEndSeconds);

        for (int i = 0; i < TotalSamples; i++)
        {
            double input = 0.0;

            if (i >= start && i < end)
            {
                double seconds = i / (double)SampleRate;
                double wobble = 1.0 + MotorWobbleLevel * Math.Sin(2.0 * Math.PI * MotorWobbleFrequency * seconds);

                input = MotorGain * FadeGain(i, start, end) * wobble * noise.NextSample();
            }

            signal[i] += bandPass.Process(input);
        }
    }

    /// <summary>
    /// The seek: three passes of separate light steps. Every step draws its own level and its own
    /// place off the nominal time, so the row of clicks is not a grid.
    /// </summary>
    private static void RenderSeek(double[] signal, Xorshift32 noise)
    {
        foreach ((double passStart, int steps, double stepSeconds) in Passes)
        {
            for (int step = 0; step < steps; step++)
            {
                double u = noise.NextUnit();
                double u2 = noise.NextUnit();

                double level = LightLevel * (LightLevelBase + LightLevelSwing * u);
                double seconds = passStart + step * stepSeconds + (u2 - 0.5) * StepJitterSeconds;

                RenderClick(signal, noise, Index(seconds), LightClickSamples, level, false);
            }
        }
    }

    private static void RenderClacks(double[] signal, Xorshift32 noise)
    {
        foreach (double seconds in HeavyClackSeconds)
            RenderClick(signal, noise, Index(seconds), HeavyClickSamples, HeavyLevel, true);
    }

    /// <summary>
    /// One click at local time t, L times the sum of the high-passed transient, the frame modes and
    /// the body. The heavy clack keeps the long body and adds the low frame tone.
    /// </summary>
    private static void RenderClick(double[] signal, Xorshift32 noise, int start, int length, double level, bool heavy)
    {
        double bodyTau = heavy ? HeavyBodyTau : LightBodyTau;
        double lowPass = 0.0;

        // Every click draws its own body pitch and its own phases, so repeated clicks never
        // line up into a tone.
        double bodyFrequency = BodyFrequency * (BodySpreadBase + BodySpread * noise.NextUnit());

        double[] modePhases = new double[FrameModes.Length];
        for (int mode = 0; mode < FrameModes.Length; mode++)
            modePhases[mode] = 2.0 * Math.PI * noise.NextUnit();

        double bodyPhase = 2.0 * Math.PI * noise.NextUnit();
        double lowPhase = 2.0 * Math.PI * noise.NextUnit();

        for (int i = 0; i < length && start + i < TotalSamples; i++)
        {
            double t = i / (double)SampleRate;

            double raw = noise.NextSample();
            lowPass += ClickLowPassCoefficient * (raw - lowPass);

            double value = (raw - lowPass) * Math.Exp(-t / ClickNoiseTau);

            for (int mode = 0; mode < FrameModes.Length; mode++)
            {
                (double modeLevel, double frequency, double tau) = FrameModes[mode];
                value += modeLevel * Math.Sin(2.0 * Math.PI * frequency * t + modePhases[mode]) * Math.Exp(-t / tau);
            }

            value += BodyLevel * Math.Sin(2.0 * Math.PI * bodyFrequency * t + bodyPhase) * Math.Exp(-t / bodyTau);

            if (heavy)
                value += HeavyLowLevel * Math.Sin(2.0 * Math.PI * HeavyLowFrequency * t + lowPhase) * Math.Exp(-t / HeavyLowTau);

            signal[start + i] += level * value;
        }
    }

    private static double FadeGain(int index, int start, int end)
    {
        if (index < start + MotorFadeSamples)
            return (index - start) / (double)(MotorFadeSamples - 1);

        if (index >= end - MotorFadeSamples)
            return (MotorFadeSamples - 1 - (index - (end - MotorFadeSamples))) / (double)(MotorFadeSamples - 1);

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

        public double NextUnit()
        {
            return Next() / 4294967296.0;
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
