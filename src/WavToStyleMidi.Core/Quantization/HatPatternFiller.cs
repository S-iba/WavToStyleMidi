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

        if (!_options.FillMaskedHats)
        {
            return hits;
        }

        var loopTicks = (long)tempo.Bars * tempo.BeatsPerBar * MidiConstants.TicksPerQuarterNote;
        var cymbalTicks = hits.Where(h => h.Kind is DrumKind.ClosedHat or DrumKind.OpenHat or DrumKind.Crash or DrumKind.Ride).Select(h => h.Tick).ToHashSet();
        var snareTicks = hits.Where(h => h.Kind == DrumKind.Snare).Select(h => h.Tick).ToHashSet();
        var crashTicks = hits.Where(h => h.Kind == DrumKind.Crash).Select(h => h.Tick).ToList();
        var closedHats = hits.Where(h => h.Kind == DrumKind.ClosedHat).ToList();
        if (closedHats.Count < MinimumHatsForPattern)
        {
            return hits;
        }

        var crashMaskTicks = (long)Math.Round(_options.CrashMaskMilliseconds / 1000.0 * tempo.Bpm / 60.0 * MidiConstants.TicksPerQuarterNote);

        foreach (var spacing in SpacingsFinestFirst)
        {
            // Slots where another drum hides a hat: on a snare, the slot right after a snare
            // (its tail), and anywhere a crash is still ringing.
            bool IsMasked(long tick) =>
                snareTicks.Contains(tick)
                || snareTicks.Contains(tick - spacing)
                || crashTicks.Any(c => IsWithinAfter(c, tick, crashMaskTicks, loopTicks));

            var slots = Enumerable.Range(0, (int)(loopTicks / spacing)).Select(i => (long)i * spacing).ToList();
            var maskedSlots = slots.Where(IsMasked).ToList();
            var clearSlots = slots.Except(maskedSlots).ToList();
            if (maskedSlots.Count == 0 || clearSlots.Count < MinimumHatsForPattern)
            {
                continue;
            }

            var covered = clearSlots.Count(cymbalTicks.Contains);
            if ((double)covered / clearSlots.Count < _options.HatPatternCoverage)
            {
                continue;
            }

            var velocity = Median(closedHats.Select(h => h.Velocity));
            var added = maskedSlots
                .Where(t => !cymbalTicks.Contains(t))
                .Select(t => new QuantizedHit(t, DrumKind.ClosedHat, velocity));

            return hits.Concat(added).OrderBy(h => h.Tick).ThenBy(h => h.Kind).ToList();
        }

        return hits;
    }

    /// <summary>True when <paramref name="tick"/> falls after <paramref name="start"/> by less than <paramref name="length"/>, wrapping round the loop.</summary>
    private static bool IsWithinAfter(long start, long tick, long length, long loopTicks)
    {
        var distance = ((tick - start) % loopTicks + loopTicks) % loopTicks;
        return distance > 0 && distance < length;
    }

    private static int Median(IEnumerable<int> values)
    {
        var sorted = values.Order().ToList();
        return sorted[sorted.Count / 2];
    }
}
