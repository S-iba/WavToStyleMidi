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

    public int NoteFor(DrumKind kind) => kind switch
    {
        DrumKind.Kick => Kick,
        DrumKind.Snare => Snare,
        DrumKind.ClosedHat => ClosedHat,
        DrumKind.OpenHat => OpenHat,
        DrumKind.Crash => Crash,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
