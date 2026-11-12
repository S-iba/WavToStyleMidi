using WavToStyleMidi.Core.Models;

namespace WavToStyleMidi.Core.Audio;

public interface IAudioLoader
{
    /// <summary>Loads a WAV file and mixes it down to mono.</summary>
    Task<AudioClip> LoadAsync(string path, CancellationToken cancellationToken);
}
