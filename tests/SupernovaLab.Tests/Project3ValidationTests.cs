using SupernovaLab.App.Services;
using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Compact;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Text;
using SupernovaLab.Core.Units;
using SupernovaLab.Rendering;
using Xunit.Abstractions;
using static SupernovaLab.Tests.CompactResearch;

namespace SupernovaLab.Tests;

/// <summary>
/// Project 3's success criteria (spec §1, criteria 1–4 and 6; criterion 5 is the in-app benchmark, docs/perf-2026-11.md), one test
/// group per criterion, re-asserted over every preset and every outcome type. The detailed formula and value checks live in the task
/// tests (<see cref="BlackHoleOpticsTests"/>, <see cref="NeutronStarModelTests"/>, <see cref="ViewsAgreeTests"/>,
/// <see cref="CoreCollapseHistoryTests"/>, <see cref="PulseTraceTests"/>, <see cref="MagnetarFlaresTests"/>); these call the same model
/// functions and helpers and assert the criterion end to end. Flash safety's details are in <see cref="FlashSafetyTests"/>.
/// </summary>
public class Project3ValidationTests(ITestOutputHelper output)
{
    private const double Year = PhysicalConstants.Year;

    private static Outcome ForPreset(Preset p) => TestOutcomes.Engine.Compute(p.Inputs, p.Pins);

    private static IEnumerable<double> LogTimes(double from, double to, int n)
    {
        for (int i = 0; i < n; i++) yield return from * Math.Pow(to / from, i / (double)(n - 1));
    }

    /// <summary>
    /// Every outcome the criteria are checked over: Task 13's list (every hand-built type, every fate example, the collapsar, every
    /// preset) plus the lab's magnetars, its fallback black hole and an electron-capture star (the outcomes the in-app sweep also plays).
    /// </summary>
    public static IEnumerable<object?[]> AllPresetsAndOutcomeTypes()
    {
        foreach (var row in TourCloseUpTests.AllPresetsAndTypes()) yield return new[] { row[0], row[1], row[2] };
        var lab = LabInputs.Default;
        yield return new object?[] { "lab magnetar (fast)", TestOutcomes.Engine.Compute(lab with { Field = CoreField.Strong, Rotation = Rotation.Fast }), ObserverSetup.Default };
        yield return new object?[] { "lab magnetar (slow)", TestOutcomes.Engine.Compute(lab with { Field = CoreField.Strong, Rotation = Rotation.Slow }), ObserverSetup.Default };
        yield return new object?[] { "lab fallback black hole", TestOutcomes.Engine.Compute(lab with { InitialMass = 27.4 }), ObserverSetup.Default };
        yield return new object?[] { "lab 9 Msun", TestOutcomes.Engine.Compute(lab with { InitialMass = 9 }), ObserverSetup.Default };
        yield return new object?[] { "hand-built magnetar", TestOutcomes.Custom(SupernovaType.IIP, 1e51, 10, 0.07, 3e13,
            TestOutcomes.Pulsar(2.5, 1e15, RemnantKind.Magnetar)), ObserverSetup.Default };
    }

    private static IEnumerable<(string Name, Outcome Outcome, ObserverSetup Observer)> Cases()
        => AllPresetsAndOutcomeTypes().Select(r => ((string)r[0]!, (Outcome)r[1]!, (ObserverSetup)r[2]!));

    /// <summary>The scrubber's whole range in source time (1 ms to 1,000 yr, as the sweep), with the first second sampled finely too.</summary>
    private static IEnumerable<double> Timeline() => LogTimes(1e-3, 3.156e10, 40).Concat(LogTimes(1.1e-3, 3, 25));

    private static bool HasCompactRemnant(Outcome o) => o.Remnant.Kind is RemnantKind.NeutronStar or RemnantKind.Magnetar or RemnantKind.BlackHole;

    /// <summary>Every number the state draws or shows is finite, and every readout is a Quantity with a confidence and a source.</summary>
    private static void AssertAllFinite(CloseUpState s, string what)
    {
        Assert.False(string.IsNullOrWhiteSpace(s.Title), what);
        Assert.True(double.IsFinite(s.FrameRadiusKm) && s.FrameRadiusKm > 0, $"{what}: framing {s.FrameRadiusKm}");
        Assert.All(s.Notes, n => Assert.False(string.IsNullOrWhiteSpace(n), what));
        foreach (var r in s.Readouts)
        {
            Assert.True(double.IsFinite(r.Value.Value), $"{what}: {r.Label} = {r.Value.Value}");
            Assert.True(r.Second is null || double.IsFinite(r.Second.Value), $"{what}: {r.Label}'s second number");
            Assert.False(string.IsNullOrWhiteSpace(r.ValueText), $"{what}: {r.Label}");
        }
        if (s.NeutronStar is { } ns)
        {
            Assert.True(double.IsFinite(ns.PeriodS) && ns.PeriodS > 0, what);
            Assert.True(double.IsFinite(s.DrawnPeriodS) && s.DrawnPeriodS >= Magnetosphere.MinDrawnPeriodS, what);
            Assert.True(double.IsFinite(s.LightCylinderKm) && s.LightCylinderKm > s.SurfaceRadiusKm, what);
            Assert.True(double.IsFinite(s.BeamTiltRad) && double.IsFinite(s.BeamHalfAngleRad), what);
            Assert.True(double.IsFinite(s.EarthDirection.Length) && Math.Abs(s.EarthDirection.Length - 1) < 1e-9, what);
            Assert.True(double.IsFinite(ns.OffsetCm.Length), what);
        }
        if (s.BlackHole is { } bh)
        {
            Assert.InRange(bh.DrawnSpin, 0, 0.998);
            Assert.True(double.IsFinite(bh.DrawnMassMsun) && bh.DrawnMassMsun > 0, what);
            Assert.True(double.IsFinite(s.GravitationalRadiusKm), what);
            Assert.True(double.IsFinite(bh.OffsetCm.Length), what);
        }
        if (s.Core is not null) Assert.True(double.IsFinite(s.CoreSphereRadiusKm) && s.CoreSphereRadiusKm > 0, $"{what}: core sphere");
    }

    // ------------------------------------------------------------------------------------------------ criterion 1

    [Theory, MemberData(nameof(AllPresetsAndOutcomeTypes))]
    public void Criterion1_CloseUpPlaysWholeTimeline(string what, Outcome o, ObserverSetup obs)
    {
        var scene = new CloseUpScene(o, obs, 1);
        var eras = new HashSet<CloseUpEra>();
        foreach (double t in Timeline())
        {
            var st = scene.At(t);
            AssertAllFinite(st, $"{what} at {t:G4} s");
            eras.Add(st.Era);
            // Outcomes with no compact remnant say why (spec §2.3).
            if (st.Era == CloseUpEra.None)
            {
                Assert.False(string.IsNullOrWhiteSpace(st.NoRemnantMessage), what);
                Assert.False(st.DrawsScene);
            }
            else Assert.Null(st.NoRemnantMessage);
        }
        if (HasCompactRemnant(o))
        {
            Assert.DoesNotContain(CloseUpEra.None, eras);
            var last = scene.At(3.156e10).Era;
            Assert.Equal(o.Remnant.Kind == RemnantKind.BlackHole ? CloseUpEra.BlackHole : CloseUpEra.NeutronStar, last);
        }
        else Assert.Equal(new[] { CloseUpEra.None }, eras);
    }

    [Fact]
    public void Criterion1_EveryEraIsReached()
    {
        var eras = new HashSet<CloseUpEra>();
        foreach (var (_, o, obs) in Cases())
        {
            var scene = new CloseUpScene(o, obs, 1);
            foreach (double t in Timeline().Append(5e-4)) eras.Add(scene.At(t).Era);
        }
        Assert.Equal(Enum.GetValues<CloseUpEra>().ToHashSet(), eras);
    }

    [Fact]
    public void EveryReadout_HasConfidenceAndSource()   // over all eras
    {
        var eras = new HashSet<CloseUpEra>();
        int readouts = 0;
        foreach (var (what, o, obs) in Cases())
        {
            var scene = new CloseUpScene(o, obs, 1);
            foreach (double t in Timeline().Append(5e-4))
            {
                var st = scene.At(t);
                eras.Add(st.Era);
                foreach (var r in st.Readouts.Concat(st.Cutaway?.Readouts ?? Array.Empty<Readout>()))
                {
                    readouts++;
                    Assert.True(Enum.IsDefined(r.Value.Confidence), $"{what}: {r.Label}");
                    Assert.False(string.IsNullOrWhiteSpace(r.Value.Source.Citation), $"{what} at {t:G4} s: {r.Label} has no source");
                    Assert.False(string.IsNullOrWhiteSpace(r.Value.Source.ResearchRef), $"{what} at {t:G4} s: {r.Label} has no research reference");
                    // The column's row carries the same confidence and names the source in its badge's tooltip.
                    var row = ReadoutRow.From(r);
                    Assert.Equal(r.Value.Confidence, row.Confidence);
                    Assert.Contains(r.Value.Source.Citation, row.BadgeToolTip);
                }
            }
        }
        output.WriteLine($"{readouts:N0} readouts checked over the eras {string.Join(", ", eras)}");
        Assert.Equal(Enum.GetValues<CloseUpEra>().Length - 1, eras.Count(e => e != CloseUpEra.None));
    }

    // ------------------------------------------------------------------------------------------------ criterion 2

    /// <summary>
    /// Criterion 2: every exact result drawn matches its formula. The formulas against the exact numbers (√27, 3, Bardeen's outline, the
    /// ISCO, the ergosphere, the visible fraction), the tracer against them (Task 3's helpers), and the Close-up's readouts and disk
    /// against the formulas for every black-hole outcome and time.
    /// </summary>
    [Fact]
    public void Criterion2_ExactResults()
    {
        // Non-spinning: shadow √27 GM/c² (0.5%), photon sphere 3 GM/c²; the tracer's edge within 0.5%.
        Assert.Equal(C7.ShadowRadius, BlackHoleOptics.SchwarzschildShadowRadius, 5);
        Assert.Equal(C7.PhotonSphere, 1.5 * BlackHoleOptics.HorizonRadius(0), 12);
        double edge = BlackHoleOpticsTests.BisectCaptureEdge(spin: 0, cameraR: 1000);
        Assert.InRange(edge, C7.ShadowRadius * (1 - C7.ShadowFractionalTolerance), C7.ShadowRadius * (1 + C7.ShadowFractionalTolerance));

        // Spinning: Bardeen's outline, traced, within 1% of the shadow radius at a = 0.9 side-on and from 17° (criterion 2's cases).
        foreach (double inc in new[] { 90.0, 17.0 })
            foreach (var (alpha, beta) in BlackHoleOptics.KerrShadowOutline(C7.OutlineSpin, inc * Math.PI / 180, 24))
            {
                double traced = BlackHoleOpticsTests.EdgeAlongRay(C7.OutlineSpin, inc, alpha, beta);
                Assert.True(Math.Abs(traced - Math.Sqrt(alpha * alpha + beta * beta)) < C7.OutlineTolerance, $"i = {inc}°: ({alpha:F3}, {beta:F3})");
            }

        // ISCO (Bardeen, Press & Teukolsky 1972) and the ergosphere's outer edge r_E(θ) = 1 + √(1 − a² cos² θ).
        foreach (double a in new[] { 0, 0.1, 0.5, 0.9, 0.998 })
        {
            double z1 = 1 + Math.Cbrt(1 - a * a) * (Math.Cbrt(1 + a) + Math.Cbrt(1 - a));
            double z2 = Math.Sqrt(3 * a * a + z1 * z1);
            Assert.Equal(3 + z2 - Math.Sqrt((3 - z1) * (3 + z1 + 2 * z2)), BlackHoleOptics.IscoRadius(a), 9);
            foreach (double th in new[] { 0, 0.4, Math.PI / 2 })
                Assert.Equal(1 + Math.Sqrt(1 - a * a * Math.Cos(th) * Math.Cos(th)), BlackHoleOptics.ErgosphereRadius(a, th), 12);
        }

        // The Close-up's readouts and disk against the formulas, for every black-hole outcome at every time.
        int holes = 0;
        foreach (var (what, o, obs) in Cases().Where(c => c.Outcome.Remnant.Kind == RemnantKind.BlackHole))
        {
            var scene = new CloseUpScene(o, obs, 1);
            foreach (double t in Timeline())
            {
                var st = scene.At(t);
                if (st.BlackHole is not { } bh) continue;
                double rgKm = BlackHoleOptics.GravitationalRadiusCm(bh.MassMsun) / 1e5;
                var byLabel = st.Readouts.ToDictionary(r => r.Label);
                Assert.Equal(2 * BlackHoleOptics.HorizonRadius(bh.Spin) * rgKm, byLabel["Horizon"].Value.Value, 9);
                Assert.Equal(BlackHoleOptics.ApparentShadowDiameterRad(bh.MassMsun, 1e5, bh.Spin), byLabel["Shadow"].Value.Value, 9);
                if (bh.Spin == 0) Assert.Equal(2 * Math.Sqrt(27) * rgKm, byLabel["Shadow"].Value.Value, 6);
                if (st.HasDisk) Assert.Equal(BlackHoleOptics.IscoRadius(bh.DrawnSpin), st.DiskInnerGm, 9);
                holes++;
            }
        }
        Assert.True(holes > 100, $"{holes} black-hole states");

        // The neutron star's visible surface fraction: the tracer against the exact Schwarzschild quadrature, for every neutron star.
        var stars = Cases().Select(c => c.Outcome.Remnant)
            .Where(r => r.Kind is RemnantKind.NeutronStar or RemnantKind.Magnetar && r.Mass is not null && r.RadiusKm is not null)
            .Select(r => (M: r.Mass!.Value, R: r.RadiusKm!.Value)).Distinct().ToList();
        Assert.True(stars.Count >= 2);
        foreach (var (m, r) in stars)
            Assert.Equal(BlackHoleOptics.VisibleSurfaceFraction(m, r), BlackHoleOpticsTests.TracerVisibleFraction(m, r), 2);
        Assert.InRange(BlackHoleOptics.VisibleSurfaceFraction(1.4, 12), C7.VisibleFraction14Low, C7.VisibleFraction14High);
    }

    // ------------------------------------------------------------------------------------------------ criterion 3

    [Fact]
    public void Criterion3_ObservedChecks()
    {
        // The Crab: its period at its age (33.4 ms within Project 1's 5%) and its light cylinder (≈ 1,600 km), as the Close-up shows them.
        var crab = PresetCatalog.Get("sn1054");
        var crabState = new CloseUpScene(ForPreset(crab), crab.Observer, 1).At(2.9568e10);
        double period = crabState.Readouts.Single(r => r.Label == "Spin").Value.Value;
        Assert.InRange(period, 0.0333924 * 0.95, 0.0333924 * 1.05);
        Assert.Equal(Magnetosphere.LightCylinderKm(period), crabState.LightCylinderKm, 9);
        Assert.InRange(crabState.LightCylinderKm, 1500, 1700);
        Assert.Equal(C4.CrabLightCylinderKm, Magnetosphere.LightCylinderKm(0.0334), 0.5);

        // Cas A: the lab's far-away temperature about 1.8 MK against the observed 1.4–1.5 MK, disclosed (not tuned) in the readout's tooltip.
        var casa = PresetCatalog.Get("casa");
        var casaState = new CloseUpScene(ForPreset(casa), casa.Observer, 1).At(340 * Year);
        var surface = casaState.Readouts.Single(r => r.Label == "Surface");
        Assert.InRange(surface.Value.Value, 1.75e6, 1.9e6);
        Assert.Equal(NeutronStarModel.CasAComparison(ForPreset(casa), 340 * Year), surface.ToolTip);
        Assert.Contains("1.4–1.5", surface.ToolTip);
        Assert.Contains("warm", surface.ToolTip);
        Assert.Contains("#54", surface.ToolTip);

        // The EHT: the shadow law against M87*'s ring (stellar-dynamics mass) and Sgr A*'s inferred shadow (VLTI δ), scaled by mass.
        double m87 = BlackHoleOpticsTests.MicroArcsec(BlackHoleOptics.ApparentShadowDiameterRad(C9.M87StellarDynamicsMassMsun,
            C9.M87DistanceMpc * PhysicalConstants.Mpc)) * C9.RingToShadow;
        Assert.InRange(m87, C9.M87RingMicroArcsec - C9.TolM87, C9.M87RingMicroArcsec + C9.TolM87);
        double sgrA = BlackHoleOpticsTests.MicroArcsec(BlackHoleOptics.ApparentShadowDiameterRad(C9.SgrAMassMsun, C9.SgrADistanceKpc * PhysicalConstants.Kpc));
        double sch = 2 * Math.Sqrt(27) * C9.SgrAThetaGVltiMicroArcsec;
        Assert.InRange(sgrA, sch * (1 + C9.SgrADeltaVlti - C9.SgrADeltaVltiSigma), sch * (1 + C9.SgrADeltaVlti + C9.SgrADeltaVltiSigma));

        // SN 1987A's neutrinos: the energy (the neutron star's binding energy, 2–3 × 10⁵³ erg, R46) and the burst (Kamiokande-II's 6–20
        // expected over 20 s, 11 seen, with most of them inside the observed 12.4 s), as the neutrino panel tests them.
        var sn87 = PresetCatalog.Get("sn1987a");
        var o87 = ForPreset(sn87);
        double binding = NeutrinoModel.BindingEnergyErg(o87.Remnant.Mass!.Value, o87.Remnant.RadiusKm!.Value);
        Assert.InRange(binding, 2e53, 3.1e53);
        double kam = NeutrinoModel.KamiokandeIIEvents(o87, sn87.Observer);
        Assert.InRange(kam, 6, 20);
        double within = NeutrinoModel.WaterCherenkovIbdEvents(o87, sn87.Observer, NeutrinoModel.KamiokandeIIKilotons, NeutrinoModel.KamiokandeIIEfficiency, 12.439);
        output.WriteLine($"SN 1987A: binding {binding:E3} erg; Kamiokande-II {kam:0.0} over 20 s, {within:0.0} within 12.4 s ({within / kam:P0})");
        Assert.True(within / kam > 0.8, $"{within / kam:P0} of the burst inside the observed 12.4 s");
    }

    // ------------------------------------------------------------------------------------------------ criterion 4

    [Fact]
    public void Criterion4_ViewsAgree()
    {
        int pulsars = 0, holes = 0, kicks = 0;
        foreach (var (what, o, obs) in Cases())
        {
            var close = new CloseUpScene(o, obs, 1);
            var main = new ExplosionScene(o, obs.TimeDilation, obs, 1);
            foreach (double t in LogTimes(1, 3.15e10, 30))
            {
                var m = main.At(t);
                var c = close.At(t);
                // The kick: the neutron star's (or black hole's) position is the same in both windows.
                var offset = KickDrift.OffsetCm(o, obs, 1, t);
                Assert.Equal(offset, m.RemnantOffsetCm);
                if (c.NeutronStar is { } ns) { Assert.Equal(offset, ns.OffsetCm); kicks++; }
                if (c.BlackHole is { } bh && bh.SourceTimeS == t) { Assert.Equal(offset, bh.OffsetCm); kicks++; }
                // Pulsar beams: one tilt, one half-width, one period (so one drawn rate and, through the shared BeamClock, one phase).
                if (m.Marker == SceneMarker.Pulsar && c.NeutronStar is { } star)
                {
                    Assert.Equal(m.BeamTiltRad, c.BeamTiltRad);
                    Assert.Equal(m.BeamHalfAngleRad, c.BeamHalfAngleRad);
                    Assert.Equal(m.PulsarPeriodS, star.PeriodS);
                    Assert.Equal(Magnetosphere.DrawnPeriodS(m.PulsarPeriodS), c.DrawnPeriodS);
                    pulsars++;
                }
            }
            // A black hole forms at exactly the time the neutrino panel cuts off (a failed supernova, a fallback, a collapsar).
            if (CoreCollapseHistory.BlackHoleFormsS(o) is var tBh && double.IsFinite(tBh))
            {
                Assert.Equal(o.Remnant.FormationTimeS!.Value, tBh);
                var nu = new NeutrinoModel();
                double before = tBh * (1 - 1e-6), after = tBh * (1 + 1e-6);
                Assert.True(nu.Sample(o, obs, before, "lum_anue") > 0, $"{what}: neutrinos just before t_BH");
                Assert.Equal(0, nu.Sample(o, obs, after, "lum_anue"));
                Assert.NotEqual(CloseUpEra.BlackHoleForming, close.At(before).Era);
                Assert.NotEqual(CloseUpEra.BlackHole, close.At(before).Era);
                Assert.Contains(close.At(after).Era, new[] { CloseUpEra.BlackHoleForming, CloseUpEra.BlackHoleSettling, CloseUpEra.BlackHole });
                holes++;
            }
        }
        output.WriteLine($"{pulsars} pulsar states, {kicks} positions, {holes} black-hole formations checked");
        Assert.True(pulsars > 50 && kicks > 100 && holes >= 4, $"{pulsars}, {kicks}, {holes}");

        // One phase: both scene controllers read the one BeamClock (ViewsAgreeTests.BothViews_SamePhaseFromBeamClock, run again here).
        new ViewsAgreeTests().BothViews_SamePhaseFromBeamClock();
    }

    // ------------------------------------------------------------------------------------------------ criterion 6

    /// <summary>
    /// Criterion 6, the beams: over every pulsar or magnetar outcome and time, two beams sweep past at most once a second each way, so a
    /// beam's brightness on screen changes at most 2 / drawn period ≤ 1 Hz, under the 3 Hz limit. Flares, the limiter, the pulse trace
    /// and the first second's animation are in <see cref="FlashSafetyTests"/>.
    /// </summary>
    [Fact]
    public void Criterion6_NoPeriodicBrightnessOver3Hz()
    {
        int cases = 0;
        foreach (var (what, o, obs) in Cases())
        {
            if (NeutronStarModel.At(o, obs, 1, 0) is null) continue;
            foreach (double t in LogTimes(1, 3.156e10, 60))
            {
                double period = NeutronStarModel.At(o, obs, 1, t)!.PeriodS;
                Assert.True(2 / Magnetosphere.DrawnPeriodS(period) <= 1.0, $"{what} at {t:G3} s: period {period}");
                Assert.True(2 / Magnetosphere.DrawnPeriodS(period) <= 3.0);
                cases++;
            }
        }
        Assert.True(cases > 500, $"{cases}");
    }

    // ------------------------------------------------------------------------------------------------ the glossary

    /// <summary>The spec's new terms each have a plain-language tooltip (global constraint "Glossary").</summary>
    [Theory]
    [InlineData(nameof(Glossary.NeutronDrip))]
    [InlineData(nameof(Glossary.NuclearPasta))]
    [InlineData(nameof(Glossary.LightCylinder))]
    [InlineData(nameof(Glossary.Ergosphere))]
    [InlineData(nameof(Glossary.PhotonRing))]
    [InlineData(nameof(Glossary.BlackHoleShadow))]
    [InlineData(nameof(Glossary.Isco))]
    [InlineData(nameof(Glossary.Sasi))]
    [InlineData(nameof(Glossary.GainRegion))]
    [InlineData(nameof(Glossary.ProtoNeutronStar))]
    [InlineData(nameof(Glossary.GiantFlare))]
    [InlineData(nameof(Glossary.FrameDragging))]
    [InlineData(nameof(Glossary.EventHorizon))]
    [InlineData(nameof(Glossary.StalledShock))]
    public void EveryNewGlossaryKey_Exists(string key)
    {
        Assert.True(Glossary.All.TryGetValue(key, out var text), key);
        Assert.True(text!.Length > 60, key);
    }

    /// <summary>
    /// Task 14: the Close-up's readout labels and notes that name a term show its text on hover: the label's tooltip starts with the
    /// term, and every note that names one gets it from the converter's lookup.
    /// </summary>
    [Fact]
    public void CloseUpTerms_ReachTheirTooltips()
    {
        foreach (var (label, key) in Glossary.CloseUpReadoutTerms) Assert.True(Glossary.All.ContainsKey(key), label);
        foreach (var (phrase, key) in Glossary.CloseUpNoteTerms) Assert.True(Glossary.All.ContainsKey(key), phrase);

        var hole = PresetCatalog.Get("sn1998bw");
        var holeState = new CloseUpScene(ForPreset(hole), hole.Observer, 1).At(8);
        var horizon = ReadoutRow.From(holeState.Readouts.Single(r => r.Label == "Horizon"));
        Assert.StartsWith(Glossary.EventHorizon, horizon.LabelToolTip);
        Assert.Contains("twice the horizon's radius", horizon.LabelToolTip);   // the readout's own note follows
        Assert.StartsWith(Glossary.BlackHoleShadow, ReadoutRow.From(holeState.Readouts.Single(r => r.Label == "Shadow")).LabelToolTip);
        Assert.Contains(holeState.Notes, n => Glossary.CloseUpTermFor(n) == Glossary.Isco);
        Assert.Equal(Glossary.Ergosphere, Glossary.CloseUpTermFor("the ergosphere outlined"));
        Assert.Equal(Glossary.Sasi, Glossary.CloseUpTermFor("Shock distortion (convection and SASI): about 10%"));
        Assert.Null(Glossary.CloseUpTermFor("isco in lower case is not the abbreviation"));
        Assert.Null(Glossary.CloseUpTermFor(null));

        var core = new CloseUpScene(ForPreset(PresetCatalog.Get("sn1987a")), ObserverSetup.Default, 1).At(0.1);
        var gain = core.Readouts.SingleOrDefault(r => r.Label == "Gain radius");
        if (gain is not null) Assert.StartsWith(Glossary.GainRegion, ReadoutRow.LabelTip(gain));
        Assert.Contains(core.Notes, n => Glossary.CloseUpTermFor(n) is not null);
        // A label with no term keeps the readout's own tooltip unchanged.
        var age = new Readout("Age", TestOutcomes.Q(1, "s"), "Time since core bounce");
        Assert.Equal("Time since core bounce", ReadoutRow.LabelTip(age));
    }
}
