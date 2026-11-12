namespace WavToStyleMidi.Core.Models;

/// <summary>Mono audio in the range [-1, 1].</summary>
public sealed record AudioClip(float[] Samples, int SampleRate)
{
    public double DurationSeconds => (double)Samples.Length / SampleRate;
}
