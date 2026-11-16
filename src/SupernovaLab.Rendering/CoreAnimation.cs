using System.Globalization;
using SupernovaLab.Core.Compact;

namespace SupernovaLab.Rendering;

/// <summary>
/// Plan Task 9: how the first second moves on screen (spec §3.2), as pure functions the tests check for flash safety (spec §1,
/// criterion 6: nothing changes brightness periodically faster than 3 times a second).
/// <list type="bullet">
/// <item>The shock's sloshing (SASI) and churning are a schematic shape change that swings once every <see cref="SloshPeriodS"/> of
/// wall time (0.25 Hz), far slower than in the simulations.</item>
/// <item>The infalling matter falls from <see cref="InfallOuterFactor"/> shock radii to the shock with the free-fall profile
/// v ∝ r^(−1/2) of the research's speed (√(G M / r), compact §C1), slowed by a power of ten so one fall takes
/// <see cref="MinInfallCycleS"/> to 10 × that of wall time. A particle fades in and out once per fall, so no particle's
/// brightness changes faster than once in 2 s.</item>
/// </list>
/// </summary>
public static class CoreAnimation
{
    /// <summary>[Presentation, schematic] One swing of the shock's sloshing, wall seconds.</summary>
    public const double SloshPeriodS = 4;

    /// <summary>[Presentation] The shortest wall time one particle's fall takes (and so the slowest flicker it could make).</summary>
    public const double MinInfallCycleS = 2;

    /// <summary>[Schematic] Infalling particles start this many shock radii out.</summary>
    public const double InfallOuterFactor = 2.2;

    /// <summary>The real time (s) matter takes to fall from <see cref="InfallOuterFactor"/> R_sh to R_sh, with |v| = v_sh √(R_sh / r).</summary>
    public static double RealFallTimeS(CoreState s)
    {
        double rIn = s.ShockRadiusKm, v = s.InfallSpeedKmS;
        if (!(rIn > 0) || !(v > 0)) return double.NaN;
        double rOut = InfallOuterFactor * rIn;
        // dr/dt = −v √(R_sh / r)  ⇒  (2/3)(r_out^1.5 − r^1.5) = v √R_sh t.
        return 2.0 / 3 * (Math.Pow(rOut, 1.5) - Math.Pow(rIn, 1.5)) / (v * Math.Sqrt(rIn));
    }

    /// <summary>The slow-down shown: the smallest power of ten that makes one fall last at least <see cref="MinInfallCycleS"/>; NaN without infall.</summary>
    public static double Slowdown(CoreState s)
    {
        double real = RealFallTimeS(s);
        if (!(real > 0)) return double.NaN;
        return Math.Pow(10, Math.Max(0, Math.Ceiling(Math.Log10(MinInfallCycleS / real) - 1e-9)));
    }

    /// <summary>One particle's fall on screen, wall seconds (between <see cref="MinInfallCycleS"/> and 10 × that); NaN without infall.</summary>
    public static double InfallCycleS(CoreState s) => RealFallTimeS(s) * Slowdown(s);

    /// <summary>"Infall shown 1,000× slower"; null without infall.</summary>
    public static string? InfallLabel(CoreState s)
    {
        double k = Slowdown(s);
        return k > 0 ? $"Infall shown {k.ToString("N0", CultureInfo.InvariantCulture)}× slower" : null;
    }
}
