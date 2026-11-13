namespace SupernovaLab.Core.Structure;

/// <summary>
/// Where a debris particle is (plan ruling P4). Before any interaction
/// <code>r = R*·(v / v_max) + v·(t − t_bo)</code>
/// continuous with the star at breakout (the fastest particle at the stellar surface) and homologous later (r ∝ v): with
/// s = v / v_max and K = R* + v_max (t − t_bo), the fastest particle's free radius, r_free = s·K.
/// <para>Once <see cref="ShockStructureModel"/> gives shocks, the particles the reverse shock has passed are placed between
/// R_r and R_c in order of speed (ruling P4). The shocked ones are those faster than the reverse shock's Lagrangian speed:
/// ruling R-P2-24's running minimum v_m = min over t' of R_r(t') / (t' − t_bo) (<see cref="ShockRadii.ReverseLagrangianCmS"/>),
/// so s_m = v_m / v_max. (For radii built by hand, with no running minimum, s_m = R_r / K: the particles whose free radius lies
/// beyond R_r.) A shocked particle goes to</para>
/// <code>r_conf = R_r + (R_c − R_r)·(s − s_m)/(1 − s_m)</code>
/// <para>and ruling R-P2-4 weights it in: r = r_free + w (r_conf − r_free), w = <see cref="ShockRadii.ConfinementWeight"/>.</para>
/// The 3D view's vertex shader (<c>particle.vert</c>) mirrors this function exactly, given s_m from
/// <see cref="ShockedSpeedRatio"/>: change one, change the other.
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
        double sm = ShockedSpeedRatio(shocks, since, starRadiusCm, maxCmS);
        if (shocks is null || !(ratio > sm)) return free;
        double w = Math.Clamp(shocks.ConfinementWeight, 0, 1);
        double confined = shocks.ReverseCm + (shocks.ContactCm - shocks.ReverseCm) * Math.Min(1, (ratio - sm) / (1 - sm));
        return free + w * (confined - free);
    }

    /// <summary>
    /// s_m: the speed ratio v / v_max above which particles are shocked and confined; +∞ when none are (no shocks, w = 0,
    /// or s_m ≥ 1). The shaders take it as a uniform.
    /// </summary>
    public static double ShockedSpeedRatio(ShockRadii? shocks, double sinceBreakoutS, double starRadiusCm, double maxCmS)
    {
        if (shocks is null || !(maxCmS > 0) || !(shocks.ConfinementWeight > 0)) return double.PositiveInfinity;
        double since = sinceBreakoutS > 0 ? sinceBreakoutS : 0;
        double sm;
        if (double.IsNaN(shocks.ReverseLagrangianCmS))
        {
            double k = starRadiusCm + maxCmS * since;
            if (!(k > 0)) return double.PositiveInfinity;
            sm = shocks.ReverseCm / k;
        }
        else sm = shocks.ReverseLagrangianCmS / maxCmS;
        return sm >= 0 && sm < 1 ? sm : double.PositiveInfinity;
    }

    /// <summary>
    /// How much of the hot, shocked look a particle takes (the shader's tint): ruling R-P2-4's weight w for a particle the
    /// reverse shock has passed, 0 for any other.
    /// </summary>
    public static double ShockedWeight(double vCmS, double sinceBreakoutS, double starRadiusCm, double maxCmS, ShockRadii? shocks)
    {
        double ratio = maxCmS > 0 ? vCmS / maxCmS : 0;
        return shocks is not null && ratio > ShockedSpeedRatio(shocks, sinceBreakoutS, starRadiusCm, maxCmS)
            ? Math.Clamp(shocks.ConfinementWeight, 0, 1) : 0;
    }

    /// <summary>
    /// The inverse of <see cref="RadiusCm"/> along a direction: the free-expansion radius s·K of the particle now drawn at
    /// <paramref name="radiusCm"/> (so the density profile's speed v = s·v_max is readable from a drawn radius; the cut faces use
    /// it). Above s_m the map is linear in s, r = r(s_m⁺) + (s − s_m)·slope, so it inverts exactly; a radius between the last
    /// unshocked particle (s_m K) and the first shocked one is given s_m. Identity without shocks.
    /// </summary>
    public static double FreeRadiusCm(double radiusCm, double sinceBreakoutS, double starRadiusCm, double maxCmS, ShockRadii? shocks)
    {
        double since = sinceBreakoutS > 0 ? sinceBreakoutS : 0;
        double k = starRadiusCm + maxCmS * since;
        double sm = ShockedSpeedRatio(shocks, since, starRadiusCm, maxCmS);
        if (shocks is null || !(k > 0) || double.IsInfinity(sm)) return radiusCm;
        double w = Math.Clamp(shocks.ConfinementWeight, 0, 1);
        double first = (1 - w) * sm * k + w * shocks.ReverseCm;   // r(s_m⁺)
        double slope = (1 - w) * k + w * (shocks.ContactCm - shocks.ReverseCm) / (1 - sm);
        if (radiusCm >= first && slope > 0) return (sm + (radiusCm - first) / slope) * k;
        return Math.Min(radiusCm / k, sm) * k;
    }
}
