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
    public void IIP_at_20_days_has_Halpha_absorption_blueshifted_6000_to_10000_kms()
    {
        // Hand check. H-alpha is anchored at 7,300 km/s at day 50 (signals 10.2, Gutierrez 2017) and held before day 50
        // (class remarks), so v = 7,300 km/s at day 20. (Extrapolating the t^-0.46 law back would give
        // 7300 x (20/50)^-0.46 = 7300 x exp(0.46 x 0.91629) = 11,130 km/s, outside 6,000-10,000.)
        // Absorption minimum: 6563 x (1 - 7300/299792.458) = 6563 x (1 - 0.024350) = 6403.2 A. The accepted 6,000-10,000 km/s
        // band is 6563 x (1 - 0.033356) = 6344.1 A to 6563 x (1 - 0.020014) = 6431.7 A.
        var s = Model.At(RedSupergiant16, 20 * Day);
        Assert.True(Has(s, "Hα"));
        double lambdaMin = MinimumIn(s, 6150, 6563);
        double v = CKmS * (6563 - lambdaMin) / 6563;
        Assert.InRange(v, 6000, 10000);
        Assert.Equal(7300, s.PhotosphereVelocityKmS, 6);
    }

    [Fact]
    public void Hydrogen_velocity_follows_the_day_50_anchor_and_the_minus_0_46_law()
    {
        // At 100 d: (100/50)^-0.46 = exp(-0.46 x 0.693147 = -0.318848) = 0.726985; 7300 x 0.726985 = 5,307.0 km/s.
        var s = Model.At(RedSupergiant16, 100 * Day);
        Assert.Equal(5307.0, s.PhotosphereVelocityKmS, 0);
    }

    [Fact]
    public void Ia_at_peak_has_Si_II_6355_absorbed_near_6130_A()
    {
        // Hand check: Ia v = 10,600 km/s at V maximum (Wang 2009). 6355 x (1 - 10600/299792.458) = 6355 x (1 - 0.035357)
        // = 6130.3 A (the research quotes "near 6150 A").
        var ia = TestOutcomes.TypeIa();
        double tPeak = Curves.PeakTimeS(ia);
        var s = Model.At(ia, tPeak);
        Assert.True(Has(s, "Si II 6355"));
        Assert.False(Has(s, "Hα"));
        Assert.Equal(10600, s.PhotosphereVelocityKmS, 6);
        Assert.InRange(MinimumIn(s, 5900, 6355), 6080, 6180);
        Assert.False(s.Nebular);
    }

    [Fact]
    public void Ia_velocity_follows_the_law_after_peak_and_hits_the_floor()
    {
        // At 5 t_peak: 10600 x 5^-0.46 = 10600 x exp(-0.46 x 1.609438 = -0.740342) = 10600 x 0.476951 = 5,055.7 km/s.
        var ia = TestOutcomes.TypeIa();
        double tPeak = Curves.PeakTimeS(ia);
        Assert.Equal(5055.7, Model.At(ia, 5 * tPeak).PhotosphereVelocityKmS, 0);
        // At 1 yr the law gives less than 0.3 v_ref (t/t_peak > 0.3^(-1/0.46) = 13.7 for t_peak < 26 d):
        // the floor 0.3 x 10600 = 3,180 km/s applies.
        Assert.True(tPeak < 26 * Day);
        Assert.Equal(3180, Model.At(ia, PhysicalConstants.Year).PhotosphereVelocityKmS, 6);
        // Before peak the velocity is held at v_ref.
        Assert.Equal(10600, Model.At(ia, 0.5 * tPeak).PhotosphereVelocityKmS, 6);
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
    /// flux divided by the blackbody continuum.</summary>
    private static double OxygenTroughWidthKmS(Spectrum s)
    {
        const double H = 6.62607015e-27, C = 2.99792458e10, K = 1.380649e-16;
        double Bb(double a) { double l = a * 1e-8; return Math.Pow(l, -5) / (Math.Exp(H * C / (l * K * s.ColorTemperatureK)) - 1); }
        var wl = s.WavelengthsAngstrom;
        double[] r = new double[wl.Length];
        for (int i = 0; i < wl.Length; i++) r[i] = s.RelativeFlux[i] / Bb(wl[i]);
        int lo = Array.FindIndex(wl, w => w >= 6900), hi = Array.FindIndex(wl, w => w >= 7774);
        int min = lo;
        for (int i = lo; i <= hi; i++) if (r[i] < r[min]) min = i;
        double baseline = Math.Max(r[lo], r[hi]);
        double half = r[min] + 0.5 * (baseline - r[min]);
        int a = min, b = min;
        while (a > 0 && r[a] < half) a--;
        while (b < wl.Length - 1 && r[b] < half) b++;
        return CKmS * (wl[b] - wl[a]) / wl[min];
    }

    [Fact]
    public void IcBL_lines_are_wider_than_15000_kms()
    {
        // Hand values: Fe II 16,700 km/s without a GRB, 19,200 with one (Modjaz 2016); FWHM = v/c x lambda = 0.0557 x 7774
        // = 433 A for O I without a GRB. The width is measured from the spectrum (the O I trough at half its depth over the
        // blackbody continuum).
        foreach (bool grb in new[] { false, true })
        {
            var o = Custom(SupernovaType.IcBL, grb);
            var s = Model.At(o, Curves.PeakTimeS(o));
            Assert.Equal(grb ? 19200 : 16700, s.PhotosphereVelocityKmS, 6);
            Assert.True(OxygenTroughWidthKmS(s) > 15000, $"width {OxygenTroughWidthKmS(s)}");
        }
        // A plain Ic (11,000 km/s) is narrower.
        var ic = Custom(SupernovaType.Ic);
        Assert.True(OxygenTroughWidthKmS(Model.At(ic, Curves.PeakTimeS(ic))) < 15000);
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
