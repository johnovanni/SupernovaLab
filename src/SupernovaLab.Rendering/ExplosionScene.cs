using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Signals;
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
    public bool ShowGas { get; init; }
    public double GasRadiusCm { get; init; } = double.NaN;
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
}

/// <summary>
/// The simple 3D view's content (spec §6) for one outcome, as a function of source time: no OpenGL here.
/// <list type="bullet">
/// <item>Before breakout: the star, sized to its radius and coloured by its surface temperature.</item>
/// <item>Gas around the star (spec §6), when it is significant: the dense-gas cases <see cref="CsmInteractionModel"/>
/// does not model (<see cref="CsmInteractionModel.HasDenseCsm"/>), or a wind with A_* ≥ <see cref="DenseWindAStar"/>. A
/// translucent shell out to <see cref="GasRadiusCm"/>, until the ejecta pass it; framing includes it.</item>
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

    /// <summary>
    /// [APPROX] A wind counts as significant gas around the star from A_* = 100: ten times the lab's ordinary
    /// red-supergiant wind (Ṁ = 1e-6 M☉/yr at 10 km/s, A_* ≈ 10, signals §0.4). The research gives no threshold.
    /// </summary>
    public const double DenseWindAStar = 100;

    /// <summary>
    /// [APPROX, Speculative] The drawn extent of the gas around the star, cm: SN 2023ixf's dense confined gas "within
    /// ~1e15 cm" (Jacobson-Galán et al. 2023, presets.md, SN 2023ixf [R]). The research gives no extent rule, so every
    /// case uses this one example.
    /// </summary>
    public const double GasRadiusCm = 1e15;

    public const string SchematicNote = "Colours schematic";

    public const string GasNote =
        "Gas around the star (schematic): its extent is not in the research; drawn to 1e15 cm, like SN 2023ixf's";

    /// <summary>Review I4: what the stated ejecta radius is, in plain words.</summary>
    public const string UniformSphereNote =
        "Radius: the edge of a uniform ball of debris with this energy and mass (the spec's simple model); the fastest " +
        "outer layers, and the line speeds in the spectrum, are faster";

    private static readonly LightCurveModel LightCurve = new();
    private static readonly PulsarModel Pulsar = new();
    private static readonly GrbModel Grb = new();
    private static readonly ShockBreakoutModel Breakout = new();

    private readonly double _z1;
    private readonly bool _ejecta, _thermonuclear, _gas, _lowLuminosityGrb, _pinnedGrb;
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
        _gas = outcome.Star is not null
               && (CsmInteractionModel.HasDenseCsm(outcome) || CsmInteractionModel.WindAStar(outcome) >= DenseWindAStar);

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
    }

    public Outcome Outcome { get; }

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

        if (!_ejecta) return WithGas(WithoutEjecta(state, t, notes), notes, double.NaN);

        bool jets = !double.IsNaN(_jetsFromS) && t >= _jetsFromS && t <= _jetsToS;
        AddJetNote(t, jets, notes);
        state = state with { ShowJets = jets, JetHalfAngleRad = GrbModel.JetHalfAngleRad };

        if (!_thermonuclear && t < _breakoutS)
        {
            notes.Insert(0, $"The shock is still inside the star: it reaches the surface {UnitFormat.Time(_breakoutS * _z1)} after core bounce");
            notes.Add(SchematicNote);
            return WithGas(state with { ShowStar = true, Notes = notes }, notes, double.NaN);
        }

        double r = EjectaKinematics.RadiusCm(Outcome, t);
        double temperature = EjectaTemperatureK(t);
        var marker = MarkerAt(t, notes, out double period);
        string label = t >= RemnantEvolutionModel.StartS ? "Blast-wave radius"
            : t > EjectaKinematics.BlendStartS ? "Outer radius" : "Ejecta radius";
        if (t >= RemnantEvolutionModel.StartS) notes.Add("Radius from the remnant model's blast wave");
        else notes.Add(UniformSphereNote);
        if (t > _lightCurveEndS) notes.Add("Past the light-curve model (3 yr): debris drawn dimly");
        notes.Add(SchematicNote);
        state = state with
        {
            ShowEjecta = true, EjectaRadiusCm = r, FrameRadiusCm = r, RadiusCm = r, RadiusLabel = label,
            EjectaTemperatureK = temperature, EjectaColor = BlackbodyColor.FromTemperature(temperature),
            EjectaBrightness = Brightness(t), Marker = marker, PulsarPeriodS = period, Notes = notes,
        };
        return WithGas(state, notes, r);
    }

    /// <summary>Adds the gas shell (and its note, and framing to include it) until ejecta of this radius pass it.</summary>
    private SceneState WithGas(SceneState state, List<string> notes, double ejectaRadiusCm)
    {
        if (!_gas || ejectaRadiusCm >= GasRadiusCm) return state;
        notes.Insert(Math.Max(0, notes.Count - 1), GasNote);
        return state with
        {
            ShowGas = true, GasRadiusCm = GasRadiusCm,
            FrameRadiusCm = Math.Max(GasRadiusCm, double.IsNaN(state.FrameRadiusCm) ? 0 : state.FrameRadiusCm),
            Notes = notes,
        };
    }

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
                notes.Add(o.Fate == Fate.FailedSupernova
                    ? "No explosion: the core collapsed to a black hole, and the star is simply removed here"
                    : "The core collapsed to a black hole; the shells thrown off by the pulses are not drawn");
                notes.Add("Black hole (schematic marker)");
                state = state with { Marker = SceneMarker.BlackHole, RadiusLabel = "Former star's radius" };
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
