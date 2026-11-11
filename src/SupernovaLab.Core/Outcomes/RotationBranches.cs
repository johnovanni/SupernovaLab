using System.Globalization;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Outcomes;

/// <summary>
/// Outcomes §8.2 step 7: rotation and the core magnetic field. (a) collapsar and long GRB, (b) magnetar, (c)
/// magnetorotational explosion, (d) rotation-enlarged cores. Confidence as outcomes §8.3 labels them: collapsar
/// conditions ModelDependent, magnetar P and B Speculative, magnetorotational explosions Speculative.
/// </summary>
public sealed partial class OutcomeEngine
{
    private enum Step7 { None, Collapsar, Magnetorotational, Magnetar }

    // Step 7a: extreme rotation, Z ≤ 0.004 (Z/Z☉ ≲ 0.3), no H envelope, and step 4 says BH or M_CO ≳ 7.
    private const double CollapsarMaxZ = 0.3, CollapsarMinCoCore = 7;
    // Step 7a, Cano et al. 2017 average GRB-SN (mean, σ): E_K 2.5e52 (1.8e52) erg, M_Ni 0.4 (0.2), M_ej 6 (4) M☉.
    private const double CollapsarEnergy = 2.5e52, CollapsarEnergySigma = 1.8e52;
    private const double CollapsarNickel = 0.4, CollapsarNickelSigma = 0.2, CollapsarEjecta = 6, CollapsarEjectaSigma = 4;
    // Step 7c, "use the L16/T19 Ic-BL means": T19 (34 objects) E_K 7 ± 6 B, M_Ni 0.31 ± 0.16, M_ej 4 ± 3 M☉.
    private const double MrEnergy = 7e51, MrEnergySigma = 6e51, MrNickel = 0.31, MrNickelSigma = 0.16;
    private const double MrEjecta = 4, MrEjectaSigma = 3;
    // Step 7b: SLSN when E_p ≳ 10⁵¹ erg with t_p ≈ days to weeks. [APPROX] "Days to weeks" is taken as 1–100 days; the
    // N17 median magnetar (2.4 ms, 0.8×10¹⁴ G) has t_p ≈ 43 days.
    private const double SlsnMinEnergy = 1e51, SlsnMinTime = PhysicalConstants.Day, SlsnMaxTime = 100 * PhysicalConstants.Day;
    // Step 7b: E_K ≈ E_SN + E_p, capped by N17's typical 1.9–9.8 B.
    private const double SlsnMaxEnergy = 9.8e51;
    // Ruling R22 [APPROX]: an H-rich magnetar supernova is SLSN-II only if its peak is ≥ 7×10⁴³ erg/s (M ≲ −21).
    private const double SlsnIIMinPeak = 7e43;
    // Step 7d [APPROX]: rotational mixing raises M_He and M_CO by ~10–25%; the middle is taken.
    private const double RotatingCoreFactor = 1.175, RotatingCoreLow = 1.10, RotatingCoreHigh = 1.25;

    private static readonly Source Cano17 =
        new("Cano et al. 2017, Adv. Astron. 2017, 8929054 (average GRB-SN, mean ± σ)", "outcomes §5.1, §8.2 step 7a");
    private static readonly Source T19IcBL =
        new("Taddia et al. 2019, A&A 621, A71 (Ic-BL means ± σ); Mösta et al. 2015, Nature 528, 376; Burrows et al. " +
            "2007, ApJ 664, 416", "outcomes §5.2, §5.4, §8.2 step 7c");

    /// <summary>
    /// Step 7 a and c, after step 4: a collapsar when all of step 7a's conditions hold, else a magnetorotational explosion
    /// when a fast-spinning, strongly magnetized core would form a black hole. Null when neither applies. Step 7a is
    /// checked first: its conditions are the stricter, and they add the GRB.
    /// </summary>
    private static Draft? RotationBranch(LabInputs x, PresupernovaStar star, Collapse c, List<string> notes)
    {
        bool blackHoleCore = !c.Explodes || c.Fallback;
        if (x.Rotation == Rotation.Extreme)
        {
            string? missing = CollapsarMissing(x, star, blackHoleCore);
            if (missing is null) return Collapsar(x, star, c, notes);
            notes.Add($"Extreme rotation alone does not make a collapsar and a gamma-ray burst here: {missing}.");
        }
        if (x.Rotation != Rotation.Slow && x.Field == CoreField.Strong && blackHoleCore)
            return Magnetorotational(x, star, notes);
        return null;
    }

    /// <summary>The first step 7a condition this star misses, in plain words; null when it meets them all.</summary>
    private static string? CollapsarMissing(LabInputs x, PresupernovaStar star, bool blackHoleCore)
    {
        // Step 7a asks for no H envelope "via CHE or stripping"; the physical condition (§5.1) is no H envelope, which is
        // what is tested, so a star whose winds removed its envelope also qualifies.
        if (x.Metallicity > CollapsarMaxZ)
            return "that needs low metal content (below about 0.3 of the Sun's), or winds carry the core's spin away";
        if (star.HydrogenEnvelopeMass.Value > 0)
            return "the star still has a hydrogen envelope, which would stop the jets from getting out";
        if (!blackHoleCore && star.CoCoreMass.Value < CollapsarMinCoCore)
            return $"its core makes a neutron star, and its carbon-oxygen core ({UnitFormat.Mass(star.CoCoreMass.Value)}) " +
                   "is under the roughly 7 M☉ that a black hole with a disk needs";
        return null;
    }

    /// <summary>Step 7a: Ic-BL + long GRB, black hole of M_f − M_ej, Cano 2017's average GRB-SN values.</summary>
    private static Draft Collapsar(LabInputs x, PresupernovaStar star, Collapse c, List<string> notes)
    {
        var md = Confidence.ModelDependent;
        var ejecta = BlackHoleEjecta(star, CollapsarEjecta, CollapsarEjectaSigma, md, Cano17, notes);
        var bh = Q(star.FinalMass.Value - ejecta.Value, MSun, RemnantModel.Weakest(star.FinalMass.Confidence, ejecta.Confidence),
            new Source("M_BH ≈ M_f − M_ej; " + Cano17.Citation, "outcomes §5.1, §8.2 step 7a"));
        // [APPROX] The black hole forms early in the collapse; t_BH from the core's compactness when known, else 1.0 s.
        // The jets and ejecta carry momentum, so the kick is momentum-scaled.
        var remnant = RemnantModel.BlackHole(bh, RemnantModel.DirectCollapseTime(c.Compactness, c.Confidence),
            RemnantModel.BlackHoleKick(bh.Value, directCollapse: false));
        var energy = Q(CollapsarEnergy, "erg", md, Cano17, CollapsarEnergy - CollapsarEnergySigma, CollapsarEnergy + CollapsarEnergySigma);
        var nickel = CapNickel(Q(CollapsarNickel, MSun, md, Cano17,
            CollapsarNickel - CollapsarNickelSigma, CollapsarNickel + CollapsarNickelSigma), ejecta.Value);

        notes.Add("With extreme rotation at low metal content the collapsing core spins fast enough to form a black hole " +
                  $"of {UnitFormat.Mass(bh.Value)} wrapped in a disk that launches jets: a collapsar. The jets drill out of " +
                  "the hydrogen-free star as a long gamma-ray burst, and the explosion is a broad-lined Type Ic supernova. " +
                  "The conditions are model-dependent and the rotation needed is speculative.");
        var type = WithDenseCsm(x, SupernovaType.IcBL, notes);
        return new Draft(Fate.Supernova, type, remnant, energy, ejecta, nickel, PeakRule.Arnett)
        {
            HasGrb = true, Branch = Step7.Collapsar,
        };
    }

    /// <summary>
    /// Step 7c [SPECULATIVE]: a magnetorotational explosion of a black-hole-forming core, with the T19 Ic-BL means. Ic-BL
    /// when no hydrogen is left; a star that keeps hydrogen keeps its hydrogen class. [APPROX] The research gives no ejecta
    /// or remnant: the T19 ejecta plus any H-rich envelope are thrown off and the rest forms a black hole, as for the
    /// collapsar (M_BH = M_f − M_ej).
    /// </summary>
    private static Draft Magnetorotational(LabInputs x, PresupernovaStar star, List<string> notes)
    {
        var sp = Confidence.Speculative;
        double envelope = star.HydrogenEnvelopeMass.Value;
        var src = T19IcBL with { Citation = T19IcBL.Citation + (envelope > 0 ? "; plus the H-rich envelope [APPROX]" : "") };
        var ejecta = BlackHoleEjecta(star, MrEjecta + envelope, MrEjectaSigma, sp, src, notes);
        var bh = Q(star.FinalMass.Value - ejecta.Value, MSun, sp,
            new Source("M_BH ≈ M_f − M_ej, as for a collapsar [APPROX]", "outcomes §5.4, §8.2 step 7c"));
        var remnant = RemnantModel.BlackHole(bh, RemnantModel.FallbackFormationTime(),
            RemnantModel.BlackHoleKick(bh.Value, directCollapse: false));
        var energy = Q(MrEnergy, "erg", sp, T19IcBL, MrEnergy - MrEnergySigma, MrEnergy + MrEnergySigma);
        var nickel = CapNickel(Q(MrNickel, MSun, sp, T19IcBL, MrNickel - MrNickelSigma, MrNickel + MrNickelSigma), ejecta.Value);

        notes.Add("Its fast rotation winds up the strong magnetic field of the collapsing core into jets that blow the star " +
                  "apart even though neutrinos alone could not: a magnetorotational explosion, a hypernova of about " +
                  $"{UnitFormat.Energy(MrEnergy)}. What is left forms a black hole of {UnitFormat.Mass(bh.Value)}. Such " +
                  "explosions are speculative.");
        SupernovaType type;
        if (envelope > 0)
        {
            var (hydrogenType, typeNote) = SpectralTyper.Classify(star, x.InitialMass, x.Stripping, x.Mixing);
            type = hydrogenType;
            notes.Add(typeNote);
        }
        else
        {
            type = SupernovaType.IcBL;
            notes.Add("With no hydrogen and very fast ejecta that broaden its lines, it is a broad-lined Type Ic supernova.");
        }
        var peak = SpectralTyper.HasPlateau(type) ? PeakRule.Plateau : PeakRule.Arnett;
        type = WithDenseCsm(x, type, notes);
        return new Draft(Fate.Supernova, type, remnant, energy, ejecta, nickel, peak, PeakSpeculative: true)
        {
            Branch = Step7.Magnetorotational,
        };
    }

    /// <summary>
    /// Ejecta of a step 7 explosion that leaves a black hole of M_f − M_ej. [APPROX] The black hole is kept at least as
    /// heavy as the heaviest neutron star (<see cref="RemnantModel.MinBlackHoleMass"/>), so a light star ejects less.
    /// </summary>
    private static Quantity BlackHoleEjecta(PresupernovaStar star, double nominal, double sigma, Confidence confidence,
        Source source, List<string> notes)
    {
        double room = star.FinalMass.Value - RemnantModel.MinBlackHoleMass;
        if (nominal <= room)
            return Q(nominal, MSun, confidence, source, Math.Max(MinEjecta, nominal - sigma), nominal + sigma);
        double ejecta = Math.Max(MinEjecta, room);
        notes.Add($"The star ({UnitFormat.Mass(star.FinalMass.Value)}) is too light to throw off the usual " +
                  $"{UnitFormat.Mass(nominal)} and still leave a black hole, so it is taken to throw off " +
                  $"{UnitFormat.Mass(ejecta)}; this is an approximation.");
        return Q(ejecta, MSun, Confidence.Speculative, source with
        {
            Citation = source.Citation + "; reduced to leave a black hole of at least 2.56 M☉ [APPROX]",
        });
    }

    // ---------------------------------------------------------------- step 7b: magnetar

    /// <summary>
    /// What step 7b makes of a newborn neutron star: the magnetar remnant, the kinetic energy, the spin-down energy and
    /// time when they change the light (<see cref="PowersLight"/>, with <see cref="DiffusionEnergy"/> = E_SN + E_p
    /// uncapped for the diffusion time), and the superluminous class when they power one.
    /// </summary>
    private sealed record MagnetarOutcome(Remnant Remnant, Quantity Energy, Quantity? SpinDownEnergy, Quantity? SpinDownTime,
        double? DiffusionEnergy, SupernovaType? Superluminous)
    {
        public bool PowersLight => SpinDownEnergy is not null;
    }

    private static MagnetarOutcome Magnetar(LabInputs x, PresupernovaStar star, Remnant neutronStar, Quantity energy,
        Quantity ejecta, List<string> notes)
    {
        var remnant = MagnetarModel.Magnetar(neutronStar, x.Rotation);
        var period = remnant.SpinPeriodS!;
        var ep = MagnetarModel.SpinDownEnergy(period);
        var tp = MagnetarModel.SpinDownTime(period, remnant.MagneticFieldG!);
        string spin = $"spins every {UnitFormat.Time(period.Value)} with a field of {FieldText(remnant.MagneticFieldG!.Value)}";

        if (x.Rotation == Rotation.Slow)
        {
            // Step 7b: slow rotation gives a normal supernova with a magnetar remnant and no light-curve effect.
            notes.Add($"The strong magnetic field makes the neutron star a magnetar. It {spin}, so its spin-down energy " +
                      $"(about {UnitFormat.Energy(ep.Value)}) is too small to change the light curve.");
            return new MagnetarOutcome(remnant, energy, null, null, null, null);
        }

        string born = $"With {(x.Rotation == Rotation.Extreme ? "extreme" : "fast")} rotation and a strong magnetic field " +
                      $"the neutron star is born a magnetar. It {spin}";
        bool superluminous = ep.Value >= SlsnMinEnergy && tp.Value >= SlsnMinTime && tp.Value <= SlsnMaxTime;
        if (!superluminous)
        {
            notes.Add($"{born}; its spin-down adds about {UnitFormat.Energy(ep.Value)} over about {UnitFormat.Time(tp.Value)}.");
            return new MagnetarOutcome(remnant, energy, ep, tp, energy.Value + ep.Value, null);
        }

        // Step 7b: E_K ≈ E_SN + E_p, capped at N17's 9.8 B for display; the diffusion time takes the uncapped sum (§5.3).
        double total = energy.Value + ep.Value;
        var kinetic = Q(Math.Min(total, SlsnMaxEnergy), "erg", Confidence.Speculative,
            new Source($"E_K ≈ E_SN + E_p, capped at 9.8 B (Nicholl, Guillochon & Berger 2017, ApJ 850, 55); E_SN from " +
                       energy.Source.Citation, "outcomes §5.3, §8.2 step 7b"));
        string pours = $"{born}, and as it slows it pours about {UnitFormat.Energy(ep.Value)} into the ejecta over about " +
                       $"{UnitFormat.Time(tp.Value)}.";

        if (star.HydrogenEnvelopeMass.Value <= 0)
        {
            notes.Add($"{pours} That makes a superluminous supernova without hydrogen (Type I). The step from the star to " +
                      "its spin and field is speculative.");
            return new MagnetarOutcome(remnant, kinetic, ep, tp, total, SupernovaType.SlsnI);
        }

        // Ruling R22 [APPROX]: hydrogen-rich events count as SLSN-II only above the usual superluminous cut.
        double peak = MagnetarModel.PeakLuminosityErgS(ep.Value, tp.Value, ejecta.Value, total);
        if (peak >= SlsnIIMinPeak)
        {
            notes.Add($"{pours} That makes a superluminous supernova with hydrogen lines (Type II); how such " +
                      "hydrogen-rich events arise is speculative.");
            return new MagnetarOutcome(remnant, kinetic, ep, tp, total, SupernovaType.SlsnII);
        }
        notes.Add($"{pours} Its hydrogen envelope holds the light back, so it is not superluminous; it stays a " +
                  "hydrogen-rich supernova, made unusually luminous by the magnetar (speculative).");
        return new MagnetarOutcome(remnant, kinetic, ep, tp, total, null);
    }

    private static string FieldText(double gauss)
        => $"{(gauss / 1e14).ToString("0.##", CultureInfo.InvariantCulture)} × 10¹⁴ G";

    // ---------------------------------------------------------------- step 7d: rotation-enlarged cores

    /// <summary>
    /// Step 7d applies to fast or extreme rotation without a strong field, for unstripped stars only (ruling R20): a
    /// stripped star's fate is keyed on its initial mass (Sch21), so raising its CO core would only erase its He shell. A
    /// chemically homogeneous star (the CHE option) is already mixed throughout, so its cores are left as they are.
    /// </summary>
    private static bool RaisesCores(LabInputs x, PresupernovaStar star)
        => x.Rotation != Rotation.Slow && x.Field == CoreField.Normal && x.Stripping == Stripping.None &&
           !star.ChemicallyHomogeneous;

    /// <summary>
    /// Step 7d [APPROX]: raise M_He and M_CO by 17.5% (the middle of ~10–25%, Heger, Langer & Woosley 2000) before step 4,
    /// keeping the final mass (the He core grows into the H-rich envelope). Speculative.
    /// </summary>
    private static PresupernovaStar WithRotatingCores(LabInputs x, PresupernovaStar star, List<string> notes)
    {
        double mf = star.FinalMass.Value;
        double He(double f) => Math.Min(star.HeCoreMass.Value * f, mf);
        double Co(double f) => Math.Min(star.CoCoreMass.Value * f, He(f));
        const string raised = "; raised by 17.5% (10–25%) for rotational mixing (Heger, Langer & Woosley 2000, ApJ 528, " +
                              "368; Limongi & Chieffi 2018) [APPROX]";
        Source From(Quantity q) => new(q.Source.Citation + raised, q.Source.ResearchRef + "; outcomes §8.2 step 7d");
        Quantity Raised(Quantity q, Func<double, double> mass) => Q(mass(RotatingCoreFactor), MSun, Confidence.Speculative,
            From(q), mass(RotatingCoreLow), mass(RotatingCoreHigh));
        Quantity Layer(Quantity q, double value) => Q(value, MSun, Confidence.Speculative, From(q));

        double he = He(RotatingCoreFactor), co = Co(RotatingCoreFactor);
        string rotation = x.Rotation == Rotation.Extreme ? "Extreme" : "Fast";
        notes.Add($"{rotation} rotation mixes fresh fuel into the burning core, so its helium and carbon-oxygen cores grow " +
                  $"by about a sixth (10–25%), to {UnitFormat.Mass(he)} and {UnitFormat.Mass(co)}. How much is model-dependent.");
        return star with
        {
            HeCoreMass = Raised(star.HeCoreMass, He),
            CoCoreMass = Raised(star.CoCoreMass, Co),
            HydrogenEnvelopeMass = Layer(star.HydrogenEnvelopeMass, mf - he),
            HeliumEnvelopeMass = Layer(star.HeliumEnvelopeMass, he - co),
        };
    }

    // ---------------------------------------------------------------- inputs that had no effect

    /// <summary>
    /// A note for a rotation or field input that changed nothing, so the lab never ignores an input silently.
    /// </summary>
    private static void NoteUnusedSpinAndField(LabInputs x, EarlyFate early, PresupernovaStar star, bool coresRaised,
        Draft d, List<string> notes)
    {
        bool spins = x.Rotation != Rotation.Slow, strong = x.Field == CoreField.Strong;
        if (x.Kind == StarKind.WhiteDwarf)
        {
            if (spins || strong)
                notes.Add("Rotation and the core magnetic field apply only to massive stars, so they have no effect on " +
                          "this white dwarf's explosion.");
            return;
        }

        bool rotationUsed = star.ChemicallyHomogeneous || coresRaised ||
                            d.Branch is Step7.Collapsar or Step7.Magnetorotational or Step7.Magnetar;
        if (spins && !rotationUsed)
        {
            string why = x.Stripping != Stripping.None && early == EarlyFate.CoreCollapse
                ? "a stripped star's fate is set by its initial mass (Schneider et al. 2021), so the lab does not grow " +
                  "its cores for rotation"
                : early switch
            {
                EarlyFate.NotDeadYet or EarlyFate.WhiteDwarf => "it matters only for stars whose core collapses",
                EarlyFate.ElectronCapture => "the lab does not shift the electron-capture window for rotation",
                EarlyFate.PulsationalPairInstability or EarlyFate.PairInstability or EarlyFate.DirectCollapseAbovePisn
                    => "pair instability is decided by the helium core alone",
                _ => "nothing in this outcome depends on it",
            };
            notes.Add($"The star's {(x.Rotation == Rotation.Extreme ? "extreme" : "fast")} rotation does not change this " +
                      $"outcome in the lab: {why}.");
        }

        if (strong && d.Branch is not (Step7.Magnetorotational or Step7.Magnetar))
            notes.Add("The strong core magnetic field has no effect here: no neutron star is left for it to make into a " +
                      "magnetar.");
    }
}
