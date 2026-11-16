using SupernovaLab.Core.Compact;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Rendering;

/// <summary>What the Close-up shows at one moment (spec §2–§6). Later tasks fill in the eras before the remnant settles.</summary>
public enum CloseUpEra { None, PreCollapse, FirstSecond, NeutronStar, BlackHoleForming, BlackHoleSettling, BlackHole }

/// <summary>
/// One number in the Close-up's readout column (spec §2.1): its label, its value with confidence and source, and an optional tooltip
/// for the label. <see cref="ValueText"/> is the value as the column shows it. Plan Task 6: a row may carry a second number on the same
/// line, a <see cref="Quantity"/> of its own (the spin's Ṗ, the kick's distance from the explosion's centre, spec §4.5), named by
/// <see cref="SecondLabel"/>; the row's badge is the first number's, and the tooltip names the second's source.
/// </summary>
public sealed record Readout(string Label, Quantity Value, string? ToolTip = null, Quantity? Second = null, string? SecondLabel = null)
{
    /// <summary>The value and its unit ("10 M☉", "59.1 km", a bare number for a unitless value), then the second number if any.</summary>
    public string ValueText => Second is null ? Format(Value)
        : $"{Format(Value)} · {(SecondLabel is null ? "" : SecondLabel + " ")}{Format(Second)}";

    /// <summary>
    /// One quantity as the column shows it: a mass in M☉, a time in its own unit ("33.4 ms", "937 years"), a temperature in MK (its
    /// band when it has one: "1.2–2.9 MK"), a field or a Ṗ in powers of ten, a distance in km, AU or pc, else the number and its unit.
    /// </summary>
    public static string Format(Quantity q) => q.Unit switch
    {
        "M☉" => UnitFormat.Mass(q.Value),
        "" => UnitFormat.Number(q.Value),
        "s" => UnitFormat.Time(q.Value),
        "K" => Kelvin(q),
        "G" or "s/s" or "g/cm³" or "erg/s" => UnitFormat.Scientific(q.Value, q.Unit),
        "%" => $"{UnitFormat.Number(q.Value)}%",
        "cm" => UnitFormat.Distance(q.Value),
        _ => $"{UnitFormat.Number(q.Value)} {q.Unit}",
    };

    private static string Kelvin(Quantity q)
    {
        bool band = q.Low is double lo && q.High is double hi && hi > lo * 1.001;
        double top = band ? q.High!.Value : q.Value;
        string unit = top >= 1e6 ? "MK" : "K";
        double scale = top >= 1e6 ? 1e6 : 1;
        return band
            ? $"{UnitFormat.Number(q.Low!.Value / scale)}–{UnitFormat.Number(q.High!.Value / scale)} {unit}"
            : $"{UnitFormat.Number(q.Value / scale)} {unit}";
    }
}

/// <summary>
/// What the Close-up draws at one moment (the counterpart of <see cref="SceneState"/>). <see cref="FrameRadiusKm"/> is what the
/// Close-up's director frames. <see cref="NoRemnantMessage"/> is set, and nothing is drawn, for an outcome with no compact remnant
/// (spec §2.3). For the lensing pass: <see cref="MassMsun"/> (the compact object's gravitational mass, NaN for none),
/// <see cref="Spin"/> (a, 0 to 1) and <see cref="SurfaceRadiusKm"/> (a neutron star's surface, 0 for a black hole).
/// </summary>
public sealed record CloseUpState
{
    public CloseUpEra Era { get; init; }
    public string Title { get; init; } = "";
    public IReadOnlyList<Readout> Readouts { get; init; } = Array.Empty<Readout>();
    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
    public double FrameRadiusKm { get; init; }
    public string? NoRemnantMessage { get; init; }
    public double MassMsun { get; init; } = double.NaN;
    public double Spin { get; init; }
    public double SurfaceRadiusKm { get; init; }

    // ---- Plan Task 6: the neutron star (spec §4.1, §4.3, §4.6). All NaN, false or null for any other era. ----

    /// <summary>The neutron star now (Task 5's state); null for any other object.</summary>
    public NeutronStarState? NeutronStar { get; init; }

    /// <summary>The period the spin is drawn at, max(real, 2 s) (<see cref="Magnetosphere.DrawnPeriodS"/>), shared with the main view.</summary>
    public double DrawnPeriodS { get; init; } = double.NaN;

    /// <summary>"Shown 60× slower", or "Shown at real speed" (<see cref="Magnetosphere.SpeedLabel"/>); null without a neutron star.</summary>
    public string? SpeedLabel { get; init; }

    /// <summary>The light cylinder's radius, c P / 2π, km.</summary>
    public double LightCylinderKm { get; init; } = double.NaN;

    /// <summary>The surface's colour: the blackbody colour of T_s^∞ (<see cref="BlackbodyColor"/>), normalized sRGB; tone-mapped when drawn.</summary>
    public (float R, float G, float B) SurfaceColor { get; init; } = (1f, 1f, 1f);

    /// <summary>Whether the beams are drawn: from <see cref="Magnetosphere.MagnetosphereFormsS"/> (spec C6).</summary>
    public bool ShowBeams { get; init; }

    /// <summary>α, the magnetic axis' tilt from the spin axis (<see cref="Magnetosphere.TiltRad"/>), as in the main view.</summary>
    public double BeamTiltRad { get; init; } = double.NaN;

    /// <summary>ρ, each beam's half-opening angle now (<see cref="Magnetosphere.BeamHalfAngleRad"/>), as in the main view.</summary>
    public double BeamHalfAngleRad { get; init; } = double.NaN;

    /// <summary>The unit vector to Earth (<see cref="Magnetosphere.EarthDirection"/>): the "To Earth" arrow.</summary>
    public Vec3 EarthDirection { get; init; }

    /// <summary>Whether a beam sweeps across Earth (<see cref="Magnetosphere.BeamCanCrossEarth"/>).</summary>
    public bool BeamCanCrossEarth { get; init; }

    /// <summary>The polar cap's angular radius, asin √(R / R_L): where the open field lines leave the surface.</summary>
    public double PolarCapRad { get; init; } = double.NaN;

    /// <summary>The dipole field, G (the number of field lines drawn grows with it, schematic).</summary>
    public double FieldG { get; init; } = double.NaN;

    /// <summary>
    /// Plan Task 7 (spec §4.2): what the cutaway shows for this star now (its layers, the neutron drip line, the magnified strip, or the
    /// proto-neutron star before the crust forms); null for any other object. Drawn only while the Cutaway switch is on.
    /// </summary>
    public CutawayInfo? Cutaway { get; init; }

    // ---- Plan Task 9: the core before collapse and in its first second (spec §3). NaN or null for any other era. ----

    /// <summary>The core's state now (<see cref="CoreCollapseHistory.At"/>); null outside the PreCollapse and FirstSecond eras.</summary>
    public CoreState? Core { get; init; }

    /// <summary>The drawn sphere, km: the iron (or O-Ne-Mg) core before collapse, then the proto-neutron star (<see cref="CoreCollapseHistory.DrawnProtoNsRadiusKm"/>).</summary>
    public double CoreSphereRadiusKm { get; init; } = double.NaN;

    /// <summary>
    /// Spec §3.2: the proto-neutron star's brightness, Project 2's logarithmic rule (<see cref="ExplosionScene.LogBrightness"/>) of the
    /// neutrino panel's total luminosity now against its peak; NaN outside the first second.
    /// </summary>
    public float ProtoNsBrightness { get; init; } = float.NaN;

    /// <summary>The drawn sphere's colour (schematic), normalized sRGB.</summary>
    public (float R, float G, float B) CoreColor { get; init; } = (1f, 1f, 1f);

    /// <summary>The stage's label on the view (spec §3.2: "From simulations"); null outside the first second.</summary>
    public string? StageLabel { get; init; }

    /// <summary>The stage's key on the view, one line each (what each drawn thing is); empty outside the core's eras.</summary>
    public IReadOnlyList<string> StageKey { get; init; } = Array.Empty<string>();

    /// <summary>Whether the core is drawn (the PreCollapse or FirstSecond era): unlensed surfaces (plan Task 9, as ruling R-P3-19).</summary>
    public bool IsCoreStage => Core is not null;

    /// <summary>Whether the view draws anything (its rings and the object): every era but None.</summary>
    public bool DrawsScene => Era != CloseUpEra.None;

    /// <summary>GM/c² in km for <see cref="MassMsun"/>; NaN without a compact object.</summary>
    public double GravitationalRadiusKm => MassMsun > 0 ? BlackHoleOptics.GravitationalRadiusCm(MassMsun) / 1e5 : double.NaN;

    /// <summary>Whether the lensing pass draws this state (a compact object with a mass).</summary>
    public bool IsLensed => Era != CloseUpEra.None && MassMsun > 0;
}

/// <summary>
/// The single source of what the Close-up draws at each moment (spec §8.2; the counterpart of <see cref="ExplosionScene"/>): a pure
/// function of the outcome, the observer, the run number and the source time. In this build it gives the no-remnant messages
/// (spec §2.3), a placeholder black hole (its mass, and spin 0 unless the bench's diagnostic spin is given) and (plan Task 6) the neutron
/// star at each time (spec §4: its readouts, notes, surface colour, beams, field, light cylinder and Earth's direction). Plan Task 9:
/// for a core collapse, the core before collapse up to 1 ms (the PreCollapse era) and its first second until
/// <see cref="CoreCollapseHistory.StageEndS"/> (the FirstSecond era: spec §3), then the remnant (from 1 s to 1 minute a neutron star
/// still contracts to its radius, compact §C2); the black hole's forming and settling eras come later (Task 10).
/// </summary>
public sealed class CloseUpScene
{
    /// <summary>The view's label (plan's exact strings): the close-up is where no telescope could be (spec §6.5).</summary>
    public const string NoTelescopeNote = "Camera where no telescope could be";

    /// <summary>Spec §2.3: a Type Ia or a pair-instability supernova.</summary>
    public const string DestroyedMessage = "No compact remnant: the star was destroyed completely.";

    /// <summary>Spec §2.3: no supernova, or a white dwarf (a Type Iax's bound remnant included).</summary>
    public const string WhiteDwarfMessage = "This star leaves a white dwarf. The white-dwarf close-up is not part of the lab.";

    /// <summary>The black hole's note while only its mass is drawn.</summary>
    public const string PlaceholderBlackHoleNote =
        "The black hole is drawn with its mass only: its formation, settling and spin are not shown yet.";

    /// <summary>The bench's note when it gives the placeholder black hole a spin.</summary>
    public const string BenchSpinNote = "Spin set by the --bench-spin diagnostic, not by the lab's model.";

    /// <summary>The neutron star's note while only its size is drawn.</summary>
    public const string PlaceholderNeutronStarNote =
        "The neutron star is drawn as a plain sphere of its radius (placeholder colour), with the bending of light around it.";

    /// <summary>What the lensing pass bends, said once.</summary>
    public const string SurroundingsNote =
        "The surroundings are the main view's scene seen from the remnant, with their light bent as computed from general relativity (each pixel's ray traced through the hole's spacetime).";

    /// <summary>The black hole is framed at this many shadow radii (the shadow fills about a third of the view's height).</summary>
    public const double BlackHoleFrameShadowRadii = 1.5;

    /// <summary>
    /// The neutron star is framed at this many radii. Plan Task 6: 2.5 (Task 4 had 4), so the surface and its markings read; its first
    /// launch showed the star only about 30 px across at 4.
    /// </summary>
    public const double NeutronStarFrameRadii = 2.5;

    private static readonly Source ShadowSource = new(
        "Exact (general relativity): horizon r₊ = GM/c² (1 + √(1 − a²)); shadow radius √27 GM/c² for no spin (Bardeen 1973 for spin)",
        "compact §C7");

    // ---- Plan Task 6: the neutron star's notes (spec §4.1, §4.3, C2, C6, C11, C12) ----

    /// <summary>Spec C6, the plan's exact words: why the close-up shows beams the main view does not draw yet.</summary>
    public const string BeamsFromBirthNote = "Beams exist from birth; the main view draws them once the pulses escape the debris";

    /// <summary>Before the magnetosphere forms (compact §C4.2): no beams yet.</summary>
    public const string BeamsNotYetNote =
        "The beams are drawn once the star's magnetosphere forms, about 30 s after bounce (approximate: 10 to 100 s, compact §C4.2)";

    /// <summary>Spec §4.1: the colour and where the star really shines.</summary>
    public const string SurfaceColourNote =
        "Surface colour: the blackbody colour of its temperature seen from afar. It shines mostly in X-rays; its visible light is only the faint tail of that blackbody";

    /// <summary>Spec C12.</summary>
    public const string ToneMappedNote =
        "Brightness tone-mapped so the surface reads: not how bright the star would look from any real distance";

    /// <summary>Spec §4.1 and C11.</summary>
    public const string SurfaceBendingNote =
        "Light from the surface follows general relativity's paths, as round the black hole, so part of the far side shows past the limb (exact for a non-rotating star; its own spin is neglected in the bending)";

    /// <summary>The surface's markings (schematic).</summary>
    public const string SurfaceMarkingsNote =
        "The surface's grid lines and the polar-cap rings are schematic: they turn with the star to show its spin and the bending. The polar caps are where the open field lines leave";

    /// <summary>Spec §4.3: the beam width's published relation.</summary>
    public const string BeamWidthNote =
        "Beam half-width from the published relation with the spin period, 5.6° P^(-1/2) (Biggs 1990; fits 4.9° to 6.3°), in both views";

    /// <summary>Spec §4.3: the tilt, when no preset publishes one.</summary>
    public const string SchematicTiltNote = "Magnetic tilt: schematic 45° (usually unknown), the same in both views";

    /// <summary>Spec §4.3: the field lines.</summary>
    public const string FieldLinesNote =
        "Field lines: a dipole, the accepted shape near the star. How many are drawn is schematic (more for a stronger field); lines that reach the light cylinder open out and are drawn to it";

    /// <summary>What the overlays leave out.</summary>
    public const string OverlaysUnbentNote =
        "The field lines, beams, light cylinder and Earth arrow are drawn without the bending of light";

    /// <summary>Spec §4.6: a kick direction with no measurement.</summary>
    public const string SeededKickNote =
        "Kick: direction drawn from the run number (display randomness, not physics); it changes with Re-roll";

    private static readonly Source AgeSource = new("The timeline's time since core bounce, in the source's frame", "spec §4.5");
    private static readonly Source PdotSource = new(
        "Ṗ = P0 / (2 τ0 √(1 + t/τ0)): the lab's vacuum-dipole spin-down (n = 3; Lorimer & Kramer 2005)", "signals §8.1");
    private static readonly Source DriftSource = new("Distance from the explosion's centre: the kick speed × the age", "spec §4.6; compact §C4");

    private readonly Outcome _outcome;
    private readonly double _spin;
    private readonly bool _isNeutronStar;

    /// <param name="placeholderSpin">The placeholder black hole's spin (0 unless the bench's <c>--bench-spin</c> sets it).</param>
    public CloseUpScene(Outcome outcome, ObserverSetup observer, int runNumber, double placeholderSpin = 0)
    {
        _outcome = outcome ?? throw new ArgumentNullException(nameof(outcome));
        ArgumentNullException.ThrowIfNull(observer);
        Observer = observer;
        RunNumber = runNumber;
        _spin = Math.Clamp(double.IsFinite(placeholderSpin) ? placeholderSpin : 0, 0, 0.998);
        _isNeutronStar = NeutronStarModel.At(outcome, observer, runNumber, 0) is not null;
        _track = CoreCollapseHistory.TrackFor(outcome);
        _stageEndS = CoreCollapseHistory.StageEndS(outcome);
        _neutrinoPeak = _track is null ? double.NaN : CoreCollapseHistory.PeakNeutrinoLuminosityErgS(outcome);
    }

    private readonly CoreTrack? _track;
    private readonly double _stageEndS, _neutrinoPeak;
    private CloseUpState? _preCollapse;

    public ObserverSetup Observer { get; }
    public int RunNumber { get; }

    /// <summary>
    /// The Close-up at this source time (s after bounce). A neutron star changes with the time (its spin, temperature, age and drift,
    /// plan Task 6); every other state in this build is the same at all times, so it is made once (the spinning shadow's size
    /// integrates the outline).
    /// </summary>
    public CloseUpState At(double sourceTimeS)
    {
        if (_track is not null)
        {
            // Plan ruling P3-12, spec §3: the core before collapse up to the scrubber's left end (1 ms), then its first second.
            if (!(sourceTimeS > PreCollapseEndS)) return _preCollapse ??= PreCollapse();
            if (sourceTimeS < _stageEndS) return FirstSecondAt(sourceTimeS);
        }
        return _isNeutronStar ? NeutronStarAt(sourceTimeS) : _settled ??= Settled();
    }

    private CloseUpState? _settled;

    // Plan Task 7: the cutaway's two forms (a star's layers do not change; only whether the crust has formed), made once each.
    private CutawayInfo? _cutawayWithCrust, _cutawayProto;

    private CloseUpState Settled()
    {
        var remnant = _outcome.Remnant;
        return remnant.Kind switch
        {
            RemnantKind.BlackHole when remnant.Mass is { Value: > 0 } => BlackHole(remnant.Mass),
            RemnantKind.NeutronStar or RemnantKind.Magnetar when remnant.Mass is { Value: > 0 } && remnant.RadiusKm is { Value: > 0 }
                => NeutronStar(remnant),
            RemnantKind.WhiteDwarf => NoRemnant(WhiteDwarfMessage, "White dwarf"),
            _ => _outcome.Fate is Fate.NotDeadYet or Fate.WhiteDwarf
                ? NoRemnant(WhiteDwarfMessage, "White dwarf")
                : NoRemnant(DestroyedMessage, "No compact remnant"),
        };
    }

    private static CloseUpState NoRemnant(string message, string title)
        => new() { Era = CloseUpEra.None, Title = title, NoRemnantMessage = message, FrameRadiusKm = 1 };

    private CloseUpState BlackHole(Quantity mass)
    {
        double rgKm = BlackHoleOptics.GravitationalRadiusCm(mass.Value) / 1e5;
        double horizonKm = 2 * BlackHoleOptics.HorizonRadius(_spin) * rgKm;
        // The shadow's mean diameter (exactly 2√27 GM/c² without spin), in km: the apparent size at 1 km.
        double shadowKm = BlackHoleOptics.ApparentShadowDiameterRad(mass.Value, 1e5, _spin);
        var readouts = new List<Readout>
        {
            new("Mass", mass),
            new("Horizon diameter", new Quantity(horizonKm, "km", mass.Confidence, ShadowSource),
                "Twice the event horizon's radius. The horizon itself is never visible."),
            new("Shadow diameter", new Quantity(shadowKm, "km", mass.Confidence, ShadowSource),
                "The dark region where light falls in: about 2.6 times the horizon's size for a non-spinning hole."),
        };
        var notes = new List<string> { SurroundingsNote, PlaceholderBlackHoleNote };
        if (_spin > 0)
        {
            readouts.Add(new("Spin", new Quantity(_spin, "", Confidence.Speculative,
                new Source("Diagnostic (--bench-spin)", "plan Task 4"))));
            notes.Add(BenchSpinNote);
        }
        return new CloseUpState
        {
            Era = CloseUpEra.BlackHole,
            Title = "Black hole",
            Readouts = readouts,
            Notes = notes,
            FrameRadiusKm = BlackHoleFrameShadowRadii * shadowKm / 2,
            MassMsun = mass.Value,
            Spin = _spin,
        };
    }

    /// <summary>
    /// Plan Task 6: the neutron star at this time (spec §4): its title, the readouts of spec §4.5 (each a <see cref="Quantity"/> with
    /// its confidence and source), the notes, and what the view draws (surface colour, beams, field, light cylinder, Earth's
    /// direction). A time before bounce shows the star at bounce (the eras before it come with Task 9).
    /// </summary>
    private CloseUpState NeutronStarAt(double sourceTimeS)
    {
        double t = sourceTimeS > 0 ? sourceTimeS : 0;
        var o = _outcome;
        var r = o.Remnant;
        var ns = NeutronStarModel.At(o, Observer, RunNumber, t)!;
        // Plan Task 9 (spec §3.1 item 3, compact §C2): until 1 minute the proto-neutron star is still contracting to the remnant's radius.
        bool contracting = t < CoreCollapseHistory.ContractionEndS;
        double period = ns.PeriodS, radius = contracting ? CoreCollapseHistory.ContractionRadiusKm(ns.RadiusKm, t) : ns.RadiusKm;
        double lightCylinder = Magnetosphere.LightCylinderKm(period);
        bool beams = t >= Magnetosphere.MagnetosphereFormsS(o);
        string periodText = UnitFormat.Time(period);
        // Ruling R-P3-17 (#60): a preset whose star has never shown pulses is titled a pulsar of the lab's model only.
        bool noPulsesSeen = o.Discloses(NoPulsesSeenInconsistency);
        string title = ns.IsMagnetar
            ? beams ? $"Magnetar ({periodText})" : $"Proto-neutron star (magnetar, {periodText})"
            : beams ? (noPulsesSeen ? $"Neutron star (a pulsar in the lab's model, {periodText})" : $"Neutron star (pulsar, {periodText})")
            : $"Proto-neutron star ({periodText})";

        var spinConfidence = Worse(r.SpinPeriodS!.Confidence, Confidence.ModelDependent);
        var spin = new Quantity(period, "s", spinConfidence, new Source(
            $"Birth period: {r.SpinPeriodS.Source.Citation}; spun down since by the lab's vacuum-dipole law (n = 3; Lorimer & Kramer 2005)",
            "signals §8.1"));
        var pdot = new Quantity(ns.PeriodDerivative, "s/s", spinConfidence, PdotSource);
        var kick = r.KickKmS ?? new Quantity(0, "km/s", Confidence.Speculative, new Source("The outcome gives no kick", "spec §4.6"));
        var drift = new Quantity(ns.OffsetCm.Length, "cm", kick.Confidence, DriftSource);
        bool measuredKick = o.KickPositionAngleDeg is not null;
        string kickTip = (measuredKick ? KickDrift.LineOfSightNote : SeededKickNote) +
                         ". The second number is the distance from the explosion's centre (" + DriftSource.Citation.ToLowerInvariant() + ")." +
                         (o.Discloses(KickSpeedInconsistency) ? " " + CasAKickComparison(kick.Value) : "");
        string surfaceTip = NeutronStarModel.CasAComparison(o, t)
                            ?? "Seen from afar (redshifted): the lab's cooling model; before 30 years an early-cooling band (compact §C3).";
        var readouts = new[]
        {
            new Readout("Mass", r.Mass!, "Gravitational mass"),
            contracting
                ? new Readout("Radius", new Quantity(radius, "km", Confidence.ModelDependent, CoreCollapseHistory.ContractionSource),
                    $"Still contracting: the proto-neutron star shrinks to the remnant's {UnitFormat.Number(ns.RadiusKm)} km by about one minute after bounce (approximate)")
                : new Readout("Radius", r.RadiusKm!),
            new Readout("Spin", spin,
                $"The spin period now, and Ṗ, how fast it lengthens ({PdotSource.Citation}). The light cylinder, c P / 2π, grows as the star slows.",
                pdot, "Ṗ"),
            new Readout("Field", r.MagneticFieldG!, "The surface dipole field"),
            new Readout("Surface", ns.SurfaceTemperatureK, surfaceTip),
            new Readout("Age", new Quantity(t, "s", Confidence.Established, AgeSource), "Time since core bounce"),
            new Readout("Kick", kick, kickTip, drift),
        };

        var notes = new List<string>();
        if (noPulsesSeen) notes.Add(NoPulsesSeenNote);
        notes.AddRange(new[]
        {
            Magnetosphere.SlowdownLabel(period, Observer.TimeDilation) + (period < Magnetosphere.MinDrawnPeriodS
                ? ": the drawn spin never turns faster than once in 2 s, so no beam crosses Earth more than once a second"
                : ""),
            SurfaceColourNote,
            ToneMappedNote,
            SurfaceBendingNote,
            SurfaceMarkingsNote,
            beams ? BeamsFromBirthNote : BeamsNotYetNote,
            BeamWidthNote,
            o.MagneticInclinationDeg is double tilt
                ? $"Magnetic tilt: the published {tilt.ToString("0", System.Globalization.CultureInfo.InvariantCulture)}° " +
                  $"({o.MagneticInclinationSource?.Citation ?? "preset"}), the same in both views"
                : SchematicTiltNote,
            FieldLinesNote,
            OverlaysUnbentNote,
            Magnetosphere.BeamCanCrossEarth(o, Observer)
                ? "To Earth: a beam sweeps across Earth (the lab's \"Jet or beam toward Earth\" checkbox); the arrow's direction round the spin axis is schematic"
                : "To Earth: the beams miss Earth (the lab's \"Jet or beam toward Earth\" checkbox is off); the arrow's direction round the spin axis is schematic",
            measuredKick ? KickDrift.LineOfSightNote : SeededKickNote,
            SurroundingsNote,
        });

        return new CloseUpState
        {
            Era = CloseUpEra.NeutronStar,
            Title = title,
            Readouts = readouts,
            Notes = notes,
            FrameRadiusKm = NeutronStarFrameRadii * radius,
            MassMsun = ns.MassMsun,
            SurfaceRadiusKm = radius,
            NeutronStar = contracting ? ns with { RadiusKm = radius } : ns,
            DrawnPeriodS = Magnetosphere.DrawnPeriodS(period),
            SpeedLabel = Magnetosphere.SlowdownLabel(period, Observer.TimeDilation),
            LightCylinderKm = lightCylinder,
            SurfaceColor = BlackbodyColor.FromTemperature(ns.SurfaceTemperatureK.Value),
            ShowBeams = beams,
            BeamTiltRad = Magnetosphere.TiltRad(o),
            BeamHalfAngleRad = Magnetosphere.BeamHalfAngleRad(period),
            EarthDirection = Magnetosphere.EarthDirection(o, Observer),
            BeamCanCrossEarth = Magnetosphere.BeamCanCrossEarth(o, Observer),
            PolarCapRad = FieldLines.PolarCapRad(radius, lightCylinder),
            FieldG = ns.FieldG,
            Cutaway = ns.HasCrust ? _cutawayWithCrust ??= CutawayInfo.Build(o, ns.MassMsun, radius, true)
                : contracting ? CutawayInfo.Build(o, ns.MassMsun, radius, false)
                : _cutawayProto ??= CutawayInfo.Build(o, ns.MassMsun, radius, false),
        };
    }

    /// <summary>Ruling R-P3-17: the inconsistency a preset lists when its neutron star has never shown pulses (Cas A's CCO).</summary>
    public const int NoPulsesSeenInconsistency = 60;

    /// <summary>Ruling R-P3-17: the inconsistency a preset lists when the lab's kick speed differs from the measured one (Cas A).</summary>
    public const int KickSpeedInconsistency = 61;

    /// <summary>Ruling R-P3-17 (#60), first among the notes for such a preset.</summary>
    public const string NoPulsesSeenNote =
        "Drawn as a pulsar because the lab's model gives every neutron star a spin and a field, but no pulses have ever been seen from Cas A's central compact object (pulsed fraction below 12%, Heinke & Ho 2010; compact §C4.3; inconsistency #60)";

    /// <summary>The measured kick of Cas A's neutron star (Holland-Ashford, Slane &amp; Long 2024; compact §C4.4), km/s.</summary>
    public const double CasAMeasuredKickKmS = 433;

    /// <summary>
    /// Ruling R-P3-17 (#61): the Kick tooltip's comparison for Cas A: the lab's speed against the measured 433 km/s (corrected for the
    /// galaxy's rotation and the Sun's motion; heliocentric 436 ± 89 km/s).
    /// </summary>
    public static string CasAKickComparison(double labKmS)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        double percent = (labKmS / CasAMeasuredKickKmS - 1) * 100;
        string how = Math.Abs(percent) < 5 ? "about as measured"
            : $"about {Math.Abs(percent).ToString("0", inv)}% {(percent < 0 ? "slow" : "fast")}";
        return $"Measured: 433 km/s (corrected for the galaxy's rotation and the Sun's motion; heliocentric 436 ± 89 km/s; Holland-Ashford, " +
               $"Slane & Long 2024). The lab gives {labKmS.ToString("0", inv)} km/s, {how}; the lab's kick speed is not tuned to it (#61).";
    }

    /// <summary>The less certain of two confidences.</summary>
    private static Confidence Worse(Confidence a, Confidence b) => (Confidence)Math.Max((int)a, (int)b);

    // ---- Plan Task 9: the core before collapse and its first second (spec §3; compact §C1, §C2) ----

    /// <summary>Plan ruling P3-12 and spec §3: up to this source time (the timeline's left end, 1 ms) the core is shown before collapse.</summary>
    public const double PreCollapseEndS = 1e-3;

    /// <summary>[Presentation] The pre-collapse core is framed at this many radii.</summary>
    public const double PreCollapseFrameRadii = 1.25;

    /// <summary>[Presentation] With a shock, the view frames this many (distorted) shock radii; the director's ladder covers 1 to 10,000 km.</summary>
    public const double ShockFrameRadii = 1.15;

    /// <summary>The largest framing radius of the first second, km (the shock leaves the frame at about 3,000 km).</summary>
    public const double MaxCoreFrameKm = 10_000;

    /// <summary>Spec §3.2: the only observation of this stage.</summary>
    public const string Sn1987aNeutrinosNote =
        "Nothing in this stage has been observed directly except SN 1987A's neutrinos (about two dozen, caught in 1987 by Kamiokande-II, IMB and Baksan)";

    /// <summary>Spec §3.2 and §C1.1: the tracks are the simulations' typical ones.</summary>
    public const string TypicalTracksNote =
        "From simulations: the shock, gain and proto-neutron-star radii follow typical tracks (no published dependence on the star was found; compact §C1). Radii and times: model-dependent";

    /// <summary>Plan Task 9 (as ruling R-P3-19): the first second is drawn as plain surfaces.</summary>
    public const string CoreUnbentNote =
        "Drawn without the bending of light, which near the proto-neutron star is a few degrees; the star around the core is not drawn";

    /// <summary>Spec §3.2: the shapes.</summary>
    public const string DistortionNote =
        "Shock distortion (convection plumes and the sloshing SASI): about 10% of its radius (approximate, Hanke et al. 2013); the shape is schematic, and the sloshing is drawn slowed to one swing in 4 s (much faster in the simulations)";

    /// <summary>Spec §3.2: the infalling matter.</summary>
    public const string InfallNote =
        "Infalling matter: speeds of about √(G M / r) ahead of the shock (Müller 2020), paths schematic (straight in), slowed down so they can be seen";

    /// <summary>Spec §3.2: the brightness and colour.</summary>
    public const string ProtoNsBrightnessNote =
        "Proto-neutron star: its brightness follows the neutrino panel's total luminosity (the 3D view's logarithmic rule); colour schematic. It shines almost only in neutrinos";

    /// <summary>§C1.1: the neutrinospheres start outside the shock.</summary>
    public const string HotCoreNote =
        "For the first few milliseconds the shock is still inside the neutrinospheres, so the hot core is drawn bounded by the shock (O'Connor & Ott 2011)";

    /// <summary>The gain shell.</summary>
    public const string GainNote = "Gain radius (faint shell): outside it, neutrinos heat the gas more than they cool it";

    /// <summary>Spec §3.2: what is left out.</summary>
    public const string CoreNotDrawnNote = "Not drawn: the neutrino-driven wind, neutrino flavour physics";

    /// <summary>§C1 App: an O-Ne-Mg core revives early; its radii are the iron-core track's (inconsistency #62).</summary>
    public const string ElectronCaptureTrackNote =
        "An O-Ne-Mg core revives its shock early, at about 0.07 s (Kitaura et al. 2006), but its radii are drawn from the iron-core track (#62)";

    /// <summary>The revived shock leaving the frame.</summary>
    public const string ShockLeavesNote =
        "The revived shock leaves the frame at about 1 s; the main view's cutaway follows it out to the star's surface";

    private static readonly Source NeutrinoSource = new(
        "The neutrino panel's total luminosity, ν_e + anti-ν_e + 4 ν_x (the lab's neutrino model)", "signals §1");
    private static readonly Source SinceBounceSource = new("The timeline's time since core bounce, in the source's frame", "spec §3");

    /// <summary>[Schematic] The colours: the iron core and the O-Ne-Mg core.</summary>
    public static readonly (float R, float G, float B) IronCoreColor = (0.86f, 0.52f, 0.32f), OneMgCoreColor = (0.80f, 0.70f, 0.50f);

    /// <summary>[Schematic] The proto-neutron star's colour: a very hot blackbody's blue-white.</summary>
    public static readonly (float R, float G, float B) ProtoNsColor = BlackbodyColor.FromTemperature(1e5);

    /// <summary>Plan Task 9: the core before collapse (spec §3: its size from the research, one value per kind of core).</summary>
    private CloseUpState PreCollapse()
    {
        var o = _outcome;
        bool oneMg = CoreCollapseHistory.IsElectronCapture(o);
        double radius = CoreCollapseHistory.PreCollapseCoreRadiusKm(o);
        string kind = oneMg ? "oxygen-neon-magnesium core" : "iron core";
        var readouts = new[]
        {
            new Readout("Core radius", new Quantity(radius, "km", Confidence.ModelDependent, CoreCollapseHistory.PreCollapseSource),
                $"The {kind}'s radius as collapse begins (stellar-evolution models; one value for every {kind})"),
        };
        var notes = new List<string>
        {
            $"Before collapse: the star's {kind}, about {UnitFormat.Number(radius)} km in radius, about to fall in on itself. Press Explode or move the time on to see bounce",
            "Its size is from stellar-evolution models (" + CoreCollapseHistory.PreCollapseSource.Citation + "); colour schematic",
            CoreUnbentNote,
        };
        if (_track == CoreTrack.Magnetorotational) notes.Add(CoreCollapseHistory.MagnetorotationalNote);
        return new CloseUpState
        {
            Era = CloseUpEra.PreCollapse,
            Title = char.ToUpperInvariant(kind[0]) + kind[1..] + " before collapse",
            Readouts = readouts,
            Notes = notes,
            FrameRadiusKm = PreCollapseFrameRadii * radius,
            Core = CoreCollapseHistory.At(o, 0),
            CoreSphereRadiusKm = radius,
            CoreColor = oneMg ? OneMgCoreColor : IronCoreColor,
            StageLabel = "Before collapse",
            StageKey = new[] { $"● {kind} (colour schematic)" },
        };
    }

    /// <summary>Plan Task 9: the first second (spec §3.1, §3.2), from bounce to the stage's end.</summary>
    private CloseUpState FirstSecondAt(double t)
    {
        var o = _outcome;
        var core = CoreCollapseHistory.At(o, t);
        double sphere = CoreCollapseHistory.DrawnProtoNsRadiusKm(core);
        bool shock = double.IsFinite(core.ShockRadiusKm), gain = double.IsFinite(core.GainRadiusKm);
        double tbh = CoreCollapseHistory.BlackHoleFormsS(o);
        double luminosity = CoreCollapseHistory.NeutrinoLuminosityErgS(o, t);
        var md = Confidence.ModelDependent;
        var src = CoreCollapseHistory.TrackSource;

        string title = core.Phase switch
        {
            CorePhase.Bounce => "Core bounce: the shock forms",
            CorePhase.Stalled => _track == CoreTrack.Failed ? "Stalled shock: it never revives" : "Stalled shock round the proto-neutron star",
            CorePhase.Revived => "Revived shock: the explosion begins",
            CorePhase.Collapsing => "Proto-neutron star collapsing to a black hole",
            _ => double.IsFinite(tbh) ? "Proto-neutron star (it collapses to a black hole later)" : "Proto-neutron star",
        };

        var readouts = new List<Readout>();
        if (shock)
            readouts.Add(new Readout("Shock radius", new Quantity(core.ShockRadiusKm, "km", md, src),
                "The shock's mean radius (typical track from simulations)"));
        if (gain)
            readouts.Add(new Readout("Gain radius", new Quantity(core.GainRadiusKm, "km", md, src),
                "Outside it neutrinos heat the gas more than they cool it"));
        readouts.Add(new Readout("Proto-neutron star",
            new Quantity(sphere, "km", md, core.Phase == CorePhase.Cooling ? CoreCollapseHistory.ContractionSource : src),
            "The hot, swollen newborn neutron star: radius where the density reaches 10¹¹ g/cm³ (bounded by the shock in the first few milliseconds)"));
        if (shock && core.DistortionFraction > 0)
            readouts.Add(new Readout("Shock distortion", new Quantity(core.DistortionFraction * 100, "%", Confidence.Speculative,
                CoreCollapseHistory.DistortionSource), "Approximate size of the convection plumes and SASI sloshing; their shape is schematic"));
        if (double.IsFinite(core.InfallSpeedKmS))
            readouts.Add(new Readout("Infall speed", new Quantity(core.InfallSpeedKmS, "km/s", md, CoreCollapseHistory.InfallSource),
                "How fast matter falls onto the shock"));
        if (luminosity >= 0)
            readouts.Add(new Readout("Neutrinos", new Quantity(luminosity, "erg/s", md, NeutrinoSource),
                "Total neutrino luminosity, as on the neutrino panel"));
        readouts.Add(new Readout("Since bounce", new Quantity(t, "s", Confidence.Established, SinceBounceSource)));

        var notes = new List<string> { TypicalTracksNote, Sn1987aNeutrinosNote };
        if (_track == CoreTrack.Magnetorotational) notes.Add(CoreCollapseHistory.MagnetorotationalNote);
        if (_track == CoreTrack.Failed)
            notes.Add($"The shock never revives: the proto-neutron star grows by accretion and collapses to a black hole at {UnitFormat.Time(tbh)}, " +
                      "when the neutrino signal stops. The track's later part is stretched in time to end then");
        else if (double.IsFinite(tbh))
            notes.Add($"The explosion succeeds, but matter falling back tips the proto-neutron star over: it collapses to a black hole at {UnitFormat.Time(tbh)}");
        if (CoreCollapseHistory.IsElectronCapture(o)) notes.Add(ElectronCaptureTrackNote);
        if (sphere < core.ProtoNsRadiusKm) notes.Add(HotCoreNote);
        notes.Add(ProtoNsBrightnessNote);
        if (gain) notes.Add(GainNote);
        if (shock) notes.Add(DistortionNote);
        if (shock) notes.Add(InfallNote);
        if (core.Phase == CorePhase.Revived) notes.Add(ShockLeavesNote);
        notes.Add(CoreUnbentNote);
        notes.Add(CoreNotDrawnNote);

        var key = new List<string> { "● proto-neutron star" };
        if (shock) key.Add("◯ shock (translucent; shape schematic)");
        if (gain) key.Add("◌ gain radius");
        if (CoreAnimation.InfallLabel(core) is { } infall) key.Add("· infalling matter, " + infall.Replace("Infall shown", "shown"));

        double frame = shock
            ? Math.Min(MaxCoreFrameKm, ShockFrameRadii * core.ShockRadiusKm * (1 + core.DistortionFraction))
            : NeutronStarFrameRadii * sphere;
        return new CloseUpState
        {
            Era = CloseUpEra.FirstSecond,
            Title = title,
            Readouts = readouts,
            Notes = notes,
            FrameRadiusKm = Math.Max(frame, 1),
            Core = core,
            CoreSphereRadiusKm = sphere,
            ProtoNsBrightness = ExplosionScene.LogBrightness(luminosity, _neutrinoPeak),
            CoreColor = ProtoNsColor,
            StageLabel = CoreCollapseHistory.FromSimulationsLabel + " · typical track",
            StageKey = key,
        };
    }

    /// <summary>A neutron star the lab cannot model (no spin or field): its size only, as Task 4 drew it.</summary>
    private static CloseUpState NeutronStar(Remnant remnant)
    {
        var mass = remnant.Mass!;
        var radius = remnant.RadiusKm!;
        return new CloseUpState
        {
            Era = CloseUpEra.NeutronStar,
            Title = remnant.Kind == RemnantKind.Magnetar ? "Magnetar" : "Neutron star",
            Readouts = new[] { new Readout("Mass", mass), new Readout("Radius", radius) },
            Notes = new[] { SurroundingsNote, PlaceholderNeutronStarNote },
            FrameRadiusKm = NeutronStarFrameRadii * radius.Value,
            MassMsun = mass.Value,
            SurfaceRadiusKm = radius.Value,
        };
    }
}
