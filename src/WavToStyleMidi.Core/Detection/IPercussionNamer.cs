using WavToStyleMidi.Core.Models;

namespace WavToStyleMidi.Core.Detection;

/// <summary>Decides which sound groups are percussion and gives each its own GM percussion note.</summary>
public interface IPercussionNamer
{
    /// <summary>The percussion kind per group id. Groups left out keep the drums the rules found.</summary>
    IReadOnlyDictionary<int, DrumKind> Name(IReadOnlyList<SoundGroup> groups);
}
