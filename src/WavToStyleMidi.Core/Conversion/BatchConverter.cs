using WavToStyleMidi.Core.Models;

namespace WavToStyleMidi.Core.Conversion;

public sealed class BatchConverter(ILoopConverter loopConverter) : IBatchConverter
{
    public async Task<IReadOnlyList<ConversionResult>> ConvertAsync(ConversionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var files = FindInputs(request.InputPath, request.Recursive);
        if (files.Count == 0)
        {
            return [ConversionResult.Failed(request.InputPath, "No .wav files found.")];
        }

        var results = new List<ConversionResult>(files.Count);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await loopConverter.ConvertAsync(file, request, cancellationToken));
        }

        return results;
    }

    internal static IReadOnlyList<string> FindInputs(string inputPath, bool recursive)
    {
        if (File.Exists(inputPath))
        {
            return [inputPath];
        }

        if (!Directory.Exists(inputPath))
        {
            return [];
        }

        var options = new EnumerationOptions
        {
            MatchCasing = MatchCasing.CaseInsensitive,
            RecurseSubdirectories = recursive,
        };

        return Directory.EnumerateFiles(inputPath, "*.wav", options)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
