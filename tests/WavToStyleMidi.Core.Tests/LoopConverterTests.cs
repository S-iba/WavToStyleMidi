using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Microsoft.Extensions.DependencyInjection;
using WavToStyleMidi.Core.Conversion;
using WavToStyleMidi.Core.DependencyInjection;
using WavToStyleMidi.Core.Midi;
using WavToStyleMidi.Core.Models;

namespace WavToStyleMidi.Core.Tests;

/// <summary>End-to-end: synthetic WAV loop through the whole pipeline to a MIDI file.</summary>
public sealed class LoopConverterTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("wav2style-").FullName;
    private readonly ServiceProvider _services = new ServiceCollection()
        .AddLogging()
        .AddWavToStyleMidi()
        .BuildServiceProvider();

    public void Dispose()
    {
        _services.Dispose();
        Directory.Delete(_folder, recursive: true);
    }

    public static TheoryData<double> Tempos => [85, 100, 128];

    [Theory]
    [MemberData(nameof(Tempos))]
    public async Task Recovers_a_basic_rock_groove(double bpm)
    {
        var loop = new SyntheticLoop(bpm, bars: 2)
            .Add(DrumKind.Kick, 0, 6, 8, 16, 22, 24)
            .Add(DrumKind.Snare, 4, 12, 20, 28)
            .Add(DrumKind.ClosedHat, 2, 4, 6, 8, 10, 12, 16, 18, 20, 22, 24, 26, 28)
            .Add(DrumKind.OpenHat, 14, 30);

        var notes = await ConvertAsync(loop, $"rock_{bpm}bpm.wav");

        AssertMatches(loop, notes);
    }

    [Fact]
    public async Task Crash_on_the_one_reads_as_crash()
    {
        // Hats come back once the crash has died down; a hat right under a loud ringing
        // crash is a known blind spot.
        var loop = new SyntheticLoop(96, bars: 2)
            .Add(DrumKind.Crash, 0)
            .Add(DrumKind.Kick, 0, 8, 16, 24)
            .Add(DrumKind.Snare, 4, 12, 20, 28)
            .Add(DrumKind.ClosedHat, 16, 18, 22, 24, 26, 30);

        var notes = await ConvertAsync(loop, "crash_96bpm.wav");

        AssertMatches(loop, notes);
    }

    [Theory]
    [MemberData(nameof(Tempos))]
    public async Task Hats_under_a_ringing_crash_are_kept(double bpm)
    {
        var loop = new SyntheticLoop(bpm, bars: 2)
            .Add(DrumKind.Crash, 0)
            .Add(DrumKind.Kick, 0, 6, 8, 16, 22, 24)
            .Add(DrumKind.Snare, 4, 12, 20, 28)
            .Add(DrumKind.ClosedHat, 2, 4, 6, 8, 10, 12, 16, 18, 20, 22, 24, 26, 28)
            .Add(DrumKind.OpenHat, 14, 30);

        var notes = await ConvertAsync(loop, $"crashhats_{bpm}bpm.wav");

        AssertMatches(loop, notes);
    }

    [Fact]
    public async Task Played_timing_snaps_back_to_the_grid()
    {
        var loop = new SyntheticLoop(94, bars: 2, jitterMilliseconds: 15)
            .Add(DrumKind.Kick, 0, 3, 8, 10, 16, 19, 24)
            .Add(DrumKind.Snare, 4, 12, 20, 28)
            .Add(DrumKind.ClosedHat, 0, 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30);

        var notes = await ConvertAsync(loop, "played_94bpm.wav");

        AssertMatches(loop, notes);
    }

    [Fact]
    public async Task Loop_without_hats_does_not_invent_hats()
    {
        var loop = new SyntheticLoop(92, bars: 1)
            .Add(DrumKind.Kick, 0, 3, 8, 10)
            .Add(DrumKind.Snare, 4, 12);

        var notes = await ConvertAsync(loop, "boombap_92bpm.wav");

        AssertMatches(loop, notes);
    }

    [Fact]
    public async Task Sixteenth_hats_stay_closed()
    {
        var loop = new SyntheticLoop(110, bars: 1)
            .Add(DrumKind.Kick, 0, 7, 10)
            .Add(DrumKind.Snare, 4, 12)
            .Add(DrumKind.ClosedHat, Enumerable.Range(0, 16).ToArray());

        var notes = await ConvertAsync(loop, "busy_110bpm.wav");

        AssertMatches(loop, notes);
    }

    [Theory]
    [MemberData(nameof(Tempos))]
    public async Task Tom_fill_is_split_by_pitch(double bpm)
    {
        var loop = new SyntheticLoop(bpm, bars: 1)
            .Add(DrumKind.Kick, 0, 8)
            .Add(DrumKind.Snare, 4)
            .Add(DrumKind.ClosedHat, 0, 2, 6)
            .Add(DrumKind.HighTom, 10)
            .Add(DrumKind.MidTom, 12)
            .Add(DrumKind.LowTom, 14);

        var notes = await ConvertAsync(loop, $"fill_{bpm}bpm.wav");

        AssertMatches(loop, notes);
    }

    [Fact]
    public async Task Ride_pattern_reads_as_ride()
    {
        var loop = new SyntheticLoop(104, bars: 2)
            .Add(DrumKind.Kick, 0, 6, 8, 16, 22, 24)
            .Add(DrumKind.Snare, 4, 12, 20, 28)
            .Add(DrumKind.Ride, 0, 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30);

        var notes = await ConvertAsync(loop, "jazzride_104bpm.wav");

        AssertMatches(loop, notes);
    }

    [Fact]
    public async Task Clap_backbeat_reads_as_clap()
    {
        var loop = new SyntheticLoop(98, bars: 1)
            .Add(DrumKind.Kick, 0, 7, 8)
            .Add(DrumKind.Clap, 4, 12)
            .Add(DrumKind.ClosedHat, 0, 2, 6, 8, 10, 14);

        var notes = await ConvertAsync(loop, "clap_98bpm.wav");

        AssertMatches(loop, notes);
    }

    [Fact]
    public async Task Side_stick_reads_as_side_stick()
    {
        var loop = new SyntheticLoop(90, bars: 1)
            .Add(DrumKind.Kick, 0, 8, 10)
            .Add(DrumKind.SideStick, 4, 12)
            .Add(DrumKind.ClosedHat, 0, 2, 6, 8, 10, 14);

        var notes = await ConvertAsync(loop, "rimshot_90bpm.wav");

        AssertMatches(loop, notes);
    }

    [Fact]
    public async Task Percussion_gets_its_own_lane_per_sound()
    {
        var loop = new SyntheticLoop(90, bars: 1)
            .Add(DrumKind.Kick, 0, 8)
            .Add(DrumKind.Tambourine, 1, 5, 9, 13)
            .Add(DrumKind.LowWoodBlock, 3, 7, 11, 15)
            .Add(DrumKind.ClosedHat, 4, 12)
            .Add(DrumKind.HighBongo, 6, 14);

        var notes = await ConvertAsync(loop, "perc_90bpm.wav");

        AssertMatches(loop, notes);
    }

    [Fact]
    public async Task Quiet_repeats_of_a_sound_are_kept()
    {
        var loop = new SyntheticLoop(90, bars: 1)
            .Add(DrumKind.Kick, 0, 8)
            .Add(DrumKind.Clap, 4, 12)
            .Add(DrumKind.LowWoodBlock, 3, 11)
            .Add(DrumKind.LowWoodBlock, 0.2f, 6, 14);

        var notes = await ConvertAsync(loop, "ghosts_90bpm.wav");

        AssertMatches(loop, notes);
    }

    [Fact]
    public async Task Works_out_tempo_from_loop_length()
    {
        var loop = new SyntheticLoop(100, bars: 2)
            .Add(DrumKind.Kick, 0, 8, 16, 24)
            .Add(DrumKind.Snare, 4, 12, 20, 28);
        var path = Path.Combine(_folder, "nameless.wav");
        loop.WriteWav(path);

        var converter = _services.GetRequiredService<IBatchConverter>();
        var result = Assert.Single(await converter.ConvertAsync(new ConversionRequest { InputPath = _folder }, CancellationToken.None));

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(TempoSource.LoopLength, result.Tempo!.Source);
        Assert.Equal(100, result.Tempo.Bpm, 1);
        Assert.Equal(2, result.Tempo.Bars);
    }

    [Fact]
    public async Task Reports_unreadable_file()
    {
        var path = Path.Combine(_folder, "broken.wav");
        await File.WriteAllTextAsync(path, "not a wav", CancellationToken.None);

        var converter = _services.GetRequiredService<ILoopConverter>();
        var result = await converter.ConvertAsync(path, new ConversionRequest { InputPath = path }, CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    private async Task<List<(int Step, DrumKind Kind)>> ConvertAsync(SyntheticLoop loop, string fileName)
    {
        var path = Path.Combine(_folder, fileName);
        loop.WriteWav(path);

        var converter = _services.GetRequiredService<ILoopConverter>();
        var result = await converter.ConvertAsync(path, new ConversionRequest { InputPath = path }, CancellationToken.None);
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(loop.Bpm, result.Tempo!.Bpm, 2);

        var file = MidiFile.Read(result.OutputPath!);
        const int ticksPerStep = MidiConstants.TicksPerQuarterNote / 4;
        return file.GetNotes()
            .Select(n => ((int)(n.Time / ticksPerStep), KindFor(n.NoteNumber)))
            .ToList();
    }

    private static readonly DrumNoteMap Notes = new();

    private static DrumKind KindFor(int note) =>
        Enum.GetValues<DrumKind>().Where(k => Notes.NoteFor(k) == note).DefaultIfEmpty((DrumKind)(-1)).Single() is var kind && Enum.IsDefined(kind)
            ? kind
            : throw new InvalidOperationException($"Unexpected note {note}");

    private static void AssertMatches(SyntheticLoop loop, List<(int Step, DrumKind Kind)> actual)
    {
        var expected = loop.Hits.ToHashSet();
        var found = actual.ToHashSet();
        var missing = expected.Except(found).OrderBy(h => h.Step).ToList();
        var extra = found.Except(expected).OrderBy(h => h.Step).ToList();

        Assert.True(missing.Count == 0 && extra.Count == 0,
            $"Missing: {string.Join(", ", missing)}{Environment.NewLine}Extra: {string.Join(", ", extra)}");
    }
}
