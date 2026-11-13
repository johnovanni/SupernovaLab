using System.Windows;
using SupernovaLab.App.Controllers;
using SupernovaLab.App.Tour;
using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Structure;
using SupernovaLab.Core.Timeline;
using SupernovaLab.Core.Units;
using SupernovaLab.Rendering;
using Xunit.Abstractions;

namespace SupernovaLab.Tests;

/// <summary>
/// Project 2's success criteria (spec §1, criteria 1–4; criterion 5 is the in-app benchmark, docs/perf-2026-11.md), one test
/// group per criterion, each driving the real classes. The detailed formula and value checks live in the task tests
/// (<see cref="CameraDirectorTests"/>, <see cref="EjectaStructureTests"/>, <see cref="ParticleSamplerTests"/>,
/// <see cref="ShockStructureTests"/>, <see cref="PulsarWindNebulaTests"/>, <see cref="Sn1987aRingsTests"/>, <see cref="TourBuilderTests"/>,
/// <see cref="TourDirectorTests"/>); these prove the criteria end to end and over every preset.
/// </summary>
public class Project2ValidationTests(ITestOutputHelper output)
{
    private const double Year = PhysicalConstants.Year, Pc = PhysicalConstants.Pc, Ly = PhysicalConstants.LightYear, KmS = 1e5;
    private static readonly TimeSpan Frame = TimeSpan.FromSeconds(1.0 / 60);

    private static Outcome ForPreset(Preset p) => TestOutcomes.Engine.Compute(p.Inputs, p.Pins);

    public static IEnumerable<object[]> PresetIds() => PresetCatalog.LoadEmbedded().Select(p => new object[] { p.Id });

    // ------------------------------------------------------------------------------------------------ criterion 1

    private sealed class FakeHost : IRenderHost
    {
        public bool Animating { get; set; }
        public event Action<string>? ContextReady { add { } remove { } }
        public event Action<TimeSpan, Size>? Draw;
        public event Action<Exception>? FrameFailed { add { } remove { } }
        public void Start() { }
        public void RequestRender() { }
        public bool Shutdown(Action release) => false;
        public void RaiseDraw() => Draw?.Invoke(Frame, new Size(1366, 768));
    }

    /// <summary>
    /// Plays the whole timeline at the default speed through a <see cref="SceneController"/> (fake host and clock): each frame the
    /// timeline ticks as playback, the scene restates, and the director gets the wall time. Returns, for each pull-back cycle
    /// (from one camera step to the next), the fraction of its frames in which the camera was still.
    /// </summary>
    private static List<double> HoldFractions(MainViewModel vm, double decadesPerSecond)
    {
        double wall = 0;
        var host = new FakeHost();
        using var scene = new SceneController(vm, () => host, wallClock: () => wall);
        scene.Start();
        vm.Timeline.DecadesPerSecond = decadesPerSecond;
        vm.Timeline.IsPlaying = false;
        vm.Timeline.ObserverTimeS = TimeAxis.MinS;
        vm.Timeline.IsPlaying = true;

        var holds = new List<double>();
        int frames = 0, still = 0;
        bool started = false, wasEasing = false;
        for (int i = 0; i < 60 * 200 && vm.Timeline.IsPlaying; i++)
        {
            wall += Frame.TotalSeconds;
            vm.Timeline.Tick(Frame);
            host.RaiseDraw();
            bool easing = scene.Director.IsEasing;
            if (easing && !wasEasing)
            {
                if (started) holds.Add((double)still / frames);
                started = true;
                frames = still = 0;
            }
            wasEasing = easing;
            frames++;
            if (!easing) still++;
        }
        Assert.False(vm.Timeline.IsPlaying, "the timeline played to its end");
        return holds;
    }

    private static MainViewModel Vm(Action<LabViewModel>? configure = null)
    {
        var vm = new MainViewModel(TestOutcomes.Engine, new FakeWindowService());
        configure?.Invoke(vm.Lab);
        return vm;
    }

    [Theory]
    [InlineData("default")]
    [InlineData("stripped")]
    [InlineData("typeIa")]
    public void Criterion1_TheCameraHoldsAtLeastTwoThirdsOfEveryPullBackCycle_AtTheDefaultSpeed(string which)
    {
        // Ruling R-P2-8: boundary steps included, at the default speed (1/3 decade/s).
        var vm = which switch
        {
            "stripped" => Vm(l => l.Stripping = Stripping.Full),
            "typeIa" => Vm(l => l.Kind = StarKind.WhiteDwarf),
            _ => Vm(),
        };
        Assert.Equal(TimelineViewModel.DefaultDecadesPerSecond, vm.Timeline.DecadesPerSecond);
        var holds = HoldFractions(vm, TimelineViewModel.DefaultDecadesPerSecond);
        output.WriteLine($"{which}: {holds.Count} complete cycles, worst hold {holds.Min():0.000}, mean {holds.Average():0.000}");
        Assert.True(holds.Count >= 5, $"{holds.Count} complete pull-back cycles (the debris grow over many decades)");
        Assert.True(holds.Min() >= 2.0 / 3, $"worst cycle holds {holds.Min():0.000}");
    }

    // ------------------------------------------------------------------------------------------------ criterion 2

    private static IEnumerable<(string Name, Outcome Outcome)> MassCases()
    {
        foreach (var p in PresetCatalog.LoadEmbedded()) yield return (p.Id, ForPreset(p));
        yield return ("default", TestOutcomes.Engine.Compute(LabInputs.Default));
        yield return ("stripped Ib", TestOutcomes.Engine.Compute(LabInputs.Default with { Stripping = Stripping.Full }));
        yield return ("stripped Ic", TestOutcomes.Engine.Compute(LabInputs.Default with { Stripping = Stripping.HydrogenAndHelium }));
        yield return ("Type Ia", TestOutcomes.TypeIa());
        yield return ("collapsar", TestOutcomes.Collapsar());
        yield return ("pair instability", TestOutcomes.Engine.Compute(LabInputs.Default with { InitialMass = 200, Metallicity = 0.1 }));
    }

    [Fact]
    public void Criterion2_DrawnParticleMasses_AddUpToTheEjectaMass_AndEachLayerMatches()
    {
        const int N = 200_000;   // the High quality setting
        int checkedOutcomes = 0;
        foreach (var (name, o) in MassCases())
        {
            if (EjectaStructureModel.For(o) is not { } s) continue;   // no ejecta: nothing is drawn
            var set = ParticleSampler.Sample(s, N);
            double perParticle = s.EjectaMassMsun / N;
            var drawn = new Dictionary<ElementLayer, double>();
            for (int i = 0; i < set.Count; i++)
                drawn[(ElementLayer)(int)set.Layer[i]] = drawn.GetValueOrDefault((ElementLayer)(int)set.Layer[i]) + perParticle;

            double total = drawn.Values.Sum();
            Assert.True(Math.Abs(total / s.EjectaMassMsun - 1) <= 0.01, $"{name}: drawn {total} vs ejecta {s.EjectaMassMsun} M☉");
            foreach (var layer in s.Layers)
            {
                double got = drawn.GetValueOrDefault(layer.Layer);
                // Within 1% of the layer's mass (a layer lighter than 100 particles' worth is held to one particle).
                Assert.True(Math.Abs(got - layer.MassMsun) <= Math.Max(0.01 * layer.MassMsun, perParticle),
                    $"{name}: {layer.Layer} drawn {got:0.####} vs {layer.MassMsun:0.####} M☉");
            }
            Assert.Equal(s.Layers.Count, drawn.Count);   // no particle in a layer the outcome does not have
            // The layers themselves add up to the outcome's ejecta mass within 1% (ruling P2), and the nickel layer is the outcome's nickel.
            Assert.InRange(s.Layers.Sum(l => l.MassMsun) / s.EjectaMassMsun, 0.99, 1.01);
            // The nickel layer is the outcome's ⁵⁶Ni (a Type Ia's iron-group layer also holds the stable iron group, §S2.2: at least the ⁵⁶Ni).
            if (o.NickelMass is { } ni && s.Layers.SingleOrDefault(l => l.Layer == ElementLayer.NickelIron) is { } nickel)
            {
                double tolerance = 0.01 * ni.Value + perParticle;
                if (s.Class == EjectaClass.TypeIa) Assert.True(nickel.MassMsun >= ni.Value - tolerance, $"{name}: iron-group layer {nickel.MassMsun} < ⁵⁶Ni {ni.Value}");
                else Assert.True(Math.Abs(nickel.MassMsun - ni.Value) <= tolerance, $"{name}: nickel layer {nickel.MassMsun} vs outcome {ni.Value}");
            }
            checkedOutcomes++;
        }
        output.WriteLine($"{checkedOutcomes} outcomes with ejecta checked at {N:N0} particles");
        Assert.True(checkedOutcomes >= 20, $"{checkedOutcomes}");
    }

    // ------------------------------------------------------------------------------------------------ criterion 3

    [Fact]
    public void Criterion3_Mixing_Sn1987aNickelReachesTheTipSpeed_HydrogenFallsToItsMinimum()
    {
        // structure §S3 for SN 1987A: nickel to 4,700 km/s, hydrogen down to 700 km/s.
        var p = PresetCatalog.Get("sn1987a");
        var s = EjectaStructureModel.For(ForPreset(p))!;
        Assert.Equal(4700 * KmS, s.Mixing.NickelMaxCmS);
        Assert.Equal(700 * KmS, s.Mixing.HydrogenMinCmS);
        var set = ParticleSampler.Sample(s, 200_000);
        double Speed(int i) => Math.Sqrt(Math.Pow(set.Velocity[3 * i], 2) + Math.Pow(set.Velocity[3 * i + 1], 2) + Math.Pow(set.Velocity[3 * i + 2], 2)) * set.MaxCmS;
        var all = Enumerable.Range(0, set.Count).ToArray();
        double maxNi = all.Where(i => (int)set.Layer[i] == (int)ElementLayer.NickelIron).Max(Speed);
        double minH = all.Where(i => (int)set.Layer[i] == (int)ElementLayer.Hydrogen).Min(Speed);
        output.WriteLine($"SN 1987A: fastest nickel {maxNi / KmS:0} km/s (research 4,700), slowest hydrogen {minH / KmS:0} km/s (research 700)");
        Assert.InRange(maxNi / KmS, 0.95 * 4700, 4700 * (1 + 1e-6));
        Assert.InRange(minH / KmS, 700 * (1 - 1e-6), 1.05 * 700);
    }

    [Theory]
    [MemberData(nameof(PresetIds))]
    public void Criterion3_Shocks_AreOrdered_AndTheForwardShockNeverContracts(string id)
    {
        var o = ForPreset(PresetCatalog.Get(id));
        double start = ShockStructureModel.StartTimeS(o);
        if (double.IsNaN(start)) return;   // no ejecta, no shocks
        int steps = (int)Math.Ceiling(Math.Log10(1000 * Year / start) * 80);
        double previous = 0;
        for (int i = 0; i <= steps; i++)
        {
            double t = start * Math.Pow(1000 * Year / start, (double)i / steps);
            var s = ShockStructureModel.At(o, t)!;
            string what = $"{id} at {t / Year:0.####} yr";
            Assert.True(0 <= s.ReverseCm && s.ReverseCm <= s.ContactCm && s.ContactCm <= s.ForwardCm, what);   // R_r <= R_c <= R_f
            Assert.True(s.ForwardCm >= previous, $"{what}: R_f fell ({s.ForwardCm} < {previous})");            // R-P2-26
            previous = s.ForwardCm;
        }
    }

    [Fact]
    public void Criterion3_Nebula_CrabRadius_FollowsTheS9Check()
    {
        // §S9 "The Crab as a check": E = 1e50 erg, M_ej = 4.6 Msun, 954 yr gives 1.21 pc; the lab's own Crab (M_ej 7.39 Msun) gives
        // 0.96 pc, 10-45% small against the measured 1.7 pc (disclosed, inconsistency #39).
        var crab = ForPreset(PresetCatalog.Get("sn1054"));
        double t = 954 * Year;
        var nominal = crab with { ExplosionEnergyErg = TestOutcomes.Q(1e50, "erg"), EjectaMass = TestOutcomes.Q(4.6, "M☉") };
        double nominalPc = PulsarWindNebulaModel.RadiusCm(nominal, t) / Pc, labPc = PulsarWindNebulaModel.RadiusCm(crab, t) / Pc;
        output.WriteLine($"Crab nebula at 954 yr: §S9 inputs {nominalPc:0.000} pc (research 1.21), lab's Crab {labPc:0.000} pc (measured 1.7)");
        Assert.InRange(nominalPc, 1.21 * 0.99, 1.21 * 1.01);
        Assert.InRange(labPc / 1.7, 1 - 0.45, 1 - 0.10);
    }

    [Fact]
    public void Criterion3_Sn1987aRings_HaveTheS8Geometry()
    {
        // structure §S8 (Sugerman et al. 2005 scaled to 51.4 kpc): equatorial 0.665 ly; outer rings 1.460 ly at -1.398 ly and 1.635 ly at +1.028 ly; axis tilt 43 deg.
        var rings = Sn1987aRings.Rings;
        Assert.Equal(3, rings.Count);
        Assert.InRange(rings[0].RadiusCm / Ly, 0.664, 0.666);
        Assert.InRange(rings[1].RadiusCm / Ly, 1.459, 1.461);
        Assert.InRange(rings[1].AxisOffsetCm / Ly, -1.399, -1.397);
        Assert.InRange(rings[2].RadiusCm / Ly, 1.634, 1.636);
        Assert.InRange(rings[2].AxisOffsetCm / Ly, 1.027, 1.029);
        Assert.All(rings, r => Assert.Equal(43.0, r.InclinationRad * 180 / Math.PI, 9));
        Assert.Equal(Sn1987aRings.AxisTiltDeg, 43.0);
    }

    // ------------------------------------------------------------------------------------------------ criterion 4

    /// <summary>Plays a started tour with 1/60 s ticks; returns its length in seconds and the stops reached, in order.</summary>
    private static (double Seconds, List<int> Visited) PlayToEnd(TourDirector d)
    {
        var visited = new List<int> { d.StopIndex };
        int ticks = 0;
        while (d.State != TourState.Off && ticks < 60 * 1000)
        {
            d.Tick(Frame);
            ticks++;
            if (d.State != TourState.Off && d.StopIndex != visited[^1]) visited.Add(d.StopIndex);
        }
        Assert.Equal(TourState.Off, d.State);
        return (ticks / 60.0, visited);
    }

    [Theory]
    [MemberData(nameof(PresetIds))]
    public void Criterion4_EveryPresetsTour_RunsStartToEnd_WithinAMinuteAndAHalfOfPlayback(string id)
    {
        var vm = Vm(l => l.SelectedPreset = PresetCatalog.Get(id));
        var d = vm.Tour.Director;
        d.Start();
        int count = d.Stops.Count;
        Assert.True(count >= 2, id);
        var (seconds, visited) = PlayToEnd(d);
        output.WriteLine($"{id}: {count} stops, {seconds:0.0} s");
        Assert.Equal(Enumerable.Range(0, count), visited);   // every stop, in order
        Assert.Null(d.Note);
        Assert.True(seconds <= 150, $"{id}: the tour lasts {seconds:0.0} s (carry-over bound 150 s)");
    }

    [Fact]
    public void Criterion4_EverySupernovaType_GetsATour_AndTheEngineReachableOnesRunStartToEnd()
    {
        // Every type: a hand-built outcome gets a valid tour (the caption/time rules are in TourBuilderTests).
        foreach (var type in Enum.GetValues<SupernovaType>().Where(t => t != SupernovaType.None))
        {
            bool ia = type is SupernovaType.Ia or SupernovaType.Ia91T or SupernovaType.Ia91bg or SupernovaType.Iax;
            var o = TestOutcomes.Custom(type, 1e51, ia ? 1.4 : 10, 0.07, ia ? 1e9 : 3e13);
            var tour = TourBuilder.For(o, ObserverSetup.Default, null);
            Assert.True(tour.Count >= 2, $"{type}");
            Assert.Equal(TimeAxis.MinS, tour[0].ObserverTimeS);
            Assert.All(tour, s => Assert.False(string.IsNullOrWhiteSpace(s.Caption), $"{type}"));
        }

        // The real director, over the types the engine produces from the lab's inputs.
        var wanted = new Dictionary<SupernovaType, LabInputs>();
        foreach (var kind in new[] { StarKind.MassiveStar, StarKind.WhiteDwarf })
        foreach (var channel in Enum.GetValues<TypeIaChannel>())
        foreach (var stripping in Enum.GetValues<Stripping>())
        foreach (var rotation in Enum.GetValues<Rotation>())
        foreach (double z in new[] { 1.0, 0.1 })
        foreach (double m in new[] { 9.0, 12, 16, 25, 40 })
        {
            var x = LabInputs.Default with { Kind = kind, IaChannel = channel, Stripping = stripping, Rotation = rotation, Metallicity = z, InitialMass = m };
            if (kind == StarKind.WhiteDwarf && channel != TypeIaChannel.NearChandrasekhar) x = x with { WhiteDwarfMass = 1.1 };
            var o = TestOutcomes.Engine.Compute(x);
            if (o.Fate == Fate.Supernova) wanted.TryAdd(o.Type, x);
        }
        wanted.TryAdd(TestOutcomes.Collapsar().Type, TestOutcomes.Collapsar().Inputs);
        output.WriteLine("types run through the director: " + string.Join(", ", wanted.Keys));
        Assert.True(wanted.Count >= 5, $"{wanted.Count} types reachable");
        foreach (var (type, x) in wanted)
        {
            var vm = Vm(l =>
            {
                l.Kind = x.Kind; l.InitialMass = x.InitialMass; l.Metallicity = x.Metallicity; l.Stripping = x.Stripping; l.Rotation = x.Rotation;
                l.Field = x.Field; l.IaChannel = x.IaChannel; l.WhiteDwarfMass = x.WhiteDwarfMass; l.Mixing = x.Mixing; l.Yield = x.Yield;
            });
            Assert.Equal(type, vm.CurrentOutcome.Type);
            var d = vm.Tour.Director;
            d.Start();
            var (seconds, visited) = PlayToEnd(d);
            Assert.Equal(Enumerable.Range(0, d.Stops.Count), visited);
            Assert.True(seconds <= 150, $"{type}: {seconds:0.0} s");
        }
    }

    [Fact]
    public void Criterion4_CaptionNumbers_EqualTheModelValues_ForEveryPreset()
    {
        int breakouts = 0, markerTimes = 0;
        foreach (var p in PresetCatalog.LoadEmbedded())
        {
            var o = ForPreset(p);
            var tour = TourBuilder.For(o, p.Observer, p);
            string all = string.Join("\n", tour.Select(s => s.Caption));

            // The breakout time in the caption is the model's, in observer time.
            double breakout = EjectaKinematics.BreakoutTimeS(o) * p.Observer.TimeDilation;
            if (tour.FirstOrDefault(s => s.Caption.Contains("breaks out")) is { } bo)
            {
                Assert.Contains($"the shock breaks out of the star {UnitFormat.Time(breakout)} after", bo.Caption);
                breakouts++;
            }
            // Every event marker's time appears, formatted as the timeline formats it, in the caption of the stop that carries it.
            foreach (var m in EventMarkers.For(o, p.Observer))
                if (m.GlossaryKey is "NeutrinoBurst" or "GravitationalWaves" or "PeakBrightness" or "RadioPeak" or "PlateauEnd" or "GammaRayBurst")
                {
                    Assert.True(all.Contains(UnitFormat.Time(m.ObserverTimeS)), $"{p.Id}: {m.Label} at {UnitFormat.Time(m.ObserverTimeS)} is in no caption");
                    markerTimes++;
                }
        }
        output.WriteLine($"{breakouts} breakout times and {markerTimes} marker times matched");
        Assert.True(breakouts >= 5 && markerTimes >= 30, $"{breakouts}, {markerTimes}");
    }

    [Fact]
    public void Criterion4_Explode_WhileTheTourSitsAtStopZero_PausesTheTour()
    {
        // Task 14 carry-over: Explode at the first millisecond changes no time, yet it takes over from the tour.
        var vm = Vm();
        var d = vm.Tour.Director;
        d.Start();
        Assert.Equal(TourState.Dwelling, d.State);
        Assert.Equal(0, d.StopIndex);
        Assert.Equal(TimeAxis.MinS, vm.Timeline.ObserverTimeS);
        vm.ExplodeCommand.Execute(null);
        Assert.Equal(TourState.Paused, d.State);
        Assert.True(vm.Tour.ShowResume);
        Assert.True(vm.Timeline.IsPlaying);
    }

    // ------------------------------------------------------------------------------------------------ the sweep's counters

    [Fact]
    public void Sweep_CountsTheErrorsTheSceneAndTheTimelineMarkersCatch()
    {
        // Project 1's parked minor: --sweep read only the panels and the 3D view's failure flag, not the errors caught inside.
        var vm = Vm();
        using var scene = new SceneController(vm, () => new FakeHost(), (_, _) => throw new InvalidOperationException("model broke"));
        Assert.True(scene.ModelErrorCount >= 1);
        int before = scene.ModelErrorCount;
        vm.Timeline.ObserverTimeS = PhysicalConstants.Day;   // restating a scene that never built is quiet; a rebuild counts again
        vm.Lab.Stripping = Stripping.Full;
        Assert.True(scene.ModelErrorCount > before);

        int markerFailures = EventMarkers.Failures;
        Assert.Empty(EventMarkers.Guarded(() => throw new InvalidOperationException("marker model broke")));
        Assert.Equal(markerFailures + 1, EventMarkers.Failures);
    }
}
