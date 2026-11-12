using System.Globalization;
using System.Text.RegularExpressions;
using WavToStyleMidi.Core.Models;

namespace WavToStyleMidi.Core.Tempo;

public sealed partial class TempoResolver : ITempoResolver
{
    internal const double MinimumBpm = 70;
    internal const double MaximumBpm = 180;
    private const double PreferredBpm = 110;
    private static readonly int[] CandidateBars = [1, 2, 4, 8, 16];

    public TempoInfo Resolve(string fileName, double durationSeconds, double? bpm, int? bars, int beatsPerBar)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(durationSeconds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(beatsPerBar);

        if (bpm is > 0)
        {
            return new TempoInfo(bpm.Value, bars ?? BarsFor(durationSeconds, bpm.Value, beatsPerBar), beatsPerBar, TempoSource.Argument);
        }

        if (TryParseBpm(fileName, out var parsedBpm))
        {
            return new TempoInfo(parsedBpm, bars ?? BarsFor(durationSeconds, parsedBpm, beatsPerBar), beatsPerBar, TempoSource.FileName);
        }

        if (bars is > 0)
        {
            return new TempoInfo(BpmFor(durationSeconds, bars.Value, beatsPerBar), bars.Value, beatsPerBar, TempoSource.LoopLength);
        }

        return FromLoopLength(durationSeconds, beatsPerBar);
    }

    internal static bool TryParseBpm(string fileName, out double bpm)
    {
        bpm = 0;
        var name = Path.GetFileNameWithoutExtension(fileName);
        var match = BpmPattern().Match(name);
        if (!match.Success)
        {
            return false;
        }

        var text = (match.Groups["before"].Success ? match.Groups["before"] : match.Groups["after"]).Value.Replace(',', '.');
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        if (value is < 40 or > 300)
        {
            return false;
        }

        bpm = value;
        return true;
    }

    private static TempoInfo FromLoopLength(double durationSeconds, int beatsPerBar)
    {
        var candidates = CandidateBars
            .Select(b => (Bars: b, Bpm: BpmFor(durationSeconds, b, beatsPerBar)))
            .Where(c => c.Bpm is >= MinimumBpm and <= MaximumBpm)
            .OrderBy(c => Math.Abs(c.Bpm - PreferredBpm))
            .ToList();

        if (candidates.Count == 0)
        {
            // Outside the usual range: fall back to the bar count that gets closest to it.
            var closest = CandidateBars
                .Select(b => (Bars: b, Bpm: BpmFor(durationSeconds, b, beatsPerBar)))
                .OrderBy(c => Math.Abs(c.Bpm - PreferredBpm))
                .First();
            return new TempoInfo(closest.Bpm, closest.Bars, beatsPerBar, TempoSource.LoopLength, Ambiguous: true);
        }

        var best = candidates[0];
        return new TempoInfo(best.Bpm, best.Bars, beatsPerBar, TempoSource.LoopLength, Ambiguous: candidates.Count > 1);
    }

    private static double BpmFor(double durationSeconds, int bars, int beatsPerBar) =>
        Math.Round(bars * beatsPerBar * 60.0 / durationSeconds, 2);

    private static int BarsFor(double durationSeconds, double bpm, int beatsPerBar) =>
        Math.Max(1, (int)Math.Round(durationSeconds * bpm / 60.0 / beatsPerBar));

    [GeneratedRegex(@"(?<![\d.])(?<before>\d{2,3}(?:[.,]\d+)?)\s*[-_ ]?bpm|bpm\s*[-_ ]?(?<after>\d{2,3}(?:[.,]\d+)?)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BpmPattern();
}
