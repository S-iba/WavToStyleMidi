using WavToStyleMidi.Core.Models;

namespace WavToStyleMidi.Core.Midi;

public interface IDrumMidiWriter
{
    /// <summary>
    /// Builds a Standard MIDI File with tempo, time signature and the drum notes. Format 1 (the
    /// default, needed by Style Converter XT) puts tempo and meter in track 1 and the drums in
    /// track 2; format 0 puts everything in one track. Tracks end exactly at the loop length so
    /// the DAW keeps the loop size.
    /// </summary>
    byte[] Build(IReadOnlyList<QuantizedHit> hits, TempoInfo tempo, int channel, string trackName, int format = 1);
}
