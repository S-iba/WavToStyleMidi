namespace WavToStyleMidi.Core.Models;

/// <summary>A detected drum hit before quantization.</summary>
/// <param name="TimeSeconds">Onset time from the start of the loop.</param>
/// <param name="Strength">Relative strength in [0, 1], used for velocity.</param>
public readonly record struct DrumHit(double TimeSeconds, DrumKind Kind, float Strength);

/// <summary>A hit placed on the MIDI timeline.</summary>
public readonly record struct QuantizedHit(long Tick, DrumKind Kind, int Velocity);
