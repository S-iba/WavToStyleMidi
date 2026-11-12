using Microsoft.Extensions.Options;
using WavToStyleMidi.Core.Midi;
using WavToStyleMidi.Core.Models;

namespace WavToStyleMidi.Core.Quantization;

public sealed class GrooveQuantizer(IOptions<DetectionOptions> options) : IGrooveQuantizer
{
    private readonly int _minimumVelocity = Math.Clamp(options.Value.MinimumVelocity, 1, 127);

    public IReadOnlyList<QuantizedHit> Quantize(IReadOnlyList<DrumHit> hits, TempoInfo tempo, int gridDivision, double strength)
    {
        ArgumentNullException.ThrowIfNull(hits);
        ArgumentNullException.ThrowIfNull(tempo);

        var gridTicks = GridTicks(gridDivision);
        strength = Math.Clamp(strength, 0, 1);

        const int ppq = MidiConstants.TicksPerQuarterNote;
        var loopTicks = (long)tempo.Bars * tempo.BeatsPerBar * ppq;
        var ticksPerSecond = tempo.Bpm / 60.0 * ppq;

        // One hit per drum per tick: keep the loudest.
        var placed = new Dictionary<(long Tick, DrumKind Kind), int>();
        foreach (var hit in hits)
        {
            var raw = hit.TimeSeconds * ticksPerSecond;
            var snapped = Math.Round(raw / gridTicks) * gridTicks;
            var tick = (long)Math.Round(raw + (snapped - raw) * strength);

            // A hit that lands on the loop end is the next downbeat played slightly early.
            if (tick >= loopTicks && tick - loopTicks < gridTicks / 2.0 + 0.5)
            {
                tick = 0;
            }

            // Onsets are found a few ms early, so a downbeat can read as just before the loop starts.
            if (tick < 0 && tick > -gridTicks / 2.0)
            {
                tick = 0;
            }

            if (tick < 0 || tick >= loopTicks)
            {
                continue;
            }

            var velocity = ToVelocity(hit.Strength);
            var key = (tick, hit.Kind);
            if (!placed.TryGetValue(key, out var existing) || existing < velocity)
            {
                placed[key] = velocity;
            }
        }

        return placed
            .Select(p => new QuantizedHit(p.Key.Tick, p.Key.Kind, p.Value))
            .OrderBy(h => h.Tick)
            .ThenBy(h => h.Kind)
            .ToList();
    }

    internal static int GridTicks(int gridDivision)
    {
        const int wholeNote = 4 * MidiConstants.TicksPerQuarterNote;
        if (gridDivision is not (4 or 8 or 12 or 16 or 24 or 32 or 48))
        {
            throw new ArgumentOutOfRangeException(nameof(gridDivision), gridDivision, "Use 4, 8, 12, 16, 24, 32 or 48.");
        }

        return wholeNote / gridDivision;
    }

    private int ToVelocity(float strength) =>
        (int)Math.Round(_minimumVelocity + (127 - _minimumVelocity) * Math.Clamp(strength, 0f, 1f));
}
