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

    /// <summary>The fingers' centres spread over this fraction of the chord's half-width.</summary>
    public const float Spread = 0.7f;
    /// <summary>
    /// A finger's stalk angular half-width at its widest, in slots (2 Spread w / count). With s the fraction of the way from root
    /// to tip, the stalk is StalkWidth (StalkRoot + (1 − StalkRoot) min(1, s / NeckStart)) slots wide (half), thin at the root
    /// so the gaps stay open for the tongues, then narrows by up to StalkNarrow from NeckStart to the cap (its neck).
    /// </summary>
    public const float StalkWidth = 0.28f;
    /// <summary>The stalk's half-width at its root, as a fraction of its widest; where the widening stops and the neck begins.</summary>
    public const float StalkRoot = 0.25f, NeckStart = 0.5f;
    /// <summary>The stalk's narrowing from NeckStart to the cap (fraction).</summary>
    public const float StalkNarrow = 0.3f;
    /// <summary>The stalk's angular half-width never exceeds this many slots.</summary>
    public const float StalkMaxAngle = 0.42f;
    /// <summary>The mushroom cap's radial semi-axis above its widest line, as a fraction of the tip radius; below it the cap is
    /// flatter (CapUnder of that).</summary>
    public const float CapLength = 0.07f, CapUnder = 0.5f;
    /// <summary>The cap's angular half-width, in the stalk's widest half-widths.</summary>
    public const float CapWidth = 2.4f;
    /// <summary>Centreline waviness: amplitude in chord half-widths, and bends per finger length.</summary>
    public const float WiggleAmp = 0.1f, WiggleFreq = 2.5f;
    /// <summary>A tongue's centreline waviness, in chord half-widths.</summary>
    public const float TongueWiggle = 0.06f;
    /// <summary>A tongue's angular half-width at its root, in slots; it narrows to a spike at its tip.</summary>
    public const float TongueWidth = 0.7f;
    /// <summary>A tongue's angular half-width never exceeds this many slots.</summary>
    public const float TongueMaxAngle = 0.7f;
    /// <summary>Ragged edges: the edge noise's frequency (per outer radius; 2 octaves) and the width's relative change.</summary>
    public const float EdgeFreq = 28f, Ragged = 0.5f;
    /// <summary>Smeared boundaries: noise frequency per radian of face angle (3 octaves); relative amplitude outside and inside
    /// the cones; the face angle over which the inside amplitude fades out beyond a chord.</summary>
    public const float BoundaryFreq = 7f, BoundaryOut = 0.02f, BoundaryIn = 0.08f, BoundaryFalloff = 0.25f;
    /// <summary>A boundary moves at most this fraction of the gap to its neighbouring boundaries, so the layers keep their order.</summary>
    public const double BoundaryGapShare = 0.45;
    /// <summary>The fingers start inside the nickel layer (this fraction of its radius) and the tongues inside the hydrogen layer
    /// (this multiple of its inner radius, beyond any smearing), so both join their own layer.</summary>
    public const double FingerRootFraction = 0.8, TongueRootFraction = 1.08;
    /// <summary>Fixed seeds: the cone's (plus its index), the edge noise's (plus the plane), the boundaries' (plus the boundary index).</summary>
    public const uint ConeSeed = 0x51ed0000u, EdgeSeed = 17u, BoundarySeed = 101u;

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

    /// <summary>Each boundary's largest relative move (<see cref="BoundaryGapShare"/> of its gaps; 0 for the outer edge).</summary>
    public IReadOnlyList<float> BoundaryCap => Shading.Cap;

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
    /// a cluster's chord fading over BoundaryFalloff: ragged, more so in the cones, never past a neighbour (cap_k).</item>
    /// <item>Nickel fingers (inside a cluster's chord, |psi − centre| &lt;= w): from the finger root to the tip, a stalk about a
    /// wavy centreline (1D noise along it), thin at the root, widening to mid-length and narrowing slightly into a neck (see
    /// <see cref="StalkWidth"/>), times (1 + Ragged E), E the 2D edge fbm; then a mushroom cap, an ellipse flatter below its widest
    /// line, whose top is the tip; the tips are at most NickelMax.</item>
    /// <item>Hydrogen tongues (the gaps between fingers): from the tongue root down to the tongue's tip (at least HydrogenMin),
    /// narrowing to a spike at the tip, wavy and ragged likewise. Nickel wins over hydrogen, which wins over the layer; a finger
    /// or tongue counts only where it intrudes into another layer (a tongue inside the hydrogen layer is that layer, with its fade).</item>
    /// </list>
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
            float R = sh.Outer[k], A = MathF.Min(amp, sh.Cap[k]);
            if (r <= R * (1f - A)) { layer = k; break; }
            if (r <= R * (1f + A) && r <= R * (1f + A * FaceNoise.Fbm1(BoundarySeed + (uint)k, psi * BoundaryFreq, 3))) { layer = k; break; }
        }
        var unmixed = Layers[layer].Layer;

        // A finger or tongue is drawn only where it intrudes into another layer: inside its own layer it is that layer (so a
        // tongue's root in a faded hydrogen layer fades with it).
        if (clusters.Count > 0 && unmixed != ElementLayer.NickelIron)
        {
            float edge = FaceNoise.Fbm2(EdgeSeed + (uint)plane, a * EdgeFreq, b * EdgeFreq, 2);
            float rag = 1f + Ragged * edge;
            foreach (var c in clusters)
            {
                if (MathF.Abs(psi - c.CentreRad) > c.HalfWidthRad) continue;
                float slot = 2f * Spread * c.HalfWidthRad / c.Fingers.Count;
                foreach (var f in c.Fingers)
                    if (InFinger(r, psi, f, c.HalfWidthRad, slot, sh.FingerRoot, rag)) return new CutFacePoint(ElementLayer.NickelIron, CutFaceMaterial.NickelFinger, unmixed);
            }
            foreach (var c in clusters)
            {
                if (MathF.Abs(psi - c.CentreRad) > c.HalfWidthRad || unmixed == ElementLayer.Hydrogen) continue;
                float slot = 2f * Spread * c.HalfWidthRad / c.Fingers.Count;
                foreach (var t in c.Tongues)
                    if (InTongue(r, psi, t, c.HalfWidthRad, slot, sh.TongueRoot, rag)) return new CutFacePoint(ElementLayer.Hydrogen, CutFaceMaterial.HydrogenTongue, unmixed);
            }
        }
        return new CutFacePoint(unmixed, CutFaceMaterial.Layer, unmixed);
    }

    private static bool InFinger(float r, float psi, FaceFinger f, float w, float slot, float root, float rag)
    {
        if (r > f.TipRadius || r < root) return false;
        float s = (r - root) / (f.TipRadius - root);
        float centre = f.CentreRad + WiggleAmp * w * FaceNoise.Value1(f.Seed, s * WiggleFreq);
        float d = MathF.Abs(psi - centre);
        float capR = CapLength * f.TipRadius;
        float capC = f.TipRadius - capR;
        if (r >= capC - CapUnder * capR)
        {
            float x = d / (CapWidth * f.HalfWidth * rag);
            float y = (r - capC) / (r >= capC ? capR : CapUnder * capR);
            if (x * x + y * y <= 1f) return true;
        }
        if (r > capC) return false;
        float grow = StalkRoot + (1f - StalkRoot) * MathF.Min(1f, s / NeckStart);
        float neck = 1f - StalkNarrow * MathF.Max(0f, s - NeckStart) / (1f - NeckStart);
        float half = f.HalfWidth * grow * neck * rag;
        return d <= MathF.Min(half, StalkMaxAngle * slot);
    }

    private static bool InTongue(float r, float psi, FaceTongue t, float w, float slot, float root, float rag)
    {
        if (r < t.TipRadius || r > root) return false;
        float s = (r - t.TipRadius) / (root - t.TipRadius);
        float centre = t.CentreRad + TongueWiggle * w * FaceNoise.Value1(t.Seed, s * WiggleFreq);
        float d = MathF.Abs(psi - centre);
        float half = t.HalfWidth * (0.2f + 0.8f * s) * rag;
        return d <= MathF.Min(half, TongueMaxAngle * slot);
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
        public readonly float[] Outer, Cap;
        public readonly float FingerRoot, TongueRoot;
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
            Cap = new float[n];
            for (int k = 0; k < n; k++)
            {
                double R = m.Layers[k].OuterRadiusCm, below = k == 0 ? R : R - m.Layers[k - 1].OuterRadiusCm;
                Outer[k] = k == n - 1 ? 1f : (float)(R / outer);
                Cap[k] = k == n - 1 ? 0f : (float)(BoundaryGapShare * Math.Min(below, m.Layers[k + 1].OuterRadiusCm - R) / R);
            }

            double niMax = m.NickelMaxRadiusCm / outer, hMin = m.HydrogenMinRadiusCm / outer;
            double fingerRoot = FingerRootFraction * m.NickelInnerRadiusCm / outer;
            double tongueRoot = Math.Min(1, TongueRootFraction * m.HydrogenInnerRadiusCm / outer);
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
                    list.Add(Cluster(k, x, niMax, hMin, tongues ? tongueRoot : double.NaN));
                }
                Clusters[plane] = list;
            }
        }

        /// <summary>Cone k's cluster: 3 to 5 fingers spread across the chord, tips 0.7 to 1 of NickelMax (one exactly), and a
        /// tongue in each gap (one reaching HydrogenMin exactly). Everything from hashes of the cone index.</summary>
        private static FingerCluster Cluster(int k, FaceCrossing x, double niMax, double hMin, double tongueRoot)
        {
            uint seed = FaceNoise.Hash(ConeSeed + (uint)k);
            int count = 3 + (int)(FaceNoise.Hash(seed) % 3u);
            int longest = (int)(FaceNoise.Hash(seed + 1u) % (uint)count);
            double w = x.HalfWidthRad, slot = 2 * Spread * w / count;
            var fingers = new FaceFinger[count];
            for (int j = 0; j < count; j++)
            {
                uint fs = FaceNoise.Hash(seed + 16u + (uint)j) >> 8;   // 24 bits: exact as a float uniform
                double centre = x.CentreRad + w * Spread * ((2.0 * j + 1) / count - 1) + 0.12 * slot * (2 * FaceNoise.Unit(fs) - 1);
                float tip = j == longest ? (float)niMax : (float)((0.7 + 0.27 * FaceNoise.Unit(fs + 1u)) * niMax);
                fingers[j] = new FaceFinger((float)centre, tip, (float)(StalkWidth * slot), fs);
            }
            var tongues = new List<FaceTongue>();
            if (double.IsFinite(tongueRoot))
            {
                int deepest = (int)(FaceNoise.Hash(seed + 2u) % (uint)(count - 1));
                for (int j = 0; j < count - 1; j++)
                {
                    uint ts = FaceNoise.Hash(seed + 64u + (uint)j) >> 8;
                    double centre = 0.5 * (fingers[j].CentreRad + fingers[j + 1].CentreRad) + 0.1 * slot * (2 * FaceNoise.Unit(ts) - 1);
                    float tip = (float)hMin;
                    if (j != deepest) tip += (float)((tongueRoot - hMin) * 0.35 * FaceNoise.Unit(ts + 1u));
                    tongues.Add(new FaceTongue((float)centre, tip, (float)(TongueWidth * slot), ts));
                }
            }
            return new FingerCluster(k, (float)x.CentreRad, (float)w, fingers, tongues);
        }
    }
}

/// <summary>What a face point is drawn as (Task 16).</summary>
public enum CutFaceMaterial { Layer, NickelFinger, HydrogenTongue }

/// <summary>A cut-face point: the element drawn, whether it is the layer or a finger / tongue, and the (smeared) unmixed layer beneath.</summary>
public readonly record struct CutFacePoint(ElementLayer Layer, CutFaceMaterial Material, ElementLayer Unmixed);

/// <summary>One nickel finger of a cluster: centre (face angle, rad), tip radius and widest stalk half-width (face angle,
/// rad), and its noise seed.</summary>
public readonly record struct FaceFinger(float CentreRad, float TipRadius, float HalfWidth, uint Seed);

/// <summary>One hydrogen tongue: centre (face angle), tip radius (its deepest point) and root half-width (face angle, rad), and its seed.</summary>
public readonly record struct FaceTongue(float CentreRad, float TipRadius, float HalfWidth, uint Seed);

/// <summary>A cone's mixing on one face: its chord's centre and half-width (face angles, rad), fingers in order across it and
/// the tongues between them. <see cref="Cone"/> is the cone's index (its seeds).</summary>
public sealed record FingerCluster(int Cone, float CentreRad, float HalfWidthRad, IReadOnlyList<FaceFinger> Fingers, IReadOnlyList<FaceTongue> Tongues);
