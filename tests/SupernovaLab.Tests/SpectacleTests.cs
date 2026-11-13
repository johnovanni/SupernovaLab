using System.IO;
using System.Windows;
using SupernovaLab.App.Controllers;
using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Signals;
using SupernovaLab.Rendering;

namespace SupernovaLab.Tests;

/// <summary>
/// Task 11 (Project 2): the breakout flash, the star's limb darkening, the jets' glow, the quality governor and the retuned
/// exposure. Rendering rules without OpenGL; the pictures themselves are checked with --snapshot.
/// </summary>
public class SpectacleTests
{
    private static readonly ShockBreakoutModel Breakout = new();
    private static readonly LightCurveModel LightCurve = new();

    private static Outcome Default() => TestOutcomes.Engine.Compute(LabInputs.Default);

    // ---------------------------------------------------------------- the flash

    [Fact]
    public void Flash_IsZeroOutsideBreakout_AndPositiveDuring()
    {
        var o = Default();
        var scene = new ExplosionScene(o);
        double tbo = Breakout.BreakoutTimeS(o), end = Breakout.Cooling(o)?.StartS ?? Breakout.ValidRange(o).EndS;
        Assert.True(double.IsFinite(tbo) && end > tbo);

        Assert.Equal(0f, scene.At(0.5 * tbo).FlashIntensity);            // the shock is still inside the star
        Assert.Equal(0f, scene.At(tbo * 0.999999).FlashIntensity);
        Assert.True(scene.At(tbo).FlashIntensity > 0);                   // the flash
        Assert.True(scene.At(0.5 * (tbo + end)).FlashIntensity >= 0);    // the bridge to the cooling phase
        Assert.Equal(0f, scene.At(end * 1.001 + 1).FlashIntensity);      // the cooling glow and everything later
        Assert.Equal(0f, scene.At(30 * PhysicalConstantsDay).FlashIntensity);
        Assert.Equal(0f, scene.At(1e9).FlashIntensity);
    }

    private const double PhysicalConstantsDay = 86400;

    [Fact]
    public void Flash_RedSupergiant_OutshinesThePeak()
    {
        var o = Default();
        double tbo = Breakout.BreakoutTimeS(o);
        float flash = new ExplosionScene(o).At(tbo).FlashIntensity;
        Assert.True(flash > 1, $"flash {flash}");
        Assert.True(flash <= ExplosionScene.MaxFlashIntensity);
    }

    [Fact]
    public void Flash_IsOnePlusLog10OfTheBreakoutOverThePeak_Clamped()
    {
        var o = Default();
        var (start, end) = LightCurve.ValidRange(o);
        double peak = 0;
        for (int i = 0; i <= 200; i++)
            peak = Math.Max(peak, LightCurve.Sample(o, ObserverSetup.Default, start * Math.Pow(end / start, i / 200.0), "lbol"));
        double tbo = Breakout.BreakoutTimeS(o);
        double lbo = Breakout.Sample(o, ObserverSetup.Default, tbo, "lum");
        double expected = Math.Clamp(1 + Math.Log10(lbo / peak), 0, ExplosionScene.MaxFlashIntensity);
        Assert.Equal(expected, new ExplosionScene(o).At(tbo).FlashIntensity, 3);
    }

    [Fact]
    public void Flash_ColourComesFromTheBreakoutsFlashTemperature_BlueWhite()
    {
        var o = Default();
        double tbo = Breakout.BreakoutTimeS(o);
        var s = new ExplosionScene(o).At(tbo);
        var expected = BlackbodyColor.FromTemperature(Breakout.FlashTemperatureK(o));
        Assert.Equal(expected, s.FlashColor);
        Assert.True(s.FlashColor.B >= s.FlashColor.R);   // saturates to blue-white
        Assert.Contains(s.Notes, n => n == ExplosionScene.FlashNote);
        Assert.Equal("Breakout flash: brightness from the breakout model, mostly UV and X-rays in reality", ExplosionScene.FlashNote);
        Assert.DoesNotContain(new ExplosionScene(o).At(0.5 * tbo).Notes, n => n == ExplosionScene.FlashNote);
    }

    [Fact]
    public void FlashTemperatureK_IsTheModelsOwnFlashValue()
    {
        var o = Default();
        double tbo = Breakout.BreakoutTimeS(o);
        // The "temp" channel in the flash phase is the flash state's characteristic temperature (the νLν peak).
        double inFlash = Breakout.Sample(o, ObserverSetup.Default, tbo, "temp");
        Assert.True(inFlash > 0);
        Assert.Equal(inFlash, Breakout.FlashTemperatureK(o), 6);
        Assert.True(double.IsNaN(Breakout.FlashTemperatureK(TestOutcomes.TypeIa())));   // no modelled breakout
    }

    [Fact]
    public void Flash_TypeIa_NeverFlashes()
    {
        var scene = new ExplosionScene(TestOutcomes.TypeIa());
        foreach (double t in new[] { 1e-3, 1, 100, 1e4, 1e6 }) Assert.Equal(0f, scene.At(t).FlashIntensity);
    }

    [Fact]
    public void NoHdr_FlashStillShows_AsWhiteOverlay()
    {
        // Review Focus 5: without the HDR target (no RGBA16F, or no context yet) the scene still reports the flash and the
        // controller draws it as a full-screen additive quad at min(1, intensity / 8).
        var vm = new MainViewModel(TestOutcomes.Engine, new FakeWindowService());
        double tbo = Breakout.BreakoutTimeS(vm.CurrentOutcome);
        vm.Timeline.ObserverTimeS = tbo * vm.CurrentObserver.TimeDilation;
        var scene = new SceneController(vm, () => new NullHost());
        Assert.False(scene.HdrEnabled);
        Assert.True(scene.State.FlashIntensity > 0);
        Assert.Equal(Math.Min(1f, scene.State.FlashIntensity / 8f), scene.FlashOverlayAlpha, 6);
        Assert.True(scene.FlashOverlayAlpha > 0);

        Assert.Equal(0f, FlashLayer.NoHdrAlpha(0f));
        Assert.Equal(0.5f, FlashLayer.NoHdrAlpha(4f), 6);
        Assert.Equal(1f, FlashLayer.NoHdrAlpha(8f), 6);
        Assert.Equal(1f, FlashLayer.NoHdrAlpha(30f), 6);
        Assert.Equal(0f, FlashLayer.NoHdrAlpha(-1f));
        Assert.Equal(0f, FlashLayer.NoHdrAlpha(float.NaN));
        vm.Timeline.ObserverTimeS = 1e7;
        Assert.Equal(0f, scene.FlashOverlayAlpha);
    }

    private sealed class NullHost : IRenderHost
    {
        public bool Animating { get; set; }
        public event Action<string>? ContextReady { add { } remove { } }
        public event Action<TimeSpan, Size>? Draw { add { } remove { } }
        public event Action<Exception>? FrameFailed { add { } remove { } }
        public void Start() { }
        public void RequestRender() { }
        public bool Shutdown(Action release) => false;
    }

    [Fact]
    public void FlashLayer_HdrLight_GrowsWithIntensity_AndBlowsOutAtTheMaximum()
    {
        float low = FlashLayer.HdrCore(1f), high = FlashLayer.HdrCore(8f);
        Assert.True(low > 0 && high > low);
        // At the cap, the tone-mapped centre (glow plus veil) is essentially white at the scene's exposure; a flash of 1 is not.
        Assert.True(HdrPipeline.Tonemap((high + FlashLayer.HdrVeil(8f)) * ExplosionScene.BaseExposure) > 0.9f);
        Assert.True(HdrPipeline.Tonemap((low + FlashLayer.HdrVeil(1f)) * ExplosionScene.BaseExposure) < 0.5f);
        Assert.Equal(0f, FlashLayer.HdrCore(0f));
    }

    // ---------------------------------------------------------------- limb darkening, jets, exposure

    [Fact]
    public void LimbDarkening_IsTheLinearLaw_WithUOfPointSix()
    {
        Assert.Equal(0.6f, StarRenderer.LimbDarkeningU);
        Assert.Equal(1f, StarRenderer.LimbDarkening(1f), 6);
        Assert.Equal(0.4f, StarRenderer.LimbDarkening(0f), 6);
        Assert.Equal(0.7f, StarRenderer.LimbDarkening(0.5f), 6);
        Assert.Contains("u_limbU", ShaderText("star.frag"));
    }

    [Fact]
    public void StarOnScreen_CarriesTheLimbDarkeningNote()
    {
        var s = new ExplosionScene(Default()).At(1e-3);
        Assert.True(s.ShowStar);
        Assert.Contains(s.Notes, n => n == ExplosionScene.LimbDarkeningNote);
        Assert.Equal("Limb darkening: a generic law (schematic)", ExplosionScene.LimbDarkeningNote);
        Assert.Equal(ExplosionScene.SchematicNote, s.Notes[^1]);
        var after = new ExplosionScene(Default()).At(1e6);
        Assert.DoesNotContain(after.Notes, n => n == ExplosionScene.LimbDarkeningNote);
    }

    [Fact]
    public void Jets_RenderIntoTheHdrTargetAtFourTimes_AndOnceWithoutIt()
    {
        Assert.Equal(4f, JetRenderer.HdrGain);
        Assert.Equal(4f, JetRenderer.GainFor(hdr: true));
        Assert.Equal(1f, JetRenderer.GainFor(hdr: false));
    }

    [Fact]
    public void Exposure_FollowsProject1sBrightness_AndIsTheBaseWithoutEjecta()
    {
        float atPeak = ExplosionScene.ExposureFor(1f), dim = ExplosionScene.ExposureFor(0.25f);
        Assert.Equal(ExplosionScene.BaseExposure, atPeak, 6);
        Assert.True(dim < atPeak && dim > 0);
        Assert.Equal(ExplosionScene.BaseExposure, ExplosionScene.ExposureFor(0f), 6);   // nothing drawn: the base
        var scene = new ExplosionScene(Default());
        Assert.Equal(ExplosionScene.BaseExposure, scene.At(1e-3).Exposure, 6);
        var late = scene.At(3e8);
        Assert.Equal(ExplosionScene.ExposureFor(late.EjectaBrightness), late.Exposure, 6);
    }

    [Fact]
    public void RetunedConstants_AreNoLongerLabelledFirstGuessInTheirComments()
    {
        // The retune replaced "eyeballed" with the frames it was tuned against (see the Task 11 report).
        string src = File.ReadAllText(Path.Combine(RepoRoot(), "src", "SupernovaLab.App", "Controllers", "SceneController.cs"));
        Assert.DoesNotContain("first guess for Task 11", src);
        Assert.DoesNotContain("eyeballed", src);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SupernovaLab.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
    }

    private static string ShaderText(string name)
    {
        var asm = typeof(ShaderProgram).Assembly;
        string resource = asm.GetManifestResourceNames().Single(n => n.EndsWith(name, StringComparison.Ordinal));
        using var reader = new StreamReader(asm.GetManifestResourceStream(resource)!);
        return reader.ReadToEnd();
    }

    // ---------------------------------------------------------------- the quality governor (ruling R-P2-30: GPU render time)

    /// <summary>Feeds frames of this render time at this wall interval for this many seconds; true if the level changed.</summary>
    private static bool Feed(QualityGovernor g, double seconds, double renderS, double intervalS)
    {
        bool changed = false;
        for (double t = 0; t < seconds - 1e-9; t += intervalS) changed |= g.Update(renderS, intervalS);
        return changed;
    }

    [Fact]
    public void Governor_ThirtyHertzIntervals_WithSmallRenderTimes_NeverStepsDown()
    {
        // A 30 Hz host (or an event-bound on-demand rate) with a fast GPU: the interval is 33 ms, the render time 4 ms.
        var g = new QualityGovernor(QualityLevel.High);
        Assert.False(Feed(g, 60, 0.004, 1.0 / 30));
        Assert.Equal(QualityLevel.High, g.Level);
    }

    [Fact]
    public void Governor_RenderTimesAbove20ms_ForThreeSeconds_StepsDownOnce_NotOnASpike()
    {
        // One 0.3 s render spike among 4 ms frames leaves the mean under 20 ms: no change.
        var spiky = new QualityGovernor(QualityLevel.High);
        Assert.False(Feed(spiky, 1.4, 0.004, 1.0 / 60));
        Assert.False(spiky.Update(0.3, 0.3));
        Assert.False(Feed(spiky, 3.0, 0.004, 1.0 / 60));
        Assert.Equal(QualityLevel.High, spiky.Level);

        var slow = new QualityGovernor(QualityLevel.High);
        Assert.False(Feed(slow, 2.9, 0.025, 0.025));
        Assert.Equal(QualityLevel.High, slow.Level);
        Assert.True(Feed(slow, 0.3, 0.025, 0.025));      // three seconds: one step
        Assert.Equal(QualityLevel.Medium, slow.Level);
        Assert.False(Feed(slow, 2.5, 0.025, 0.025));     // the window starts again
        Assert.True(Feed(slow, 0.8, 0.025, 0.025));
        Assert.Equal(QualityLevel.Low, slow.Level);

        // Just under the limit (20 ms) is fine.
        Assert.False(Feed(new QualityGovernor(QualityLevel.High), 9, 0.0199, 0.02));
    }

    [Fact]
    public void Governor_NeverBelowLow_AndNeverStepsUp()
    {
        var g = new QualityGovernor(QualityLevel.Low);
        Assert.False(Feed(g, 30, 0.1, 0.1));
        Assert.Equal(QualityLevel.Low, g.Level);

        var h = new QualityGovernor(QualityLevel.High);
        Feed(h, 3.1, 0.025, 0.025);
        Assert.Equal(QualityLevel.Medium, h.Level);
        Assert.False(Feed(h, 60, 0.002, 1.0 / 144));
        Assert.Equal(QualityLevel.Medium, h.Level);
    }

    [Fact]
    public void Governor_IgnoresBadTimes_AndAManualChoiceResetsIt()
    {
        var g = new QualityGovernor(QualityLevel.High);
        Assert.False(g.Update(double.NaN, 0.02));
        Assert.False(g.Update(0.02, -1));
        Assert.False(g.Update(-0.01, 0.02));
        Feed(g, 2.0, 0.025, 0.025);
        g.Reset(QualityLevel.Medium);
        Assert.Equal(QualityLevel.Medium, g.Level);
        Assert.False(Feed(g, 2.0, 0.025, 0.025));          // the two slow seconds before the reset do not count
        Assert.True(Feed(g, 1.2, 0.025, 0.025));
        Assert.Equal(QualityLevel.Low, g.Level);
    }

    [Fact]
    public void Controller_FedSlowRenderTimes_StepsDown_AndAManualChoiceResetsTheGovernor()
    {
        var vm = new MainViewModel(TestOutcomes.Engine, new FakeWindowService());
        var scene = new SceneController(vm, () => new SlowHost());
        Assert.False(scene.GpuTimingAvailable);   // no GL context here: nothing measures the GPU, so nothing steps down by itself
        Assert.Equal(QualityLevel.High, scene.Quality);
        for (int i = 0; i < 130; i++) scene.FeedRenderTime(0.025, 0.025);
        Assert.Equal(QualityLevel.Medium, scene.Quality);
        Assert.Contains(SceneController.QualityStepDownNote(QualityLevel.Medium), scene.NotesText);
        Assert.True(scene.IsQualityMedium);

        scene.IsQualityHigh = true;
        Assert.Equal(QualityLevel.High, scene.Quality);
        Assert.DoesNotContain("lowered", scene.NotesText);
        scene.FeedRenderTime(0.025, 0.025);
        Assert.Equal(QualityLevel.High, scene.Quality);

        scene.AutoQuality = false;
        for (int i = 0; i < 400; i++) scene.FeedRenderTime(0.025, 0.025);
        Assert.Equal(QualityLevel.High, scene.Quality);

        // Fast frames at a 30 Hz cadence, and idle gaps, never step down.
        scene.AutoQuality = true;
        for (int i = 0; i < 600; i++) scene.FeedRenderTime(0.004, 1.0 / 30);
        for (int i = 0; i < 20; i++) scene.FeedRenderTime(5.0, 5.0);
        Assert.Equal(QualityLevel.High, scene.Quality);
    }

    private sealed class SlowHost : IRenderHost
    {
        public bool Animating { get; set; }
        public event Action<string>? ContextReady { add { } remove { } }
        public event Action<TimeSpan, Size>? Draw { add { } remove { } }
        public event Action<Exception>? FrameFailed { add { } remove { } }
        public void Start() { }
        public void RequestRender() { }
        public bool Shutdown(Action release) => false;
    }

    // ---------------------------------------------------------------- the collapsible notes overlay (ruling R-P2-30)

    [Fact]
    public void Notes_AreCollapsedByDefault_WithTheCount_AndExpandingShowsAll()
    {
        var vm = new MainViewModel(TestOutcomes.Engine, new FakeWindowService());
        vm.Timeline.ObserverTimeS = 1e-3;
        var scene = new SceneController(vm, () => new SlowHost());
        var all = scene.NotesText.Split(Environment.NewLine);
        Assert.True(all.Length > 1);
        Assert.False(scene.IsNotesExpanded);
        Assert.Equal(all[0], scene.NotesDisplayText);
        Assert.Equal(SceneController.MoreNotesText(all.Length - 1), scene.NotesToggleText);
        Assert.Contains($"{all.Length - 1} more notes", scene.NotesToggleText);

        scene.ToggleNotesCommand.Execute(null);
        Assert.True(scene.IsNotesExpanded);
        Assert.Equal(scene.NotesText, scene.NotesDisplayText);
        Assert.Equal(SceneController.HideNotesText, scene.NotesToggleText);

        // The state is kept when the notes change (a new time).
        vm.Timeline.ObserverTimeS = 1e6;
        Assert.True(scene.IsNotesExpanded);
        Assert.Equal(scene.NotesText, scene.NotesDisplayText);
        scene.ToggleNotesCommand.Execute(null);
        Assert.Equal(scene.NotesText.Split(Environment.NewLine)[0], scene.NotesDisplayText);
        Assert.Equal("1 more note ▾", SceneController.MoreNotesText(1));
    }
}
