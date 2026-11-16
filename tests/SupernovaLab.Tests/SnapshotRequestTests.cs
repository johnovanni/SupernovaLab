using SupernovaLab.App.Services;
using SupernovaLab.Rendering;

namespace SupernovaLab.Tests;

/// <summary>
/// Ruling R19: the <c>--snapshot</c> diagnostic's arguments,
/// <c>--snapshot &lt;out-dir&gt; [--preset &lt;id&gt;] [--time &lt;s&gt;] [--size &lt;WxH&gt;] [--window main|instruments|details|presetinfo]</c>.
/// </summary>
public class SnapshotRequestTests
{
    [Fact]
    public void NoSnapshotFlag_IsNotASnapshot() => Assert.Null(SnapshotRequest.Parse(Array.Empty<string>()));

    [Fact]
    public void OutDirOnly_UsesTheDefaults()
    {
        var r = SnapshotRequest.Parse(new[] { "--snapshot", @"C:\out" })!;

        Assert.Equal(@"C:\out", r.OutDirectory);
        Assert.Null(r.PresetId);
        Assert.Empty(r.PresetIds);
        Assert.Null(r.ObserverTimeS);
        Assert.Equal(new[] { new SnapshotSize(1366, 768) }, r.Sizes);
        Assert.Equal(new[] { SnapshotWindow.Main }, r.Windows);
    }

    [Fact]
    public void EveryOption_IsRead()
    {
        var r = SnapshotRequest.Parse(new[]
        {
            "--snapshot", "shots", "--preset", "sn1987a", "--time", "8.64e6", "--size", "1920x1080", "--window", "details",
        })!;

        Assert.Equal("shots", r.OutDirectory);
        Assert.Equal("sn1987a", r.PresetId);
        Assert.Equal(8.64e6, r.ObserverTimeS);
        Assert.Equal(new[] { new SnapshotSize(1920, 1080) }, r.Sizes);
        Assert.Equal(new[] { SnapshotWindow.Details }, r.Windows);
    }

    /// <summary>Several captures in one launch: repeated or comma-separated sizes and windows.</summary>
    [Fact]
    public void SizesAndWindows_CanBeListed()
    {
        var r = SnapshotRequest.Parse(new[]
        {
            "--snapshot", "out", "--size", "1366x768,1920x1080", "--size", "1366X728",
            "--window", "main,details", "--window", "presetinfo", "--window", "instruments",
            "--preset", "sn1987a,sn2011fe", "--preset", "sn1998bw",
        })!;

        Assert.Equal(new[] { "sn1987a", "sn2011fe", "sn1998bw" }, r.PresetIds);
        Assert.Equal(new[] { new SnapshotSize(1366, 768), new SnapshotSize(1920, 1080), new SnapshotSize(1366, 728) }, r.Sizes);
        Assert.Equal(new[] { SnapshotWindow.Main, SnapshotWindow.Details, SnapshotWindow.PresetInfo, SnapshotWindow.Instruments },
            r.Windows);
    }

    [Theory]
    [InlineData("--snapshot")]                                 // no directory
    [InlineData("--snapshot", "--preset", "sn1987a")]          // the directory is missing
    [InlineData("--snapshot", "out", "--time")]                // no value
    [InlineData("--snapshot", "out", "--time", "soon")]
    [InlineData("--snapshot", "out", "--time", "-5")]
    [InlineData("--snapshot", "out", "--size", "1366")]
    [InlineData("--snapshot", "out", "--size", "0x768")]
    [InlineData("--snapshot", "out", "--window", "everything")]
    [InlineData("--snapshot", "out", "--colour", "red")]       // unknown option
    public void BadArguments_Throw(params string[] args)
        => Assert.Throws<ArgumentException>(() => SnapshotRequest.Parse(args));

    [Fact]
    public void Bench_AndInstrumentTabs_AreRead()
    {
        var r = SnapshotRequest.Parse(new[] { "--snapshot", "out", "--window", "instruments,instrument-tabs,main-advanced", "--bench", "12" })!;

        Assert.Equal(new[] { SnapshotWindow.Instruments, SnapshotWindow.InstrumentTabs, SnapshotWindow.MainAdvanced }, r.Windows);
        Assert.Equal(12, r.BenchSeconds);
        Assert.Null(SnapshotRequest.Parse(new[] { "--snapshot", "out" })!.BenchSeconds);
    }

    [Fact]
    public void Quality_IsReadAsOneOrSeveralLevels_AndDefaultsToTheAppDefault()
    {
        Assert.Equal(new[] { SupernovaLab.Rendering.QualityLevel.Medium },
            SnapshotRequest.Parse(new[] { "--snapshot", "out", "--quality", "medium" })!.Qualities);
        Assert.Equal(new[] { SupernovaLab.Rendering.QualityLevel.High, SupernovaLab.Rendering.QualityLevel.Low },
            SnapshotRequest.Parse(new[] { "--snapshot", "out", "--quality", "High,low" })!.Qualities);
        Assert.Equal(new[] { SupernovaLab.Rendering.QualityLevels.Default },
            SnapshotRequest.Parse(new[] { "--snapshot", "out" })!.Qualities);
        Assert.Throws<ArgumentException>(() => SnapshotRequest.Parse(new[] { "--snapshot", "out", "--quality", "ultra" }));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("forever")]
    [InlineData("601")]
    public void Bench_NeedsAPositiveNumberOfSeconds(string value)
        => Assert.Throws<ArgumentException>(() => SnapshotRequest.Parse(new[] { "--snapshot", "out", "--bench", value }));

    [Fact]
    public void Sweep_IsReadAsAStepCount()
    {
        Assert.Equal(54, SnapshotRequest.Parse(new[] { "--snapshot", "out", "--sweep", "54", "--bench", "10" })!.SweepSteps);
        Assert.Null(SnapshotRequest.Parse(new[] { "--snapshot", "out" })!.SweepSteps);
        Assert.Equal(SnapshotRequest.MaxSweepSteps,
            SnapshotRequest.Parse(new[] { "--snapshot", "out", "--sweep", SnapshotRequest.MaxSweepSteps.ToString() })!.SweepSteps);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("2.5")]
    [InlineData("many")]
    [InlineData("2001")]
    public void Sweep_NeedsAStepCountFromTwoToTheMaximum(string value)
        => Assert.Throws<ArgumentException>(() => SnapshotRequest.Parse(new[] { "--snapshot", "out", "--sweep", value }));

    [Fact]
    public void APreset_MayCarryItsOwnTime()
    {
        var r = SnapshotRequest.Parse(new[] { "--snapshot", "out", "--preset", "sn1987a,sn1054@2.9e10", "--time", "1e7" })!;

        Assert.Equal(new[] { "sn1987a", "sn1054@2.9e10" }, r.PresetIds);
        Assert.Equal(("sn1987a", (double?)null), SnapshotRequest.SplitPreset(r.PresetIds[0]));
        Assert.Equal(("sn1054", (double?)2.9e10), SnapshotRequest.SplitPreset(r.PresetIds[1]));
        Assert.Throws<ArgumentException>(() => SnapshotRequest.Parse(new[] { "--snapshot", "out", "--preset", "sn1054@soon" }));
    }

    [Fact]
    public void APresetsOwnTime_GoesInTheFileName_SoOnePresetCanBeTakenAtSeveralTimes()
    {
        Assert.Equal("", SnapshotRequest.FileSuffix(null));
        Assert.Equal("-sn1987a", SnapshotRequest.FileSuffix("sn1987a"));
        Assert.Equal("-sn1987a-t86400", SnapshotRequest.FileSuffix("sn1987a@86400"));
        Assert.Equal("-sn1987a-t0.001", SnapshotRequest.FileSuffix("sn1987a@1e-3"));
    }

    [Fact]
    public void APreset_MayTurnOnTheDenseGasInput()
    {
        // Task 16 fix round 1: no preset has the lab's dense-CSM input, so a snapshot can turn it on per entry.
        var r = SnapshotRequest.Parse(new[] { "--snapshot", "out", "--preset", "sn2023ixf@0.001+densecsm,sn1998bw" })!;
        Assert.Equal(("sn2023ixf", (double?)0.001), SnapshotRequest.SplitPreset(r.PresetIds[0]));
        Assert.True(SnapshotRequest.WantsDenseCsm(r.PresetIds[0]));
        Assert.False(SnapshotRequest.WantsDenseCsm(r.PresetIds[1]));
        Assert.Equal("-sn2023ixf-t0.001-densecsm", SnapshotRequest.FileSuffix(r.PresetIds[0]));
        Assert.Throws<ArgumentException>(() => SnapshotRequest.Parse(new[] { "--snapshot", "out", "--preset", "sn1054+dense" }));
    }

    [Fact]
    public void SnapshotRequest_ParsesCutaway()
    {
        // Task 7: --cutaway is a flag (no value); it can sit anywhere among the options.
        Assert.False(SnapshotRequest.Parse(new[] { "--snapshot", "out" })!.Cutaway);
        Assert.True(SnapshotRequest.Parse(new[] { "--snapshot", "out", "--cutaway" })!.Cutaway);
        var r = SnapshotRequest.Parse(new[] { "--snapshot", "out", "--cutaway", "--preset", "sn1987a@3e7", "--size", "1366x768" })!;
        Assert.True(r.Cutaway);
        Assert.Equal(new[] { "sn1987a@3e7" }, r.PresetIds);
        Assert.Equal(new[] { new SnapshotSize(1366, 768) }, r.Sizes);
    }

    [Fact]
    public void APresetEntry_MayAskForTheTour_SoPlainAndTourCapturesShareOneLaunch()
    {
        // Final fix wave: with a "+tour" entry, --tour's stops are captured for that entry only; the others are plain captures.
        var r = SnapshotRequest.Parse(new[] { "--snapshot", "out", "--preset", "default@2592000,sn1987a+tour,casa@1e10+cutaway+tour",
            "--tour", "0,last" })!;
        Assert.False(r.ToursEntry(r.PresetIds[0]));
        Assert.True(r.ToursEntry(r.PresetIds[1]));
        Assert.True(r.ToursEntry(r.PresetIds[2]));
        Assert.Equal(("sn1987a", (double?)null), SnapshotRequest.SplitPreset(r.PresetIds[1]));
        Assert.Equal("-sn1987a", SnapshotRequest.FileSuffix(r.PresetIds[1]));
        Assert.True(SnapshotRequest.WantsCutaway(r.PresetIds[2]));
        Assert.Equal(("casa", (double?)1e10), SnapshotRequest.SplitPreset(r.PresetIds[2]));
        // Without any "+tour" entry, --tour applies to every entry (as before); without --tour, to none.
        var all = SnapshotRequest.Parse(new[] { "--snapshot", "out", "--preset", "default,sn1987a", "--tour", "1" })!;
        Assert.True(all.ToursEntry(all.PresetIds[0]) && all.ToursEntry(all.PresetIds[1]));
        var none = SnapshotRequest.Parse(new[] { "--snapshot", "out", "--preset", "sn1987a+tour" })!;
        Assert.False(none.ToursEntry(none.PresetIds[0]));
    }

    [Fact]
    public void APresetEntry_MayTurnOnTheCutaway()
    {
        // Task 8: "+cutaway" turns the wedge on for that entry only, so plain and cutaway views share one launch.
        var r = SnapshotRequest.Parse(new[] { "--snapshot", "out", "--preset",
            "default@946080000,default@946080000+cutaway,casa@1e10+densecsm+cutaway" })!;
        Assert.False(SnapshotRequest.WantsCutaway(r.PresetIds[0]));
        Assert.True(SnapshotRequest.WantsCutaway(r.PresetIds[1]));
        Assert.False(SnapshotRequest.WantsDenseCsm(r.PresetIds[1]));
        Assert.Equal(((string?)null, (double?)946080000), SnapshotRequest.SplitPreset(r.PresetIds[1]));
        Assert.Equal("-default-t946080000-cutaway", SnapshotRequest.FileSuffix(r.PresetIds[1]));
        Assert.True(SnapshotRequest.WantsCutaway(r.PresetIds[2]));
        Assert.True(SnapshotRequest.WantsDenseCsm(r.PresetIds[2]));
        Assert.Equal(("casa", (double?)1e10), SnapshotRequest.SplitPreset(r.PresetIds[2]));
        Assert.Equal("-casa-t10000000000-densecsm-cutaway", SnapshotRequest.FileSuffix(r.PresetIds[2]));
        Assert.Throws<ArgumentException>(() => SnapshotRequest.Parse(new[] { "--snapshot", "out", "--preset", "casa+cut" }));
    }

    [Fact]
    public void TheDefaultLabOutcome_CanCarryPerEntryTimes()
    {
        // R-P2-20: "default@seconds" takes the lab as launched (no preset) at its own time.
        var r = SnapshotRequest.Parse(new[] { "--snapshot", "out", "--preset", "default@1000,default@2.5e3,sn1987a@8.64e6,default" })!;
        Assert.Equal(("default", (double?)null), (r.PresetIds[3], (double?)null));
        Assert.Equal(((string?)null, (double?)1000), SnapshotRequest.SplitPreset(r.PresetIds[0]));
        Assert.Equal(((string?)null, (double?)2500), SnapshotRequest.SplitPreset(r.PresetIds[1]));
        Assert.Equal(("sn1987a", (double?)8.64e6), SnapshotRequest.SplitPreset(r.PresetIds[2]));
        Assert.Equal(((string?)null, (double?)null), SnapshotRequest.SplitPreset(r.PresetIds[3]));
        Assert.Equal("-default-t1000", SnapshotRequest.FileSuffix(r.PresetIds[0]));
        Assert.Equal("-default-t2500", SnapshotRequest.FileSuffix(r.PresetIds[1]));
        Assert.Equal("", SnapshotRequest.FileSuffix("default"));
        Assert.Throws<ArgumentException>(() => SnapshotRequest.Parse(new[] { "--snapshot", "out", "--preset", "default@soon" }));
    }

    [Fact]
    public void SnapshotRequest_ParsesTour()
    {
        var r = SnapshotRequest.Parse(new[] { "--snapshot", "out", "--preset", "sn1987a", "--tour", "0,3, last", "--tour", "5" })!;
        Assert.Equal(new[] { 0, 3, SnapshotRequest.LastStop, 5 }, r.TourStops);
        Assert.Empty(SnapshotRequest.Parse(new[] { "--snapshot", "out" })!.TourStops);
        Assert.Equal("-tour3", SnapshotRequest.TourSuffix(3));
        Assert.Equal("-tourlast", SnapshotRequest.TourSuffix(SnapshotRequest.LastStop));
    }

    [Theory]
    [InlineData("--tour", "soon")]
    [InlineData("--tour", "-1")]
    [InlineData("--tour", "")]
    public void BadTourStops_Throw(params string[] tour)
        => Assert.Throws<ArgumentException>(() => SnapshotRequest.Parse(new[] { "--snapshot", "out" }.Concat(tour).ToArray()));

    [Fact]
    public void BenchTour_IsAFlag_ThatMakesTheBenchPlayTheTour()
    {
        var r = SnapshotRequest.Parse(new[] { "--snapshot", "out", "--bench", "20", "--bench-tour", "--preset", "sn1987a" })!;
        Assert.True(r.BenchTour);
        Assert.Equal(20, r.BenchSeconds);
        Assert.False(SnapshotRequest.Parse(new[] { "--snapshot", "out", "--bench", "20" })!.BenchTour);
    }

    // ---- Project 3, Task 4: the Close-up ----

    [Fact]
    public void Snapshot_ParsesCloseUpOptions()
    {
        var r = SnapshotRequest.Parse(new[] { "--snapshot", "out", "--closeup", "--window", "closeup" })!;
        Assert.True(r.OpenCloseUp); Assert.Contains(SnapshotWindow.CloseUp, r.Windows);
    }

    [Fact]
    public void Snapshot_CloseUpWindowAlone_OpensIt_AndTheBenchCommandParses()
    {
        Assert.True(SnapshotRequest.Parse(new[] { "--snapshot", "out", "--window", "main,closeup" })!.OpenCloseUp);
        Assert.False(SnapshotRequest.Parse(new[] { "--snapshot", "out" })!.OpenCloseUp);
        var bench = SnapshotRequest.Parse(new[] { "--snapshot", @"out\p3t4", "--preset", "n6946bh1,sn1998bw", "--closeup", "--window",
            "main,closeup", "--size", "1366x768,1920x1080", "--bench", "20", "--bench-spin", "0.9" })!;
        Assert.True(bench.OpenCloseUp);
        Assert.Equal(0.9, bench.BenchSpin);
        Assert.Equal(20, bench.BenchSeconds);
        Assert.Equal(new[] { SnapshotWindow.Main, SnapshotWindow.CloseUp }, bench.Windows);
        Assert.Equal(new[] { "n6946bh1", "sn1998bw" }, bench.PresetIds);
        Assert.Null(SnapshotRequest.Parse(new[] { "--snapshot", "out" })!.BenchSpin);
    }

    /// <summary>
    /// Plan Task 11: <c>--lab-mass</c> sets the lab's initial mass (M☉) before the captures, so a "default@t" entry can show an outcome no
    /// preset has (the fallback black hole, 27.4 M☉ at the default inputs); within the lab's range only.
    /// </summary>
    [Fact]
    public void LabMass_SetsTheLabsInitialMass_ForDefaultEntries()
    {
        var r = SnapshotRequest.Parse(new[] { "--snapshot", "out", "--preset", "default@10", "--lab-mass", "27.4", "--window", "closeup" })!;
        Assert.Equal(27.4, r.LabMass);
        Assert.Null(SnapshotRequest.Parse(new[] { "--snapshot", "out" })!.LabMass);
        Assert.Throws<ArgumentException>(() => SnapshotRequest.Parse(new[] { "--snapshot", "out", "--lab-mass", "heavy" }));
        Assert.Throws<ArgumentException>(() => SnapshotRequest.Parse(new[] { "--snapshot", "out", "--lab-mass", "100000" }));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("-0.1")]
    [InlineData("fast")]
    public void BadBenchSpin_Throws(string spin)
        => Assert.Throws<ArgumentException>(() => SnapshotRequest.Parse(new[] { "--snapshot", "out", "--bench-spin", spin }));

    /// <summary>Review fix 1 and Task 4b: <c>--bench-baseline</c> runs the main view alone (the Close-up and the Instruments hidden), the Close-up
    /// hidden, with its lensing off, and at 1/4 (one launch).</summary>
    [Fact]
    public void BenchBaseline_IsAFlag_ThatOpensTheCloseUp_AndRunsTheBaseline()
    {
        var r = SnapshotRequest.Parse(new[] { "--snapshot", "out", "--bench", "20", "--bench-baseline" })!;
        Assert.True(r.BenchBaseline);
        Assert.True(r.OpenCloseUp);
        Assert.Equal(new[] { CloseUpBenchRun.MainOnly, CloseUpBenchRun.Hidden, CloseUpBenchRun.LensingOff, CloseUpBenchRun.Quarter },
            r.CloseUpBenchRuns);
        var plain = SnapshotRequest.Parse(new[] { "--snapshot", "out", "--bench", "20", "--closeup" })!;
        Assert.False(plain.BenchBaseline);
        Assert.Equal(new[] { CloseUpBenchRun.Governed, CloseUpBenchRun.Full, CloseUpBenchRun.Half, CloseUpBenchRun.Quarter },
            plain.CloseUpBenchRuns);
    }

    /// <summary>
    /// Task 4b: <c>--bench-detail</c> and <c>--bench-camera</c> run the Close-up at each light-bending detail with each camera (still
    /// and drawn on demand as the app does, still but drawn every frame as Task 6's beams will make it, or orbiting), between two
    /// hidden-Close-up markers (a drift check over the launch).
    /// </summary>
    [Fact]
    public void BenchDetailAndCamera_GiveTheMatrix_BetweenTwoMarkers()
    {
        var r = SnapshotRequest.Parse(new[]
        {
            "--snapshot", "out", "--bench", "20", "--bench-detail", "auto,full", "--bench-camera", "still,orbit,still-forced",
        })!;
        Assert.True(r.OpenCloseUp);
        Assert.Equal(new[] { LensingDetail.Auto, LensingDetail.Full }, r.BenchDetails);
        Assert.Equal(new[] { CloseUpBenchCamera.Still, CloseUpBenchCamera.Orbit, CloseUpBenchCamera.StillForced }, r.BenchCameras);
        var cases = r.CloseUpBenchCases;
        Assert.Equal(2 + 2 * 3, cases.Count);
        Assert.Equal(CloseUpBenchRun.Hidden, cases[0].Run);
        Assert.Equal(CloseUpBenchRun.Hidden, cases[^1].Run);
        Assert.Equal(new CloseUpBenchCase(CloseUpBenchRun.Detail, LensingDetail.Auto, CloseUpBenchCamera.Still), cases[1]);
        Assert.Equal(new CloseUpBenchCase(CloseUpBenchRun.Detail, LensingDetail.Full, CloseUpBenchCamera.StillForced), cases[6]);

        // Without them, the cases are the old runs, drawn every frame with a still camera.
        var old = SnapshotRequest.Parse(new[] { "--snapshot", "out", "--bench", "20", "--closeup" })!;
        Assert.Equal(old.CloseUpBenchRuns, old.CloseUpBenchCases.Select(c => c.Run));
        Assert.All(old.CloseUpBenchCases, c => Assert.Equal(CloseUpBenchCamera.StillForced, c.Camera));
        Assert.Equal(LensingDetail.Half, old.CloseUpBenchCases[2].Detail);
    }

    [Theory]
    [InlineData("--bench-detail", "ultra")]
    [InlineData("--bench-camera", "spin")]
    public void BadBenchDetailOrCamera_Throws(string option, string value)
        => Assert.Throws<ArgumentException>(() => SnapshotRequest.Parse(new[] { "--snapshot", "out", option, value }));

    /// <summary>Plan Task 10: "+grid" turns the Close-up's grid on for one entry; it is stripped from the id and named in the file.</summary>
    [Fact]
    public void GridFlag_TurnsTheCloseUpGridOnForThatEntry()
    {
        var r = SnapshotRequest.Parse(new[] { "--snapshot", "out", "--preset", "n6946bh1@10+grid,sn1998bw@10", "--window", "closeup" })!;
        Assert.True(SnapshotRequest.WantsGrid(r.PresetIds[0]));
        Assert.False(SnapshotRequest.WantsGrid(r.PresetIds[1]));
        Assert.Equal(("n6946bh1", (double?)10), SnapshotRequest.SplitPreset(r.PresetIds[0]));
        Assert.Equal("-n6946bh1-t10-grid", SnapshotRequest.FileSuffix(r.PresetIds[0]));
        Assert.Equal("-sn1998bw-t10-grid-cutaway", SnapshotRequest.FileSuffix("sn1998bw@10+grid+cutaway"));
    }
}
