using System.Globalization;
using WavToStyleMidi.Core.Models;

namespace WavToStyleMidi.Cli;

internal static class ResultFormatter
{
    public static string Format(ConversionResult result)
    {
        var name = Path.GetFileName(result.InputPath);
        if (!result.Succeeded)
        {
            return $"FAILED  {name}: {result.Error}";
        }

        var tempo = result.Tempo!;
        var source = tempo.Source switch
        {
            TempoSource.Argument => "--bpm",
            TempoSource.FileName => "file name",
            _ => tempo.Ambiguous ? "loop length, check this" : "loop length",
        };

        var counts = string.Join(", ", Enum.GetValues<DrumKind>()
            .Where(k => result.HitCounts.ContainsKey(k))
            .Select(k => $"{Label(k)} {result.HitCounts[k]}"));

        return string.Create(CultureInfo.InvariantCulture,
            $"OK      {name} -> {Path.GetFileName(result.OutputPath)} | {tempo.Bpm:0.##} BPM ({source}), {tempo.Bars} bar(s) | {(counts.Length == 0 ? "no hits found" : counts)}");
    }

    private static string Label(DrumKind kind) => kind switch
    {
        DrumKind.Kick => "kick",
        DrumKind.Snare => "snare",
        DrumKind.ClosedHat => "closed hat",
        DrumKind.OpenHat => "open hat",
        DrumKind.Crash => "crash",
        DrumKind.Ride => "ride",
        DrumKind.Clap => "clap",
        DrumKind.SideStick => "side stick",
        DrumKind.LowTom => "low tom",
        DrumKind.MidTom => "mid tom",
        DrumKind.HighTom => "high tom",
        DrumKind.LowConga => "low conga",
        DrumKind.OpenHighConga => "open high conga",
        DrumKind.MuteHighConga => "mute high conga",
        DrumKind.LowBongo => "low bongo",
        DrumKind.HighBongo => "high bongo",
        DrumKind.LowWoodBlock => "low wood block",
        DrumKind.HighWoodBlock => "high wood block",
        DrumKind.Claves => "claves",
        DrumKind.Tambourine => "tambourine",
        DrumKind.Maracas => "maracas",
        DrumKind.Cabasa => "cabasa",
        _ => kind.ToString(),
    };
}
