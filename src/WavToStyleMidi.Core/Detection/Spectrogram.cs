using System.Numerics;
using MathNet.Numerics.IntegralTransforms;

namespace WavToStyleMidi.Core.Detection;

/// <summary>
/// Short-time magnitude spectrum. Frame f is centred on sample f * Hop, so frame times
/// line up with the audio without a separate offset.
/// </summary>
internal sealed class Spectrogram
{
    private const float LogCompression = 1000f;

    private Spectrogram(float[][] magnitude, float[][] logMagnitude, int windowSize, int hop, int sampleRate, int leadFrames)
    {
        LeadFrames = leadFrames;
        Magnitude = magnitude;
        LogMagnitude = logMagnitude;
        WindowSize = windowSize;
        Hop = hop;
        SampleRate = sampleRate;
    }

    public float[][] Magnitude { get; }

    public float[][] LogMagnitude { get; }

    public int WindowSize { get; }

    public int Hop { get; }

    public int SampleRate { get; }

    public int FrameCount => Magnitude.Length;

    /// <summary>Silent frames before sample 0, so a hit right at the start still shows a rise.</summary>
    public int LeadFrames { get; }

    public double BinHz => (double)SampleRate / WindowSize;

    public double HopSeconds => (double)Hop / SampleRate;

    public double FrameTime(int frame) => (frame - LeadFrames) * HopSeconds;

    public (int First, int Last) BinRange(double lowHz, double highHz)
    {
        var nyquistBin = WindowSize / 2;
        var first = Math.Clamp((int)Math.Ceiling(lowHz / BinHz), 1, nyquistBin);
        var last = Math.Clamp((int)Math.Floor(highHz / BinHz), first, nyquistBin);
        return (first, last);
    }

    public static Spectrogram Compute(float[] samples, int sampleRate, double windowMilliseconds, double hopMilliseconds, CancellationToken cancellationToken)
    {
        var windowSize = (int)BitOperations.RoundUpToPowerOf2((uint)Math.Max(64, sampleRate * windowMilliseconds / 1000.0));
        var hop = Math.Max(1, (int)Math.Round(sampleRate * hopMilliseconds / 1000.0));
        var half = windowSize / 2;
        var bins = half + 1;
        var leadFrames = (half + hop - 1) / hop + 1;
        var frames = samples.Length / hop + 1 + leadFrames;

        var window = new float[windowSize];
        var windowSum = 0f;
        for (var i = 0; i < windowSize; i++)
        {
            window[i] = (float)(0.5 - 0.5 * Math.Cos(2 * Math.PI * i / windowSize));
            windowSum += window[i];
        }

        var scale = 2f / windowSum;
        var buffer = new Complex32Buffer(windowSize);
        var magnitude = new float[frames][];
        var logMagnitude = new float[frames][];

        for (var f = 0; f < frames; f++)
        {
            if ((f & 63) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var start = (f - leadFrames) * hop - half;
            for (var i = 0; i < windowSize; i++)
            {
                var s = start + i;
                var value = s >= 0 && s < samples.Length ? samples[s] * window[i] : 0f;
                buffer.Data[i] = new MathNet.Numerics.Complex32(value, 0f);
            }

            Fourier.Forward(buffer.Data, FourierOptions.NoScaling);

            var mag = new float[bins];
            var log = new float[bins];
            for (var k = 0; k < bins; k++)
            {
                var m = buffer.Data[k].Magnitude * scale;
                mag[k] = m;
                log[k] = MathF.Log(1f + LogCompression * m);
            }

            magnitude[f] = mag;
            logMagnitude[f] = log;
        }

        return new Spectrogram(magnitude, logMagnitude, windowSize, hop, sampleRate, leadFrames);
    }

    private sealed class Complex32Buffer(int size)
    {
        public MathNet.Numerics.Complex32[] Data { get; } = new MathNet.Numerics.Complex32[size];
    }
}
