using NAudio.Wave;
using WavToStyleMidi.Core.Models;

namespace WavToStyleMidi.Core.Tests;

/// <summary>Builds simple synthetic drum loops with known hit positions.</summary>
/// <param name="jitterMilliseconds">Random timing offset per hit, to mimic a played groove.</param>
internal sealed class SyntheticLoop(double bpm, int bars, int sampleRate = 44100, double jitterMilliseconds = 0)
{
    private readonly List<(int Step, DrumKind Kind)> _hits = [];
    private readonly Random _random = new(42);

    public double Bpm => bpm;
    public int Bars => bars;
    public int SampleRate => sampleRate;
    public IReadOnlyList<(int Step, DrumKind Kind)> Hits => _hits;

    public double StepSeconds => 60.0 / bpm / 4;

    public int Steps => bars * 16;

    public SyntheticLoop Add(DrumKind kind, params int[] steps)
    {
        foreach (var step in steps)
        {
            _hits.Add((step, kind));
        }

        return this;
    }

    public float[] Render()
    {
        var length = (int)Math.Round(Steps * StepSeconds * sampleRate);
        var buffer = new float[length];
        foreach (var (step, kind) in _hits)
        {
            var offset = jitterMilliseconds == 0 ? 0 : (_random.NextDouble() * 2 - 1) * jitterMilliseconds / 1000.0;
            var start = Math.Max(0, (int)Math.Round((step * StepSeconds + offset) * sampleRate));
            var sound = Synthesize(kind);
            for (var i = 0; i < sound.Length; i++)
            {
                // Tails wrap round like a seamless loop.
                buffer[(start + i) % length] += sound[i];
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
        DrumKind.ClosedHat => Hat(0.012, 0.25f),
        DrumKind.OpenHat => Hat(0.15, 0.25f),
        DrumKind.Crash => Hat(0.7, 0.35f),
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
