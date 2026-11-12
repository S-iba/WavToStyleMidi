using Microsoft.Extensions.Options;
using WavToStyleMidi.Core.Models;
using WavToStyleMidi.Core.Quantization;

namespace WavToStyleMidi.Core.Tests;

public sealed class HatPatternFillerTests
{
    private const int Eighth = 240;
    private static readonly TempoInfo OneBar = new(100, 1, 4, TempoSource.Argument);

    [Fact]
    public void Adds_hats_on_snares_in_a_steady_eighth_pattern()
    {
        var hits = Hats(0, 1, 3, 4, 5, 7)
            .Append(new QuantizedHit(2 * Eighth, DrumKind.Snare, 120))
            .Append(new QuantizedHit(6 * Eighth, DrumKind.Snare, 120))
            .ToList();

        var result = Filler(enabled: true).Fill(hits, OneBar);

        var hatTicks = result.Where(h => h.Kind == DrumKind.ClosedHat).Select(h => h.Tick / Eighth);
        Assert.Equal([0, 1, 2, 3, 4, 5, 6, 7], hatTicks);
    }

    [Fact]
    public void Leaves_sparse_hats_alone()
    {
        var hits = Hats(0, 3, 5, 7)
            .Append(new QuantizedHit(2 * Eighth, DrumKind.Snare, 120))
            .Append(new QuantizedHit(6 * Eighth, DrumKind.Snare, 120))
            .ToList();

        var result = Filler(enabled: true).Fill(hits, OneBar);

        Assert.Equal(hits.Count, result.Count);
    }

    [Fact]
    public void Adds_hats_hidden_by_a_ringing_crash()
    {
        // 100 BPM: an 8th is 300 ms, so a 600 ms crash ring hides the next slot only.
        var hits = Hats(3, 4, 5, 6, 7)
            .Append(new QuantizedHit(0, DrumKind.Crash, 120))
            .ToList();

        var result = Filler(enabled: true).Fill(hits, OneBar);

        var hatTicks = result.Where(h => h.Kind == DrumKind.ClosedHat).Select(h => h.Tick / Eighth);
        Assert.Equal([1, 3, 4, 5, 6, 7], hatTicks);
    }

    [Fact]
    public void Can_be_switched_off()
    {
        var hits = Hats(0, 1, 3, 4, 5, 7)
            .Append(new QuantizedHit(2 * Eighth, DrumKind.Snare, 120))
            .ToList();

        Assert.Same(hits, Filler(enabled: false).Fill(hits, OneBar));
    }

    private static HatPatternFiller Filler(bool enabled) =>
        new(Options.Create(new DetectionOptions { FillMaskedHats = enabled }));

    private static IEnumerable<QuantizedHit> Hats(params int[] eighths) =>
        eighths.Select(e => new QuantizedHit(e * Eighth, DrumKind.ClosedHat, 80));
}
