namespace SupernovaLab.Core.Structure;

/// <summary>
/// What the debris cutaway's two cut faces show (ruling R-P2-21, which replaces plan ruling P7's particle slabs): the
/// element at each point of a face, worked out analytically from the particle set's own construction, so the faces are
/// continuous surfaces however few particles lie near the planes. Schematic: a face shows the layers as nested shells (the layer
/// boundaries are where the mass coordinate q crosses each layer's cumulative mass, mapped through the profile's v(q) and
/// <see cref="ParticleKinematics"/>'s radius rule), with Rayleigh-Taylor mixing drawn at two scales (ruling R-P2-35, see
/// <see cref="At"/>): small fingers along every layer boundary, pushing both ways, and big nickel plumes from the finger cones
/// reaching out to NickelMax with hydrogen downflows reaching in to HydrogenMin. The physics (reach, cones, fractions, particles)
/// is <see cref="MixingModel"/>'s; the drawn shapes are schematic, and the small fingers' size is not from the lab's model.
/// Once there are shocks the radii are the confined ones (<see cref="ParticleKinematics.RadiusCm"/> with <see cref="Shocks"/>),
/// so the faces stop at the contact surface as the particles do. The 3D view's <c>cutface.frag</c> mirrors
/// <see cref="At"/> and <see cref="Opacity"/> operation for operation (single precision, <see cref="FaceNoise"/>): change one, change the other.
/// </summary>
/// <param name="Layers">The layers, centre outward, with the radius (cm) of each one's outer boundary; the last is <see cref="OuterRadiusCm"/>.</param>
/// <param name="NickelMaxRadiusCm">Radius of the nickel plume tips (NaN: no plumes).</param>
/// <param name="HydrogenMinRadiusCm">Radius the hydrogen downflows reach inward to (NaN: none).</param>
/// <param name="Cones">The finger cones (empty when there is no mixing).</param>
/// <param name="TransitionRadiusCm">Free-expansion radius of the profile's break speed v_t (ruling R-P2-23; compared with
/// <see cref="FreeRadiusCm"/>).</param>
/// <param name="OuterIndex">The profile's outer slope n: the face thins as (v / v_t)^-n beyond v_t.</param>
public sealed record CutFaceModel(IReadOnlyList<(ElementLayer Layer, double OuterRadiusCm)> Layers, double NickelMaxRadiusCm,
    double HydrogenMinRadiusCm, FingerCones Cones, double TransitionRadiusCm, double OuterIndex)
{
    // ---- Presentation constants [Task 16 / R-P2-35, schematic; cutface.frag declares the per-pixel ones with the same values] ----

    // Small scale: fingers along every layer boundary (only when the outcome mixes at all).

    /// <summary>A boundary finger's nominal length as a fraction of its boundary's radius, and its stalk's length-to-width ratio
    /// before the finger's own width factor (0.8 to 1.05: aspect 1.9 to 2.5). Each finger's length is 0.5 to 1 of nominal; a finger
    /// never reaches past FingerShare of its boundary's gap cap, and the two directions differ by at most FingerBalance.</summary>
    public const float FingerLength = 0.15f, FingerAspect = 2f, FingerBalance = 2.5f;
    /// <summary>A boundary finger's head: half-width in stalk half-widths, height in head half-widths.</summary>
    public const float FingerHead = 1.35f, FingerHeadHeight = 0.8f;
    /// <summary>A boundary finger's stalk narrows by this fraction from base to head; it leans by up to FingerLean of its height
    /// sideways; FingerGap of the cells have no finger.</summary>
    public const float FingerNarrow = 0.15f, FingerLean = 0.15f, FingerGap = 0.2f;
    /// <summary>A cell (one outward and one inward finger) is FingerPeriod times the boundary's longer nominal finger long, as an arc
    /// (a wavelength of about 0.24 of the radius); a finger's centre is jittered by up to FingerJitter of its cell.</summary>
    public const float FingerPeriod = 1.6f, FingerJitter = 0.08f;
    /// <summary>Shares of a boundary's gap cap (<see cref="BoundaryGapShare"/>) given to the slow undulation and to the fingers.</summary>
    public const double UndulationShare = 0.2, FingerShare = 0.7;
    /// <summary>The slow undulation: noise frequency per radian of face angle (3 octaves, the sum stretched by BoundaryGain and
    /// clamped to ±1); relative amplitude outside and inside the cones; the face angle over which the inside amplitude fades.</summary>
    public const float BoundaryFreq = 3f, BoundaryGain = 1.6f, BoundaryOut = 0.04f, BoundaryIn = 0.08f, BoundaryFalloff = 0.3f;

    // Large scale: nickel plumes from the physics cones, drawn within a band of Band chord half-widths either side of the chord's
    // centre (R-P2-35 option a; the faces only), with downflows of hydrogen between and beside them.

    /// <summary>The band, in chord half-widths: nickel and tongues stay within Band w of the chord's centre.</summary>
    public const float Band = 2f;
    /// <summary>Two plumes sit at ±PlumeSpread w; one sits on the chord's centre.</summary>
    public const float PlumeSpread = 0.9f;
    /// <summary>A plume's stalk half-width (arc) as a fraction of its length (root to tip), divided by sqrt(count); each plume's own varies by
    /// ±15%. The stalk narrows by StalkNarrow from root to head.</summary>
    public const float StalkArc = 0.11f, StalkNarrow = 0.15f;
    /// <summary>The base: where it leaves the nickel layer the stalk is StalkRoot of its full width (a fan that still covers well over
    /// a quarter of the chord there), widening to the full stalk over the first BaseLength of the plume's length, so the plumes do not
    /// fill the narrow inner chord with one slice.</summary>
    public const float StalkRoot = 0.3f, BaseLength = 0.45f;
    /// <summary>A plume's mushroom head: half-width in stalk half-widths; dome height in head half-widths; its rolled-back underside
    /// hangs below the widest line by height (CapDroop0 + CapDroop x^2) sqrt(1 − x^2), x = arc / head half-width, deepest towards
    /// the rim (the overhang). Boundary fingers' heads use the same shape.</summary>
    public const float CapWidth = 2f, CapHeight = 0.6f, CapDroop0 = 0.35f, CapDroop = 0.9f;
    /// <summary>A plume centreline's slow meander: angular amplitude MeanderAmp w s (none at the base), MeanderFreq cycles per length.</summary>
    public const float MeanderAmp = 0.3f, MeanderFreq = 1.3f;
    /// <summary>Sheaths dragged out with a plume (and a helium rim on a tongue): thickness as a fraction of the local radius.</summary>
    public const float RimOxygen = 0.016f, RimHelium = 0.013f;
    /// <summary>Tongues: one on each flank of a cluster at ±TongueFlank w and one between two plumes; root half-width (arc) as a
    /// fraction of the root's radius, tapering to 0.15 of that at the tip; meander in chord half-widths.</summary>
    public const float TongueFlank = 1.55f, TongueArc = 0.09f, TongueMeander = 0.08f;
    /// <summary>Ragged edges: a width is multiplied by 1 + EdgeLowAmp fbm2(EdgeLowFreq) + EdgeHighAmp value2(EdgeHighFreq)
    /// (frequencies per outer radius): large, slow lumps plus a little fine roughness.</summary>
    public const float EdgeLowFreq = 9f, EdgeLowAmp = 0.3f, EdgeHighFreq = 40f, EdgeHighAmp = 0.1f;
    /// <summary>Clumps: a plume's base is solid; its mid-stalk is nickel only where a 2D fbm (ClumpFreq per outer radius, 2 octaves)
    /// exceeds ClumpLevel; it turns solid again between ClumpSolidFrom and ClumpSolidTo, below the head.</summary>
    public const float ClumpFreq = 20f, ClumpLevel = -0.2f, ClumpFrom = 0.05f, ClumpTo = 0.2f, ClumpSolidFrom = 0.55f, ClumpSolidTo = 0.9f;
    /// <summary>A boundary moves at most this fraction of the gap to its neighbouring boundaries (undulation and fingers together
    /// use 0.9 of it), so the layers keep their order.</summary>
    public const double BoundaryGapShare = 0.45;
    /// <summary>The plumes start inside the nickel layer (this fraction of its radius) and the tongues inside the hydrogen layer
    /// (this multiple of its inner boundary's farthest reach), so both join their own layer.</summary>
    public const double FingerRootFraction = 0.9, TongueRootFraction = 1.03;
    /// <summary>Plumes per cone on a face.</summary>
    public const int MinPlumes = 1, MaxPlumes = 2;
    /// <summary>Fixed seeds: the cone's (plus its index), the edge noise's (plus the plane), the boundaries' (plus the boundary
    /// index), the clumps', the boundary fingers' (plus 7919 x boundary and the cell).</summary>
    public const uint ConeSeed = 0x51ed0000u, EdgeSeed = 17u, BoundarySeed = 101u, ClumpSeed = 29u, BoundaryFingerSeed = 0x7a3c0000u;

    /// <summary>The outermost radius a face is drawn to, cm: the fastest particle's.</summary>
    public double OuterRadiusCm => Layers[^1].OuterRadiusCm;

    /// <summary>The shocks the radii are confined by (null: free expansion).</summary>
    public ShockRadii? Shocks { get; init; }

    /// <summary>The wedge's angle about the scene's y axis (ruling R-P2-22): where the two faces lie.</summary>
    public double WedgeAngleRad { get; init; }

    /// <summary>t − t_bo, s, R* and v_max of the particle rule the radii come from (for <see cref="FreeRadiusCm"/>).</summary>
    public double SinceBreakoutS { get; init; }
    public double StarRadiusCm { get; init; }
    public double MaxCmS { get; init; }

    /// <summary>The free-expansion radius of the debris drawn at this radius (<see cref="ParticleKinematics.FreeRadiusCm"/>).</summary>
    public double FreeRadiusCm(double radiusCm) => ParticleKinematics.FreeRadiusCm(radiusCm, SinceBreakoutS, StarRadiusCm, MaxCmS, Shocks);

    /// <summary>
    /// The faces for this structure <paramref name="sinceBreakoutS"/> after breakout, for a particle set whose fastest particle
    /// moves at <paramref name="maxCmS"/>; null for a structure with no layers or a failed supernova's thin shell (no cutaway faces).
    /// <paramref name="cones"/> are the set's finger cones (<see cref="MixingModel.Cones"/> with the sampler's seed);
    /// <paramref name="shocks"/> confine the radii as they confine the particles (Task 8); <paramref name="wedgeAngleRad"/> places the faces.
    /// </summary>
    public static CutFaceModel? Build(EjectaStructure s, FingerCones cones, double sinceBreakoutS, double starRadiusCm, double maxCmS,
        ShockRadii? shocks = null, double wedgeAngleRad = 0)
    {
        ArgumentNullException.ThrowIfNull(s);
        if (s.IsEnvelopeShell || s.Layers.Count == 0 || !(s.EjectaMassMsun > 0) || !(maxCmS > 0)) return null;

        double Radius(double v) => ParticleKinematics.RadiusCm(v, sinceBreakoutS, starRadiusCm, maxCmS, shocks);
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
        double transition = ParticleKinematics.RadiusCm(s.TransitionCmS, sinceBreakoutS, starRadiusCm, maxCmS, null);
        return new CutFaceModel(layers, ni, h, fingers ? cones : FingerCones.Empty, transition, s.N)
        {
            Shocks = shocks, SinceBreakoutS = sinceBreakoutS, StarRadiusCm = starRadiusCm, MaxCmS = maxCmS, WedgeAngleRad = wedgeAngleRad,
        };
    }

    /// <summary>Outer boundary of the unmixed nickel layer, cm (0 when there is none): the fingers rise from here.</summary>
    public double NickelInnerRadiusCm
        => Layers.Where(l => l.Layer == ElementLayer.NickelIron).Select(l => l.OuterRadiusCm).DefaultIfEmpty(0).Max();

    /// <summary>Inner boundary of the unmixed hydrogen layer, cm (NaN when there is no hydrogen layer outside another): the tongues hang from here.</summary>
    public double HydrogenInnerRadiusCm
    {
        get
        {
            for (int k = 1; k < Layers.Count; k++)
                if (Layers[k].Layer == ElementLayer.Hydrogen) return Layers[k - 1].OuterRadiusCm;
            return double.NaN;
        }
    }


    // ---- The shading, in face coordinates normalised to the outer radius (single precision, as the shader) ----

    /// <summary>Each layer's outer radius over <see cref="OuterRadiusCm"/> (the last is 1), as the shader receives it.</summary>
    public IReadOnlyList<float> NormalisedOuter => Shading.Outer;

    /// <summary>The interior boundaries (k = 0 .. layers − 2: the outer boundary of layer k) as the shader receives them.</summary>
    public IReadOnlyList<BoundaryFingers> Boundaries => Shading.Boundaries;

    /// <summary>Where the plumes start and the tongues hang from, over <see cref="OuterRadiusCm"/> (-1: none).</summary>
    public float FingerRootRadius => Shading.FingerRoot;
    public float TongueRootRadius => Shading.TongueRoot;
    /// <summary>Tongues are drawn only below this radius (the hydrogen layer's inner boundary at its farthest undulation), so they
    /// never cut through that boundary's outward (helium) fingers.</summary>
    public float TongueCutRadius => Shading.TongueCut;

    /// <summary>The plume clusters on face <paramref name="plane"/> (0: w.x = 0, 1: w.z = 0): one per cone that crosses the face's
    /// plane substantially (<see cref="WedgeOrientation.ConeOnFace"/>) with some of its band on the drawn half.</summary>
    public IReadOnlyList<FingerCluster> ClustersOn(int plane) => Shading.Clusters[plane];

    /// <summary>
    /// The face point at normalised face coordinates (<paramref name="a"/> along y, <paramref name="b"/> &gt;= 0 along the face's
    /// outward axis, both over <see cref="OuterRadiusCm"/>) of face <paramref name="plane"/>; null off the face or beyond the debris.
    /// <list type="bullet">
    /// <item>Layers: the point is inside boundary k when it is below the boundary's slowly undulating base and not in one of its
    /// inward fingers, or in one of its outward fingers (<see cref="InsideBoundary"/>); its layer is the first such k. Fingers alternate
    /// around the face, the inner layer's outward (with a mushroom head) and the outer layer's inward; neighbouring boundaries never
    /// meet (undulation and fingers together use 0.9 of <see cref="BoundaryGapShare"/> of each gap).</item>
    /// <item>Nickel plumes (1 or 2 per cluster, nickel within <see cref="Band"/> w of the chord's centre): from inside the nickel layer a
    /// solid broad base narrowing to a meandering, lumpy stalk (clumpy at mid-length), ending in a mushroom head about twice its
    /// width with a rolled-back underside; the top of the head is the tip (at most NickelMax). Each wears an oxygen/silicon then a
    /// helium sheath.</item>
    /// <item>Hydrogen tongues on both flanks of a cluster and between its plumes: from the tongue root, inside the hydrogen layer,
    /// down to the tip (at least HydrogenMin), broad at the root and tapering, with a helium rim.</item>
    /// </list>
    /// Precedence: a plume's nickel, a tongue's hydrogen, the plumes' sheaths, a tongue's helium rim, the layer. Nothing is mixed
    /// inside the nickel layer, and a plume, sheath or tongue counts only where it intrudes into another layer (a tongue inside the
    /// hydrogen layer is that layer, with its fade).
    /// </summary>
    public CutFacePoint? At(int plane, float a, float b)
    {
        if (!(b >= 0f)) return null;
        float r = MathF.Sqrt(a * a + b * b);
        if (!(r <= 1f)) return null;
        var sh = Shading;
        float psi = MathF.Atan2(a, b);
        var clusters = sh.Clusters[plane];

        float weight = 0f;
        foreach (var c in clusters)
            weight = MathF.Max(weight, 1f - SmoothStep(c.HalfWidthRad, c.HalfWidthRad + BoundaryFalloff, MathF.Abs(psi - c.CentreRad)));
        float amp = BoundaryOut + (BoundaryIn - BoundaryOut) * weight;
        float rag = 1f + EdgeLowAmp * FaceNoise.Fbm2(EdgeSeed + (uint)plane, a * EdgeLowFreq, b * EdgeLowFreq, 2)
                       + EdgeHighAmp * FaceNoise.Value2(EdgeSeed + 8u + (uint)plane, a * EdgeHighFreq, b * EdgeHighFreq);
        int n = sh.Outer.Length, layer = n - 1;
        for (int k = 0; k < n - 1; k++)
            if (InsideBoundary(k, sh.Boundaries[k], r, psi, amp, rag)) { layer = k; break; }
        var unmixed = Layers[layer].Layer;

        if (clusters.Count > 0 && unmixed != ElementLayer.NickelIron)
        {
            float rimO = sh.OxygenLayer >= 0 ? RimOxygen : 0f, rimHe = sh.HeliumLayer >= 0 ? RimHelium : 0f;
            float clump = FaceNoise.Fbm2(ClumpSeed + (uint)plane, a * ClumpFreq, b * ClumpFreq, 2);
            int level = 0;   // 1 nickel, 2 oxygen sheath, 3 helium sheath
            foreach (var c in clusters)
            {
                float off = MathF.Abs(psi - c.CentreRad), band = Band * c.HalfWidthRad;
                if (off > band + rimO + rimHe) continue;
                foreach (var f in c.Fingers)
                {
                    int l = InPlume(r, psi, f, c.HalfWidthRad, sh.FingerRoot, rag, clump, rimO, rimHe);
                    if (l == 1 && off > band) l = rimO > 0f ? 2 : 3;   // nickel stays within the band
                    if (l > 0 && (level == 0 || l < level)) level = l;
                }
            }
            if (level == 1) return Mixed(ElementLayer.NickelIron, CutFaceMaterial.NickelFinger, unmixed);
            int tongue = 0;   // 1 hydrogen, 2 its helium rim
            if (unmixed != ElementLayer.Hydrogen && r <= sh.TongueCut)   // not through the hydrogen boundary's outward fingers
                foreach (var c in clusters)
                {
                    if (MathF.Abs(psi - c.CentreRad) > Band * c.HalfWidthRad) continue;
                    foreach (var t in c.Tongues)
                    {
                        int l = InTongue(r, psi, t, c.HalfWidthRad, sh.TongueRoot, rag, rimHe);
                        if (l > 0 && (tongue == 0 || l < tongue)) tongue = l;
                    }
                }
            // Nickel, then a tongue's hydrogen, then the plumes' sheaths, then a tongue's helium rim.
            if (tongue == 1) return Mixed(ElementLayer.Hydrogen, CutFaceMaterial.HydrogenTongue, unmixed);
            if (level == 2) return Mixed(ElementLayer.CarbonOxygenCore, CutFaceMaterial.OxygenSheath, unmixed);
            if (level == 3 || tongue == 2) return Mixed(ElementLayer.Helium, CutFaceMaterial.HeliumSheath, unmixed);
        }
        return new CutFacePoint(unmixed, CutFaceMaterial.Layer, unmixed);
    }

    private static CutFacePoint Mixed(ElementLayer element, CutFaceMaterial material, ElementLayer unmixed)
        => element == unmixed ? new CutFacePoint(unmixed, CutFaceMaterial.Layer, unmixed) : new CutFacePoint(element, material, unmixed);

    /// <summary>
    /// Whether a point is inside boundary k (in layer k or below): below the slowly undulating base radius and not in one of the
    /// boundary's inward fingers, or in one of its outward fingers. The face angle is cut into cells of P radians (with the
    /// boundary's own phase); each cell holds an outward finger at its middle and an inward finger at its edge.
    /// </summary>
    private static bool InsideBoundary(int k, BoundaryFingers bf, float r, float psi, float amp, float rag)
    {
        float lo = MathF.Min(amp, bf.UndulationDown), hi = MathF.Min(amp, bf.UndulationUp);
        if (r <= bf.Radius * (1f - lo) - bf.LengthDown) return true;
        if (r > bf.Radius * (1f + hi) + bf.LengthUp) return false;
        float nn = Math.Clamp(BoundaryGain * FaceNoise.Fbm1(BoundarySeed + (uint)k, psi * BoundaryFreq, 3), -1f, 1f);
        float rb = bf.Radius * (1f + (nn >= 0f ? hi : lo) * nn);
        if (!(bf.Period > 0f)) return r <= rb;
        float u = psi / bf.Period + bf.Phase;
        if (r > rb)
        {
            float cell = MathF.Floor(u);
            uint s = FaceNoise.Hash(BoundaryFingerSeed + (uint)k * 7919u + unchecked((uint)(int)cell));
            float x = (u - cell - 0.5f - FingerJitter * (2f * FaceNoise.Unit(s + 1u) - 1f)) * bf.Period * bf.Radius;
            return InBoundaryFinger(x, r - rb, bf.LengthUp, s, rag);
        }
        float j = MathF.Floor(u + 0.5f);
        uint s2 = FaceNoise.Hash(BoundaryFingerSeed + 0x8000u + (uint)k * 7919u + unchecked((uint)(int)j));
        float x2 = (u - j - FingerJitter * (2f * FaceNoise.Unit(s2 + 1u) - 1f)) * bf.Period * bf.Radius;
        return !InBoundaryFinger(x2, rb - r, bf.LengthDown, s2, rag);
    }

    /// <summary>A boundary finger (seed s) at arc offset x from its base's axis and height y above its base: absent in FingerGap of
    /// cells; otherwise length 0.5 to 1 of nominal, a stalk FingerAspect / (0.8 to 1.05) times longer than wide, leaning sideways,
    /// narrowing slightly, ending in a mushroom head whose top is the finger's length.</summary>
    private static bool InBoundaryFinger(float x, float y, float length, uint s, float rag)
    {
        if (!(length > 0f) || FaceNoise.Unit(s + 4u) < FingerGap) return false;
        float l = length * (0.5f + 0.5f * FaceNoise.Unit(s));
        float h = l / (2f * FingerAspect) * (0.8f + 0.25f * FaceNoise.Unit(s + 2u));
        x = MathF.Abs(x - FingerLean * (2f * FaceNoise.Unit(s + 3u) - 1f) * y);
        float capW = FingerHead * h * rag;
        float capH = FingerHeadHeight * FingerHead * h;
        float capC = l - capH;
        if (y <= capC && x <= h * (1f - FingerNarrow * y / l) * rag) return true;
        return InHead(x, y, capW, capH, capC);
    }

    /// <summary>
    /// A plume at this face point: 0 outside, 1 its nickel, 2 its oxygen sheath, 3 its helium sheath. Widths are arcs: from the root
    /// (inside the nickel layer) a narrower base widening to the stalk, about a slowly meandering centreline, ending in a mushroom
    /// head (a dome whose top is the tip, and a rolled-back underside that overhangs the stalk). The sheaths are the same shape
    /// grown by the rim thicknesses (fractions of the local radius).
    /// </summary>
    private static int InPlume(float r, float psi, FaceFinger f, float w, float root, float rag, float clump, float rimO, float rimHe)
    {
        float rims = rimO + rimHe;
        if (r < root || r > f.TipRadius * (1f + rims)) return 0;
        float s = Math.Clamp((r - root) / (f.TipRadius - root), 0f, 1f);
        float centre = f.CentreRad + MeanderAmp * w * s * FaceNoise.Value1(f.Seed, s * MeanderFreq);
        float arc = MathF.Abs(psi - centre) * r;
        float stalk = f.HalfWidth * (1f - StalkNarrow * s) * rag;
        float hw = stalk * (StalkRoot + (1f - StalkRoot) * SmoothStep(0f, BaseLength, s));
        float capW = CapWidth * f.HalfWidth * rag;
        float capR = CapHeight * CapWidth * f.HalfWidth;
        float capC = f.TipRadius - capR;
        float clumpy = SmoothStep(ClumpFrom, ClumpTo, s) * (1f - SmoothStep(ClumpSolidFrom, ClumpSolidTo, s));
        bool dense = clump > -2f + (ClumpLevel + 2f) * clumpy;   // solid base, clumpy mid-stalk, solid again below the head
        for (int level = 1; level <= 3; level++)
        {
            float ex = level == 1 ? 0f : level == 2 ? rimO : rims;
            if (level > 1 && ex == (level == 2 ? 0f : rimO)) continue;   // no such sheath
            if (r <= capC && arc <= hw + ex * r && (level > 1 || dense)) return level;
            if (InHead(arc, r, capW + ex * r, capR + ex * f.TipRadius, capC)) return level;
        }
        return 0;
    }

    /// <summary>A mushroom head, in arcs: above its widest line (height capC) a dome of half-width W and height R; below it the
    /// rolled-back underside, hanging deepest towards the rim.</summary>
    private static bool InHead(float arc, float y, float W, float R, float capC)
    {
        float x = arc / W;
        if (x >= 1f) return false;
        if (y >= capC)
        {
            float t = (y - capC) / R;
            return x * x + t * t <= 1f;
        }
        return capC - y <= R * (CapDroop0 + CapDroop * x * x) * MathF.Sqrt(1f - x * x);
    }

    /// <summary>A tongue at this face point: 0 outside, 1 its hydrogen, 2 its helium rim. From the root (inside the hydrogen layer)
    /// down to its tip, broad at the root and tapering (widths are arcs), meandering and ragged.</summary>
    private static int InTongue(float r, float psi, FaceTongue t, float w, float root, float rag, float rimHe)
    {
        if (r < t.TipRadius || r > root) return 0;
        float s = (r - t.TipRadius) / (root - t.TipRadius);
        float centre = t.CentreRad + TongueMeander * w * FaceNoise.Value1(t.Seed, s * MeanderFreq);
        float arc = MathF.Abs(psi - centre) * r;
        float hw = t.HalfWidth * (0.15f + 0.85f * s) * rag;
        if (arc <= hw) return 1;
        return arc <= hw + rimHe * r ? 2 : 0;
    }

    /// <summary>GLSL's smoothstep, written out (cutface.frag uses the same expression).</summary>
    private static float SmoothStep(float e0, float e1, float x)
    {
        float t = Math.Clamp((x - e0) / (e1 - e0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    /// <summary>The face's opacity at a face point (<see cref="Opacity"/> with the radius and whether it is mixed material); 0 off the face.</summary>
    public double OpacityAt(int plane, float a, float b)
        => At(plane, a, b) is { } p ? Opacity(MathF.Sqrt(a * a + b * b) * OuterRadiusCm, p.Material != CutFaceMaterial.Layer) : 0;

    /// <summary>
    /// The face's opacity at this radius (ruling R-P2-23): the density profile of structure section S1 relative to its value at
    /// v_t, rho(v) / rho(v_t) = 1 inside v_t and (v / v_t)^-n outside, where v is the speed of the debris drawn at this radius
    /// (the free-expansion radius is proportional to speed in <see cref="ParticleKinematics"/>'s rule, so
    /// v / v_t = <see cref="FreeRadiusCm"/>(radius) / <see cref="TransitionRadiusCm"/>; without shocks the free radius is the radius).
    /// Plume, sheath and tongue pixels are opaque; the rest of the face fades like the cloud, a glowing ball with a slice removed.
    /// </summary>
    public double Opacity(double radiusCm, bool inFinger)
    {
        double free = FreeRadiusCm(radiusCm);
        if (inFinger || !(free > TransitionRadiusCm)) return 1;
        return Math.Clamp(Math.Pow(free / TransitionRadiusCm, -OuterIndex), 0, 1);
    }

    // ---- The shading's precomputed inputs (what the shader gets as uniforms), rebuilt if the inputs differ ----

    private ShadingData? _shading;

    private ShadingData Shading
    {
        get
        {
            var sh = _shading;
            if (sh is null || !sh.IsFor(this)) _shading = sh = new ShadingData(this);
            return sh;
        }
    }

    private sealed class ShadingData
    {
        public readonly float[] Outer;
        public readonly BoundaryFingers[] Boundaries;
        public readonly float FingerRoot, TongueRoot, TongueCut;
        /// <summary>Indices of the oxygen/silicon and helium layers (-1: none): the colours of the sheaths.</summary>
        public readonly int OxygenLayer, HeliumLayer;
        public readonly IReadOnlyList<FingerCluster>[] Clusters;
        private readonly IReadOnlyList<(ElementLayer, double)> _layers;
        private readonly FingerCones _cones;
        private readonly double _phi, _ni, _h;

        public bool IsFor(CutFaceModel m) => ReferenceEquals(_layers, m.Layers) && ReferenceEquals(_cones, m.Cones)
            && _phi.Equals(m.WedgeAngleRad) && _ni.Equals(m.NickelMaxRadiusCm) && _h.Equals(m.HydrogenMinRadiusCm);

        public ShadingData(CutFaceModel m)
        {
            _layers = m.Layers;
            _cones = m.Cones;
            _phi = m.WedgeAngleRad;
            _ni = m.NickelMaxRadiusCm;
            _h = m.HydrogenMinRadiusCm;
            double outer = m.OuterRadiusCm;
            int n = m.Layers.Count;
            bool mixing = m.Cones.Axes.Count > 0;
            Outer = new float[n];
            Boundaries = new BoundaryFingers[Math.Max(0, n - 1)];
            for (int k = 0; k < n; k++)
                Outer[k] = k == n - 1 ? 1f : (float)(m.Layers[k].OuterRadiusCm / outer);
            for (int k = 0; k < n - 1; k++)
            {
                double R = m.Layers[k].OuterRadiusCm, below = k == 0 ? R : R - m.Layers[k - 1].OuterRadiusCm;
                double capUp = BoundaryGapShare * (m.Layers[k + 1].OuterRadiusCm - R) / R, capDown = BoundaryGapShare * below / R;
                double rn = R / outer;
                double up = mixing ? Math.Min(FingerLength, FingerShare * capUp) * rn : 0;
                double down = mixing ? Math.Min(FingerLength, FingerShare * capDown) * rn : 0;
                (up, down) = (Math.Min(up, FingerBalance * down), Math.Min(down, FingerBalance * up));   // comparable both ways
                double period = mixing ? FingerPeriod * Math.Max(up, down) / rn : 0;   // a cell's arc, over the radius
                Boundaries[k] = new BoundaryFingers((float)rn, (float)Math.Min(BoundaryIn, UndulationShare * capUp), (float)Math.Min(BoundaryIn, UndulationShare * capDown),
                    (float)up, (float)down, (float)period, FaceNoise.Unit(BoundarySeed + 0x100u + (uint)k));
            }

            OxygenLayer = HeliumLayer = -1;
            for (int k = 0; k < n; k++)
            {
                if (m.Layers[k].Layer == ElementLayer.CarbonOxygenCore) OxygenLayer = k;
                if (m.Layers[k].Layer == ElementLayer.Helium) HeliumLayer = k;
            }
            double niMax = m.NickelMaxRadiusCm / outer, hMin = m.HydrogenMinRadiusCm / outer;
            double fingerRoot = FingerRootFraction * m.NickelInnerRadiusCm / outer;
            // The tongues hang from beyond the farthest the hydrogen layer's inner boundary reaches outward.
            int hBoundary = -1;
            for (int k = 1; k < n; k++)
                if (m.Layers[k].Layer == ElementLayer.Hydrogen) { hBoundary = k - 1; break; }
            double tongueRoot = hBoundary < 0 ? double.NaN
                : Math.Min(1, TongueRootFraction * (Boundaries[hBoundary].Radius * (1 + Boundaries[hBoundary].UndulationUp) + Boundaries[hBoundary].LengthUp));
            bool plumes = mixing && niMax > fingerRoot;
            bool tongues = plumes && hMin < tongueRoot;
            FingerRoot = plumes ? (float)fingerRoot : -1f;
            TongueRoot = tongues ? (float)tongueRoot : -1f;
            TongueCut = tongues ? Boundaries[hBoundary].Radius * (1f + Boundaries[hBoundary].UndulationUp) : -1f;
            Clusters = new IReadOnlyList<FingerCluster>[2];
            for (int plane = 0; plane < 2; plane++)
            {
                var list = new List<FingerCluster>();
                for (int k = 0; plumes && k < m.Cones.Axes.Count; k++)
                {
                    if (WedgeOrientation.ConeOnFace(plane, _phi, m.Cones.Axes[k], m.Cones.HalfAngleRad) is not { } x) continue;
                    if (Math.Abs(x.CentreRad) - Band * x.HalfWidthRad >= Math.PI / 2) continue;   // all on the plane's undrawn half
                    list.Add(Cluster(k, x, niMax, hMin, tongues ? tongueRoot : double.NaN, fingerRoot));
                }
                Clusters[plane] = list;
            }
        }

        /// <summary>
        /// Cone k's cluster: 1 or 2 plumes (on the chord's centre, or at ±PlumeSpread w), tips 0.7 to 1 of NickelMax (one exactly),
        /// and tongues on both flanks (±TongueFlank w) and between two plumes (one reaching HydrogenMin exactly). Everything from
        /// hashes of the cone index.
        /// </summary>
        private static FingerCluster Cluster(int k, FaceCrossing x, double niMax, double hMin, double tongueRoot, double fingerRoot)
        {
            uint seed = FaceNoise.Hash(ConeSeed + (uint)k);
            int count = MinPlumes + (int)(FaceNoise.Hash(seed) % (uint)(MaxPlumes - MinPlumes + 1));
            int longest = (int)(FaceNoise.Hash(seed + 1u) % (uint)count);
            double w = x.HalfWidthRad;
            var fingers = new FaceFinger[count];
            for (int j = 0; j < count; j++)
            {
                uint fs = FaceNoise.Hash(seed + 16u + (uint)j) >> 8;   // 24 bits: exact as a float uniform
                double centre = x.CentreRad + (count == 1 ? 0 : (2 * j - 1) * PlumeSpread * w) + 0.1 * w * (2 * FaceNoise.Unit(fs) - 1);
                float tip = j == longest ? (float)niMax : (float)((0.7 + 0.27 * FaceNoise.Unit(fs + 1u)) * niMax);
                double stalk = StalkArc * (tip - fingerRoot) / Math.Sqrt(count) * (0.85 + 0.3 * FaceNoise.Unit(fs + 2u));
                fingers[j] = new FaceFinger((float)centre, tip, (float)stalk, fs);
            }
            var tongues = new List<FaceTongue>();
            if (double.IsFinite(tongueRoot))
            {
                // Positions: the left flank, between the plumes (two plumes), the right flank.
                var at = new List<double> { x.CentreRad - TongueFlank * w };
                if (count == 2) at.Add(0.5 * (fingers[0].CentreRad + fingers[1].CentreRad));
                at.Add(x.CentreRad + TongueFlank * w);
                int deepest = (int)(FaceNoise.Hash(seed + 2u) % (uint)at.Count);
                for (int j = 0; j < at.Count; j++)
                {
                    uint ts = FaceNoise.Hash(seed + 64u + (uint)j) >> 8;
                    float tip = (float)hMin;
                    if (j != deepest) tip += (float)((tongueRoot - hMin) * 0.35 * FaceNoise.Unit(ts + 1u));
                    tongues.Add(new FaceTongue((float)(at[j] + 0.05 * w * (2 * FaceNoise.Unit(ts) - 1)), tip, (float)(TongueArc * tongueRoot), ts));
                }
            }
            return new FingerCluster(k, (float)x.CentreRad, (float)w, fingers, tongues);
        }
    }
}

/// <summary>An interior layer boundary as the faces draw it (R-P2-35), all over the outer radius: its radius; the slow undulation's
/// largest relative move outward and inward; its fingers' nominal lengths outward (the inner layer's) and inward (the outer
/// layer's), 0 without mixing; the angular period of a cell (one finger each way); the cells' phase.</summary>
public readonly record struct BoundaryFingers(float Radius, float UndulationUp, float UndulationDown, float LengthUp, float LengthDown,
    float Period, float Phase);

/// <summary>What a face point is drawn as (Task 16): the layer (boundary fingers included), a nickel plume, a hydrogen tongue, or
/// a sheath (oxygen/silicon or helium dragged along a plume; helium also rims a tongue).</summary>
public enum CutFaceMaterial { Layer, NickelFinger, HydrogenTongue, OxygenSheath, HeliumSheath }

/// <summary>A cut-face point: the element drawn, whether it is the layer or mixed material, and the (fingered) layer beneath.</summary>
public readonly record struct CutFacePoint(ElementLayer Layer, CutFaceMaterial Material, ElementLayer Unmixed);

/// <summary>One nickel plume of a cluster: centre (face angle, rad), tip radius and stalk half-width (arc, over the outer
/// radius), and its noise seed.</summary>
public readonly record struct FaceFinger(float CentreRad, float TipRadius, float HalfWidth, uint Seed);

/// <summary>One hydrogen tongue: centre (face angle), tip radius (its deepest point) and root half-width (arc, over the outer radius), and its seed.</summary>
public readonly record struct FaceTongue(float CentreRad, float TipRadius, float HalfWidth, uint Seed);

/// <summary>A cone's mixing on one face: its chord's centre and half-width (face angles, rad; the plumes and tongues stay within
/// <see cref="CutFaceModel.Band"/> half-widths of the centre), plumes in order across it and the tongues beside and between them.
/// <see cref="Cone"/> is the cone's index (its seeds).</summary>
public sealed record FingerCluster(int Cone, float CentreRad, float HalfWidthRad, IReadOnlyList<FaceFinger> Fingers, IReadOnlyList<FaceTongue> Tongues);
