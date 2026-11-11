using SupernovaLab.Core.Observer;

namespace SupernovaLab.Core.Timeline;

/// <summary>
/// The shared log-time axis (spec §4): 1 ms to 1,000 years of observer time, sampled at 400 log-spaced points.
/// Models take source time, which is observer time ÷ (1 + z).
/// </summary>
public static class TimeAxis
{
    public const double MinS = 1e-3, MaxS = 3.156e10;
    public const int Samples = 400;

    private static readonly double LogMin = Math.Log(MinS), LogSpan = Math.Log(MaxS) - Math.Log(MinS);

    /// <summary>
    /// The panels' 400 observer times, log-spaced from <see cref="MinS"/> to <see cref="MaxS"/> (the ends exact). The same
    /// for every source; convert each with <see cref="ToSource"/>.
    /// </summary>
    public static double[] LogGrid()
    {
        var g = new double[Samples];
        for (int i = 0; i < Samples; i++) g[i] = FromFraction((double)i / (Samples - 1));
        g[0] = MinS;
        g[^1] = MaxS;
        return g;
    }

    /// <summary>Source time for an observer time: t_obs ÷ (1 + z).</summary>
    public static double ToSource(double observerS, ObserverSetup obs) => observerS / obs.TimeDilation;

    /// <summary>Position 0..1 on the log axis; times outside the axis (including t ≤ 0) are clamped to its ends.</summary>
    public static double ToFraction(double observerS)
    {
        if (!(observerS > MinS)) return 0;
        if (observerS >= MaxS) return 1;
        return (Math.Log(observerS) - LogMin) / LogSpan;
    }

    /// <summary>The observer time at a position 0..1 on the log axis (clamped to 0..1).</summary>
    public static double FromFraction(double f)
    {
        if (!(f > 0)) return MinS;
        if (f >= 1) return MaxS;
        return Math.Exp(LogMin + f * LogSpan);
    }
}
