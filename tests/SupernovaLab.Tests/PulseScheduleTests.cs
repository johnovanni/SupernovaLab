using SupernovaLab.Core.Compact;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Tests;

/// <summary>Plan Task 8: when the pulses arrive, at what period, and the trace's words (spec §4.4).</summary>
public class PulseScheduleTests
{
    private const double Year = PhysicalConstants.Year;
    private const double CrabAgeS = 2.9568e10;

    private static Outcome Of(string id)
    {
        var p = PresetCatalog.Get(id);
        return TestOutcomes.Engine.Compute(p.Inputs, p.Pins);
    }

    private static readonly Outcome Crab = Of("sn1054");
    private static readonly ObserverSetup CrabObs = ObserverSetup.Default;

    private static ObserverSetup Obs(bool beamTowardEarth = true, double distancePc = 10_000)
        => ObserverSetup.Default with { BeamTowardEarth = beamTowardEarth, DistancePc = distancePc };

    [Fact]
    public void HiddenUntilEmergence_ThenReceived()
    {
        Assert.Equal(PulseVisibility.HiddenByDebris, PulseSchedule.Visibility(Crab, CrabObs, PulsarModel.EmergenceYears * Year * 0.9));
        Assert.Equal(PulseVisibility.Received, PulseSchedule.Visibility(Crab, CrabObs, CrabAgeS));
        Assert.Equal(PulseVisibility.Received, PulseSchedule.Visibility(Crab, CrabObs, PulsarModel.EmergenceYears * Year));
    }

    [Fact]
    public void BeamOff_Misses() => Assert.Equal(PulseVisibility.BeamMissesEarth, PulseSchedule.Visibility(Crab, Obs(false), CrabAgeS));

    [Fact]
    public void BeamOff_WinsOverDebris_AsTheRadioPanelDoes()
        => Assert.Equal(PulseVisibility.BeamMissesEarth, PulseSchedule.Visibility(Crab, Obs(false), 5 * Year));

    [Fact]
    public void NoPulsar_IsNotAPulsar()
    {
        var bh = TestOutcomes.Collapsar();
        Assert.Equal(PulseVisibility.NotAPulsar, PulseSchedule.Visibility(bh, CrabObs, CrabAgeS));
        Assert.True(double.IsNaN(PulseSchedule.ReceivedPeriodS(bh, CrabObs, CrabAgeS)));
        Assert.Empty(PulseSchedule.CrossingPhasesRad(bh, CrabObs));
    }

    [Fact]
    public void AgreesWithTheRadioPanel_AndMagnetosphere_OnEveryPreset()
    {
        var model = new PulsarModel();
        foreach (var preset in PresetCatalog.LoadEmbedded())
        {
            var o = Of(preset.Id);
            foreach (bool toward in new[] { true, false })
            foreach (double t in new[] { 1.0, 5 * Year, 10 * Year, 100 * Year })
            {
                var obs = Obs(toward);
                var v = PulseSchedule.Visibility(o, obs, t);
                double emergence = model.EmergenceTimeS(o);
                if (double.IsNaN(emergence)) { Assert.Equal(PulseVisibility.NotAPulsar, v); continue; }
                Assert.Equal(Magnetosphere.BeamCanCrossEarth(o, obs), v != PulseVisibility.BeamMissesEarth);
                if (v == PulseVisibility.BeamMissesEarth) continue;
                Assert.Equal(t >= emergence ? PulseVisibility.Received : PulseVisibility.HiddenByDebris, v);
                // the radio panel: NotYet iff the pulses are still hidden
                var d = model.Detect(o, obs, t, Detectors.Get("fast"));
                Assert.Equal(v == PulseVisibility.HiddenByDebris, d.Status == DetectionStatus.NotYet);
            }
        }
    }

    [Fact]
    public void Redshift_StretchesReceivedPeriod()       // Review Focus 2
    {
        var far = Obs(distancePc: 4e8);
        Assert.True(far.TimeDilation > 1.05);
        double p = PulsarModel.Spin.Of(Crab).PeriodS(CrabAgeS);
        Assert.Equal(0.0334, p, 4);
        Assert.Equal(p * far.TimeDilation, PulseSchedule.ReceivedPeriodS(Crab, far, CrabAgeS), 12);
        Assert.Equal(p, PulseSchedule.ReceivedPeriodS(Crab, CrabObs, CrabAgeS), 12);
    }

    [Fact]
    public void TraceText_NamesBothRates_OrTheReason()
    {
        Assert.Equal("Sound: real rate, 30 a second · Picture: shown 60× slower", PulseSchedule.TraceText(Crab, CrabObs, CrabAgeS));
        Assert.Equal(PulseSchedule.HiddenText, PulseSchedule.TraceText(Crab, CrabObs, 5 * Year));
        Assert.Equal("To Earth: hidden by debris", PulseSchedule.HiddenText);
        Assert.Equal("Beam misses Earth", PulseSchedule.TraceText(Crab, Obs(false), CrabAgeS));
    }

    [Theory]
    [InlineData(0.0334, "30 a second")]
    [InlineData(1.0, "1 a second")]
    [InlineData(0.001, "1,000 a second")]
    [InlineData(3.4, "one every 3.4 s")]
    [InlineData(5, "one every 5 s")]
    public void RateText(double periodS, string expected) => Assert.Equal(expected, PulseSchedule.RateText(periodS));

    [Fact]
    public void CrossingPhases_MainBeamAlways_OppositeOnlyForAnInterpulse()
    {
        Assert.Equal(new[] { 1.5 * Math.PI }, PulseSchedule.CrossingPhasesRad(Crab, CrabObs));
        Assert.Empty(PulseSchedule.CrossingPhasesRad(Crab, Obs(false)));
    }
}

public class PulseScheduleFixTests
{
    [Theory]
    [InlineData(1 / 1.5, "1.5 a second")]
    [InlineData(1 / 2.5, "2.5 a second")]
    [InlineData(1 / 7.26, "7.3 a second")]
    [InlineData(1 / 30.0, "30 a second")]
    [InlineData(5, "one every 5 s")]
    [InlineData(1 / 0.2, "one every 5 s")]
    [InlineData(1, "1 a second")]
    public void RateText_KeepsADecimalUnderTen(double periodS, string expected) => Assert.Equal(expected, PulseSchedule.RateText(periodS));

    [Fact]
    public void RateText_PointTwoARate_IsOneEveryFive() => Assert.Equal("one every 5 s", PulseSchedule.RateText(5));

    [Fact]
    public void TraceText_PictureFactorIsDrawnOverReceived()
    {
        var p = PresetCatalog.Get("sn1054");
        var crab = TestOutcomes.Engine.Compute(p.Inputs, p.Pins);
        var far = ObserverSetup.Default with { DistancePc = 4e8 };
        double period = PulsarModel.Spin.Of(crab).PeriodS(2.9568e10);
        string expected = Magnetosphere.SlowdownLabel(period, far.TimeDilation);
        Assert.EndsWith("Picture: " + char.ToLowerInvariant(expected[0]) + expected[1..], PulseSchedule.TraceText(crab, far, 2.9568e10));
    }
}
