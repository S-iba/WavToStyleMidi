using WavToStyleMidi.Core.Models;

namespace WavToStyleMidi.Core.Quantization;

public interface IGrooveQuantizer
{
    /// <summary>
    /// Places hits on the MIDI timeline at the loop tempo, pulls them toward the grid,
    /// drops anything past the loop end, and turns strength into velocity.
    /// </summary>
    IReadOnlyList<QuantizedHit> Quantize(IReadOnlyList<DrumHit> hits, TempoInfo tempo, int gridDivision, double strength);
}
