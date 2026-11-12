using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Outcomes;

/// <summary>
/// Outcomes §8.2: turns the lab inputs into an <see cref="Outcome"/>. <see cref="ProgenitorModel"/> does steps 0–3; this
/// class adds the step 3 outcomes, step 4 (does the core explode?), step 5 (explosion properties), step 6 (spectral type
/// and peak luminosity) and step 8 (remnant). Step 7 (rotation and the core field) is in RotationBranches.cs, and the
/// Type Ia path (step 9) in <see cref="TypeIaModel"/>. Pure and deterministic.
/// </summary>
public sealed partial class OutcomeEngine
{
    private const string MSun = "M☉";

    // Step 3 (outcomes §2.3): the He-core masses at which pulsational and full pair instability set in, as in
    // ProgenitorModel.
    private const double PpisnHeCore = 35, PisnHeCore = 64;
    // Step 3: M_BH ≈ min(M_He,final, ~45) after pulsational pair instability (F19; APPROXIMATION for the shape).
    private const double PpisnMaxBlackHole = 45;
    // Below 0.01 M☉ of pulse ejecta the PPISN is shown without an interacting transient (Task 4 review).
    private const double PpisnMinEjecta = 0.01;
    // Step 4: the S16 lookup stands in for core structure for H-rich single stars of M ≤ 30.
    private const double SingleStarLookupTop = 30;
    // Step 4, H-rich stars with M > 30: a final He core of ≳ 7.5 M☉ collapses directly to a black hole.
    private const double HeavyStarDirectCollapseHeCore = 7.5;
    // Step 4, stripped stars (Sch21 Case A/B): black holes for 31.5–34 M☉ and ≳ 70 M☉, neutron stars otherwise.
    private const double StrippedBlackHoleLow = 31.5, StrippedBlackHoleHigh = 34, StrippedBlackHoleAbove = 70;
    // S16 §4.1: between 15 and 22 M☉ the outcome is "highly variable for even small changes in the initial mass".
    private const double VariableBandLow = 15, VariableBandHigh = 22;
    // Step 5: the calibrated ⁵⁶Ni default halves the S16 yields of stars above 12 M☉ (the Z9.6-engine yields below
    // 12 M☉ already match the observed median).
    private const double NickelCalibrationAbove = 12;
    // Step 5, failed SN: neutrino losses before the horizon forms are unlikely to exceed ~0.3 M☉ (S16 §5.2).
    private const double NeutrinoMassLossMax = 0.3;
    // Ruling R9 [APPROX]: an ultra-stripped star ejects at least 0.1 M☉ (ultra-stripped supernovae eject ~0.05–0.2 M☉).
    private const double MinEjecta = RemnantModel.MinEjectaMass;
    // Ruling R13: an explosion with ≤ 0.2 M☉ of ejecta is ultra-stripped and gets E = 1e50 erg.
    private const double UltraStrippedMaxEjecta = 0.2, UltraStrippedEnergy = 1e50;
    // Ruling R15 [APPROX]: ⁵⁶Ni never exceeds half the ejecta mass.
    private const double NickelMaxEjectaFraction = 0.5;

    private static readonly Source S16Explosions =
        new("Sukhbold et al. 2016, ApJ 821, 38 (per-model Z9.6 / N20 results, doi:10.17617/1.b)", "outcomes §1.2, §1.4, §8.2 steps 4–5");
    private static readonly Source MagnetarPeakSource =
        new("Kasen & Bildsten 2010, ApJ 717, 245 (spin-down power, eq. 14) diffused through Arnett's kernel (Inserra et al. " +
            "2013, ApJ 770, 128, Eq. D1), κ = 0.2 cm²/g, peak of the drawn light curve without ⁵⁶Ni (ruling R32) [APPROX]",
            "outcomes §5.3, §8.2 step 7b; signals §4.2, §4.4");
    private static readonly Source Sch21 =
        new("Schneider, Podsiadlowski & Müller 2021, A&A 645, A5 (stripped Case A/B stars)", "outcomes §3.3, §8.2 steps 4–5");

    private readonly ExplodabilityTable _table;

    public OutcomeEngine(ExplodabilityTable table) => _table = table ?? throw new ArgumentNullException(nameof(table));

    public Outcome Compute(LabInputs inputs, PresetPins? pins = null)
    {
        var x = inputs.Clamped();
        // The unclamped inputs go in so that ProgenitorModel notes any clamping. It applies step 7a's CHE option.
        var (early, star, evolveNotes) = ProgenitorModel.Evolve(inputs, _table);
        var notes = new List<string>(evolveNotes);
        if (x.Kind == StarKind.WhiteDwarf)
        {
            var ia = TypeIa(x, notes);
            NoteUnusedSpinAndField(x, early, star, false, ia, notes);
            return Finish(x, ia, star, notes, pins);
        }

        // Step 7d, before step 4: fast rotation without a strong field enlarges an unstripped star's cores. Ruling R20:
        // step 3 is then re-run on the raised He core, which can reach the PPISN range (it stays below 64 M☉, since it
        // started below 35; a core already above the PISN ceiling stays a direct collapse).
        bool coresRaised = early == EarlyFate.CoreCollapse && RaisesCores(x, star);
        if (coresRaised)
        {
            double heBefore = star.HeCoreMass.Value;
            star = WithRotatingCores(x, star, notes);
            if (heBefore < PpisnHeCore && star.HeCoreMass.Value >= PpisnHeCore)
            {
                early = EarlyFate.PulsationalPairInstability;
                notes.Add($"Its enlarged helium core of {UnitFormat.Mass(star.HeCoreMass.Value)} reaches the pulsational " +
                          "pair-instability range (about 35–64 M☉): pulses throw off mass before the core collapses to a " +
                          "black hole.");
            }
        }

        Draft d = early switch
        {
            EarlyFate.NotDeadYet => new Draft(Fate.NotDeadYet, SupernovaType.None, Remnant.None),
            EarlyFate.WhiteDwarf => new Draft(Fate.WhiteDwarf, SupernovaType.None, RemnantModel.WhiteDwarf(star)),
            EarlyFate.PairInstability => PairInstability(star, notes),
            EarlyFate.PulsationalPairInstability => PulsationalPairInstability(x, star, notes),
            EarlyFate.DirectCollapseAbovePisn => AbovePairInstability(star),
            EarlyFate.ElectronCapture or EarlyFate.CoreCollapse when TooLightToCollapse(x, star)
                => NeverCollapses(star, notes),
            EarlyFate.ElectronCapture => Exploded(x, star, ElectronCapture(notes), notes),
            _ => CoreCollapse(x, star, coresRaised, notes),
        };
        NoteUnusedSpinAndField(x, early, star, coresRaised, d, notes);
        return Finish(x, d, star, notes, pins);
    }

    // ---------------------------------------------------------------- assembling the outcome

    private enum PeakRule { None, Plateau, Arnett, Magnetar }

    /// <summary>
    /// The outcome before preset pins and the peak luminosity, which depends on the pinned values. <see cref="Branch"/>
    /// records which step 7 branch, if any, shaped it.
    /// </summary>
    private sealed record Draft(Fate Fate, SupernovaType Type, Remnant Remnant, Quantity? Energy = null,
        Quantity? Ejecta = null, Quantity? Nickel = null, PeakRule Peak = PeakRule.None, bool PeakSpeculative = false)
    {
        public bool HasGrb { get; init; }
        public Quantity? MagnetarEnergy { get; init; }
        public Quantity? MagnetarTimescale { get; init; }
        public Step7 Branch { get; init; }
    }

    private static Outcome Finish(LabInputs x, Draft d, PresupernovaStar star, List<string> notes, PresetPins? pins)
    {
        var (energy, ejecta, nickel) = (d.Energy, d.Ejecta, d.Nickel);
        bool published = false;
        if (pins is not null)
        {
            // A preset's values are one author's published fit to the real event (presets.md), so ModelDependent.
            Quantity Pin(double v, string unit)
            {
                published = true;
                return new Quantity(v, unit, Confidence.ModelDependent, pins.Source, Published: true);
            }
            if (pins.ExplosionEnergyErg is double e) energy = Pin(e, "erg");
            if (pins.EjectaMass is double ej) ejecta = Pin(ej, MSun);
            if (pins.NickelMass is double ni) nickel = Pin(ni, MSun);
            if (pins.RadiusCm is double r) star = star with { RadiusCm = Pin(r, "cm") };
        }

        var peak = PeakLuminosity(x, d, star, energy, ejecta, nickel);
        // Step 9 / ruling R21: the Phillips outputs for Type Ia outcomes.
        Quantity? magB = null, dm15 = null;
        if (x.Kind == StarKind.WhiteDwarf && peak is not null)
        {
            magB = TypeIaModel.PeakAbsoluteMagB(peak);
            dm15 = TypeIaModel.DeclineRateDm15B(magB, notes);
        }
        return new Outcome(x, d.Fate, d.Type, d.HasGrb, d.Remnant, star, energy, ejecta, nickel, peak,
            d.MagnetarEnergy, d.MagnetarTimescale, notes, published, magB, dm15);
    }

    /// <summary>
    /// Step 6 light-curve output. II-P / II-L with at least 1 M☉ of hydrogen envelope: the day-50 plateau luminosity of
    /// Goldberg, Bildsten &amp; Paxton 2019 (<see cref="PlateauModel"/>), the plateau the light curve draws (ruling R33c;
    /// step 6 allows S16 or GBP19). Thinner II-L envelopes, stripped supernovae and SNe Ia: Arnett's rule,
    /// L_peak = 2.0×10⁴³ (M_Ni / M☉) erg/s (§7.2). Magnetar-powered supernovae (step 7b): ruling R32's one magnetar
    /// peak (<see cref="MagnetarModel.PeakLuminosityErgS"/>).
    /// </summary>
    private static Quantity? PeakLuminosity(LabInputs x, Draft d, PresupernovaStar star, Quantity? energy, Quantity? ejecta,
        Quantity? nickel)
    {
        // Ruling R32: the one magnetar peak, with the outcome's own (displayed or pinned) kinetic energy and ejecta and
        // ruling R33a's opacity, as the light curve uses. [APPROX] The ⁵⁶Ni heating (~10⁴² erg/s) is left out next to
        // the magnetar's heating.
        var env = star.HydrogenEnvelopeMass;
        if (d.Peak == PeakRule.Magnetar && d.MagnetarEnergy is { } ep && d.MagnetarTimescale is { } tp &&
            ejecta is { Value: > 0 } && energy is { Value: > 0 })
            return new Quantity(MagnetarModel.PeakLuminosityErgS(ep.Value, tp.Value, ejecta.Value, energy.Value,
                    MagnetarModel.EjectaOpacity(x.InitialMass, env.Value, ejecta.Value)), "erg/s",
                Confidence.Speculative, MagnetarPeakSource);

        if (d.Peak == PeakRule.Plateau && energy is { Value: > 0 } && ejecta is { Value: > 0 } &&
            env.Value >= PlateauModel.MinEnvelopeMsun)
        {
            double l = PlateauModel.LuminosityErgS(energy.Value, ejecta.Value, star.RadiusCm.Value);
            // The fit is to II-P models; for II-L and interacting supernovae it is only a guide.
            var conf = RemnantModel.Weakest(energy.Confidence, ejecta.Confidence, star.RadiusCm.Confidence,
                d.PeakSpeculative ? Confidence.Speculative : Confidence.ModelDependent);
            return new Quantity(l, "erg/s", conf,
                new Source("Goldberg, Bildsten & Paxton 2019, ApJ 879, 3 (plateau luminosity at day 50; the plateau the " +
                           "light curve draws)", "outcomes §8.2 step 6; signals §4.3"));
        }
        if (d.Peak is PeakRule.Arnett or PeakRule.Plateau && nickel is { Value: > 0 })
            return new Quantity(2.0e43 * nickel.Value, "erg/s",
                RemnantModel.Weakest(nickel.Confidence, d.PeakSpeculative ? Confidence.Speculative : Confidence.ModelDependent),
                new Source("Arnett 1982, ApJ 253, 785 (Arnett's rule, as used by Stritzinger et al. 2006, A&A 460, 793)",
                    "outcomes §7.2, §8.2 step 6"));
        return null;
    }

    // ---------------------------------------------------------------- step 3 outcomes

    private static readonly double[] Hw02HeCore = { 70, 80, 90, 100, 110, 120, 130, 133.3 };
    private static readonly double[] Hw02Nickel = { 0.0117, 0.129, 1.31, 5.82, 14.2, 24.6, 39.6, 57.0 };

    private static Draft PairInstability(PresupernovaStar star, List<string> notes)
    {
        double he = star.HeCoreMass.Value, mf = star.FinalMass.Value;
        notes.Add(star.HydrogenEnvelopeMass.Value > 0
            ? "It still has its hydrogen, so the spectrum shows hydrogen lines (a Type II pair-instability supernova)."
            : "It has lost its hydrogen, so the spectrum shows none (a Type I pair-instability supernova).");

        // HW02 Table 3 ⁵⁶Ni, interpolated linearly in log M_Ni (it spans four decades). [APPROX] Below 70 M☉ of He the
        // 70–80 segment is extended.
        bool extrapolated = he < Hw02HeCore[0];
        int i = 1;
        while (i < Hw02HeCore.Length - 1 && Hw02HeCore[i] < he) i++;
        double f = (he - Hw02HeCore[i - 1]) / (Hw02HeCore[i] - Hw02HeCore[i - 1]);
        double logNi = Lerp(Math.Log10(Hw02Nickel[i - 1]), Math.Log10(Hw02Nickel[i]), f);
        var nickel = new Quantity(Math.Pow(10, logNi), MSun,
            RemnantModel.Weakest(star.HeCoreMass.Confidence, extrapolated ? Confidence.Speculative : Confidence.ModelDependent),
            new Source("Heger & Woosley 2002, ApJ 567, 532, Table 3 (log-interpolated in M_He)" +
                       (extrapolated ? ", extended below 70 M☉ [APPROX]" : ""), "outcomes §2.3, §8.2 step 3"));

        // [APPROX] outcomes §2.3: log E linear from ~4 B at M_He = 65 to 93.5 B (HW02) at 133.
        double logE = Lerp(Math.Log10(4e51), Math.Log10(9.35e52), (he - 65) / (133 - 65));
        var energy = new Quantity(Math.Pow(10, logE), "erg", Confidence.Speculative,
            new Source("Heger & Woosley 2002, ApJ 567, 532 (93.5 B at M_He = 133.3); log-linear from ~4 B at 65 M☉ [APPROX]",
                "outcomes §2.3, §8.2 step 3"));

        var ejecta = new Quantity(mf, MSun, star.FinalMass.Confidence,
            new Source("Heger & Woosley 2002, ApJ 567, 532 (complete disruption, M_ej = M_f)", "outcomes §2.3, §8.2 step 3"));
        return new Draft(Fate.PairInstabilitySupernova, SupernovaType.Pisn, Remnant.None, energy, ejecta,
            CapNickel(nickel, ejecta.Value));
    }

    private static Draft PulsationalPairInstability(LabInputs x, PresupernovaStar star, List<string> notes)
    {
        // Consequence of the research's min(M_He, 45) rule: a bare He star of 35–45 M☉ (M_f = M_He) keeps all its mass,
        // so the pulses eject 0 M☉ (e.g. Full-stripped 100 M☉ at Z = 0.1). Kept as the research states it.
        double he = star.HeCoreMass.Value, mf = star.FinalMass.Value, mbh = Math.Min(he, PpisnMaxBlackHole);
        var src = new Source("Farmer et al. 2019, ApJ 887, 53; Woosley 2017, ApJ 836, 244 (M_BH ≈ min(M_He, 45 M☉)) [APPROX]",
            "outcomes §2.3, §8.2 step 3");
        var mass = new Quantity(mbh, MSun, Confidence.Speculative, src);
        var ejecta = new Quantity(mf - mbh, MSun, Confidence.Speculative, src);

        // Step 3: an optional interacting transient, IIn-like with hydrogen, Ibn / Icn-like without [MODEL-DEPENDENT].
        // With (almost) nothing ejected there is no transient to show.
        SupernovaType type;
        if (ejecta.Value < PpisnMinEjecta)
        {
            type = SupernovaType.None;
            notes.Add($"The pulses are too weak to throw off much mass, and the core collapses to a black hole of " +
                      $"{UnitFormat.Mass(mbh)}.");
        }
        else
        {
            type = SpectralTyper.WithDenseCsm(SpectralTyper.Classify(star, x.InitialMass, x.Stripping, x.Mixing).Type);
            notes.Add($"The pulses throw off {UnitFormat.Mass(mf - mbh)} and leave a black hole of {UnitFormat.Mass(mbh)}. " +
                      $"Colliding shells can shine as an interacting transient like a Type {type} supernova (model-dependent).");
        }
        var remnant = RemnantModel.BlackHole(mass, RemnantModel.DirectCollapseTime(null),
            RemnantModel.BlackHoleKick(mbh, directCollapse: true));
        return new Draft(Fate.PulsationalPairInstability, type, remnant, Ejecta: ejecta);
    }

    private static Draft AbovePairInstability(PresupernovaStar star)
    {
        var mass = new Quantity(star.FinalMass.Value, MSun,
            RemnantModel.Weakest(star.FinalMass.Confidence, Confidence.ModelDependent),
            new Source("Heger & Woosley 2002, ApJ 567, 532 (direct collapse above the PISN range, M_BH = M_f)",
                "outcomes §2.3, §8.2 step 3"));
        return new Draft(Fate.FailedSupernova, SupernovaType.None, RemnantModel.BlackHole(mass,
            RemnantModel.DirectCollapseTime(null), RemnantModel.BlackHoleKick(mass.Value, directCollapse: true)));
    }

    private static bool TooLightToCollapse(LabInputs x, PresupernovaStar star)
        => x.Stripping != Stripping.None && !RemnantModel.CanCollapse(star.FinalMass.Value);

    /// <summary>
    /// Ruling R13 [APPROX]: a stripped star too light to make a 1.2 M☉ neutron star plus 0.1 M☉ of ejecta never reaches
    /// collapse in this model. It ends as an O-Ne or C-O white dwarf, taken to be its CO core (the thin He and H layers
    /// above are neglected, as for ProgenitorModel's white dwarfs). A safety net: since ruling R14 every stripped star
    /// ends at M_CO + 0.05 ≥ 1.45 M☉, so no LabInputs reach it; RemnantModel.CanCollapse carries the tested rule.
    /// </summary>
    private static Draft NeverCollapses(PresupernovaStar star, List<string> notes)
    {
        var mass = Q(star.CoCoreMass.Value, MSun, Confidence.Speculative,
            new Source($"{star.CoCoreMass.Source.Citation}; the CO core of a stripped star too light to make a 1.2 M☉ " +
                       "neutron star, the lightest measured (Özel & Freire 2016, ARA&A 54, 401), plus 0.1 M☉ of ejecta [APPROX]",
                "outcomes §1.1, §3.3, §6"));
        // ProgenitorModel's fate note for this path says the core collapses ("The iron core of this … collapses …" or the
        // electron-capture "… core collapses when electrons are captured …"). It is the only note so far that says
        // "collapses", and this override contradicts it, so it goes.
        notes.RemoveAll(n => n.Contains("collapses", StringComparison.Ordinal));
        notes.Add($"The star ({UnitFormat.Mass(star.FinalMass.Value)}) was stripped so heavily that " +
                  "its core never reached collapse: it is too light to make even the lightest neutron star. It ends as a " +
                  $"{UnitFormat.Mass(mass.Value)} oxygen-neon or carbon-oxygen white dwarf, with no supernova.");
        return new Draft(Fate.WhiteDwarf, SupernovaType.None, RemnantModel.WhiteDwarf(mass));
    }

    /// <summary>Step 9: the Type Ia explosion of the chosen white dwarf; Arnett's rule gives its peak.</summary>
    private static Draft TypeIa(LabInputs x, List<string> notes)
    {
        var ia = TypeIaModel.Explode(x, notes);
        return new Draft(Fate.Supernova, ia.Type, ia.Remnant, ia.Energy, ia.Ejecta, ia.Nickel, PeakRule.Arnett);
    }

    // ---------------------------------------------------------------- step 4: does the core explode?

    /// <summary>
    /// Step 4's verdict. An explosion carries step 5's inputs: energy, theory ⁵⁶Ni, and the remnant's baryonic mass (the
    /// neutron star, or the fallback black hole), plus the neutron star's gravitational mass when the source gives it.
    /// </summary>
    private sealed record Collapse(Confidence Confidence)
    {
        public Quantity? Energy { get; init; }
        public Quantity? Nickel { get; init; }
        public Quantity? RemnantBaryonic { get; init; }
        public Quantity? NeutronStarMass { get; init; }
        public bool Fallback { get; init; }
        /// <summary>S16 yield of a star above 12 M☉, which the calibrated default halves.</summary>
        public bool HalveCalibratedNickel { get; init; }
        /// <summary>The electron-capture yield, the same in theory and observation.</summary>
        public bool FixedNickel { get; init; }
        public bool SmallKick { get; init; }
        public double? Compactness { get; init; }
        public bool Explodes => Energy is not null;
    }

    private Draft CoreCollapse(LabInputs x, PresupernovaStar star, bool coresRaised, List<string> notes)
    {
        var c = Explodability(x, star, coresRaised, notes);
        // Step 7 a and c, applied after step 4, can override its verdict.
        return RotationBranch(x, star, c, notes)
            ?? (c.Explodes ? Exploded(x, star, c, notes) : Failed(x, star, c, notes));
    }

    private Collapse Explodability(LabInputs x, PresupernovaStar star, bool coresRaised, List<string> notes)
    {
        double m = x.InitialMass;
        // Ruling R12: a PISN-range core above the metallicity ceiling collapses directly to a black hole. ProgenitorModel
        // has already said so in a note; the ceiling for stripped stars is an approximation.
        if (star.HeCoreMass.Value >= PisnHeCore) return new Collapse(Confidence.Speculative);

        if (x.Stripping != Stripping.None) return StrippedStar(m, notes);

        // Rulings R1 / R16, step 4: the S16 per-mass results decide the fate only for H-rich single stars of M ≤ 30 M☉ —
        // at Z☉ the nearest row by initial mass, at other Z the row with the nearest CO core (the S16 M_CO column as the
        // conversion). Heavier stars follow step 4's M > 30 rules at every Z, Z☉ included, so the fate does not jump at
        // Z = 1. (This drops S16's wind-dependent 70 M☉ black-hole island, which is model-dependent anyway.)
        // Step 7d: cores that rotation has enlarged are matched on the CO core at every Z, as for other Z.
        if (m <= SingleStarLookupTop)
        {
            if (IsSolar(x.Metallicity) && !coresRaised) return FromRow(_table.Lookup(m), m, star, notes, matchNote: null);
            string why = coresRaised ? "With its cores enlarged by rotation" : "Away from the Sun's metal content";
            var row = NearestCoCore(star.CoCoreMass.Value);
            return FromRow(row, m, star, notes,
                $"{why}, whether it explodes is judged by its carbon-oxygen core ({UnitFormat.Mass(star.CoCoreMass.Value)}), " +
                $"matched to the solar model of {UnitFormat.Mass(row.InitialMass)} with the most similar core.");
        }
        return HeavySingleStar(star, notes);
    }

    /// <summary>The S16 row (M ≤ 30 M☉) whose CO core is nearest; the lighter on a tie.</summary>
    private ExplodabilityRow NearestCoCore(double co)
    {
        ExplodabilityRow best = _table.Rows[0];
        foreach (var row in _table.Rows)
        {
            if (row.InitialMass > SingleStarLookupTop) break;
            if (Math.Abs(row.CoCoreMass - co) < Math.Abs(best.CoCoreMass - co)) best = row;
        }
        return best;
    }

    /// <summary>
    /// The step 4 verdict of an S16 row: the star's own row (<paramref name="matchNote"/> null), or the row matched on the
    /// CO core, explained by <paramref name="matchNote"/>.
    /// </summary>
    private static Collapse FromRow(ExplodabilityRow row, double m, PresupernovaStar star, List<string> notes, string? matchNote)
    {
        bool solar = matchNote is null;
        // A matched row stands in for a core whose mass comes from an approximate scheme (§2.2 or step 7d), so Speculative.
        var conf = solar ? Confidence.ModelDependent : Confidence.Speculative;
        var src = solar ? S16Explosions : S16Explosions with
        {
            Citation = S16Explosions.Citation + $"; solar model of {row.InitialMass:0.##} M☉ matched on the CO-core mass",
        };
        string who = solar ? $"A star of {UnitFormat.Mass(m)}" : "This star";
        if (matchNote is not null) notes.Add(matchNote);

        Collapse c;
        switch (row.Result)
        {
            case CollapseResult.DirectBlackHole:
                notes.Add($"{who} collapses straight to a black hole in this model: its core is too compact for neutrinos " +
                          "to restart the shock.");
                c = new Collapse(conf) { Compactness = row.Compactness25 };
                break;
            case CollapseResult.FallbackBlackHole:
                notes.Add($"{who} explodes weakly, but so much matter falls back that the newborn neutron star collapses " +
                          "to a black hole.");
                // Step 5: a fallback black hole makes ⁵⁶Ni ≈ 0; the table's ~10⁻¹³ M☉ is numerical noise.
                c = new Collapse(conf)
                {
                    Energy = Q(row.ExplosionEnergyErg, "erg", conf, src),
                    Nickel = Q(0, MSun, conf, src with { Citation = src.Citation + "; fallback swallows the ⁵⁶Ni (M_Ni ≈ 0)" }),
                    RemnantBaryonic = Q(row.RemnantBaryonicMass, MSun, conf, src),
                    Fallback = true,
                };
                break;
            default:
                notes.Add($"{who} explodes in this model: neutrinos from the newborn neutron star restart the stalled shock.");
                c = new Collapse(conf)
                {
                    Energy = Q(row.ExplosionEnergyErg, "erg", conf, src),
                    Nickel = Q(row.NickelMass, MSun, conf, src),
                    RemnantBaryonic = Q(row.RemnantBaryonicMass, MSun, conf, src),
                    HalveCalibratedNickel = row.InitialMass > NickelCalibrationAbove,
                };
                break;
        }

        if (solar && m >= VariableBandLow && m <= VariableBandHigh)
            notes.Add("Between 15 and 22 M☉ the outcome changes with small differences in mass (Sukhbold et al. 2016), " +
                      "so a neighbouring mass may end differently.");
        return c;
    }

    /// <summary>
    /// Step 4, unstripped single stars above 30 M☉, at every Z including Z☉ (ruling R16): a direct black hole when the
    /// He core is ≳ 7.5 M☉, otherwise a wind-stripped Wolf–Rayet star that explodes.
    /// </summary>
    private static Collapse HeavySingleStar(PresupernovaStar star, List<string> notes)
    {
        // Consequence of the step 4 rule on ProgenitorModel's structure: above 120 M☉ at Z☉ the star keeps the 120 M☉
        // model's 5% (a bare He star of ~7.5 M☉ at 150 M☉), so 150 M☉ explodes as Ib (He 7.497 < 7.5) while heavier
        // stars, with bigger cores, collapse. Kept as the rules state it; the structure there is Speculative.
        double he = star.HeCoreMass.Value;
        if (he >= HeavyStarDirectCollapseHeCore)
        {
            notes.Add($"Its helium core of {UnitFormat.Mass(he)} is above about 7.5 M☉, so the core collapses straight to " +
                      "a black hole.");
            return new Collapse(Confidence.ModelDependent);
        }

        notes.Add($"Winds have stripped it to a compact {UnitFormat.Mass(star.FinalMass.Value)} Wolf–Rayet star. Models " +
                  "with strong explosion engines make such stars explode (Sukhbold et al. 2016), but this depends on the model.");
        var src = new Source("Sukhbold et al. 2016, ApJ 821, 38 (W18 / N20 engines, wind-stripped 60–120 M☉ stars)",
            "outcomes §1.4, §8.2 step 4");
        return new Collapse(Confidence.ModelDependent)
        {
            Energy = Q(0.6e51, "erg", Confidence.ModelDependent, src),
            Nickel = Q(0.08, MSun, Confidence.ModelDependent, src),
            // [APPROX] Step 4 gives no neutron-star mass for this branch. §1.4's band for these Wolf–Rayet stars has
            // neutron stars of ~1.7–2.0 M☉ baryonic; we take the middle.
            RemnantBaryonic = Q(1.85, MSun, Confidence.Speculative,
                src with { Citation = src.Citation + "; middle of the 1.7–2.0 M☉ baryonic band [APPROX]" }, 1.7, 2.0),
        };
    }

    /// <summary>Step 4, stripped stars: Sch21's Case A/B ranges, keyed on the initial mass.</summary>
    private static Collapse StrippedStar(double m, List<string> notes)
    {
        string mass = UnitFormat.Mass(m);
        if ((m > StrippedBlackHoleLow && m < StrippedBlackHoleHigh) || m >= StrippedBlackHoleAbove)
        {
            notes.Add($"Even stripped by a companion, a star of {mass} has a core too compact to explode (Schneider et al. " +
                      "2021), so it collapses to a black hole.");
            return new Collapse(Confidence.ModelDependent);
        }

        notes.Add($"Stripped by a companion, a star of {mass} has a core that neutrinos can explode (Schneider et al. " +
                  "2021), leaving a neutron star.");
        // Sch21: Ib/c explosion energies 0.88 ± 0.31 B, mean ⁵⁶Ni 0.059 M☉, stripped NS average 1.42 M☉.
        const double nsMass = 1.42;
        return new Collapse(Confidence.ModelDependent)
        {
            Energy = Q(0.88e51, "erg", Confidence.ModelDependent, Sch21, 0.57e51, 1.19e51),
            Nickel = Q(0.059, MSun, Confidence.ModelDependent, Sch21),
            NeutronStarMass = Q(nsMass, MSun, Confidence.ModelDependent, Sch21),
            RemnantBaryonic = Q(RemnantModel.BaryonicMass(nsMass), MSun, Confidence.ModelDependent,
                Sch21 with { Citation = Sch21.Citation + "; baryonic mass from Lattimer & Yahil 1989, ApJ 340, 426" }),
        };
    }

    /// <summary>Step 1, electron-capture supernova: E = 0.1 B, M_Ni = 0.003, NS 1.25 M☉, M_ej = M_f − 1.36.</summary>
    private static Collapse ElectronCapture(List<string> notes)
    {
        notes.Add("It explodes weakly (about 10⁵⁰ erg) and makes very little nickel, leaving a light neutron star.");
        const string refs = "outcomes §1.1, §8.2 step 1";
        var md = Confidence.ModelDependent;
        return new Collapse(md)
        {
            Energy = Q(1e50, "erg", md, new Source("Kitaura, Janka & Hillebrandt 2006, A&A 450, 345", refs)),
            Nickel = Q(0.003, MSun, md, new Source("Wanajo et al. 2011, as quoted in Sukhbold et al. 2016 §4.2", refs)),
            NeutronStarMass = Q(1.25, MSun, md,
                new Source("Sukhbold et al. 2016 §5.1; Schwab, Podsiadlowski & Rappaport 2010, ApJ 719, 722", refs)),
            RemnantBaryonic = Q(1.36, MSun, md, new Source("Sukhbold et al. 2016 §5.1 (ECSN neutron star, baryonic)", refs)),
            FixedNickel = true,
            SmallKick = true,
        };
    }

    // ---------------------------------------------------------------- steps 5, 6 and 8

    private static Draft Exploded(LabInputs x, PresupernovaStar star, Collapse c, List<string> notes)
    {
        var (baseType, typeNote) = SpectralTyper.Classify(star, x.InitialMass, x.Stripping, x.Mixing);
        int typeNoteAt = notes.Count;
        notes.Add(typeNote);

        var energy = c.Energy!;
        var remnantBaryonic = c.RemnantBaryonic!;
        var neutronStarMass = c.NeutronStarMass;
        bool smallKick = c.SmallKick;
        double mf = star.FinalMass.Value;
        Quantity ejecta;
        if (mf - remnantBaryonic.Value < MinEjecta)
        {
            // Ruling R9 [APPROX]: the lightest stripped stars (H&He-stripped below ~10 M☉, M_f ≈ M_CO + 0.05) are lighter
            // than a neutron star plus ejecta. Such ultra-stripped stars are taken to eject 0.1 M☉, and the remnant is
            // what is left.
            // TooLightToCollapse (R13) has already sent stars below 1.408 M☉ to a white dwarf, so this remnant is at
            // least 1.308 M☉ baryonic = 1.2 M☉ gravitational.
            var floor = new Source("[APPROX] ultra-stripped supernova: ejecta floored at 0.1 M☉, remnant M_f − 0.1 M☉",
                "outcomes §3.3, §8.2 step 5");
            ejecta = Q(MinEjecta, MSun, Confidence.Speculative, floor);
            remnantBaryonic = Q(mf - MinEjecta, MSun, Confidence.Speculative, floor);
            neutronStarMass = null;
            smallKick = true;
            energy = energy with { Confidence = Confidence.Speculative };
            notes.Add($"The stripped star ({UnitFormat.Mass(mf)}) is barely heavier than a neutron star. As an " +
                      "ultra-stripped supernova it is taken to throw off only 0.1 M☉ and leave a lighter neutron star; " +
                      "this is an approximation.");
        }
        else
        {
            ejecta = Q(mf - remnantBaryonic.Value, MSun, RemnantModel.Weakest(star.FinalMass.Confidence, remnantBaryonic.Confidence),
                new Source("M_ej = M_f − M_rem,bary (Sukhbold et al. 2016, ApJ 821, 38)", "outcomes §1.4, §8.2 step 5"));
        }

        bool ultraStripped = false;
        if (x.Stripping != Stripping.None && !c.Fallback && ejecta.Value <= UltraStrippedMaxEjecta)
        {
            // Ruling R13 [APPROX]: an ultra-stripped explosion has E ~ 10⁵⁰ erg, not Sch21's 0.88 B. Tauris, Langer &
            // Podsiadlowski 2015 (arXiv:1505.00270) scale their ultra-stripped light curves in units of 10⁵⁰ erg and
            // quote ≲ 10⁵⁰ erg from electron-capture explosion models (Kitaura et al. 2006); no fitted value is given.
            energy = Q(UltraStrippedEnergy, "erg", Confidence.Speculative,
                new Source("Tauris, Langer & Podsiadlowski 2015 (arXiv:1505.00270), energy scale ~10^50 erg; " +
                           "Kitaura, Janka & Hillebrandt 2006, A&A 450, 345 [APPROX]", "outcomes §1.1, §8.2 step 5"));
            notes.Add($"With only {UnitFormat.Mass(ejecta.Value)} of ejecta this is an ultra-stripped supernova, a weak " +
                      "explosion of about 10⁵⁰ erg.");
            // §6: ultra-stripped neutron stars get the slow kick component.
            smallKick = true;
            ultraStripped = true;
        }

        Remnant remnant;
        if (c.Fallback)
            remnant = RemnantModel.BlackHole(remnantBaryonic, RemnantModel.FallbackFormationTime(),
                RemnantModel.BlackHoleKick(remnantBaryonic.Value, directCollapse: false));
        else
        {
            remnant = neutronStarMass is not null
                ? RemnantModel.NeutronStar(neutronStarMass, smallKick)
                : RemnantModel.FromBaryonicMass(remnantBaryonic, smallKick);
            if (remnant.Kind == RemnantKind.BlackHole)
                notes.Add("The neutron star would be heavier than about 2.2 M☉, more than a neutron star can hold, so it " +
                          "collapses to a black hole.");
        }

        // Ruling R15: the theory yield for fallback, electron-capture and ultra-stripped explosions and for the Theory
        // option; otherwise the observed-calibrated yield. Either way at most half the ejecta.
        var nickel = c.Fallback || c.FixedNickel || ultraStripped || x.Yield == NickelYield.Theory
            ? c.Nickel!
            : CalibratedNickel(c, baseType);
        nickel = CapNickel(nickel, ejecta.Value);

        var type = baseType;
        var peak = SpectralTyper.HasPlateau(baseType) ? PeakRule.Plateau : PeakRule.Arnett;
        bool peakSpeculative = x.DenseCsm || baseType == SupernovaType.IIL;
        Quantity? spinDownEnergy = null, spinDownTime = null;
        var branch = Step7.None;
        if (x.Field == CoreField.Strong && remnant.Kind == RemnantKind.NeutronStar)
        {
            // Step 7b: a strong core field makes the neutron star a magnetar.
            var mag = Magnetar(x, star, remnant, energy, ejecta, notes);
            (remnant, energy, spinDownEnergy, spinDownTime, branch) =
                (mag.Remnant, mag.Energy, mag.SpinDownEnergy, mag.SpinDownTime, Step7.Magnetar);
            if (mag.PowersLight)
            {
                peak = PeakRule.Magnetar;
                peakSpeculative = true;
            }
            if (mag.Superluminous is { } slsn)
            {
                // Ruling R22: the superluminous class replaces the base class, so its sentence goes.
                type = slsn;
                notes.RemoveAt(typeNoteAt);
            }
        }
        type = WithDenseCsm(x, type, notes);

        return new Draft(Fate.Supernova, type, remnant, energy, ejecta, nickel, peak, peakSpeculative)
        {
            MagnetarEnergy = spinDownEnergy, MagnetarTimescale = spinDownTime, Branch = branch,
        };
    }

    /// <summary>
    /// The "pre-supernova eruption / dense CSM" override (step 6): narrow lines from the surrounding gas, where the class
    /// has a narrow-line version (<see cref="SpectralTyper.WithDenseCsm"/>).
    /// </summary>
    private static SupernovaType WithDenseCsm(LabInputs x, SupernovaType type, List<string> notes)
    {
        if (!x.DenseCsm) return type;
        var csm = SpectralTyper.WithDenseCsm(type);
        notes.Add(csm != type
            ? "Dense gas thrown off shortly before the explosion surrounds the star; the collision makes narrow " +
              $"lines, so it is seen as a Type {csm} supernova (a speculative mapping)."
            : "Dense gas thrown off shortly before the explosion surrounds the star. The collision adds light, but the " +
              "lab does not change the class of this kind of supernova for it.");
        return csm;
    }

    /// <summary>
    /// Step 5, "observed-calibrated" ⁵⁶Ni [APPROXIMATION]; ruling R15 reads "clip it to the observed medians" as
    /// min(theory, median), lowering a yield and never raising it. Theory exceeds the observed SN II median about twice,
    /// so S16 yields above 12 M☉ are halved for II-P / II-L; stripped-envelope classes are clipped to Anderson 2019's
    /// medians (IIb 0.102, Ib 0.163, Ic 0.155 M☉).
    /// </summary>
    private static Quantity CalibratedNickel(Collapse c, SupernovaType baseType)
    {
        var theory = c.Nickel!;
        const string refs = "outcomes §1.2, §8.2 step 5";
        if (SpectralTyper.HasPlateau(baseType))
            return c.HalveCalibratedNickel
                ? Q(0.5 * theory.Value, MSun, Confidence.Speculative, new Source(theory.Source.Citation +
                    "; halved to match the observed SN II median 0.032 M☉ (Anderson 2019, A&A 628, A7) [APPROX]", refs))
                : theory;

        double median = baseType switch { SupernovaType.IIb => 0.102, SupernovaType.Ib => 0.163, _ => 0.155 };
        if (theory.Value <= median) return theory;
        return Q(median, MSun, Confidence.Speculative,
            new Source($"{theory.Source.Citation}; clipped to the observed median for SNe {baseType} " +
                       "(Anderson 2019, A&A 628, A7) [APPROX]", refs));
    }

    /// <summary>
    /// Ruling R15 [APPROX]: ⁵⁶Ni is at most half the ejecta, so a tiny explosion (ultra-stripped, or one with heavy
    /// fallback) cannot be made mostly of nickel. Ruling R17: core-collapse and pair-instability outcomes only; Type Ia
    /// and Iax keep their published yields (<see cref="TypeIaModel"/>).
    /// </summary>
    private static Quantity CapNickel(Quantity nickel, double ejecta)
    {
        double cap = NickelMaxEjectaFraction * ejecta;
        if (nickel.Value <= cap) return nickel;
        return Q(cap, MSun, Confidence.Speculative, new Source(nickel.Source.Citation +
            "; capped at half the ejecta mass, since an explosion cannot be mostly nickel [APPROX]", "outcomes §8.2 step 5"));
    }

    private static Draft Failed(LabInputs x, PresupernovaStar star, Collapse c, List<string> notes)
    {
        // Step 5, failed SN: M_BH = M_He (S16 default), up to M_f − 0.3 M☉ if the whole star falls in.
        double he = star.HeCoreMass.Value, mf = star.FinalMass.Value, whole = mf - NeutrinoMassLossMax;
        var mass = Q(he, MSun, RemnantModel.Weakest(star.HeCoreMass.Confidence, c.Confidence),
            new Source("Sukhbold et al. 2016, ApJ 821, 38 §5.2 (black hole = He core; at most M_f − 0.3 M☉)",
                "outcomes §1.5, §8.2 step 5"), Math.Min(he, whole), Math.Max(he, whole));
        if (mf - he > 0.01)
            notes.Add($"Its loosely bound outer layers ({UnitFormat.Mass(mf - he)}) may be thrown off in a faint outburst " +
                      "as the core collapses (Lovegrove & Woosley 2013).");
        if (x.DenseCsm)
            notes.Add("With no explosion, the dense gas around the star has nothing to light it up.");

        var remnant = RemnantModel.BlackHole(mass, RemnantModel.DirectCollapseTime(c.Compactness, c.Confidence),
            RemnantModel.BlackHoleKick(he, directCollapse: true));
        return new Draft(Fate.FailedSupernova, SupernovaType.None, remnant);
    }

    // ---------------------------------------------------------------- helpers

    private static bool IsSolar(double z) => Math.Abs(z - 1) < 1e-9;

    private static double Lerp(double a, double b, double f) => a + (b - a) * f;

    private static Quantity Q(double value, string unit, Confidence confidence, Source source, double? low = null, double? high = null)
        => new(value, unit, confidence, source, low, high);
}
