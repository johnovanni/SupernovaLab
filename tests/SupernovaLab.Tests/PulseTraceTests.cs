using SupernovaLab.App.Services;
using SupernovaLab.Core.Compact;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Tests;

/// <summary>Plan Task 8 (spec §4.4, §4.3, flash safety C2): the pulse trace marks pulses at the drawn rate, never over 1 a second.</summary>
public class PulseTraceTests
{
    private const double Year = PhysicalConstants.Year;
    private const double TwoPi = 2 * Math.PI;

    private static readonly double[] Crossing = { 1.5 * Math.PI };

    [Fact]
    public void Recorder_MarksAPulseEachTimeTheBeamCrossesTheArrow()
    {
        var r = new PulseTraceRecorder();
        double phase = 0;
        for (int i = 0; i <= 600; i++)               // 10 s at 60 fps, one turn per 2 s
        {
            double wall = i / 60.0;
            phase = TwoPi * wall / 2;
            r.Update(wall, phase, Crossing, active: true);
        }
        Assert.Equal(5, r.TickTimesS.Count);          // turns 0..5 cross at 1.5, 3.5, 5.5, 7.5, 9.5 s -> within the window; the first at 1.5 s is 8.5 s old
        Assert.Equal(1.5 + 2 * 4, r.TickTimesS[^1], 6);
        Assert.Equal(2, r.TickTimesS[1] - r.TickTimesS[0], 6);
    }

    [Fact]
    public void Recorder_KeepsTheLastTenSeconds_AndForgetsWhenInactive()
    {
        var r = new PulseTraceRecorder();
        for (int i = 0; i <= 60 * 30; i++)
        {
            double wall = i / 60.0;
            r.Update(wall, TwoPi * wall / 2, Crossing, true);
        }
        Assert.All(r.TickTimesS, t => Assert.InRange(t, 20 - 1e-6, 20 + 1e-6 + 10));
        Assert.True(r.TickTimesS.Count is 5 or 6);
        r.Update(30.1, 99, Crossing, active: false);
        Assert.Empty(r.TickTimesS);
    }

    [Fact]
    public void Recorder_NoCrossings_NoTicks()
    {
        var r = new PulseTraceRecorder();
        for (int i = 0; i < 300; i++) r.Update(i / 60.0, TwoPi * i / 120.0, Array.Empty<double>(), true);
        Assert.Empty(r.TickTimesS);
    }

    [Fact]
    public void Recorder_TwoBeamsCrossingEarth_TickEveryHalfTurn()
    {
        var r = new PulseTraceRecorder();
        for (int i = 0; i <= 600; i++) r.Update(i / 60.0, TwoPi * i / 120.0, new[] { 1.5 * Math.PI, 0.5 * Math.PI }, true);
        Assert.Equal(10, r.TickTimesS.Count);
        for (int i = 1; i < r.TickTimesS.Count; i++) Assert.Equal(1, r.TickTimesS[i] - r.TickTimesS[i - 1], 6);
    }

    /// <summary>
    /// Spec §4.3 and criterion (flash safety, C2): for every preset pulsar, at the times it is received, the trace marks pulses no
    /// faster than once a second, at the drawn rate, whatever the real period (the Crab's 30 a second, a magnetar's seconds).
    /// </summary>
    [Fact]
    public void Trace_PulsesAtDrawnRate_NeverOver1Hz()
    {
        int pulsarsChecked = 0;
        foreach (var preset in PresetCatalog.LoadEmbedded())
        {
            var o = TestOutcomes.Engine.Compute(preset.Inputs, preset.Pins);
            var obs = ObserverSetup.Default;
            foreach (double t in new[] { 10 * Year, 50 * Year, 300 * Year, 900 * Year })
            {
                if (PulseSchedule.Visibility(o, obs, t) != PulseVisibility.Received) continue;
                pulsarsChecked++;
                var crossings = PulseSchedule.CrossingPhasesRad(o, obs);
                double period = PulsarModel.Spin.Of(o).PeriodS(t);
                double drawn = Magnetosphere.DrawnPeriodS(period);
                double wall = 0;
                var clock = new BeamClock(() => wall);
                var rec = new PulseTraceRecorder();
                var all = new List<double>();
                for (int i = 0; i <= 60 * 40; i++)    // 40 s at 60 fps
                {
                    wall = i / 60.0;
                    int before = rec.TotalTicks;
                    rec.Update(wall, clock.PhaseRad(drawn), crossings, true);
                    if (rec.TotalTicks > before) all.AddRange(rec.TickTimesS.TakeLast(rec.TotalTicks - before));
                }
                // The marked rate is the drawn rate: crossings.Count per drawn period, so never over 1 a second.
                Assert.InRange(all.Count, 40 * crossings.Count / drawn - 1.5, 40 * crossings.Count / drawn + 1.5);
                for (int i = 1; i < all.Count; i++)
                    Assert.True(all[i] - all[i - 1] >= 1 - 1e-6, $"{preset.Id} at {t / Year} yr: pulses {all[i] - all[i - 1]} s apart");
                Assert.True(all.Count / 40.0 <= 1 + 1e-9);
            }
        }
        Assert.True(pulsarsChecked >= 4, "the presets include pulsars");
    }

    [Fact]
    public void Trace_AMagnetarAtRealSpeed_TicksOncePerItsPeriod()
    {
        double wall = 0;
        var clock = new BeamClock(() => wall);
        var rec = new PulseTraceRecorder();
        for (int i = 0; i <= 60 * 30; i++)
        {
            wall = i / 60.0;
            rec.Update(wall, clock.PhaseRad(6.0), Crossing, true);   // one turn per 6 s
        }
        Assert.Equal(5, rec.TotalTicks);
    }
}
