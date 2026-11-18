namespace DegaussNG.SoundSynth;

/// <summary>
/// The synthesized degauss sound: a relay clack, then the decaying thump of the yoke.
/// Every sample is produced by hand, so the output is byte-for-byte reproducible.
/// 44100 Hz, 16 bit PCM, mono, 2.2 seconds.
/// </summary>
public static class DegaussSound
{
    public const int SampleRate = 44100;
    public const double DurationSeconds = 2.2;
    public const int TotalSamples = 97020; // 44100 * 2.2

    private const uint Seed = 0x9E3779B9;

    private const int ClackSamples = 1764;      // 40 ms
    private const int ClackNoiseSamples = 132;  // 3 ms
    private const int ClackPingSamples = 1543;  // 35 ms
    private const int ThumpStartSample = 5292;  // 120 ms
    private const int GlideSamples = 8820;      // 200 ms
    private const int FadeSamples = 2205;       // 50 ms

    private const double PeakLevel = 0.7071067811865476; // -3 dBFS
    private const double DecayTau = 0.55;
    private const double LowPassAlpha = 0.02;
    private const double NoiseBedLevel = 0.06;
    private const double ClackNoiseLevel = 0.9;
    private const double ClackPingLevel = 0.5;
    private const double ClackPingFrequency = 1800.0;
    private const double ClackPingTau = 0.008;
    private const double ThumpLevel = 0.6;
    private const double StartFrequency = 70.0;
    private const double EndFrequency = 50.0;

    // The fundamental and its harmonics. Notebook speakers cannot reproduce 50 Hz on their own,
    // so the upper partials carry most of what is heard.
    private static readonly double[] Harmonics = { 1.0, 0.45, 0.30, 0.20, 0.15, 0.10 };

    public static short[] Render()
    {
        double[] signal = new double[TotalSamples];
        Xorshift32 noise = new(Seed);

        // Relay clack: a short noise burst plus a ping that is gone within 35 ms.
        for (int i = 0; i < ClackSamples; i++)
        {
            double seconds = i / (double)SampleRate;
            double value = 0.0;

            if (i < ClackNoiseSamples)
            {
                value += ClackNoiseLevel * noise.NextSample();
            }

            if (i < ClackPingSamples)
            {
                value += ClackPingLevel * Math.Exp(-seconds / ClackPingTau) * Math.Sin(2.0 * Math.PI * ClackPingFrequency * seconds);
            }

            signal[i] = value;
        }

        // Thump: the base frequency glides down and stays low, harmonics and a quiet
        // low-passed noise bed give it the coil and power-supply character.
        double phase = 0.0;
        double filtered = 0.0;

        for (int i = ThumpStartSample; i < TotalSamples; i++)
        {
            double seconds = (i - ThumpStartSample) / (double)SampleRate;
            double frequency = i - ThumpStartSample < GlideSamples
                ? StartFrequency + (EndFrequency - StartFrequency) * (seconds / (GlideSamples / (double)SampleRate))
                : EndFrequency;

            phase += 2.0 * Math.PI * frequency / SampleRate;

            double envelope = Math.Exp(-seconds / DecayTau);

            double tone = 0.0;
            for (int harmonic = 0; harmonic < Harmonics.Length; harmonic++)
            {
                tone += Harmonics[harmonic] * Math.Sin((harmonic + 1) * phase);
            }

            filtered += (noise.NextSample() - filtered) * LowPassAlpha;

            signal[i] = envelope * (ThumpLevel * tone + NoiseBedLevel * filtered);
        }

        double peak = 0.0;
        for (int i = 0; i < TotalSamples; i++)
        {
            double absolute = Math.Abs(signal[i]);
            if (absolute > peak)
            {
                peak = absolute;
            }
        }

        double scale = peak <= double.Epsilon ? 1.0 : PeakLevel / peak;

        short[] samples = new short[TotalSamples];
        int fadeStart = TotalSamples - FadeSamples;

        for (int i = 0; i < TotalSamples; i++)
        {
            double gain = i < fadeStart
                ? 1.0
                : (FadeSamples - 1 - (i - fadeStart)) / (double)(FadeSamples - 1);

            samples[i] = (short)Math.Round(signal[i] * scale * gain * 32767.0);
        }

        return samples;
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
}
