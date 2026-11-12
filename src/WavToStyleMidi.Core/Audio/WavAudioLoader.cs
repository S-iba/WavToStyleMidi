using NAudio.Wave;
using WavToStyleMidi.Core.Models;

namespace WavToStyleMidi.Core.Audio;

public sealed class WavAudioLoader : IAudioLoader
{
    private const int ReadBlockFrames = 8192;

    public async Task<AudioClip> LoadAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        using var stream = new MemoryStream(bytes, writable: false);
        return Decode(stream, cancellationToken);
    }

    internal static AudioClip Decode(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new WaveFileReader(stream);
        var provider = reader.ToSampleProvider();
        var channels = provider.WaveFormat.Channels;
        var totalFrames = (int)(reader.SampleCount);

        var mono = new float[totalFrames];
        var buffer = new float[ReadBlockFrames * channels];
        var frame = 0;
        int read;

        while ((read = provider.Read(buffer.AsSpan())) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var frames = read / channels;
            for (var i = 0; i < frames && frame < mono.Length; i++, frame++)
            {
                var sum = 0f;
                for (var c = 0; c < channels; c++)
                {
                    sum += buffer[i * channels + c];
                }

                mono[frame] = sum / channels;
            }
        }

        if (frame < mono.Length)
        {
            Array.Resize(ref mono, frame);
        }

        return new AudioClip(mono, provider.WaveFormat.SampleRate);
    }
}
