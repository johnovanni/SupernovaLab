using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Tests;

/// <summary>
/// Outcomes for signal-model tests (ruling R3): computed by the engine, or hand-built with <see cref="Custom"/> so a
/// formula test can pin the inputs it checks. Later tasks extend this factory.
/// </summary>
public static class TestOutcomes
{
    public static OutcomeEngine Engine { get; } = new(ExplodabilityTable.LoadEmbedded());

    private static readonly Source TestSource = new("Hand-built test outcome", "tests");

    /// <summary>A massive star of this initial mass (M☉), everything else at <see cref="LabInputs.Default"/>.</summary>
    public static Outcome Star(double initialMass) => Engine.Compute(LabInputs.Default with { InitialMass = initialMass });

    /// <summary>The default Type Ia: a near-Chandrasekhar white dwarf.</summary>
    public static Outcome TypeIa() => Engine.Compute(LabInputs.Default with { Kind = StarKind.WhiteDwarf });

    /// <summary>A quantity with the test source.</summary>
    public static Quantity Q(double value, string unit) => new(value, unit, Confidence.ModelDependent, TestSource);

    /// <summary>A neutron star of this gravitational mass (M☉) and radius (km).</summary>
    public static Remnant NeutronStar(double mass, double radiusKm = 12)
        => new(RemnantKind.NeutronStar, Q(mass, "M☉"), Q(radiusKm, "km"), Q(0.1, "s"), Q(3e12, "G"), Q(350, "km/s"), null);

    /// <summary>A black hole of this mass (M☉) that forms <paramref name="formationTimeS"/> after bounce.</summary>
    public static Remnant BlackHole(double mass, double formationTimeS)
        => new(RemnantKind.BlackHole, Q(mass, "M☉"), null, null, null, Q(0, "km/s"), Q(formationTimeS, "s"));

    /// <summary>
    /// A hand-built outcome: explosion energy in erg, ejecta and ⁵⁶Ni masses in M☉, progenitor radius in cm. The remnant
    /// defaults to a 1.4 M☉, 12 km neutron star. Thermonuclear types get white-dwarf inputs. A null energy, ejecta or
    /// nickel mass leaves that quantity null (as for a failed supernova).
    /// </summary>
    public static Outcome Custom(SupernovaType type, double? energyErg, double? ejectaMass, double? nickelMass, double radiusCm,
        Remnant? remnant = null, Fate fate = Fate.Supernova, bool hasGrb = false, LabInputs? inputs = null)
    {
        bool thermonuclear = type is SupernovaType.Ia or SupernovaType.Ia91T or SupernovaType.Ia91bg or SupernovaType.Iax;
        var x = inputs ?? (thermonuclear ? LabInputs.Default with { Kind = StarKind.WhiteDwarf } : LabInputs.Default);
        double ej = ejectaMass ?? 0;
        var star = new PresupernovaStar(Q(ej + 1.5, "M☉"), Q(ej / 2 + 1.5, "M☉"), Q(ej / 4 + 1.5, "M☉"), Q(ej / 2, "M☉"),
            Q(ej / 4, "M☉"), Q(radiusCm, "cm"), Q(5000, "K"), Q(1e-6, "M☉/yr"), Q(10, "km/s"), false);
        return new Outcome(x, fate, type, hasGrb, remnant ?? (thermonuclear ? Remnant.None : NeutronStar(1.4)), star,
            energyErg is double e ? Q(e, "erg") : null,
            ejectaMass is double m ? Q(m, "M☉") : null,
            nickelMass is double ni ? Q(ni, "M☉") : null,
            null, null, null, Array.Empty<string>(), false);
    }
}
