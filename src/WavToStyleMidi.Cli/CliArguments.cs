using System.Globalization;
using WavToStyleMidi.Core.Models;

namespace WavToStyleMidi.Cli;

/// <summary>Turns command-line arguments into a <see cref="ConversionRequest"/>.</summary>
internal static class CliArguments
{
    public const string Usage = """
        Usage: wav2style <file.wav | folder> [options]

        Converts WAV drum loops into General MIDI drum files for building
        Yamaha (SFF) and Roland styles.

        Options:
          -o, --output <folder>   Where to write .mid files (default: next to each .wav)
              --bpm <number>      Loop tempo. Default: read from the file name
                                  (e.g. "funk_95bpm.wav"), else worked out from the length
              --bars <number>     Bars in the loop (default: from tempo and length)
              --beats <number>    Beats per bar (default: 4)
              --grid <number>     Grid: 8, 16, 32, or 12/24 for triplets (default: 16)
              --quantize <0-1>    0 keeps the played feel, 1 snaps fully (default: 1)
              --channel <1-16>    MIDI channel (default: 10)
          -r, --recursive         Include subfolders
          -h, --help              Show this help
        """;

    public static bool TryParse(string[] args, out ConversionRequest? request, out string? error)
    {
        request = null;
        error = null;

        if (args.Length == 0 || args.Any(a => a is "-h" or "--help" or "/?"))
        {
            return false;
        }

        string? input = null;
        string? output = null;
        double? bpm = null;
        int? bars = null;
        var beats = 4;
        var grid = 16;
        var quantize = 1.0;
        var channel = 10;
        var recursive = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "-o" or "--output":
                    if (!TryValue(args, ref i, out output, ref error)) return false;
                    break;
                case "--bpm":
                    if (!TryNumber(args, ref i, 20, 400, out var b, ref error)) return false;
                    bpm = b;
                    break;
                case "--bars":
                    if (!TryNumber(args, ref i, 1, 256, out var bb, ref error)) return false;
                    bars = (int)bb;
                    break;
                case "--beats":
                    if (!TryNumber(args, ref i, 1, 16, out var be, ref error)) return false;
                    beats = (int)be;
                    break;
                case "--grid":
                    if (!TryNumber(args, ref i, 1, 96, out var g, ref error)) return false;
                    grid = (int)g;
                    break;
                case "--quantize":
                    if (!TryNumber(args, ref i, 0, 1, out quantize, ref error)) return false;
                    break;
                case "--channel":
                    if (!TryNumber(args, ref i, 1, 16, out var c, ref error)) return false;
                    channel = (int)c;
                    break;
                case "-r" or "--recursive":
                    recursive = true;
                    break;
                default:
                    if (arg.StartsWith('-'))
                    {
                        error = $"Unknown option '{arg}'.";
                        return false;
                    }

                    if (input is not null)
                    {
                        error = $"Only one input path is allowed, got '{input}' and '{arg}'.";
                        return false;
                    }

                    input = arg;
                    break;
            }
        }

        if (input is null)
        {
            error = "Give a .wav file or a folder.";
            return false;
        }

        if (grid is not (4 or 8 or 12 or 16 or 24 or 32 or 48))
        {
            error = "--grid must be 4, 8, 12, 16, 24, 32 or 48.";
            return false;
        }

        request = new ConversionRequest
        {
            InputPath = input,
            OutputDirectory = output,
            Bpm = bpm,
            Bars = bars,
            BeatsPerBar = beats,
            GridDivision = grid,
            QuantizeStrength = quantize,
            MidiChannel = channel,
            Recursive = recursive,
        };
        return true;
    }

    private static bool TryValue(string[] args, ref int index, out string? value, ref string? error)
    {
        value = null;
        if (index + 1 >= args.Length)
        {
            error = $"{args[index]} needs a value.";
            return false;
        }

        value = args[++index];
        return true;
    }

    private static bool TryNumber(string[] args, ref int index, double min, double max, out double value, ref string? error)
    {
        value = 0;
        var name = args[index];
        if (!TryValue(args, ref index, out var text, ref error))
        {
            return false;
        }

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || value < min || value > max)
        {
            error = $"{name} must be a number from {min} to {max}.";
            return false;
        }

        return true;
    }
}
