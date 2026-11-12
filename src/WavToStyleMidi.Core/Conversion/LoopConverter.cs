using Microsoft.Extensions.Logging;
using WavToStyleMidi.Core.Audio;
using WavToStyleMidi.Core.Detection;
using WavToStyleMidi.Core.Midi;
using WavToStyleMidi.Core.Models;
using WavToStyleMidi.Core.Quantization;
using WavToStyleMidi.Core.Tempo;

namespace WavToStyleMidi.Core.Conversion;

public sealed class LoopConverter(
    IAudioLoader audioLoader,
    ITempoResolver tempoResolver,
    IHitDetector hitDetector,
    IGrooveQuantizer quantizer,
    IHatPatternFiller hatPatternFiller,
    IDrumMidiWriter midiWriter,
    ILogger<LoopConverter> logger) : ILoopConverter
{
    public async Task<ConversionResult> ConvertAsync(string wavPath, ConversionRequest request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wavPath);
        ArgumentNullException.ThrowIfNull(request);

        AudioClip clip;
        try
        {
            clip = await audioLoader.LoadAsync(wavPath, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or FormatException or InvalidDataException or ArgumentException or NotSupportedException)
        {
            logger.LogWarning(ex, "Could not read {File}", wavPath);
            return ConversionResult.Failed(wavPath, $"Could not read the WAV file: {ex.Message}");
        }

        if (clip.Samples.Length == 0)
        {
            return ConversionResult.Failed(wavPath, "The WAV file has no audio.");
        }

        var tempo = tempoResolver.Resolve(Path.GetFileName(wavPath), clip.DurationSeconds, request.Bpm, request.Bars, request.BeatsPerBar);
        if (tempo.Ambiguous)
        {
            logger.LogWarning("{File}: tempo guessed as {Bpm} BPM over {Bars} bars from the loop length; pass --bpm if this is wrong", wavPath, tempo.Bpm, tempo.Bars);
        }

        var hits = hitDetector.Detect(clip, cancellationToken);
        var placed = hatPatternFiller.Fill(quantizer.Quantize(hits, tempo, request.GridDivision, request.QuantizeStrength), tempo);
        var trackName = Path.GetFileNameWithoutExtension(wavPath);
        var bytes = midiWriter.Build(placed, tempo, request.MidiChannel, trackName);

        var outputPath = OutputPathFor(wavPath, request.OutputDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllBytesAsync(outputPath, bytes, cancellationToken);

        var counts = placed
            .GroupBy(h => h.Kind)
            .ToDictionary(g => g.Key, g => g.Count());

        return new ConversionResult(wavPath, outputPath, tempo, counts);
    }

    internal static string OutputPathFor(string wavPath, string? outputDirectory)
    {
        var directory = outputDirectory ?? Path.GetDirectoryName(Path.GetFullPath(wavPath))!;
        return Path.Combine(directory, Path.GetFileNameWithoutExtension(wavPath) + ".mid");
    }
}
