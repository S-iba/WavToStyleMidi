namespace WavToStyleMidi.Core.Models;

public sealed record ConversionResult(
    string InputPath,
    string? OutputPath,
    TempoInfo? Tempo,
    IReadOnlyDictionary<DrumKind, int> HitCounts,
    string? Error = null)
{
    public bool Succeeded => Error is null;

    public static ConversionResult Failed(string inputPath, string error) =>
        new(inputPath, null, null, new Dictionary<DrumKind, int>(), error);
}
