namespace SupernovaLab.Core.Signals;

/// <summary>
/// Draws the individual events shown as dots (spec §2.4): the only randomness in the lab, seeded by the run number.
/// </summary>
public static class EventSampler
{
    // Sub-interval edges: the union of two grids over the window. One is evenly spaced in u = ln(1 + (t − t0)/τ),
    // τ = 1e-6 of the window, which is fine near t0 (a 2.5 ms neutrino burst in a 100 s window). The other is evenly
    // spaced in t, so no sub-interval anywhere is wider than 1/2048 of the window.
    private const int LogIntervals = 1024, LinearIntervals = 2048;
    private const int ProbesPerInterval = 16;
    private const double ScaleFraction = 1e-6;
    // The rate's maximum over a sub-interval is estimated from the probes, then raised by this margin.
    private const double BoundMargin = 1.25;
    // A sub-interval whose rate exceeds its bound is split into this many parts, at most this many times over.
    private const int RefineSplit = 16, MaxRefineDepth = 4;

    /// <summary>
    /// Event times in [t0, t1], ascending, from an inhomogeneous Poisson process with this rate (events/s), by thinning
    /// (Lewis & Shedler 1979). The same seed always gives the same times. A NaN or negative rate counts as zero.
    /// <para>Each sub-interval's bound comes from probing the rate. When a candidate finds the rate above that bound,
    /// the sub-interval's events are discarded and it is resampled in finer parts with fresh probes; at the finest
    /// level the bound is raised instead. Sub-intervals are finest near <paramref name="t0"/>, so put t0 at the
    /// sharpest feature (core bounce). A feature narrower than the probe spacing (~1/30,000 of the window) whose
    /// surroundings have zero rate cannot be found by any probe and is missed.</para>
    /// </summary>
    /// <param name="ratePerSecond">The event rate at time t, events/s.</param>
    /// <param name="t0">Start of the window, s. The sub-intervals are finest here.</param>
    /// <param name="t1">End of the window, s. An empty window (t1 ≤ t0) gives no events.</param>
    /// <param name="seed">The run number.</param>
    public static IReadOnlyList<double> SampleTimes(Func<double, double> ratePerSecond, double t0, double t1, int seed)
    {
        var times = new List<double>();
        if (!(t1 > t0)) return times;

        var rng = new Random(seed);
        double Rate(double t) => ratePerSecond(t) is var r && r > 0 && double.IsFinite(r) ? r : 0;

        double[] edges = Edges(t0, t1);
        for (int k = 1; k < edges.Length; k++) SampleInterval(Rate, edges[k - 1], edges[k], 0, rng, times);
        return times;
    }

    private static double[] Edges(double t0, double t1)
    {
        double tau = (t1 - t0) * ScaleFraction, uMax = Math.Log(1 + (t1 - t0) / tau);
        var edges = new List<double>(LogIntervals + LinearIntervals + 2);
        for (int k = 0; k <= LogIntervals; k++) edges.Add(t0 + tau * (Math.Exp(uMax * k / LogIntervals) - 1));
        for (int k = 0; k <= LinearIntervals; k++) edges.Add(t0 + (t1 - t0) * k / LinearIntervals);
        edges.Add(t1);
        return edges.Where(t => t >= t0 && t <= t1).Distinct().OrderBy(t => t).ToArray();
    }

    private static void SampleInterval(Func<double, double> rate, double a, double b, int depth, Random rng, List<double> times)
    {
        if (!(b > a)) return;
        double max = 0;
        for (int p = 0; p <= ProbesPerInterval; p++) max = Math.Max(max, rate(a + (b - a) * p / ProbesPerInterval));
        if (max <= 0) return;
        double bound = max * BoundMargin;

        // Homogeneous Poisson at the bound, each event kept with probability rate / bound.
        var local = new List<double>();
        double t = a;
        while (true)
        {
            t += -Math.Log(1 - rng.NextDouble()) / bound;
            if (t > b) break;
            double r = rate(t);
            if (r > bound)
            {
                if (depth < MaxRefineDepth)
                {
                    // The probes missed a peak: resample this sub-interval in finer parts.
                    for (int k = 0; k < RefineSplit; k++)
                        SampleInterval(rate, a + (b - a) * k / RefineSplit, a + (b - a) * (k + 1) / RefineSplit, depth + 1,
                            rng, times);
                    return;
                }
                bound = r * BoundMargin;
            }
            if (rng.NextDouble() * bound < r) local.Add(t);
        }
        times.AddRange(local);
    }
}
