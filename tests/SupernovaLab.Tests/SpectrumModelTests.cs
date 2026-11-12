using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Tests;

public class SpectrumModelTests
{
    private static readonly SpectrumModel Model = new();
    private static readonly LightCurveModel Curves = new();
    private const double Day = PhysicalConstants.Day, CKmS = 299792.458;

    private static readonly Outcome RedSupergiant16 = TestOutcomes.Star(16); // ruling R8: explodes as II-P

    private static Outcome Custom(SupernovaType t, bool grb = false)
        => TestOutcomes.Custom(t, 1e51, t == SupernovaType.Ia ? 1.38 : 3, 0.3, 1e11, hasGrb: grb);

    private static bool Has(Spectrum s, string label) => s.Lines.Any(l => l.Label == label);

    /// <summary>The wavelength (A) of the lowest flux in [lo, hi].</summary>
    private static double MinimumIn(Spectrum s, double lo, double hi)
    {
        int best = -1;
        for (int i = 0; i < s.WavelengthsAngstrom.Length; i++)
            if (s.WavelengthsAngstrom[i] >= lo && s.WavelengthsAngstrom[i] <= hi && (best < 0 || s.RelativeFlux[i] < s.RelativeFlux[best]))
                best = i;
        return s.WavelengthsAngstrom[best];
    }

    [Fact]
    public void IIP_at_20_days_has_Halpha_absorption_blueshifted_6000_to_12000_kms()
    {
        // Hand check (ruling R43). H-alpha is anchored at 7,300 km/s at day 50 (signals 10.2, Gutierrez 2017) and follows
        // v = 7300 (t/50 d)^-0.46 back to the V peak, so at day 20 (after the peak):
        // 7300 x (20/50)^-0.46 = 7300 x exp(0.46 x 0.916291 = 0.421494) = 7300 x 1.52424 = 11,127 km/s (about 11,130).
        // Ruling R53: v_ref is scaled by √((E/M_ej) / (1e51 erg / 10 M☉)). The default 16 M☉ II-P has E = 1.25537e51 erg and
        // M_ej = 11.59828 M☉: E/M = 1.08238e50, √1.08238 = 1.040375, so the day-50 anchor is 7300 x 1.040375 = 7,594.7 km/s.
        // At day 20: 7594.7 x 1.52424 = 11,576 km/s. Absorption minimum: 6563 x (1 - 11576/299792.458) = 6309.6 A, inside
        // the accepted 6,000-12,000 km/s band, 6563 x (1 - 0.040028) = 6300.3 A to 6563 x (1 - 0.020014) = 6431.7 A.
        var s = Model.At(RedSupergiant16, 20 * Day);
        Assert.True(Has(s, "Hα"));
        double lambdaMin = MinimumIn(s, 6150, 6563);
        double v = CKmS * (6563 - lambdaMin) / 6563;
        Assert.InRange(v, 6000, 12000);
        Assert.Equal(11576, s.PhotosphereVelocityKmS, 1.0);
    }

    [Fact]
    public void Halpha_velocity_falls_with_time()
    {
        Assert.True(Model.At(RedSupergiant16, 20 * Day).PhotosphereVelocityKmS
                    > Model.At(RedSupergiant16, 50 * Day).PhotosphereVelocityKmS);
    }

    [Fact]
    public void Hydrogen_velocity_follows_the_day_50_anchor_and_the_minus_0_46_law()
    {
        // At 100 d: (100/50)^-0.46 = exp(-0.46 x 0.693147 = -0.318848) = 0.726985; 7300 x 0.726985 = 5,307.0 km/s before
        // ruling R53's scaling.
        // Ruling R53: v_ref is scaled by √((E/M_ej) / (1e51 erg / 10 M☉)). The default 16 M☉ II-P has E = 1.25537e51 erg and
        // M_ej = 11.59828 M☉: E/M = 1.08238e50, √1.08238 = 1.040375, so the day-50 anchor is 7300 x 1.040375 = 7,594.7 km/s.
        // 7594.7 x 0.726985 = 5,521.2 km/s.
        var s = Model.At(RedSupergiant16, 100 * Day);
        Assert.Equal(5521.2, s.PhotosphereVelocityKmS, 1.0);
    }

    [Fact]
    public void Ia_at_peak_has_Si_II_6355_absorbed_near_6130_A()
    {
        // Hand check: Ia v = 10,600 km/s at V maximum (Wang 2009), scaled by ruling R53 against the Ia reference
        // 1.3e51 erg / 1.4 M☉: the default Ia has 1.35e51 erg in 1.38 M☉, (0.978261 / 0.928571) = 1.053511, √ = 1.026407,
        // v = 10,879.9 km/s. 6355 x (1 - 10879.9/299792.458) = 6355 x (1 - 0.036292) = 6124.4 A (the research quotes "near
        // 6150 A").
        var ia = TestOutcomes.TypeIa();
        double tPeak = Curves.PeakTimeS(ia);
        var s = Model.At(ia, tPeak);
        Assert.True(Has(s, "Si II 6355"));
        Assert.False(Has(s, "Hα"));
        Assert.Equal(10879.9, s.PhotosphereVelocityKmS, 0.1);
        Assert.InRange(MinimumIn(s, 5900, 6355), 6080, 6180);
        Assert.False(s.Nebular);
    }

    [Fact]
    public void Ia_velocity_follows_the_law_after_peak_and_hits_the_floor()
    {
        // v_ref = 10,879.9 km/s (ruling R53's scaling, see above). At 5 t_peak: 5^-0.46 = exp(-0.46 x 1.609438 = -0.740342)
        // = 0.476951, so 10879.9 x 0.476951 = 5,189.2 km/s.
        var ia = TestOutcomes.TypeIa();
        double tPeak = Curves.PeakTimeS(ia);
        Assert.Equal(5189.2, Model.At(ia, 5 * tPeak).PhotosphereVelocityKmS, 0.2);
        // At 1 yr the law gives less than 0.3 v_ref (t/t_peak > 0.3^(-1/0.46) = 13.7 for t_peak < 26 d):
        // the floor 0.3 x 10879.9 = 3,264.0 km/s applies.
        Assert.True(tPeak < 26 * Day);
        Assert.Equal(3264.0, Model.At(ia, PhysicalConstants.Year).PhotosphereVelocityKmS, 0.1);
        // Before peak the velocity is held at v_ref.
        Assert.Equal(10879.9, Model.At(ia, 0.5 * tPeak).PhotosphereVelocityKmS, 0.1);
    }

    [Fact]
    public void Ic_has_neither_hydrogen_nor_He_I_5876()
    {
        var s = Model.At(Custom(SupernovaType.Ic), 20 * Day);
        Assert.NotEmpty(s.Lines);
        Assert.DoesNotContain(s.Lines, l => l.Label.StartsWith("H") && !l.Label.StartsWith("He"));
        Assert.DoesNotContain(s.Lines, l => l.Label.StartsWith("He"));
        Assert.True(Has(s, "O I 7774"));
    }

    [Fact]
    public void Ib_has_He_I_5876_but_not_Halpha()
    {
        var o = Custom(SupernovaType.Ib);
        var s = Model.At(o, 20 * Day);
        Assert.True(Has(s, "He I 5876"));
        Assert.False(Has(s, "Hα"));
        // Fe II 9,100 km/s at V maximum (Liu 2016).
        Assert.Equal(9100, Model.At(o, Curves.PeakTimeS(o)).PhotosphereVelocityKmS, 6);
    }

    [Fact]
    public void IIb_starts_with_hydrogen_and_helium_strengthens_within_weeks()
    {
        var o = Custom(SupernovaType.IIb);
        var early = Model.At(o, 3 * Day);
        var late = Model.At(o, 45 * Day);
        double Strength(Spectrum s, string label) => s.Lines.Single(l => l.Label == label).Strength;
        // Hand values: H-alpha(3 d) = 1 (flat to day 10); H-alpha(45 d) = 1 - 0.7 = 0.3 (the ramp ends at day 40);
        // He I 5876 (3 d) = 0.1 + 0.9 x 3/30 = 0.19; (45 d) = 1.
        Assert.Equal(1.0, Strength(early, "Hα"), 9);
        Assert.Equal(0.3, Strength(late, "Hα"), 9);
        Assert.Equal(0.19, Strength(early, "He I 5876"), 9);
        Assert.Equal(1.0, Strength(late, "He I 5876"), 9);
    }

    /// <summary>The width (km/s) of the O I 7774 absorption trough at half its depth, measured in the spectrum itself on the
    /// flux divided by the blackbody continuum, from the line-free continuum at 6000 A and converted at 7774 A.</summary>
    private static double OxygenTroughWidthKmS(Spectrum s)
    {
        const double H = 6.62607015e-27, C = 2.99792458e10, K = 1.380649e-16;
        double Bb(double a) { double l = a * 1e-8; return Math.Pow(l, -5) / (Math.Exp(H * C / (l * K * s.ColorTemperatureK)) - 1); }
        var wl = s.WavelengthsAngstrom;
        double[] r = new double[wl.Length];
        for (int i = 0; i < wl.Length; i++) r[i] = s.RelativeFlux[i] / Bb(wl[i]);
        int lo = Array.FindIndex(wl, w => w >= 6000), hi = Array.FindIndex(wl, w => w >= 7774);
        int min = lo;
        for (int i = lo; i <= hi; i++) if (r[i] < r[min]) min = i;
        double half = r[min] + 0.5 * (r[lo] - r[min]);
        int a = min, b = min;
        while (a > 0 && r[a] < half) a--;
        while (b < wl.Length - 1 && r[b] < half) b++;
        return CKmS * (wl[b] - wl[a]) / 7774;
    }

    [Fact]
    public void IcBL_lines_are_wider_than_15000_kms()
    {
        // Hand values: tracer Fe II 16,700 km/s without a GRB, 19,200 with one (Modjaz 2016); O I is 13,600/11,000 = 1.2364
        // times faster (Liu 2016), so its FWHM = v/c x lambda gives 20,647 km/s (no GRB) and 23,737 km/s (GRB), i.e. 0.0689 x
        // 7774 = 535 A and 0.0792 x 7774 = 616 A. The width is measured from the spectrum (the O I trough at half its depth
        // over the blackbody continuum); one grid step is 10 A = 386 km/s at 7774 A, and the red-side emission adds a little.
        // Ruling R53: these Ic-BL outcomes carry their type references' energy per mass (Taddia 2019's 7e51 erg / 4 M☉;
        // with a GRB Cano 2017's 2.5e52 erg / 6 M☉), so the scaling factor is 1.
        foreach (bool grb in new[] { false, true })
        {
            var o = grb ? TestOutcomes.Custom(SupernovaType.IcBL, 2.5e52, 6, 0.4, 1e11, hasGrb: true)
                        : TestOutcomes.Custom(SupernovaType.IcBL, 7e51, 4, 0.3, 1e11);
            var s = Model.At(o, Curves.PeakTimeS(o));
            double tracer = grb ? 19200 : 16700;
            Assert.Equal(tracer, s.PhotosphereVelocityKmS, 6);
            double width = OxygenTroughWidthKmS(s);
            Assert.True(width > 15000, $"width {width}");
            Assert.InRange(width, tracer * 13600.0 / 11000 - 1500, tracer * 13600.0 / 11000 + 1500);
        }
        // A plain Ic (O I at 13,600 km/s) is narrower than 15,000 km/s.
        var ic = Custom(SupernovaType.Ic);
        double icWidth = OxygenTroughWidthKmS(Model.At(ic, Curves.PeakTimeS(ic)));
        Assert.InRange(icWidth, 13600 - 1500, 13600 + 1500);
    }

    [Fact]
    public void IIP_Fe_II_5169_moves_at_about_half_the_Halpha_velocity()
    {
        // Hand values (R45b): Fe II/Halpha = 3,700/7,300 = 0.5068 (signals 10.2). At 30 d (after the day-15 hold),
        // Halpha v = 7300 x 0.6^-0.46 = 7300 x exp(0.46 x 0.510826 = 0.234980) = 7300 x 1.26480 = 9,233 km/s,
        // so Fe II v = 4,680 km/s and its minimum is 5169 x (1 - 0.015611) = 5088.3 A (grid step 10 A = 580 km/s).
        var s = Model.At(RedSupergiant16, 30 * Day);
        double vFe = CKmS * (5169 - MinimumIn(s, 4950, 5169)) / 5169;
        double vHa = CKmS * (6563 - MinimumIn(s, 6150, 6563)) / 6563;
        Assert.InRange(vFe, 4680 - 700, 4680 + 700);
        Assert.InRange(vFe / vHa, 0.4, 0.62);
    }

    [Fact]
    public void IIb_Halpha_moves_faster_than_the_Fe_II_tracer()
    {
        // Hand value (R45b): Halpha 12,500 over Fe II 8,400 (Liu 2016) = 1.488 at V maximum.
        var o = Custom(SupernovaType.IIb);
        var s = Model.At(o, Curves.PeakTimeS(o));
        double vHa = CKmS * (6563 - MinimumIn(s, 6000, 6563)) / 6563;
        Assert.InRange(vHa / s.PhotosphereVelocityKmS, 1.25, 1.75);
    }

    [Fact]
    public void Iax_is_slow_5000_kms()
    {
        // R45c: outcomes 3.2 gives 2,000-8,000 km/s; the middle, 5,000 km/s, held to the V peak.
        var o = Custom(SupernovaType.Iax);
        Assert.Equal(5000, Model.At(o, Curves.PeakTimeS(o)).PhotosphereVelocityKmS, 6);
        Assert.Equal(5000, Model.At(o, 2 * Day).PhotosphereVelocityKmS, 6);
    }

    [Fact]
    public void Hydrogen_velocity_is_held_before_day_15()
    {
        // R45d: the V peak of the 16 M-sun II-P is at 8.5 d, earlier than 15 d, so the hold is at 15 d:
        // 7300 x (15/50)^-0.46 = 7300 x exp(0.46 x 1.203973 = 0.553828) = 7300 x 1.73990 = 12,701 km/s before ruling R53,
        // which scales the anchor by 1.040375 (see above): 12,701 x 1.040375 = 13,213.8 km/s.
        Assert.True(Curves.PeakTimeS(RedSupergiant16) < 15 * Day);
        double v5 = Model.At(RedSupergiant16, 5 * Day).PhotosphereVelocityKmS;
        double v15 = Model.At(RedSupergiant16, 15 * Day).PhotosphereVelocityKmS;
        Assert.Equal(v15, v5, 9);
        Assert.InRange(v15, 13212, 13216);
    }

    [Fact]
    public void Interacting_types_keep_narrow_Halpha_in_the_nebular_phase()
    {
        var iin = TestOutcomes.Custom(SupernovaType.IIn, 1e51, 10, 0.05, 500 * PhysicalConstants.RSun);
        var early = Model.At(iin, 30 * Day);
        Assert.True(Has(early, "Hα"));
        var neb = Model.At(iin, 300 * Day);
        Assert.True(neb.Nebular);
        Assert.True(Has(neb, "Hα"));
        Assert.True(Has(neb, "[O I] 6300"));
        // Narrow: 1,000 km/s FWHM = 22 A at 6563 A, so the line is a one-to-three point spike.
        int i0 = Array.FindIndex(neb.WavelengthsAngstrom, w => w >= 6500), n = 0;
        for (int i = i0; i < neb.WavelengthsAngstrom.Length && neb.WavelengthsAngstrom[i] <= 6630; i++)
            if (neb.RelativeFlux[i] > 0.5 * neb.RelativeFlux.Max()) n++;
        Assert.InRange(n, 1, 4);
    }

    [Fact]
    public void Arrays_are_empty_before_one_day_after_one_year_and_for_failed_supernovae()
    {
        foreach (double t in new[] { 0.5 * Day, 0.0, 1.1 * PhysicalConstants.Year })
        {
            var s = Model.At(RedSupergiant16, t);
            Assert.Empty(s.WavelengthsAngstrom);
            Assert.Empty(s.RelativeFlux);
            Assert.Empty(s.Lines);
        }
        var failed = TestOutcomes.Custom(SupernovaType.None, null, null, null, 1e13, fate: Fate.FailedSupernova);
        Assert.Empty(Model.At(failed, 20 * Day).RelativeFlux);
    }

    [Fact]
    public void Spectrum_has_700_points_over_3000_to_10000_A_normalized_to_one()
    {
        var s = Model.At(RedSupergiant16, 20 * Day);
        Assert.Equal(700, s.WavelengthsAngstrom.Length);
        Assert.Equal(700, s.RelativeFlux.Length);
        Assert.Equal(3000, s.WavelengthsAngstrom[0], 9);
        Assert.Equal(10000, s.WavelengthsAngstrom[^1], 9);
        Assert.Equal(1.0, s.RelativeFlux.Max(), 12);
        Assert.All(s.RelativeFlux, f => Assert.True(f > 0));
    }

    [Theory]
    [InlineData(16)]
    [InlineData(12)]
    public void Early_colour_is_hot_never_a_clamped_1000_K_and_the_light_curve_is_bright(double initialMass)
    {
        // Ruling R45a. Before shock cooling starts (about 2.5-3 d for a red supergiant) the colour is the cooling colour at its
        // start, the hottest, never NaN (which the inversion used to turn into 1,000 K, and the light curve into M_V -2.4).
        var o = TestOutcomes.Star(initialMass);
        var lc = LightCurve.Of(o)!;
        for (double d = 1; d <= 5.0001; d += 0.5)
        {
            var s = Model.At(o, d * Day);
            Assert.InRange(s.ColorTemperatureK, 5000, 50000);
            double mv = lc.AbsoluteV(d * Day);
            Assert.True(double.IsFinite(mv) && mv < -14, $"M_V {mv} at {d} d");
        }
    }

    [Fact]
    public void Colour_temperature_is_exactly_the_hot_photosphere_before_a_type_Ia_peak()
    {
        // Hand value: a Type Ia before maximum has B-V of a 10,000 K blackbody (LightCurve.HotPhotosphereK), so the
        // temperature recovered from that colour is 10,000 K (bisection in log T, 80 steps: well inside 1 K).
        var ia = TestOutcomes.TypeIa();
        Assert.Equal(10000, Curves.ColourTemperatureK(ia, 2 * Day), 0);
        Assert.True(double.IsNaN(BolometricCorrection.BlackbodyTemperatureForBMinusV(double.NaN)));
    }

    [Fact]
    public void Colour_temperature_is_the_light_curves()
    {
        // A hydrogen-free explosion at day 40 sits near the light curve's hot-photosphere colour, 10,000 K (signals 4.5,
        // middle of 8,000-12,000 K), within 10% where shock cooling still adds blue light.
        var o = Custom(SupernovaType.Ic);
        var s = Model.At(o, 40 * Day);
        Assert.Equal(Curves.ColourTemperatureK(o, 40 * Day), s.ColorTemperatureK, 6);
        Assert.InRange(s.ColorTemperatureK, 9000, 11000);
        // A plateau II-P at day 60 sits at hydrogen recombination, 6,000 K.
        var iip = Model.At(RedSupergiant16, 60 * Day);
        Assert.InRange(iip.ColorTemperatureK, 5500, 7000);
    }

    [Fact]
    public void Nebular_phase_switches_to_nebular_emission_lines()
    {
        var iip = Model.At(RedSupergiant16, 300 * Day);
        Assert.True(iip.Nebular);
        Assert.True(Has(iip, "[O I] 6300"));
        Assert.True(Has(iip, "[Ca II] 7291"));
        Assert.True(Has(iip, "Hα"));
        Assert.False(Model.At(RedSupergiant16, 100 * Day).Nebular);
        // [O I] 6364 is a third of 6300 (3:1, optically thin).
        Assert.Equal(1.0 / 3, iip.Lines.Single(l => l.Label == "[O I] 6364").Strength, 9);

        var ia = TestOutcomes.TypeIa();
        Assert.False(Model.At(ia, 150 * Day).Nebular);
        var neb = Model.At(ia, 300 * Day);
        Assert.True(neb.Nebular);
        Assert.True(Has(neb, "[Fe III] 4658"));
        Assert.False(Has(neb, "Si II 6355"));
        // Stripped core collapse: [O I] but no H-alpha.
        Assert.False(Has(Model.At(Custom(SupernovaType.Ic), 300 * Day), "Hα"));
    }

    [Fact]
    public void Model_labels_are_speculative_and_cite_section_10()
    {
        Assert.Equal(Confidence.Speculative, Model.Confidence);
        Assert.Contains(Model.Sources, s => s.ResearchRef.Contains("§10"));
    }
}
