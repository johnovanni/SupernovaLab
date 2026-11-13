namespace SupernovaLab.Core.Structure;

/// <summary>
/// Where a debris particle is (plan ruling P4). Before any interaction
/// <code>r = R*·(v / v_max) + v·(t − t_bo)</code>
/// continuous with the star at breakout (the fastest particle at the stellar surface) and homologous later (r ∝ v). The
/// 3D view's vertex shader (<c>particle.vert</c>) mirrors this function exactly: change one, change the other.
/// </summary>
public static class ParticleKinematics
{
    /// <summary>
    /// The particle's radius, cm. <paramref name="sinceBreakoutS"/> is t − t_bo (a negative value is held at 0: no
    /// particle has left its starting position before breakout). <paramref name="shocks"/> is null until Task 8, which
    /// adds the confinement branch (particles faster than the reverse shock's Lagrangian speed placed between R_r and R_c,
    /// weighted in by ruling R-P2-4); with null this is the free rule.
    /// </summary>
    public static double RadiusCm(double vCmS, double sinceBreakoutS, double starRadiusCm, double maxCmS, ShockRadii? shocks)
    {
        double since = sinceBreakoutS > 0 ? sinceBreakoutS : 0;
        double ratio = maxCmS > 0 ? vCmS / maxCmS : 0;
        double free = starRadiusCm * ratio + vCmS * since;
        if (shocks is null) return free;
        // Task 8 (ruling P4's confinement, weighted by R-P2-4) replaces this stub; no test reaches it.
        return free;
    }
}
