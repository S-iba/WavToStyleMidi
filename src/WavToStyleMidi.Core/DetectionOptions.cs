namespace WavToStyleMidi.Core;

/// <summary>
/// Tuning values for hit detection and classification. Bound from the "Detection"
/// section of appsettings.json so they can be adjusted without recompiling.
/// </summary>
public sealed class DetectionOptions
{
    public const string SectionName = "Detection";

    /// <summary>Hop between analysis frames in milliseconds.</summary>
    public double HopMilliseconds { get; set; } = 5.0;

    /// <summary>Analysis window length in milliseconds (rounded up to a power of two in samples).</summary>
    public double WindowMilliseconds { get; set; } = 40.0;

    public double KickLowHz { get; set; } = 30;
    public double KickHighHz { get; set; } = 110;
    public double SnareLowHz { get; set; } = 1000;
    public double SnareHighHz { get; set; } = 5000;
    public double HatLowHz { get; set; } = 7000;
    public double HatHighHz { get; set; } = 16000;

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
    /// A band that never holds at least this share of a hit's total power gain is treated as
    /// empty, so bleed from other drums is not labelled as that drum.
    /// </summary>
    public double EmptyBandShare { get; set; } = 0.02;

    /// <summary>Band power gain needed to label a hit, 0-1 relative to the loop's strongest hit of that drum.</summary>
    public double KickThreshold { get; set; } = 0.25;
    public double SnareThreshold { get; set; } = 0.25;

    /// <summary>Hat-band gain needed for a hat, relative to a typical hat in the loop (upper quartile).</summary>
    public double HatThreshold { get; set; } = 0.3;

    /// <summary>
    /// On a snare hit, the hat band must hold this many times more power (relative to the snare
    /// band) than the loop's plainest snare before a hat is added on top.
    /// </summary>
    public double HatOverSnareFactor { get; set; } = 2.0;

    /// <summary>Hat-band decay (to -15 dB) at or above this is an open hat.</summary>
    public double OpenHatDecayMilliseconds { get; set; } = 120;

    /// <summary>Hat-band decay at or above this is a crash.</summary>
    public double CrashDecayMilliseconds { get; set; } = 450;

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
