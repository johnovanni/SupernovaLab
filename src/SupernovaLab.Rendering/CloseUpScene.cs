using SupernovaLab.Core.Compact;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Rendering;

/// <summary>What the Close-up shows at one moment (spec §2–§6). Later tasks fill in the eras before the remnant settles.</summary>
public enum CloseUpEra { None, PreCollapse, FirstSecond, NeutronStar, BlackHoleForming, BlackHoleSettling, BlackHole }

/// <summary>
/// One number in the Close-up's readout column (spec §2.1): its label, its value with confidence and source, and an optional tooltip
/// for the label. <see cref="ValueText"/> is the value as the column shows it.
/// </summary>
public sealed record Readout(string Label, Quantity Value, string? ToolTip = null)
{
    /// <summary>The value and its unit: "10 M☉", "59.1 km", or a bare number for a unitless value.</summary>
    public string ValueText => Value.Unit switch
    {
        "M☉" => UnitFormat.Mass(Value.Value),
        "" => UnitFormat.Number(Value.Value),
        _ => $"{UnitFormat.Number(Value.Value)} {Value.Unit}",
    };
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

    /// <summary>GM/c² in km for <see cref="MassMsun"/>; NaN without a compact object.</summary>
    public double GravitationalRadiusKm => MassMsun > 0 ? BlackHoleOptics.GravitationalRadiusCm(MassMsun) / 1e5 : double.NaN;

    /// <summary>Whether the lensing pass draws this state (a compact object with a mass).</summary>
    public bool IsLensed => Era != CloseUpEra.None && MassMsun > 0;
}

/// <summary>
/// The single source of what the Close-up draws at each moment (spec §8.2; the counterpart of <see cref="ExplosionScene"/>): a pure
/// function of the outcome, the observer, the run number and the source time. In this build it gives the no-remnant messages
/// (spec §2.3), a placeholder black hole (its mass, and spin 0 unless the bench's diagnostic spin is given) and a placeholder neutron
/// star (mass and radius); the eras before the remnant settles come later.
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
        "The surroundings are the main view's scene seen from the remnant, with their light bent exactly as general relativity says (each pixel's ray traced through the hole's spacetime).";

    /// <summary>The black hole is framed at this many shadow radii (the shadow fills about a third of the view's height).</summary>
    public const double BlackHoleFrameShadowRadii = 1.5;

    /// <summary>The neutron star is framed at this many radii.</summary>
    public const double NeutronStarFrameRadii = 4;

    private static readonly Source ShadowSource = new(
        "Exact (general relativity): horizon r₊ = GM/c² (1 + √(1 − a²)); shadow radius √27 GM/c² for no spin (Bardeen 1973 for spin)",
        "compact §C7");

    private readonly Outcome _outcome;
    private readonly double _spin;

    /// <param name="placeholderSpin">The placeholder black hole's spin (0 unless the bench's <c>--bench-spin</c> sets it).</param>
    public CloseUpScene(Outcome outcome, ObserverSetup observer, int runNumber, double placeholderSpin = 0)
    {
        _outcome = outcome ?? throw new ArgumentNullException(nameof(outcome));
        ArgumentNullException.ThrowIfNull(observer);
        Observer = observer;
        RunNumber = runNumber;
        _spin = Math.Clamp(double.IsFinite(placeholderSpin) ? placeholderSpin : 0, 0, 0.998);
    }

    public ObserverSetup Observer { get; }
    public int RunNumber { get; }

    /// <summary>
    /// The Close-up at this source time (s after bounce). Every state in this build is the same at all times, so it is made once (the
    /// spinning shadow's size integrates the outline).
    /// </summary>
    public CloseUpState At(double sourceTimeS) => _settled ??= Settled();

    private CloseUpState? _settled;

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
