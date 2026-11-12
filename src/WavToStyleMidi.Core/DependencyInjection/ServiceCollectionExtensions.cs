using Microsoft.Extensions.DependencyInjection;
using WavToStyleMidi.Core.Audio;
using WavToStyleMidi.Core.Conversion;
using WavToStyleMidi.Core.Detection;
using WavToStyleMidi.Core.Midi;
using WavToStyleMidi.Core.Quantization;
using WavToStyleMidi.Core.Tempo;

namespace WavToStyleMidi.Core.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the WAV-to-drum-MIDI services. Configure <see cref="DetectionOptions"/> and
    /// <see cref="DrumNoteMap"/> with the options pattern (e.g. bind from configuration).
    /// </summary>
    public static IServiceCollection AddWavToStyleMidi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<DetectionOptions>();
        services.AddOptions<DrumNoteMap>();

        services.AddSingleton<IAudioLoader, WavAudioLoader>();
        services.AddSingleton<ITempoResolver, TempoResolver>();
        services.AddSingleton<ISoundGrouper, SoundGrouper>();
        services.AddSingleton<IPercussionNamer, PercussionNamer>();
        services.AddSingleton<IHitDetector, SpectralHitDetector>();
        services.AddSingleton<IGrooveQuantizer, GrooveQuantizer>();
        services.AddSingleton<IHatPatternFiller, HatPatternFiller>();
        services.AddSingleton<IDrumMidiWriter, DrumMidiWriter>();
        services.AddSingleton<ILoopConverter, LoopConverter>();
        services.AddSingleton<IBatchConverter, BatchConverter>();

        return services;
    }
}
