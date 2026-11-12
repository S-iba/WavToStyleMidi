namespace WavToStyleMidi.Core.Detection;

/// <summary>
/// Average-linkage agglomerative clustering on the RMS distance between fingerprints. The number
/// of groups is not fixed; merging stops once the closest two groups are further apart than the
/// limit. A loop holds at most a few hundred hits, so the simple O(n^3) form is fast enough.
/// </summary>
public sealed class SoundGrouper : ISoundGrouper
{
    public IReadOnlyList<int> Group(IReadOnlyList<float[]> fingerprints, double maximumDistance)
    {
        ArgumentNullException.ThrowIfNull(fingerprints);

        var count = fingerprints.Count;
        if (count == 0)
        {
            return [];
        }

        var distance = new double[count, count];
        for (var i = 0; i < count; i++)
        {
            for (var j = i + 1; j < count; j++)
            {
                distance[i, j] = distance[j, i] = Distance(fingerprints[i], fingerprints[j]);
            }
        }

        var clusters = Enumerable.Range(0, count).Select(i => new List<int> { i }).ToList();
        while (clusters.Count > 1)
        {
            var (bestA, bestB, best) = (-1, -1, double.MaxValue);
            for (var a = 0; a < clusters.Count; a++)
            {
                for (var b = a + 1; b < clusters.Count; b++)
                {
                    var d = AverageDistance(clusters[a], clusters[b], distance);
                    if (d < best)
                    {
                        (bestA, bestB, best) = (a, b, d);
                    }
                }
            }

            if (best > maximumDistance)
            {
                break;
            }

            clusters[bestA].AddRange(clusters[bestB]);
            clusters.RemoveAt(bestB);
        }

        var groups = new int[count];
        var ordered = clusters.OrderBy(c => c.Min()).ToList();
        for (var g = 0; g < ordered.Count; g++)
        {
            foreach (var i in ordered[g])
            {
                groups[i] = g;
            }
        }

        return groups;
    }

    private static double AverageDistance(List<int> a, List<int> b, double[,] distance)
    {
        var sum = 0.0;
        foreach (var i in a)
        {
            foreach (var j in b)
            {
                sum += distance[i, j];
            }
        }

        return sum / (a.Count * b.Count);
    }

    private static double Distance(float[] a, float[] b)
    {
        if (a.Length != b.Length)
        {
            throw new ArgumentException("Fingerprints must have the same length.");
        }

        if (a.Length == 0)
        {
            return 0;
        }

        var sum = 0.0;
        for (var k = 0; k < a.Length; k++)
        {
            var d = a[k] - b[k];
            sum += d * d;
        }

        return Math.Sqrt(sum / a.Length);
    }
}
