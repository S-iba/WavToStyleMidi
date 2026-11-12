using Microsoft.Extensions.Options;
using WavToStyleMidi.Core.Models;

namespace WavToStyleMidi.Core.Detection;

/// <summary>
/// Finds hits from sudden power rises in a set of frequency bands, then labels each hit from
/// which bands came in (in dB, against what was already ringing), the pitch of its low end,
/// whether its top end is noise or separate tones, and how long it rings.
/// One onset can produce several hits, e.g. kick and closed hat together.
/// </summary>
public sealed class SpectralHitDetector(IOptions<DetectionOptions> options) : IHitDetector
{
    private const int KickBand = 0;
    private const int TomBand = 1;
    private const int SnareBand = 2;
    private const int HatBand = 3;
    private const int BodyBand = 4;
    private const int RimBand = 5;
    private const int MidBand = 6;
    private const int CymbalBand = 7;
    private const int BandCount = 8;
    private static readonly int[] OnsetBands = [KickBand, TomBand, SnareBand, HatBand, RimBand];

    private const double PitchLowHz = 35;
    private const double PitchHighHz = 450;
    private const double DecayDropDb = 15;
    private const double MaximumDecaySeconds = 1.5;
    private const double MinimumExtrapolationSeconds = 0.04;
    private const double FullRangeLowHz = 20;
    private const double LevelFloorDb = -60;

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

    /// <summary>How one band changed at an onset.</summary>
    /// <param name="RiseDb">Level just after the onset over the level just before it.</param>
    /// <param name="LevelDb">Level just after the onset against this band's loudest hit in the loop (0 = loudest).</param>
    /// <param name="Gain">Power added by the hit (after minus before), for comparing bands.</param>
    /// <param name="Relative">Gain against the largest gain in this band in the loop (0-1).</param>
    /// <param name="Typical">Gain against a typical (upper-quartile) gain in this band, so one loud crash doesn't hide normal hats.</param>
    internal readonly record struct BandChange(double RiseDb, double LevelDb, double Gain, double Relative, double Typical);

    /// <summary>What was measured at one onset. Exposed to tests for tuning.</summary>
    /// <param name="PitchHz">Strongest low frequency (35-450 Hz) once the hit has settled.</param>
    /// <param name="TonalShare">
    /// Share of 2-9 kHz power just after the hit that sits in narrow peaks: near 0 for noise
    /// (snare, hats, crash), high for the separate tones of a ride.
    /// </param>
    /// <param name="HatExcess">
    /// High-band gain beyond what the snare, clap or tom at this onset explains; 1 means fully
    /// explained. Infinity when there is none of those.
    /// </param>
    internal readonly record struct OnsetFeatures(
        double TimeSeconds,
        BandChange Kick,
        BandChange Tom,
        BandChange Snare,
        BandChange Hat,
        BandChange Rim,
        double PitchHz,
        double BodyRatio,
        double LowMidToNoise,
        double RimShare,
        double MidDecayMilliseconds,
        double HatDecayMilliseconds,
        double TonalShare,
        double HatExcess);

    internal IEnumerable<DrumHit> Label(OnsetFeatures onset)
    {
        var lowPitched = onset.PitchHz < _options.TomMinimumPitchHz;
        var isRide = IsRide(onset);
        var isSideStick = !isRide && IsSideStick(onset);
        var isTom = !isSideStick && IsTom(onset);
        var isSnareOrClap = !isRide && !isSideStick && !isTom && IsSnareActive(onset);

        if (IsKick(onset))
        {
            yield return Hit(onset, DrumKind.Kick, onset.Kick);
        }

        if (isTom)
        {
            var tom = onset.PitchHz < _options.LowTomMaximumPitchHz ? DrumKind.LowTom
                : onset.PitchHz < _options.MidTomMaximumPitchHz ? DrumKind.MidTom
                : DrumKind.HighTom;
            yield return TomHit(onset, tom);
        }

        if (isRide)
        {
            // The ride fills the snare and hat bands itself, so a snare on a ride hit is found by
            // its body arriving (a clear jump at 80-450 Hz, strong against the 1-5 kHz gain).
            // Not with a kick, whose low end also lands there.
            yield return Hit(onset, DrumKind.Ride, onset.Snare.Relative >= onset.Hat.Relative ? onset.Snare : onset.Hat);
            if (!IsKick(onset) && IsTomActive(onset.Tom) && onset.BodyRatio >= _options.RideSnareMinimumBodyRatio)
            {
                yield return Hit(onset, DrumKind.Snare, onset.Snare);
            }

            yield break;
        }

        if (isSideStick)
        {
            yield return Hit(onset, DrumKind.SideStick, onset.Rim);
        }

        if (isSnareOrClap)
        {
            var kind = onset.BodyRatio >= _options.ClapMaximumBodyRatio ? DrumKind.Snare : DrumKind.Clap;
            yield return Hit(onset, kind, onset.Snare);
        }

        if (IsHatActive(onset) && onset.HatExcess >= _options.HatOverSnareFactor)
        {
            var kind = onset.HatDecayMilliseconds >= _options.CrashDecayMilliseconds ? DrumKind.Crash
                : onset.HatDecayMilliseconds >= _options.OpenHatDecayMilliseconds ? DrumKind.OpenHat
                : DrumKind.ClosedHat;
            yield return Hit(onset, kind, onset.Hat);
        }
    }

    /// <summary>
    /// Toms ring, so a repeated tom adds little power on top of the last one. They are judged
    /// by a clear jump in dB and a level not far below the loudest low-mid hit.
    /// </summary>
    private bool IsTomActive(BandChange band) =>
        band.RiseDb >= _options.BandRiseDb && band.LevelDb >= -_options.BandLevelRangeDb;

    private bool IsSnareActive(OnsetFeatures onset) => onset.Snare.Relative >= _options.SnareThreshold;

    private bool IsHatActive(OnsetFeatures onset) => onset.Hat.Typical >= _options.HatThreshold;

    private static DrumHit Hit(OnsetFeatures onset, DrumKind kind, BandChange band) =>
        new(onset.TimeSeconds, kind, (float)Math.Clamp(band.Relative, 0, 1));

    private DrumHit TomHit(OnsetFeatures onset, DrumKind kind) =>
        new(onset.TimeSeconds, kind, (float)Math.Clamp(1 + onset.Tom.LevelDb / _options.BandLevelRangeDb, 0, 1));

    private bool IsTom(OnsetFeatures onset) =>
        IsTomActive(onset.Tom)
        && onset.PitchHz >= _options.TomMinimumPitchHz
        && onset.LowMidToNoise >= _options.TomMinimumLowMidToNoise;

    /// <summary>
    /// A ride re-struck while ringing adds little power, so it only needs a clear jump in the
    /// snare or hat band on top of a tonal top end.
    /// </summary>
    private bool IsRide(OnsetFeatures onset) =>
        onset.TonalShare >= _options.RideMinimumTonalShare
        && (onset.Snare.RiseDb >= _options.BandRiseDb || onset.Hat.RiseDb >= _options.BandRiseDb);

    /// <summary>
    /// Either a big share of the loop's biggest kick, or a clear jump not far below it, so a
    /// kick next to a louder one (or a ringing low tom) still counts.
    /// </summary>
    private bool IsKick(OnsetFeatures onset) =>
        onset.PitchHz < _options.TomMinimumPitchHz
        && (onset.Kick.Relative >= _options.KickThreshold
            || (onset.Kick.RiseDb >= 2 * _options.BandRiseDb && onset.Kick.LevelDb >= -_options.BandLevelRangeDb / 2));

    private bool IsSideStick(OnsetFeatures onset) =>
        onset.Rim.Relative >= _options.SideStickThreshold
        && onset.RimShare >= _options.SideStickMinimumShare
        && onset.MidDecayMilliseconds < _options.SideStickMaximumDecayMilliseconds;

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
        var onsets = FindOnsets(OnsetBands.Select(b => power[b]).ToArray(), halfWindow, spectrogram.HopSeconds);
        if (onsets.Count == 0)
        {
            return [];
        }

        cancellationToken.ThrowIfCancellationRequested();

        var count = onsets.Count;
        var next = Enumerable.Range(0, count).Select(i => i + 1 < count ? onsets[i + 1] : spectrogram.FrameCount).ToArray();
        var totalPower = BandPower(spectrogram, spectrogram.BinRange(FullRangeLowHz, spectrogram.SampleRate / 2.0));

        var before = new double[count, BandCount];
        var after = new double[count, BandCount];
        var totalGain = new double[count];
        for (var i = 0; i < count; i++)
        {
            for (var b = 0; b < BandCount; b++)
            {
                (before[i, b], after[i, b]) = BeforeAfter(power[b], onsets[i], next[i], halfWindow);
            }

            var (tb, ta) = BeforeAfter(totalPower, onsets[i], next[i], halfWindow);
            totalGain[i] = Math.Max(0, ta - tb);
        }

        double Gain(int i, int b) => Math.Max(0, after[i, b] - before[i, b]);

        var changes = new BandChange[count, BandCount];
        for (var b = 0; b < BandCount; b++)
        {
            var loudest = Enumerable.Range(0, count).Max(i => after[i, b]);
            var gains = Enumerable.Range(0, count).Select(i => Gain(i, b)).ToArray();
            var maxGain = gains.Max();
            var audible = gains.Where(g => g > maxGain * 0.01).Order().ToList();
            var typicalGain = audible.Count == 0 ? 0 : audible[(int)Math.Floor((audible.Count - 1) * 0.75)];
            var floor = Math.Max(loudest * Math.Pow(10, LevelFloorDb / 10), 1e-20);

            // A band that never carries a real share of any hit's added power only holds bleed.
            var maxShare = Enumerable.Range(0, count).Max(i => totalGain[i] > 0 ? Gain(i, b) / totalGain[i] : 0);
            var empty = maxShare < _options.EmptyBandShare;

            for (var i = 0; i < count; i++)
            {
                changes[i, b] = empty
                    ? new BandChange(0, LevelFloorDb, 0, 0, 0)
                    : new BandChange(
                        10 * Math.Log10((after[i, b] + floor) / (before[i, b] + floor)),
                        10 * Math.Log10((after[i, b] + floor) / (loudest + floor)),
                        gains[i],
                        maxGain > 0 ? gains[i] / maxGain : 0,
                        typicalGain > 0 ? Math.Min(gains[i] / typicalGain, 10) : 0);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        var amplitude = power.Select(band => band.Select(p => (float)Math.Sqrt(p)).ToArray()).ToArray();
        double DecayMs(int band, int i) =>
            DecaySeconds(amplitude[band], onsets[i], next[i] - halfWindow, halfWindow, spectrogram.HopSeconds) * 1000.0;

        var pitchDelay = (int)Math.Round(_options.PitchDelayMilliseconds / 1000.0 / spectrogram.HopSeconds);
        var features = new OnsetFeatures[count];
        for (var i = 0; i < count; i++)
        {
            var pitchFrame = Math.Max(onsets[i], Math.Min(onsets[i] + pitchDelay, next[i] - halfWindow));
            features[i] = new OnsetFeatures(
                spectrogram.FrameTime(onsets[i]),
                changes[i, KickBand],
                changes[i, TomBand],
                changes[i, SnareBand],
                changes[i, HatBand],
                changes[i, RimBand],
                PeakPitch(spectrogram, pitchFrame, PitchLowHz, PitchHighHz),
                BodyRatio: Gain(i, BodyBand) / Math.Max(Gain(i, SnareBand), 1e-20),
                LowMidToNoise: Gain(i, TomBand) / Math.Max(Gain(i, SnareBand), 1e-20),
                RimShare: totalGain[i] > 0 ? Gain(i, RimBand) / totalGain[i] : 0,
                MidDecayMilliseconds: DecayMs(MidBand, i),
                HatDecayMilliseconds: DecayMs(HatBand, i),
                TonalShare: TonalShare(spectrogram, bands[CymbalBand], Math.Min(onsets[i] + 2, Math.Max(onsets[i], next[i] - halfWindow))),
                HatExcess: double.PositiveInfinity);
        }

        // A snare, clap or tom also puts noise in the hat band. For each of them, its hit with
        // the least high end tells us how much comes from the drum itself; well above that is a hat.
        var carriers = new Dictionary<DrumKind, List<int>>();
        for (var i = 0; i < count; i++)
        {
            var carrier = Carrier(features[i]);
            if (carrier is { } kind)
            {
                (carriers.TryGetValue(kind, out var list) ? list : carriers[kind] = []).Add(i);
            }
        }

        foreach (var (_, members) in carriers)
        {
            var baseline = members.Min(i => HatRatio(features[i]));
            foreach (var i in members)
            {
                features[i] = features[i] with
                {
                    HatExcess = baseline <= 0 ? double.PositiveInfinity : HatRatio(features[i]) / baseline,
                };
            }
        }

        // A ride's partials land in the snare band too, so snares are measured against non-ride hits.
        var snareGains = Enumerable.Range(0, count).Where(i => !IsRide(features[i])).Select(i => features[i].Snare.Gain).ToList();
        var snareMax = snareGains.Count == 0 ? 0 : snareGains.Max();
        for (var i = 0; i < count; i++)
        {
            features[i] = features[i] with
            {
                Snare = features[i].Snare with { Relative = snareMax > 0 ? Math.Min(features[i].Snare.Gain / snareMax, 1) : 0 },
            };
        }

        // Hats are measured against hits that are hats, not snare noise in the hat band.
        var hatCandidates = Enumerable.Range(0, count).Where(i => features[i].HatExcess >= _options.HatOverSnareFactor).ToList();
        var hatGains = hatCandidates.Select(i => features[i].Hat.Gain).ToList();

        // If no candidate carries a real share of its hit's power in the hat band, there are no hats.
        if (hatCandidates.All(i => totalGain[i] <= 0 || features[i].Hat.Gain / totalGain[i] < _options.EmptyBandShare))
        {
            hatGains.Clear();
        }

        var hatMax = hatGains.Count == 0 ? 0 : hatGains.Max();
        var hatAudible = hatGains.Where(g => g > hatMax * 0.01).Order().ToList();
        var hatTypical = hatAudible.Count == 0 ? 0 : hatAudible[(int)Math.Floor((hatAudible.Count - 1) * 0.75)];
        for (var i = 0; i < count; i++)
        {
            var hat = features[i].Hat;
            features[i] = features[i] with
            {
                Hat = hat with
                {
                    Relative = hatMax > 0 ? Math.Min(hat.Gain / hatMax, 1) : 0,
                    Typical = hatTypical > 0 ? Math.Min(hat.Gain / hatTypical, 10) : 0,
                },
            };
        }

        return features;
    }

    /// <summary>Which non-cymbal drum at this onset also makes high-end noise.</summary>
    private DrumKind? Carrier(OnsetFeatures onset)
    {
        if (IsRide(onset) || IsSideStick(onset))
        {
            return null;
        }

        if (IsTom(onset))
        {
            return DrumKind.LowTom;
        }

        if (IsSnareActive(onset))
        {
            return onset.BodyRatio >= _options.ClapMaximumBodyRatio ? DrumKind.Snare : DrumKind.Clap;
        }

        return null;
    }

    /// <summary>
    /// Hat-band gain over 1-5 kHz gain. Snares, claps and a tom's stick attack each have a
    /// fairly fixed balance between the two; a hat on top tips it towards the hat band.
    /// </summary>
    private static double HatRatio(OnsetFeatures onset) =>
        onset.Hat.Gain / Math.Max(onset.Snare.Gain, 1e-20);

    private (int First, int Last)[] BandRanges(Spectrogram spectrogram)
    {
        var nyquist = spectrogram.SampleRate / 2.0;
        return
        [
            spectrogram.BinRange(_options.KickLowHz, _options.KickHighHz),
            spectrogram.BinRange(_options.TomLowHz, _options.TomHighHz),
            spectrogram.BinRange(_options.SnareLowHz, _options.SnareHighHz),
            spectrogram.BinRange(_options.HatLowHz, Math.Min(_options.HatHighHz, nyquist)),
            spectrogram.BinRange(150, 350),
            spectrogram.BinRange(400, 2000),
            spectrogram.BinRange(300, 5000),
            spectrogram.BinRange(2000, Math.Min(9000, nyquist)),
        ];
    }

    /// <summary>
    /// Onsets from the rise in each band's total power, in dB, over half a window.
    /// Summing a whole band averages out the bin-to-bin flicker of noisy sounds, so a ringing
    /// crash reads as flat and a hat played on top of it still shows as a clear rise.
    /// Rises are capped so one loud hit from silence does not dwarf the rest.
    /// </summary>
    private List<int> FindOnsets(double[][] power, int lag, double hopSeconds)
    {
        var frames = power[0].Length;
        var cap = _options.OnsetRiseCapDb;
        var bandRise = new float[power.Length][];
        var curve = new float[frames];

        for (var b = 0; b < power.Length; b++)
        {
            var band = power[b];
            bandRise[b] = new float[frames];

            // Floor relative to the band's loudest frame, so silence and dither don't count as rises.
            var floor = band.Max() * Math.Pow(10, -_options.OnsetFloorDb / 10);
            if (floor <= 0)
            {
                continue;
            }

            for (var f = lag; f < frames; f++)
            {
                var riseDb = 10 * Math.Log10((band[f] + floor) / (band[f - lag] + floor));
                bandRise[b][f] = (float)Math.Clamp(riseDb, 0, cap);
                curve[f] = Math.Max(curve[f], bandRise[b][f]);
            }
        }

        var picked = OnsetPicker.Pick(curve, hopSeconds, _options.OnsetDelta, _options.MinimumHitGapMilliseconds / 1000.0);
        return DropContinuations(picked, bandRise, hopSeconds);
    }

    /// <summary>
    /// Drops an onset that only shows bands that were already rising at the hit just before it,
    /// such as a kick whose pitch is still sliding down into the kick band. A hit that brings a
    /// new band (a hat right after a kick) is kept.
    /// </summary>
    private List<int> DropContinuations(List<int> onsets, float[][] bandRise, double hopSeconds)
    {
        var window = (int)Math.Round(_options.ContinuationMilliseconds / 1000.0 / hopSeconds);
        var threshold = (float)(_options.OnsetDelta * _options.OnsetRiseCapDb);
        var kept = new List<int>(onsets.Count);
        var previousBands = Array.Empty<bool>();
        var previous = int.MinValue / 2;

        foreach (var onset in onsets)
        {
            var bands = bandRise.Select(r => r[onset] >= threshold).ToArray();
            var isContinuation = onset - previous <= window
                && bands.Select((rising, b) => !rising || previousBands[b]).All(x => x);

            if (!isContinuation)
            {
                kept.Add(onset);
                previous = onset;
                previousBands = bands;
            }
        }

        return kept;
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

    /// <summary>
    /// Band power just before the onset (lowest of a few frames, before the window reaches
    /// the hit) and just after it (highest until the next hit starts to show).
    /// </summary>
    private static (double Before, double After) BeforeAfter(double[] power, int onset, int nextOnset, int halfWindow)
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

        return (before, after);
    }

    /// <summary>
    /// Share of a band's power just after a hit that sits in narrow peaks standing well above
    /// the spectrum around them. Noise (snare, hats, crash) scores near 0 whatever its tilt;
    /// the separate partials of a ride score high.
    /// </summary>
    private static double TonalShare(Spectrogram spectrogram, (int First, int Last) band, int afterFrame)
    {
        const int Neighbourhood = 8;
        const float PeakFactor = 4f;
        var after = spectrogram.Magnitude[Math.Clamp(afterFrame, 0, spectrogram.FrameCount - 1)];
        var window = new float[2 * Neighbourhood + 1];
        double tonal = 0, total = 0;

        for (var k = band.First; k <= band.Last; k++)
        {

            var count = 0;
            for (var j = Math.Max(0, k - Neighbourhood); j <= Math.Min(after.Length - 1, k + Neighbourhood); j++)
            {
                window[count++] = after[j];
            }

            Array.Sort(window, 0, count);
            var p = (double)after[k] * after[k];
            total += p;
            if (after[k] >= PeakFactor * window[count / 2])
            {
                tonal += p;
            }
        }

        return total <= 0 ? 0 : tonal / total;
    }

    /// <summary>
    /// Strongest frequency in the range, shortly after the onset once a kick's pitch drop has
    /// settled, refined between bins.
    /// </summary>
    private static double PeakPitch(Spectrogram spectrogram, int frame, double lowHz, double highHz)
    {
        var (first, last) = spectrogram.BinRange(lowHz, highHz);
        var magnitude = spectrogram.Magnitude[Math.Clamp(frame, 0, spectrogram.FrameCount - 1)];
        var best = first;
        for (var k = first + 1; k <= last; k++)
        {
            if (magnitude[k] > magnitude[best])
            {
                best = k;
            }
        }

        var offset = 0.0;
        if (best > 0 && best < magnitude.Length - 1)
        {
            double a = MathF.Log(magnitude[best - 1] + 1e-9f), b = MathF.Log(magnitude[best] + 1e-9f), c = MathF.Log(magnitude[best + 1] + 1e-9f);
            var denominator = a - 2 * b + c;
            if (denominator < 0)
            {
                offset = Math.Clamp(0.5 * (a - c) / denominator, -0.5, 0.5);
            }
        }

        return (best + offset) * spectrogram.BinHz;
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
        if (elapsed < MinimumExtrapolationSeconds)
        {
            // Too little to judge the fall from: report what was seen rather than guess long.
            return (last - onset) * hopSeconds;
        }

        var dropDb = 20 * Math.Log10(peak / Math.Max(Excess(last), 1e-9f));
        if (elapsed <= 0 || dropDb < 1)
        {
            return MaximumDecaySeconds;
        }

        var peakOffset = (peakFrame - onset) * hopSeconds;
        return Math.Min(MaximumDecaySeconds, peakOffset + elapsed * DecayDropDb / dropDb);
    }
}
