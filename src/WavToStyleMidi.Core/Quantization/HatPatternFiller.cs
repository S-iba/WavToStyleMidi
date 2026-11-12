using Microsoft.Extensions.Options;
using WavToStyleMidi.Core.Midi;
using WavToStyleMidi.Core.Models;

namespace WavToStyleMidi.Core.Quantization;

public sealed class HatPatternFiller(IOptions<DetectionOptions> options) : IHatPatternFiller
{
    private const int MinimumHatsForPattern = 4;
    private static readonly int[] SpacingsFinestFirst = [MidiConstants.TicksPerQuarterNote / 4, MidiConstants.TicksPerQuarterNote / 2];

    private readonly DetectionOptions _options = options.Value;

    public IReadOnlyList<QuantizedHit> Fill(IReadOnlyList<QuantizedHit> hits, TempoInfo tempo)
    {
        ArgumentNullException.ThrowIfNull(hits);
        ArgumentNullException.ThrowIfNull(tempo);

        if (!_options.FillHatsUnderSnares)
        {
            return hits;
        }

        var loopTicks = (long)tempo.Bars * tempo.BeatsPerBar * MidiConstants.TicksPerQuarterNote;
        var cymbalTicks = hits.Where(h => h.Kind is DrumKind.ClosedHat or DrumKind.OpenHat or DrumKind.Crash).Select(h => h.Tick).ToHashSet();
        var snareTicks = hits.Where(h => h.Kind == DrumKind.Snare).Select(h => h.Tick).ToHashSet();
        var closedHats = hits.Where(h => h.Kind == DrumKind.ClosedHat).ToList();
        if (snareTicks.Count == 0 || closedHats.Count < MinimumHatsForPattern)
        {
            return hits;
        }

        foreach (var spacing in SpacingsFinestFirst)
        {
            var slots = Enumerable.Range(0, (int)(loopTicks / spacing)).Select(i => (long)i * spacing).ToList();
            var openSlots = slots.Where(t => !snareTicks.Contains(t)).ToList();
            if (openSlots.Count == 0)
            {
                continue;
            }

            var covered = openSlots.Count(cymbalTicks.Contains);
            if ((double)covered / openSlots.Count < _options.HatPatternCoverage)
            {
                continue;
            }

            var velocity = Median(closedHats.Select(h => h.Velocity));
            // Fill on the snare and on the slot right after it, where the snare's tail masks the hat.
            var added = slots
                .Where(t => (snareTicks.Contains(t) || snareTicks.Contains(t - spacing)) && !cymbalTicks.Contains(t))
                .Select(t => new QuantizedHit(t, DrumKind.ClosedHat, velocity));

            return hits.Concat(added).OrderBy(h => h.Tick).ThenBy(h => h.Kind).ToList();
        }

        return hits;
    }

    private static int Median(IEnumerable<int> values)
    {
        var sorted = values.Order().ToList();
        return sorted[sorted.Count / 2];
    }
}
