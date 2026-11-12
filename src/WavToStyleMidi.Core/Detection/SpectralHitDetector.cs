using Microsoft.Extensions.Options;
using WavToStyleMidi.Core.Models;

namespace WavToStyleMidi.Core.Detection;

/// <summary>
/// Finds hits with spectral flux, then labels each hit from how much power each band
/// (kick, snare, hats) gained, and for hats how long the high band rings.
/// One onset can produce several hits, e.g. kick and closed hat together.
/// </summary>
public sealed class SpectralHitDetector(IOptions<DetectionOptions> options) : IHitDetector
{
    private const int KickBand = 0;
    private const int SnareBand = 1;
    private const int HatBand = 2;
    private const int BandCount = 3;
    private const double DecayDropDb = 15;
    private const double MaximumDecaySeconds = 1.5;
    private const double FullRangeLowHz = 20;

    private readonly DetectionOptions _options = options.Value;

    public IReadOnlyList<DrumHit> Detect(AudioClip clip, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(clip);

        var hits = new List<DrumHit>();
        foreach (var onset in Analyze(clip, cancellationToken))
        {
            hits.AddRange(Label(onset));
        }

        return hits;
    }

    /// <summary>What was measured at one onset. Exposed to tests for tuning.</summary>
    /// <param name="Kick">Kick-band power gain, 0-1 relative to the loop's strongest kick.</param>
    /// <param name="Snare">Snare-band power gain, 0-1 relative to the loop's strongest snare.</param>
    /// <param name="Hat">Hat-band power gain, 0-1 relative to the loop's strongest hat or cymbal (used for velocity).</param>
    /// <param name="HatPresence">
    /// Hat-band power gain relative to a typical hat in this loop, so one loud crash does not
    /// push the normal hats below the threshold.
    /// </param>
    /// <param name="HatExcess">
    /// How much more high-band power this hit has than a plain snare in this loop would give
    /// (1 = what the snare alone explains). Infinity when there is no snare at this onset.
    /// </param>
    internal readonly record struct OnsetFeatures(
        double TimeSeconds, float Kick, float Snare, float Hat, float HatPresence, double HatExcess, double HatDecayMilliseconds);

    internal IEnumerable<DrumHit> Label(OnsetFeatures onset)
    {
        if (onset.Kick >= _options.KickThreshold)
        {
            yield return new DrumHit(onset.TimeSeconds, DrumKind.Kick, onset.Kick);
        }

        if (onset.Snare >= _options.SnareThreshold)
        {
            yield return new DrumHit(onset.TimeSeconds, DrumKind.Snare, onset.Snare);
        }

        if (onset.HatPresence >= _options.HatThreshold && onset.HatExcess >= _options.HatOverSnareFactor)
        {
            var kind = onset.HatDecayMilliseconds >= _options.CrashDecayMilliseconds ? DrumKind.Crash
                : onset.HatDecayMilliseconds >= _options.OpenHatDecayMilliseconds ? DrumKind.OpenHat
                : DrumKind.ClosedHat;
            yield return new DrumHit(onset.TimeSeconds, kind, onset.Hat);
        }
    }

    internal IReadOnlyList<OnsetFeatures> Analyze(AudioClip clip, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(clip);
        if (clip.Samples.Length == 0)
        {
            return [];
        }

        var spectrogram = Spectrogram.Compute(clip.Samples, clip.SampleRate, _options.WindowMilliseconds, _options.HopMilliseconds, cancellationToken);
        var bands = BandRanges(spectrogram);
        var power = bands.Select(b => BandPower(spectrogram, b)).ToArray();

        // Frames are centred on their time, so a hit starts showing about half a window early.
        var halfWindow = (int)Math.Ceiling(spectrogram.WindowSize / 2.0 / spectrogram.Hop);
        var onsets = FindOnsets(power, halfWindow, spectrogram.HopSeconds);
        if (onsets.Count == 0)
        {
            return [];
        }

        cancellationToken.ThrowIfCancellationRequested();

        var totalPower = BandPower(spectrogram, spectrogram.BinRange(FullRangeLowHz, spectrogram.SampleRate / 2.0));
        var rises = new double[onsets.Count, BandCount];
        var totalRises = new double[onsets.Count];
        for (var i = 0; i < onsets.Count; i++)
        {
            var next = i + 1 < onsets.Count ? onsets[i + 1] : spectrogram.FrameCount;
            for (var b = 0; b < BandCount; b++)
            {
                rises[i, b] = Rise(power[b], onsets[i], next, halfWindow);
            }

            totalRises[i] = Rise(totalPower, onsets[i], next, halfWindow);
        }

        var kick = Normalise(rises, totalRises, KickBand, _ => true);
        var snare = Normalise(rises, totalRises, SnareBand, _ => true);

        // A snare also puts noise in the hat band. The snare hit with the least high end tells
        // us how much of that comes from the snare itself; anything well above it is a hat.
        var snareHits = Enumerable.Range(0, onsets.Count).Where(i => snare[i] >= _options.SnareThreshold).ToList();
        var snareBaseline = snareHits.Count == 0 ? 0 : snareHits.Min(i => HatToSnare(rises, i));
        var hatExcess = Enumerable.Range(0, onsets.Count)
            .Select(i => snare[i] < _options.SnareThreshold || snareBaseline <= 0
                ? double.PositiveInfinity
                : HatToSnare(rises, i) / snareBaseline)
            .ToArray();
        var hatCandidate = (Func<int, bool>)(i => hatExcess[i] >= _options.HatOverSnareFactor);
        var hat = Normalise(rises, totalRises, HatBand, hatCandidate);
        var hatPresence = Presence(rises, HatBand, hatCandidate, hat);

        var hatAmplitude = power[HatBand].Select(p => (float)Math.Sqrt(p)).ToArray();
        var features = new List<OnsetFeatures>(onsets.Count);
        for (var i = 0; i < onsets.Count; i++)
        {
            var next = i + 1 < onsets.Count ? onsets[i + 1] : spectrogram.FrameCount;
            var decay = DecaySeconds(hatAmplitude, onsets[i], next - halfWindow, halfWindow, spectrogram.HopSeconds);
            features.Add(new OnsetFeatures(
                spectrogram.FrameTime(onsets[i]), kick[i], snare[i], hat[i], hatPresence[i], hatExcess[i], decay * 1000.0));
        }

        return features;
    }

    private (int First, int Last)[] BandRanges(Spectrogram spectrogram) =>
    [
        spectrogram.BinRange(_options.KickLowHz, _options.KickHighHz),
        spectrogram.BinRange(_options.SnareLowHz, _options.SnareHighHz),
        spectrogram.BinRange(_options.HatLowHz, Math.Min(_options.HatHighHz, spectrogram.SampleRate / 2.0)),
    ];

    /// <summary>
    /// Onsets from the rise in each band's total power, in dB, over half a window.
    /// Summing a whole band averages out the bin-to-bin flicker of noisy sounds, so a ringing
    /// crash reads as flat and a hat played on top of it still shows as a clear rise.
    /// Rises are capped so one loud hit from silence does not dwarf the rest.
    /// </summary>
    private List<int> FindOnsets(double[][] power, int lag, double hopSeconds)
    {
        var frames = power[0].Length;
        var curve = new float[frames];
        var cap = _options.OnsetRiseCapDb;

        foreach (var band in power)
        {
            // Floor relative to the band's loudest frame, so silence and dither don't count as rises.
            var floor = band.Max() * Math.Pow(10, -_options.OnsetFloorDb / 10);
            if (floor <= 0)
            {
                continue;
            }

            for (var f = lag; f < frames; f++)
            {
                var riseDb = 10 * Math.Log10((band[f] + floor) / (band[f - lag] + floor));
                curve[f] = Math.Max(curve[f], (float)Math.Clamp(riseDb, 0, cap));
            }
        }

        return OnsetPicker.Pick(curve, hopSeconds, _options.OnsetDelta, _options.MinimumHitGapMilliseconds / 1000.0);
    }

    private static double[] BandPower(Spectrogram spectrogram, (int First, int Last) band)
    {
        var power = new double[spectrogram.FrameCount];
        for (var f = 0; f < power.Length; f++)
        {
            var magnitude = spectrogram.Magnitude[f];
            var sum = 0.0;
            for (var k = band.First; k <= band.Last; k++)
            {
                sum += (double)magnitude[k] * magnitude[k];
            }

            power[f] = sum;
        }

        return power;
    }

    /// <summary>Peak power just after the onset minus the level just before it.</summary>
    private static double Rise(double[] power, int onset, int nextOnset, int halfWindow)
    {
        var beforeEnd = Math.Max(0, onset - halfWindow);
        var before = double.MaxValue;
        for (var f = Math.Max(0, beforeEnd - 2); f <= beforeEnd; f++)
        {
            before = Math.Min(before, power[f]);
        }

        var afterEnd = Math.Min(power.Length - 1, Math.Min(onset + halfWindow, Math.Max(onset, nextOnset - halfWindow)));
        var after = 0.0;
        for (var f = onset; f <= afterEnd; f++)
        {
            after = Math.Max(after, power[f]);
        }

        return Math.Max(0, after - before);
    }

    private static double HatToSnare(double[,] rises, int onset) =>
        rises[onset, HatBand] / Math.Max(rises[onset, SnareBand], 1e-12);

    /// <summary>
    /// Scales one band's rises to 0-1 against the strongest qualifying hit. A band that never
    /// holds more than a small share of a hit's power is treated as empty (all zeros), so
    /// bleed from other drums is not mistaken for that drum.
    /// </summary>
    private float[] Normalise(double[,] rises, double[] totalRises, int band, Func<int, bool> qualifies)
    {
        var count = totalRises.Length;
        var result = new float[count];
        var maxShare = 0.0;
        var maxRise = 0.0;
        for (var i = 0; i < count; i++)
        {
            if (!qualifies(i))
            {
                continue;
            }

            maxRise = Math.Max(maxRise, rises[i, band]);
            if (totalRises[i] > 0)
            {
                maxShare = Math.Max(maxShare, rises[i, band] / totalRises[i]);
            }
        }

        if (maxRise <= 0 || maxShare < _options.EmptyBandShare)
        {
            return result;
        }

        for (var i = 0; i < count; i++)
        {
            result[i] = (float)Math.Clamp(rises[i, band] / maxRise, 0, 1);
        }

        return result;
    }

    /// <summary>
    /// Band rise relative to the upper quartile of qualifying hits in a non-empty band.
    /// </summary>
    private static float[] Presence(double[,] rises, int band, Func<int, bool> qualifies, float[] normalised)
    {
        var count = normalised.Length;
        var result = new float[count];
        if (normalised.All(v => v == 0))
        {
            return result;
        }

        var qualifying = Enumerable.Range(0, count).Where(qualifies).Select(i => rises[i, band]).Order().ToList();
        if (qualifying.Count == 0)
        {
            return result;
        }

        var reference = qualifying[(int)Math.Floor((qualifying.Count - 1) * 0.75)];
        if (reference <= 0)
        {
            return result;
        }

        for (var i = 0; i < count; i++)
        {
            result[i] = (float)Math.Min(rises[i, band] / reference, 10);
        }

        return result;
    }

    /// <summary>
    /// Time for the band's level above what was already ringing to fall by
    /// <see cref="DecayDropDb"/>. When the next hit arrives first, the decay is extrapolated
    /// from the drop so far, so a crash under a busy hat pattern still reads as long and an
    /// open hat cut short by a closed hat still reads as open.
    /// </summary>
    private static double DecaySeconds(float[] amplitude, int onset, int stopFrame, int halfWindow, double hopSeconds)
    {
        var background = amplitude[Math.Max(0, onset - halfWindow)];
        float Excess(int f) => Math.Max(0f, amplitude[f] - background);

        var peakFrame = onset;
        for (var f = onset; f < Math.Min(amplitude.Length, onset + halfWindow + 1); f++)
        {
            if (Excess(f) > Excess(peakFrame))
            {
                peakFrame = f;
            }
        }

        var peak = Excess(peakFrame);
        if (peak <= 0)
        {
            return 0;
        }

        var target = peak * (float)Math.Pow(10, -DecayDropDb / 20);
        var limit = Math.Min(amplitude.Length, peakFrame + (int)(MaximumDecaySeconds / hopSeconds));
        var cutOff = stopFrame < limit;
        limit = Math.Min(limit, Math.Max(stopFrame, peakFrame + 1));

        var frame = peakFrame;
        while (frame < limit && Excess(frame) > target)
        {
            frame++;
        }

        if (frame < limit || !cutOff)
        {
            return (frame - onset) * hopSeconds;
        }

        var last = Math.Max(peakFrame, limit - 1);
        var elapsed = (last - peakFrame) * hopSeconds;
        var dropDb = 20 * Math.Log10(peak / Math.Max(Excess(last), 1e-9f));
        if (elapsed <= 0 || dropDb < 1)
        {
            return MaximumDecaySeconds;
        }

        var peakOffset = (peakFrame - onset) * hopSeconds;
        return Math.Min(MaximumDecaySeconds, peakOffset + elapsed * DecayDropDb / dropDb);
    }
}
