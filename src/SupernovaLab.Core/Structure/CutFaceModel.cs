namespace SupernovaLab.Core.Structure;

/// <summary>
/// What the debris cutaway's two cut faces show (ruling R-P2-21, which replaces plan ruling P7's particle slabs): the
/// element at each point of a face, worked out analytically from the particle set's own construction, so the faces are
/// continuous surfaces however few particles lie near the planes. Schematic: a face shows the unmixed layers as nested
/// spheres (the layer boundaries are where the mass coordinate q crosses each layer's cumulative mass, mapped through the
/// profile's v(q) and <see cref="ParticleKinematics"/>'s radius rule), with Rayleigh-Taylor mixing drawn inside each finger
/// cone's chord across the face (Task 16, see <see cref="At"/>): a cluster of nickel fingers reaching out to NickelMax and
/// hydrogen tongues reaching in to HydrogenMin in the gaps between them, with ragged layer boundaries. The physics (reach,
/// cones, fractions, particles) is <see cref="MixingModel"/>'s; only the drawn shapes are schematic.
/// Once there are shocks the radii are the confined ones (<see cref="ParticleKinematics.RadiusCm"/> with <see cref="Shocks"/>),
/// so the faces stop at the contact surface as the particles do. The 3D view's <c>cutface.frag</c> mirrors
/// <see cref="At"/> and <see cref="Opacity"/> operation for operation (single precision, <see cref="FaceNoise"/>): change one, change the other.
/// </summary>
/// <param name="Layers">The layers, centre outward, with the radius (cm) of each one's outer boundary; the last is <see cref="OuterRadiusCm"/>.</param>
/// <param name="NickelMaxRadiusCm">Radius of the nickel finger tips (NaN: no nickel fingers).</param>
/// <param name="HydrogenMinRadiusCm">Radius the hydrogen tongues reach inward to (NaN: none).</param>
/// <param name="Cones">The finger cones (empty when there are no fingers).</param>
/// <param name="TransitionRadiusCm">Free-expansion radius of the profile's break speed v_t (ruling R-P2-23; compared with
/// <see cref="FreeRadiusCm"/>).</param>
/// <param name="OuterIndex">The profile's outer slope n: the face thins as (v / v_t)^-n beyond v_t.</param>
public sealed record CutFaceModel(IReadOnlyList<(ElementLayer Layer, double OuterRadiusCm)> Layers, double NickelMaxRadiusCm,
    double HydrogenMinRadiusCm, FingerCones Cones, double TransitionRadiusCm, double OuterIndex)
{
    // ---- Presentation constants [Task 16, schematic; cutface.frag declares the per-pixel ones with the same values] ----

    // A cone's chord on a face has half-width w (face angle). Its 2 or 3 plumes are columns whose widths are ARCS (fractions of the
    // outer radius), not angles, so their sides run roughly parallel instead of meeting at the centre; near the core, where the
    // chord is narrower than the columns, they merge into one broad trunk that fills the chord (nickel never leaves the cone).

    /// <summary>The outermost plumes' centres sit at ±SpreadEdge w (a third, if any, on the chord's centre).</summary>
    public const float SpreadEdge = 0.6f;
    /// <summary>A plume's stalk half-width (arc) as a fraction of its tip radius, divided by sqrt(count − 1); each plume's own varies
    /// by ±15%. The stalk narrows by StalkNarrow from root to head.</summary>
    public const float StalkArc = 0.05f, StalkNarrow = 0.15f;
    /// <summary>The base: the stalk is (1 + BaseBoost) times wider where it leaves the nickel layer, narrowing to the stalk over the
    /// first BaseLength of the plume's length.</summary>
    public const float BaseBoost = 0.8f, BaseLength = 0.35f;
    /// <summary>The mushroom head: half-width in stalk half-widths; dome height in head half-widths; its rolled-back underside
    /// hangs below the widest line by height (CapDroop0 + CapDroop x^2) sqrt(1 − x^2), x = arc / head half-width, deepest towards
    /// the rim (the overhang).</summary>
    public const float CapWidth = 2.3f, CapHeight = 0.7f, CapDroop0 = 0.35f, CapDroop = 0.9f;
    /// <summary>The centreline's slow meander: angular amplitude MeanderAmp w s (none at the base, growing outward; comparable to
    /// the stalk's width near the head), MeanderFreq cycles per plume length.</summary>
    public const float MeanderAmp = 0.25f, MeanderFreq = 1.3f;
    /// <summary>Sheaths dragged out with a plume (and a helium rim on a tongue): thickness as a fraction of the local radius.</summary>
    public const float RimOxygen = 0.016f, RimHelium = 0.013f;
    /// <summary>A tongue's half-width (arc) at its root, as a fraction of half the arc between its two plumes' centres there (so it
    /// fills TongueWidth of the space between them, where their stalks are clumpy or narrow; nickel wins where they overlap); it
    /// tapers to 0.15 of that at its tip. Its meander, in chord half-widths.</summary>
    public const float TongueWidth = 0.6f, TongueMeander = 0.05f;
    /// <summary>Ragged edges: a width is multiplied by 1 + EdgeLowAmp fbm2(EdgeLowFreq) + EdgeHighAmp value2(EdgeHighFreq)
    /// (frequencies per outer radius): large, slow lumps plus a little fine roughness.</summary>
    public const float EdgeLowFreq = 9f, EdgeLowAmp = 0.3f, EdgeHighFreq = 40f, EdgeHighAmp = 0.1f;
    /// <summary>Convoluted boundaries: noise frequency per radian of face angle (3 octaves, low frequency, the sum stretched by
    /// BoundaryGain and clamped to ±1); relative amplitude outside and inside the cones; the face angle over which the inside
    /// amplitude fades out beyond a chord.</summary>
    public const float BoundaryFreq = 3f, BoundaryGain = 1.6f, BoundaryOut = 0.06f, BoundaryIn = 0.15f, BoundaryFalloff = 0.3f;
    /// <summary>Clumps: a plume's base (below ClumpFrom..ClumpTo of its length) is solid; its mid-stalk is nickel only where a 2D fbm
    /// (ClumpFreq per outer radius, 2 octaves) exceeds ClumpLevel; it turns solid again between ClumpSolidFrom and ClumpSolidTo, below
    /// the head: the fragmented, clumpy nickel and oxygen of the inner mixing zone instead of solid spokes from the core.</summary>
    public const float ClumpFreq = 20f, ClumpLevel = -0.05f, ClumpFrom = 0.05f, ClumpTo = 0.2f, ClumpSolidFrom = 0.55f, ClumpSolidTo = 0.9f;
    /// <summary>A boundary moves at most this fraction of the gap to its neighbouring boundaries, so the layers keep their order.</summary>
    public const double BoundaryGapShare = 0.45;
    /// <summary>The plumes start inside the nickel layer (this fraction of its radius) and the tongues inside the hydrogen layer
    /// (this multiple of its inner boundary's farthest undulation), so both join their own layer.</summary>
    public const double FingerRootFraction = 0.9, TongueRootFraction = 1.03;
    /// <summary>Plumes per cone on a face.</summary>
    public const int MinPlumes = 2, MaxPlumes = 3;
    /// <summary>Fixed seeds: the cone's (plus its index), the edge noise's (plus the plane), the boundaries' (plus the boundary index).</summary>
    public const uint ConeSeed = 0x51ed0000u, EdgeSeed = 17u, BoundarySeed = 101u, ClumpSeed = 29u;

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

    // ---- Task 16: the shading, in face coordinates normalised to the outer radius (single precision, as the shader) ----

    /// <summary>Each layer's outer radius over <see cref="OuterRadiusCm"/> (the last is 1), as the shader receives it.</summary>
    public IReadOnlyList<float> NormalisedOuter => Shading.Outer;

    /// <summary>Each boundary's largest relative move outward and inward (<see cref="BoundaryGapShare"/> of the gap above and of
    /// the gap below it; 0 for the outer edge), so neighbouring boundaries never meet.</summary>
    public IReadOnlyList<float> BoundaryCapUp => Shading.CapUp;
    public IReadOnlyList<float> BoundaryCapDown => Shading.CapDown;

    /// <summary>Where the fingers start and the tongues hang from, over <see cref="OuterRadiusCm"/> (-1: none).</summary>
    public float FingerRootRadius => Shading.FingerRoot;
    public float TongueRootRadius => Shading.TongueRoot;

    /// <summary>The finger clusters on face <paramref name="plane"/> (0: w.x = 0, 1: w.z = 0): one per cone that crosses the face's
    /// plane substantially (<see cref="WedgeOrientation.ConeOnFace"/>) with some of its chord on the drawn half.</summary>
    public IReadOnlyList<FingerCluster> ClustersOn(int plane) => Shading.Clusters[plane];

    /// <summary>
    /// The face point at normalised face coordinates (<paramref name="a"/> along y, <paramref name="b"/> &gt;= 0 along the face's
    /// outward axis, both over <see cref="OuterRadiusCm"/>) of face <paramref name="plane"/>; null off the face or beyond the debris.
    /// <list type="bullet">
    /// <item>Layers: the first layer whose boundary radius R_k (1 + A_k N_k(psi)) is not inside the point, N_k a 1D fbm of the face
    /// angle psi = atan2(a, b) seeded by the boundary, A_k = min(cap_k, BoundaryOut + (BoundaryIn − BoundaryOut) W), W = 1 inside
    /// a cluster's chord fading over BoundaryFalloff: low-frequency undulations, larger in the cones, never past a neighbour (cap_k).</item>
    /// <item>Nickel plumes (2 or 3 per cluster, nickel only inside the chord, |psi − centre| &lt;= w): from inside the nickel layer
    /// a solid broad base narrowing to a stalk (clumpy nickel and oxygen at mid-length, see <see cref="ClumpLevel"/>), about a slowly meandering centreline, ending in a mushroom head about twice the stalk's
    /// width whose rolled-back underside overhangs the stalk; the top of the head is the tip (at most NickelMax). Widths are
    /// multiplied by 1 + low-frequency lumps + fine roughness (<see cref="EdgeLowAmp"/>). Each plume wears an oxygen/silicon then a
    /// helium sheath (<see cref="RimOxygen"/>, <see cref="RimHelium"/>), the material it dragged outward.</item>
    /// <item>Hydrogen tongues (one in each gap between plumes): from the tongue root, inside the hydrogen layer, down to the
    /// tongue's tip (at least HydrogenMin), broad at the root and tapering, meandering and ragged, with a thin helium rim.</item>
    /// </list>
    /// Precedence: plume (nickel, then its sheaths) over tongue over layer. Below the nickel layer's (undulating) boundary the face
    /// is the core; a plume, sheath or tongue counts only where it intrudes into another layer (a tongue inside the hydrogen layer
    /// is that layer, with its fade).
    /// </summary>
    public CutFacePoint? At(int plane, float a, float b)
    {
        if (!(b >= 0f)) return null;
        float r = MathF.Sqrt(a * a + b * b);
        if (!(r <= 1f)) return null;
        var sh = Shading;
        float psi = MathF.Atan2(a, b);
        var clusters = sh.Clusters[plane];

        // Smeared boundaries.
        float weight = 0f;
        foreach (var c in clusters)
            weight = MathF.Max(weight, 1f - SmoothStep(c.HalfWidthRad, c.HalfWidthRad + BoundaryFalloff, MathF.Abs(psi - c.CentreRad)));
        float amp = BoundaryOut + (BoundaryIn - BoundaryOut) * weight;
        int n = sh.Outer.Length, layer = n - 1;
        for (int k = 0; k < n - 1; k++)
        {
            float R = sh.Outer[k], lo = MathF.Min(amp, sh.CapDown[k]), hi = MathF.Min(amp, sh.CapUp[k]);
            if (r <= R * (1f - lo)) { layer = k; break; }
            if (r <= R * (1f + hi))
            {
                float nn = Math.Clamp(BoundaryGain * FaceNoise.Fbm1(BoundarySeed + (uint)k, psi * BoundaryFreq, 3), -1f, 1f);
                if (r <= R * (1f + (nn >= 0f ? hi : lo) * nn)) { layer = k; break; }
            }
        }
        var unmixed = Layers[layer].Layer;

        // Mixing is drawn only outside the unmixed nickel core (below its boundary the face is the normal core), and a plume,
        // sheath or tongue counts only where it intrudes into another layer: inside its own layer it is that layer (so a tongue's
        // root in a faded hydrogen layer fades with it).
        if (clusters.Count > 0 && unmixed != ElementLayer.NickelIron)
        {
            float rimO = sh.OxygenLayer >= 0 ? RimOxygen : 0f, rimHe = sh.HeliumLayer >= 0 ? RimHelium : 0f;
            float rag = 1f + EdgeLowAmp * FaceNoise.Fbm2(EdgeSeed + (uint)plane, a * EdgeLowFreq, b * EdgeLowFreq, 2)
                           + EdgeHighAmp * FaceNoise.Value2(EdgeSeed + 8u + (uint)plane, a * EdgeHighFreq, b * EdgeHighFreq);
            float clump = FaceNoise.Fbm2(ClumpSeed + (uint)plane, a * ClumpFreq, b * ClumpFreq, 2);
            int level = 0;   // 1 nickel, 2 oxygen sheath, 3 helium sheath
            foreach (var c in clusters)
            {
                float off = MathF.Abs(psi - c.CentreRad);
                if (off > c.HalfWidthRad + rimO + rimHe) continue;
                foreach (var f in c.Fingers)
                {
                    int l = InPlume(r, psi, f, c.HalfWidthRad, sh.FingerRoot, rag, clump, rimO, rimHe);
                    if (l == 1 && off > c.HalfWidthRad) l = rimO > 0f ? 2 : 3;   // nickel stays inside its cone
                    if (l > 0 && (level == 0 || l < level)) level = l;
                }
            }
            if (level == 1) return Mixed(ElementLayer.NickelIron, CutFaceMaterial.NickelFinger, unmixed);
            int tongue = 0;   // 1 hydrogen, 2 its helium rim
            if (unmixed != ElementLayer.Hydrogen)
                foreach (var c in clusters)
                {
                    if (MathF.Abs(psi - c.CentreRad) > c.HalfWidthRad) continue;
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
    /// A plume at this face point: 0 outside, 1 its nickel, 2 its oxygen sheath, 3 its helium sheath. A column (widths are arcs):
    /// from the root (inside the nickel layer) a broad base narrowing to the stalk, about a slowly meandering centreline, ending
    /// in a mushroom head about twice the stalk's width (a dome whose top is the tip, and a rolled-back underside that overhangs
    /// the stalk). The sheaths are the same shape grown by the rim thicknesses (fractions of the local radius).
    /// </summary>
    private static int InPlume(float r, float psi, FaceFinger f, float w, float root, float rag, float clump, float rimO, float rimHe)
    {
        float rims = rimO + rimHe;
        if (r < root || r > f.TipRadius * (1f + rims)) return 0;
        float s = Math.Clamp((r - root) / (f.TipRadius - root), 0f, 1f);
        float centre = f.CentreRad + MeanderAmp * w * s * FaceNoise.Value1(f.Seed, s * MeanderFreq);
        float arc = MathF.Abs(psi - centre) * r;
        float stalk = f.HalfWidth * (1f - StalkNarrow * s) * rag;
        float hw = stalk * (1f + BaseBoost * (1f - SmoothStep(0f, BaseLength, s)));
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

    /// <summary>The mushroom head, in arcs: above its widest line (radius capC) a dome of half-width W and height R; below it the
    /// rolled-back underside, hanging deepest towards the rim.</summary>
    private static bool InHead(float arc, float r, float W, float R, float capC)
    {
        float x = arc / W;
        if (x >= 1f) return false;
        if (r >= capC)
        {
            float y = (r - capC) / R;
            return x * x + y * y <= 1f;
        }
        return capC - r <= R * (CapDroop0 + CapDroop * x * x) * MathF.Sqrt(1f - x * x);
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

    /// <summary>The face's opacity at a face point (<see cref="Opacity"/> with the radius and whether it is a finger or tongue); 0 off the face.</summary>
    public double OpacityAt(int plane, float a, float b)
        => At(plane, a, b) is { } p ? Opacity(MathF.Sqrt(a * a + b * b) * OuterRadiusCm, p.Material != CutFaceMaterial.Layer) : 0;

    /// <summary>
    /// The face's opacity at this radius (ruling R-P2-23): the density profile of structure section S1 relative to its value at
    /// v_t, rho(v) / rho(v_t) = 1 inside v_t and (v / v_t)^-n outside, where v is the speed of the debris drawn at this radius
    /// (the free-expansion radius is proportional to speed in <see cref="ParticleKinematics"/>'s rule, so
    /// v / v_t = <see cref="FreeRadiusCm"/>(radius) / <see cref="TransitionRadiusCm"/>; without shocks the free radius is the radius).
    /// A finger or tongue pixel is opaque; the rest of the face fades like the cloud, a glowing ball with a slice removed.
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
        public readonly float[] Outer, CapUp, CapDown;
        public readonly float FingerRoot, TongueRoot;
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
            Outer = new float[n];
            CapUp = new float[n];
            CapDown = new float[n];
            for (int k = 0; k < n; k++)
            {
                double R = m.Layers[k].OuterRadiusCm, below = k == 0 ? R : R - m.Layers[k - 1].OuterRadiusCm;
                Outer[k] = k == n - 1 ? 1f : (float)(R / outer);
                CapDown[k] = k == n - 1 ? 0f : (float)(BoundaryGapShare * below / R);
                CapUp[k] = k == n - 1 ? 0f : (float)(BoundaryGapShare * (m.Layers[k + 1].OuterRadiusCm - R) / R);
            }

            OxygenLayer = HeliumLayer = -1;
            for (int k = 0; k < n; k++)
            {
                if (m.Layers[k].Layer == ElementLayer.CarbonOxygenCore) OxygenLayer = k;
                if (m.Layers[k].Layer == ElementLayer.Helium) HeliumLayer = k;
            }
            double niMax = m.NickelMaxRadiusCm / outer, hMin = m.HydrogenMinRadiusCm / outer;
            double fingerRoot = FingerRootFraction * m.NickelInnerRadiusCm / outer;
            // The tongues hang from beyond the farthest the hydrogen layer's inner boundary undulates.
            int hBoundary = -1;
            for (int k = 1; k < n; k++)
                if (m.Layers[k].Layer == ElementLayer.Hydrogen) { hBoundary = k - 1; break; }
            double tongueRoot = hBoundary < 0 ? double.NaN
                : Math.Min(1, TongueRootFraction * (1 + Math.Min(BoundaryIn, CapUp[hBoundary])) * m.HydrogenInnerRadiusCm / outer);
            bool fingers = m.Cones.Axes.Count > 0 && niMax > fingerRoot;
            bool tongues = fingers && hMin < tongueRoot;
            FingerRoot = fingers ? (float)fingerRoot : -1f;
            TongueRoot = tongues ? (float)tongueRoot : -1f;
            Clusters = new IReadOnlyList<FingerCluster>[2];
            for (int plane = 0; plane < 2; plane++)
            {
                var list = new List<FingerCluster>();
                for (int k = 0; fingers && k < m.Cones.Axes.Count; k++)
                {
                    if (WedgeOrientation.ConeOnFace(plane, _phi, m.Cones.Axes[k], m.Cones.HalfAngleRad) is not { } x) continue;
                    if (Math.Abs(x.CentreRad) - x.HalfWidthRad >= Math.PI / 2) continue;   // all on the plane's undrawn half
                    list.Add(Cluster(k, x, niMax, hMin, tongues ? tongueRoot : double.NaN,
                        (OxygenLayer >= 0 ? RimOxygen : 0) + (HeliumLayer >= 0 ? RimHelium : 0)));
                }
                Clusters[plane] = list;
            }
        }

        /// <summary>
        /// Cone k's cluster: 2 or 3 plumes across the chord (the outer ones at ±SpreadEdge w), tips 0.7 to 1 of NickelMax (one
        /// exactly), and a tongue in each gap (one reaching HydrogenMin exactly) whose root half-width is TongueWidth of half the arc
        /// between its plumes' centres at the tongue root. Everything from hashes of the cone index.
        /// </summary>
        private static FingerCluster Cluster(int k, FaceCrossing x, double niMax, double hMin, double tongueRoot, double rims)
        {
            uint seed = FaceNoise.Hash(ConeSeed + (uint)k);
            int count = MinPlumes + (int)(FaceNoise.Hash(seed) % (uint)(MaxPlumes - MinPlumes + 1));
            int longest = (int)(FaceNoise.Hash(seed + 1u) % (uint)count);
            double w = x.HalfWidthRad, spacing = 2 * SpreadEdge * w / (count - 1);
            var fingers = new FaceFinger[count];
            for (int j = 0; j < count; j++)
            {
                uint fs = FaceNoise.Hash(seed + 16u + (uint)j) >> 8;   // 24 bits: exact as a float uniform
                double centre = x.CentreRad - SpreadEdge * w + j * spacing + 0.05 * spacing * (2 * FaceNoise.Unit(fs) - 1);
                float tip = j == longest ? (float)niMax : (float)((0.7 + 0.27 * FaceNoise.Unit(fs + 1u)) * niMax);
                double stalk = StalkArc * tip / Math.Sqrt(count - 1) * (0.85 + 0.3 * FaceNoise.Unit(fs + 2u));
                fingers[j] = new FaceFinger((float)centre, tip, (float)stalk, fs);
            }
            var tongues = new List<FaceTongue>();
            if (double.IsFinite(tongueRoot))
            {
                int deepest = (int)(FaceNoise.Hash(seed + 2u) % (uint)(count - 1));
                for (int j = 0; j < count - 1; j++)
                {
                    uint ts = FaceNoise.Hash(seed + 64u + (uint)j) >> 8;
                    double centre = 0.5 * (fingers[j].CentreRad + fingers[j + 1].CentreRad) + 0.05 * spacing * (2 * FaceNoise.Unit(ts) - 1);
                    float tip = (float)hMin;
                    if (j != deepest) tip += (float)((tongueRoot - hMin) * 0.35 * FaceNoise.Unit(ts + 1u));
                    double between = (fingers[j + 1].CentreRad - fingers[j].CentreRad) * tongueRoot;   // centre to centre, arc
                    tongues.Add(new FaceTongue((float)centre, tip, (float)(TongueWidth * 0.5 * between), ts));
                }
            }
            return new FingerCluster(k, (float)x.CentreRad, (float)w, fingers, tongues);
        }
    }

    /// <summary>The nominal gap (arc, over the outer radius) between neighbouring plumes' sheaths at radius r: their centres'
    /// separation as an arc less both stalk half-widths (before narrowing and ragging) and both sheaths.</summary>
    public static double NominalGap(FaceFinger left, FaceFinger right, double r, double rims)
        => (right.CentreRad - left.CentreRad) * r - left.HalfWidth - right.HalfWidth - 2 * rims * r;
}

/// <summary>What a face point is drawn as (Task 16): the layer, a nickel plume, a hydrogen tongue, or a sheath (oxygen/silicon or
/// helium dragged along a plume; helium also rims a tongue).</summary>
public enum CutFaceMaterial { Layer, NickelFinger, HydrogenTongue, OxygenSheath, HeliumSheath }

/// <summary>A cut-face point: the element drawn, whether it is the layer or a finger / tongue, and the (smeared) unmixed layer beneath.</summary>
public readonly record struct CutFacePoint(ElementLayer Layer, CutFaceMaterial Material, ElementLayer Unmixed);

/// <summary>One nickel plume of a cluster: centre (face angle, rad), tip radius and stalk half-width (arc, over the outer
/// radius), and its noise seed.</summary>
public readonly record struct FaceFinger(float CentreRad, float TipRadius, float HalfWidth, uint Seed);

/// <summary>One hydrogen tongue: centre (face angle), tip radius (its deepest point) and root half-width (arc, over the outer radius), and its seed.</summary>
public readonly record struct FaceTongue(float CentreRad, float TipRadius, float HalfWidth, uint Seed);

/// <summary>A cone's mixing on one face: its chord's centre and half-width (face angles, rad), fingers in order across it and
/// the tongues between them. <see cref="Cone"/> is the cone's index (its seeds).</summary>
public sealed record FingerCluster(int Cone, float CentreRad, float HalfWidthRad, IReadOnlyList<FaceFinger> Fingers, IReadOnlyList<FaceTongue> Tongues);
