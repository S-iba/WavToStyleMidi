using System.Buffers.Binary;
using System.Text;
using Microsoft.Extensions.Options;
using WavToStyleMidi.Core.Models;

namespace WavToStyleMidi.Core.Midi;

public sealed class DrumMidiWriter(IOptions<DrumNoteMap> noteMap) : IDrumMidiWriter
{
    private const int NoteLengthTicks = MidiConstants.TicksPerQuarterNote / 8;
    private readonly DrumNoteMap _noteMap = noteMap.Value;

    public byte[] Build(IReadOnlyList<QuantizedHit> hits, TempoInfo tempo, int channel, string trackName, int format = 1)
    {
        ArgumentNullException.ThrowIfNull(hits);
        ArgumentNullException.ThrowIfNull(tempo);
        ArgumentOutOfRangeException.ThrowIfLessThan(channel, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(channel, 16);
        if (format is not (0 or 1))
        {
            throw new ArgumentOutOfRangeException(nameof(format), format, "Use 0 or 1.");
        }

        var loopTicks = (long)tempo.Bars * tempo.BeatsPerBar * MidiConstants.TicksPerQuarterNote;
        var status = channel - 1;

        List<TimedEvent> conductor =
        [
            new(0, 0, Meta(0x03, Encoding.ASCII.GetBytes(trackName))),
            new(0, 0, Meta(0x51, TempoBytes(tempo.Bpm))),
            new(0, 0, Meta(0x58, TimeSignatureBytes(tempo.BeatsPerBar))),
        ];

        // Format 1 (what style converters expect): tempo and meter in track 1, drums in track 2.
        var drums = format == 0 ? conductor : [new(0, 0, Meta(0x03, Encoding.ASCII.GetBytes("Drums")))];

        var nextSameNote = NextTickForSameNote(hits);
        for (var i = 0; i < hits.Count; i++)
        {
            var hit = hits[i];
            var note = (byte)Math.Clamp(_noteMap.NoteFor(hit.Kind), 0, 127);
            var velocity = (byte)Math.Clamp(hit.Velocity, 1, 127);
            var off = Math.Min(hit.Tick + NoteLengthTicks, Math.Min(nextSameNote[i], loopTicks));
            off = Math.Max(off, hit.Tick + 1);

            drums.Add(new TimedEvent(hit.Tick, 2, [(byte)(0x90 | status), note, velocity]));
            drums.Add(new TimedEvent(off, 1, [(byte)(0x80 | status), note, 0]));
        }

        var tracks = format == 0 ? [conductor] : new[] { conductor, drums };
        var endTick = Math.Max(loopTicks, tracks.Max(t => t.Max(e => e.Tick)));

        using var file = new MemoryStream();
        file.Write("MThd"u8);
        WriteUInt32(file, 6);
        WriteUInt16(file, (ushort)format);
        WriteUInt16(file, (ushort)tracks.Length);
        WriteUInt16(file, MidiConstants.TicksPerQuarterNote);
        foreach (var events in tracks)
        {
            WriteTrack(file, events, endTick);
        }

        return file.ToArray();
    }

    /// <summary>Writes one track chunk. Every track ends exactly at the loop length so the DAW keeps the loop size.</summary>
    private static void WriteTrack(Stream file, List<TimedEvent> events, long endTick)
    {
        // Note-offs before note-ons at the same tick, so a retriggered note is not cut.
        var ordered = events.OrderBy(e => e.Tick).ThenBy(e => e.Order).ToList();

        using var track = new MemoryStream();
        long previous = 0;
        foreach (var e in ordered)
        {
            WriteVariableLength(track, e.Tick - previous);
            track.Write(e.Data);
            previous = e.Tick;
        }

        WriteVariableLength(track, endTick - previous);
        track.Write(Meta(0x2F, []));

        file.Write("MTrk"u8);
        WriteUInt32(file, (uint)track.Length);
        track.WriteTo(file);
    }

    private long[] NextTickForSameNote(IReadOnlyList<QuantizedHit> hits)
    {
        var next = new long[hits.Count];
        var lastSeen = new Dictionary<int, long>();
        for (var i = hits.Count - 1; i >= 0; i--)
        {
            var note = _noteMap.NoteFor(hits[i].Kind);
            next[i] = lastSeen.TryGetValue(note, out var tick) ? tick : long.MaxValue;
            lastSeen[note] = hits[i].Tick;
        }

        return next;
    }

    private static byte[] Meta(byte type, byte[] payload)
    {
        using var stream = new MemoryStream(payload.Length + 6);
        stream.WriteByte(0xFF);
        stream.WriteByte(type);
        WriteVariableLength(stream, payload.Length);
        stream.Write(payload);
        return stream.ToArray();
    }

    private static byte[] TempoBytes(double bpm)
    {
        var microsecondsPerQuarter = (int)Math.Round(60_000_000.0 / bpm);
        return [(byte)(microsecondsPerQuarter >> 16), (byte)(microsecondsPerQuarter >> 8), (byte)microsecondsPerQuarter];
    }

    private static byte[] TimeSignatureBytes(int beatsPerBar) => [(byte)beatsPerBar, 2, 24, 8];

    private static void WriteVariableLength(Stream stream, long value)
    {
        Span<byte> buffer = stackalloc byte[5];
        var index = buffer.Length - 1;
        buffer[index] = (byte)(value & 0x7F);
        while ((value >>= 7) > 0)
        {
            buffer[--index] = (byte)((value & 0x7F) | 0x80);
        }

        stream.Write(buffer[index..]);
    }

    private static void WriteUInt32(Stream stream, uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteUInt16(Stream stream, ushort value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
        stream.Write(buffer);
    }

    private readonly record struct TimedEvent(long Tick, int Order, byte[] Data);
}
