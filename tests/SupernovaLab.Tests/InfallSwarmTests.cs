using SupernovaLab.Rendering;

namespace SupernovaLab.Tests;

/// <summary>
/// Task 20 (the owner: dots "that sometimes move inward and sometimes outward"). The infalling matter's dots were placed between the
/// shock and 2.2 shock radii every frame, so when the shock grew every dot was pushed outward with it. Now each dot keeps its own radius
/// in km and only falls; the shock overtakes it (it fades out there) and it comes back farther out.
/// </summary>
public class InfallSwarmTests
{
    private static double[] Radii(InfallSwarm s) => Enumerable.Range(0, s.Count).Select(s.RadiusKm).ToArray();

    [Fact]
    public void AfterAReset_TheDotsFillTheBandOutsideTheShock()
    {
        var s = new InfallSwarm(1500);
        s.Reset(200);
        Assert.All(Radii(s), r => Assert.InRange(r, 200, CoreAnimation.InfallOuterFactor * 200 + 1e-6));
        Assert.All(Enumerable.Range(0, s.Count), i => Assert.Equal(1f, s.Alpha(i)));
    }

    [Fact]
    public void WhileTheShockGrows_NoDotEverMovesOutward()
    {
        var s = new InfallSwarm(1500);
        double shock = 150;
        s.Reset(shock);
        var before = Radii(s);
        for (int f = 0; f < 600; f++)   // ten seconds of frames while the shock grows tenfold
        {
            shock *= Math.Pow(10, 1.0 / 600);
            s.Step(1 / 60.0, shock, infallKmS: 30000, slowdown: 1000);
            var now = Radii(s);
            for (int i = 0; i < s.Count; i++)
                if (!s.Respawned(i)) Assert.True(now[i] <= before[i] + 1e-9, $"dot {i} moved out at frame {f}");
            before = now;
        }
    }

    [Fact]
    public void ADotTheShockReaches_FadesOutThere_ThenComesBackFartherOut()
    {
        var s = new InfallSwarm(200);
        s.Reset(100);
        s.Step(1 / 60.0, 300, infallKmS: 30000, slowdown: 1000);   // the shock jumps past most dots
        int caught = Enumerable.Range(0, s.Count).First(i => s.IsFadingOut(i));
        Assert.True(s.Alpha(caught) < 1f);
        for (int f = 0; f < 60; f++) s.Step(1 / 60.0, 300, 30000, 1000);   // a second: the fade (0.5 s) is over
        Assert.False(s.IsFadingOut(caught));
        Assert.InRange(s.RadiusKm(caught), 300, CoreAnimation.InfallOuterFactor * 300 + 1e-6);
        Assert.True(s.Alpha(caught) > 0f);
    }

    [Fact]
    public void ADotFallsAtTheFreeFallSpeed_SlowedDown()
    {
        var s = new InfallSwarm(50);
        s.Reset(1000);
        int i = Enumerable.Range(0, s.Count).First(k => s.RadiusKm(k) > 1500);
        double r0 = s.RadiusKm(i);
        s.Step(0.01, 1000, infallKmS: 20000, slowdown: 100);
        double expected = 20000 * Math.Sqrt(1000 / r0) * 0.01 / 100;   // v = v_sh √(R_sh / r), in real time = wall / slowdown
        Assert.Equal(expected, r0 - s.RadiusKm(i), 6);
    }

    [Fact]
    public void NoDotsBrightnessChanges_FasterThanItsHalfSecondFade()
    {
        var s = new InfallSwarm(300);
        s.Reset(100);
        var prev = Enumerable.Range(0, s.Count).Select(s.Alpha).ToArray();
        double shock = 100;
        for (int f = 0; f < 300; f++)
        {
            shock *= 1.01;
            s.Step(1 / 60.0, shock, 30000, 1000);
            for (int i = 0; i < s.Count; i++)
            {
                Assert.InRange(Math.Abs(s.Alpha(i) - prev[i]), 0, (1 / 60.0) / InfallSwarm.FadeS + 1e-6);
                prev[i] = s.Alpha(i);
            }
        }
    }

    [Fact]
    public void Vertices_CarryPositionAndAlpha()
    {
        var s = new InfallSwarm(10);
        s.Reset(100);
        var v = s.Vertices();
        Assert.Equal(40, v.Length);
        var d = new System.Numerics.Vector3(v[0], v[1], v[2]);
        Assert.Equal(s.RadiusKm(0), d.Length(), 2);
        Assert.Equal(s.Alpha(0), v[3]);
    }
}
