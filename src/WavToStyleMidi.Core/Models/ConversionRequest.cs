namespace WavToStyleMidi.Core.Models;

/// <summary>Per-run settings that the user chooses on the command line.</summary>
public sealed record ConversionRequest
{
    public required string InputPath { get; init; }

    /// <summary>Output folder. Null writes next to each input file.</summary>
    public string? OutputDirectory { get; init; }

    public double? Bpm { get; init; }

    public int? Bars { get; init; }

    public int BeatsPerBar { get; init; } = 4;

    /// <summary>Grid as a note division: 8, 16, 32, or 12/24 for triplets.</summary>
    public int GridDivision { get; init; } = 16;

    /// <summary>0 keeps the played timing, 1 snaps fully to the grid.</summary>
    public double QuantizeStrength { get; init; } = 1.0;

    /// <summary>MIDI channel 1-16. Drums are normally on 10.</summary>
    public int MidiChannel { get; init; } = 10;

    public bool Recursive { get; init; }
}
