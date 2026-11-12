using NAudio.Wave;
using WavToStyleMidi.Core.Models;

namespace WavToStyleMidi.Core.Tests;

/// <summary>Builds simple synthetic drum loops with known hit positions.</summary>
/// <param name="jitterMilliseconds">Random timing offset per hit, to mimic a played groove.</param>
internal sealed class SyntheticLoop(double bpm, int bars, int sampleRate = 44100, double jitterMilliseconds = 0)
{
    private readonly List<(int Step, DrumKind Kind)> _hits = [];
    private readonly List<float> _gains = [];
    private readonly Random _random = new(42);

    public double Bpm => bpm;
    public int Bars => bars;
    public int SampleRate => sampleRate;
    public IReadOnlyList<(int Step, DrumKind Kind)> Hits => _hits;

    public double StepSeconds => 60.0 / bpm / 4;

    public int Steps => bars * 16;

    public SyntheticLoop Add(DrumKind kind, params int[] steps) => Add(kind, 1f, steps);

    /// <summary>Adds hits played at <paramref name="gain"/> (1 = normal), e.g. ghost notes.</summary>
    public SyntheticLoop Add(DrumKind kind, float gain, params int[] steps)
    {
        foreach (var step in steps)
        {
            _hits.Add((step, kind));
            _gains.Add(gain);
        }

        return this;
    }

    public float[] Render()
    {
        var length = (int)Math.Round(Steps * StepSeconds * sampleRate);
        var buffer = new float[length];
        for (var h = 0; h < _hits.Count; h++)
        {
            var (step, kind) = _hits[h];
            var gain = _gains[h];
            var offset = jitterMilliseconds == 0 ? 0 : (_random.NextDouble() * 2 - 1) * jitterMilliseconds / 1000.0;
            var start = Math.Max(0, (int)Math.Round((step * StepSeconds + offset) * sampleRate));
            var sound = Synthesize(kind);
            for (var i = 0; i < sound.Length; i++)
            {
                // Tails wrap round like a seamless loop.
                buffer[(start + i) % length] += gain * sound[i];
            }
        }

        var peak = buffer.Max(MathF.Abs);
        if (peak > 0.95f)
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] *= 0.95f / peak;
            }
        }

        return buffer;
    }

    public void WriteWav(string path)
    {
        var samples = Render();
        using var writer = new WaveFileWriter(path, WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2));
        var stereo = new float[samples.Length * 2];
        for (var i = 0; i < samples.Length; i++)
        {
            stereo[2 * i] = samples[i];
            stereo[2 * i + 1] = samples[i];
        }

        writer.WriteSamples(stereo, 0, stereo.Length);
    }

    private float[] Synthesize(DrumKind kind) => kind switch
    {
        DrumKind.Kick => Kick(),
        DrumKind.Snare => Snare(),
        DrumKind.LowTom => Tom(95),
        DrumKind.MidTom => Tom(140),
        DrumKind.HighTom => Tom(220),
        DrumKind.Ride => Ride(),
        DrumKind.Clap => Clap(),
        DrumKind.SideStick => SideStick(),
        DrumKind.ClosedHat => Hat(0.012, 0.25f),
        DrumKind.OpenHat => Hat(0.15, 0.25f),
        DrumKind.Crash => Hat(0.7, 0.35f),
        DrumKind.LowConga => Resonance(230, 0.12, 0.05f),
        DrumKind.HighBongo => Resonance(820, 0.06, 0.05f),
        DrumKind.LowWoodBlock => Resonance(1150, 0.03, 0.03f),
        DrumKind.Tambourine => Tambourine(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private float[] Kick()
    {
        var n = (int)(0.35 * sampleRate);
        var output = new float[n];
        var phase = 0.0;
        for (var i = 0; i < n; i++)
        {
            var t = (double)i / sampleRate;
            var frequency = 50 + 100 * Math.Exp(-t / 0.03);
            phase += 2 * Math.PI * frequency / sampleRate;
            output[i] = (float)(0.9 * Math.Sin(phase) * Math.Exp(-t / 0.12));
        }

        return output;
    }

    private float[] Snare()
    {
        var n = (int)(0.25 * sampleRate);
        var output = new float[n];
        var previous = 0.0;
        for (var i = 0; i < n; i++)
        {
            var t = (double)i / sampleRate;
            var noise = _random.NextDouble() * 2 - 1;
            var bright = noise - 0.5 * previous;
            previous = noise;
            var body = Math.Sin(2 * Math.PI * 190 * t) * Math.Exp(-t / 0.05);
            output[i] = (float)(0.45 * body + 0.4 * bright * Math.Exp(-t / 0.06));
        }

        return output;
    }

    private float[] Tom(double pitchHz)
    {
        var n = (int)(0.8 * sampleRate);
        var output = new float[n];
        var phase = 0.0;
        for (var i = 0; i < n; i++)
        {
            var t = (double)i / sampleRate;
            phase += 2 * Math.PI * pitchHz * (1 + 0.15 * Math.Exp(-t / 0.02)) / sampleRate;
            var stick = (_random.NextDouble() * 2 - 1) * Math.Exp(-t / 0.004);
            output[i] = (float)(0.6 * Math.Sin(phase) * Math.Exp(-t / 0.25) + 0.15 * stick);
        }

        return output;
    }

    /// <summary>Separate inharmonic partials with a long ring, a stick tick and a little hiss.</summary>
    private float[] Ride()
    {
        double[] partials = [3150, 4280, 5410, 6730, 8120];
        double[] levels = [1.0, 0.8, 0.6, 0.45, 0.3];
        var n = (int)(1.6 * sampleRate);
        var output = new float[n];
        var phases = partials.Select(_ => _random.NextDouble() * 2 * Math.PI).ToArray();
        for (var i = 0; i < n; i++)
        {
            var t = (double)i / sampleRate;
            var sum = 0.0;
            for (var p = 0; p < partials.Length; p++)
            {
                sum += levels[p] * Math.Sin(2 * Math.PI * partials[p] * t + phases[p]);
            }

            var hiss = (_random.NextDouble() * 2 - 1) * Math.Exp(-t / 0.05);
            var tick = (_random.NextDouble() * 2 - 1) * Math.Exp(-t / 0.003);
            output[i] = (float)(0.08 * sum * Math.Exp(-t / 0.6) + 0.03 * hiss + 0.25 * tick);
        }

        return output;
    }

    /// <summary>Three quick bursts of 1-3 kHz noise, then a short tail; no body.</summary>
    private float[] Clap()
    {
        var n = (int)(0.4 * sampleRate);
        var noise = new float[n];
        for (var i = 0; i < n; i++)
        {
            noise[i] = (float)(_random.NextDouble() * 2 - 1);
        }

        var band = BandPass(noise, 1800, 1.2);
        var output = new float[n];
        for (var i = 0; i < n; i++)
        {
            var t = (double)i / sampleRate;
            var envelope = Math.Exp(-t / 0.004) + Math.Exp(-Math.Max(0, t - 0.009) / 0.004) * (t >= 0.009 ? 1 : 0)
                + (t >= 0.018 ? Math.Exp(-(t - 0.018) / 0.07) : 0);
            output[i] = (float)(0.9 * band[i] * envelope);
        }

        return output;
    }

    /// <summary>Woody click: short damped tones around 0.5-2 kHz.</summary>
    private float[] SideStick()
    {
        var n = (int)(0.15 * sampleRate);
        var output = new float[n];
        for (var i = 0; i < n; i++)
        {
            var t = (double)i / sampleRate;
            output[i] = (float)(
                0.5 * Math.Sin(2 * Math.PI * 520 * t) * Math.Exp(-t / 0.012)
                + 0.4 * Math.Sin(2 * Math.PI * 1250 * t) * Math.Exp(-t / 0.008)
                + 0.3 * Math.Sin(2 * Math.PI * 1900 * t) * Math.Exp(-t / 0.006));
        }

        return output;
    }

    /// <summary>A struck resonance (wood block, conga, bongo): a damped tone with a slight pitch drop and a short click.</summary>
    private float[] Resonance(double pitchHz, double decaySeconds, float click)
    {
        var n = (int)(Math.Min(0.6, decaySeconds * 6) * sampleRate);
        var output = new float[n];
        var phase = 0.0;
        for (var i = 0; i < n; i++)
        {
            var t = (double)i / sampleRate;
            phase += 2 * Math.PI * pitchHz * (1 + 0.04 * Math.Exp(-t / 0.01)) / sampleRate;
            var noise = (_random.NextDouble() * 2 - 1) * Math.Exp(-t / 0.002);
            output[i] = (float)(0.6 * Math.Sin(phase) * Math.Exp(-t / decaySeconds) + click * noise);
        }

        return output;
    }

    /// <summary>A frame-drum knock around 1.65 kHz under a bright jingle of high noise.</summary>
    private float[] Tambourine()
    {
        var knock = Resonance(1650, 0.025, 0f);
        var jingle = Hat(0.06, 1.2f);
        var output = new float[Math.Max(knock.Length, jingle.Length)];
        for (var i = 0; i < output.Length; i++)
        {
            output[i] = (i < knock.Length ? 0.6f * knock[i] : 0) + (i < jingle.Length ? jingle[i] : 0);
        }

        return output;
    }

    private float[] BandPass(float[] input, double centreHz, double q)
    {
        var w = 2 * Math.PI * centreHz / sampleRate;
        var alpha = Math.Sin(w) / (2 * q);
        double b0 = alpha, b2 = -alpha, a0 = 1 + alpha, a1 = -2 * Math.Cos(w), a2 = 1 - alpha;
        var output = new float[input.Length];
        double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
        for (var i = 0; i < input.Length; i++)
        {
            var y = (b0 * input[i] + b2 * x2 - a1 * y1 - a2 * y2) / a0;
            x2 = x1;
            x1 = input[i];
            y2 = y1;
            y1 = y;
            output[i] = (float)y;
        }

        return output;
    }

    /// <summary>High-passed noise: three first differences push the energy above ~6 kHz.</summary>
    private float[] Hat(double decaySeconds, float level)
    {
        var n = (int)(Math.Min(2.0, decaySeconds * 6) * sampleRate);
        var output = new float[n];
        double x1 = 0, x2 = 0, x3 = 0;
        for (var i = 0; i < n; i++)
        {
            var t = (double)i / sampleRate;
            var x0 = _random.NextDouble() * 2 - 1;
            var high = x0 - 3 * x1 + 3 * x2 - x3;
            x3 = x2;
            x2 = x1;
            x1 = x0;
            output[i] = (float)(level * high / 8 * Math.Exp(-t / decaySeconds));
        }

        return output;
    }
}
