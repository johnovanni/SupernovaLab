using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Structure;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Rendering;

/// <summary>The marker drawn for what is left at the centre (spec §6).</summary>
public enum SceneMarker
{
    None,
    /// <summary>A small bright point with a dark outline.</summary>
    NeutronStar,
    /// <summary>A neutron star with two sweeping beams, once its radio pulses escape the debris.</summary>
    Pulsar,
    /// <summary>A black disk with a thin bright ring.</summary>
    BlackHole,
    /// <summary>A small white point.</summary>
    WhiteDwarf,
}

/// <summary>
/// What the 3D view shows at one moment. Radii in cm; colours normalized sRGB; <see cref="EjectaBrightness"/> 0–1.
/// <see cref="FrameRadiusCm"/> is what auto-framing fits to 60% of the view's height; <see cref="RadiusCm"/> is the
/// radius the overlay states, named by <see cref="RadiusLabel"/>. <see cref="Notes"/> are the overlay's honest labels
/// ("colours schematic", "slowed down", ...).
/// </summary>
public sealed record SceneState
{
    public double SourceTimeS { get; init; }
    public double FrameRadiusCm { get; init; } = double.NaN;
    public double RadiusCm { get; init; } = double.NaN;
    public string RadiusLabel { get; init; } = "Radius";
    /// <summary>
    /// The gas around the star (structure §S7): its wind, as a faint haze, and any dense shell. Null for a Type Ia and for no
    /// star. The view draws the haze outside the forward shock and the dense shell until the shock has crossed it.
    /// </summary>
    public CircumstellarStructure? Surroundings { get; init; }
    /// <summary>0 to 1: how brightly the shock lights the gas it meets now (<see cref="CircumstellarStructureModel.Glow"/>); 0 before it arrives.</summary>
    public float SurroundingsGlow { get; init; }
    /// <summary>
    /// 0 to 1: how brightly the dense shell is lit now. The shock's glow (<see cref="SurroundingsGlow"/>) for a dense shell the lab
    /// models; for SN 2023ixf's pinned confined gas (rulings R-P2-27, R-P2-28) a schematic flare: lit from the debris' start until
    /// the forward shock has swept the shell (when it is no longer drawn).
    /// </summary>
    public float ShellFlare { get; init; }
    public bool ShowStar { get; init; }
    public double StarRadiusCm { get; init; } = double.NaN;
    public (float R, float G, float B) StarColor { get; init; } = (1f, 1f, 1f);
    public bool ShowEjecta { get; init; }
    public double EjectaRadiusCm { get; init; } = double.NaN;
    public double EjectaTemperatureK { get; init; } = double.NaN;
    public (float R, float G, float B) EjectaColor { get; init; } = (1f, 1f, 1f);
    public float EjectaBrightness { get; init; }
    public bool ShowJets { get; init; }
    public double JetHalfAngleRad { get; init; }
    public SceneMarker Marker { get; init; }
    /// <summary>The lab's model spin period (s) now; NaN unless <see cref="Marker"/> is <see cref="SceneMarker.Pulsar"/>.</summary>
    public double PulsarPeriodS { get; init; } = double.NaN;
    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();

    /// <summary>Whether the layered particles are drawn now (an explosion past breakout, or a failed supernova's envelope shell past its launch).</summary>
    public bool ShowParticles { get; init; }
    /// <summary>t - t_bo, s: the time the particles have moved since breakout (ruling P4); 0 when they are not drawn.</summary>
    public double SinceBreakoutS { get; init; }
    /// <summary>The radius of the fastest particle (v = v_max) now, cm (<see cref="ParticleKinematics.RadiusCm"/>); NaN when no particles.</summary>
    public double MaxRadiusCm { get; init; } = double.NaN;
    /// <summary>The radius of the particles at the 99%-of-mass speed now, cm (ruling P1's framing radius); NaN when no particles.</summary>
    public double Frame99RadiusCm { get; init; } = double.NaN;

    /// <summary>The presupernova star's layers while the shock crosses it (structure §S6), centre outward with their outer radius in cm; empty otherwise.</summary>
    public IReadOnlyList<(ElementLayer Layer, double OuterRadiusCm)> StarLayers { get; init; } = Array.Empty<(ElementLayer, double)>();
    /// <summary>The shock's radius inside the star now, cm (structure §S5); NaN when it is not inside the star.</summary>
    public double ShockInStarRadiusCm { get; init; } = double.NaN;

    /// <summary>
    /// The forward shock, contact surface and reverse shock now (<see cref="ShockStructureModel.At"/>, structure §S4), with ruling
    /// R-P2-4's confinement weight; null before they start or without ejecta. The particles past the reverse shock are confined
    /// by these (<see cref="ParticleKinematics.RadiusCm"/>), and the shells are drawn at them.
    /// </summary>
    public ShockRadii? Shocks { get; init; }

    /// <summary>
    /// The readout's second line while shocks are drawn (ruling R-P2-25): the drawn forward shock, cm (NaN without shocks), and
    /// its label (<see cref="ExplosionScene.RadioShockLabel"/>, <see cref="ExplosionScene.BlendShockLabel"/> or
    /// <see cref="ExplosionScene.ForwardShockLabel"/>).
    /// </summary>
    public double ForwardShockCm { get; init; } = double.NaN;
    public string ForwardShockLabel { get; init; } = "";

    /// <summary>
    /// The pulsar wind nebula's radius now, cm (<see cref="PulsarWindNebulaModel.RadiusCm"/>, structure §S9): a bubble inside the debris;
    /// NaN when the outcome has none or the debris are not drawn yet.
    /// </summary>
    public double NebulaRadiusCm { get; init; } = double.NaN;

    /// <summary>Whether SN 1987A's three rings are drawn (structure §S8: the outcome has the equatorial ring and the debris are drawn).</summary>
    public bool ShowRings1987A { get; init; }

    /// <summary>0 to 1: how brightly the equatorial ring's hotspot beads glow now (<see cref="Sn1987aRings.HotspotBrightness"/>).</summary>
    public float RingBrightness { get; init; }
}

/// <summary>
/// The simple 3D view's content (spec §6) for one outcome, as a function of source time: no OpenGL here.
/// <list type="bullet">
/// <item>Before breakout: the star, sized to its radius and coloured by its surface temperature.</item>
/// <item>Gas around the star (spec §6, structure §S7): the star's own wind as a faint haze, and for the dense-gas cases
/// <see cref="CsmInteractionModel"/> does not model (<see cref="CsmInteractionModel.HasDenseCsm"/>) a dense shell
/// (<see cref="SceneState.Surroundings"/>), lit where the forward shock meets it (<see cref="SceneState.SurroundingsGlow"/>). The haze
/// never drives the framing (it reaches parsecs); a dense shell does, while the debris are inside it.</item>
/// <item>After breakout: the ejecta shell at <see cref="EjectaKinematics.RadiusCm"/>, coloured by the light curve's
/// colour temperature (<see cref="LightCurveModel.ColourTemperatureK"/>, the one the V magnitudes and the schematic
/// spectrum use, so the view agrees with them; it is defined from breakout on, ruling R45a). The Stefan–Boltzmann
/// <see cref="EjectaKinematics.PhotosphereTemperatureK"/> is used only for an outcome with no light curve. The ejecta dim
/// as the bolometric luminosity falls (over four decades below the peak, to a quarter). After the light curve ends (3
/// yr) they are drawn dimly at the 3,000 K floor's colour: a remnant's light is not a blackbody, so this is schematic.</item>
/// <item>The remnant marker (after breakout), a gamma-ray burst's jets while the GRB model covers the burst, and a
/// pulsar's beams once its pulses escape (<see cref="PulsarModel.EmergenceTimeS"/>).</item>
/// <item>Failed supernova: the star until the black hole forms, then only the black hole. Type Ia: no star; a white
/// dwarf point while the debris are still about its size.</item>
/// </list>
/// </summary>
public sealed class ExplosionScene
{
    /// <summary>The ejecta are dimmed over this many decades of luminosity below the peak.</summary>
    private const double DimmingDecades = 4;

    /// <summary>The faintest ejecta brightness: still visible as a shell.</summary>
    private const float MinBrightness = 0.25f;

    /// <summary>A pulsar's beams turn once in this many seconds on screen, whatever its model period ("slowed down").</summary>
    public const double ShownPulsarPeriodS = 2;

    public const string SchematicNote = "Colours schematic";

    /// <summary>Shown while the Rayleigh-Taylor fingers are drawn (structure S3).</summary>
    public const string MixingNote = "Mixing fingers: shapes schematic; their reach is from 3D simulations (structure §S3)";

    /// <summary>Project 1's note for a dense shell whose extent is the 1e15 cm fallback (§S7 gives none).</summary>
    public const string GasNote =
        "Gas around the star (schematic): its extent is not in the research; drawn to 1e15 cm, like SN 2023ixf's";

    /// <summary>Ruling R-P2-27: SN 2023ixf's confined gas is observed geometry; its flare is schematic.</summary>
    public const string ConfinedGasNote =
        "SN 2023ixf's confined gas: observed extent (Jacobson-Galán et al. 2023); its flare is schematic — the lab's instruments do not model this gas";

    /// <summary>The haze's note is shown only while the view is no wider than this many stellar radii (the haze is then in view).</summary>
    public const double HazeNoteMaxFrameRadii = 100;

    /// <summary>The pulsar wind nebula's note is shown while it is at least this fraction of the framing radius (otherwise it is a speck).</summary>
    public const double NebulaNoteMinFrameFraction = 0.01;

    /// <summary>
    /// SN 1987A's rings' note is shown once the framing radius is at least this fraction of the equatorial ring's radius: from there the
    /// director's view (half-height H of at least frame / 0.54, rungs of 1-2-5) can contain the ring, which the 3D view draws once
    /// H &gt; 0.35 R (<c>TorusRingRenderer.Visibility</c>). For SN 1987A that is day 1,880 (5 yr). The rings never drive the framing.
    /// </summary>
    public const double RingsNoteMinFrameFraction = 0.15;

    /// <summary>The note when the extent is from a source ({0}: its citation).</summary>
    public const string GasNoteFormat = "Gas around the star: extent from {0}";

    /// <summary>Review I4: what the stated ejecta radius is, in plain words.</summary>
    public const string UniformSphereNote =
        "Radius: the edge of a uniform ball of debris with this energy and mass (the spec's simple model); the fastest " +
        "outer layers, and the line speeds in the spectrum, are faster";

    /// <summary>Shown while the shocks are drawn (structure §S4; ruling R-P2-24).</summary>
    public const string ShockNote =
        "Shocks: radii from the radio model's shock with Chevalier 1982's ratios in the first years, then Truelove & McKee 1999 " +
        "and Tang & Chevalier 2017 for steep outer debris (structure §S4)";

    /// <summary>The forward-shock readout's label from 10 yr, and for an outcome with no wind era (rulings R-P2-24, R-P2-25).</summary>
    public const string ForwardShockLabel = "Forward shock (steep-ejecta solution)";

    /// <summary>The forward-shock readout's label in the wind era, to 3 yr: the radio model's shock (ruling R-P2-25).</summary>
    public const string RadioShockLabel = "Forward shock (radio model's shock)";

    /// <summary>The forward-shock readout's label while the two eras blend, 3 to 10 yr (rulings R-P2-25, R-P2-26).</summary>
    public const string BlendShockLabel = "Forward shock (join between the wind and remnant models; schematic)";

    /// <summary>From 10 yr (ruling R-P2-24): Project 1's uniform-ejecta value, which the panels use ({0}: the radius).</summary>
    public const string UniformRemnantNoteFormat =
        "The remnant model behind the panels assumes uniform ejecta and gives {0}; see scientific inconsistency #33";

    /// <summary>[APPROX, schematic] The temperature whose blackbody colour tints the shocked debris: X-ray-hot gas, about 10⁷ K.</summary>
    public const double HotGasTemperatureK = 1e7;

    /// <summary>The shocked debris' tint: <see cref="BlackbodyColor"/> at <see cref="HotGasTemperatureK"/> (normalized, so capped at 1).</summary>
    public static (float R, float G, float B) HotGasColor { get; } = BlackbodyColor.FromTemperature(HotGasTemperatureK);

    /// <summary>Task 16 re-review N1: the 3 to 10 yr hand-off from the uniform ball to the remnant model's blast wave.</summary>
    public const string BlendNote =
        "Radius: a smooth blend (3 to 10 years) from the uniform ball of debris to the remnant model's blast wave";

    private static readonly LightCurveModel LightCurve = new();
    private static readonly PulsarModel Pulsar = new();
    private static readonly GrbModel Grb = new();
    private static readonly ShockBreakoutModel Breakout = new();

    private readonly double _z1;
    private readonly bool _nebula;
    private readonly bool _ejecta, _thermonuclear, _lowLuminosityGrb, _pinnedGrb;
    private readonly CircumstellarStructure? _surroundings;
    private readonly object _particleLock = new();
    private ParticleSet? _particles;
    private readonly double _particleStartS;
    private readonly double _breakoutS, _peakLuminosity, _lightCurveEndS, _whiteDwarfS, _pulsarS, _jetsFromS, _jetsToS;

    /// <summary>The scene for this outcome; <paramref name="timeDilation"/> (1 + z) puts the notes' times in observer time.</summary>
    public ExplosionScene(Outcome outcome, double timeDilation = 1)
    {
        Outcome = outcome ?? throw new ArgumentNullException(nameof(outcome));
        _z1 = timeDilation > 0 ? timeDilation : 1;
        _ejecta = EjectaKinematics.HasEjecta(outcome);
        _thermonuclear = outcome.Type is SupernovaType.Ia or SupernovaType.Ia91T or SupernovaType.Ia91bg or SupernovaType.Iax;
        _breakoutS = EjectaKinematics.BreakoutTimeS(outcome);
        _pulsarS = Pulsar.Applies(outcome) ? Pulsar.EmergenceTimeS(outcome) : double.NaN;
        _surroundings = CircumstellarStructureModel.For(outcome);
        _nebula = PulsarWindNebulaModel.Applies(outcome);

        // Review I3: the jets are drawn while the GRB model covers the burst: burst and afterglow for a classical burst;
        // for a pinned low-luminosity burst (ruling R39) the prompt burst only, since the model draws no afterglow for it.
        _jetsFromS = _jetsToS = double.NaN;
        if (Grb.Applies(outcome))
        {
            _lowLuminosityGrb = Grb.IsLowLuminosity(outcome);
            _pinnedGrb = Grb.IsPinned(outcome);
            var (from, to) = Grb.ValidRange(outcome);
            _jetsFromS = from;
            _jetsToS = _lowLuminosityGrb ? Grb.PromptEndS(outcome) : to;
        }

        var (start, end) = LightCurve.ValidRange(outcome);
        _lightCurveEndS = end;
        _peakLuminosity = 0;
        if (end > start && start > 0)
            for (int i = 0; i <= 200; i++)
            {
                double l = Luminosity(start * Math.Pow(end / start, i / 200.0));
                if (l > _peakLuminosity) _peakLuminosity = l;
            }

        // A Type Ia's white dwarf is drawn while the debris are within about its own radius of it.
        double v = EjectaKinematics.OuterVelocityCmS(outcome);
        _whiteDwarfS = _thermonuclear && outcome.Star is { } wd && v > 0 ? wd.RadiusCm.Value / v : 0;

        // The layered ejecta (Project 2): built once for the outcome; the particles are sampled on first use.
        Structure = EjectaStructureModel.For(outcome);
        _particleStartS = Structure is null ? 0
            : Structure.EnvelopeShellLaunchS ?? (double.IsFinite(_breakoutS) ? _breakoutS : 0);
    }

    public Outcome Outcome { get; }

    /// <summary>The ejecta's layers, velocity profile and mixing (null without ejecta); a failed supernova gives its envelope shell.</summary>
    public EjectaStructure? Structure { get; }

    /// <summary>The particles at the default <see cref="QualityLevel"/> (see <see cref="ParticlesFor"/>); null without ejecta.</summary>
    public ParticleSet? Particles => ParticlesFor((int)QualityLevels.Default);

    /// <summary>How many sets have been sampled (a test hook: <see cref="At"/> must never sample).</summary>
    public int ParticleBuildCount { get; private set; }

    /// <summary>
    /// Builds (or returns) the particles at this count (seed 1987). The last count asked for is kept, so a set is built once
    /// per outcome and again only when the count (the quality level) changes. The 3D view calls this when the outcome or
    /// the quality changes, never per frame; <see cref="At"/> only reads the set built last. Null without ejecta.
    /// </summary>
    public ParticleSet? ParticlesFor(int count)
    {
        if (Structure is null) return null;
        lock (_particleLock)
        {
            if (_particles is null || _particles.Count != count)
            {
                _particles = ParticleSampler.Sample(Structure, count, ParticleSampler.DefaultSeed);
                ParticleBuildCount++;
            }
            return _particles;
        }
    }

    /// <summary>
    /// The particle fields of the state at source time t, and the framing radius they imply (ruling P1: the larger of
    /// Project 1's frame radius and the radius enclosing 99% of the particle mass). Not shown before the particles start
    /// (breakout, or a shell's launch).
    /// </summary>
    private SceneState WithParticles(SceneState state, double t, double starRadiusCm)
    {
        if (Structure is null || t < _particleStartS) return state;
        // v_max is the built set's (the one the shader scales by); before any set is built, the profile's own v_max.
        double since = t - _particleStartS, vmax = _particles?.MaxCmS ?? Structure.MaxCmS;
        // The radii as drawn: confined behind the reverse shock once there are shocks (ruling P4; a monotonic map, so the
        // 99%-of-mass particle is still the one at Frame99CmS).
        double max = ParticleKinematics.RadiusCm(vmax, since, starRadiusCm, vmax, state.Shocks);
        double frame99 = double.IsFinite(Structure.Frame99CmS)
            ? ParticleKinematics.RadiusCm(Structure.Frame99CmS, since, starRadiusCm, vmax, state.Shocks) : double.NaN;
        double frame = double.IsFinite(frame99) && !(frame99 <= state.FrameRadiusCm) ? frame99 : state.FrameRadiusCm;
        return state with
        {
            ShowParticles = true, SinceBreakoutS = since, MaxRadiusCm = max, Frame99RadiusCm = frame99, FrameRadiusCm = frame,
        };
    }

    /// <summary>The scene notes the structure carries (layer scaling, relativistic, shell label) and the mixing note.</summary>
    private void AddStructureNotes(List<string> notes, bool beforeLast)
    {
        int at = beforeLast ? Math.Max(0, notes.Count - 1) : notes.Count;
        foreach (string n in Structure!.Notes) if (!notes.Contains(n)) notes.Insert(at++, n);
        if (Structure.Mixing.HasFingers && !Structure.IsEnvelopeShell) notes.Insert(at, MixingNote);
    }

    /// <summary>The view at this source time (s after bounce, or ignition for a Type Ia).</summary>
    public SceneState At(double sourceTimeS)
    {
        double t = sourceTimeS;
        var notes = new List<string>();
        var star = Outcome.Star;
        double r0 = star?.RadiusCm.Value ?? double.NaN;
        var starColor = BlackbodyColor.FromTemperature(star?.SurfaceTemperatureK.Value ?? double.NaN);
        var state = new SceneState
        {
            SourceTimeS = t, StarRadiusCm = r0, StarColor = starColor, FrameRadiusCm = r0, RadiusCm = r0,
            RadiusLabel = "Star radius",
        };

        if (!_ejecta) return WithSurroundings(WithoutEjecta(state, t, notes), notes, t, double.NaN);

        bool jets = !double.IsNaN(_jetsFromS) && t >= _jetsFromS && t <= _jetsToS;
        AddJetNote(t, jets, notes);
        state = state with { ShowJets = jets, JetHalfAngleRad = GrbModel.JetHalfAngleRad };

        if (!_thermonuclear && t < _breakoutS)
        {
            notes.Insert(0, $"The shock is crossing the star: it reaches the surface {UnitFormat.Time(_breakoutS * _z1)} after core bounce");
            notes.Add(SchematicNote);
            return WithSurroundings(state with
            {
                ShowStar = true, Notes = notes,
                StarLayers = StarInterior.Layers(Outcome), ShockInStarRadiusCm = StarInterior.ShockRadiusCm(Outcome, t),
            }, notes, t, double.NaN);
        }

        double r = EjectaKinematics.RadiusCm(Outcome, t);
        double temperature = EjectaTemperatureK(t);
        var marker = MarkerAt(t, notes, out double period);
        var shocks = ShockStructureModel.At(Outcome, t);
        // Ruling R-P2-25: the readout's first line is the debris radius as Project 1 gives it; while shocks are drawn a second
        // line gives the drawn forward shock, labelled by era. From 10 yr a note gives Project 1's uniform-ejecta value.
        bool remnant = t >= RemnantEvolutionModel.StartS && shocks is not null;
        string label = t >= RemnantEvolutionModel.StartS ? "Blast-wave radius"
            : t > EjectaKinematics.BlendStartS ? "Outer radius" : "Ejecta radius";
        string shockLabel = shocks is null ? ""
            : !ShockStructureModel.HasWindEra(Outcome) ? ForwardShockLabel
            : t >= RemnantEvolutionModel.StartS ? (HeldJoinMaximum(shocks!.ForwardCm, t) ? BlendShockLabel : ForwardShockLabel)
            : t > EjectaKinematics.BlendStartS ? BlendShockLabel : RadioShockLabel;
        if (remnant) notes.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, UniformRemnantNoteFormat, UnitFormat.Distance(r)));
        else if (t >= RemnantEvolutionModel.StartS) notes.Add("Radius from the remnant model's blast wave");
        else if (t > EjectaKinematics.BlendStartS) notes.Add(BlendNote);
        else notes.Add(UniformSphereNote);
        if (t > _lightCurveEndS) notes.Add("Past the light-curve model (3 yr): debris drawn dimly");
        if (shocks is not null) notes.Add(ShockNote);
        notes.Add(SchematicNote);
        state = state with
        {
            ShowEjecta = true, EjectaRadiusCm = r, FrameRadiusCm = r, RadiusCm = r, RadiusLabel = label,
            ForwardShockCm = shocks?.ForwardCm ?? double.NaN, ForwardShockLabel = shockLabel,
            EjectaTemperatureK = temperature, EjectaColor = BlackbodyColor.FromTemperature(temperature),
            EjectaBrightness = Brightness(t), Marker = marker, PulsarPeriodS = period, Notes = notes, Shocks = shocks,
        };
        state = WithParticles(state, t, double.IsNaN(r0) ? 0 : r0);
        if (state.ShowParticles) AddStructureNotes(notes, beforeLast: true);
        state = WithShockFrame(state, t);
        state = WithNebula(state, notes, t);
        state = WithSurroundings(state, notes, t, r);
        return WithRings(state, notes, t);
    }

    /// <summary>The pulsar wind nebula (structure §S9): its radius and, once it is more than a speck, its note. Inside the debris, so it never drives the framing.</summary>
    private SceneState WithNebula(SceneState state, List<string> notes, double t)
    {
        if (!_nebula) return state;
        double radius = PulsarWindNebulaModel.RadiusCm(Outcome, t);
        if (!(radius > 0)) return state;
        if (radius >= NebulaNoteMinFrameFraction * state.FrameRadiusCm)
            notes.Insert(Math.Max(0, notes.Count - 1), PulsarWindNebulaModel.Note);
        return state with { NebulaRadiusCm = radius };
    }

    /// <summary>
    /// SN 1987A's rings (structure §S8): drawn while the debris are, with the hotspots' brightness. The rings are far outside the debris
    /// for years, so they are never part of the framing; the note appears once the view can reach them.
    /// </summary>
    private SceneState WithRings(SceneState state, List<string> notes, double t)
    {
        if (!Outcome.HasEquatorialRing) return state;
        if (state.FrameRadiusCm >= RingsNoteMinFrameFraction * Sn1987aRings.EquatorialRadiusCm)
            notes.Insert(Math.Max(0, notes.Count - 1), Sn1987aRings.Note);
        return state with { ShowRings1987A = true, RingBrightness = Sn1987aRings.HotspotBrightness(t) };
    }

    /// <summary>
    /// Ruling R-P2-25: the frame blends geometrically from the debris frame F_d (Project 1's radius or the 99%-mass particle
    /// radius) to max(F_d, R_f) over 3 to 10 yr, with the shocks' smoothstep in log t, so it includes the forward shock from 10 yr
    /// and never jumps. Before 3 yr the wind-era shell may run past the view.
    /// </summary>
    private static SceneState WithShockFrame(SceneState state, double t)
    {
        if (state.Shocks is not { } shocks || !(state.FrameRadiusCm > 0) || !(shocks.ForwardCm > state.FrameRadiusCm)) return state;
        double s = ShockStructureModel.BlendWeight(t);
        if (!(s > 0)) return state;
        double debris = state.FrameRadiusCm;
        return state with { FrameRadiusCm = Math.Exp((1 - s) * Math.Log(debris) + s * Math.Log(shocks.ForwardCm)) };
    }

    /// <summary>
    /// From 10 yr R_f is the running maximum of the blended radius (ruling R-P2-26): where the join with the wind era overshot the
    /// remnant model's forward shock, R_f is held at that maximum until the class-n Truelove &amp; McKee solution (the steep-ejecta
    /// solution) reaches it. True while the drawn R_f is above that solution (by more than the model's 1e-4 interpolation error).
    /// </summary>
    private bool HeldJoinMaximum(double forwardCm, double t)
    {
        var o = Outcome;
        var (rch, tch) = ShockStructureModel.CharacteristicScales(o.ExplosionEnergyErg!.Value, o.EjectaMass!.Value,
            RemnantEvolutionModel.AmbientDensity);
        int n = ShockStructureModel.RemnantIndex(EjectaStructureModel.ClassOf(o));
        double tm99 = ShockStructureModel.TmForward(n, t / tch) * rch;
        return forwardCm > tm99 * (1 + HeldJoinTolerance);
    }

    /// <summary>The relative amount by which R_f must exceed the steep-ejecta solution to count as held (the running maximum's interpolation error is ~1e-5).</summary>
    private const double HeldJoinTolerance = 1e-4;

    /// <summary>
    /// The gas around the star (structure §S7): its note, <see cref="SceneState.Surroundings"/> and the shock's glow. The haze is not
    /// part of the framing; a dense shell is, while the ejecta (of this radius) are inside it.
    /// </summary>
    private SceneState WithSurroundings(SceneState state, List<string> notes, double t, double ejectaRadiusCm)
    {
        if (_surroundings is not { } csm) return state;
        double forward = state.Shocks?.ForwardCm ?? 0;
        // The shell is framed while the debris are inside it; it is drawn (and noted) until the forward shock has swept it.
        bool framed = csm.ShellOuterCm is { } outer && !(ejectaRadiusCm >= outer);
        bool shell = framed && !(forward >= csm.ShellOuterCm!.Value);
        bool haze = csm.WindAStar > 0 && csm.WindOuterCm > csm.WindInnerCm;
        float glow = CircumstellarStructureModel.Glow(Outcome, t);
        state = state with { Surroundings = csm, SurroundingsGlow = glow, ShellFlare = ConfinedFlare(t) ?? glow };
        // Notes: one while a shell is drawn; the haze's only while the view is close enough to see it (frame at most 100 R*).
        string? note = null;
        double frame = double.IsNaN(state.FrameRadiusCm) ? 0 : state.FrameRadiusCm;
        if (shell)
            note = Outcome.ConfinedCsmOuterCm is > 0 ? ConfinedGasNote
                : csm.ShellConfidence == Confidence.Speculative ? GasNote
                : string.Format(System.Globalization.CultureInfo.InvariantCulture, GasNoteFormat, csm.Source.Citation);
        else if (haze && frame <= HazeNoteMaxFrameRadii * csm.WindInnerCm)
            note = string.Format(System.Globalization.CultureInfo.InvariantCulture, GasNoteFormat, CircumstellarStructureModel.WindSourceCitation);
        if (note is not null) notes.Insert(Math.Max(0, notes.Count - 1), note);
        if (!framed) return state with { Notes = notes };
        return state with
        {
            FrameRadiusCm = Math.Max(csm.ShellOuterCm!.Value, double.IsNaN(state.FrameRadiusCm) ? 0 : state.FrameRadiusCm),
            Notes = notes,
        };
    }

    /// <summary>
    /// Ruling R-P2-28's schematic flare of SN 2023ixf's pinned confined gas: lit from the debris' start for as long as the shell is
    /// drawn (it is removed once the forward shock has swept it); null when the outcome has no pinned gas.
    /// </summary>
    private float? ConfinedFlare(double t)
        => Outcome.ConfinedCsmOuterCm is > 0 && Structure is not null ? (t >= _particleStartS ? 1f : 0f) : null;

    /// <summary>The jets' note while they are drawn, and after a low-luminosity burst's jets stop, why they stopped.</summary>
    private void AddJetNote(double t, bool shown, List<string> notes)
    {
        if (double.IsNaN(_jetsFromS) || t < _jetsFromS) return;
        string until = UnitFormat.Time(_jetsToS * _z1);
        if (shown)
        {
            string geometry = _pinnedGrb
                ? "jet geometry schematic"
                : $"schematic, {GrbModel.JetHalfAngleRad * 180 / Math.PI:0}° half-angle";
            notes.Add(_lowLuminosityGrb
                ? $"Gamma-ray-burst jets ({geometry}), drawn only during the burst itself, to {until} after bounce: a " +
                  "low-luminosity burst, whose slower outflow the lab does not model"
                : $"Gamma-ray-burst jets ({geometry}), drawn while the lab models the burst and its afterglow, to {until} " +
                  "after bounce");
            return;
        }
        notes.Add(_lowLuminosityGrb
            ? $"The gamma-ray burst's jets are no longer drawn: the lab models only the burst itself (to {until} after " +
              "bounce) for this low-luminosity event"
            : $"The gamma-ray burst's jets are no longer drawn: the lab's afterglow model ends {until} after bounce");
    }

    /// <summary>A star that does not explode: still alive, a white dwarf, or a collapse to a black hole.</summary>
    private SceneState WithoutEjecta(SceneState state, double t, List<string> notes)
    {
        var o = Outcome;
        switch (o.Fate)
        {
            case Fate.NotDeadYet:
                notes.Add("The star has not died yet");
                state = state with { ShowStar = true };
                break;
            case Fate.WhiteDwarf:
                notes.Add("A white dwarf (marker), with no supernova");
                state = state with { Marker = SceneMarker.WhiteDwarf, RadiusLabel = "White dwarf radius" };
                break;
            case Fate.FailedSupernova or Fate.PulsationalPairInstability when o.Remnant.Kind == RemnantKind.BlackHole:
                double tbh = o.Remnant.FormationTimeS?.Value ?? 0;
                if (t < tbh)
                {
                    notes.Add($"The core is collapsing: a black hole forms {UnitFormat.Time(tbh * _z1)} after core bounce");
                    state = state with { ShowStar = true };
                    break;
                }
                bool shell = o.Fate == Fate.FailedSupernova && Structure is { IsEnvelopeShell: true } && t >= _particleStartS;
                notes.Add(o.Fate == Fate.FailedSupernova
                    ? shell ? "No explosion: the core collapsed to a black hole; only the star's weak outer envelope is thrown off"
                        : "No explosion: the core collapsed to a black hole, and the star is simply removed here"
                    : "The core collapsed to a black hole; the shells thrown off by the pulses are not drawn");
                notes.Add("Black hole (schematic marker)");
                state = state with { Marker = SceneMarker.BlackHole, RadiusLabel = "Former star's radius" };
                if (shell)
                {
                    // Ruling R-P2-19: the shell starts at its launch. It has no light curve: drawn dimly at the floor colour.
                    state = state with
                    {
                        EjectaTemperatureK = EjectaKinematics.TemperatureFloorK,
                        EjectaColor = BlackbodyColor.FromTemperature(EjectaKinematics.TemperatureFloorK),
                        EjectaBrightness = MinBrightness,
                    };
                    state = WithParticles(state, t, double.IsNaN(state.StarRadiusCm) ? 0 : state.StarRadiusCm);
                    AddStructureNotes(notes, beforeLast: false);
                }
                break;
            default:
                state = state with { ShowStar = o.Star is not null };
                break;
        }
        notes.Add(SchematicNote);
        return state with { Notes = notes };
    }

    /// <summary>The remnant's marker after breakout, with its note; the pulsar's model period now (NaN otherwise).</summary>
    private SceneMarker MarkerAt(double t, List<string> notes, out double periodS)
    {
        periodS = double.NaN;
        switch (Outcome.Remnant.Kind)
        {
            case RemnantKind.NeutronStar or RemnantKind.Magnetar:
                string what = Outcome.Remnant.Kind == RemnantKind.Magnetar ? "Magnetar" : "Neutron star";
                if (!double.IsNaN(_pulsarS) && t >= _pulsarS)
                {
                    periodS = Pulsar.Sample(Outcome, ObserverSetup.Default, t, "periodS");
                    notes.Add($"{what}'s beams slowed down (the lab's model period is {UnitFormat.Time(periodS)})");
                    return SceneMarker.Pulsar;
                }
                notes.Add($"{what} (marker)" + (double.IsNaN(_pulsarS) ? "" :
                    $"; its beams are drawn once its radio pulses escape the debris, {UnitFormat.Time(_pulsarS * _z1)} after bounce"));
                return SceneMarker.NeutronStar;
            case RemnantKind.BlackHole:
                double tbh = Outcome.Remnant.FormationTimeS?.Value ?? 0;
                if (t < tbh)
                {
                    notes.Add($"Neutron star (marker); it collapses to a black hole {UnitFormat.Time(tbh * _z1)} after bounce");
                    return SceneMarker.NeutronStar;
                }
                notes.Add("Black hole (schematic marker)");
                return SceneMarker.BlackHole;
            case RemnantKind.WhiteDwarf:
                notes.Add("Surviving white dwarf (marker)");
                return SceneMarker.WhiteDwarf;
            default:
                if (_thermonuclear && t < _whiteDwarfS)
                {
                    notes.Add("The white dwarf (marker) is being blown apart");
                    return SceneMarker.WhiteDwarf;
                }
                return SceneMarker.None;
        }
    }

    /// <summary>
    /// The light curve's colour temperature while it runs (defined from breakout on), else, for an outcome without a light
    /// curve, the Stefan–Boltzmann temperature, else the 3,000 K floor.
    /// </summary>
    private double EjectaTemperatureK(double t)
    {
        double k = t <= _lightCurveEndS ? LightCurve.ColourTemperatureK(Outcome, t) : double.NaN;
        if (!(k > 0)) k = EjectaKinematics.PhotosphereTemperatureK(Outcome, t);
        return k > 0 ? k : EjectaKinematics.TemperatureFloorK;
    }

    /// <summary>1 at the light curve's peak, falling with log L to <see cref="MinBrightness"/> four decades below it.</summary>
    private float Brightness(double t)
    {
        double l = Luminosity(t);
        if (!(l > 0) || !(_peakLuminosity > 0)) return MinBrightness;
        double x = Math.Clamp(1 + Math.Log10(l / _peakLuminosity) / DimmingDecades, 0, 1);
        return (float)(MinBrightness + (1 - MinBrightness) * x);
    }

    /// <summary>The light curve's bolometric luminosity, or the breakout model's before it starts; NaN when neither runs.</summary>
    private double Luminosity(double t)
    {
        double l = LightCurve.Sample(Outcome, ObserverSetup.Default, t, "lbol");
        return l > 0 ? l : Breakout.Sample(Outcome, ObserverSetup.Default, t, "lum");
    }
}
