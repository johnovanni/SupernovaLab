using SupernovaLab.App.Services;
using SupernovaLab.Core.Compact;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Rendering;

namespace SupernovaLab.Tests;

/// <summary>
/// Spec §1 flash safety and criterion 6 (WCAG 2.3.1's general flash threshold, 3 a second), over every outcome Project 3's validation
/// covers: the pulse trace marks at the drawn rate, a giant flare is one smooth event that the limiter keeps to one rise per 0.5 s however
/// the timeline is played, and the first second's animation repeats no faster than once in 2 s. The beams' rate is
/// <see cref="Project3ValidationTests.Criterion6_NoPeriodicBrightnessOver3Hz"/>.
/// </summary>
public class FlashSafetyTests
{
    private const double Year = Core.Units.PhysicalConstants.Year;

    private static IEnumerable<(string Name, Outcome Outcome, ObserverSetup Observer)> Cases()
        => Project3ValidationTests.AllPresetsAndOutcomeTypes().Select(r => ((string)r[0]!, (Outcome)r[1]!, (ObserverSetup)r[2]!));

    /// <summary>
    /// The pulse trace's marks (two beams' crossings per drawn turn), for every pulsar: never closer than 1/3 s (the 3 Hz limit), and on
    /// average no faster than the drawn rate (at most about one a second). Single gaps can be a little under 1 s (ruling R-P3-28).
    /// </summary>
    [Fact]
    public void PulseTrace_MarksAtLeastAThirdOfASecondApart_AndOnAverageAtTheDrawnRate()
    {
        int checkedCases = 0;
        foreach (var (what, o, obs) in Cases())
        {
            if (NeutronStarModel.At(o, obs, 1, 0) is null) continue;
            var crossings = PulseSchedule.CrossingPhasesRad(o, obs);
            Assert.True(crossings.Count <= 2, what);
            foreach (double t in new[] { 60.0, 1 * Year, 30 * Year, 300 * Year, 1000 * Year })
            {
                double drawn = Magnetosphere.DrawnPeriodS(NeutronStarModel.At(o, obs, 1, t)!.PeriodS);
                double wall = 0;
                var clock = new BeamClock(() => wall);
                var rec = new PulseTraceRecorder();
                double last = double.NegativeInfinity;
                int marks = 0;
                for (int i = 0; i <= 60 * 12; i++)   // 12 s at 60 fps
                {
                    wall = i / 60.0;
                    int before = rec.TotalTicks;
                    rec.Update(wall, clock.PhaseRad(drawn), crossings, true);
                    if (rec.TotalTicks == before) continue;
                    marks += rec.TotalTicks - before;
                    // The two beams' crossings need not be half a turn apart (Earth off the spin equator: the interpulse is uneven, e.g.
                    // 0.98 s and 1.02 s for a 2 s turn), so a single gap can be a little under 1 s; never near the 3 Hz limit.
                    Assert.True(wall - last >= 1.0 / 3, $"{what} at {t / Year:G3} yr: marks {wall - last:0.###} s apart");
                    last = wall;
                }
                // On average no more than the drawn rate: crossings per drawn turn, at most 2 per 2 s.
                Assert.True(marks <= 12 * crossings.Count / drawn + 1, $"{what} at {t / Year:G3} yr: {marks} marks in 12 s");
                checkedCases++;
            }
        }
        Assert.True(checkedCases > 50, $"{checkedCases}");
    }

    /// <summary>
    /// Every giant flare, for every magnetar and many runs, is one event: its drawn brightness has a single rise to a single peak and a
    /// single fade, and the rise lasts at least the limiter's 0.15 s of playback at the default speed.
    /// </summary>
    [Fact]
    public void GiantFlares_AreSingleEvents()
    {
        Assert.True(MagnetarFlares.RisePlaybackS >= FlashLimiter.MinRiseS);
        int flares = 0;
        foreach (var (what, o, _) in Cases().Where(c => MagnetarFlares.Applies(c.Outcome)))
            for (int run = 1; run <= 40; run++)
                foreach (var f in MagnetarFlares.For(o, run))
                {
                    int maxima = 0;
                    double previous = 0, slope = 0;
                    for (int k = -100; k <= 200; k++)
                    {
                        double t = f.SourceTimeS * Math.Pow(10, k * 0.001);
                        double v = MagnetarFlares.DrawnIntensity(f, t);
                        double s = v - previous;
                        if (slope > 0 && s < 0) maxima++;
                        if (s != 0) slope = s;
                        previous = v;
                    }
                    Assert.Equal(1, maxima);
                    Assert.InRange(MagnetarFlares.DrawnIntensity(f, f.SourceTimeS), 0.5, 1);
                    flares++;
                }
        Assert.True(flares > 20, $"{flares} flares");
    }

    /// <summary>
    /// Whatever the playback does (the whole timeline at the fastest speed, or a scrub back and forth across the run's flares), the
    /// limited brightness starts a new rise at most once per 0.5 s of wall time, so it can never flash 3 times a second.
    /// </summary>
    [Theory]
    [InlineData(1.0 / 3)]
    [InlineData(3.0)]
    public void GiantFlares_ThroughTheLimiter_RiseAtMostTwiceASecond(double decadesPerSecond)
    {
        foreach (var (what, o, _) in Cases().Where(c => MagnetarFlares.Applies(c.Outcome)))
            for (int run = 1; run <= 10; run++)
            {
                var flares = MagnetarFlares.For(o, run);
                if (flares.Count == 0) continue;
                var limiter = new FlashLimiter();
                double logT = Math.Log10(flares[0].SourceTimeS) - 0.5, wall = 0, previous = 0;
                bool rising = false;
                var riseStarts = new List<double>();
                double end = Math.Log10(flares[^1].SourceTimeS) + 0.5;
                int direction = 1;
                while (logT < end && wall < 600)
                {
                    wall += 1.0 / 60;
                    logT += direction * decadesPerSecond / 60;
                    if (wall % 1.3 < 1.0 / 60) direction = -direction;   // scrub back and forth too
                    double v = limiter.Apply(MagnetarFlares.IntensityAt(flares, Math.Pow(10, logT)), wall);
                    if (v > previous + 1e-9 && !rising) riseStarts.Add(wall);
                    rising = v > previous + 1e-9;
                    previous = v;
                }
                for (int i = 1; i < riseStarts.Count; i++)
                    Assert.True(riseStarts[i] - riseStarts[i - 1] >= FlashLimiter.MinRiseSpacingS - 1e-9,
                        $"{what}, run {run}: rises {riseStarts[i] - riseStarts[i - 1]:0.###} s apart");
            }
    }

    /// <summary>The first second's animation: the shock's slosh and the infall's cycle repeat no faster than once in 2 s, for every track.</summary>
    [Fact]
    public void FirstSecond_RepeatsNoFasterThanOnceIn2s()
    {
        Assert.True(CoreAnimation.SloshPeriodS >= 2);
        int states = 0;
        foreach (var (what, o, obs) in Cases())
        {
            if (CoreCollapseHistory.TrackFor(o) is null) continue;
            var scene = new CloseUpScene(o, obs, 1);
            foreach (double t in new[] { 0.0015, 0.005, 0.02, 0.05, 0.1, 0.2, 0.4, 0.7, 0.95 })
            {
                var st = scene.At(t);
                if (st.Core is not { } core) continue;
                Assert.True(CoreAnimation.InfallCycleS(core) >= CoreAnimation.MinInfallCycleS - 1e-9, $"{what} at {t} s");
                states++;
            }
        }
        Assert.True(states > 100, $"{states}");
    }
}
