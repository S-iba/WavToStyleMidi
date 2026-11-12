using WavToStyleMidi.Core.Models;
using WavToStyleMidi.Core.Tempo;

namespace WavToStyleMidi.Core.Tests;

public sealed class TempoResolverTests
{
    private readonly TempoResolver _resolver = new();

    [Theory]
    [InlineData("funk_95bpm.wav", 95)]
    [InlineData("Funk 95 BPM.wav", 95)]
    [InlineData("loop-128_bpm-tight.wav", 128)]
    [InlineData("BPM120_groove.wav", 120)]
    [InlineData("swing 87.5bpm.wav", 87.5)]
    public void Reads_bpm_from_file_name(string fileName, double expected)
    {
        var tempo = _resolver.Resolve(fileName, durationSeconds: 60.0 / expected * 8, bpm: null, bars: null, beatsPerBar: 4);

        Assert.Equal(TempoSource.FileName, tempo.Source);
        Assert.Equal(expected, tempo.Bpm, 3);
        Assert.Equal(2, tempo.Bars);
    }

    [Fact]
    public void Argument_overrides_file_name()
    {
        var tempo = _resolver.Resolve("funk_95bpm.wav", 4.0, bpm: 120, bars: null, beatsPerBar: 4);

        Assert.Equal(TempoSource.Argument, tempo.Source);
        Assert.Equal(120, tempo.Bpm);
        Assert.Equal(2, tempo.Bars);
    }

    [Fact]
    public void Works_out_tempo_from_length_when_there_is_no_bpm()
    {
        // 2 bars of 4/4 at 100 BPM = 4.8 s. 4 bars would be 200 BPM, out of range.
        var tempo = _resolver.Resolve("groove.wav", 4.8, bpm: null, bars: null, beatsPerBar: 4);

        Assert.Equal(TempoSource.LoopLength, tempo.Source);
        Assert.Equal(100, tempo.Bpm, 2);
        Assert.Equal(2, tempo.Bars);
        Assert.False(tempo.Ambiguous);
    }

    [Fact]
    public void Flags_lengths_that_fit_two_tempos()
    {
        // 3 s is 1 bar at 80 or 2 bars at 160.
        var tempo = _resolver.Resolve("groove.wav", 3.0, bpm: null, bars: null, beatsPerBar: 4);

        Assert.True(tempo.Ambiguous);
        Assert.Equal(80, tempo.Bpm, 2);
    }

    [Fact]
    public void Uses_given_bar_count_with_length()
    {
        var tempo = _resolver.Resolve("groove.wav", 3.0, bpm: null, bars: 2, beatsPerBar: 4);

        Assert.Equal(160, tempo.Bpm, 2);
        Assert.False(tempo.Ambiguous);
    }

    [Theory]
    [InlineData("kit_2024_take3.wav")]
    [InlineData("groove.wav")]
    public void Ignores_numbers_that_are_not_a_bpm(string fileName) =>
        Assert.False(TempoResolver.TryParseBpm(fileName, out _));
}
