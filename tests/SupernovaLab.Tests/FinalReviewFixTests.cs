using System.IO;
using System.Windows;
using System.Xml.Linq;
using SupernovaLab.App.Controllers;
using SupernovaLab.App.Tour;
using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Structure;
using SupernovaLab.Core.Text;
using SupernovaLab.Core.Units;
using SupernovaLab.Rendering;

namespace SupernovaLab.Tests;

/// <summary>
/// Project 2's final whole-branch review: one test group per finding (I1–I12, M3–M5), under rulings R-P2-32 and R-P2-33.
/// Formula values are hand-computed in the comments, never recomputed with the code under test.
/// </summary>
public class FinalReviewFixTests
{
    private const double Day = PhysicalConstants.Day, Year = PhysicalConstants.Year, Pc = PhysicalConstants.Pc;

    private static Outcome Preset(string id)
    {
        var p = PresetCatalog.Get(id);
        return TestOutcomes.Engine.Compute(p.Inputs, p.Pins);
    }

    private static Outcome Default() => TestOutcomes.Engine.Compute(LabInputs.Default);

    /// <summary>The lab's own Type IIn: the default star with the dense-gas switch.</summary>
    private static Outcome LabIIn() => TestOutcomes.Engine.Compute(LabInputs.Default with { DenseCsm = true });

    private sealed class FakeHost : IRenderHost
    {
        public bool Animating { get; set; }
        public event Action<string>? ContextReady { add { } remove { } }
        public event Action<TimeSpan, Size>? Draw { add { } remove { } }
        public event Action<Exception>? FrameFailed { add { } remove { } }
        public void Start() { }
        public void RequestRender() { }
        public bool Shutdown(Action release) => false;
    }

    // ------------------------------------------------------------------------------------------------ I1 (ruling R-P2-32)

    [Fact]
    public void I1_Sn1987a_RingsAreDisclosedAsSymmetric_WheneverTheirNoteShows()
    {
        var scene = new ExplosionScene(Preset("sn1987a"));
        int shown = 0;
        foreach (double d in new[] { 1000.0, 3000, 8000, 12000 })
        {
            var notes = scene.At(d * Day).Notes;
            Assert.Equal(notes.Contains(Sn1987aRings.Note), notes.Contains(ExplosionScene.RingsSymmetricNote));
            if (notes.Contains(Sn1987aRings.Note)) shown++;
        }
        Assert.True(shown >= 2);
        Assert.Contains("#38", ExplosionScene.RingsSymmetricNote);
        Assert.DoesNotContain(ExplosionScene.RingsSymmetricNote, new ExplosionScene(Default()).At(8000 * Day).Notes);
    }

    [Fact]
    public void I1_Sn1987a_TheNebulaIsDisclosedAsUnconfirmed_WithItsNote()
    {
        var scene = new ExplosionScene(Preset("sn1987a"));
        int shown = 0;
        foreach (double d in new[] { 300.0, 1000, 3000, 8000, 30000 })
        {
            var notes = scene.At(d * Day).Notes;
            bool nebula = notes.Contains(PulsarWindNebulaModel.Note);
            Assert.Equal(nebula, notes.Contains(ExplosionScene.Sn1987aNebulaNote));
            if (nebula) shown++;
        }
        Assert.True(shown >= 1, "the nebula's note shows at some time");
        Assert.Contains("#49", ExplosionScene.Sn1987aNebulaNote);
        // The default star also has a nebula, but no #49 note: it is not SN 1987A.
        Assert.DoesNotContain(ExplosionScene.Sn1987aNebulaNote, new ExplosionScene(Default()).At(300 * Year).Notes);
    }

    [Fact]
    public void I1_CasA_TheReverseShockMissIsDisclosed_InTheRemnantEra_AndItsNumberIsTheDrawnOne()
    {
        var o = Preset("casa");
        Assert.True(o.Discloses(46));
        var scene = new ExplosionScene(o);
        var s = scene.At(350 * Year);
        Assert.Contains(ExplosionScene.CasAReverseShockNote, s.Notes);
        Assert.DoesNotContain(ExplosionScene.CasAReverseShockNote, scene.At(1 * Year).Notes);   // not before the remnant era
        // The note states 0.85 at Cas A's age (#46: 1.98 of 2.33 pc at 350 yr).
        Assert.Equal(0.85, Math.Round(s.Shocks!.ReverseCm / s.Shocks.ForwardCm, 2));
        Assert.Contains("0.85", ExplosionScene.CasAReverseShockNote);
        Assert.Contains("0.62", ExplosionScene.CasAReverseShockNote);
        Assert.DoesNotContain(ExplosionScene.CasAReverseShockNote, new ExplosionScene(Default()).At(350 * Year).Notes);
        Assert.DoesNotContain(ExplosionScene.CasAReverseShockNote, new ExplosionScene(Preset("tycho")).At(450 * Year).Notes);
    }

    [Fact]
    public void I1_Crab_TheSmallNebulaIsDisclosed_AndItsNumberIsTheDrawnOne()
    {
        var o = Preset("sn1054");
        Assert.True(o.Discloses(47));
        var s = new ExplosionScene(o).At(954 * Year);
        Assert.Contains(PulsarWindNebulaModel.Note, s.Notes);
        Assert.Contains(ExplosionScene.CrabNebulaNote, s.Notes);
        // #47: 0.959 pc at 954 yr; the note says "about 0.96 pc".
        Assert.Equal(0.96, Math.Round(s.NebulaRadiusCm / Pc, 2));
        Assert.Contains("0.96 pc", ExplosionScene.CrabNebulaNote);
        Assert.Contains("#47", ExplosionScene.CrabNebulaNote);
        Assert.DoesNotContain(ExplosionScene.CrabNebulaNote, new ExplosionScene(Default()).At(954 * Year).Notes);
    }

    [Fact]
    public void I1_TheDisclosuresArePassThrough_TheyChangeNoModelledValue()
    {
        var p = PresetCatalog.Get("casa");
        Assert.Equal(new[] { 46, 54, 60, 61 }, p.Pins!.SceneDisclosures);   // #54: the Cas A temperature comparison (Task 5); #60, #61: no pulses seen, kick speed (Task 6, R-P3-17)
        var bare = TestOutcomes.Engine.Compute(p.Inputs, p.Pins with { SceneDisclosures = null });
        var with = Preset("casa");
        Assert.Equal(bare.UsesPublishedValues, with.UsesPublishedValues);
        Assert.Equal(bare.ExplosionEnergyErg, with.ExplosionEnergyErg);
        Assert.Equal(bare.EjectaMass, with.EjectaMass);
        Assert.False(bare.Discloses(46));
        Assert.False(new PresetPins(null, null, null, null, new Source("t", "t"), SceneDisclosures: new[] { 46 }).ChangesModelledValue);
    }

    [Fact]
    public void I1_TheToursOrbit_KeepsTheViewAnimating_AndPauseExitAndAStillStopStopIt()
    {
        var vm = new MainViewModel(TestOutcomes.Engine, new FakeWindowService());
        var host = new FakeHost();
        var scene = new SceneController(vm, () => host);
        scene.Start();
        var d = vm.Tour.Director;
        d.Scene = scene;
        Assert.False(host.Animating);

        d.Start();   // stop 0: 1 ms, a cutaway with the sway
        Assert.True(d.Current!.Sways);
        Assert.True(scene.IsTourOrbiting);
        Assert.True(host.Animating);

        d.Pause();
        Assert.False(scene.IsTourOrbiting);
        Assert.False(host.Animating);
        d.Resume();
        Assert.True(scene.IsTourOrbiting);

        int still = d.Stops.ToList().FindIndex(s => !s.Sways);
        Assert.True(still > 0);
        d.GoTo(still);
        Assert.False(scene.IsTourOrbiting);

        int orbit = d.Stops.ToList().FindIndex(still, s => s.Sways);   // the mixing stop (I7)
        Assert.True(orbit > still);
        d.GoTo(orbit);
        Assert.True(scene.IsTourOrbiting);

        d.Exit();
        Assert.False(scene.IsTourOrbiting);
        scene.Dispose();
    }

    [Fact]
    public void I1_TheReadme_ListsTheMissesShownOnlyInTheInconsistencyList()
    {
        string readme = File.ReadAllText(Path.Combine(RepoRoot(), "README.md"));
        int known = readme.IndexOf("### Known misses", StringComparison.Ordinal);
        int next = readme.IndexOf("### Other known limits", StringComparison.Ordinal);
        Assert.True(known >= 0 && next > known);
        string section = readme[known..next];
        foreach (string n in new[] { "#35", "#36", "#37", "#41", "#44" }) Assert.Contains(n, section);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SupernovaLab.sln")) &&
               !Directory.Exists(Path.Combine(dir.FullName, "docs", "research")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("repository root");
    }

    // ------------------------------------------------------------------------------------------------ I2

    [Fact]
    public void I2_At_NeverRebuildsTheEjectaStructure_ForANeutronStarOutcome()
    {
        foreach (var o in new[] { Default(), Preset("sn1987a"), Preset("sn1054") })
        {
            Assert.True(PulsarWindNebulaModel.Applies(o));
            var scene = new ExplosionScene(o);
            int before = EjectaStructureModel.BuildCountOnThisThread;
            foreach (double t in new[] { 1e-3, 30 * Day, 5 * Year, 100 * Year, 954 * Year }) scene.At(t);
            Assert.Equal(before, EjectaStructureModel.BuildCountOnThisThread);
        }
    }

    [Fact]
    public void I2_TheNebulaOverload_GivesTheSameRadius_FromTheScenesStructureAndShocks()
    {
        var o = Default();
        var structure = EjectaStructureModel.For(o)!;
        foreach (double t in new[] { 30 * Day, 5 * Year, 300 * Year })
            Assert.Equal(PulsarWindNebulaModel.RadiusCm(o, t),
                PulsarWindNebulaModel.RadiusCm(o, structure, ShockStructureModel.At(o, t), t));
    }

    // ------------------------------------------------------------------------------------------------ I3

    public static IEnumerable<object[]> Eras() =>
        from id in new[] { "default", "sn1987a", "sn1998bw" }
        from era in new[] { "pre-breakout", "30 d", "5 yr", "100 yr" }
        select new object[] { id, era };

    [Theory]
    [MemberData(nameof(Eras))]
    public void I3_TheCollapsedNotes_LeadWithTheEventStageOrReadout_AndTheMarkerAndJetsComeLast(string id, string era)
    {
        var o = id == "default" ? Default() : Preset(id);
        double t = era switch
        {
            "pre-breakout" => 0.5 * EjectaKinematics.BreakoutTimeS(o),
            "30 d" => 30 * Day,
            "5 yr" => 5 * Year,
            _ => 100 * Year,
        };
        var notes = SceneController.OrderNotes(new ExplosionScene(o).At(t).Notes, null);
        var ranks = notes.Select(SceneController.NoteRank).ToList();
        for (int i = 1; i < ranks.Count; i++) Assert.True(ranks[i] >= ranks[i - 1], $"{id} {era}: {string.Join(" | ", notes)}");
        Assert.InRange(ranks[0], 1, 3);   // an event, the stage, or what the readout is
        string expected = era switch
        {
            "pre-breakout" => "The shock is crossing the star",
            "30 d" => ExplosionScene.UniformSphereNote,
            "5 yr" => ExplosionScene.BlendNote,
            _ => ExplosionScene.UniformRemnantNote,
        };
        // SN 1987A's rings are the current event once the view reaches them (from about 5 yr).
        if (notes[0] != Sn1987aRings.Note) Assert.StartsWith(expected, notes[0]);
        else Assert.Equal("sn1987a", id);
        Assert.DoesNotContain(notes, n => n.Contains("(marker)", StringComparison.Ordinal) && SceneController.NoteRank(n) != 6);
    }

    [Theory]
    [InlineData("Neutron star (marker)", 6)]
    [InlineData("The gamma-ray burst's jets are no longer drawn: the lab's afterglow model ends 2 days after bounce", 6)]
    [InlineData(ExplosionScene.FlashNote, 1)]
    [InlineData(SceneController.CutawayNote, 1)]
    [InlineData(ExplosionScene.BlendNote, 3)]
    [InlineData(ExplosionScene.ShockNote, 3)]
    [InlineData(ExplosionScene.CrabShellNote, 4)]
    [InlineData(ExplosionScene.RingsSymmetricNote, 4)]
    [InlineData(ExplosionScene.SchematicNote, 5)]
    [InlineData(ExplosionScene.MixingNote, 5)]
    public void I3_NoteRanks(string note, int rank) => Assert.Equal(rank, SceneController.NoteRank(note));

    [Fact]
    public void I3_TheQualityNote_StillComesFirst()
    {
        var notes = SceneController.OrderNotes(new[] { ExplosionScene.SchematicNote, ExplosionScene.FlashNote }, "q");
        Assert.Equal(new[] { "q", ExplosionScene.FlashNote, ExplosionScene.SchematicNote }, notes);
    }

    // ------------------------------------------------------------------------------------------------ I4 (ruling R-P2-33)

    [Theory]
    // 0.3 pc against a 2 pc reference: 0.3 x 3.2616 = 0.978 ly.
    [InlineData(0.3 * 3.0857e18, 2 * 3.0857e18, "0.3 pc (0.978 light-years)")]
    // 5,000 AU against 2 pc: 5,000 / 206,265 = 0.0242 pc = 0.0791 ly.
    [InlineData(5000 * 1.495978707e13, 2 * 3.0857e18, "0.0242 pc (0.0791 light-years)")]
    // 0.5 AU against 3 AU: AU.
    [InlineData(0.5 * 1.495978707e13, 3 * 1.495978707e13, "0.5 AU")]
    // 2 AU against 0.5 AU: km (3.0e8 km).
    [InlineData(2 * 1.495978707e13, 0.5 * 1.495978707e13, "299,000,000 km")]
    // 1.5 kpc against 3 kpc: kpc (4,890 ly).
    [InlineData(1500 * 3.0857e18, 3000 * 3.0857e18, "1.5 kpc (4,890 light-years)")]
    public void I4_ADistance_InAnotherValuesUnit(double cm, double reference, string expected)
        => Assert.Equal(expected, UnitFormat.Distance(cm, reference));

    [Fact]
    public void I4_RemnantEraReadout_NamesTheRemnantPanelsBlastWave_AndBothLinesShareOneUnit()
    {
        foreach (var o in new[] { Default(), Preset("casa"), Preset("tycho") })
        foreach (double yr in new[] { 10.5, 100, 350 })
        {
            var s = new ExplosionScene(o).At(yr * Year);
            Assert.Equal(ExplosionScene.RemnantPanelLabel, s.RadiusLabel);
            var lines = SceneController.ReadoutText(s).Split(Environment.NewLine);
            Assert.Equal(2, lines.Length);
            Assert.StartsWith("Remnant panel's blast wave (uniform ejecta): ", lines[0]);
            Assert.Equal(UnitOf(lines[0]), UnitOf(lines[1]));
        }
        // Before the remnant era the labels are unchanged.
        Assert.Equal("Ejecta radius", new ExplosionScene(Default()).At(Year).RadiusLabel);
    }

    /// <summary>The unit word after the number of "Label: 1.23 pc (...)".</summary>
    private static string UnitOf(string line) => line[(line.LastIndexOf(": ", StringComparison.Ordinal) + 2)..].Split(' ')[1];

    // ------------------------------------------------------------------------------------------------ I5

    [Fact]
    public void I5_AnOutcomeWithoutAWindEra_GetsTheNoWindShockNote()
    {
        var ia = TestOutcomes.TypeIa();
        Assert.False(ShockStructureModel.HasWindEra(ia));
        var notes = new ExplosionScene(ia).At(30 * Year).Notes;
        Assert.DoesNotContain(ExplosionScene.ShockNote, notes);
        // A Type Ia's remnant solution is TM99's n = 7 (structure §S4.2).
        Assert.Contains("Shocks: radii from Truelove & McKee 1999 and Tang & Chevalier 2017 for power-law outer debris (n = 7), drawn from 3 years",
            Assert.Single(notes, n => n.StartsWith("Shocks:", StringComparison.Ordinal)));
        Assert.DoesNotContain("radio model", ExplosionScene.ShockNoteFor(ia));

        var cc = Default();
        Assert.True(ShockStructureModel.HasWindEra(cc));
        Assert.Contains(ExplosionScene.ShockNote, new ExplosionScene(cc).At(30 * Year).Notes);
        Assert.Contains("Type Ia", Glossary.ForwardShock);
        Assert.Contains("without that wind model", Glossary.ForwardShock);
    }

    // ------------------------------------------------------------------------------------------------ I6

    [Fact]
    public void I6_ALabIIn_BeforeBreakout_FramesTheStar_AndFramesTheShellOnceTheDebrisStart()
    {
        foreach (var o in new[] { LabIIn(), Preset("sn2023ixf") })
        {
            var scene = new ExplosionScene(o);
            double tbo = EjectaKinematics.BreakoutTimeS(o);
            var before = scene.At(0.5 * tbo);
            Assert.True(before.ShowStar);
            Assert.NotNull(before.Surroundings!.ShellOuterCm);
            Assert.Equal(o.Star!.RadiusCm.Value, before.FrameRadiusCm);
            var after = scene.At(1.5 * tbo);
            Assert.True(after.FrameRadiusCm >= after.Surroundings!.ShellOuterCm!.Value, $"{o.Type}: the shell frames the debris' start");
        }
        Assert.Equal(SupernovaType.IIn, LabIIn().Type);
    }

    // ------------------------------------------------------------------------------------------------ I7 (ruling R-P2-33)

    [Theory]
    [InlineData("default")]
    [InlineData("sn1987a")]
    public void I7_AToursMixingStop_IsACutawayAfterBreakout_AtPeakLight(string id)
    {
        var p = id == "default" ? null : PresetCatalog.Get(id);
        var o = p is null ? Default() : Preset(id);
        var obs = p?.Observer ?? ObserverSetup.Default;
        var tour = TourBuilder.For(o, obs, p);
        double breakout = EjectaKinematics.BreakoutTimeS(o) * obs.TimeDilation;
        var mixing = Assert.Single(tour, s => s.Caption.Contains("a slice through the debris shows the mixing fingers", StringComparison.Ordinal));
        Assert.True(mixing.ObserverTimeS > breakout);
        Assert.True(mixing.Cutaway);
        Assert.True(mixing.Sways);
        double peak = new LightCurveModel().PeakTimeS(o) * obs.TimeDilation;
        // At peak light: the stop is the peak's own merged group (SN 1987A's anchor is the observed peak line, 84.5 d, 0.009 decades earlier).
        Assert.True(Math.Abs(Math.Log10(mixing.ObserverTimeS / peak)) < TourBuilder.MergeDecades, $"{mixing.ObserverTimeS} s vs peak {peak} s");
        Assert.Contains(mixing.Notes, n => n.Kind == TourSourceKind.LabModel && n.Source.ResearchRef.Contains("§S3"));
        Assert.Contains(id == "sn1987a" ? "observed in SN 1987A" : "from 3D simulations", mixing.Caption);
        Assert.Single(tour, s => s.Cutaway && s.ObserverTimeS > breakout);
    }

    [Fact]
    public void I7_Sn1987asMixingNote_SaysItsReachIsObserved()
    {
        // The tour's caption and the scene note agree: SN 1987A's reach is the observed one (structure §S3), not a simulation's.
        var notes = new ExplosionScene(Preset("sn1987a")).At(100 * Day).Notes;
        Assert.Contains(ExplosionScene.Sn1987aMixingNote, notes);
        Assert.DoesNotContain(ExplosionScene.MixingNote, notes);
        Assert.Contains(ExplosionScene.MixingNote, new ExplosionScene(Default()).At(100 * Day).Notes);
    }

    [Fact]
    public void I7_NoMixingStop_WithoutFingers()
    {
        var ia = TestOutcomes.TypeIa();
        Assert.False(EjectaStructureModel.For(ia)!.Mixing.HasFingers);
        Assert.DoesNotContain(TourBuilder.For(ia, ObserverSetup.Default, null), s => s.Caption.Contains("mixing fingers"));
    }

    // ------------------------------------------------------------------------------------------------ I8

    [Fact]
    public void I8_TheReadoutAndTheNotes_SitInTwoEqualColumns_SoTheyCannotOverlap()
    {
        var xaml = XDocument.Load(Path.Combine(RepoRoot(), "src", "SupernovaLab.App", "MainWindow.xaml"));
        XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var readout = xaml.Descendants(p + "TextBlock").Single(e => (string?)e.Attribute("Text") == "{Binding RadiusText}");
        var notes = xaml.Descendants(p + "TextBlock").Single(e => (string?)e.Attribute("Text") == "{Binding NotesDisplayText}");
        var readoutBox = readout.Ancestors(p + "Border").First();
        var notesBox = notes.Ancestors(p + "Border").First();
        Assert.Same(readoutBox.Parent, notesBox.Parent);
        var grid = readoutBox.Parent!;
        Assert.Equal(p + "Grid", grid.Name);
        var columns = grid.Element(p + "Grid.ColumnDefinitions")!.Elements(p + "ColumnDefinition").Select(c => (string?)c.Attribute("Width")).ToList();
        Assert.Equal(new[] { "*", "*" }, columns);
        Assert.Equal("0", (string?)readoutBox.Attribute("Grid.Column"));
        Assert.Equal("1", (string?)notesBox.Attribute("Grid.Column"));
    }

    // ------------------------------------------------------------------------------------------------ I9 (ruling R-P2-33)

    private const double Rstar = 3e13, VMax = 3e9, Since = 1e8, K = Rstar + VMax * Since;

    /// <summary>A reverse shock standing ahead of the shocked debris' free radius: R_r = 0.8 K, R_c = 0.9 K, s_m = 0.5.</summary>
    private static ShockRadii AheadShocks(double w) => new(0.95 * K, 0.9 * K, 0.8 * K) { ConfinementWeight = w, ReverseLagrangianCmS = 0.5 * VMax };

    [Fact]
    public void I9_AConfinedParticle_IsNeverDrawnAheadOfItsFreeRadius()
    {
        // s = 0.6: r_conf = 0.8 + 0.1 x (0.1 / 0.5) = 0.82 K, ahead of the free 0.6 K: clamped to 0.6 K.
        Assert.Equal(0.6, ParticleKinematics.RadiusCm(0.6 * VMax, Since, Rstar, VMax, AheadShocks(1)) / K, 12);
        // s = 0.95: r_conf = 0.8 + 0.1 x 0.9 = 0.89 K, behind the free 0.95 K: kept.
        Assert.Equal(0.89, ParticleKinematics.RadiusCm(0.95 * VMax, Since, Rstar, VMax, AheadShocks(1)) / K, 12);
        // w = 0.5, s = 0.6: 0.6 + 0.5 x (0.82 - 0.6) = 0.71 K > 0.6 K: clamped; s = 0.95: 0.95 + 0.5 x (0.89 - 0.95) = 0.92 K.
        Assert.Equal(0.6, ParticleKinematics.RadiusCm(0.6 * VMax, Since, Rstar, VMax, AheadShocks(0.5)) / K, 12);
        Assert.Equal(0.92, ParticleKinematics.RadiusCm(0.95 * VMax, Since, Rstar, VMax, AheadShocks(0.5)) / K, 12);
    }

    [Fact]
    public void I9_TheCutFacesInverse_FollowsTheClamp()
    {
        // FreeRadiusCm inverts the clamped map: 0.6 K (clamped, free) -> 0.6 K; 0.89 K (w = 1) and 0.92 K (w = 0.5) -> 0.95 K.
        Assert.Equal(0.6, ParticleKinematics.FreeRadiusCm(0.6 * K, Since, Rstar, VMax, AheadShocks(1)) / K, 12);
        Assert.Equal(0.95, ParticleKinematics.FreeRadiusCm(0.89 * K, Since, Rstar, VMax, AheadShocks(1)) / K, 12);
        Assert.Equal(0.6, ParticleKinematics.FreeRadiusCm(0.6 * K, Since, Rstar, VMax, AheadShocks(0.5)) / K, 12);
        Assert.Equal(0.95, ParticleKinematics.FreeRadiusCm(0.92 * K, Since, Rstar, VMax, AheadShocks(0.5)) / K, 12);
        // Round trip over the speeds.
        foreach (double w in new[] { 0.3, 1.0 })
        for (int i = 1; i <= 100; i++)
        {
            double s = i / 100.0, r = ParticleKinematics.RadiusCm(s * VMax, Since, Rstar, VMax, AheadShocks(w));
            Assert.Equal(s, ParticleKinematics.FreeRadiusCm(r, Since, Rstar, VMax, AheadShocks(w)) / K, 9);
        }
    }

    [Fact]
    public void I9_TheShadersMirrorTheClamp()
    {
        string Text(string name) => System.Text.Encoding.ASCII.GetString(ReadShader(name));
        Assert.Contains("r = min(r + u_shockWeight * (confined - r), r);", Text("particle.vert"));
        Assert.Contains("ParticleKinematics.RadiusCm", Text("particle.vert"));
        Assert.Contains("max(r / u_freeScale, sm + (r - first) / slope) * u_freeScale", Text("cutface.frag"));
        Assert.Contains("ParticleKinematics.FreeRadiusCm", Text("cutface.frag"));
    }

    private static byte[] ReadShader(string name)
    {
        var asm = typeof(ParticleRenderer).Assembly;
        string resource = asm.GetManifestResourceNames().Single(n => n.EndsWith("." + name, StringComparison.Ordinal));
        using var stream = asm.GetManifestResourceStream(resource)!;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    [Theory]
    [InlineData("sn1987a")]
    [InlineData("casa")]
    [InlineData("sn1993j")]
    public void I9_NoDrawnParticle_ExceedsItsFreeRadius(string id)
    {
        // Before the clamp SN 1987A's 18 fastest particles were drawn up to x1.72 ahead at 10 yr, Cas A x1.19, SN 1993J x1.12.
        var o = Preset(id);
        var scene = new ExplosionScene(o);
        var set = scene.ParticlesFor(200_000)!;
        double r0 = o.Star!.RadiusCm.Value;
        foreach (double yr in new[] { 5.0, 10, 30 })
        {
            var st = scene.At(yr * Year);
            Assert.NotNull(st.Shocks);
            for (int i = 0; i < set.Count; i++)
            {
                double vx = set.Velocity[3 * i], vy = set.Velocity[3 * i + 1], vz = set.Velocity[3 * i + 2];
                double v = Math.Sqrt(vx * vx + vy * vy + vz * vz) * set.MaxCmS;
                double free = ParticleKinematics.RadiusCm(v, st.SinceBreakoutS, r0, set.MaxCmS, null);
                double drawn = ParticleKinematics.RadiusCm(v, st.SinceBreakoutS, r0, set.MaxCmS, st.Shocks);
                Assert.True(drawn <= free * (1 + 1e-12), $"{id} at {yr} yr: particle {i} drawn at {drawn / free:0.000} x its free radius");
            }
        }
    }

    // ------------------------------------------------------------------------------------------------ I10 (ruling R-P2-33)

    [Fact]
    public void I10_AIInsPeakBeforeBreakout_IsNotATourStop()
    {
        var o = LabIIn();
        var obs = ObserverSetup.Default;
        double breakout = EjectaKinematics.BreakoutTimeS(o) * obs.TimeDilation;
        // The premise (inconsistency #51): Project 1's IIn light curve peaks before the shock reaches the surface.
        var peak = Assert.Single(EventMarkers.For(o, obs), m => m.GlossaryKey == nameof(Glossary.PeakBrightness));
        Assert.True(peak.ObserverTimeS < breakout, $"peak {peak.ObserverTimeS} s, breakout {breakout} s");
        var tour = TourBuilder.For(o, obs, null);
        Assert.DoesNotContain(tour, s => s.Caption.Contains("reaches its peak brightness", StringComparison.Ordinal));
        Assert.DoesNotContain(tour, s => s.Highlight == Messenger.VisibleLight && s.ObserverTimeS < breakout);
        // An ordinary star keeps its peak stop.
        Assert.Contains(TourBuilder.For(Default(), obs, null), s => s.Caption.Contains("reaches its peak brightness", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------------------------------------ I11

    [Fact]
    public void I11_TheReadme_NoLongerContradictsTheApp()
    {
        string readme = File.ReadAllText(Path.Combine(RepoRoot(), "README.md"));
        Assert.DoesNotContain("light as the blast wave reaches the ring", readme);
        Assert.DoesNotContain("The Crab's pulsar wind nebula,", readme);
        Assert.DoesNotContain("The lab has no outward mixing of the cobalt.", readme);
    }

    // ------------------------------------------------------------------------------------------------ I12 (ruling R-P2-33)

    [Fact]
    public void I12_TheCrabIsFlaggedByItsPreset_AndItsShellNoteShowsOnlyFrom10Years()
    {
        var crab = Preset("sn1054");
        Assert.True(crab.HasUndetectedOuterShell);
        Assert.False(Preset("casa").HasUndetectedOuterShell);
        Assert.False(Default().HasUndetectedOuterShell);
        Assert.False(new PresetPins(null, null, null, null, new Source("t", "t"), UndetectedOuterShell: true).ChangesModelledValue);

        var scene = new ExplosionScene(crab);
        Assert.NotNull(scene.At(5 * Year).Shocks);
        Assert.DoesNotContain(ExplosionScene.CrabShellNote, scene.At(5 * Year).Notes);
        Assert.Contains(ExplosionScene.CrabShellNote, scene.At(954 * Year).Notes);
        // A measured pulsar alone no longer marks the Crab.
        Assert.DoesNotContain(ExplosionScene.CrabShellNote, new ExplosionScene(crab with { HasUndetectedOuterShell = false }).At(954 * Year).Notes);
    }

    // ------------------------------------------------------------------------------------------------ minor items

    [Fact]
    public void M3_TheDenseShellsGlossary_SaysSn2023ixfsExtentIsObserved()
    {
        Assert.Contains("SN 2023ixf's extent is observed", Glossary.DenseCircumstellarShell);
        Assert.DoesNotContain("Its extent is a schematic estimate", Glossary.DenseCircumstellarShell);
    }

    [Fact]
    public void M4_ShocksAtANonFiniteTime_AreNull()
    {
        Assert.Null(ShockStructureModel.At(Default(), double.PositiveInfinity));
        Assert.Null(ShockStructureModel.At(Default(), double.NaN));
    }

    [Fact]
    public void M5_Sn2011fesTourLine_HasNoOverclaim_AndExplainsPtf()
    {
        var line = Assert.Single(PresetCatalog.Get("sn2011fe").TourLines, l => l.Text.Contains("PTF", StringComparison.Ordinal));
        Assert.DoesNotContain("best-observed", line.Text);
        Assert.Contains("transient survey (PTF)", line.Text);
    }
}
