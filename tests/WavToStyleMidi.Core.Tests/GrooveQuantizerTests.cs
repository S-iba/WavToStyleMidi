using Microsoft.Extensions.Options;
using WavToStyleMidi.Core.Models;
using WavToStyleMidi.Core.Quantization;

namespace WavToStyleMidi.Core.Tests;

public sealed class GrooveQuantizerTests
{
    private static readonly TempoInfo Tempo = new(120, 1, 4, TempoSource.Argument);
    private readonly GrooveQuantizer _quantizer = new(Options.Create(new DetectionOptions { MinimumVelocity = 40 }));

    [Fact]
    public void Snaps_to_sixteenth_grid()
    {
        // At 120 BPM a sixteenth is 0.125 s = 120 ticks.
        var hits = new[] { new DrumHit(0.130, DrumKind.Snare, 1f) };

        var result = _quantizer.Quantize(hits, Tempo, 16, 1.0);

        Assert.Equal(120, Assert.Single(result).Tick);
    }

    [Fact]
    public void Partial_strength_keeps_some_feel()
    {
        var hits = new[] { new DrumHit(0.150, DrumKind.Snare, 1f) }; // 144 ticks

        var result = _quantizer.Quantize(hits, Tempo, 16, 0.5);

        Assert.Equal(132, Assert.Single(result).Tick);
    }

    [Fact]
    public void Hit_on_loop_end_wraps_to_downbeat_and_merges()
    {
        var hits = new[]
        {
            new DrumHit(0.0, DrumKind.Kick, 0.5f),
            new DrumHit(1.995, DrumKind.Kick, 1f),
        };

        var result = _quantizer.Quantize(hits, Tempo, 16, 1.0);

        var hit = Assert.Single(result);
        Assert.Equal(0, hit.Tick);
        Assert.Equal(127, hit.Velocity);
    }

    [Fact]
    public void Drops_hits_past_the_loop()
    {
        var hits = new[] { new DrumHit(2.5, DrumKind.Kick, 1f) };

        Assert.Empty(_quantizer.Quantize(hits, Tempo, 16, 1.0));
    }

    [Fact]
    public void Maps_strength_to_velocity_range()
    {
        var hits = new[] { new DrumHit(0, DrumKind.Kick, 0f), new DrumHit(0.5, DrumKind.Kick, 1f) };

        var result = _quantizer.Quantize(hits, Tempo, 16, 1.0);

        Assert.Equal([40, 127], result.Select(h => h.Velocity));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(0)]
    public void Rejects_grids_that_do_not_divide_a_bar(int grid) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => GrooveQuantizer.GridTicks(grid));
}
