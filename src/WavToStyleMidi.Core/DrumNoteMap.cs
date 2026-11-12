using WavToStyleMidi.Core.Models;

namespace WavToStyleMidi.Core;

/// <summary>General MIDI drum notes, which both Yamaha and Roland kits follow.</summary>
public sealed class DrumNoteMap
{
    public const string SectionName = "DrumNotes";

    public int Kick { get; set; } = 36;
    public int Snare { get; set; } = 38;
    public int ClosedHat { get; set; } = 42;
    public int OpenHat { get; set; } = 46;
    public int Crash { get; set; } = 49;
    public int Ride { get; set; } = 51;
    public int Clap { get; set; } = 39;
    public int SideStick { get; set; } = 37;

    /// <summary>GM 41, low floor tom.</summary>
    public int LowTom { get; set; } = 41;

    /// <summary>GM 45, low tom.</summary>
    public int MidTom { get; set; } = 45;

    /// <summary>GM 48, hi-mid tom.</summary>
    public int HighTom { get; set; } = 48;

    public int NoteFor(DrumKind kind) => kind switch
    {
        DrumKind.Kick => Kick,
        DrumKind.Snare => Snare,
        DrumKind.ClosedHat => ClosedHat,
        DrumKind.OpenHat => OpenHat,
        DrumKind.Crash => Crash,
        DrumKind.Ride => Ride,
        DrumKind.Clap => Clap,
        DrumKind.SideStick => SideStick,
        DrumKind.LowTom => LowTom,
        DrumKind.MidTom => MidTom,
        DrumKind.HighTom => HighTom,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
