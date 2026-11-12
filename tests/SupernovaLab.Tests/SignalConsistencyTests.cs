using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Tests;

/// <summary>
/// Ruling R53 (final review I4–I6, M7): the visible-light reason between breakout and the cooling formulas, Δm15(B) from
/// the drawn curve, the spectrum's velocities scaled by the explosion's energy per mass, and the pulsar flux on D_L.
/// </summary>
public class SignalConsistencyTests
{
    private const double Day = 86400;
    private static readonly OutcomeEngine Engine = TestOutcomes.Engine;
    private static readonly LightCurveModel Light = new();
    private static readonly ShockBreakoutModel Breakout = new();
    private static readonly Source Published = new("Test publication", "tests");

    private static (Outcome Outcome, ObserverSetup Observer) Preset(string id)
    {
        var p = PresetCatalog.Get(id);
        return (Engine.Compute(p.Inputs, p.Pins), p.Observer);
    }

    // ---------------------------------------------------------------- I4: between breakout and cooling start

    [Fact]
    public void Betelgeuse_AtThreeDays_VisibleLightIsNotModeled_AndGivesTheBreakoutTime()
    {
        // The final review's table: breakout at 2.36 d, the cooling formulas (and the light curve) from 4.75 d.
        var (o, obs) = Preset("betelgeuse");
        double breakout = Breakout.BreakoutTimeS(o);
        Assert.InRange(breakout / Day, 2.3, 2.4);
        Assert.InRange(Light.ValidRange(o).StartS / Day, 4.7, 4.8);

        var d = Light.Detect(o, obs, 3 * Day, Detectors.Get("eye"));
        Assert.Equal(DetectionStatus.NotModeled, d.Status);
        Assert.Contains(UnitFormat.Time(breakout), d.Reason);              // "2.36 days"
        Assert.Contains(UnitFormat.Time(Light.ValidRange(o).StartS), d.Reason);
        Assert.Contains("X-ray & UV", d.Reason);
        Assert.DoesNotContain("Not yet", d.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Betelgeuse_BeforeBreakout_VisibleLightIsNotYet_AtTheBreakoutTime()
    {
        var (o, obs) = Preset("betelgeuse");
        var d = Light.Detect(o, obs, 1 * Day, Detectors.Get("eye"));
        Assert.Equal(DetectionStatus.NotYet, d.Status);
        Assert.Contains(UnitFormat.Time(Breakout.BreakoutTimeS(o)), d.Reason);
    }

    // ---------------------------------------------------------------- I5: Δm15(B) from the drawn curve

    [Fact]
    public void DeclineRate_IsMeasuredOnTheDrawnCurve_ForTypeIaOnly()
    {
        var ia = TestOutcomes.TypeIa();
        double dm15 = Light.DeclineRateDm15B(ia);
        // Physically sensible for a B-band decline (Phillips 1999's range is 0.85–1.7; observed SNe Ia span ~0.7–2.0).
        Assert.InRange(dm15, 0.7, 2.0);
        // The B curve falls after its maximum: Δm15 is the B magnitude 15 days after B maximum less B at maximum.
        Assert.True(dm15 > 0);
        Assert.True(double.IsNaN(Light.DeclineRateDm15B(TestOutcomes.Star(16))));
    }

    [Fact]
    public void Sn2011fe_DeclineRateMiss_IsDisclosed()
    {
        // Pereira et al. 2013 (A&A 554, A27; arXiv:1302.1292), Table 3: Δm15(B) = 1.103 ± 0.035 (SALT2 fit). R47b: if the
        // lab's drawn value misses by more than the measurement's 1σ, the preset's summary says so.
        var (o, _) = Preset("sn2011fe");
        double dm15 = Light.DeclineRateDm15B(o);
        bool miss = Math.Abs(dm15 - 1.103) > 0.035;
        if (miss)
        {
            string summary = PresetCatalog.Get("sn2011fe").Summary;
            Assert.Contains("1.10", summary);
            Assert.Contains(dm15.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), summary);
        }
    }

    // ---------------------------------------------------------------- I6: spectrum velocities

    [Fact]
    public void Sn1054_HalphaReferenceVelocity_DropsBelowTheTypeReference()
    {
        // Ruling R53: v_ref = 7,300 km/s × √((E/M_ej) / (1e51 erg / 10 M☉)), clamped to 1,500–9,600 km/s (signals §10.2).
        // SN 1054 (electron capture): E = 1e50 erg, M_ej = M_f − 1.36 M☉ (the engine's value, read below). With
        // M_ej = 7.386 M☉: E/M = 1.3539e49 erg/M☉, ratio 0.13539, √ = 0.36795, v_ref = 7,300 × 0.36795 = 2,686 km/s.
        var (o, _) = Preset("sn1054");
        Assert.Equal(1e50, o.ExplosionEnergyErg!.Value);
        double m = o.EjectaMass!.Value;
        Assert.InRange(m, 7.3, 7.5);
        double expected = 7300 * Math.Sqrt(1e50 / m / 1e50);
        Assert.Equal(expected, SpectrumModel.ReferenceVelocityKmS(o), 1e-6);
        Assert.InRange(SpectrumModel.ReferenceVelocityKmS(o), 2600, 2760);
        // The drawn spectrum's Hα follows it: at day 60 (after the V peak and day 15), v = v_ref (60 / 50)^−0.46.
        var spectrum = new SpectrumModel().At(o, 60 * Day);
        Assert.True(spectrum.PhotosphereVelocityKmS < 7300 * Math.Pow(60.0 / 50, -0.46));
    }

    [Fact]
    public void SpectrumVelocity_ScalesWithEnergyPerMass_AndStaysInsideTheObservedRanges()
    {
        var x = LabInputs.Default;
        // A weak, heavy II-P: 1e50 erg in 30 M☉: √(3.333e48 / 1e50) = 0.18257, 7,300 × 0.18257 = 1,333 km/s, clamped up
        // to the observed 1,500 km/s.
        var weak = Engine.Compute(x, new PresetPins(1e50, 30, null, null, Published));
        Assert.Equal(1500, SpectrumModel.ReferenceVelocityKmS(weak), 1e-9);
        // A hypernova-like II-P: 1e52 erg in 5 M☉: √(2e51 / 1e50) = 4.4721, 32,650 km/s, clamped down to 9,600 km/s.
        var strong = Engine.Compute(x, new PresetPins(1e52, 5, null, null, Published));
        Assert.Equal(9600, SpectrumModel.ReferenceVelocityKmS(strong), 1e-9);
        // The default near-Chandrasekhar Ia (1.35e51 erg, 1.38 M☉) against the Ia reference 1.3e51 / 1.4 M☉:
        // (9.7826e50 / 9.2857e50) = 1.053511, √ = 1.026407, 10,600 × 1.026407 = 10,879.9 km/s, inside 10,200–12,000.
        Assert.Equal(10879.9, SpectrumModel.ReferenceVelocityKmS(TestOutcomes.TypeIa()), 0.1);
        // A Type Ib at the Ib/c reference (1e51 erg, 3 M☉) gets the type's own 9,100 km/s.
        var ib = Engine.Compute(x with { InitialMass = 15, Stripping = Stripping.Full }, new PresetPins(1e51, 3, null, null, Published));
        Assert.Equal(SupernovaType.Ib, ib.Type);
        Assert.Equal(9100, SpectrumModel.ReferenceVelocityKmS(ib), 1e-6);
    }

    [Fact]
    public void SpectrumSources_SayTheVelocitiesAreScaledFromTypeReferences()
    {
        string text = string.Join(" ", new SpectrumModel().Sources.Select(s => s.Citation));
        Assert.Contains("1e51 erg / 10 M☉", text);
        Assert.Contains("1e51 erg / 3 M☉", text);
        Assert.Contains("1.3e51 erg / 1.4 M☉", text);
        Assert.Contains(SpectrumModel.VelocityCaption, text);
    }

    // ---------------------------------------------------------------- M8: one ejecta velocity scale

    [Fact]
    public void EjectaVelocity_IsDefinedOnce_AndSharedByTheViewAndTheLines()
    {
        // Signals §0.4: v = √(10E / 3M); 1e51 erg and 3 M☉: 10e51 / (9 × 1.989e33) = 5.5863e17, √ = 7.4741e8 cm/s ("7.5e8").
        Assert.Equal(7.4741e8, ArnettDiffusion.EjectaVelocityCmS(1e51, 3), 0.0001e8);
        var o = TestOutcomes.Star(16);
        double v = ArnettDiffusion.EjectaVelocityCmS(o.ExplosionEnergyErg!.Value, o.EjectaMass!.Value);
        Assert.Equal(v, EjectaKinematics.OuterVelocityCmS(o));
        Assert.Equal(v / 1e5, GammaLineModel.LineFwhmKmS(o));
    }

    // ---------------------------------------------------------------- M7: pulsar flux on D_L with (1 + z)

    [Fact]
    public void PulsarFlux_UsesTheLuminosityDistance_AndOnePlusZ()
    {
        // S = (1 + z) L_1400 / D_L², the flat-spectrum form; at 2 kpc (z = 0) the Crab's 56 mJy kpc² gives 14 mJy.
        var (crab, near) = Preset("sn1054");
        var pulsar = new PulsarModel();
        double t = 900 * PhysicalConstants.Year;
        Assert.Equal(14, pulsar.Sample(crab, near, t, "fluxMjy"), 1e-9);
        var far = new ObserverSetup(1e9, 0, true); // 1 Gpc: z > 0
        Assert.True(far.Redshift > 0);
        double dKpc = far.LuminosityDistanceCm / PhysicalConstants.Kpc;
        Assert.Equal(56 * far.TimeDilation / (dKpc * dKpc), pulsar.Sample(crab, far, t, "fluxMjy"), 1e-15);
    }
}
