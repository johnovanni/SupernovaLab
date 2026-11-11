using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Progenitors;

/// <summary>
/// Outcomes §8.2 steps 0–3: has the star died, the low-mass branch (white dwarf, electron capture), the presupernova
/// structure, and the pair-instability regime. Pure and deterministic.
/// </summary>
public static class ProgenitorModel
{
    private const string MSun = "M☉";

    // Step 0 and 1 boundaries (outcomes §1.1, §8.2 steps 0–1).
    private const double NoDeathBelow = 0.9;
    // The ECSN window is 9.0–9.25 M☉ at Z☉ (Poelarends et al. 2008). §8.2 step 1 quotes M_up = 8.0 but also places the
    // window at "M_up ≤ M < M_up + 0.2 (default 9.0–9.25)", and S16's lightest iron-core collapse is 9.0 M☉; so stars
    // below 9.0 M☉ end as white dwarfs (H03's ~9 M☉ WD/collapse boundary, ±1 M☉ per §8.3).
    private const double EcsnLow = 9.0, EcsnHigh = 9.25;
    private const double IfmrMistTop = 7.2;

    // Step 3 He-core boundaries at death (outcomes §2.3, §8.2 step 3), ±5 M☉.
    private const double PpisnHeCore = 35, PisnHeCore = 64, PisnTopHeCore = 133;
    // Ruling R12, outcomes §2.3: a PISN from an H-rich very massive star needs Z ≲ Z☉/3 (Langer et al. 2007). A stripped
    // (WC-type) progenitor needs "much lower Z"; no number is given. [APPROX] We take 0.1 Z☉, the metallicity below which
    // §2.2 step 4 has stars stay compact and keep their mass, an order of magnitude below the H-rich ceiling.
    private const double PisnMaxZHydrogenRich = 1.0 / 3, PisnMaxZStripped = 0.1;

    // §2.2 / §8.2 step 2 wind exponents: (Z/Z☉)^0.5 below 30 M☉ (H03), ^0.85 at and above (Vink et al. 2001).
    private const double HeavyStarMass = 30;
    private const double AlphaLight = 0.5, AlphaHeavy = 0.85;
    // [APPROX] §2.2 step 3 scales He-star erosion by (Z/Z☉)^0.25–0.6. We take 0.6, the WNE iron exponent (W19 from
    // Yoon 2017): a freshly bared He star starts as a WN star. The same exponent scales the WR wind rate below.
    private const double BetaHeStar = 0.6;
    // §2.2 step 4: low-Z stars die as blue supergiants below ~0.1 Z☉.
    private const double BlueSupergiantMaxZ = 0.1;
    // H03 (outcomes §2.1, §3.2): a II-P (extended red supergiant) needs an H-rich envelope of ≳ 2 M☉.
    private const double ExtendedEnvelopeMinMass = 2.0;
    // §5.1 / §8.2 step 7a CHE option: extreme rotation, Z ≤ 0.004 (≈ 0.3 Z☉), 20 ≲ M ≲ 60 (upper bound APPROXIMATION).
    private const double CheMaxZ = 0.3, CheMinMass = 20, CheMaxMass = 60;
    // Ruling R14: stripped stars up to 30 M☉ take S16's Z-independent cores (outcomes §2.2 step 1) and keep a He shell
    // of at least 0.16 M☉ when Full-stripped (Yoon, Woosley & Langer 2010, outcomes §3.2) [APPROX].
    private const double StrippedS16CoresTop = 30, MinStrippedHeShell = 0.16;

    /// <summary>
    /// Hydrogen mass fraction X_H of the H-rich envelope (outcomes §1.3, APPROXIMATION): 0.6 for 9–20 M☉, 0.5 above (the
    /// research gives 0.5 for 20–33 M☉; [APPROX] it is held at 0.5 for heavier stars). Hydrogen mass ≈ X_H × the
    /// <see cref="PresupernovaStar.HydrogenEnvelopeMass"/>.
    /// </summary>
    public static double HydrogenMassFraction(double initialMass) => initialMass <= 20 ? 0.6 : 0.5;

    /// <summary>Evolves the (clamped) inputs to the star's state at death and its early fate.</summary>
    public static (EarlyFate Fate, PresupernovaStar Star, IReadOnlyList<string> Notes) Evolve(LabInputs inputs, ExplodabilityTable table)
    {
        var x = inputs.Clamped();
        var notes = new List<string>();
        if (x != inputs)
            notes.Add("Some inputs were outside the lab's ranges and were moved to the nearest allowed value.");

        if (x.Kind == StarKind.WhiteDwarf)
        {
            notes.Add($"This is the Type Ia path: the star is the {UnitFormat.Mass(x.WhiteDwarfMass)} white dwarf you chose, " +
                      "in a binary.");
            var src = new Source("Lab input (white-dwarf mass); Maoz, Mannucci & Nelemans 2014, ARA&A 52, 107", "outcomes §7.1, §8.2 step 9");
            return (EarlyFate.WhiteDwarf, WhiteDwarfStar(x.WhiteDwarfMass, Confidence.Established, src), notes);
        }

        double m = x.InitialMass;
        if (m < NoDeathBelow)
        {
            notes.Add($"A star of {UnitFormat.Mass(m)} lives longer than the universe is old, so it has not died yet.");
            NoteIgnoredStripping(x, notes);
            return (EarlyFate.NotDeadYet, MainSequenceStar(m), notes);
        }

        if (m < EcsnLow)
        {
            var (mwd, conf, src) = InitialFinalMass(m);
            notes.Add($"A star of {UnitFormat.Mass(m)} sheds its outer layers and ends as a {UnitFormat.Mass(mwd)} white dwarf; " +
                      "there is no supernova.");
            NoteIgnoredStripping(x, notes);
            return (EarlyFate.WhiteDwarf, WhiteDwarfStar(mwd, conf, src), notes);
        }

        bool ecsn = m < EcsnHigh;
        // Step 1: the ECSN star's envelope and radius come from the 9 M☉ row of §1.3.
        double structureMass = ecsn ? EcsnLow : m;
        bool che = x.Rotation == Rotation.Extreme && x.Metallicity <= CheMaxZ && m >= CheMinMass && m <= CheMaxMass;

        var s = x.Stripping == Stripping.None
            ? Unstripped(structureMass, x.Metallicity, table, notes)
            : Stripped(structureMass, x.Metallicity, x.Stripping, ecsn, table);
        if (che)
        {
            s = ChemicallyHomogeneous(s);
            notes.Add("With extreme rotation at low metal content the star mixes completely as it burns (chemically " +
                      "homogeneous evolution), so it turns almost all of itself into helium and keeps no hydrogen envelope.");
        }

        EarlyFate fate;
        if (ecsn)
        {
            fate = EarlyFate.ElectronCapture;
            notes.Add($"A star of {UnitFormat.Mass(m)} sits in the narrow electron-capture window (9.0–9.25 M☉): its " +
                      "oxygen-neon-magnesium core collapses when electrons are captured onto its nuclei.");
        }
        else
        {
            fate = PairInstabilityFate(s.HeCore);
            bool hydrogenRich = x.Stripping == Stripping.None && s.HydrogenEnvelope > 0;
            double ceiling = hydrogenRich ? PisnMaxZHydrogenRich : PisnMaxZStripped;
            if (fate == EarlyFate.PairInstability && x.Metallicity > ceiling)
            {
                fate = EarlyFate.CoreCollapse;
                notes.Add($"Its helium core of {UnitFormat.Mass(s.HeCore)} lies in the pair-instability range, but " +
                          "pair-instability explosions need low metal content (below about " +
                          (hydrogenRich ? "a third of the Sun's" : "a tenth of the Sun's, an approximate limit") +
                          " for this kind of star), so here the core collapses to a black hole instead.");
            }
            else notes.Add(FateNote(fate, m, s.HeCore));
        }

        return (fate, BuildStar(s, x.Metallicity, che), notes);
    }

    // ---------------------------------------------------------------- step 3

    private static EarlyFate PairInstabilityFate(double heCore) => heCore switch
    {
        > PisnTopHeCore => EarlyFate.DirectCollapseAbovePisn,
        >= PisnHeCore => EarlyFate.PairInstability,
        >= PpisnHeCore => EarlyFate.PulsationalPairInstability,
        _ => EarlyFate.CoreCollapse,
    };

    private static string FateNote(EarlyFate fate, double m, double heCore)
    {
        string he = UnitFormat.Mass(heCore);
        return fate switch
        {
            EarlyFate.DirectCollapseAbovePisn =>
                $"Its helium core of {he} is above the pair-instability range (over 133 M☉): the core collapses straight " +
                "to a black hole without exploding.",
            EarlyFate.PairInstability =>
                $"Its helium core of {he} is in the pair-instability range (64–133 M☉): the core becomes unstable and " +
                "the whole star is blown apart, leaving nothing behind.",
            EarlyFate.PulsationalPairInstability =>
                $"Its helium core of {he} is in the pulsational pair-instability range (about 35–64 M☉): pulses throw " +
                "off mass before the core collapses to a black hole.",
            _ => $"The iron core of this {UnitFormat.Mass(m)} star collapses at the end of its life.",
        };
    }

    // ---------------------------------------------------------------- steps 0–1: stars that do not explode

    private static void NoteIgnoredStripping(LabInputs x, List<string> notes)
    {
        if (x.Stripping != Stripping.None)
            notes.Add("Companion stripping is ignored here: it only matters for stars that end in a supernova.");
    }

    /// <summary>Cummings et al. 2018 IFMR (outcomes §1.1): MIST fit to 7.2 M☉, then the PARSEC slope.</summary>
    private static (double Mass, Confidence Confidence, Source Source) InitialFinalMass(double m)
    {
        double mistTop = 0.107 * IfmrMistTop + 0.471; // 1.2414 M☉
        if (m <= IfmrMistTop)
        {
            double mwd = m < 2.85 ? 0.080 * m + 0.489
                       : m < 3.60 ? 0.187 * m + 0.184
                       : 0.107 * m + 0.471;
            return (mwd, Confidence.Established,
                new Source("Cummings et al. 2018, ApJ 866, 21 (MIST-based IFMR)", "outcomes §1.1, §8.2 step 1"));
        }
        // [APPROX] outcomes §1.1: above 7.2 M☉ extrapolate the PARSEC fit (slope 0.0835, valid to 8.2 M☉), here up to
        // 9.0 M☉. The PARSEC line itself sits 0.075 M☉ below the MIST line at 7.2 M☉, so it is offset to meet the MIST
        // value there and keep the white-dwarf mass continuous.
        return (mistTop + 0.0835 * (m - IfmrMistTop), Confidence.Speculative,
            new Source("Cummings et al. 2018, ApJ 866, 21 (PARSEC slope, offset and extrapolated) [APPROX]", "outcomes §1.1, §8.2 step 1"));
    }

    private static PresupernovaStar WhiteDwarfStar(double mwd, Confidence massConfidence, Source massSource)
    {
        var mass = Q(mwd, MSun, massConfidence, massSource);
        // [APPROX] A CO (or ONe) white dwarf: the whole star is core. Its thin surface H and He layers are neglected.
        var layers = new Source("[APPROX] white-dwarf surface layers neglected; Cummings et al. 2018, ApJ 866, 21", "outcomes §1.1");
        // [APPROX] Not in the research files. Nauenberg 1972 (ApJ 175, 417) mass–radius relation for a cold white dwarf,
        // R = 0.0112 R☉ [(M/M_Ch)^(-2/3) − (M/M_Ch)^(2/3)]^(1/2), M_Ch = 1.44 M☉.
        double mu = Math.Min(mwd / 1.44, 0.999);
        double radius = 0.0112 * PhysicalConstants.RSun * Math.Sqrt(Math.Pow(mu, -2.0 / 3) - Math.Pow(mu, 2.0 / 3));
        // [APPROX] Not in the research files. White dwarfs are born above 10⁵ K and cool for billions of years; 10⁴ K is
        // a typical effective temperature of an observed white dwarf, used only to color the star.
        var noValue = new Source("[APPROX] no value in the research files; order of magnitude for an observed white dwarf",
            "outcomes §1.1");
        return new PresupernovaStar(mass, mass, mass, Q(0, MSun, Confidence.Speculative, layers),
            Q(0, MSun, Confidence.Speculative, layers),
            Q(radius, "cm", Confidence.Speculative, new Source("Nauenberg 1972, ApJ 175, 417 [APPROX]", "outcomes §1.1")),
            Q(1e4, "K", Confidence.Speculative, noValue), NoWind(), NoWind(speed: true), false);
    }

    /// <summary>
    /// A star still on the main sequence (outcomes §1.1). [APPROX] It is described as it is now: its initial mass, no He
    /// or CO core yet, and the Sun's nominal radius and temperature (a 0.8–0.9 M☉ dwarf is close to the Sun). All
    /// Speculative, like the textbook lifetime scaling behind step 0.
    /// </summary>
    private static PresupernovaStar MainSequenceStar(double m)
    {
        var src = new Source("Main-sequence lifetime t_MS ≈ 10 Gyr (M/M☉)^-2.5 [APPROX, textbook scaling]", "outcomes §1.1, §8.2 step 0");
        var sun = new Source("IAU 2015 Resolution B3 nominal solar values (Prša et al. 2016, AJ 152, 41) [APPROX]", "signals §0.2");
        return new PresupernovaStar(Q(m, MSun, Confidence.Speculative, src), Q(0, MSun, Confidence.Speculative, src),
            Q(0, MSun, Confidence.Speculative, src), Q(m, MSun, Confidence.Speculative, src),
            Q(0, MSun, Confidence.Speculative, src),
            Q(PhysicalConstants.RSun, "cm", Confidence.Speculative, sun), Q(5772, "K", Confidence.Speculative, sun),
            NoWind(), NoWind(speed: true), false);
    }

    private static Quantity NoWind(bool speed = false) => Q(0, speed ? "km/s" : "M☉/yr", Confidence.Speculative,
        new Source("[APPROX] negligible wind for a dwarf star or white dwarf; no value in the research files", "signals §0.4"));

    // ---------------------------------------------------------------- step 2: presupernova structure

    /// <summary>Masses in M☉; the radius is a ready-made Quantity because its source differs by branch.</summary>
    private sealed record Structure(double Final, double HeCore, double CoCore, double HydrogenEnvelope, double HeliumEnvelope,
        Confidence MassConfidence, Source MassSource, Quantity Radius, bool BlueSupergiant);

    private static readonly Source S16Source =
        new("Sukhbold et al. 2016, ApJ 821, 38 (per-model data, doi:10.17617/1.b)", "outcomes §1.3, §8.2 step 2");
    private static readonly Source MetallicitySchemeSource =
        new("Heger et al. 2003, ApJ 591, 288; Vink et al. 2001, A&A 369, 574; Woosley 2019, ApJ 878, 49; " +
            "Sukhbold et al. 2016, ApJ 821, 38 [APPROX scheme anchored to S16 at Z☉]", "outcomes §2.2, §8.2 step 2");

    private static bool IsSolar(double z) => Math.Abs(z - 1) < 1e-9;

    private static Structure Unstripped(double m, double z, ExplodabilityTable table, List<string> notes)
    {
        // Ruling R1: the table answers only inside its own mass range (9–120 M☉), for single stars at Z☉.
        if (IsSolar(z) && m <= table.Rows[^1].InitialMass)
            return SolarTable(m, table);

        notes.Add(IsSolar(z)
            ? "Above 120 M☉ there are no solar-metal models, so the star's winds and cores are estimated from fits; treat these masses as rough."
            : "Away from the Sun's metal content the star's winds are scaled from the solar models; treat these masses as rough.");
        return MetallicityScheme(m, z, table, notes);
    }

    /// <summary>§1.3 / §8.2 step 2: interpolate the S16 per-model structure linearly between neighboring rows.</summary>
    private static Structure SolarTable(double m, ExplodabilityTable table)
    {
        var (lo, hi, f) = Bracket(table, m);
        double final = Lerp(lo.PreSnMass, hi.PreSnMass, f);
        double he = Lerp(lo.HeCoreMass, hi.HeCoreMass, f);
        double co = Lerp(lo.CoCoreMass, hi.CoCoreMass, f);
        return new Structure(final, he, co, Math.Max(0, final - he), Math.Max(0, he - co),
            Confidence.ModelDependent, S16Source, Q(TableRadius(table, m), "cm", Confidence.ModelDependent, S16Source), false);
    }

    /// <summary>
    /// outcomes §2.2 (APPROXIMATION, so Speculative), anchored so that it equals S16 at Z☉ and the final mass falls
    /// smoothly as Z rises.
    /// </summary>
    private static Structure MetallicityScheme(double m, double z, ExplodabilityTable table, List<string> notes)
    {
        var last = table.Rows[^1];
        bool inTable = m <= last.InitialMass;

        // Solar final mass M_f☉: the S16 table. [APPROX] Above 120 M☉ the 120 M☉ model's retained fraction (5%) is kept,
        // which is continuous with the table at 120 M☉.
        double finalSun = inTable ? Lerp(Bracket(table, m), r => r.PreSnMass) : m * last.PreSnMass / last.InitialMass;

        // [APPROX] §2.2 step 2 scales the wind loss as ΔM☉ (Z/Z☉)^α. Applied additively, that subtracts more than the
        // whole star at Z > Z☉ for M ≳ 30 M☉ (S16 strips 90–95% of them) and needs a separate bare-star rule that does not
        // reproduce S16 at Z☉. We use the same scaling on the mass-loss *rate* of a star whose wind is proportional to
        // its mass: M_f = M (M_f☉ / M)^((Z/Z☉)^α). It equals S16 at Z☉, gives M at Z → 0 (§2.2 step 4, no winds), equals
        // the additive form to first order when the loss is small, never goes negative, and falls monotonically with Z.
        double final = m * Math.Pow(finalSun / m, Math.Pow(z, m < HeavyStarMass ? AlphaLight : AlphaHeavy));
        // [APPROX] Floor above Z☉. The law above would strip S16's 6–7 M☉ Wolf–Rayet stars below a neutron star's mass
        // (100 M☉ at 2 Z☉: 0.6 M☉). Such a star is already a bare He star whose wind is a WR wind, and the WR rate scales
        // as (Z/Z☉)^0.6 (W19 WNE, §2.1); its final mass is allowed to shrink only in inverse proportion to that rate,
        // M_f ≥ M_f☉ (Z/Z☉)^−0.6. The larger of two continuous, falling curves is itself continuous and falling.
        if (z > 1) final = Math.Max(final, finalSun * Math.Pow(z, -BetaHeStar));

        // §2.2 step 1: cores independent of Z. Up to 30 M☉ the S16 table; above, the W19 He-core fit at He ignition
        // (S16's cores above ~45 M☉ have been eaten by Z☉ winds). [APPROX] Where the S16 presupernova core is the larger
        // (30–40 M☉, still H-rich at Z☉ and grown during He burning) it is kept, so the core is continuous at 30 M☉.
        double he, co;
        double heW19 = W19HeCore(m);
        double coW19 = HeStar.CoCore(heW19, out _);
        if (inTable)
        {
            var b = Bracket(table, m);
            he = Math.Max(Lerp(b, r => r.HeCoreMass), heW19);
            co = Math.Min(he, Math.Max(Lerp(b, r => r.CoCoreMass), coW19));
        }
        else
        {
            he = heW19;
            co = coW19;
        }

        if (final > he)
        {
            double envelope = final - he;
            Quantity radius;
            bool blue = false;
            if (envelope < ExtendedEnvelopeMinMass)
                radius = ThinEnvelopeRadius(envelope);
            else if (z <= BlueSupergiantMaxZ)
            {
                blue = true;
                // [APPROX] §2.2 step 4: R ~ 30–100 R☉ for Z ≲ 0.1 Z☉; 50 R☉ is inside SN 1987A's 30–60 R☉ progenitor.
                radius = Q(50 * PhysicalConstants.RSun, "cm", Confidence.Speculative,
                    new Source("Sukhbold et al. 2016 §2.2 (SN 1987A progenitor Sk −69°202) [APPROX]", "outcomes §2.2"),
                    30 * PhysicalConstants.RSun, 100 * PhysicalConstants.RSun);
                notes.Add("At low metal content the star dies as a compact blue supergiant rather than a red supergiant.");
            }
            else radius = RedSupergiantRadius(m, table);
            return new Structure(final, he, co, envelope, he - co, Confidence.Speculative, MetallicitySchemeSource, radius, blue);
        }

        // The wind removes the whole envelope and then eats into the He core. [APPROX] The CO core keeps its fraction of
        // the He core, so every mass stays continuous where the envelope runs out.
        notes.Add("Its winds blow away the whole hydrogen envelope, and then some of the helium, before it dies.");
        double bareCo = co * final / he;
        // [APPROX] A star that S16 already ends bare at Z☉ (45–120 M☉: M_f = M_He = M_CO, a He-poor WC/WO surface,
        // outcomes §1.3) keeps S16's CO-to-final ratio instead, so its CO core and He shell (and so its Ib/Ic class) are
        // continuous across Z☉ (Task 4 review, ruling R16).
        if (inTable)
        {
            var b = Bracket(table, m);
            double finalS16 = Lerp(b, r => r.PreSnMass);
            if (Lerp(b, r => r.HydrogenEnvelopeMass) <= 0)
                bareCo = final * Math.Min(1, Lerp(b, r => r.CoCoreMass) / finalS16);
        }
        return new Structure(final, final, bareCo, 0, final - bareCo, Confidence.Speculative, MetallicitySchemeSource,
            HeStarRadius(he), false);
    }

    /// <summary>
    /// [APPROX] Radius of a star with a thin H-rich envelope (under the 2 M☉ that H03 needs for a II-P red supergiant).
    /// S16's 35 and 40 M☉ models keep 1.10 and 0.65 M☉ of envelope and are compact, log R = 12.16 and 11.91 (outcomes
    /// §1.3); interpolate log R in envelope mass between them, held at the ends.
    /// </summary>
    private static Quantity ThinEnvelopeRadius(double envelope)
    {
        double logR = Lerp(11.91, 12.16, Math.Clamp((envelope - 0.65) / (1.10 - 0.65), 0, 1));
        return Q(Math.Pow(10, logR), "cm", Confidence.Speculative,
            new Source("Sukhbold et al. 2016, ApJ 821, 38 (35 and 40 M☉ models); Heger et al. 2003, ApJ 591, 288 [APPROX]",
                "outcomes §1.3, §2.1"));
    }

    /// <summary>[APPROX] RSG radius in the §2.2 scheme: S16 radius to 33 M☉ (the last RSG row), then 1500 R☉.</summary>
    private static Quantity RedSupergiantRadius(double m, ExplodabilityTable table)
    {
        if (m <= 33)
            return Q(TableRadius(table, m), "cm", Confidence.Speculative, S16Source with { ResearchRef = "outcomes §1.3, §2.2" });
        // Above 33 M☉ every S16 model has lost most of its envelope, so there is no model RSG radius; a star that keeps a
        // thick envelope gets the largest observed Galactic RSGs (~1500 R☉), the top of the S16 range (1540 R☉).
        return Q(1500 * PhysicalConstants.RSun, "cm", Confidence.Speculative,
            new Source("Levesque et al. 2005, ApJ 628, 973 [APPROX]", "outcomes §4.3"));
    }

    /// <summary>outcomes §8.2 step 2 stripping table, with the §2.2 Z scaling of He-star erosion.</summary>
    private static Structure Stripped(double m, double z, Stripping stripping, bool ecsn, ExplodabilityTable table)
    {
        double heInitial, final, co;
        bool extrapolated = false, s16Cores = false;
        if (ecsn)
        {
            // [APPROX] The W19 fits fall below the 1.36 M☉ that an ECSN leaves behind, so a stripped ECSN keeps the
            // 9 M☉ row's own He and CO cores (step 1), without further He-star erosion.
            var row = table.Rows[0];
            heInitial = final = row.HeCoreMass;
            co = row.CoCoreMass;
        }
        else if (m <= StrippedS16CoresTop)
        {
            // Ruling R14, outcomes §2.2 step 1: up to 30 M☉ the He and CO cores are S16's, independent of Z. The W19 fits
            // (§1.3, §3.3) extrapolated below their range left 9–13 M☉ stripped stars lighter than their own S16 CO core
            // (12 M☉: 1.77 vs 2.09). The S16 He core is eroded by W19's He-star relation, then the final mass is floored
            // at the CO core plus the He layer that stays (below), so it never falls under M_CO.
            // Known step: at 30.01 M☉ the W19 He-core fit takes over and a Full-stripped star's final mass drops from
            // ~9.3 to ~6.3 M☉ (S16's 30 M☉ He core is 10.4 M☉, W19's fit 9.1). Deferred minor, left as it is.
            var b = Bracket(table, m);
            heInitial = Lerp(b, r => r.HeCoreMass);
            co = Lerp(b, r => r.CoCoreMass);
            final = HeStar.Eroded(heInitial, z, out extrapolated);
            // R14, H&He: M_f = max(the W19 rule, M_CO + 0.05), i.e. the larger of the two CO cores plus 0.05 M☉. At low Z
            // the barely eroded He star's W19 CO core can exceed S16's by up to ~0.1 M☉.
            if (stripping == Stripping.HydrogenAndHelium) co = Math.Max(co, HeStar.CoCore(final, out _));
            s16Cores = true;
        }
        else
        {
            heInitial = W19HeCore(m);
            final = HeStar.Eroded(heInitial, z, out bool finalExtrapolated);
            co = HeStar.CoCore(final, out bool coExtrapolated);
            extrapolated = finalExtrapolated || coExtrapolated;
        }

        // Ruling R14 [APPROX]: a Full-stripped star keeps a He shell of at least 0.16 M☉, the low end of the 0.16–1.5 M☉
        // that binary Ib/c progenitors retain at Z☉ (Yoon, Woosley & Langer 2010, ApJ 725, 940; outcomes §3.2). The S16
        // He core minus CO core is at least 0.33 M☉ above the ECSN window, so the floor stays below M_He,i.
        bool shellFloored = false;
        if (s16Cores && stripping != Stripping.HydrogenAndHelium && final < co + MinStrippedHeShell)
        {
            final = co + MinStrippedHeShell;
            shellFloored = true;
        }

        bool speculative = stripping == Stripping.Partial || ecsn || extrapolated || shellFloored || !IsSolar(z);
        var conf = speculative ? Confidence.Speculative : Confidence.ModelDependent;
        string cite = s16Cores
            ? "Sukhbold et al. 2016, ApJ 821, 38 (He and CO cores); Woosley 2019, ApJ 878, 49 (He-star erosion)"
            : "Woosley 2019, ApJ 878, 49 (He-core fit and Table 4)";
        if (extrapolated) cite += ", extrapolated beyond Table 4 [APPROX]";
        if (shellFloored) cite += "; He shell ≥ 0.16 M☉ (Yoon, Woosley & Langer 2010, ApJ 725, 940) [APPROX]";
        if (stripping == Stripping.HydrogenAndHelium) cite += "; Hachinger et al. 2012, MNRAS 422, 70";
        if (stripping == Stripping.Partial) cite += "; Chevalier & Soderberg 2010, ApJL 711, L40 [APPROX defaults]";
        if (!IsSolar(z)) cite += "; He-star erosion scaled by (Z/Z☉)^0.6 [APPROX]";
        var src = new Source(cite, IsSolar(z) ? "outcomes §3.3, §8.2 step 2" : "outcomes §2.2, §3.3, §8.2 step 2");

        switch (stripping)
        {
            case Stripping.Partial:
            {
                // [APPROX] §8.2 step 2: 0.1 M☉ of hydrogen, held in an H-rich envelope of M_H / X_H.
                double envelope = 0.1 / HydrogenMassFraction(m);
                // Chevalier & Soderberg 2010: an envelope above 0.1 M☉ makes an extended (eIIb) progenitor of ~10^13 cm.
                var radius = envelope > 0.1
                    ? Q(1e13, "cm", Confidence.Speculative, new Source("Chevalier & Soderberg 2010, ApJL 711, L40 [APPROX]", "outcomes §3.2, §8.2 step 2"))
                    : HeStarRadius(heInitial);
                return new Structure(final + envelope, final, co, envelope, final - co, conf, src, radius, false);
            }
            case Stripping.HydrogenAndHelium:
            {
                // §8.2 step 2: M_He,env = 0.05 M☉, below the Hachinger limit for visible He lines.
                const double heliumLeft = 0.05;
                return new Structure(co + heliumLeft, co + heliumLeft, co, 0, heliumLeft, conf, src, HeStarRadius(heInitial), false);
            }
            default:
                return new Structure(final, final, co, 0, final - co, conf, src, HeStarRadius(heInitial), false);
        }
    }

    /// <summary>
    /// §8.2 step 7a CHE option: M_H = 0 even without a companion (Yoon, Langer & Norman 2006).
    /// [APPROX] The research gives no CHE core masses. A chemically homogeneous star is mixed throughout and burns
    /// nearly all its hydrogen into helium, so it ends as a He star of (about) its whole final mass: the He core is set
    /// to the final mass, and the CO core follows the W19 He-star relation. Keeping the ordinary He core instead would
    /// make a 20–26 M☉ CHE star's CO core 5–7 M☉, below the M_CO ≳ 7 that Task 5's collapsar condition (§8.2 step 7a)
    /// needs, which contradicts Yoon et al.'s CHE-to-GRB picture; the whole-star core gives M_CO well above 7 M☉.
    /// </summary>
    private static Structure ChemicallyHomogeneous(Structure s)
    {
        if (s.HydrogenEnvelope <= 0) return s;
        double co = HeStar.CoCore(s.Final, out _);
        var src = new Source(s.MassSource.Citation + "; Yoon, Langer & Norman 2006, A&A 460, 199; Woosley 2019, ApJ 878, 49 [APPROX]",
            "outcomes §5.1, §8.2 step 7a");
        return new Structure(s.Final, s.Final, co, 0, s.Final - co, Confidence.Speculative, src, HeStarRadius(s.Final), false);
    }

    /// <summary>W19 He-core mass at He ignition (outcomes §1.3).</summary>
    private static double W19HeCore(double m) => m < HeavyStarMass ? 0.0385 * Math.Pow(m, 1.603) : 0.50 * m - 5.87;

    /// <summary>
    /// He-star radius from W19 (outcomes §3.3), keyed on the initial He-star mass. All [APPROX], so Speculative:
    /// 1.6–2.4 M☉ stars exceed 10^13 cm; 2.5 M☉ → 7×10^12 cm and 3.2 M☉ → 10^12 cm at Si ignition (interpolated in
    /// log R); heavier stars stay compact at ~10^11–10^12 cm (geometric middle 10^11.5).
    /// </summary>
    private static Quantity HeStarRadius(double heInitial)
    {
        var src = new Source("Woosley 2019, ApJ 878, 49 [APPROX]", "outcomes §3.3, §8.2 step 2");
        double logR = heInitial switch
        {
            <= 2.4 => 13,
            < 2.5 => Lerp(13, Math.Log10(7e12), (heInitial - 2.4) / 0.1),
            <= 3.2 => Lerp(Math.Log10(7e12), 12, (heInitial - 2.5) / 0.7),
            _ => double.NaN,
        };
        return double.IsNaN(logR)
            ? Q(Math.Pow(10, 11.5), "cm", Confidence.Speculative, src, 1e11, 1e12)
            : Q(Math.Pow(10, logR), "cm", Confidence.Speculative, src);
    }

    /// <summary>W19 standard-mass-loss He stars at Z☉ (outcomes §3.3, Table 4).</summary>
    private static class HeStar
    {
        private static readonly double[] Initial = { 3, 4, 6, 8, 10, 14, 20, 30, 40, 60, 80, 120 };
        private static readonly double[] Final = { 2.45, 3.15, 4.45, 5.64, 6.75, 8.04, 10.39, 14.81, 19.55, 29.53, 39.83, 60.78 };
        private static readonly double[] Co = { 1.61, 2.12, 3.15, 4.11, 5.07, 6.11, 8.01, 11.63, 15.67, 24.31, 33.38, 52.05 };

        /// <summary>
        /// Final mass of a He star of initial mass <paramref name="heInitial"/>, with its wind loss scaled by
        /// (Z/Z☉)^0.6 (§2.2 step 3). Table 4 inside 3–120 M☉; below 3, the W19 fit 0.939 M^0.872 (equal to the table at 3);
        /// [APPROX] above 120, the last table segment extended (<paramref name="extrapolated"/> is then true).
        /// </summary>
        public static double Eroded(double heInitial, double z, out bool extrapolated)
        {
            extrapolated = heInitial > Initial[^1];
            double finalSun = heInitial < Initial[0] ? 0.939 * Math.Pow(heInitial, 0.872) : Piecewise(Initial, Final, heInitial);
            return heInitial - (heInitial - finalSun) * Math.Pow(z, BetaHeStar);
        }

        /// <summary>
        /// CO-core mass of a He star (or bare He core) of mass <paramref name="heMass"/> at death, from the Table 4
        /// final-mass → CO pairs. [APPROX] Below 2.45 M☉, the 2.45 M☉ star's CO fraction; above 60.78, the last segment
        /// (<paramref name="extrapolated"/> is then true).
        /// </summary>
        public static double CoCore(double heMass, out bool extrapolated)
        {
            extrapolated = heMass < Final[0] || heMass > Final[^1];
            return heMass < Final[0]
                ? heMass * Co[0] / Final[0]
                : Math.Min(heMass, Piecewise(Final, Co, heMass));
        }
    }

    // ---------------------------------------------------------------- surface temperature and wind

    private static PresupernovaStar BuildStar(Structure s, double z, bool che)
    {
        Quantity M(double v) => Q(v, MSun, s.MassConfidence, s.MassSource);
        return new PresupernovaStar(M(s.Final), M(s.HeCore), M(s.CoCore), M(s.HydrogenEnvelope), M(s.HeliumEnvelope),
            s.Radius, SurfaceTemperature(s), MassLossRate(s, z), WindSpeed(s), che);
    }

    private const double RedSupergiantMinRadiusRSun = 400; // §4.3 / §2.2 step 4: RSGs are ~400–1500 R☉
    private const double ExtendedEnvelopeMinRadiusCm = 1e13; // Chevalier & Soderberg 2010 extended vs compact (§3.2)

    // The temperature and wind states follow the radius, which the structure keys on the envelope mass.
    private static bool IsRedSupergiant(Structure s)
        => s.HydrogenEnvelope > 0 && s.Radius.Value >= RedSupergiantMinRadiusRSun * PhysicalConstants.RSun;

    private static bool HasExtendedEnvelope(Structure s)
        => s.HydrogenEnvelope > 0 && s.Radius.Value >= ExtendedEnvelopeMinRadiusCm;

    private const double StefanBoltzmann = 5.670374e-5; // erg cm^-2 s^-1 K^-4 (CODATA)

    private static Quantity SurfaceTemperature(Structure s)
    {
        if (IsRedSupergiant(s))
            return Q(3500, "K", Confidence.ModelDependent,
                new Source("Jencson et al. 2023, ApJL 952, L30 (SN 2023ixf progenitor RSG, T_eff ~3500 K)", "presets §9"));
        if (s.BlueSupergiant)
            return Q(16_000, "K", Confidence.ModelDependent,
                new Source("Woosley 1988, ApJ 330, 218; Arnett et al. 1989, ARA&A 27, 629 (Sk −69°202, T_eff ~16,000 K)", "presets §1"));
        // [APPROX] No temperature for compact or stripped progenitors in the research files. Stefan–Boltzmann with the
        // radius and L = 10^5.1 L☉, the luminosity of the SN 2023ixf progenitor (Jencson et al. 2023). The same L gives
        // ~3700 K at an 846 R☉ RSG and ~15,000 K at a 50 R☉ BSG, close to the observed values above.
        double l = Math.Pow(10, 5.1) * PhysicalConstants.LSun;
        double r = s.Radius.Value;
        double t = Math.Pow(l / (4 * Math.PI * StefanBoltzmann * r * r), 0.25);
        return Q(t, "K", Confidence.Speculative,
            new Source("Stefan–Boltzmann law with L = 10^5.1 L☉ (Jencson et al. 2023, ApJL 952, L30) [APPROX]", "presets §9, outcomes §3.3"));
    }

    // Wind values by envelope state (signals §0.4): a red supergiant wind of 1e-6 M☉/yr at 10 km/s, a Wolf–Rayet wind of
    // 1e-5 M☉/yr at 1000 km/s. Extended H-rich stars get the RSG wind, compact stars the WR wind. Away from Z☉ the rate
    // scales as (Z/Z☉)^0.5 (H03, outcomes §2.1) or, for the WR wind, (Z/Z☉)^0.6 (W19 WNE, outcomes §2.1) [APPROX].
    private const double RsgMdot = 1e-6, RsgWindKmS = 10, WrMdot = 1e-5, WrWindKmS = 1000;

    private static readonly Source WindSource =
        new("Chevalier & Li 2000, ApJ 536, 195; Chevalier & Fransson 2006, ApJ 651, 381", "signals §0.4");

    private static Quantity MassLossRate(Structure s, double z)
    {
        bool slow = HasExtendedEnvelope(s);
        double rate = slow ? RsgMdot * Math.Pow(z, 0.5) : WrMdot * Math.Pow(z, BetaHeStar);
        bool typical = IsSolar(z) && WindIsTypical(s);
        var src = IsSolar(z)
            ? WindSource
            : WindSource with
            {
                Citation = WindSource.Citation + "; scaled with Z after Heger et al. 2003 / Woosley 2019 [APPROX]",
                ResearchRef = "signals §0.4, outcomes §2.1",
            };
        return Q(rate, "M☉/yr", typical ? Confidence.ModelDependent : Confidence.Speculative, src);
    }

    private static Quantity WindSpeed(Structure s)
        => Q(HasExtendedEnvelope(s) ? RsgWindKmS : WrWindKmS, "km/s",
            WindIsTypical(s) ? Confidence.ModelDependent : Confidence.Speculative, WindSource);

    /// <summary>
    /// True for the two states signals §0.4 describes: a red supergiant, or a hydrogen-free (Wolf–Rayet-like) star.
    /// [APPROX] Other stars (blue or yellow supergiants, compact stars with a thin envelope) borrow the nearer of the two.
    /// </summary>
    private static bool WindIsTypical(Structure s) => IsRedSupergiant(s) || s.HydrogenEnvelope <= 0;

    // ---------------------------------------------------------------- helpers

    private static Quantity Q(double value, string unit, Confidence confidence, Source source, double? low = null, double? high = null)
        => new(value, unit, confidence, source, low, high);

    private static (ExplodabilityRow Lo, ExplodabilityRow Hi, double F) Bracket(ExplodabilityTable table, double m)
    {
        var rows = table.Rows;
        int i = 1;
        while (i < rows.Count - 1 && rows[i].InitialMass < m) i++;
        var lo = rows[i - 1];
        var hi = rows[i];
        return (lo, hi, Math.Clamp((m - lo.InitialMass) / (hi.InitialMass - lo.InitialMass), 0, 1));
    }

    private static double Lerp((ExplodabilityRow Lo, ExplodabilityRow Hi, double F) b, Func<ExplodabilityRow, double> column)
        => Lerp(column(b.Lo), column(b.Hi), b.F);

    /// <summary>Radius interpolated linearly in log R, the tabulated column of S16 Table 2 (outcomes §1.3).</summary>
    private static double TableRadius(ExplodabilityTable table, double m)
        => Math.Pow(10, Lerp(Bracket(table, m), r => Math.Log10(r.RadiusCm)));

    private static double Lerp(double a, double b, double f) => a + (b - a) * f;

    /// <summary>Piecewise-linear through (xs, ys); beyond the ends the end segment is extended.</summary>
    private static double Piecewise(double[] xs, double[] ys, double x)
    {
        int i = 1;
        while (i < xs.Length - 1 && xs[i] < x) i++;
        return Lerp(ys[i - 1], ys[i], (x - xs[i - 1]) / (xs[i] - xs[i - 1]));
    }
}
