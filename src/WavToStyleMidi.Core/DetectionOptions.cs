namespace WavToStyleMidi.Core;

/// <summary>
/// Tuning values for hit detection and classification. Bound from the "Detection"
/// section of appsettings.json so they can be adjusted without recompiling.
/// </summary>
public sealed class DetectionOptions
{
    public const string SectionName = "Detection";

    // ---- Analysis -------------------------------------------------------------------------

    /// <summary>Hop between analysis frames in milliseconds.</summary>
    public double HopMilliseconds { get; set; } = 5.0;

    /// <summary>Analysis window length in milliseconds (rounded up to a power of two in samples).</summary>
    public double WindowMilliseconds { get; set; } = 40.0;

    public double KickLowHz { get; set; } = 30;
    public double KickHighHz { get; set; } = 110;
    public double TomLowHz { get; set; } = 80;
    public double TomHighHz { get; set; } = 450;
    public double SnareLowHz { get; set; } = 1000;
    public double SnareHighHz { get; set; } = 5000;
    public double HatLowHz { get; set; } = 7000;
    public double HatHighHz { get; set; } = 16000;

    // ---- Finding hits ---------------------------------------------------------------------

    /// <summary>
    /// How far above the local average a band's power rise must be to count as a hit,
    /// as a fraction of <see cref="OnsetRiseCapDb"/>.
    /// </summary>
    public double OnsetDelta { get; set; } = 0.12;

    /// <summary>Largest band power rise (dB) that counts; bigger rises are treated as equal.</summary>
    public double OnsetRiseCapDb { get; set; } = 10;

    /// <summary>Levels this far (dB) below a band's loudest moment are treated as silence.</summary>
    public double OnsetFloorDb { get; set; } = 50;

    /// <summary>Minimum gap between two detected hits, in milliseconds.</summary>
    public double MinimumHitGapMilliseconds { get; set; } = 35;

    /// <summary>
    /// A second rise this soon after a hit, in only the bands that hit already rose in, is
    /// treated as the same hit still developing (e.g. a kick's pitch sliding down).
    /// </summary>
    public double ContinuationMilliseconds { get; set; } = 80;

    /// <summary>
    /// A band that never holds at least this share of a hit's total power gain is treated as
    /// empty, so bleed from other drums is not labelled as that drum.
    /// </summary>
    public double EmptyBandShare { get; set; } = 0.02;

    // ---- Kick and toms --------------------------------------------------------------------

    /// <summary>Gain needed for a kick, 0-1 against the loop's biggest kick-band gain.</summary>
    public double KickThreshold { get; set; } = 0.25;

    /// <summary>Low-end pitch at or above this is a tom, below it a kick.</summary>
    public double TomMinimumPitchHz { get; set; } = 80;

    /// <summary>Toms below this pitch are low (floor) toms.</summary>
    public double LowTomMaximumPitchHz { get; set; } = 115;

    /// <summary>Toms below this pitch (and above the low tom limit) are mid toms; higher ones are high toms.</summary>
    public double MidTomMaximumPitchHz { get; set; } = 175;

    /// <summary>How long after the hit the low-end pitch is read, so a kick's pitch drop has settled.</summary>
    public double PitchDelayMilliseconds { get; set; } = 90;

    /// <summary>A tom's band must jump by at least this much (dB) to count. Also used for a ride's attack.</summary>
    public double BandRiseDb { get; set; } = 3;

    /// <summary>A tom counts only within this many dB of the loudest low-mid hit in the loop. Also sets tom velocity range.</summary>
    public double BandLevelRangeDb { get; set; } = 24;

    /// <summary>
    /// Tom-band gain over 1-5 kHz gain needed for a tom. Toms are mostly pitched low end; a
    /// snare's body sits well below this because of its noise.
    /// </summary>
    public double TomMinimumLowMidToNoise { get; set; } = 100;

    // ---- Snare, clap, side stick ----------------------------------------------------------

    /// <summary>Gain needed for a snare or clap, 0-1 against the loop's biggest 1-5 kHz gain (rides excluded).</summary>
    public double SnareThreshold { get; set; } = 0.25;

    /// <summary>150-350 Hz gain over 1-5 kHz gain. Below this a snare-band hit has no body and is a clap.</summary>
    public double ClapMaximumBodyRatio { get; set; } = 0.05;

    /// <summary>400-2000 Hz gain needed for a side stick, 0-1 against the loop's biggest.</summary>
    public double SideStickThreshold { get; set; } = 0.3;

    /// <summary>Share of the hit's total gain that must sit in 400-2000 Hz for a side stick.</summary>
    public double SideStickMinimumShare { get; set; } = 0.3;

    /// <summary>A side stick's 300 Hz-5 kHz ring must be shorter than this.</summary>
    public double SideStickMaximumDecayMilliseconds { get; set; } = 60;

    // ---- Hats and cymbals -----------------------------------------------------------------

    /// <summary>Hat-band gain needed for a hat, against a typical (upper-quartile) hat in the loop.</summary>
    public double HatThreshold { get; set; } = 0.3;

    /// <summary>
    /// On a snare, clap or tom hit, the hat band must hold this many times more power (against
    /// 1-5 kHz) than the plainest hit of that drum in the loop before a hat is added on top.
    /// </summary>
    public double HatOverSnareFactor { get; set; } = 2.0;

    /// <summary>Hat-band ring (to -15 dB) at or above this is an open hat.</summary>
    public double OpenHatDecayMilliseconds { get; set; } = 120;

    /// <summary>Hat-band ring at or above this is a crash.</summary>
    public double CrashDecayMilliseconds { get; set; } = 450;

    /// <summary>Share of 2-9 kHz power in narrow peaks needed for a ride (noise scores near 0).</summary>
    public double RideMinimumTonalShare { get; set; } = 0.4;

    /// <summary>On a ride hit, a snare is only added when its 150-350 Hz body is at least this strong against 1-5 kHz.</summary>
    public double RideSnareMinimumBodyRatio { get; set; } = 1.0;

    // ---- Output ---------------------------------------------------------------------------

    public int MinimumVelocity { get; set; } = 45;

    /// <summary>
    /// A snare or a ringing crash fills the hat band, so hats played with them are hard to hear
    /// in the audio. When the rest of the loop has a steady 8th or 16th hat pattern, add the
    /// hidden hats too.
    /// </summary>
    public bool FillMaskedHats { get; set; } = true;

    /// <summary>How long after a crash its ring is treated as hiding hats.</summary>
    public double CrashMaskMilliseconds { get; set; } = 600;

    /// <summary>Share of the unhidden grid slots that must have a hat before the pattern is filled.</summary>
    public double HatPatternCoverage { get; set; } = 0.8;
}
