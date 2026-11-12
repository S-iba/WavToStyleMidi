using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Microsoft.Extensions.Options;
using WavToStyleMidi.Core.Midi;
using WavToStyleMidi.Core.Models;

namespace WavToStyleMidi.Core.Tests;

public sealed class DrumMidiWriterTests
{
    private readonly DrumMidiWriter _writer = new(Options.Create(new DrumNoteMap()));

    [Fact]
    public void Writes_type0_file_with_tempo_notes_and_loop_length()
    {
        var tempo = new TempoInfo(95, 2, 4, TempoSource.FileName);
        var hits = new[]
        {
            new QuantizedHit(0, DrumKind.Kick, 110),
            new QuantizedHit(0, DrumKind.ClosedHat, 80),
            new QuantizedHit(960, DrumKind.Snare, 120),
        };

        var bytes = _writer.Build(hits, tempo, channel: 10, trackName: "groove");
        var file = MidiFile.Read(new MemoryStream(bytes));

        Assert.Equal(MidiFileFormat.SingleTrack, file.OriginalFormat);
        Assert.Equal(MidiConstants.TicksPerQuarterNote, ((TicksPerQuarterNoteTimeDivision)file.TimeDivision).TicksPerQuarterNote);

        var tempoMap = file.GetTempoMap();
        Assert.Equal(95, tempoMap.GetTempoAtTime(new MidiTimeSpan(0)).BeatsPerMinute, 1);

        var notes = file.GetNotes().ToList();
        Assert.Equal(3, notes.Count);
        Assert.All(notes, n => Assert.Equal(9, (int)n.Channel));
        Assert.Equal([36, 42, 38], notes.Select(n => (int)n.NoteNumber));
        Assert.Equal(960, notes[2].Time);

        // DryWetMidi drops End of Track on read, so read its delta from the raw bytes.
        var track = Assert.Single(file.GetTrackChunks());
        var lastEventTick = track.Events.Sum(e => e.DeltaTime);
        Assert.Equal(2 * 4 * MidiConstants.TicksPerQuarterNote, lastEventTick + EndOfTrackDelta(bytes));
    }

    [Fact]
    public void Shortens_note_so_a_fast_repeat_is_not_cut()
    {
        var tempo = new TempoInfo(120, 1, 4, TempoSource.Argument);
        var hits = new[]
        {
            new QuantizedHit(0, DrumKind.ClosedHat, 100),
            new QuantizedHit(30, DrumKind.ClosedHat, 100),
        };

        var file = MidiFile.Read(new MemoryStream(_writer.Build(hits, tempo, 10, "fast")));
        var notes = file.GetNotes().ToList();

        Assert.Equal(2, notes.Count);
        Assert.Equal(30, notes[0].Length);
    }

    private static long EndOfTrackDelta(byte[] bytes)
    {
        Assert.Equal([0xFF, 0x2F, 0x00], bytes[^3..]);

        var start = bytes.Length - 4;
        while (start > 0 && (bytes[start - 1] & 0x80) != 0)
        {
            start--;
        }

        long value = 0;
        for (var i = start; i <= bytes.Length - 4; i++)
        {
            value = (value << 7) | (uint)(bytes[i] & 0x7F);
        }

        return value;
    }
}
