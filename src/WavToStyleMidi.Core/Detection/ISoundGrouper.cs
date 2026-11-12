namespace WavToStyleMidi.Core.Detection;

/// <summary>Puts hits that sound alike into the same group.</summary>
public interface ISoundGrouper
{
    /// <summary>
    /// Returns a group number per fingerprint, numbered from 0 in order of first appearance.
    /// Two hits share a group when their fingerprints are on average within <paramref name="maximumDistance"/>.
    /// </summary>
    IReadOnlyList<int> Group(IReadOnlyList<float[]> fingerprints, double maximumDistance);
}
