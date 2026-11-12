namespace WavToStyleMidi.Core.Models;

public enum TempoSource
{
    Argument,
    FileName,
    LoopLength,
}

/// <param name="Ambiguous">True when the loop length allowed more than one plausible tempo.</param>
public sealed record TempoInfo(double Bpm, int Bars, int BeatsPerBar, TempoSource Source, bool Ambiguous = false)
{
    public double LoopSeconds => Bars * BeatsPerBar * 60.0 / Bpm;
}
