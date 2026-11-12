using WavToStyleMidi.Core.Models;

namespace WavToStyleMidi.Core.Detection;

/// <summary>Hits in a loop that sound alike, summed up for naming.</summary>
/// <param name="Id">Group number.</param>
/// <param name="Consensus">Drums the rules found on at least half of the group's hits (empty when the rules named none of them).</param>
/// <param name="PitchHz">Typical strongest frequency in 300 Hz-5 kHz.</param>
/// <param name="TonalShare">Typical share of 300 Hz-5 kHz power in resonant peaks (0 = noise).</param>
/// <param name="MidShare">Typical share of the hit's power in 300 Hz-5 kHz.</param>
/// <param name="HatShare">Typical share of the hit's power in the hat band.</param>
public sealed record SoundGroup(
    int Id,
    IReadOnlySet<DrumKind> Consensus,
    double PitchHz,
    double TonalShare,
    double MidShare,
    double HatShare);
