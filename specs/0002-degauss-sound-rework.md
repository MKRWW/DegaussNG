# Spec 0002: Degauss sound rework

Status: Reviewed. Part of issue #1. Replaces the section "Sound (tools/SoundSynth)" and AC6 of spec 0001.

## Why

The first sound has a falling pitch over clean harmonics and reads as a tonal kick drum. The relay
is too short and too quiet, and an audio device waking from idle can swallow it.

A real degauss has no pitch sweep. Mains current through the coil pulls on the metal at twice the
mains frequency, so the sound is a harsh, constant 100 Hz buzz that makes the shadow mask and the
chassis ring and rattle. It is loudest at the very first moment and dies away as the thermistor
heats up.

## Files

Change only these files:

```
tools/SoundSynth/DegaussSound.cs
assets/sounds/degauss.wav                       regenerated
src/DegaussNG.Core/DegaussEnvelope.cs
src/DegaussNG/SoundPlayer.cs
tests/DegaussNG.Tests/DegaussSoundTests.cs
tests/DegaussNG.Tests/DegaussEnvelopeTests.cs
```

## Timeline

44100 Hz, 16-bit PCM, mono, exactly 2.6 seconds (114660 samples).

| Time | Event |
|---|---|
| 0.00 s to 0.20 s | Digital silence, so a waking audio device cannot cut the relay off |
| 0.20 s | Relay closes |
| 0.29 s | Coil thump starts |
| 2.25 s | Thump has reached zero |
| 2.30 s | Relay opens, a quieter click |
| 2.55 s to 2.60 s | Linear fade, the last sample is exactly 0 |

## Relay

`Hit(t)` for `t >= 0`, length 60 ms, is the sum of:

- Click: high-passed noise times `exp(-t / 0.0025)`. High-pass means noise minus a one-pole low-pass
  of that noise with coefficient 0.25.
- Metal: `0.6 sin(2 pi 2150 t) exp(-t / 0.006)`, `0.45 sin(2 pi 3420 t) exp(-t / 0.004)`,
  `0.3 sin(2 pi 5230 t) exp(-t / 0.0025)`.
- Body: `0.8 sin(2 pi 310 t) exp(-t / 0.018)`, `0.6 sin(2 pi 190 t) exp(-t / 0.028)`.

Relay closing, with contact bounce: `Hit(t) + 0.45 Hit(t - 0.006) + 0.22 Hit(t - 0.013)`.
Relay opening: `Hit(t) + 0.3 Hit(t - 0.005)`.

## Thump

`t` is the time since thump onset, the length `L` is 1.96 s.

1. Current envelope:
   `I(t) = (0.45 exp(-t / 0.16) + 0.55 exp(-t / 0.65)) * min(1, t / 0.002) * taper(t)`.
   `taper` is 1 until `L - 0.3` and then falls linearly to 0 at `L`.
2. Coil wave: `w = sin(2 pi 50 t)`. The frequency is constant. There is no pitch glide.
3. Force: `s = tanh((3 + 9 I(t)) w)`, `f = I(t) * s * s`. This doubles the frequency to 100 Hz. The
   drive never falls below 3, so the buzz stays rough until it has died away and never turns into
   a clean tone.
4. `b` is `f` through a 40 Hz one-pole high-pass:
   `y[n] = a (y[n-1] + x[n] - x[n-1])`, `a = exp(-2 pi 40 / 44100)`.
5. Chassis modes: `b` feeds these parallel band-pass resonators, deliberately inharmonic.
   Constant 0 dB peak gain biquad with `w = 2 pi freq / 44100`, `alpha = sin(w) / (2 Q)`,
   `b0 = alpha`, `b1 = 0`, `b2 = -alpha`, `a0 = 1 + alpha`, `a1 = -2 cos(w)`, `a2 = 1 - alpha`.

   | Frequency (Hz) | Q | Gain |
   |---|---|---|
   | 173 | 7 | 0.50 |
   | 287 | 9 | 0.55 |
   | 419 | 11 | 0.50 |
   | 643 | 14 | 0.40 |
   | 977 | 16 | 0.30 |
   | 1523 | 18 | 0.22 |
   | 2341 | 20 | 0.14 |
   | 3307 | 22 | 0.08 |

6. Rattle: `noise * max(0, sin(2 pi 100 t))^4 * I(t)`, through the same biquad at 2500 Hz with Q 0.7,
   gain 0.5.
7. Impact, first 25 ms only: noise through a one-pole low-pass with coefficient 0.12, times
   `exp(-t / 0.012)`, gain 1.2.
8. Mix: `x = 0.35 b + modes + rattle + impact`, then `tanh(1.5 x)`.

## Mix

Render relay closing, relay opening and thump into separate buffers. Scale the closing relay so its
peak is 0.8 times the thump peak, and the opening relay to 0.35 times the thump peak. Sum the three,
peak-normalize to -3 dBFS, then apply the fade. All noise comes from the existing xorshift32
generator with its fixed seed. Output stays byte-for-byte reproducible.

## App changes

- `DegaussEnvelope.DelaySeconds` becomes 0.29, so the picture starts with the thump.
  `DecaySeconds` stays 1.78.
- `SoundPlayer` sets `DesiredLatency` of `WaveOutEvent` to 120 ms before `Init`.

## Acceptance criteria

All are automated in `DegaussSoundTests`. Windows are in seconds, start inclusive, end exclusive.

- S1: `Render()` returns 114660 samples, is identical across two calls and matches the committed WAV.
- S2: All samples in [0, 0.20) are exactly 0.
- S3: The absolute peak in [0.20, 0.25) is at least 0.70 times the peak in [0.29, 0.60).
- S4: RMS in [0.265, 0.288) is at most 0.25 times RMS in [0.20, 0.215), so the relay stands alone.
- S5: The relay is a click and not a thud: [0.200, 0.206) has at least 3000 zero crossings per second.
- S6: No pitch sweep: a Goertzel scan from 60 to 140 Hz in 1 Hz steps finds its maximum within
  100 Hz plus or minus 3 Hz in each of [0.40, 0.60), [0.80, 1.00) and [1.20, 1.40).
- S7: The sound starts harsh: RMS of the first difference divided by RMS is at least 1.3 times
  higher in [0.30, 0.45) than in [1.20, 1.50).
- S8: RMS in [1.90, 2.10) is at most 0.1 times RMS in [0.30, 0.50).
- S9: The peak in [2.30, 2.34) is at least 3 times the peak in [2.26, 2.295) and at least 0.2 times
  the overall peak.
- S10: The peak is at or below -3 dBFS, the last sample is 0, and the absolute mean is below 50.
- S12: The buzz sustains: RMS in [0.80, 1.10) is at least 0.2 times RMS in [0.30, 0.50).
- S13: The buzz stays rough: RMS of the first difference divided by RMS is at least 0.06 in
  [0.80, 1.10). A clean 100 Hz tone measures 0.014.
- S11: `DegaussEnvelopeTests` derive their time ranges from `DelaySeconds` and `TotalSeconds`
  instead of fixed millisecond numbers, and pass.

## Do NOT

- No pitch glide and no sum of clean harmonics of a fixed tone.
- No changes outside the Files section.
- No new NuGet packages.
- No mention of tools, assistants or automation in code comments or documents.
- Do not run git commands.

## Abschluss

Run these commands in this order and print their output verbatim:

1. `dotnet run --project tools/SoundSynth -- assets/sounds`
2. `dotnet build -c Release`
3. `dotnet test -c Release --no-build`
