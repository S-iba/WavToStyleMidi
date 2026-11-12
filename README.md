# WavToStyleMidi

Turns WAV drum loops into General MIDI drum files. You can then build them into
Yamaha (PSR-SX, SFF) or Roland (BK/E/G) styles in your DAW and style converter.

The tool doesn't import the audio. It transcribes the groove, so the keyboard plays
it on its own drum kit:

| Drum       | GM note |
|------------|---------|
| Kick       | 36      |
| Snare      | 38      |
| Closed hat | 42      |
| Open hat   | 46      |
| Crash      | 49      |

## Build and run

Requires the .NET 10 SDK.

```bash
dotnet build
dotnet run --project src/WavToStyleMidi.Cli -- "C:\Loops" -o "C:\Loops\midi"
```

Or publish a single executable:

```bash
dotnet publish src/WavToStyleMidi.Cli -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
# -> wav2style.exe
```

```
wav2style <file.wav | folder> [options]

  -o, --output <folder>   Where to write .mid files (default: next to each .wav)
      --bpm <number>      Loop tempo (default: from the file name, else from the length)
      --bars <number>     Bars in the loop (default: from tempo and length)
      --beats <number>    Beats per bar (default: 4)
      --grid <number>     8, 16, 32, or 12/24 for triplets (default: 16)
      --quantize <0-1>    0 keeps the played feel, 1 snaps fully (default: 1)
      --channel <1-16>    MIDI channel (default: 10)
  -r, --recursive         Include subfolders
```

Each loop prints a summary line like this one:

```
OK      funk 95bpm.wav -> funk 95bpm.mid | 95 BPM (file name), 2 bar(s) | kick 6, snare 4, closed hat 14
```

### Tempo

The tool works out the tempo in this order:

1. `--bpm`
2. A BPM in the file name: `funk_95bpm.wav`, `Funk 95 BPM.wav`, `BPM120_groove.wav`
3. The loop length, assuming the loop holds 1, 2, 4, 8 or 16 whole bars at 70 to 180 BPM.
   Some lengths fit two tempos, for example 80 BPM over 1 bar or 160 BPM over 2. When that
   happens the summary says `check this`. Pass `--bpm` to settle it.

The loop should be trimmed to exact bars. A tail longer than half a bar adds an extra bar.

## Output

- A type 0 Standard MIDI File at 480 PPQ, with tempo and time signature.
- All drums on one channel, 10 by default.
- The track ends exactly at the loop length, so it keeps its size when you drop it into a pattern.
- Velocity follows how hard each hit was.

## How it works

`WavToStyleMidi.Core` holds all the logic. The CLI is a thin host around it.

1. **Load.** `WavAudioLoader` reads the WAV (PCM 16/24/32-bit or float) and mixes it to mono.
2. **Find hits.** `SpectralHitDetector` computes a short-time spectrum and looks for sudden
   rises in three bands: kick (30–110 Hz), snare (1–5 kHz) and hats (7–16 kHz).
3. **Label hits.** For each hit it measures how much power each band gained:
   - A kick or snare is labelled when its band gained strongly compared with the other hits
     in the same loop. A band that never carries a real share of any hit counts as empty,
     so bleed from other drums isn't labelled as that drum.
   - A snare also puts noise in the hat band. A hat on a snare hit is only added when the
     hat band holds clearly more than the loop's plainest snare would explain.
   - Hats are sorted by how long they ring: closed (short), open (above 120 ms) or crash
     (above 450 ms). If the next hit cuts the ring short, the length is estimated from how
     fast it was falling.
4. **Quantize.** `GrooveQuantizer` snaps hits to the grid at the strength you chose and keeps
   only the loudest of any duplicate hits.
5. **Fill hats.** `HatPatternFiller` handles hats hidden by a snare. When at least 80% of the
   non-snare 8th or 16th slots have a hat, it adds the hats on the snare hit and on the slot
   right after it.
6. **Write.** `DrumMidiWriter` writes the SMF.

Every step sits behind an interface (`IHitDetector`, `IGrooveQuantizer`, ...) and is
registered with `services.AddWavToStyleMidi()`. That means a different detector, such as an
ONNX drum-transcription model, can replace `SpectralHitDetector` later.

### Tuning

All detection thresholds and the note map are in `src/WavToStyleMidi.Cli/appsettings.json`
(sections `Detection` and `DrumNotes`). Change them there without rebuilding. Common fixes:

| Symptom                                   | Try                                                                  |
|-------------------------------------------|----------------------------------------------------------------------|
| Ghost notes or quiet hits missing         | Lower `OnsetDelta` (e.g. 0.08) or the drum's `...Threshold`          |
| Extra hits from reverb or room sound      | Raise `OnsetDelta` or `MinimumHitGapMilliseconds`                    |
| Open hats come out closed (or the reverse)| Adjust `OpenHatDecayMilliseconds`                                    |
| Your kit uses other notes (e.g. side stick)| Change `DrumNotes`                                                  |
| Unwanted hats added on snares             | Set `FillHatsUnderSnares` to `false`                                 |

### Known limits

- The tool only knows five drums: kick, snare, closed hat, open hat and crash. Toms, ride,
  claps and percussion come out as whichever of those they sound most like.
- A hat played under a loud, still-ringing crash can be missed.
- A loop with heavy reverb or compression across the whole mix may need threshold tuning.

## Workflow to a style

1. Run the tool over a folder of loops.
2. In FL Studio, load the MIDI into a style template that has the section markers
   (Intro, Main A–D, Fill In, Ending) and build the variations.
3. Export the SMF and convert it to `.sty` with Style Converter XT. Then use StyleWorks XT
   for the Roland BK version.

## Tests

```bash
dotnet test
```

The end-to-end tests synthesise drum loops with known patterns (straight, played with timing
jitter, busy 16ths, no hats, crash). They run each loop through the full pipeline and check
that the MIDI contains exactly the hits that were played.
