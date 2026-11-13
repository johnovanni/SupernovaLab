namespace SupernovaLab.Core.Structure;

/// <summary>
/// Where a debris particle is (plan ruling P4). Before any interaction
/// <code>r = R*·(v / v_max) + v·(t − t_bo)</code>
/// continuous with the star at breakout (the fastest particle at the stellar surface) and homologous later (r ∝ v): with
/// s = v / v_max and K = R* + v_max (t − t_bo), the fastest particle's free radius, r_free = s·K.
/// <para>Once <see cref="ShockStructureModel"/> gives shocks, the particles the reverse shock has passed are placed between
/// R_r and R_c in order of speed (ruling P4): those whose free radius lies beyond R_r, i.e. faster than the reverse shock's
/// Lagrangian speed v_r = R_r / (R*/v_max + t − t_bo) (s_r = R_r / K; ruling P4 writes R_r / (t − t_bo), which drops the
/// R*/v_max term, at most R*/R_r ≈ 0.1% after the first weeks; keeping it makes the particle at v_r sit exactly on R_r, so the
/// order of radii is the order of speeds). A shocked particle goes to</para>
/// <code>r_conf = R_r + (R_c − R_r)·(s − s_r)/(1 − s_r)</code>
/// <para>and ruling R-P2-4 weights it in: r = r_free + w (r_conf − r_free), w = <see cref="ShockRadii.ConfinementWeight"/>.</para>
/// The 3D view's vertex shader (<c>particle.vert</c>) mirrors this function exactly: change one, change the other.
/// </summary>
public static class ParticleKinematics
{
    /// <summary>
    /// The particle's radius, cm. <paramref name="sinceBreakoutS"/> is t − t_bo (a negative value is held at 0: no particle has
    /// left its starting position before breakout). With <paramref name="shocks"/> null this is the free rule.
    /// </summary>
    public static double RadiusCm(double vCmS, double sinceBreakoutS, double starRadiusCm, double maxCmS, ShockRadii? shocks)
    {
        double since = sinceBreakoutS > 0 ? sinceBreakoutS : 0;
        double ratio = maxCmS > 0 ? vCmS / maxCmS : 0;
        double free = starRadiusCm * ratio + vCmS * since;
        if (!Confines(ratio, since, starRadiusCm, maxCmS, shocks, out double sr, out double w)) return free;
        double confined = shocks!.ReverseCm + (shocks.ContactCm - shocks.ReverseCm) * Math.Min(1, (ratio - sr) / (1 - sr));
        return free + w * (confined - free);
    }

    /// <summary>
    /// How much of the hot, shocked look a particle takes (the shader's tint): ruling R-P2-4's weight w for a particle the
    /// reverse shock has passed, 0 for any other.
    /// </summary>
    public static double ShockedWeight(double vCmS, double sinceBreakoutS, double starRadiusCm, double maxCmS, ShockRadii? shocks)
    {
        double since = sinceBreakoutS > 0 ? sinceBreakoutS : 0;
        double ratio = maxCmS > 0 ? vCmS / maxCmS : 0;
        return Confines(ratio, since, starRadiusCm, maxCmS, shocks, out _, out double w) ? w : 0;
    }

    /// <summary>
    /// The inverse of <see cref="RadiusCm"/> along a direction: the free-expansion radius s·K of the particle now drawn at
    /// <paramref name="radiusCm"/> (so the density profile's speed v = s·v_max is readable from a drawn radius; the cut faces use
    /// it). The confined map is linear in s on either side of s_r, so it inverts exactly. Identity without shocks.
    /// </summary>
    public static double FreeRadiusCm(double radiusCm, double sinceBreakoutS, double starRadiusCm, double maxCmS, ShockRadii? shocks)
    {
        double since = sinceBreakoutS > 0 ? sinceBreakoutS : 0;
        double k = starRadiusCm + maxCmS * since;
        if (shocks is null || !(k > 0) || !(maxCmS > 0)) return radiusCm;
        double w = Math.Clamp(shocks.ConfinementWeight, 0, 1), sr = shocks.ReverseCm / k;
        if (!(w > 0) || !(sr < 1) || !(radiusCm > shocks.ReverseCm)) return radiusCm;
        // r(s) = R_r + (s − s_r)·slope for s > s_r, slope = (1 − w) K + w (R_c − R_r) / (1 − s_r).
        double slope = (1 - w) * k + w * (shocks.ContactCm - shocks.ReverseCm) / (1 - sr);
        if (!(slope > 0)) return shocks.ReverseCm;
        return (sr + (radiusCm - shocks.ReverseCm) / slope) * k;
    }

    /// <summary>Whether this speed ratio is past the reverse shock (s &gt; s_r = R_r / K, with s_r &lt; 1 and w &gt; 0).</summary>
    private static bool Confines(double ratio, double since, double starRadiusCm, double maxCmS, ShockRadii? shocks,
        out double sr, out double w)
    {
        sr = double.NaN;
        w = 0;
        if (shocks is null) return false;
        double k = starRadiusCm + maxCmS * since;
        if (!(k > 0)) return false;
        w = Math.Clamp(shocks.ConfinementWeight, 0, 1);
        sr = shocks.ReverseCm / k;
        return w > 0 && sr < 1 && ratio > sr;
    }
}
