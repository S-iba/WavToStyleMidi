using WavToStyleMidi.Core.Detection;

namespace WavToStyleMidi.Core.Tests;

public sealed class SoundGrouperTests
{
    private readonly SoundGrouper _grouper = new();

    [Fact]
    public void Similar_sounds_share_a_group_and_groups_number_in_order()
    {
        float[] low = [1f, 0.5f, 0f, 0f];
        float[] lowSofter = [1f, 0.45f, 0.02f, 0f];
        float[] high = [0f, 0f, 0.5f, 1f];

        var groups = _grouper.Group([high, low, lowSofter, high], 0.1);

        Assert.Equal([0, 1, 1, 0], groups);
    }

    [Fact]
    public void Tighter_limit_splits_more()
    {
        float[] a = [1f, 0.5f, 0f, 0f];
        float[] b = [1f, 0.3f, 0f, 0f];

        Assert.Single(_grouper.Group([a, b], 0.2).Distinct());
        Assert.Equal(2, _grouper.Group([a, b], 0.05).Distinct().Count());
    }

    [Fact]
    public void No_fingerprints_gives_no_groups() => Assert.Empty(_grouper.Group([], 0.1));
}
