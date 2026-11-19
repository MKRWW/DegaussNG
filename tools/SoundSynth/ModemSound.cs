using System;
using System.Collections.Generic;

namespace DegaussNG.SoundSynth;

public sealed record ModemSection(string Name, double StartSeconds, double DurationSeconds);

/// <summary>
/// The synthesized dial-up modem sound: the call is placed, the far end answers, and both sides agree
/// on a carrier. Every sample is produced by hand from a fixed seed, so the output is byte-for-byte
/// reproducible. 22050 Hz, 16 bit PCM, mono.
/// </summary>
public static class ModemSound
{
    public const int SampleRate = 22050;

    private const double PeakLevel = 0.5011872336272722; // -6 dBFS
    private const int EdgeFadeSamples = 66;              // 3 ms
    private const int MasterFadeSamples = 441;           // 20 ms
    private const uint Seed = 0x9E3779B9;

    private sealed record Part(string Name, double DurationSeconds);

    private static readonly Part[] ShortParts = new Part[]
    {
        new Part("silence", 0.20),
        new Part("offhook", 0.05),
        new Part("dialtone", 0.40),
        new Part("dtmf", 0.70),
        new Part("line", 0.10),
        new Part("answer", 0.80),
        new Part("fsk", 0.50),
        new Part("probe", 0.25),
        new Part("bong", 0.60),
        new Part("sweep", 0.40),
        new Part("training", 0.60),
        new Part("data", 0.50),
        new Part("connect", 0.05),
        new Part("end", 0.10),
    };

    private static readonly Part[] FullParts = new Part[]
    {
        new Part("silence", 0.20),
        new Part("offhook", 0.10),
        new Part("dialtone", 2.00),
        new Part("dtmf", 1.40),
        new Part("line", 1.00),
        new Part("ringback", 1.50),
        new Part("pickup", 0.50),
        new Part("answer", 3.30),
        new Part("fsk", 1.80),
        new Part("probe", 1.00),
        new Part("bong", 1.50),
        new Part("sweep", 1.60),
        new Part("training", 6.00),
        new Part("data", 3.00),
        new Part("connect", 0.10),
        new Part("end", 0.20),
    };

    public static IReadOnlyList<ModemSection> ShortSections => BuildSections(ShortParts);

    public static IReadOnlyList<ModemSection> FullSections => BuildSections(FullParts);

    public static short[] RenderShort() => Render(ShortParts, false);

    public static short[] RenderFull() => Render(FullParts, true);

    private static IReadOnlyList<ModemSection> BuildSections(Part[] parts)
    {
        List<ModemSection> sections = new List<ModemSection>();
        int position = 0;

        foreach (Part part in parts)
        {
            sections.Add(new ModemSection(part.Name, position / (double)SampleRate, part.DurationSeconds));
            position += Rounded(part.DurationSeconds * SampleRate);
        }

        return sections;
    }

    private static short[] Render(Part[] parts, bool fullMode)
    {
        Xorshift32 noise = new Xorshift32(Seed);

        int total = 0;
        foreach (Part part in parts)
            total += Rounded(part.DurationSeconds * SampleRate);

        double[] signal = new double[total];

        int index = 0;
        foreach (Part part in parts)
        {
            int length = Rounded(part.DurationSeconds * SampleRate);
            RenderPart(part.Name, signal, index, length, noise, fullMode);

            // The clicks and the silence keep their full level.
            if (part.Name != "silence" && part.Name != "offhook" && part.Name != "connect" && part.Name != "end")
                ApplyEdgeFade(signal, index, length);

            index += length;
        }

        ApplyBiquad(signal, HighPass(300.0, 0.707));
        ApplyBiquad(signal, LowPass(3400.0, 0.707));

        for (int i = 0; i < signal.Length; i++)
            signal[i] = Math.Tanh(1.2 * signal[i]);

        double peak = Peak(signal, 0, signal.Length);
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

    private static void RenderPart(string name, double[] signal, int start, int length, Xorshift32 noise, bool fullMode)
    {
        switch (name)
        {
            case "silence":
            case "end":
                return;

            case "offhook":
            case "connect":
                for (int i = 0; i < length; i++)
                {
                    double seconds = i / (double)SampleRate;
                    signal[start + i] = 0.8 * noise.NextSample() * Math.Exp(-seconds / 0.006);
                }

                return;

            case "dialtone":
                for (int i = 0; i < length; i++)
                {
                    double seconds = i / (double)SampleRate;
                    signal[start + i] = 0.45 * Math.Cos(2.0 * Math.PI * 350.0 * seconds)
                        + 0.45 * Math.Cos(2.0 * Math.PI * 440.0 * seconds);
                }

                return;

            case "ringback":
                for (int i = 0; i < length; i++)
                {
                    double seconds = i / (double)SampleRate;
                    signal[start + i] = 0.35 * Math.Cos(2.0 * Math.PI * 440.0 * seconds)
                        + 0.35 * Math.Cos(2.0 * Math.PI * 480.0 * seconds);
                }

                return;

            case "line":
            case "pickup":
                for (int i = 0; i < length; i++)
                    signal[start + i] = 0.01 * noise.NextSample();

                return;

            case "dtmf":
                RenderDtmf(signal, start, fullMode);
                return;

            case "answer":
                RenderAnswer(signal, start, length);
                return;

            case "fsk":
                RenderFsk(signal, start, length, noise);
                return;

            case "probe":
                RenderProbe(signal, start, length, noise);
                return;

            case "bong":
                RenderBong(signal, start, length);
                return;

            case "sweep":
                RenderSweep(signal, start, length);
                return;

            case "training":
                RenderTraining(signal, start, length, noise);
                return;

            case "data":
                RenderData(signal, start, length, noise);
                return;

            default:
                throw new InvalidOperationException("unknown section " + name);
        }
    }

    /// <summary>
    /// The digits 5550199, each tone pair at 0.4 per tone, with a silent pause after every digit so
    /// that repeated digits stay separate.
    /// </summary>
    private static void RenderDtmf(double[] signal, int start, bool fullMode)
    {
        const string digits = "5550199";

        double onSeconds = fullMode ? 0.12 : 0.07;
        double offSeconds = fullMode ? 0.08 : 0.03;
        double cycle = onSeconds + offSeconds;

        for (int digit = 0; digit < digits.Length; digit++)
        {
            int digitStart = start + Rounded(digit * cycle * SampleRate);
            int digitEnd = digitStart + Rounded(onSeconds * SampleRate);

            (double low, double high) = DtmfTones(digits[digit]);

            for (int i = digitStart; i < digitEnd; i++)
            {
                double seconds = (i - start) / (double)SampleRate;
                signal[i] = 0.4 * Math.Cos(2.0 * Math.PI * low * seconds)
                    + 0.4 * Math.Cos(2.0 * Math.PI * high * seconds);
            }
        }
    }

    private static (double Low, double High) DtmfTones(char digit)
    {
        int row = digit == '0' ? 3 : (digit - '1') / 3;
        int column = digit == '0' ? 1 : (digit - '1') % 3;

        return (Rows[row], Columns[column]);
    }

    private static readonly double[] Rows = { 697.0, 770.0, 852.0, 941.0 };
    private static readonly double[] Columns = { 1209.0, 1336.0, 1477.0 };

    /// <summary>
    /// 2100 Hz with a phase reversal every 0.45 s and a 15 Hz amplitude modulation of depth 0.2.
    /// </summary>
    private static void RenderAnswer(double[] signal, int start, int length)
    {
        for (int i = 0; i < length; i++)
        {
            double seconds = i / (double)SampleRate;
            int reversals = (int)(seconds / 0.45);
            double amplitude = 0.7 * (1.0 + 0.2 * Math.Sin(2.0 * Math.PI * 15.0 * seconds));

            signal[start + i] = amplitude * Math.Cos(2.0 * Math.PI * 2100.0 * seconds + Math.PI * reversals);
        }
    }

    /// <summary>
    /// 300 baud random bits: 1080 Hz in the first half, 1750 Hz in the second, each shifted by
    /// 100 Hz per bit. The phase is carried from symbol to symbol.
    /// </summary>
    private static void RenderFsk(double[] signal, int start, int length, Xorshift32 noise)
    {
        double samplesPerSymbol = SampleRate / 300.0;
        int symbols = (int)Math.Round(length / samplesPerSymbol);
        int half = symbols / 2;

        double phase = 0.0;

        for (int symbol = 0; symbol < symbols; symbol++)
        {
            int blockStart = start + Rounded(symbol * samplesPerSymbol);
            int blockEnd = start + Rounded((symbol + 1) * samplesPerSymbol);

            double carrier = symbol < half ? 1080.0 : 1750.0;
            double frequency = carrier + (noise.NextBit() ? 100.0 : -100.0);

            for (int i = blockStart; i < blockEnd; i++)
            {
                signal[i] = 0.55 * Math.Cos(phase);
                phase += 2.0 * Math.PI * frequency / SampleRate;
            }
        }
    }

    /// <summary>
    /// Sum of the tones from 150 Hz to 3750 Hz in 150 Hz steps, with fixed random phases.
    /// </summary>
    private static void RenderProbe(double[] signal, int start, int length, Xorshift32 noise)
    {
        int tones = (int)Math.Round(3750.0 / 150.0);
        double[] phases = new double[tones];

        for (int tone = 0; tone < tones; tone++)
            phases[tone] = 2.0 * Math.PI * noise.Next() / 4294967296.0;

        for (int i = 0; i < length; i++)
        {
            double seconds = i / (double)SampleRate;
            double value = 0.0;

            for (int tone = 0; tone < tones; tone++)
                value += Math.Cos(2.0 * Math.PI * (150.0 + tone * 150.0) * seconds + phases[tone]);

            signal[start + i] = value;
        }

        double peak = Peak(signal, start, length);
        if (peak <= double.Epsilon)
            return;

        double scale = 0.5 / peak;
        for (int i = 0; i < length; i++)
            signal[start + i] *= scale;
    }

    /// <summary>
    /// 1200 Hz and 2400 Hz alternating every 0.15 s, starting with 1200 Hz.
    /// </summary>
    private static void RenderBong(double[] signal, int start, int length)
    {
        double samplesPerSlot = 0.15 * SampleRate;
        int slots = (int)Math.Round(length / samplesPerSlot);

        double phase = 0.0;

        for (int slot = 0; slot < slots; slot++)
        {
            int slotStart = start + Rounded(slot * samplesPerSlot);
            int slotEnd = start + Rounded((slot + 1) * samplesPerSlot);
            double frequency = slot % 2 == 0 ? 1200.0 : 2400.0;

            for (int i = slotStart; i < slotEnd; i++)
            {
                signal[i] = 0.6 * Math.Cos(phase);
                phase += 2.0 * Math.PI * frequency / SampleRate;
            }
        }
    }

    /// <summary>
    /// Linear chirps from 600 Hz to 3000 Hz of 0.4 s each, phase-continuous.
    /// </summary>
    private static void RenderSweep(double[] signal, int start, int length)
    {
        double phase = 0.0;

        for (int i = 0; i < length; i++)
        {
            double seconds = i / (double)SampleRate;
            double chirp = seconds - Math.Floor(seconds / 0.4) * 0.4;
            double frequency = 600.0 + (3000.0 - 600.0) * chirp / 0.4;

            signal[start + i] = 0.55 * Math.Cos(phase);
            phase += 2.0 * Math.PI * frequency / SampleRate;
        }
    }

    /// <summary>
    /// 1829 Hz carrier with a random QPSK phase out of 45, 135, 225 and 315 degrees at 3200 symbols
    /// per second. I and Q pass a one-pole low-pass with coefficient 0.5, so the symbols blend into
    /// each other the way a real modem training sequence does.
    /// </summary>
    private static void RenderTraining(double[] signal, int start, int length, Xorshift32 noise)
    {
        double[] phases = { 45.0, 135.0, 225.0, 315.0 };
        double samplesPerSymbol = SampleRate / 3200.0;
        int symbols = (int)Math.Round(length / samplesPerSymbol);

        double inPhase = 0.0;
        double quadrature = 0.0;

        for (int symbol = 0; symbol < symbols; symbol++)
        {
            int symbolStart = start + Rounded(symbol * samplesPerSymbol);
            int symbolEnd = start + Rounded((symbol + 1) * samplesPerSymbol);

            double constellation = phases[noise.Next() % 4] * Math.PI / 180.0;
            double targetIn = Math.Cos(constellation);
            double targetQ = Math.Sin(constellation);

            for (int i = symbolStart; i < symbolEnd; i++)
            {
                inPhase += 0.5 * (targetIn - inPhase);
                quadrature += 0.5 * (targetQ - quadrature);

                double seconds = (i - start) / (double)SampleRate;
                double carrier = 2.0 * Math.PI * 1829.0 * seconds;

                signal[i] = 0.6 * (inPhase * Math.Cos(carrier) - quadrature * Math.Sin(carrier));
            }
        }
    }

    /// <summary>
    /// White noise through a band-pass at 2000 Hz with Q 0.6, shaped by the slow 7 Hz wave.
    /// </summary>
    private static void RenderData(double[] signal, int start, int length, Xorshift32 noise)
    {
        Biquad bandPass = new Biquad(BandPass(2000.0, 0.6));

        for (int i = 0; i < length; i++)
        {
            double seconds = i / (double)SampleRate;
            double value = bandPass.Process(noise.NextSample());
            signal[start + i] = 0.6 * value * (1.0 + 0.15 * Math.Sin(2.0 * Math.PI * 7.0 * seconds));
        }
    }

    private static void ApplyEdgeFade(double[] signal, int start, int length)
    {
        for (int i = 0; i < length; i++)
        {
            double gain = 1.0;

            if (i < EdgeFadeSamples)
                gain = i / (double)(EdgeFadeSamples - 1);
            else if (i >= length - EdgeFadeSamples)
                gain = (EdgeFadeSamples - 1 - (i - (length - EdgeFadeSamples))) / (double)(EdgeFadeSamples - 1);

            signal[start + i] *= gain;
        }
    }

    private static void ApplyBiquad(double[] signal, double[] coefficients)
    {
        Biquad biquad = new Biquad(coefficients);

        for (int i = 0; i < signal.Length; i++)
            signal[i] = biquad.Process(signal[i]);
    }

    private static double[] HighPass(double frequency, double quality)
    {
        double w0 = 2.0 * Math.PI * frequency / SampleRate;
        double alpha = Math.Sin(w0) / (2.0 * quality);
        double a0 = 1.0 + alpha;
        double coefficient = (1.0 + Math.Cos(w0)) / 2.0;

        return new double[]
        {
            coefficient / a0,
            -2.0 * coefficient / a0,
            coefficient / a0,
            -2.0 * Math.Cos(w0) / a0,
            (1.0 - alpha) / a0,
        };
    }

    private static double[] LowPass(double frequency, double quality)
    {
        double w0 = 2.0 * Math.PI * frequency / SampleRate;
        double alpha = Math.Sin(w0) / (2.0 * quality);
        double a0 = 1.0 + alpha;
        double coefficient = (1.0 - Math.Cos(w0)) / 2.0;

        return new double[]
        {
            coefficient / a0,
            2.0 * coefficient / a0,
            coefficient / a0,
            -2.0 * Math.Cos(w0) / a0,
            (1.0 - alpha) / a0,
        };
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

    private static int Rounded(double value)
    {
        return (int)Math.Round(value);
    }

    private static double Peak(double[] signal, int start, int length)
    {
        double peak = 0.0;

        for (int i = start; i < start + length; i++)
        {
            double magnitude = Math.Abs(signal[i]);
            if (magnitude > peak)
                peak = magnitude;
        }

        return peak;
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

        public bool NextBit()
        {
            return (Next() & 1u) == 1u;
        }
    }
}
