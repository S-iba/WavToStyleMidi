using WavToStyleMidi.Core.Models;

namespace WavToStyleMidi.Core.Quantization;

public interface IHatPatternFiller
{
    /// <summary>
    /// Adds closed hats where another drum hides them in the audio (on a snare, the slot right
    /// after a snare, and while a crash is ringing), but only when the rest of the loop has a
    /// steady hat pattern at that spacing. Returns the hits unchanged when there is no clear pattern.
    /// </summary>
    IReadOnlyList<QuantizedHit> Fill(IReadOnlyList<QuantizedHit> hits, TempoInfo tempo);
}
