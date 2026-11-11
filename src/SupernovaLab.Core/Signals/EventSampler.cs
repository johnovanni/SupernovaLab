namespace SupernovaLab.Core.Signals;

/// <summary>
/// Draws the individual events shown as dots (spec §2.4): the only randomness in the lab, seeded by the run number.
/// </summary>
public static class EventSampler
{
    // The window is cut into sub-intervals evenly spaced in u = ln(1 + (t − t0)/τ), τ = 1e-6 of the window: fine near
    // t0, where signals change fastest (a 2.5 ms neutrino burst in a 100 s window), and coarse late.
    private const int Intervals = 1024;
    private const int ProbesPerInterval = 16;
    private const double ScaleFraction = 1e-6;
    // The rate's maximum over a sub-interval is estimated from the probes, then raised by this margin.
    private const double BoundMargin = 1.25;

    /// <summary>
    /// Event times in [t0, t1], ascending, from an inhomogeneous Poisson process with this rate (events/s), by thinning
    /// (Lewis & Shedler 1979). The same seed always gives the same times. A NaN or negative rate counts as zero.
    /// The bound in each sub-interval comes from sampling the rate, so a feature narrower than a sub-interval's probe
    /// spacing could be under-sampled; the spacing is ~10⁻⁸ of the window near t0.
    /// </summary>
    public static IReadOnlyList<double> SampleTimes(Func<double, double> ratePerSecond, double t0, double t1, int seed)
    {
        var times = new List<double>();
        if (!(t1 > t0)) return times;

        var rng = new Random(seed);
        double tau = (t1 - t0) * ScaleFraction;
        double uMax = Math.Log(1 + (t1 - t0) / tau);
        double Rate(double t) => ratePerSecond(t) is var r && r > 0 && double.IsFinite(r) ? r : 0;
        double At(double u) => t0 + tau * (Math.Exp(u) - 1);

        for (int k = 0; k < Intervals; k++)
        {
            double a = At(uMax * k / Intervals);
            double b = k == Intervals - 1 ? t1 : At(uMax * (k + 1) / Intervals);

            double max = 0;
            for (int p = 0; p <= ProbesPerInterval; p++) max = Math.Max(max, Rate(a + (b - a) * p / ProbesPerInterval));
            if (max <= 0) continue;
            double bound = max * BoundMargin;

            // Homogeneous Poisson at the bound, each event kept with probability rate / bound.
            double t = a;
            while (true)
            {
                t += -Math.Log(1 - rng.NextDouble()) / bound;
                if (t > b) break;
                if (rng.NextDouble() * bound < Rate(t)) times.Add(t);
            }
        }
        return times;
    }
}
