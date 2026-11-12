namespace WavToStyleMidi.Core.Detection;

/// <summary>Peak picking on an onset-strength curve.</summary>
internal static class OnsetPicker
{
    private const double PeakWindowSeconds = 0.015;
    private const double MeanBeforeSeconds = 0.05;
    private const double MeanAfterSeconds = 0.015;

    /// <param name="curve">Onset strength per frame. Normalised internally.</param>
    /// <returns>Frame indices of the detected onsets, in order.</returns>
    public static List<int> Pick(float[] curve, double hopSeconds, double delta, double minimumGapSeconds)
    {
        var onsets = new List<int>();
        var max = curve.Length == 0 ? 0f : curve.Max();
        if (max <= 0)
        {
            return onsets;
        }

        var peakRadius = Math.Max(1, (int)Math.Round(PeakWindowSeconds / hopSeconds));
        var before = Math.Max(1, (int)Math.Round(MeanBeforeSeconds / hopSeconds));
        var after = Math.Max(1, (int)Math.Round(MeanAfterSeconds / hopSeconds));
        var minimumGap = Math.Max(1, (int)Math.Round(minimumGapSeconds / hopSeconds));
        var last = int.MinValue / 2;

        for (var f = 0; f < curve.Length; f++)
        {
            var value = curve[f] / max;
            if (value < delta || f - last < minimumGap)
            {
                continue;
            }

            if (!IsLocalMaximum(curve, f, peakRadius))
            {
                continue;
            }

            if (value < LocalMean(curve, f, before, after) / max + delta)
            {
                continue;
            }

            onsets.Add(f);
            last = f;
        }

        return onsets;
    }

    private static bool IsLocalMaximum(float[] curve, int frame, int radius)
    {
        var start = Math.Max(0, frame - radius);
        var end = Math.Min(curve.Length - 1, frame + radius);
        for (var i = start; i <= end; i++)
        {
            // Ties resolve to the earliest frame.
            if (curve[i] > curve[frame] || (curve[i] == curve[frame] && i < frame))
            {
                return false;
            }
        }

        return true;
    }

    private static float LocalMean(float[] curve, int frame, int before, int after)
    {
        var start = Math.Max(0, frame - before);
        var end = Math.Min(curve.Length - 1, frame + after);
        var sum = 0f;
        for (var i = start; i <= end; i++)
        {
            sum += curve[i];
        }

        return sum / (end - start + 1);
    }
}
