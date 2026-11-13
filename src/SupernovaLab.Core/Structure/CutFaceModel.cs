namespace SupernovaLab.Core.Structure;

/// <summary>
/// What the debris cutaway's two cut faces show (ruling R-P2-21, which replaces plan ruling P7's particle slabs): the
/// element at each point of a face, worked out analytically from the particle set's own construction, so the faces are
/// continuous surfaces however few particles lie near the planes. Schematic: a face shows the unmixed layers as nested
/// spheres (the layer boundaries are where the mass coordinate q crosses each layer's cumulative mass, mapped through the
/// profile's v(q) and <see cref="ParticleKinematics"/>'s radius rule), plus the Rayleigh-Taylor fingers as the cones of
/// <see cref="MixingModel"/> cut by the plane: nickel from the centre out to the radius of NickelMax, hydrogen from the
/// radius of HydrogenMin outward. The 3D view's <c>cutface.frag</c> mirrors <see cref="LayerAt"/>: change one, change the other.
/// </summary>
/// <param name="Layers">The layers, centre outward, with the radius (cm) of each one's outer boundary; the last is <see cref="OuterRadiusCm"/>.</param>
/// <param name="NickelMaxRadiusCm">Radius of the nickel finger tips (NaN: no nickel fingers).</param>
/// <param name="HydrogenMinRadiusCm">Radius the hydrogen fingers reach inward to (NaN: none).</param>
/// <param name="Cones">The finger cones (empty when there are no fingers).</param>
public sealed record CutFaceModel(IReadOnlyList<(ElementLayer Layer, double OuterRadiusCm)> Layers, double NickelMaxRadiusCm,
    double HydrogenMinRadiusCm, FingerCones Cones)
{
    /// <summary>The outermost radius a face is drawn to, cm: the fastest particle's.</summary>
    public double OuterRadiusCm => Layers[^1].OuterRadiusCm;

    /// <summary>
    /// The faces for this structure <paramref name="sinceBreakoutS"/> after breakout, for a particle set whose fastest particle
    /// moves at <paramref name="maxCmS"/>; null for a structure with no layers or a failed supernova's thin shell (no cutaway faces).
    /// <paramref name="cones"/> are the set's finger cones (<see cref="MixingModel.Cones"/> with the sampler's seed).
    /// </summary>
    public static CutFaceModel? Build(EjectaStructure s, FingerCones cones, double sinceBreakoutS, double starRadiusCm, double maxCmS)
    {
        ArgumentNullException.ThrowIfNull(s);
        if (s.IsEnvelopeShell || s.Layers.Count == 0 || !(s.EjectaMassMsun > 0) || !(maxCmS > 0)) return null;

        double Radius(double v) => ParticleKinematics.RadiusCm(v, sinceBreakoutS, starRadiusCm, maxCmS, null);
        var layers = new List<(ElementLayer, double)>(s.Layers.Count);
        double cumulative = 0;
        for (int k = 0; k < s.Layers.Count; k++)
        {
            cumulative += s.Layers[k].MassMsun;
            double q = Math.Min(1, cumulative / s.EjectaMassMsun);
            layers.Add((s.Layers[k].Layer, Radius(EjectaStructureModel.VelocityAtMassFraction(s, EjectaStructureModel.TruncationFraction * q))));
        }
        // The outermost particle (mixing may push nickel past the profile's own v_max) bounds every face.
        layers[^1] = (layers[^1].Item1, Math.Max(layers[^1].Item2, Radius(maxCmS)));

        var m = s.Mixing;
        bool fingers = m.HasFingers && cones.Axes.Count > 0;
        double ni = fingers && m.NickelFingerFraction > 0 && double.IsFinite(m.NickelMaxCmS) ? Radius(m.NickelMaxCmS) : double.NaN;
        double h = fingers && double.IsFinite(m.HydrogenMinCmS) ? Radius(m.HydrogenMinCmS) : double.NaN;
        return new CutFaceModel(layers, ni, h, fingers ? cones : FingerCones.Empty);
    }

    /// <summary>
    /// The element at a point of a cut face, <paramref name="radiusCm"/> from the centre in the unit direction
    /// (<paramref name="dx"/>, <paramref name="dy"/>, <paramref name="dz"/>); null outside the debris. Nickel fingers win over
    /// hydrogen fingers, which win over the unmixed layer.
    /// </summary>
    public ElementLayer? LayerAt(double radiusCm, double dx, double dy, double dz)
    {
        if (!(radiusCm <= OuterRadiusCm)) return null;
        ElementLayer layer = Layers[^1].Layer;
        foreach (var (l, outer) in Layers)
            if (radiusCm <= outer) { layer = l; break; }

        if (Cones.Axes.Count > 0 && Cones.Contains(dx, dy, dz))
        {
            if (radiusCm <= NickelMaxRadiusCm) return ElementLayer.NickelIron;
            if (radiusCm >= HydrogenMinRadiusCm) return ElementLayer.Hydrogen;
        }
        return layer;
    }
}
