using WavToStyleMidi.Core.Models;

namespace WavToStyleMidi.Core.Detection;

/// <summary>Finds and labels drum hits in a mono drum loop.</summary>
public interface IHitDetector
{
    IReadOnlyList<DrumHit> Detect(AudioClip clip, CancellationToken cancellationToken);
}
