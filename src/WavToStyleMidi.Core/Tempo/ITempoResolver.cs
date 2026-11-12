using WavToStyleMidi.Core.Models;

namespace WavToStyleMidi.Core.Tempo;

public interface ITempoResolver
{
    /// <summary>
    /// Works out tempo and bar count from, in order: explicit values, a BPM in the file name,
    /// then the loop length assuming it holds a whole number of bars.
    /// </summary>
    TempoInfo Resolve(string fileName, double durationSeconds, double? bpm, int? bars, int beatsPerBar);
}
