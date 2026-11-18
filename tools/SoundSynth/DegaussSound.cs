namespace DegaussNG.SoundSynth;

/// <summary>
/// The synthesized degauss sound: a relay click, the constant 100 Hz buzz of mains current pulling
/// on the metal, and a quieter click when the relay opens. Every sample is produced by hand, so the
/// output is byte-for-byte reproducible. 44100 Hz, 16 bit PCM, mono, 2.6 seconds.
/// </summary>
public static class DegaussSound
{
    public const int SampleRate = 44100;
    public const double DurationSeconds = 2.6;
    public const int TotalSamples = 114660; // 44100 * 2.6

    private const uint Seed = 0x9E3779B9;

    private const int SilenceSamples = 8820;    // 200 ms, so a waking audio device cannot cut the relay off
    private const int ThumpStartSample = 12789; // 290 ms
    private const int RelayOpenSample = 101430; // 2300 ms
    private const int HitSamples = 2646;        // 60 ms
    private const int ThumpSamples = 86436;     // 1960 ms
    private const int FadeSamples = 2205;       // 50 ms

    private const double PeakLevel = 0.7071067811865476; // -3 dBFS
    private const double ClosingRelayLevel = 0.8;
    private const double OpeningRelayLevel = 0.35;

    // Hit(t): the click is high-passed noise, then the metal ring and the body of the armature.
    private const double ClickLowPassCoefficient = 0.25;
    private const double ClickTau = 0.0025;

    private static readonly (double Level, double Frequency, double Tau)[] MetalParts =
    {
        (0.6, 2150.0, 0.006),
        (0.45, 3420.0, 0.004),
        (0.3, 5230.0, 0.0025),
    };

    private static readonly (double Level, double Frequency, double Tau)[] BodyParts =
    {
        (0.8, 310.0, 0.018),
        (0.6, 190.0, 0.028),
    };

    // Contact bounce: seconds after the relay moves, then level.
    private static readonly (double Seconds, double Level)[] ClosingBounce =
    {
        (0.0, 1.0),
        (0.006, 0.45),
        (0.013, 0.22),
    };

    private static readonly (double Seconds, double Level)[] OpeningBounce =
    {
        (0.0, 1.0),
        (0.005, 0.3),
    };

    // Thump: the current envelope, the saturated coil force and the ringing chassis.
    private static readonly (double Level, double Tau)[] CurrentParts =
    {
        (0.45, 0.16),
        (0.55, 0.65),
    };

    private const double AttackSeconds = 0.002;
    private const double TaperSeconds = 0.3;
    private const double CoilFrequency = 50.0;
    private const double MinimumDrive = 3.0;
    private const double CurrentDrive = 9.0;
    private const double BuzzHighPassHz = 40.0;
    private const double BuzzLevel = 0.35;
    private const double MixDrive = 1.5;

    // Deliberately inharmonic chassis modes.
    private static readonly (double Frequency, double Quality, double Level)[] ChassisModes =
    {
        (173.0, 7.0, 0.50),
        (287.0, 9.0, 0.55),
        (419.0, 11.0, 0.50),
        (643.0, 14.0, 0.40),
        (977.0, 16.0, 0.30),
        (1523.0, 18.0, 0.22),
        (2341.0, 20.0, 0.14),
        (3307.0, 22.0, 0.08),
    };

    private const double RattleFrequency = 2500.0;
    private const double RattleQuality = 0.7;
    private const double RattleLevel = 0.5;
    private const double RattleRate = 100.0;
    private const double RattleGatePower = 4.0;

    private const double ImpactSeconds = 0.025;
    private const double ImpactLowPassCoefficient = 0.12;
    private const double ImpactTau = 0.012;
    private const double ImpactLevel = 1.2;

    public static short[] Render()
    {
        Xorshift32 noise = new(Seed);

        double[] closing = RenderRelay(noise, SilenceSamples, ClosingBounce);
        double[] opening = RenderRelay(noise, RelayOpenSample, OpeningBounce);
        double[] thump = RenderThump(noise, ThumpStartSample, ThumpSamples);

        double thumpPeak = Peak(thump);
        ScaleTo(closing, ClosingRelayLevel * thumpPeak, Peak(closing));
        ScaleTo(opening, OpeningRelayLevel * thumpPeak, Peak(opening));

        double[] signal = new double[TotalSamples];
        for (int i = 0; i < TotalSamples; i++)
        {
            signal[i] = closing[i] + opening[i] + thump[i];
        }

        double peak = Peak(signal);
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
    /// One relay transition as a click, a metal ring and the body of the armature, with contact bounce.
    /// </summary>
    private static double[] RenderRelay(Xorshift32 noise, int startSample, (double Seconds, double Level)[] bounce)
    {
        double[] buffer = new double[TotalSamples];

        foreach ((double delay, double level) in bounce)
        {
            int offset = (int)Math.Round(delay * SampleRate);
            double lowPass = 0.0;

            for (int i = 0; i < HitSamples; i++)
            {
                double seconds = i / (double)SampleRate;

                double raw = noise.NextSample();
                lowPass += ClickLowPassCoefficient * (raw - lowPass);

                double value = (raw - lowPass) * Math.Exp(-seconds / ClickTau);

                foreach ((double partLevel, double frequency, double tau) in MetalParts)
                {
                    value += partLevel * Math.Sin(2.0 * Math.PI * frequency * seconds) * Math.Exp(-seconds / tau);
                }

                foreach ((double partLevel, double frequency, double tau) in BodyParts)
                {
                    value += partLevel * Math.Sin(2.0 * Math.PI * frequency * seconds) * Math.Exp(-seconds / tau);
                }

                buffer[startSample + offset + i] += level * value;
            }
        }

        return buffer;
    }

    /// <summary>
    /// The buzz of the coil: the current envelope shapes a saturated force at twice the mains
    /// frequency, and that force rings the inharmonic chassis modes. No pitch glide.
    /// </summary>
    private static double[] RenderThump(Xorshift32 noise, int startSample, int length)
    {
        double[] buffer = new double[TotalSamples];

        BandPass[] modes = new BandPass[ChassisModes.Length];
        for (int i = 0; i < ChassisModes.Length; i++)
        {
            modes[i] = new BandPass(ChassisModes[i].Frequency, ChassisModes[i].Quality, ChassisModes[i].Level);
        }

        BandPass rattleMode = new BandPass(RattleFrequency, RattleQuality, RattleLevel);

        double highPassCoefficient = Math.Exp(-2.0 * Math.PI * BuzzHighPassHz / SampleRate);
        double highPassOutput = 0.0;
        double previousForce = 0.0;
        double impactLowPass = 0.0;

        for (int i = 0; i < length; i++)
        {
            double seconds = i / (double)SampleRate;

            double taper = seconds <= ThumpSamples / (double)SampleRate - TaperSeconds
                ? 1.0
                : (ThumpSamples / (double)SampleRate - seconds) / TaperSeconds;

            double current = 0.0;
            foreach ((double partLevel, double tau) in CurrentParts)
            {
                current += partLevel * Math.Exp(-seconds / tau);
            }

            current *= Math.Min(1.0, seconds / AttackSeconds) * taper;

            double coilWave = Math.Sin(2.0 * Math.PI * CoilFrequency * seconds);
            double saturation = Math.Tanh((MinimumDrive + CurrentDrive * current) * coilWave);
            double force = current * saturation * saturation;

            double buzz = highPassCoefficient * (highPassOutput + force - previousForce);
            previousForce = force;
            highPassOutput = buzz;

            double ringing = 0.0;
            foreach (BandPass mode in modes)
            {
                ringing += mode.Process(buzz);
            }

            double rattleGate = Math.Pow(Math.Max(0.0, Math.Sin(2.0 * Math.PI * RattleRate * seconds)), RattleGatePower);
            double rattle = noise.NextSample() * rattleGate * current;

            double impact = 0.0;
            if (seconds < ImpactSeconds)
            {
                impactLowPass += ImpactLowPassCoefficient * (noise.NextSample() - impactLowPass);
                impact = ImpactLevel * impactLowPass * Math.Exp(-seconds / ImpactTau);
            }

            double mixed = BuzzLevel * buzz + ringing + rattleMode.Process(rattle) + impact;
            buffer[startSample + i] = Math.Tanh(MixDrive * mixed);
        }

        return buffer;
    }

    /// <summary>
    /// Band-pass biquad with a constant 0 dB peak gain.
    /// </summary>
    private sealed class BandPass
    {
        private readonly double _b0;
        private readonly double _b1;
        private readonly double _b2;
        private readonly double _a1;
        private readonly double _a2;
        private readonly double _level;

        private double _x1;
        private double _x2;
        private double _y1;
        private double _y2;

        public BandPass(double frequency, double quality, double level)
        {
            double w = 2.0 * Math.PI * frequency / SampleRate;
            double alpha = Math.Sin(w) / (2.0 * quality);
            double a0 = 1.0 + alpha;

            _b0 = alpha / a0;
            _b1 = 0.0;
            _b2 = -alpha / a0;
            _a1 = -2.0 * Math.Cos(w) / a0;
            _a2 = (1.0 - alpha) / a0;
            _level = level;
        }

        public double Process(double sample)
        {
            double output = _b0 * sample + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;

            _x2 = _x1;
            _x1 = sample;
            _y2 = _y1;
            _y1 = output;

            return _level * output;
        }
    }

    private static double Peak(double[] buffer)
    {
        double peak = 0.0;

        foreach (double sample in buffer)
        {
            double absolute = Math.Abs(sample);
            if (absolute > peak)
            {
                peak = absolute;
            }
        }

        return peak;
    }

    private static void ScaleTo(double[] buffer, double targetPeak, double currentPeak)
    {
        if (currentPeak <= double.Epsilon)
        {
            return;
        }

        double factor = targetPeak / currentPeak;

        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] *= factor;
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
}
