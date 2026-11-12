using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Outcomes;

/// <summary>
/// The Type Ia explosion of a white dwarf: class, kinetic energy (erg), ejecta and ⁵⁶Ni (M☉), and remnant.
/// <see cref="SubtypeNote"/> is the note that gives the ⁵⁶Ni and the subtype (null for Iax), which a ⁵⁶Ni pin rewrites.
/// </summary>
public sealed record TypeIaExplosion(SupernovaType Type, Quantity Energy, Quantity Ejecta, Quantity Nickel, Remnant Remnant)
{
    public string? SubtypeNote { get; init; }
}

/// <summary>
/// Outcomes §8.2 step 9 (with §7.2–§7.4): the Type Ia path for a white dwarf of the chosen mass and channel. The peak
/// luminosity (Arnett's rule) is added by <see cref="OutcomeEngine"/>. Pure.
/// </summary>
public static class TypeIaModel
{
    private const string MSun = "M☉";
    private const string Refs = "outcomes §7.2–§7.4, §8.2 step 9";

    // Step 9, near-Chandrasekhar: M_ej = 1.38, M_Ni 0.4–0.8 (default 0.6).
    private const double ChandrasekharEjecta = 1.38, NearChNickel = 0.6;
    // Step 9, sub-Chandrasekhar: Sim et al. 2010, 0.97 M☉ → 0.3 M☉ and 1.15 M☉ → 0.8 M☉ of ⁵⁶Ni; §7.3 has the yield fall
    // steeply below 0.97 toward ~0.05 M☉ at ~0.9 M☉.
    private const double SimLowMass = 0.97, SimLowNickel = 0.3, SimHighMass = 1.15, SimHighNickel = 0.8;
    private const double SteepMass = 0.9, SteepNickel = 0.05;
    // Step 9 subtypes: 91T-like if M_Ni ≥ 0.8; 91bg-like if M_Ni ≲ 0.1 (APPROXIMATION boundary).
    private const double Overluminous = 0.8, Subluminous = 0.1;
    // Kromer et al. 2013 (MNRAS 429, 2287) model N5def, a pure deflagration of a 1.40 M☉ white dwarf, checked against
    // the paper (arXiv:1210.5243, Table 2 and text): 0.372 M☉ ejected with 0.158 M☉ of ⁵⁶Ni and 1.34×10⁵⁰ erg, leaving a
    // bound remnant of ~1.03 M☉.
    private const double IaxEjecta = 0.372, IaxNickel = 0.158, IaxEnergy = 1.34e50;

    private static readonly Source Kromer13 =
        new("Kromer et al. 2013, MNRAS 429, 2287 (deflagration model N5def)", Refs);

    // Step 9: M_bol = 4.74 − 2.5 log10(L_peak / 3.828×10³³ erg/s), and M_B ≈ M_bol (BC_B ≈ 0 at peak, APPROXIMATION).
    private const double MbolSun = 4.74;

    /// <summary>
    /// Step 9: peak absolute B magnitude from the peak luminosity, M_B ≈ M_bol = 4.74 − 2.5 log10(L / L☉). Speculative:
    /// BC_B ≈ 0 is an approximation.
    /// </summary>
    public static Quantity PeakAbsoluteMagB(Quantity peakLuminosity)
        => new(MbolSun - 2.5 * Math.Log10(peakLuminosity.Value / PhysicalConstants.LSun), "mag", Confidence.Speculative,
            new Source("M_bol = 4.74 − 2.5 log10(L_peak / L☉) with BC_B ≈ 0 at peak [APPROX]; L_peak from " +
                       peakLuminosity.Source.Citation, "outcomes §7.2, §8.2 step 9"));

    // Ruling R53 (I5): the inverted Phillips relation (M_B,0 = −19.3) that gave Δm15(B) here is gone. Arnett's −19.0 for
    // a normal 0.6 M☉ Ia made every normal Ia a fast decliner (Δm15 ≈ 1.41); Δm15(B) is now measured on the drawn light
    // curve (LightCurveModel.DeclineRateDm15B).

    public static TypeIaExplosion Explode(LabInputs inputs, List<string> notes)
    {
        var x = inputs.Clamped();
        return x.IaChannel == TypeIaChannel.Iax ? Iax(x.WhiteDwarfMass, notes) : Disrupted(x, notes);
    }

    /// <summary>Near- and sub-Chandrasekhar explosions: the white dwarf is destroyed, leaving no remnant.</summary>
    private static TypeIaExplosion Disrupted(LabInputs x, List<string> notes)
    {
        bool nearCh = x.IaChannel == TypeIaChannel.NearChandrasekhar;
        double mwd = x.WhiteDwarfMass;
        Quantity ejecta, nickel;
        if (nearCh)
        {
            ejecta = new Quantity(ChandrasekharEjecta, MSun, Confidence.ModelDependent,
                new Source("Near-Chandrasekhar models, M_ej ≈ 1.38–1.4 M☉ (Nomoto, Thielemann & Yokoi 1984, ApJ 286, 644)", Refs));
            nickel = new Quantity(NearChNickel, MSun, Confidence.ModelDependent,
                new Source("Stritzinger et al. 2006, A&A 460, 793 (normal SNe Ia 0.4–0.8 M☉; default 0.6 M☉)", Refs), 0.4, 0.8);
            notes.Add("The white dwarf takes matter from its companion until it nears the Chandrasekhar mass (1.38 M☉). " +
                      "Carbon ignites at its center and the whole star is blown apart: a Type Ia supernova, with nothing left behind.");
        }
        else
        {
            ejecta = new Quantity(mwd, MSun, Confidence.ModelDependent,
                new Source("Sim et al. 2010, ApJL 714, L52 (sub-Chandrasekhar detonation, M_ej = M_WD)", Refs));
            nickel = SubChandrasekharNickel(mwd);
            notes.Add($"The {UnitFormat.Mass(mwd)} white dwarf detonates below the Chandrasekhar mass (after merging with " +
                      "another white dwarf, or when a helium layer on its surface ignites first) and is blown apart, with " +
                      "nothing left behind.");
        }

        // Ruling R17: thermonuclear explosions keep the published yields, without the core-collapse cap Ni ≤ 0.5 M_ej
        // (SNe Ia are ~45–70% ⁵⁶Ni by mass). The sanity bound Ni < M_ej holds by construction: 0.6 < 1.38 near M_Ch;
        // below 0.97 the steep fall stays under 0.3 < M_WD; on the Sim line Ni < M_WD up to 1.15 (0.8 < 1.15); above 1.15
        // Ni is held at 0.8.

        // [APPROX] Step 9: E_K ≈ 1.2–1.5 B from W7-type models; the middle is taken.
        var energy = new Quantity(1.35e51, "erg", Confidence.Speculative,
            new Source("W7-type models, E_K ≈ 1.2–1.5 B (Nomoto, Thielemann & Yokoi 1984, ApJ 286, 644; Iwamoto et al. " +
                       "1999, ApJS 125, 439); middle of the range [APPROX]", Refs), 1.2e51, 1.5e51);

        var (type, note) = Subtype(nickel.Value, published: false);
        notes.Add(note);
        return new TypeIaExplosion(type, energy, ejecta, nickel, Remnant.None) { SubtypeNote = note };
    }

    /// <summary>
    /// Step 9's subtype for this ⁵⁶Ni mass (M☉): 91T-like at ≥ 0.8, 91bg-like at ≤ 0.1, else normal; and the sentence that
    /// gives it, marked as published when a preset pinned the ⁵⁶Ni (ruling R53).
    /// </summary>
    internal static (SupernovaType Type, string Note) Subtype(double nickel, bool published)
    {
        string ni = UnitFormat.Mass(nickel), pub = published ? " (the published value)" : "";
        return nickel >= Overluminous
            ? (SupernovaType.Ia91T, $"With {ni} of nickel{pub} it is unusually bright, like SN 1991T. Real events like it also fade slowly; the lab's drawn light curve does not reproduce that.")
            : nickel <= Subluminous
                ? (SupernovaType.Ia91bg, $"With only {ni} of nickel{pub} it is faint and red, like SN 1991bg. Real events like it also fade quickly; the lab's drawn light curve does not reproduce that.")
                : (SupernovaType.Ia, $"It makes {ni} of radioactive nickel{pub}, whose decay powers its light.");
    }

    /// <summary>
    /// Sub-Chandrasekhar ⁵⁶Ni (step 9, §7.3): linear in M_WD between Sim et al.'s 0.97 → 0.3 and 1.15 → 0.8 M☉
    /// (ModelDependent). [APPROX] Below 0.97 the yield "falls steeply": log-linear through ~0.05 M☉ at ~0.9 M☉, extended to
    /// the 0.85 M☉ slider end. [APPROX] Above 1.15, Sim et al.'s heaviest model, the yield is held at its 0.8 M☉ rather
    /// than extrapolated (the line would reach M_ej near 1.35 M☉). Both Speculative.
    /// </summary>
    private static Quantity SubChandrasekharNickel(double mwd)
    {
        var sim = new Source("Sim et al. 2010, ApJL 714, L52 (0.97 M☉ → 0.3 M☉, 1.15 M☉ → 0.8 M☉ of ⁵⁶Ni)", Refs);
        if (mwd < SimLowMass)
        {
            double slope = (Math.Log10(SimLowNickel) - Math.Log10(SteepNickel)) / (SimLowMass - SteepMass);
            double ni = Math.Pow(10, Math.Log10(SteepNickel) + slope * (mwd - SteepMass));
            return new Quantity(ni, MSun, Confidence.Speculative, sim with
            {
                Citation = sim.Citation + "; below 0.97 M☉ falling steeply, log-linear through ~0.05 M☉ at ~0.9 M☉ [APPROX]",
            });
        }
        if (mwd > SimHighMass)
            return new Quantity(SimHighNickel, MSun, Confidence.Speculative,
                sim with { Citation = sim.Citation + ", held at the 1.15 M☉ value above it [APPROX]" });
        double linear = SimLowNickel + (SimHighNickel - SimLowNickel) * (mwd - SimLowMass) / (SimHighMass - SimLowMass);
        return new Quantity(linear, MSun, Confidence.ModelDependent, sim with { Citation = sim.Citation + ", interpolated" });
    }

    /// <summary>
    /// Iax (step 9, §7.4): a deflagration that ejects part of the star and leaves a bound white dwarf. Kromer et al.'s
    /// N5def values; the remnant is the rest of the white dwarf. [APPROX] N5def is a near-Chandrasekhar model; for a
    /// lighter white dwarf the same ejecta are assumed (Speculative, with a note).
    /// </summary>
    private static TypeIaExplosion Iax(double mwd, List<string> notes)
    {
        bool nearCh = mwd >= ChandrasekharEjecta;
        var conf = nearCh ? Confidence.ModelDependent : Confidence.Speculative;
        var src = nearCh ? Kromer13 : Kromer13 with
        {
            Citation = Kromer13.Citation + "; a 1.40 M☉ model applied to a lighter white dwarf [APPROX]",
        };
        var ejecta = new Quantity(IaxEjecta, MSun, conf, src, 0.3, 0.4);
        var nickel = new Quantity(IaxNickel, MSun, conf,
            src with { Citation = src.Citation + "; Iax range 0.003–0.3 M☉ (Jha 2017, arXiv:1707.01110)" }, 0.003, 0.3);
        // The research files give no Iax kinetic energy; N5def's (checked against the paper) is used, Speculative.
        var energy = new Quantity(IaxEnergy, "erg", Confidence.Speculative,
            src with { Citation = src.Citation + "; Iax E_K not in the research files [APPROX]" });
        var remnantMass = new Quantity(mwd - IaxEjecta, MSun, conf,
            src with { Citation = src.Citation + "; bound remnant = M_WD − M_ej" });

        notes.Add($"The burning front fizzles before it can unbind the star: it throws off only {UnitFormat.Mass(IaxEjecta)} " +
                  $"and leaves a {UnitFormat.Mass(remnantMass.Value)} white dwarf behind. This is a faint Type Iax supernova " +
                  "(like SN 2002cx).");
        if (!nearCh)
            notes.Add("Type Iax models start from a white dwarf near the Chandrasekhar mass; for this lighter white dwarf " +
                      "the same ejected mass is assumed, which is an approximation.");
        return new TypeIaExplosion(SupernovaType.Iax, energy, ejecta, nickel, RemnantModel.WhiteDwarf(remnantMass));
    }
}
