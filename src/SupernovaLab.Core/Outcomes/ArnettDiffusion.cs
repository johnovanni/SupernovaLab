using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Outcomes;

/// <summary>
/// Arnett's diffusion model (signals §4.2), shared by the outcome engine's magnetar peak (ruling R32) and the light-curve
/// model, so both draw the same light. Times in s, masses in M☉, energies in erg, opacity in cm²/g. Pure.
/// </summary>
public static class ArnettDiffusion
{
    /// <summary>Arnett's β = 13.8 (§4.2).</summary>
    public const double Beta = 13.8;

    /// <summary>Simpson intervals for the kernel.</summary>
    private const int KernelIntervals = 200;

    /// <summary>The kernel's weight exp((t'² − t²)/τ²) is cut below e^−40.</summary>
    private const double KernelCutoff = 40;

    /// <summary>§4.2: τ_m = (2κM / (β c v))^(1/2), v = √(10E / 3M), β = 13.8; s.</summary>
    public static double DiffusionTimeS(double opacity, double ejectaMsun, double energyErg)
    {
        double m = ejectaMsun * PhysicalConstants.MSun;
        double v = Math.Sqrt(10 * energyErg / (3 * m));
        return Math.Sqrt(2 * opacity * m / (Beta * PhysicalConstants.C * v));
    }

    /// <summary>
    /// §4.2 (Inserra et al. 2013, Eq. D1): the luminosity of any heating source P (erg/s) diffusing out of the ejecta,
    /// L(t) = ∫₀ᵗ P(t') (2t'/τ_m²) exp((t'² − t²)/τ_m²) dt', written so the exponent never overflows. Simpson's rule over
    /// t' where the weight exceeds e^−40. Linear in P.
    /// </summary>
    public static double LuminosityErgS(Func<double, double> heatingErgS, double diffusionTimeS, double timeS)
    {
        if (!(timeS > 0)) return 0;
        double tau2 = diffusionTimeS * diffusionTimeS, t2 = timeS * timeS;
        double lo = Math.Sqrt(Math.Max(0, t2 - KernelCutoff * tau2));
        double h = (timeS - lo) / KernelIntervals, sum = 0;
        for (int i = 0; i <= KernelIntervals; i++)
        {
            double tp = lo + i * h;
            double w = i == 0 || i == KernelIntervals ? 1 : i % 2 == 1 ? 4 : 2;
            sum += w * heatingErgS(tp) * 2 * tp / tau2 * Math.Exp((tp * tp - t2) / tau2);
        }
        return sum * h / 3;
    }

    /// <summary>
    /// The maximum of f over [lo, hi] (s): the best of 240 log-spaced samples, refined by golden-section search in log t
    /// between its neighbours. Returns (NaN, −∞) when f is never finite.
    /// </summary>
    public static (double TimeS, double Value) Maximize(Func<double, double> f, double lo, double hi)
    {
        const int n = 240;
        double a = Math.Log(lo), step = (Math.Log(hi) - a) / (n - 1);
        int best = 0;
        double bestValue = double.NegativeInfinity;
        for (int i = 0; i < n; i++)
        {
            double v = f(Math.Exp(a + i * step));
            if (v > bestValue) (best, bestValue) = (i, v);
        }
        if (!double.IsFinite(bestValue)) return (double.NaN, bestValue);
        double x0 = a + Math.Max(best - 1, 0) * step, x1 = a + Math.Min(best + 1, n - 1) * step;
        double g = (Math.Sqrt(5) - 1) / 2;
        double c = x1 - g * (x1 - x0), d = x0 + g * (x1 - x0), fc = f(Math.Exp(c)), fd = f(Math.Exp(d));
        for (int i = 0; i < 40; i++)
        {
            if (fc > fd) { x1 = d; d = c; fd = fc; c = x1 - g * (x1 - x0); fc = f(Math.Exp(c)); }
            else { x0 = c; c = d; fc = fd; d = x0 + g * (x1 - x0); fd = f(Math.Exp(d)); }
        }
        double tBest = Math.Exp(0.5 * (x0 + x1)), vBest = f(tBest);
        return vBest >= bestValue ? (tBest, vBest) : (Math.Exp(a + best * step), bestValue);
    }
}
