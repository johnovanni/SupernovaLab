using SupernovaLab.Core.Compact;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Text;
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
    /// Plan Task 12 (spec §5): the drawn brightness, 0 to 1, of a giant flare now (<see cref="MagnetarFlares.DrawnIntensity"/>: one rise and one
    /// fade, capped at 1), before the app's <c>FlashLimiter</c> has its say on the wall clock; 0 without a flare on (and for every
    /// object but a magnetar).
    /// </summary>
    public double FlareIntensity { get; init; }

    /// <summary>Plan Task 12: the flare that is on now, or null.</summary>
    public GiantFlare? Flare { get; init; }

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

    /// <summary>
    /// Ruling R-P3-21: the compact object is still inside the star (before the shock breaks out, or, with no explosion, before the black
    /// hole forms), so its surroundings are drawn as a dim uniform haze (schematic) instead of the main view's star seen from inside.
    /// </summary>
    public bool InsideStar { get; init; }

    /// <summary>
    /// Task 20 (#71): how much the opaque debris hides the surroundings seen from the remnant, 0 (clear) to 1 (opaque)
    /// (<see cref="DebrisOpacity.VeilAt"/>); the lensing pass mixes the surroundings with <see cref="DebrisGlowSrgb"/> by it. 0 inside the
    /// star (that is <see cref="InsideStar"/>'s haze) and without ejecta.
    /// </summary>
    public float DebrisVeil { get; init; }

    /// <summary>Task 20: the opaque debris's glow, the blackbody colour of the lab's photosphere temperature (sRGB, schematic brightness).</summary>
    public (float R, float G, float B) DebrisGlowSrgb { get; init; }

    /// <summary>
    /// Task 20 (the owner: "a visible note in the closeup screen saying why the screen is opaque"): shown on the view itself while the
    /// surroundings are hidden, inside the star or inside the opaque debris, or while the debris clears; null otherwise.
    /// </summary>
    public string? ViewNote { get; init; }

    /// <summary>
    /// Plan Task 10: the black hole now (<see cref="BlackHoleModel.At"/>: its mass, spin, settling, disk and drift); null for any other
    /// era. <see cref="MassMsun"/> and <see cref="Spin"/> are then its drawn (stepped) mass and spin.
    /// </summary>
    public BlackHoleState? BlackHole { get; init; }

    // ---- Plan Task 11: the collapsar's accretion disk and jets (spec §6.2). 0, false or NaN without them. ----

    /// <summary>The disk's inner edge: the ISCO of <see cref="Spin"/>, the drawn spin (GM/c², exact; compact §C7); 0 without a disk.</summary>
    public double DiskInnerGm { get; init; }

    /// <summary>The disk's outer edge: MW99's 200 km torus in the drawn mass's GM/c² (compact §C8); 0 without a disk.</summary>
    public double DiskOuterGm { get; init; }

    /// <summary>The disk's brightness, 0 to 1 (<see cref="BlackHoleState.DiskBrightness"/>: its fade in and out).</summary>
    public double DiskBrightness { get; init; }

    /// <summary>Whether the lensing pass draws the disk (it exists: t_BH + 1 s to t_BH + 15 s for a collapsar).</summary>
    public bool HasDisk => DiskOuterGm > 0;

    /// <summary>Whether the jets' base is drawn: while the disk feeds the hole (the burst's engine).</summary>
    public bool ShowJets { get; init; }

    /// <summary>The jets' axis: the spin axis, +z, the main view's jet axis (<see cref="JetRenderer.JetAxis"/>).</summary>
    public Vec3 JetAxis { get; init; } = new(0, 0, 1);

    /// <summary>The jets' half-opening angle, the main view's (<see cref="Core.Signals.GrbModel.JetHalfAngleRad"/>); NaN without jets.</summary>
    public double JetHalfAngleRad { get; init; } = double.NaN;

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
/// (spec §2.3), the black hole (plan Task 10: its forming, settling and settled eras, spec §6, from <see cref="BlackHoleModel"/>) and (plan Task 6) the neutron
/// star at each time (spec §4: its readouts, notes, surface colour, beams, field, light cylinder and Earth's direction). Plan Task 9:
/// for a core collapse, the core before collapse up to 1 ms (the PreCollapse era) and its first second until
/// <see cref="CoreCollapseHistory.StageEndS"/> (the FirstSecond era: spec §3), then the remnant (from 1 s to 1 minute a neutron star
/// still contracts to its radius, compact §C2), or the black hole from t_BH.
/// </summary>
public sealed class CloseUpScene
{
    /// <summary>The view's label (plan's exact strings): the close-up is where no telescope could be (spec §6.5).</summary>
    public const string NoTelescopeNote = "Camera where no telescope could be";

    /// <summary>Spec §2.3: a Type Ia or a pair-instability supernova.</summary>
    public const string DestroyedMessage = "No compact remnant: the star was destroyed completely.";

    /// <summary>Spec §2.3: no supernova, or a white dwarf (a Type Iax's bound remnant included).</summary>
    public const string WhiteDwarfMessage = "This star leaves a white dwarf. The white-dwarf close-up is not part of the lab.";

    /// <summary>The bench's note when its diagnostic spin replaces the model's settled spin.
    public const string BenchSpinNote = "Spin set by the --bench-spin diagnostic, not by the lab's model.";

    /// <summary>The Surface readout's tooltip addition before 1 minute: the research does not cover the first minute.</summary>
    public const string SurfaceHeldTip = "(Before 1 minute the value is held at its 1-minute value.)";

    /// <summary>The neutron star's note while only its size is drawn.</summary>
    public const string PlaceholderNeutronStarNote =
        "The neutron star is drawn as a plain sphere of its radius (placeholder colour), with the bending of light around it.";

    /// <summary>What the lensing pass bends, said once.</summary>
    public const string SurroundingsNote =
        "The surroundings are Project 2's scene (at Low quality) seen from the remnant, with their light bent as computed from general relativity (each pixel's ray traced through the hole's spacetime). The wind's schematic haze is left out: it marks unlit gas, and from inside it would only wash the sky evenly. The main view's cutaway, the jets (drawn separately along +z) and SN 1987A's rings are also left out.";

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
        "The beams, the field lines and the light cylinder are drawn once the star's magnetosphere forms, about 30 s after bounce (approximate: 10 to 100 s, compact §C4.2)";

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
    private readonly bool _isNeutronStar, _isBlackHole;
    private readonly IReadOnlyList<GiantFlare> _flares;

    /// <param name="placeholderSpin">The bench's <c>--bench-spin</c> diagnostic: above 0 it replaces the black hole's settled spin.</param>
    public CloseUpScene(Outcome outcome, ObserverSetup observer, int runNumber, double placeholderSpin = 0)
    {
        _outcome = outcome ?? throw new ArgumentNullException(nameof(outcome));
        ArgumentNullException.ThrowIfNull(observer);
        Observer = observer;
        RunNumber = runNumber;
        _flares = MagnetarFlares.For(outcome, runNumber);
        _spin = Math.Clamp(double.IsFinite(placeholderSpin) ? placeholderSpin : 0, 0, 0.998);
        _isNeutronStar = NeutronStarModel.At(outcome, observer, runNumber, 0) is not null;
        _isBlackHole = BlackHoleModel.BirthOrNull(outcome) is not null && outcome.Remnant.Mass is { Value: > 0 } m && double.IsFinite(m.Value);
        _track = CoreCollapseHistory.TrackFor(outcome);
        _stageEndS = CoreCollapseHistory.StageEndS(outcome);
        _neutrinoPeak = _track is null ? double.NaN : CoreCollapseHistory.PeakNeutrinoLuminosityErgS(outcome);
        if (_track is not null)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            double breakout = SupernovaLab.Core.Signals.EjectaKinematics.BreakoutTimeS(outcome);
            bool breaksOut = breakout > 0 && double.IsFinite(breakout);
            InsideStarUntilS = breaksOut ? breakout : _stageEndS;
            InsideStarNote = string.Format(inv, breaksOut ? InsideStarNoteFormat : InsideFailedStarNoteFormat, UnitFormat.Time(InsideStarUntilS));
            InsideStarViewNote = string.Format(inv, breaksOut ? InsideStarViewNoteFormat : InsideFailedStarViewNoteFormat, UnitFormat.Time(InsideStarUntilS));
        }
        else InsideStarUntilS = double.NaN;
        // Fix round 1 (review item 1): the jets' times, from the GRB model (as the main view) and the disk's end (BlackHoleModel).
        if (BlackHoleModel.BirthOrNull(outcome) == BlackHoleBirth.Collapsar && JetGrb.Applies(outcome))
        {
            JetsBreakOutS = JetGrb.ValidRange(outcome).StartS;
            JetBaseEndsS = BlackHoleModel.SettleStartS(outcome) + BlackHoleModel.DiskToS;
            JetsNote = string.Format(System.Globalization.CultureInfo.InvariantCulture, JetsNoteFormat,
                UnitFormat.Time(JetsBreakOutS), UnitFormat.Time(JetBaseEndsS));
        }
    }

    private static readonly Core.Signals.GrbModel JetGrb = new();

    /// <summary>Ruling R-P3-21, the coordinator's exact words; {0} is the breakout time.</summary>
    public const string InsideStarNoteFormat =
        "Inside the star until the shock breaks out ({0}): opaque hot gas surrounds the core; drawn as a warm glow (schematic)";

    /// <summary>Ruling R-P3-21 for a star that never explodes; {0} is when the black hole forms.</summary>
    public const string InsideFailedStarNoteFormat =
        "Inside the star: it never explodes, so opaque hot gas surrounds the core until the black hole forms ({0}); drawn as a warm glow (schematic)";

    /// <summary>
    /// [Schematic, presentation] Ruling R-P3-21: the haze inside the star, in the HDR target's linear units at the main view's base exposure,
    /// and as sRGB for the no-HDR path (a dim warm brown). Fix round 1's launch: at (0.11, 0.06, 0.03) it read as a medium brown (about
    /// sRGB 0.36, 0.22, 0.12) that competed with the shock, so it was made 5× dimmer (not yet seen in a launch).
    /// </summary>
    /// Task 20 (the owner: "I don't see a haze ... I see a black screen"): brighter again, between the two (about 3× the dim one), so it
    /// reads as a warm glow without hiding the shock.
    public static readonly (float R, float G, float B) InsideStarHazeHdr = (0.065f, 0.036f, 0.017f), InsideStarHazeSrgb = (0.26f, 0.16f, 0.08f);

    /// <summary>Ruling R-P3-22: above this fraction of c the infall readout's Newtonian estimate is flagged (Project 1's R28 limit).</summary>
    public const double RelativisticInfallBeta = 0.3;

    /// <summary>Ruling R-P3-22's note.</summary>
    public const string RelativisticInfallNote =
        "Infall speed above 0.3c: the Newtonian estimate √(G M / r) is not accurate this close in (the lab's formulas are trusted only up to 0.3c)";

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
        var state = AtTime(sourceTimeS);
        if (state.Era == CloseUpEra.None) return state;
        // Ruling R-P3-21: inside the star until the shock breaks out (or, with no explosion, until the black hole forms).
        if (sourceTimeS < InsideStarUntilS)
            return state with { InsideStar = true, ViewNote = InsideStarViewNote, Notes = state.Notes.Append(InsideStarNote!).ToList() };
        // Task 20 (#71): then inside the debris, opaque from the centre for months, clearing smoothly.
        double veil = DebrisOpacity.VeilAt(_outcome, sourceTimeS);
        if (!(veil > 0) || !(state.IsLensed)) return state;
        double kelvin = Core.Signals.EjectaKinematics.PhotosphereTemperatureK(_outcome, sourceTimeS);
        var glow = BlackbodyColor.FromTemperature(double.IsFinite(kelvin) ? Math.Clamp(kelvin, GlowMinK, GlowMaxK) : GlowMinK);
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        string note = veil >= 1
            ? string.Format(inv, InsideDebrisViewNoteFormat, UnitFormat.Time(DebrisOpacity.OpaqueUntilS(_outcome)))
            : string.Format(inv, ClearingViewNoteFormat, UnitFormat.Time(DebrisOpacity.ClearByS(_outcome)));
        return state with
        {
            DebrisVeil = (float)veil, DebrisGlowSrgb = glow, ViewNote = note,
            Notes = state.Notes.Append(DebrisNote).ToList(),
        };
    }

    /// <summary>[Schematic] The debris glow's colour range, K: the lab's photosphere temperature, clamped.</summary>
    public const double GlowMinK = 3500, GlowMaxK = 15000;

    /// <summary>Task 20: the view's note inside the star (the owner asked for the reason on screen).</summary>
    public string? InsideStarViewNote { get; }

    public const string InsideStarViewNoteFormat =
        "Inside the star: the gas round the core is opaque until the shock breaks out ({0}), so nothing beyond it can be seen. Drawn as a warm glow.";

    public const string InsideFailedStarViewNoteFormat =
        "Inside the star: it never explodes, and the gas round the core is opaque until the black hole forms ({0}). Drawn as a warm glow.";

    /// <summary>Task 20: the view's note while the debris is opaque; {0} is when it starts to clear.</summary>
    public const string InsideDebrisViewNoteFormat =
        "Inside the debris: the expanding gas round the remnant stays opaque until about {0}, so the sky is hidden. Drawn as a glow.";

    /// <summary>Task 20: the view's note while the debris clears; {0} is when it is clear.</summary>
    public const string ClearingViewNoteFormat = "The debris is thinning: the sky shows through by about {0}.";

    /// <summary>Task 20: the Notes column's entry, with the source.</summary>
    public const string DebrisNote =
        "Seen from the remnant the debris stays opaque for months: until its light curve's plateau ends (at least 100 days), clear by twice " +
        "that (approximate; Kasen & Woosley 2009, Jerkstrand 2017). Blue light stays blocked for years, and dust dims the centre again from " +
        "about 1.5 years (SN 1987A); neither is drawn";

    /// <summary>
    /// Ruling R-P3-21: until when (source s) the compact object is inside the star: the shock breakout (EjectaKinematics.BreakoutTimeS);
    /// for a core collapse with no ejecta (a failed supernova; my call) the end of the first second, when the black hole forms, after which
    /// the main view's own picture of the vanished star is used. NaN with no core collapse.
    /// </summary>
    public double InsideStarUntilS { get; }

    /// <summary>Ruling R-P3-21's note for this outcome (null with no core collapse).</summary>
    public string? InsideStarNote { get; }

    private CloseUpState AtTime(double sourceTimeS)
    {
        if (_track is not null)
        {
            // Plan ruling P3-12, spec §3: the core before collapse up to the scrubber's left end (1 ms), then its first second.
            if (!(sourceTimeS > PreCollapseEndS)) return _preCollapse ??= PreCollapse();
            if (sourceTimeS < _stageEndS) return FirstSecondAt(sourceTimeS);
        }
        if (_isBlackHole) return BlackHoleAt(sourceTimeS);
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

    // ---- Plan Task 10: the black hole, its formation, settling and spin (spec §6.1–§6.5; compact §C8, §C9) ----

    /// <summary>Spec §6.4: the formation (the first second's collapse has just put the proto-neutron star behind its horizon).</summary>
    public const string FormingNote =
        "Formation: the proto-neutron star has fallen behind its new horizon, in under 1 ms (O'Connor & Ott 2011), as the neutrino signal stops. A new black hole rings down within milliseconds to a smooth spinning state (the ring-down is not drawn)";

    /// <summary>Spec §6.4 and ruling P3-11: the direct collapse's fade, a visual transition.</summary>
    public const string DirectSettlingNote =
        "Settling: the view fades from a non-spinning picture into the spinning hole over a third of a decade of time after formation (about 1 s of playback). This is a visual transition: the real ring-down takes milliseconds";

    /// <summary>Spec §6.4: the collapsar's spin-up.</summary>
    public const string CollapsarSpinUpNote =
        "Spinning up: for about 15 s the accretion disk feeds the hole, which spins it up from about 0.5 to about 0.9 of the maximum (MacFadyen & Woosley 1999; approximate shape). The picture follows the spin as it climbs";

    /// <summary>Spec §6.4: the fallback hole settles once the fallback ends (§C8).</summary>
    public const string FallbackSettlingNote =
        "Settling: matter falls back onto the hole for minutes to hours (MacFadyen, Woosley & Heger 2001); the view fades into the settled spinning hole by about 1,000 s (approximate), a visual transition";

    /// <summary>Spec §6.4: the settled hole (with the plan's caption first).</summary>
    public const string KerrNote =
        "A settled black hole is set entirely by its mass and spin (a Kerr black hole): it has no other features";

    /// <summary>Spec §6.1 and §6.4: what the spin changes on screen.</summary>
    public const string SpinLooksNote =
        "Spin shifts the shadow sideways and flattens one side (by up to about 7% seen side-on, as the camera starts); the bright photon ring at its edge, light that has looped round the hole, becomes lopsided";

    /// <summary>Ruling P3-9: the overlay outline.</summary>
    public const string ShadowEdgeNote =
        "Dashed outline: the shadow's exact edge for this camera (Bardeen 1973's photon orbits), at full resolution whatever the light-bending detail";

    /// <summary>Inconsistency #65: whose picture it is.</summary>
    public const string CoordinateCameraNote =
        "The picture aims each pixel along a direction in the light-bending calculation's own coordinates; close in, a real camera hovering there would see the shadow a little smaller (about 5% at the starting distance, #65). The readouts give the exact sizes seen from far away";

    /// <summary>Spec §6.3: the grid and its labels (the menu's switch).</summary>
    public const string GridNote =
        "View ▸ Grid: a measuring aid, a latitude-longitude sphere at 50 GM/c² bent like the surroundings (its lines twist near a spinning hole: frame dragging), with the horizon, the photon orbits and the ergosphere outlined where they are, without the bending of light";

    /// <summary>Spec §6.5: a direct collapse ejects nothing, so its hole is not kicked.</summary>
    public const string NoKickNote = "Kick: none, a direct collapse ejects nothing to push the hole (the lab's outcomes §6)";

    /// <summary>Spec §6.5: the momentum-scaled kick of a hole made in an explosion.</summary>
    public const string ScaledKickNote =
        "Kick: the neutron stars' mean kick scaled by momentum, 1.4 M☉ / M_BH (speculative); its direction is drawn from the run number (display randomness)";

    private static readonly Source FromEarthSource = new(
        "Shadow law d = 2√27 GM/(D c²) at the source's angular-diameter distance; EHT nominal resolution 20 µas (EHT Sgr A* Paper VI)",
        "compact §C9");

    /// <summary>The OGLE comparison (compact §C9 App, its exact words).</summary>
    public const string OgleComparison =
        "OGLE-2011-BLG-0462, the only known isolated stellar-mass black hole, found by microlensing: 7.15 ± 0.83 M☉ (Sahu et al. 2025); " +
        "other analyses give 6.0 (Lam & Lu 2023) to 7.9 M☉ (Mróz et al. 2022); an early analysis allowed 1.6–4.4 M☉ (Lam et al. 2022).";

    /// <summary>Spec §6.4 and §C8: the spin's checks against observed holes.</summary>
    public const string SpinChecks =
        "Speculative: the lab has no birth-spin model, so the spin comes from the Rotation input (compact §C8). Measured spins: Cygnus X-1 above 0.9985 " +
        "(Zhao et al. 2021); A0620−00 0.12 ± 0.19 (Gou et al. 2010); LIGO–Virgo–KAGRA's GWTC-3: half the merging holes below 0.25, most below 0.4 " +
        "(Abbott et al. 2023). Single stars that shed their angular momentum give about 0.01 (Fuller & Ma 2019).";

    private CloseUpState? _settledBlackHole;

    // ---- Plan Task 11: the collapsar's accretion disk and jets (spec §6.2, §6.4; compact §C7, §C8) ----

    /// <summary>The plan's exact note while the disk is drawn.</summary>
    public const string DiskNote = "Accretion disk: inner edge at the ISCO (exact); size from simulations; brightness schematic";

    /// <summary>What is exact and what schematic in the disk's light (spec §6.2).</summary>
    public const string DiskShadingNote =
        "The disk is drawn thin, in the hole's equator, from the innermost stable orbit (the ISCO, exact for the spin) to the 200 km torus of the simulations (MacFadyen & Woosley 1999), for the ≈ 15 s the infall feeds it. Its light carries the exact shift of gas orbiting a spinning hole, gravitational and Doppler together (g = 1/(u^t(1 − Ωℓ)); Bardeen, Press & Teukolsky 1972), as seen from far away: the side turning towards you is brighter (by g⁴) and bluer, the gas near the hole redder. Its radial brightness and colours are schematic: the real torus is thick, at billions of degrees, and shines mostly in neutrinos. Seen edge-on, as the camera starts, its far side shows bent over and under the shadow";

    /// <summary>
    /// The jets' base (spec §6.2: "the jets point along the spin axis, which is the main view's rotation axis"). Fix round 1 (review item 1):
    /// its times come from Core, the sources both views read: {0} the GRB model's onset (<see cref="Core.Signals.GrbModel.ValidRange"/>'s
    /// start, where the main view starts drawing the jets), {1} the disk's end, t_BH + <see cref="BlackHoleModel.DiskToS"/> (#67).
    /// </summary>
    public const string JetsNoteFormat =
        "Jets: their base, along the spin axis, the same axis as the main view's jets, drawn while the disk feeds the hole (schematic, the main view's 5° half-angle; drawn without the bending of light, hidden inside the shadow and, along straight lines, behind the disk). The jets are inside the star until they break out at {0} after bounce, when the main view starts drawing them; their base empties when the disk ends at {1} after bounce (#67)";

    /// <summary>The jets note for this outcome (<see cref="JetsNoteFormat"/>); null without a collapsar.</summary>
    public string? JetsNote { get; }

    /// <summary>When the jets break out of the star and the main view starts drawing them (s after bounce, source time); NaN without a collapsar.</summary>
    public double JetsBreakOutS { get; } = double.NaN;

    /// <summary>When the disk ends and the jets' base empties: t_BH + <see cref="BlackHoleModel.DiskToS"/> (s after bounce); NaN without a collapsar.</summary>
    public double JetBaseEndsS { get; } = double.NaN;

    /// <summary>After the disk: why it is gone (spec §6.2: "the disk fades out when accretion ends").</summary>
    public const string DiskEndedNote =
        "The accretion disk is gone: the infall that fed it ends about 15 s after the hole formed (MacFadyen & Woosley 1999), and the jets' base with it";

    /// <summary>
    /// Fix round 1 (review item 5): <see cref="DiskEndedNote"/> shows in the early settled era only, until this many times the disk's end
    /// time after bounce (160 s for SN 1998bw); later the hole is simply a settled hole.
    /// </summary>
    public const double DiskEndedNoteUntilFactor = 10;

    /// <summary>
    /// Plan Task 10: the black hole at this time (<see cref="BlackHoleModel.At"/>): the forming, settling and settled eras (ruling P3-12),
    /// the readouts of spec §6.1 (mass, horizon, shadow, spin, kick and the size from Earth), the notes and the stage caption. The lensing
    /// pass draws the stepped <see cref="BlackHoleState.DrawnMassMsun"/> and <see cref="BlackHoleState.DrawnSpin"/>.
    /// </summary>
    private CloseUpState BlackHoleAt(double sourceTimeS)
    {
        var o = _outcome;
        double t0 = BlackHoleModel.SettleStartS(o);
        var bh = BlackHoleModel.At(o, Observer, RunNumber, Math.Max(sourceTimeS, t0), _spin > 0 ? _spin : null)!;
        bool still = !(o.Remnant.KickKmS is { Value: > 0 });   // without a kick a settled hole is the same at every time
        if (bh.IsSettled && still && _settledBlackHole is { } cached) return cached;

        var mass = o.Remnant.Mass!;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        double m = bh.MassMsun, a = bh.Spin;
        double rgKm = BlackHoleOptics.GravitationalRadiusCm(m) / 1e5;
        bool growing = bh.SettledFraction < 1 && bh.FinalMassMsun > bh.FormationMassMsun;
        var massQ = growing ? new Quantity(m, "M☉", Confidence.Speculative, BlackHoleModel.GrowthSource) : mass;
        string massTip = (growing
            ? $"Growing as the star falls in: drawn from {UnitFormat.Mass(bh.FormationMassMsun)} at formation (the heaviest neutron star) to the " +
              $"outcome's {UnitFormat.Mass(bh.FinalMassMsun)}; the lab has no accretion history, so the growth's timing is schematic (#63). "
            : "") + OgleComparison;
        var horizon = new Quantity(2 * BlackHoleOptics.HorizonRadius(a) * rgKm, "km", Worse(mass.Confidence, Confidence.Speculative), ShadowSource);
        var shadow = new Quantity(BlackHoleOptics.ApparentShadowDiameterRad(m, 1e5, a), "km", massQ.Confidence, ShadowSource);
        var kick = o.Remnant.KickKmS ?? new Quantity(0, "km/s", Confidence.Speculative, new Source("The outcome gives no kick", "spec §6.5"));
        var drift = new Quantity(bh.OffsetCm.Length, "cm", kick.Confidence, DriftSource);
        double dA = Observer.LuminosityDistanceCm / Math.Pow(1 + Observer.Redshift, 2);
        var fromEarth = new Quantity(BlackHoleModel.ShadowNanoArcsec(m, dA), "nano-arcseconds", massQ.Confidence, FromEarthSource);
        string settledText = a.ToString("0.###", inv);
        var readouts = new[]
        {
            new Readout("Mass", massQ, massTip),
            new Readout("Horizon", horizon,
                "Diameter: twice the horizon's radius r₊ = GM/c² (1 + √(1 − a²)), exact for the hole's mass and spin. The horizon itself is never visible; with the grid on, its outline is labelled"),
            new Readout("Shadow", shadow,
                "Diameter of the dark region where light falls in, exact for the mass and spin (Bardeen 1973): about 2.6 times the horizon's size without spin; with spin, its mean seen side-on"),
            new Readout("Spin", bh.SpinQuantity, (bh.Birth == BlackHoleBirth.Collapsar && !bh.IsSettled
                ? $"Spinning up by accretion towards {BlackHoleModel.GrbSpin.ToString("0.0", inv)} (MacFadyen & Woosley 1999). " : "")
                + $"a* = Jc/GM², the fraction of the most a hole can spin. " + SpinChecks),
            new Readout("Kick", kick, (bh.Birth == BlackHoleBirth.DirectCollapse ? NoKickNote : ScaledKickNote) +
                ". The second number is the distance from the explosion's centre.", drift),
            new Readout("From Earth", fromEarth, BlackHoleModel.FromEarthText(m, dA) +
                ". This is why the view says \"" + NoTelescopeNote + "\""),
        };

        var (era, title, caption) = bh.Forming
            ? (CloseUpEra.BlackHoleForming, "Black hole forming", "Forming: the proto-neutron star falls behind its horizon")
            : bh.IsSettled
                ? (CloseUpEra.BlackHole, "Black hole", BlackHoleModel.SettledCaption)
                : bh.Birth == BlackHoleBirth.Collapsar
                    ? (CloseUpEra.BlackHoleSettling, "Black hole spinning up",
                        $"Spinning up by accretion: spin {a.ToString("0.00", inv)}, rising to {BlackHoleModel.SettledSpin(o).ToString("0.0", inv)}")
                    : (CloseUpEra.BlackHoleSettling, "Black hole settling", bh.Birth == BlackHoleBirth.Fallback
                        ? "Settling as the fallback ends (visual transition)"
                        : "Settling into a spinning black hole (visual transition)");

        var notes = new List<string>();
        if (bh.IsSettled) notes.AddRange(new[] { BlackHoleModel.SettledCaption, KerrNote });
        else if (bh.Forming) notes.Add(FormingNote);
        if (!bh.IsSettled)
            notes.Add(bh.Birth switch
            {
                BlackHoleBirth.Collapsar => CollapsarSpinUpNote,
                BlackHoleBirth.Fallback => FallbackSettlingNote,
                _ => DirectSettlingNote,
            });
        notes.Add($"Spin {settledText} of the maximum (speculative: from the Rotation input, compact §C8). " + SpinLooksNote);
        if (growing)
            notes.Add($"The hole is drawn growing from {UnitFormat.Mass(bh.FormationMassMsun)} at formation to the outcome's " +
                      $"{UnitFormat.Mass(bh.FinalMassMsun)} as the star falls in; the lab has no accretion history, so the timing is schematic (#63)");
        notes.Add(ShadowEdgeNote);
        notes.Add(CoordinateCameraNote);
        notes.Add(GridNote);
        notes.Add(bh.Birth == BlackHoleBirth.DirectCollapse ? NoKickNote : ScaledKickNote);
        notes.Add(SurroundingsNote);
        if (_spin > 0) notes.Add(BenchSpinNote);
        // Plan Task 11: the collapsar's disk and the jets' base while accretion feeds the hole; afterwards, why they are gone.
        bool disk = bh.HasDisk;
        if (disk) notes.InsertRange(bh.IsSettled ? 2 : 1, new[] { DiskNote, DiskShadingNote, JetsNote ?? "" }.Where(n => n.Length > 0));
        else if (bh.Birth == BlackHoleBirth.Collapsar && sourceTimeS - t0 > BlackHoleModel.DiskToS
                 && sourceTimeS <= DiskEndedNoteUntilFactor * (t0 + BlackHoleModel.DiskToS)) notes.Add(DiskEndedNote);
        var key = new List<string> { "┄ the shadow's exact edge (computed for this camera)", "View ▸ Grid: the measuring grid, horizon, photon orbits, ergosphere" };
        if (disk) key.Insert(0, "Accretion disk (inner edge at the ISCO; brightness schematic) and the jets' base along the spin axis");

        double drawnShadowKm = BlackHoleOptics.ApparentShadowDiameterRad(bh.DrawnMassMsun, 1e5, bh.DrawnSpin);
        var state = new CloseUpState
        {
            Era = era,
            Title = title,
            Readouts = readouts,
            Notes = notes,
            FrameRadiusKm = BlackHoleFrameShadowRadii * drawnShadowKm / 2,
            MassMsun = bh.DrawnMassMsun,
            Spin = bh.DrawnSpin,
            BlackHole = bh,
            StageLabel = caption,
            StageKey = key,
            DiskInnerGm = disk ? bh.DiskInnerGm : 0,
            DiskOuterGm = disk ? bh.DiskOuterGm : 0,
            DiskBrightness = disk ? bh.DiskBrightness : 0,
            ShowJets = disk,
            JetHalfAngleRad = disk ? Core.Signals.GrbModel.JetHalfAngleRad : double.NaN,
        };
        if (bh.IsSettled && still) _settledBlackHole = state;
        return state;
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
        // Plan Task 12 (ruling R-P3-3, #57): a magnetar spinning faster than the 2 s the observed ones start at says so, whatever the preset.
        bool shortMagnetar = ns.IsMagnetar && period < Magnetosphere.MinDrawnPeriodS;
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
                            ?? "Seen from afar (redshifted): the lab's cooling model; before 30 years an early-cooling band (compact §C3)." +
                               (t < NeutronStarModel.EarlyStartS ? " " + SurfaceHeldTip : "");
        var readouts = new[]
        {
            new Readout("Mass", r.Mass!, "Gravitational mass"),
            contracting
                ? new Readout("Radius", new Quantity(radius, "km", Confidence.ModelDependent, CoreCollapseHistory.ContractionSource),
                    $"Still contracting: the proto-neutron star shrinks to the remnant's {UnitFormat.Number(ns.RadiusKm)} km by about one minute after bounce (approximate)")
                : new Readout("Radius", r.RadiusKm!),
            new Readout("Spin", spin,
                $"The spin period now, and Ṗ, how fast it lengthens ({PdotSource.Citation}). The light cylinder, c P / 2π, grows as the star slows." +
                (shortMagnetar ? " " + MagnetarSpinNote(period) : ""),
                pdot, "Ṗ"),
            new Readout("Field", r.MagneticFieldG!, "The surface dipole field" +
                (ns.IsMagnetar ? $": {Comparisons.ForMagneticField(r.MagneticFieldG!.Value)} (spec §5; a fridge magnet is about 100 G). {FieldDecayNote}" : "")),
            new Readout("Surface", ns.SurfaceTemperatureK, surfaceTip),
            new Readout("Age", new Quantity(t, "s", Confidence.Established, AgeSource), "Time since core bounce"),
            new Readout("Kick", kick, kickTip, drift),
        };

        var notes = new List<string>();
        if (noPulsesSeen && beams) notes.Add(NoPulsesSeenNote);   // Task 14 (Task 6's carried minor): only once the beams exist
        // Plan Task 12: a giant flare in progress is first; then the magnetar's standing notes (spec §5): its spin against the observed
        // magnetars' (#57), the field's decay, which is not modelled, and the flares of this run.
        var flare = ns.IsMagnetar ? MagnetarFlares.Active(_flares, t) : null;
        double flareIntensity = flare is null ? 0 : MagnetarFlares.IntensityAt(_flares, t);
        if (flare is not null) notes.Add(MagnetarFlares.Note(flare));
        if (ns.IsMagnetar)
        {
            if (shortMagnetar) notes.Add(MagnetarSpinNote(period));
            notes.Add(FieldDecayNote);
            notes.Add(FlaresNote(_flares.Count));
        }
        notes.AddRange(new[]
        {
            SpeedNote(period, Observer.TimeDilation),
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
            Flare = flare,
            FlareIntensity = flareIntensity,
            Cutaway = ns.HasCrust ? _cutawayWithCrust ??= CutawayInfo.Build(o, ns.MassMsun, radius, true)
                : contracting ? CutawayInfo.Build(o, ns.MassMsun, radius, false)
                : _cutawayProto ??= CutawayInfo.Build(o, ns.MassMsun, radius, false),
        };
    }

    /// <summary>
    /// The speed note (Task 14, Task 8's carried minor). The label (<see cref="Magnetosphere.SlowdownLabel"/>) compares the drawing with the
    /// clock a telescope on Earth keeps, so under redshift a star that turns faster than once in 2 s can still be "Shown at real speed"
    /// (its pulses arrive stretched by 1 + z). The note then says so instead of seeming to contradict the label.
    /// </summary>
    public static string SpeedNote(double periodS, double timeDilation)
    {
        string label = Magnetosphere.SlowdownLabel(periodS, timeDilation);
        if (!(periodS < Magnetosphere.MinDrawnPeriodS)) return label;
        const string Floor = "the drawn spin never turns faster than once in 2 s, so no beam crosses Earth more than once a second";
        if (label != Magnetosphere.SlowdownLabel(Magnetosphere.MinDrawnPeriodS, 1)) return label + ": " + Floor;
        return $"Shown at about real speed as received on Earth: the star turns once in {UnitFormat.Time(periodS)}, the redshift stretches its " +
               $"pulses to once in {UnitFormat.Time(periodS * timeDilation)}, and {Floor}";
    }

    /// <summary>Ruling R-P3-17: the inconsistency a preset lists when its neutron star has never shown pulses (Cas A's CCO).</summary>
    public const int NoPulsesSeenInconsistency = 60;

    /// <summary>Ruling R-P3-17: the inconsistency a preset lists when the lab's kick speed differs from the measured one (Cas A).</summary>
    public const int KickSpeedInconsistency = 61;

    /// <summary>Ruling R-P3-3: the inconsistency a magnetar spinning faster than the observed ones (about 2–12 s) discloses.</summary>
    public const int MagnetarSpinInconsistency = 57;

    /// <summary>
    /// Ruling R-P3-3 (#57): the lab's magnetar spins faster than the observed magnetars (about 2–12 s; compact §C6; Olausen &amp; Kaspi 2014):
    /// the lab's spin-down law leaves its fast-born magnetars near 0.2 s at 1,000 yr and its slow-rotation one at 1.4 s. Shown, not tuned.
    /// </summary>
    public static string MagnetarSpinNote(double periodS)
        => $"Observed magnetars spin at about 2–12 s (Olausen & Kaspi 2014); this one spins at {UnitFormat.Time(periodS)} because of the lab's " +
           $"spin-down law (Kasen & Bildsten 2010's timescale), which is not tuned to them (inconsistency #{MagnetarSpinInconsistency})";

    /// <summary>Spec §5, compact §C6: the field's decay is outside the timeline and not modelled.</summary>
    public const string FieldDecayNote = "Magnetar fields decay over about 10,000 years (Beniamini et al. 2019); not shown";

    /// <summary>Spec §5: the standing note about this run's giant flares (Speculative; compact §C6).</summary>
    public static string FlaresNote(int count)
        => $"Giant flares (speculative): about 1 per {(1 / MagnetarFlares.RatePerYear).ToString("0", System.Globalization.CultureInfo.InvariantCulture)} years " +
           $"per magnetar, from the three seen; this run draws {(count == 0 ? "none" : count.ToString(System.Globalization.CultureInfo.InvariantCulture))} " +
           "in the timeline (Re-roll draws again)";

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
        "Shock distortion (convection plumes and the sloshing SASI): about 10% of its radius, rms at the sloshing's peak (approximate, Hanke et al. 2013); the shape is schematic, and the sloshing is drawn slowed to one swing in 4 s (much faster in the simulations)";

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
            readouts.Add(new Readout("Shock distortion", new Quantity(core.DistortionFraction * 100, "%", Confidence.ModelDependent,
                CoreCollapseHistory.DistortionSource), "Approximate size of the convection plumes and SASI sloshing; their shape is schematic"));
        bool relativistic = core.InfallSpeedKmS * 1e5 > RelativisticInfallBeta * PhysicalConstants.C;
        if (double.IsFinite(core.InfallSpeedKmS))
            readouts.Add(new Readout("Infall speed", new Quantity(core.InfallSpeedKmS, "km/s", md, CoreCollapseHistory.InfallSource),
                "How fast matter falls onto the shock" + (relativistic ? ". " + RelativisticInfallNote : "")));
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
        if (relativistic) notes.Add(RelativisticInfallNote);
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
