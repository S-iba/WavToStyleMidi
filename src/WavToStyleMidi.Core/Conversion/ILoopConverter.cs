using WavToStyleMidi.Core.Models;

namespace WavToStyleMidi.Core.Conversion;

public interface ILoopConverter
{
    /// <summary>Converts one WAV drum loop into a drum MIDI file.</summary>
    Task<ConversionResult> ConvertAsync(string wavPath, ConversionRequest request, CancellationToken cancellationToken);
}
