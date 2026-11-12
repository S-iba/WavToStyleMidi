using WavToStyleMidi.Core.Models;

namespace WavToStyleMidi.Core.Conversion;

public interface IBatchConverter
{
    /// <summary>Converts a single WAV file or every WAV file in a folder.</summary>
    Task<IReadOnlyList<ConversionResult>> ConvertAsync(ConversionRequest request, CancellationToken cancellationToken);
}
