using SupernovaLab.App.Services;
using SupernovaLab.Core.Compact;
using static SupernovaLab.Tests.CompactResearch;

namespace SupernovaLab.Tests;

/// <summary>Plan Task 12 (Review Focus 5): <see cref="FlashLimiter"/> keeps a giant flare to one rise per half second of wall time.</summary>
public class FlashLimiterTests
{
    // ---- The flash limiter (Review Focus 5) ----

    [Fact]
    public void FlashLimiter_RapidScrubbing_AtMostTwoRisesPerSecond()
    {
        var f = new GiantFlare(1e8, 2e46, 2e46 / C6.SpikeS, C6.SpikeS);
        var lim = new FlashLimiter();
        var outputs = new List<double>();
        // Scrubbing back and forth across the flare ten times a second for three seconds, drawn at 60 frames a second.
        for (int i = 0; i < 180; i++)
        {
            double wall = i / 60.0;
            double phase = Math.Floor(wall * 10) % 2;               // 10 Hz square-wave scrub: at the peak, then far from the flare
            double t = phase == 0 ? f.SourceTimeS : f.SourceTimeS * 100;
            outputs.Add(lim.Apply(MagnetarFlares.DrawnIntensity(f, t), wall));
        }
        Assert.All(outputs, v => Assert.InRange(v, 0, 1));
        Assert.True(CountRises(outputs) <= 6, $"{CountRises(outputs)} rises in 3 s");
        Assert.True(CountRises(outputs) >= 1);
    }

    [Fact]
    public void FlashLimiter_RapidTriangleScrubbing_AtMostTwoRisesPerSecond()
    {
        var f = new GiantFlare(1e8, 2e46, 2e46 / C6.SpikeS, C6.SpikeS);
        var lim = new FlashLimiter();
        var outputs = new List<double>();
        for (int i = 0; i < 600; i++)                                // 200 Hz sampling, a 8 Hz triangle sweep over the flare
        {
            double wall = i / 200.0;
            double x = Math.Abs(((wall * 8) % 2) - 1) * 0.5 - 0.2;   // −0.2..0.3 decades about the flare
            outputs.Add(lim.Apply(MagnetarFlares.DrawnIntensity(f, f.SourceTimeS * Math.Pow(10, x)), wall));
        }
        Assert.True(CountRises(outputs) <= 6, $"{CountRises(outputs)} rises in 3 s");
    }

    [Fact]
    public void FlashLimiter_AFlareAtNormalSpeed_PassesWholeAndSmooth()
    {
        var f = new GiantFlare(1e8, 2e46, 2e46 / C6.SpikeS, C6.SpikeS);
        var lim = new FlashLimiter();
        var outputs = new List<double>();
        // Playback at the default speed: the flare crosses in about 0.5 s of wall time; 60 frames a second.
        for (int i = 0; i < 90; i++)
        {
            double wall = i / 60.0;
            double decades = (wall - 0.3) * MagnetarFlares.ReferenceDecadesPerSecond;
            outputs.Add(lim.Apply(MagnetarFlares.DrawnIntensity(f, f.SourceTimeS * Math.Pow(10, decades)), wall));
        }
        Assert.Equal(1, CountRises(outputs));
        Assert.True(outputs.Max() > 0.9);
        Assert.True(outputs.Max() <= 1);
    }

    [Fact]
    public void FlashLimiter_ASuddenJump_ClimbsOverAtLeastAFifthOfASecond()
    {
        var lim = new FlashLimiter();
        double first = lim.Apply(0, 0);
        Assert.Equal(0, first);
        double t = 1;
        double v = lim.Apply(1, t);          // jumps to the peak in one frame
        Assert.True(v < 1);
        int frames = 1;
        while (v < 1 && frames < 100) { t += 1.0 / 60; v = lim.Apply(1, t); frames++; }
        Assert.True(frames / 60.0 >= FlashLimiter.MinRiseS - 1e-9, $"{frames} frames");
    }

    [Fact]
    public void FlashLimiter_NonFiniteAndOutOfRangeInputsAreSafe()
    {
        var lim = new FlashLimiter();
        Assert.Equal(0, lim.Apply(double.NaN, 0));
        Assert.Equal(0, lim.Apply(-3, 0.1));
        Assert.InRange(lim.Apply(50, 1), 0, 1);
    }

    [Fact]
    public void Flare_PreGammaToneMappedPeak_StaysUnderTheAreaLimit_AndTheDisplayedRiseOverTheBaselineIsSmall()
    {
        float level = SupernovaLab.App.Controllers.CloseUpSceneController.SurfaceLevel;
        float Lum(float f) => SupernovaLab.Rendering.HdrPipeline.Tonemap(level * (1 + SupernovaLab.App.Controllers.CloseUpSceneController.FlareSurfaceGain * f));
        // Lum is the ACES value BEFORE tonemap.frag's gamma 2.2 (linear light); what the screen shows is Lum^(1/2.2).
        Assert.True(Lum(1) <= 0.85f, $"pre-gamma peak {Lum(1)}");
        float Shown(float f) => MathF.Pow(Lum(f), 1f / 2.2f);
        Assert.InRange(Shown(0), 0.85f, 0.9f);                  // the baseline star is shown at about 0.88
        Assert.True(Shown(1) - Shown(0) <= 0.1f, $"the flare raises the displayed luminance by {Shown(1) - Shown(0)}");
        float beam = SupernovaLab.App.Controllers.CloseUpSceneController.BeamLevel;
        Assert.True(SupernovaLab.Rendering.HdrPipeline.Tonemap(beam * (1 + SupernovaLab.App.Controllers.CloseUpSceneController.FlareBeamGain)) <= 0.85f);
        // A request that jumps to full: the largest per-frame luminance step over the 0.15 s rise at 60 frames a second.
        var lim = new FlashLimiter();
        lim.Apply(0, 0);
        float prev = Shown(0), worst = 0;
        for (int i = 1; i <= 12; i++)
        {
            float v = Shown((float)lim.Apply(1, i / 60.0));
            worst = Math.Max(worst, Math.Abs(v - prev));
            prev = v;
        }
        Assert.True(worst <= 0.02f, $"displayed step {worst}");
        // The bloom is thinned during a flare, never raised.
        Assert.Equal(0.5f, SupernovaLab.App.Controllers.CloseUpSceneController.BloomDuringFlare(0.5f, 0));
        Assert.Equal(0.25f, SupernovaLab.App.Controllers.CloseUpSceneController.BloomDuringFlare(0.5f, 1));
    }

    [Fact]
    public void FlashLimiter_FallIsSlewedToo_SoAClippedFlareNeverEndsAsAHardCut()
    {
        var lim = new FlashLimiter();
        double t = 0, v = 0;
        for (int i = 0; i < 20; i++) { t += 1.0 / 60; v = lim.Apply(1, t); }
        Assert.Equal(1, v, 6);
        double first = lim.Apply(0, t += 1.0 / 60);
        Assert.True(first > 0.8, $"{first}");                 // not a cut to 0
        int frames = 1;
        while (v > 0 && frames < 100) { v = lim.Apply(0, t += 1.0 / 60); frames++; }
        Assert.True(frames / 60.0 >= FlashLimiter.MinRiseS - 1e-9);
    }

    [Fact]
    public void FlashLimiter_OutcomeChangingEveryFrameDuringAFlare_NeitherCutsNorRisesFasterThanTheSpacing()
    {
        // Dragging a lab slider makes a new Outcome every frame; the limiter must keep its rise spacing and slew its fall across them.
        var lim = new FlashLimiter();
        var outputs = new List<double>();
        for (int i = 0; i < 180; i++)                           // 3 s at 60 frames a second
        {
            double wall = i / 60.0;
            lim.ResetIfChanged(new object(), 1);                // a new outcome on every frame
            // The flare is asked for in alternating tenth-of-a-second bursts, as scrubbing or a changing flare list would.
            double want = Math.Floor(wall * 10) % 2 == 0 ? 1 : 0;
            outputs.Add(lim.Apply(want, wall));
        }
        Assert.True(CountRises(outputs) <= 6, $"{CountRises(outputs)} rises in 3 s");
        double worstFall = 0;
        for (int i = 1; i < outputs.Count; i++) worstFall = Math.Max(worstFall, outputs[i - 1] - outputs[i]);
        Assert.True(worstFall <= 1.0 / 60 / FlashLimiter.MinRiseS * 1.01 + 1e-9, $"hard cut of {worstFall} in one frame");
    }

    [Fact]
    public void FlashLimiter_ANewContext_KeepsTheOutputAndTheLastRiseTime()
    {
        var lim = new FlashLimiter();
        double t = 0, v = 0;
        for (int i = 0; i < 20; i++) v = lim.Apply(1, t += 1.0 / 60);
        Assert.True(lim.ResetIfChanged(new object(), 2));       // Re-roll
        double after = lim.Apply(1, t += 1.0 / 60);
        Assert.True(after >= v - 1e-9);                        // not zeroed
        Assert.False(lim.ResetIfChanged(lim, 2) && lim.ResetIfChanged(lim, 2));   // the same context again is not a change
    }

    [Fact]
    public void FlareDimmed_WhenTheLimiterHoldsTheFlareClearlyBelowTheRequest()
    {
        Assert.False(SupernovaLab.App.Controllers.CloseUpSceneController.FlareDimmedBy(0, 0));
        Assert.False(SupernovaLab.App.Controllers.CloseUpSceneController.FlareDimmedBy(0.9, 0.85));
        Assert.True(SupernovaLab.App.Controllers.CloseUpSceneController.FlareDimmedBy(0.9, 0.2));
        Assert.Contains("flash-safety limit", SupernovaLab.App.ViewModels.CloseUpViewModel.FlareDimmedNote);
    }

    [Theory]
    [InlineData("sn2015bn")]     // a magnetar: a lensed neutron star
    [InlineData("sn2011fe")]     // a Type Ia: no remnant, so not lensed
    public void FlareDimmedNote_IsInTheNotes_WhenClipped_LensedOrNot(string preset)
    {
        var main = new SupernovaLab.App.ViewModels.MainViewModel(TestOutcomes.Engine, new FakeWindowService());
        main.Lab.SelectedPreset = SupernovaLab.Core.Presets.PresetCatalog.Get(preset);
        main.Timeline.ObserverTimeS = 100;
        var vm = new SupernovaLab.App.ViewModels.CloseUpViewModel(main);
        Assert.DoesNotContain(SupernovaLab.App.ViewModels.CloseUpViewModel.FlareDimmedNote, vm.Notes);
        vm.FlareDimmed = true;
        Assert.Equal(SupernovaLab.App.ViewModels.CloseUpViewModel.FlareDimmedNote, vm.Notes[0]);
        vm.FlareDimmed = false;
        Assert.DoesNotContain(SupernovaLab.App.ViewModels.CloseUpViewModel.FlareDimmedNote, vm.Notes);
    }

    [Fact]
    public void MarkerGlow_StaysBelowWhite_ByTheShadersOwnBound()
    {
        // marker.frag: the halo is u_glow (at most 1, the limiter's output) x (1 - r)^2, and is drawn at 0.8 of that, so it never reaches white;
        // the marker's own core is the same white at any brightness. Pinned in the source, since no GL context is available here.
        var asm = typeof(SupernovaLab.Rendering.ShaderProgram).Assembly;
        string name = asm.GetManifestResourceNames().Single(n => n.EndsWith("marker.frag"));
        using var reader = new System.IO.StreamReader(asm.GetManifestResourceStream(name)!);
        string src = reader.ReadToEnd();
        Assert.Contains("float halo = u_glow * pow(max(1.0 - r, 0.0), 2.0);", src);
        Assert.Contains("max(core, halo * 0.8)", src);
        Assert.Contains("mix(u_color, vec3(1.0), u_glow)", src);     // the colour is at most white
        Assert.True(0.8 * 1 * 1 < 1);
    }

    /// <summary>The number of separate rises: transitions from not rising to rising by more than a hair.</summary>
    private static int CountRises(IReadOnlyList<double> v)
    {
        int rises = 0;
        bool rising = false;
        for (int i = 1; i < v.Count; i++)
        {
            double d = v[i] - v[i - 1];
            if (d > 1e-3) { if (!rising) rises++; rising = true; }
            else if (d < -1e-3) rising = false;
        }
        return rises;
    }

}
