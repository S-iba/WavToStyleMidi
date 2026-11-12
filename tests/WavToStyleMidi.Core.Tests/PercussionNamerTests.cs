using Microsoft.Extensions.Options;
using WavToStyleMidi.Core.Detection;
using WavToStyleMidi.Core.Models;

namespace WavToStyleMidi.Core.Tests;

public sealed class PercussionNamerTests
{
    private readonly PercussionNamer _namer = new(Options.Create(new DetectionOptions()));

    private static SoundGroup Group(int id, double pitchHz, double tonal = 0.8, double midShare = 0.9, double hatShare = 0.05, params DrumKind[] consensus) =>
        new(id, consensus.ToHashSet(), pitchHz, tonal, midShare, hatShare);

    [Fact]
    public void Drums_and_plain_noise_are_not_percussion()
    {
        var names = _namer.Name(
        [
            Group(0, 300, consensus: DrumKind.Kick),
            Group(1, 2000, tonal: 0.05, consensus: DrumKind.Clap),
            Group(2, 4000, tonal: 0.0, midShare: 0.1, hatShare: 0.8, consensus: DrumKind.ClosedHat),
            Group(3, 1200, consensus: DrumKind.SideStick),
        ]);

        Assert.Empty(names);
    }

    [Fact]
    public void Resonant_clap_or_hat_group_is_named_by_pitch()
    {
        var names = _namer.Name(
        [
            Group(0, 820, consensus: DrumKind.Clap),
            Group(1, 1150, consensus: [DrumKind.SideStick, DrumKind.ClosedHat]),
            Group(2, 300),
        ]);

        Assert.Equal(DrumKind.HighBongo, names[0]);
        Assert.Equal(DrumKind.LowWoodBlock, names[1]);
        Assert.Equal(DrumKind.OpenHighConga, names[2]);
    }

    [Fact]
    public void Different_pitches_on_the_same_step_get_separate_notes_in_pitch_order()
    {
        var names = _namer.Name([Group(0, 1300), Group(1, 1100), Group(2, 1450)]);

        Assert.Equal(DrumKind.LowWoodBlock, names[1]);
        Assert.Equal(DrumKind.HighWoodBlock, names[0]);
        Assert.Equal(DrumKind.Claves, names[2]);
    }

    [Fact]
    public void Collisions_at_the_top_push_lower_sounds_down()
    {
        var names = _namer.Name([Group(0, 3000), Group(1, 4000)]);

        Assert.Equal(DrumKind.HighWoodBlock, names[0]);
        Assert.Equal(DrumKind.Claves, names[1]);
    }

    [Fact]
    public void Same_pitch_played_two_ways_shares_a_note()
    {
        var names = _namer.Name([Group(0, 1150), Group(1, 1170)]);

        Assert.Equal(names[0], names[1]);
    }

    [Fact]
    public void Resonance_with_bright_jingle_is_tambourine_once()
    {
        var names = _namer.Name([Group(0, 1650, hatShare: 0.3), Group(1, 900, tonal: 0.6, hatShare: 0.25)]);

        Assert.Equal(DrumKind.Tambourine, names[0]);
        Assert.Equal(DrumKind.HighBongo, names[1]);
    }

    [Fact]
    public void Unnamed_noise_is_a_shaker()
    {
        var names = _namer.Name(
        [
            Group(0, 4000, tonal: 0.1, midShare: 0.2, hatShare: 0.7),
            Group(1, 3000, tonal: 0.1, midShare: 0.3, hatShare: 0.6),
        ]);

        Assert.Equal(DrumKind.Maracas, names[0]);
        Assert.Equal(DrumKind.Cabasa, names[1]);
    }
}
