using Microsoft.Extensions.Options;
using WavToStyleMidi.Core.Models;

namespace WavToStyleMidi.Core.Detection;

/// <summary>
/// A group is percussion when the rules only found clap, side stick or hats in it (or nothing)
/// but its mids ring at a resonance, which a clap or hat does not. A lone side stick stays a
/// side stick. Resonant groups get a conga, bongo, wood block or claves note by pitch, a
/// resonant group with a bright jingly top gets tambourine, and an unnamed noise group gets
/// maracas or cabasa. No two groups share a note, so every sound keeps its own lane.
/// </summary>
public sealed class PercussionNamer(IOptions<DetectionOptions> options) : IPercussionNamer
{
    private static readonly HashSet<DrumKind> MaskableKinds = [DrumKind.Clap, DrumKind.SideStick, DrumKind.ClosedHat, DrumKind.OpenHat];

    /// <summary>Pitched percussion from low to high, with the pitch each one goes up to.</summary>
    private static readonly (DrumKind Kind, double UpToHz)[] PitchLadder =
    [
        (DrumKind.LowConga, 250),
        (DrumKind.OpenHighConga, 400),
        (DrumKind.MuteHighConga, 550),
        (DrumKind.LowBongo, 750),
        (DrumKind.HighBongo, 1000),
        (DrumKind.LowWoodBlock, 1500),
        (DrumKind.HighWoodBlock, 2200),
        (DrumKind.Claves, double.MaxValue),
    ];

    private static readonly DrumKind[] NoiseKinds = [DrumKind.Maracas, DrumKind.Cabasa];

    private const double NoiseMaximumMidShare = 0.5;

    /// <summary>Resonances within 4% (under a semitone) are the same instrument.</summary>
    private const double SamePitchRatio = 1.04;

    private readonly DetectionOptions _options = options.Value;

    public IReadOnlyDictionary<int, DrumKind> Name(IReadOnlyList<SoundGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);

        var names = new Dictionary<int, DrumKind>();
        var pitched = new List<SoundGroup>();
        var noise = new List<SoundGroup>();
        var tambourineTaken = false;

        foreach (var group in groups.Where(IsPercussion).OrderByDescending(g => g.TonalShare))
        {
            if (group.Consensus.Count == 0 && group.MidShare < NoiseMaximumMidShare)
            {
                noise.Add(group);
            }
            else if (!tambourineTaken && group.HatShare >= _options.TambourineMinimumHatShare)
            {
                names[group.Id] = DrumKind.Tambourine;
                tambourineTaken = true;
            }
            else
            {
                pitched.Add(group);
            }
        }

        AssignPitched(pitched, names);

        for (var i = 0; i < noise.Count; i++)
        {
            // Past the two shakers, the rest share the last one rather than steal a pitched note.
            names[noise[i].Id] = NoiseKinds[Math.Min(i, NoiseKinds.Length - 1)];
        }

        return names;
    }

    private bool IsPercussion(SoundGroup group)
    {
        if (!group.Consensus.All(MaskableKinds.Contains))
        {
            return false;
        }

        if (group.Consensus.Count == 0)
        {
            return true;
        }

        return !(group.Consensus.Count == 1 && group.Consensus.Contains(DrumKind.SideStick))
            && group.TonalShare >= _options.PercussionMinimumTonalShare;
    }

    /// <summary>
    /// Groups ringing at the same pitch are one instrument played a little differently, so they
    /// share a note. Then each pitch takes the ladder step for it; when two land on the same
    /// step the higher one moves up (or, at the top, the lower one moves down), so pitch order is kept.
    /// </summary>
    private static void AssignPitched(List<SoundGroup> pitched, Dictionary<int, DrumKind> names)
    {
        if (pitched.Count == 0)
        {
            return;
        }

        var instruments = new List<List<SoundGroup>>();
        foreach (var group in pitched.OrderBy(g => g.PitchHz))
        {
            if (instruments.Count > 0 && group.PitchHz <= instruments[^1][0].PitchHz * SamePitchRatio)
            {
                instruments[^1].Add(group);
            }
            else
            {
                instruments.Add([group]);
            }
        }

        // More instruments than notes: the extra ones join the nearest pitch.
        while (instruments.Count > PitchLadder.Length)
        {
            var closest = Enumerable.Range(1, instruments.Count - 1)
                .MinBy(i => instruments[i][0].PitchHz / instruments[i - 1][^1].PitchHz);
            instruments[closest - 1].AddRange(instruments[closest]);
            instruments.RemoveAt(closest);
        }

        var steps = instruments.Select(g => Array.FindIndex(PitchLadder, p => g[0].PitchHz < p.UpToHz)).ToArray();
        for (var i = 1; i < steps.Length; i++)
        {
            steps[i] = Math.Max(steps[i], steps[i - 1] + 1);
        }

        var top = PitchLadder.Length - 1;
        for (var i = steps.Length - 1; i >= 0; i--)
        {
            steps[i] = Math.Min(steps[i], i == steps.Length - 1 ? top : steps[i + 1] - 1);
        }

        for (var i = 0; i < instruments.Count; i++)
        {
            foreach (var group in instruments[i])
            {
                names[group.Id] = PitchLadder[steps[i]].Kind;
            }
        }
    }
}
