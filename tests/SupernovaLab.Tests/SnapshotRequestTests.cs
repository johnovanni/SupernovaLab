using SupernovaLab.App.Services;

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

    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("forever")]
    [InlineData("601")]
    public void Bench_NeedsAPositiveNumberOfSeconds(string value)
        => Assert.Throws<ArgumentException>(() => SnapshotRequest.Parse(new[] { "--snapshot", "out", "--bench", value }));

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
}
