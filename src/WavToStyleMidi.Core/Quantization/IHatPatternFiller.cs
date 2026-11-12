using WavToStyleMidi.Core.Models;

namespace WavToStyleMidi.Core.Quantization;

public interface IHatPatternFiller
{
    /// <summary>
    /// Adds closed hats on snare hits (and on the slot right after, where the snare's tail
    /// masks the hat) when the rest of the loop has a steady hat pattern at that spacing. Returns the hits unchanged when there is no clear pattern.
    /// </summary>
    IReadOnlyList<QuantizedHit> Fill(IReadOnlyList<QuantizedHit> hits, TempoInfo tempo);
}
