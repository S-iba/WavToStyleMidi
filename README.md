# WavToStyleMidi

Turns WAV drum loops into General MIDI drum files. You can then build them into
Yamaha (PSR-SX, SFF) or Roland (BK/E/G) styles in your DAW and style converter.

The tool doesn't import the audio. It transcribes the groove, so the keyboard plays
it on its own drum kit:

| Drum        | GM note |
|-------------|---------|
| Kick        | 36      |
| Side stick  | 37      |
| Snare       | 38      |
| Clap        | 39      |
| Low tom     | 41      |
| Closed hat  | 42      |
| Mid tom     | 45      |
| Open hat    | 46      |
| High tom    | 48      |
| Crash       | 49      |
| Ride        | 51      |

Percussion gets its own notes (see "Percussion" below):

| Percussion       | GM note |
|------------------|---------|
| Tambourine       | 54      |
| High bongo       | 60      |
| Low bongo        | 61      |
| Mute high conga  | 62      |
| Open high conga  | 63      |
| Low conga        | 64      |
| Cabasa           | 69      |
| Maracas          | 70      |
| Claves           | 75      |
| High wood block  | 76      |
| Low wood block   | 77      |

All of these are in `DrumNotes` in `appsettings.json` if your kit maps them differently.

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
   power rises (in dB) in five bands: kick (30–110 Hz), toms (80–450 Hz), side stick
   (400–2000 Hz), snare (1–5 kHz) and hats (7–16 kHz). It measures each band as a whole, so
   the flicker of a ringing cymbal reads as flat and a hit played on top of it still shows
   up. A second rise in the same bands within 80 ms is treated as the same hit.
3. **Label hits.** For each hit it measures what came in and how:
   - **Kick or tom:** the pitch of the low end about 90 ms after the hit. Below 80 Hz it's a
     kick. Above that it's a tom, as long as the low end clearly outweighs the noise, which
     rules out a snare's body. Toms split into low (below 115 Hz), mid (below 175 Hz) and
     high.
   - **Snare or clap:** a strong 1–5 kHz gain. With a 150–350 Hz body it's a snare; with
     no body it's a clap.
   - **Side stick:** a short click (under 60 ms) that carries most of its power at 400–2000 Hz.
   - **Ride:** a top end made of separate tones rather than noise.
   - **Hats and crash:** high-band gain. On a snare, clap or tom hit, a hat is only added
     when the hat band holds clearly more than that drum's plainest hit would explain.
     Ring time sorts them: closed (short), open (above 120 ms) or crash (above 450 ms). If
     the next hit cuts the ring short, the length is estimated from how fast it was falling.
   - A band that never carries a real share of any hit's power counts as empty, so bleed
     from other drums isn't labelled as that drum.
4. **Group by sound.** `SoundGrouper` gives every hit a fingerprint (the shape of the
   spectrum it added, in 24 bands, plus how long it rings) and groups hits that sound alike.
   `PercussionNamer` then decides which groups are percussion (see below). Other groups keep
   the drums the rules found. A clear hit the rules missed, such as a ghost note, takes its
   group's drums when the sound repeats in the loop. Set `GroupBySound` to `false` to turn
   this off.
5. **Quantize.** `GrooveQuantizer` snaps hits to the grid at the strength you chose and keeps
   only the loudest of any duplicate hits.
6. **Fill hats.** `HatPatternFiller` handles hats hidden by another drum: on a snare, on the
   slot right after a snare, and while a crash is ringing (600 ms by default). When at least
   80% of the other 8th or 16th slots have a hat, it adds the hidden ones.
7. **Write.** `DrumMidiWriter` writes the SMF.

Every step sits behind an interface (`IHitDetector`, `IGrooveQuantizer`, ...) and is
registered with `services.AddWavToStyleMidi()`. That means a different detector, such as an
ONNX drum-transcription model, can replace `SpectralHitDetector` later.

### Percussion

A group is percussion when the rules found only clap, side stick or hats in it (or nothing),
but its mids (300 Hz–5 kHz) ring at a clear resonance, which a real clap or hat doesn't. A
group that is only a side stick stays a side stick.

- A resonance with a bright jingle on top (`TambourineMinimumHatShare`) is a tambourine.
- Other resonances get a note by pitch: low conga (below 250 Hz), open high conga, mute high
  conga, low bongo, high bongo (below 1 kHz), low wood block (below 1.5 kHz), high wood block
  (below 2.2 kHz), then claves.
- An unnamed noise sound with little in the mids is maracas, then cabasa.
- No two percussion sounds share a note, so each keeps its own lane. If two land on the same
  pitch step, the higher one moves up a note, so pitch order is kept. Groups ringing at the
  same pitch (within 4%) are one instrument and share a note.

The names are a best guess from pitch and noise. What matters is that every sound gets its own
lane with the right timing. If a name is wrong, change it for the whole lane in FL Studio's
piano roll, or change the note in `DrumNotes`.

Percussion hits are also kept out of the levels that the snare and hat thresholds compare
against, so a loud wood block doesn't hide a real clap.

### Tuning

All detection thresholds and the note map are in `src/WavToStyleMidi.Cli/appsettings.json`
(sections `Detection` and `DrumNotes`). Change them there without rebuilding. Common fixes:

| Symptom                                   | Try                                                                  |
|-------------------------------------------|----------------------------------------------------------------------|
| Ghost notes or quiet hits missing         | Lower `OnsetDelta` (e.g. 0.08) or the drum's `...Threshold`          |
| Extra hits from reverb or room sound      | Raise `OnsetDelta` or `MinimumHitGapMilliseconds`                    |
| Open hats come out closed (or the reverse)| Adjust `OpenHatDecayMilliseconds`                                    |
| Your kit uses other notes                 | Change `DrumNotes`                                                   |
| Kicks come out as low toms (or reverse)   | Adjust `TomMinimumPitchHz`                                           |
| Snares come out as claps (or reverse)     | Adjust `ClapMaximumBodyRatio`                                        |
| Unwanted hats added on snares or crashes  | Set `FillMaskedHats` to `false`                                      |
| Two different sounds share a lane         | Lower `SoundGroupDistance` (e.g. 0.07)                               |
| One sound is split across two lanes       | Raise `SoundGroupDistance` (e.g. 0.13)                               |
| A clap or hat comes out as percussion     | Raise `PercussionMinimumTonalShare`                                  |

### Known limits

- Percussion names are guesses from pitch (see Percussion). A low conga sounds like a tom
  and is labelled as one. Cowbell has no rule of its own and comes out as a pitched
  percussion note.
- A strongly tuned clap (a narrow band of noise) can read as percussion.
- The same tom struck twice quickly (16ths) while it's still ringing often comes out as
  one hit. A fill that moves between toms is fine.
- A kick tuned above 80 Hz reads as a low tom, and a tom below 80 Hz as a kick. Adjust
  `TomMinimumPitchHz` to suit your kits.
- In a ride pattern, a hat played with a ride hit is labelled as ride.
- A hat that is much quieter than a crash still ringing over it doesn't show up in the audio.
  The tool only puts it back when the rest of the loop has a steady hat pattern.
- The rules are tuned on synthetic drums (see Tests) and one real percussion loop. Real loops with heavy processing
  may need threshold changes; see the tuning table above.

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

The end-to-end tests synthesise drum loops with known patterns: straight rock at three
tempos, timing jitter, busy 16ths, no hats, crash, hats under a ringing crash, a tom fill,
a ride pattern, claps, side stick, a percussion loop (tambourine, wood block, bongo) and
quiet ghost notes. They run each loop through the full pipeline and check
that the MIDI contains exactly the hits that were played.
