using WavToStyleMidi.Core.Models;

namespace WavToStyleMidi.Core.Midi;

public interface IDrumMidiWriter
{
    /// <summary>
    /// Builds a type 0 Standard MIDI File with tempo, time signature and the drum notes.
    /// The track ends exactly at the loop length so the DAW keeps the loop size.
    /// </summary>
    byte[] Build(IReadOnlyList<QuantizedHit> hits, TempoInfo tempo, int channel, string trackName);
}
