using System.Globalization;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Text;

/// <summary>One displayed quantity of an outcome: the group it is listed under, its label and glossary key.</summary>
public sealed record OutcomeQuantity(string Group, string Label, string GlossaryKey, Quantity Quantity);

/// <summary>
/// The outcome's plain-language texts (spec §3): names of types, fates and remnants, the one-sentence summary, the label of
/// every quantity, and the formatting of values for the UI boundary.
/// </summary>
public static class OutcomeTexts
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public const string ExplosionGroup = "Explosion", BurstGroup = "Gamma-ray burst", RemnantGroup = "What is left";

    // ---------------------------------------------------------------- names

    /// <summary>"Type II-P" and so on; null for <see cref="SupernovaType.None"/>.</summary>
    public static string? TypeName(SupernovaType type) => type switch
    {
        SupernovaType.None => null,
        SupernovaType.IIP => "Type II-P",
        SupernovaType.IIL => "Type II-L",
        SupernovaType.IIb => "Type IIb",
        SupernovaType.IIn => "Type IIn",
        SupernovaType.Ib => "Type Ib",
        SupernovaType.Ic => "Type Ic",
        SupernovaType.IcBL => "Type Ic-BL",
        SupernovaType.Ibn => "Type Ibn",
        SupernovaType.Icn => "Type Icn",
        SupernovaType.SlsnI => "SLSN-I",
        SupernovaType.SlsnII => "SLSN-II",
        SupernovaType.Ia => "Type Ia",
        SupernovaType.Ia91T => "Type Ia (91T-like)",
        SupernovaType.Ia91bg => "Type Ia (91bg-like)",
        SupernovaType.Iax => "Type Iax",
        SupernovaType.Pisn => "Pair-instability SN",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    /// <summary>The glossary key explaining the type; null for <see cref="SupernovaType.None"/>.</summary>
    public static string? TypeGlossaryKey(SupernovaType type) => type switch
    {
        SupernovaType.None => null,
        SupernovaType.IIP => nameof(Glossary.TypeIIP),
        SupernovaType.IIL => nameof(Glossary.TypeIIL),
        SupernovaType.IIb => nameof(Glossary.TypeIIb),
        SupernovaType.IIn => nameof(Glossary.TypeIIn),
        SupernovaType.Ib => nameof(Glossary.TypeIb),
        SupernovaType.Ic => nameof(Glossary.TypeIc),
        SupernovaType.IcBL => nameof(Glossary.TypeIcBL),
        SupernovaType.Ibn => nameof(Glossary.TypeIbn),
        SupernovaType.Icn => nameof(Glossary.TypeIcn),
        SupernovaType.SlsnI => nameof(Glossary.SlsnI),
        SupernovaType.SlsnII => nameof(Glossary.SlsnII),
        SupernovaType.Ia => nameof(Glossary.TypeIa),
        SupernovaType.Ia91T => nameof(Glossary.TypeIa91T),
        SupernovaType.Ia91bg => nameof(Glossary.TypeIa91bg),
        SupernovaType.Iax => nameof(Glossary.TypeIax),
        SupernovaType.Pisn => nameof(Glossary.Pisn),
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    public static string FateName(Fate fate) => fate switch
    {
        Fate.NotDeadYet => "Not dead yet",
        Fate.WhiteDwarf => "No supernova",
        Fate.Supernova => "Supernova",
        Fate.FailedSupernova => "Failed supernova",
        Fate.PairInstabilitySupernova => "Pair-instability supernova",
        Fate.PulsationalPairInstability => "Pulsational pair instability",
        _ => throw new ArgumentOutOfRangeException(nameof(fate)),
    };

    public static string FateGlossaryKey(Fate fate) => fate switch
    {
        Fate.NotDeadYet => nameof(Glossary.NotDeadYet),
        Fate.WhiteDwarf => nameof(Glossary.WhiteDwarf),
        Fate.Supernova => nameof(Glossary.Supernova),
        Fate.FailedSupernova => nameof(Glossary.FailedSupernova),
        Fate.PairInstabilitySupernova => nameof(Glossary.PairInstability),
        Fate.PulsationalPairInstability => nameof(Glossary.PulsationalPairInstability),
        _ => throw new ArgumentOutOfRangeException(nameof(fate)),
    };

    public static string RemnantName(RemnantKind kind) => kind switch
    {
        RemnantKind.None => "No remnant",
        RemnantKind.WhiteDwarf => "White dwarf",
        RemnantKind.NeutronStar => "Neutron star",
        RemnantKind.Magnetar => "Magnetar",
        RemnantKind.BlackHole => "Black hole",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>The glossary key explaining the remnant; null for no remnant.</summary>
    public static string? RemnantGlossaryKey(RemnantKind kind) => kind switch
    {
        RemnantKind.None => null,
        RemnantKind.WhiteDwarf => nameof(Glossary.WhiteDwarf),
        RemnantKind.NeutronStar => nameof(Glossary.NeutronStar),
        RemnantKind.Magnetar => nameof(Glossary.Magnetar),
        RemnantKind.BlackHole => nameof(Glossary.BlackHole),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>
    /// What t = 0 is, for "3 days after …": ignition for a white dwarf, core bounce for a collapse, the explosion for a
    /// pair-instability supernova (no bounce), and "the start" when the star does not die.
    /// </summary>
    public static string TimeOrigin(Outcome o)
    {
        if (o.Inputs.Kind == StarKind.WhiteDwarf) return "ignition";
        return o.Fate switch
        {
            Fate.Supernova or Fate.FailedSupernova or Fate.PulsationalPairInstability => "core bounce",
            Fate.PairInstabilitySupernova => "the explosion",
            _ => "the start",
        };
    }

    // ---------------------------------------------------------------- the one-sentence summary

    /// <summary>A one-sentence, plain-language summary of the outcome (spec §3).</summary>
    public static string Summary(Outcome o)
    {
        string leaves = o.Remnant.Kind switch
        {
            RemnantKind.NeutronStar => "leaving a neutron star",
            RemnantKind.Magnetar => "leaving a magnetar",
            RemnantKind.BlackHole => "leaving a black hole",
            RemnantKind.WhiteDwarf => "leaving part of the white dwarf behind",
            _ => "leaving nothing behind",
        };
        bool thermonuclear = o.Inputs.Kind == StarKind.WhiteDwarf;
        return o.Fate switch
        {
            Fate.NotDeadYet => "A star this light lives longer than the universe is old, so it has not died yet.",
            Fate.WhiteDwarf => "The star sheds its outer layers and its core settles into a white dwarf: there is no supernova.",
            Fate.FailedSupernova =>
                "The core collapses but the explosion fails: the star falls into a black hole, with a burst of neutrinos but little light.",
            Fate.PairInstabilitySupernova => "Pair instability blows the whole star apart, leaving nothing behind.",
            Fate.PulsationalPairInstability =>
                "Pair-instability pulses throw off shells of gas, and then the core collapses to a black hole.",
            Fate.Supernova when thermonuclear =>
                $"Runaway carbon burning blows the white dwarf apart in {TypePhrase(o.Type)}, {leaves}.",
            Fate.Supernova =>
                $"The core collapses and the star explodes as {TypePhrase(o.Type)}, {leaves}" +
                (o.HasGrb ? ", and its jets launch a long gamma-ray burst." : "."),
            _ => throw new ArgumentOutOfRangeException(nameof(o)),
        };
    }

    /// <summary>"a Type II-P supernova"; superluminous classes read "an SLSN-I (superluminous supernova)".</summary>
    private static string TypePhrase(SupernovaType type) => type switch
    {
        SupernovaType.SlsnI or SupernovaType.SlsnII => $"an {TypeName(type)} (superluminous supernova)",
        _ => $"a {TypeName(type)} supernova",
    };

    /// <summary>
    /// Whether an engine note says an input had no effect (spec §9: such a note is shown in the outcome card). It matches
    /// the engine's wordings: "has no effect", "have no effect", "does not change this outcome", "is ignored", "is/are
    /// not used".
    /// </summary>
    public static bool IsInputNote(string note)
        => note.Contains("no effect", StringComparison.Ordinal) ||
           note.Contains("does not change this outcome", StringComparison.Ordinal) ||
           note.Contains("is ignored", StringComparison.Ordinal) ||
           note.Contains("not used", StringComparison.Ordinal);

    // ---------------------------------------------------------------- every quantity

    /// <summary>
    /// Every non-null quantity of the outcome, in display order, with its group, label and glossary key: the explosion,
    /// the gamma-ray burst, the remnant and the star. Ruling R34: <see cref="Outcome.PeakLuminosityErgS"/> is labelled
    /// as the engine's classification estimate, and the Type Ia peak B magnitude as an estimate from it (ruling R53), never
    /// as the peak brightness (which comes from the drawn light curve, as does Δm15(B)).
    /// </summary>
    public static IReadOnlyList<OutcomeQuantity> Quantities(Outcome o)
    {
        var list = new List<OutcomeQuantity>();
        void Add(string group, string label, string key, Quantity? q)
        {
            if (q is not null) list.Add(new OutcomeQuantity(group, label, key, q));
        }

        const string ex = ExplosionGroup;
        Add(ex, "Explosion energy", nameof(Glossary.ExplosionEnergy), o.ExplosionEnergyErg);
        Add(ex, "Ejecta mass", nameof(Glossary.EjectaMass), o.EjectaMass);
        Add(ex, "⁵⁶Ni mass (radioactive nickel)", nameof(Glossary.Nickel56), o.NickelMass);
        Add(ex, "⁴⁴Ti mass (radioactive titanium)", nameof(Glossary.Titanium44), o.Ti44Mass);
        Add(ex, ClassificationLuminosityLabel(o), nameof(Glossary.ClassificationLuminosity), o.PeakLuminosityErgS);
        Add(ex, "Magnetar spin energy", nameof(Glossary.MagnetarEnergy), o.MagnetarEnergyErg);
        Add(ex, "Magnetar spin-down time", nameof(Glossary.MagnetarTimescale), o.MagnetarTimescaleS);
        Add(ex, "Peak absolute B magnitude, estimated from the engine's peak luminosity (BC_B ≈ 0; it does not shape the " +
                "light curve)", nameof(Glossary.PhillipsRelation), o.PeakAbsoluteMagB);

        const string grb = BurstGroup;
        Add(grb, "Isotropic-equivalent energy", nameof(Glossary.GrbIsotropicEnergy), o.GrbIsotropicEnergyErg);
        Add(grb, "Peak photon energy (rest frame)", nameof(Glossary.GrbPeakEnergy), o.GrbPeakEnergyKeV);
        Add(grb, "Duration T90", nameof(Glossary.T90), o.GrbT90S);

        const string rem = RemnantGroup;
        var r = o.Remnant;
        string massLabel = r.Kind switch
        {
            RemnantKind.NeutronStar => "Neutron star mass (gravitational)",
            RemnantKind.Magnetar => "Magnetar mass (gravitational)",
            RemnantKind.BlackHole => "Black hole mass",
            RemnantKind.WhiteDwarf => "White dwarf mass",
            _ => "Remnant mass",
        };
        Add(rem, massLabel, nameof(Glossary.RemnantMass), r.Mass);
        Add(rem, "Radius", nameof(Glossary.NeutronStarRadius), r.RadiusKm);
        Add(rem, "Spin period at birth", nameof(Glossary.SpinPeriod), r.SpinPeriodS);
        Add(rem, "Surface magnetic field", nameof(Glossary.MagneticField), r.MagneticFieldG);
        Add(rem, "Kick velocity", nameof(Glossary.Kick), r.KickKmS);
        Add(rem, "Black hole forms at", nameof(Glossary.BlackHoleForms), r.FormationTimeS);
        Add(rem, "Pulsar radio luminosity (1.4 GHz)", nameof(Glossary.PulsarRadioLuminosity), o.PulsarRadioLuminosityMjyKpc2);

        if (o.Star is { } s)
        {
            string star = StarGroup(o);
            Add(star, o.Fate == Fate.NotDeadYet ? "Mass" : "Final mass", nameof(Glossary.FinalMass), s.FinalMass);
            Add(star, "Helium core mass", nameof(Glossary.HeliumCore), s.HeCoreMass);
            Add(star, "Carbon-oxygen core mass", nameof(Glossary.CarbonOxygenCore), s.CoCoreMass);
            Add(star, "Hydrogen-rich envelope mass", nameof(Glossary.HydrogenEnvelope), s.HydrogenEnvelopeMass);
            Add(star, "Helium layer mass", nameof(Glossary.HeliumLayer), s.HeliumEnvelopeMass);
            Add(star, "Radius", nameof(Glossary.StarRadius), s.RadiusCm);
            Add(star, "Surface temperature", nameof(Glossary.SurfaceTemperature), s.SurfaceTemperatureK);
            Add(star, "Wind mass-loss rate", nameof(Glossary.MassLossRate), s.MassLossRateMsunPerYr);
            Add(star, "Wind speed", nameof(Glossary.WindSpeed), s.WindSpeedKmS);
        }
        return list;
    }

    /// <summary>The heading of the star's quantities: the star now, or the star when it dies.</summary>
    public static string StarGroup(Outcome o) => o.Fate == Fate.NotDeadYet ? "The star now" : "The star when it dies";

    /// <summary>
    /// Ruling R34: <see cref="Outcome.PeakLuminosityErgS"/> is the engine's classification estimate, labelled by what it
    /// is: the plateau luminosity for plateau supernovae, otherwise an estimate. The light curve's own peak can differ.
    /// </summary>
    private static string ClassificationLuminosityLabel(Outcome o)
        => o.Type is SupernovaType.IIP
            ? "Plateau luminosity (engine estimate for the type label)"
            : "Luminosity estimate (engine estimate for the type label)";

    // ---------------------------------------------------------------- formatting

    /// <summary>The quantity's value with its unit, formatted for the UI.</summary>
    public static string Format(Quantity q) => Format(q.Value, q.Unit);

    /// <summary>"low – high" in the quantity's unit, or null when it has no range.</summary>
    public static string? FormatRange(Quantity q)
        => q is { Low: double lo, High: double hi } ? $"{Format(lo, q.Unit)} – {Format(hi, q.Unit)}" : null;

    /// <summary>A value in one of the outcome's units, formatted for the UI.</summary>
    public static string Format(double value, string unit)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return "not available";
        return unit switch
        {
            "M☉" => value != 0 && Math.Abs(value) < 0.01 ? UnitFormat.Scientific(value, unit) : UnitFormat.Mass(value),
            "erg" => UnitFormat.Energy(value),
            "erg/s" or "G" => UnitFormat.Scientific(value, unit),
            "M☉/yr" => value == 0 ? "0 M☉/yr" : UnitFormat.Scientific(value, unit),
            "s" => UnitFormat.Time(value),
            "cm" => $"{UnitFormat.Number(value / PhysicalConstants.RSun)} R☉",
            "mag" => Magnitude(value),
            "K" or "km" or "km/s" or "keV" => $"{UnitFormat.Number(value)} {unit}",
            _ => $"{UnitFormat.Figure(value)} {unit}",
        };
    }

    /// <summary>A magnitude to two decimals, with a true minus sign: "−17.52".</summary>
    public static string Magnitude(double mag)
        => mag.ToString("0.00", Inv).Replace('-', '−');
}
