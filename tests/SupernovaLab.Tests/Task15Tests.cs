using System.IO;
using System.Windows;
using SupernovaLab.App.Controllers;
using SupernovaLab.App.Services;
using SupernovaLab.App.Tour;
using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Structure;
using SupernovaLab.Core.Timeline;
using SupernovaLab.Rendering;

namespace SupernovaLab.Tests;

/// <summary>Task 15: the tour stops fighting the user, the core stays in sight, panels fold, the gravitational-wave order is steady.</summary>
public class Task15Tests
{
    private static readonly TimeSpan Frame = TimeSpan.FromSeconds(1.0 / 60);

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

    private static (MainViewModel Vm, SceneController Scene, TourDirector Director) Rig(string? presetId = "sn1987a", LayoutStore? store = null)
    {
        var vm = new MainViewModel(TestOutcomes.Engine, new FakeWindowService(), store);
        if (presetId is not null) vm.Lab.SelectedPreset = PresetCatalog.Get(presetId);
        var scene = new SceneController(vm, () => new FakeHost());
        vm.Tour.Director.Scene = scene;
        return (vm, scene, vm.Tour.Director);
    }

    public static IEnumerable<object[]> PresetIds() => PresetCatalog.LoadEmbedded().Select(p => new object[] { p.Id });

    private static string TempFolder()
    {
        string dir = Path.Combine(Path.GetTempPath(), "SupernovaLabTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    // ---- 1. The tour stops fighting the user ----

    [Theory]
    [InlineData(TourState.Playing)]
    [InlineData(TourState.Dwelling)]
    public void DragZoomAndHome_DoNotPauseTheTour(TourState during)
    {
        var (vm, scene, d) = Rig();
        d.Start();
        if (during == TourState.Playing) while (d.State != TourState.Playing) d.Tick(Frame);
        Assert.Equal(during, d.State);

        scene.BeginUserDrag();
        scene.Rotate(30, 5);
        scene.EndUserDrag();
        Assert.Equal(during, d.State);
        scene.Zoom(2);
        Assert.Equal(during, d.State);
        scene.Home();
        Assert.Equal(during, d.State);
        Assert.False(vm.Tour.ShowResume);
    }

    [Fact]
    public void ATimelineScrub_StillPauses_AndACaptionClickResumes()
    {
        var (vm, _, d) = Rig();
        d.Start();
        vm.Timeline.ObserverTimeS = 5;
        Assert.Equal(TourState.Paused, d.State);
        Assert.True(vm.Tour.ShowResume);

        vm.Tour.OnCaptionClicked();   // a click anywhere on the caption panel

        Assert.NotEqual(TourState.Paused, d.State);
        Assert.False(vm.Tour.ShowResume);
        vm.Tour.OnCaptionClicked();   // not paused: nothing happens
        Assert.NotEqual(TourState.Paused, d.State);
    }

    [Fact]
    public void TheResumeButton_StillResumes()
    {
        var (vm, _, d) = Rig();
        d.Start();
        d.Pause();
        vm.Tour.ResumeCommand.Execute(null);
        Assert.NotEqual(TourState.Paused, d.State);
    }

    // ---- 2. The sway ----

    [Fact]
    public void TheSway_IsCentrePlus60DegTimesSine_WithAPeriodOf12Seconds()
    {
        var (_, scene, d) = Rig();
        d.Start();
        Assert.True(d.Current!.Cutaway && d.Current.Sways);
        double centre = scene.NotchFacingYawDeg;
        Assert.Equal(centre, scene.Camera.YawDeg, 6);   // enters at the centre

        var (plus, minus) = d.CurrentSwayAmplitudesDeg;
        Assert.Equal(60, Math.Max(plus, minus));   // the roomy side reaches the full 60 deg (ruling R-P2-34)
        double min = double.MaxValue, max = double.MinValue;
        double t = 0;
        for (int i = 0; i < 12 * 60; i++)
        {
            d.Tick(Frame);
            t += Frame.TotalSeconds;
            double want = Norm(TourDirector.SwayYawDeg(centre, plus, minus, t));
            Assert.Equal(want, scene.Camera.YawDeg, 4);
            double off = Norm(scene.Camera.YawDeg - centre + 180) - 180;
            min = Math.Min(min, off);
            max = Math.Max(max, off);
            Assert.True(d.Current!.Cutaway);   // the first two stops are cutaways; the sway runs on through the change of stop
        }
        Assert.InRange(max, plus - 1, plus + 0.0001);
        Assert.InRange(min, -minus - 0.0001, -minus + 1);
    }

    private static double Norm(double deg) => ((deg % 360) + 360) % 360;

    [Fact]
    public void TheSway_RunsOnAcrossTwoCutawayStops_WithoutAJump()
    {
        var (_, scene, d) = Rig();
        d.Start();
        Assert.True(d.Stops[1].Cutaway, "sn1987a's first two stops are before the breakout");
        double prev = scene.Camera.YawDeg;
        double maxStep = 0;
        for (int i = 0; i < 60 * 40 && d.StopIndex < 2; i++)
        {
            d.Tick(Frame);
            maxStep = Math.Max(maxStep, Math.Abs(Norm(scene.Camera.YawDeg - prev + 180) - 180));
            prev = scene.Camera.YawDeg;
        }
        Assert.True(d.StopIndex >= 2);
        Assert.True(maxStep < 1.0, $"the largest frame-to-frame turn was {maxStep} deg (60 deg * 2 pi / 12 s / 60 fps = 0.52)");
    }

    /// <summary>
    /// "The cut faces and core are never seen from behind on cutaway stops": over one whole period of the sway (and a margin of
    /// 1e-9 for the two instants at its extremes, where the face is edge-on), both cut faces face the eye.
    /// </summary>
    [Theory]
    [MemberData(nameof(PresetIds))]
    public void TheSway_KeepsBothCutFacesExposed_OverOnePeriod(string id)
    {
        var (_, scene, _) = Rig(id);
        scene.IsCutaway = true;
        double phi = scene.WedgeAngleRad, centre = scene.NotchFacingYawDeg, pitch = scene.Camera.PitchDeg;
        var (plus, minus) = scene.SwayAmplitudesFor(centre, TourDirector.SwayAmplitudeDeg);
        Assert.Equal(60, Math.Max(plus, minus));   // the roomy side reaches the full 60 deg (ruling R-P2-34)
        if (phi == 0) Assert.Equal((60.0, 60.0), (plus, minus));
        // Each side is the largest whole degree <= 60 that keeps both faces exposed: one more degree on a capped side would not.
        Assert.True(plus == 60 || !BothExposed(scene, centre + plus + 1), $"{id}: plus side {plus} is not the largest");
        Assert.True(minus == 60 || !BothExposed(scene, centre - minus - 1), $"{id}: minus side {minus} is not the largest");
        var camera = new OrbitCamera { PitchDeg = pitch };
        for (int i = 0; i <= 1200; i++)
        {
            camera.YawDeg = TourDirector.SwayYawDeg(centre, plus, minus, 12.0 * i / 1200);
            var eye = camera.Backward;
            Assert.True(WedgeOrientation.FaceExposed(0, phi, eye.X, eye.Y, eye.Z) || AtEdge(0, phi, eye), $"{id}: face 0 at step {i}");
            Assert.True(WedgeOrientation.FaceExposed(1, phi, eye.X, eye.Y, eye.Z) || AtEdge(1, phi, eye), $"{id}: face 1 at step {i}");
        }
    }

    private static bool BothExposed(SceneController scene, double yawDeg)
    {
        var camera = new OrbitCamera { PitchDeg = scene.Camera.PitchDeg, YawDeg = yawDeg };
        var eye = camera.Backward;
        double phi = scene.WedgeAngleRad;
        return (WedgeOrientation.FaceExposed(0, phi, eye.X, eye.Y, eye.Z) || AtEdge(0, phi, eye))
            && (WedgeOrientation.FaceExposed(1, phi, eye.X, eye.Y, eye.Z) || AtEdge(1, phi, eye));
    }

    /// <summary>The eye is exactly edge-on to the face (w = 0 to rounding): the extreme instant of the sway, not a view from behind.</summary>
    private static bool AtEdge(int plane, double phi, OpenTK.Mathematics.Vector3d eye)
    {
        var w = WedgeOrientation.ToWedge(phi, eye.X, eye.Y, eye.Z);
        return Math.Abs(plane == 0 ? w.X : w.Z) < 1e-9;
    }

    [Fact]
    public void WhileTheUserDrags_TheSwayWaits_AndAfterTheReleaseItCarriesOnFromTheirAngle()
    {
        var (_, scene, d) = Rig();
        d.Start();
        for (int i = 0; i < 90; i++) d.Tick(Frame);   // 1.5 s into the sway

        scene.BeginUserDrag();
        scene.Rotate(100, 0);
        double userYaw = scene.Camera.YawDeg;
        for (int i = 0; i < 60; i++) d.Tick(Frame);
        Assert.Equal(userYaw, scene.Camera.YawDeg, 9);   // suspended while the mouse is down
        scene.EndUserDrag();

        d.Tick(Frame);
        // The centre is now the user's yaw, the phase restarts: a first step of 60 deg * sin(2 pi / 12 / 60) from it.
        var (plus, minus) = d.CurrentSwayAmplitudesDeg;
        double dt = Frame.TotalSeconds;
        Assert.Equal(Norm(TourDirector.SwayYawDeg(userYaw, plus, minus, dt)), scene.Camera.YawDeg, 4);
        for (int i = 0; i < 179; i++) d.Tick(Frame);   // a quarter period later
        Assert.Equal(Norm(TourDirector.SwayYawDeg(userYaw, plus, minus, 180 * dt)), scene.Camera.YawDeg, 4);
        Assert.NotEqual(TourState.Paused, d.State);
    }

    [Fact]
    public void Home_ReturnsToTheDefaultView_AndTheSwayCarriesOnFromThere()
    {
        var (_, scene, d) = Rig();
        d.Start();
        scene.BeginUserDrag();
        scene.Rotate(200, 20);
        scene.EndUserDrag();
        d.Tick(Frame);
        scene.Home();
        Assert.Equal(scene.NotchFacingYawDeg, scene.Camera.YawDeg, 6);
        d.Tick(Frame);
        var (plus, minus) = d.CurrentSwayAmplitudesDeg;
        Assert.Equal(Norm(TourDirector.SwayYawDeg(scene.NotchFacingYawDeg, plus, minus, Frame.TotalSeconds)), scene.Camera.YawDeg, 4);
        Assert.NotEqual(TourState.Paused, d.State);
    }

    [Fact]
    public void AWheelZoom_FreezesTheScale_UntilTheNextStopHandsItBack()
    {
        var (_, scene, d) = Rig();
        d.Start();
        double unit = scene.UnitCm;
        scene.Zoom(3);
        Assert.NotEqual(TourState.Paused, d.State);
        for (int i = 0; i < 30; i++) d.Tick(Frame);
        Assert.Equal(unit, scene.UnitCm);   // frozen at the scale the user chose
        Assert.False(scene.IsAutoFraming);

        d.Next();

        Assert.True(scene.IsAutoFraming);
        Assert.Equal(scene.Director.HalfHeightCm, scene.UnitCm);
        Assert.Equal(OrbitCamera.MinDistanceMpc <= scene.Camera.DistanceMpc ? SceneController.AutoDistance : 0, scene.Camera.DistanceMpc, 9);
    }

    // ---- 3. The caption ----

    [Fact]
    public void TheCaption_SitsAtTheTop_OnACutawayStop_AndAtTheBottomOtherwise()
    {
        var (vm, _, d) = Rig();
        d.Start();
        for (int i = 0; i < d.Stops.Count; i++)
        {
            d.GoTo(i);
            Assert.Equal(d.Stops[i].Cutaway, vm.Tour.CaptionAtTop);
        }
        Assert.Contains(d.Stops, s => s.Cutaway);
        Assert.Contains(d.Stops, s => !s.Cutaway);
    }

    [Fact]
    public void TheCaption_FoldsToAOneLineBar_AndTheStateIsKept()
    {
        var (vm, _, d) = Rig();
        d.Start();
        var tour = vm.Tour;
        Assert.True(tour.IsExpanded);
        Assert.Equal("▾", tour.CollapseToggleText);

        tour.ToggleCaptionCommand.Execute(null);
        Assert.True(tour.IsCollapsed);
        Assert.Equal("▸", tour.CollapseToggleText);
        Assert.StartsWith(string.Join(' ', tour.Caption.Split(' ').Take(TourViewModel.CollapsedWords)), tour.CollapsedText);
        Assert.Equal("Stop 1 of " + d.Stops.Count, tour.CounterText);
        d.Next();   // the state is kept from stop to stop
        Assert.True(tour.IsCollapsed);
        Assert.True(vm.Layout.IsCaptionCollapsed);

        tour.ToggleCaptionCommand.Execute(null);
        Assert.False(tour.IsCollapsed);
    }

    // ---- 4. Panels ----

    [Fact]
    public void FoldingTheLabPanelAndTheOutcomeColumn_LeavesAThinStrip()
    {
        var layout = new LayoutViewModel();
        Assert.Equal(new GridLength(300), layout.LabColumnWidth);
        Assert.True(layout.ShowLabPanel);
        Assert.Equal("Hide the lab panel", layout.LabToggleToolTip);
        Assert.Equal("◂", layout.LabToggleText);

        layout.ToggleLabCommand.Execute(null);
        Assert.Equal(new GridLength(LayoutViewModel.StripWidth), layout.LabColumnWidth);
        Assert.False(layout.ShowLabPanel);
        Assert.Equal("Show the lab panel", layout.LabToggleToolTip);
        Assert.Equal("▸", layout.LabToggleText);

        Assert.Equal("Hide the outcome column", layout.OutcomeToggleToolTip);
        layout.ToggleOutcomeCommand.Execute(null);
        Assert.Equal(new GridLength(LayoutViewModel.StripWidth), layout.OutcomeColumnWidth);
        Assert.False(layout.ShowOutcomeColumn);
        Assert.Equal("Show the outcome column", layout.OutcomeToggleToolTip);
        Assert.Equal("◂", layout.OutcomeToggleText);
        Assert.True(layout.ShowInstrumentStrip);   // the strip is the Focus view's
    }

    [Fact]
    public void TheFocusView_HidesBothColumnsAndTheStrip_AndRestoresWhatWasOpenBefore()
    {
        var layout = new LayoutViewModel();
        layout.ToggleLabCommand.Execute(null);   // the lab panel is folded; the outcome column is open
        var changed = new List<string?>();
        layout.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        layout.ToggleFocusCommand.Execute(null);
        Assert.True(layout.IsFocusView);
        Assert.Equal(new GridLength(0), layout.LabColumnWidth);
        Assert.Equal(new GridLength(0), layout.OutcomeColumnWidth);
        Assert.False(layout.ShowLabPanel);
        Assert.False(layout.ShowOutcomeColumn);
        Assert.False(layout.ShowInstrumentStrip);
        Assert.Contains(nameof(LayoutViewModel.ShowInstrumentStrip), changed);
        Assert.Contains("(F)", layout.FocusToggleText);
        Assert.Contains("F", layout.FocusToggleToolTip);

        layout.ToggleFocusCommand.Execute(null);
        Assert.False(layout.IsFocusView);
        Assert.True(layout.IsLabFolded);                                    // as it was
        Assert.Equal(new GridLength(LayoutViewModel.StripWidth), layout.LabColumnWidth);
        Assert.True(layout.ShowOutcomeColumn);                              // open before, open again
        Assert.True(layout.ShowInstrumentStrip);
    }

    [Fact]
    public void TheKeyFoldsToAKeyTab()
    {
        var layout = new LayoutViewModel();
        Assert.Equal("Key ▾", layout.KeyToggleText);
        layout.ToggleKeyCommand.Execute(null);
        Assert.True(layout.IsKeyFolded);
        Assert.Equal("Key ▸", layout.KeyToggleText);
    }

    [Fact]
    public void TheNotesFold_IsKeptInTheLayout_BothWays()
    {
        var (vm, scene, _) = Rig();
        scene.IsNotesExpanded = true;
        Assert.True(vm.Layout.IsNotesExpanded);
        vm.Layout.IsNotesExpanded = false;
        Assert.False(scene.IsNotesExpanded);

        // A scene made after the layout says "expanded" starts expanded.
        vm.Layout.IsNotesExpanded = true;
        using var second = new SceneController(vm, () => new FakeHost());
        Assert.True(second.IsNotesExpanded);
    }

    [Fact]
    public void TheFShortcut_AppliesOnlyOutsideTextEditors()
    {
        Assert.True(LayoutViewModel.FocusShortcutApplies(null));
        Assert.True(LayoutViewModel.FocusShortcutApplies(new object()));
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                Assert.False(LayoutViewModel.FocusShortcutApplies(new System.Windows.Controls.TextBox()));
                Assert.False(LayoutViewModel.FocusShortcutApplies(new System.Windows.Controls.ComboBox()));
                Assert.False(LayoutViewModel.FocusShortcutApplies(new System.Windows.Controls.PasswordBox()));
                Assert.True(LayoutViewModel.FocusShortcutApplies(new System.Windows.Controls.Button()));
                // An open drop-down list's focus is a ComboBoxItem; anything inside a popup, a menu item and a context menu count too.
                Assert.False(LayoutViewModel.FocusShortcutApplies(new System.Windows.Controls.ComboBoxItem()));
                Assert.False(LayoutViewModel.FocusShortcutApplies(new System.Windows.Controls.MenuItem()));
                var popup = new System.Windows.Controls.Primitives.Popup();
                var inPopup = new System.Windows.Controls.Button();
                popup.Child = inPopup;
                Assert.False(LayoutViewModel.FocusShortcutApplies(inPopup));
                var combo = new System.Windows.Controls.ComboBox();
                var inCombo = new System.Windows.Controls.TextBlock();
                combo.Items.Add(inCombo);
                Assert.False(LayoutViewModel.FocusShortcutApplies(inCombo));
            }
            catch (Exception e)
            {
                error = e;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(error);
    }

    [Fact]
    public void TheLayout_SurvivesARoundTrip_ThroughTheFile()
    {
        string dir = TempFolder();
        try
        {
            var store = new LayoutStore(dir);
            var first = new MainViewModel(TestOutcomes.Engine, new FakeWindowService(), store);
            Assert.False(first.Layout.IsLabFolded);   // no file yet: defaults
            first.Layout.IsLabFolded = true;
            first.Layout.IsOutcomeFolded = true;
            first.Layout.IsFocusView = true;
            first.Layout.IsKeyFolded = true;
            first.Layout.IsNotesExpanded = true;
            first.Tour.ToggleCaptionCommand.Execute(null);
            first.Layout.Flush();   // what the window does on close
            Assert.True(File.Exists(Path.Combine(dir, "layout.json")));

            var second = new MainViewModel(TestOutcomes.Engine, new FakeWindowService(), new LayoutStore(dir));
            Assert.Equal(first.Layout.Settings, second.Layout.Settings);
            Assert.True(second.Layout.IsLabFolded && second.Layout.IsOutcomeFolded && second.Layout.IsFocusView && second.Layout.IsKeyFolded
                        && second.Layout.IsNotesExpanded && second.Layout.IsCaptionCollapsed);
            Assert.True(second.Tour.IsCollapsed);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json {{{")]
    [InlineData("[1,2,3]")]
    [InlineData("{\"LabPanelFolded\": \"yes\"}")]
    [InlineData("null")]
    public void ACorruptLayoutFile_GivesTheDefaults_AndDoesNotThrow(string content)
    {
        string dir = TempFolder();
        try
        {
            File.WriteAllText(Path.Combine(dir, "layout.json"), content);
            var vm = new MainViewModel(TestOutcomes.Engine, new FakeWindowService(), new LayoutStore(dir));
            Assert.Equal(new LayoutSettings(), vm.Layout.Settings);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void AMissingFolder_AndAnUnwritableOne_AreNotErrors()
    {
        string dir = Path.Combine(TempFolder(), "not", "yet", "there");
        var store = new LayoutStore(dir);
        Assert.Equal(new LayoutSettings(), store.Load());
        store.Save(new LayoutSettings { FocusView = true });   // creates the folders
        Assert.True(store.Load().FocusView);

        // A path that cannot be a folder (a file stands in its way): the save is silently dropped.
        string file = Path.Combine(TempFolder(), "afile");
        File.WriteAllText(file, "x");
        var blocked = new LayoutStore(Path.Combine(file, "sub"));
        blocked.Save(new LayoutSettings { FocusView = true });
        Assert.Equal(new LayoutSettings(), blocked.Load());
    }

    [Fact]
    public void TheLayoutIsSavedAtMostOncePer500ms_AndFlushWritesTheLastChange_Atomically()
    {
        string dir = TempFolder();
        try
        {
            var now = TimeSpan.FromSeconds(100);
            var store = new LayoutStore(dir);
            var layout = new LayoutViewModel(store, clock: () => now);
            string file = Path.Combine(dir, "layout.json");

            layout.IsLabFolded = true;                    // the first change is written at once
            Assert.True(store.Load().LabPanelFolded);
            now += TimeSpan.FromMilliseconds(100);
            layout.IsOutcomeFolded = true;                // inside the interval: waits
            layout.IsKeyFolded = true;
            Assert.False(store.Load().OutcomeColumnFolded);
            layout.Flush();                               // the window closing
            var saved = store.Load();
            Assert.True(saved.LabPanelFolded && saved.OutcomeColumnFolded && saved.KeyFolded);

            now += TimeSpan.FromMilliseconds(600);
            layout.IsFocusView = true;                    // after the interval: written at once again
            Assert.True(store.Load().FocusView);

            Assert.Equal(new[] { file }, Directory.GetFiles(dir));   // no temporary file left behind
            layout.Flush();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void ATestViewModelWithoutAStore_TouchesNoFile()
    {
        var vm = new MainViewModel(TestOutcomes.Engine, new FakeWindowService());
        vm.Layout.IsFocusView = true;   // nothing to save to, and no error
        Assert.True(vm.Layout.IsFocusView);
    }

    // ---- 5. A steady order ----

    private static MainViewModel VmWith(string? presetId)
    {
        var vm = new MainViewModel(TestOutcomes.Engine, new FakeWindowService());
        if (presetId is not null) vm.Lab.SelectedPreset = PresetCatalog.Get(presetId);
        return vm;
    }

    [Theory]
    [MemberData(nameof(PresetIds))]
    public void NeutrinoBurstIsRow0_AndGravitationalWavesRow1_WheneverBothApply(string id)
    {
        var vm = VmWith(id);
        foreach (double width in new[] { 400.0, 900, 1300 })
        {
            vm.Timeline.LayoutMarkerLabels(width);
            var burst = vm.Timeline.MarkerLabels.FirstOrDefault(l => l.Marker.Label == "Neutrino burst");
            var waves = vm.Timeline.MarkerLabels.FirstOrDefault(l => l.Marker.Label == "Gravitational waves");
            bool both = vm.Timeline.Markers.Any(m => m.Label == "Neutrino burst") && vm.Timeline.Markers.Any(m => m.Label == "Gravitational waves");
            if (!both) continue;
            Assert.NotNull(burst);
            Assert.NotNull(waves);
            Assert.Equal(0, burst!.Row);
            Assert.Equal(1, waves!.Row);
        }
    }

    [Fact]
    public void TheGravitationalWaveLabel_StaysOnRow1_AcrossLabOutcomes()
    {
        var vm = VmWith(null);
        var rows = new List<int>();
        foreach (double mass in new[] { 9.0, 12, 15, 20, 25, 40 })
        {
            vm.Lab.InitialMass = mass;
            vm.Timeline.LayoutMarkerLabels(1000);
            var gw = vm.Timeline.MarkerLabels.FirstOrDefault(l => l.Marker.Label == "Gravitational waves");
            var nu = vm.Timeline.MarkerLabels.FirstOrDefault(l => l.Marker.Label == "Neutrino burst");
            if (gw is not null) rows.Add(gw.Row);
            if (gw is not null) Assert.Equal(0, nu!.Row);
        }
        Assert.NotEmpty(rows);
        Assert.All(rows, r => Assert.Equal(1, r));
    }

    [Theory]
    [InlineData("sn1987a")]
    [InlineData("sn1993j")]
    [InlineData("sn1054")]
    public void TheJudgedModels_KeepTheSameOrder_At1ms_1s_1day(string id)
    {
        var vm = VmWith(id);
        foreach (var panel in vm.Panels)
        {
            var orders = new List<string>();
            foreach (double t in new[] { 1e-3, 1.0, PhysicalConstantsDay })
            {
                vm.Timeline.ObserverTimeS = t;
                orders.Add(string.Join(",", panel.JudgedModelIds));
            }
            Assert.Single(orders.Distinct());
            foreach (var instrument in panel.Instruments)
            {
                panel.SelectedInstrument = instrument;
                var perTime = new List<string>();
                foreach (double t in new[] { 1e-3, 1.0, PhysicalConstantsDay })
                {
                    vm.Timeline.ObserverTimeS = t;
                    perTime.Add(string.Join(",", panel.JudgedModelIds));
                }
                Assert.Single(perTime.Distinct());
            }
        }
    }

    private const double PhysicalConstantsDay = 86400;

    // ---- the snapshot tool's --layout ----

    [Fact]
    public void Snapshot_LayoutOption_SetsStates_AndAnEntryCanAddItsOwn()
    {
        var request = SnapshotRequest.Parse(new[] { "--snapshot", "out", "--layout", "focus,lab-folded", "--preset", "sn1987a+tour,default+keyfolded" })!;
        Assert.True(request.Layout!.FocusView);
        Assert.True(request.Layout.LabPanelFolded);
        Assert.False(request.Layout.KeyFolded);
        Assert.True(request.LayoutFor("default+keyfolded").KeyFolded);
        Assert.False(request.LayoutFor("sn1987a+tour").KeyFolded);
        Assert.Equal("-sn1987a-keyfolded", SnapshotRequest.FileSuffix("sn1987a+keyfolded"));
        Assert.Equal(new LayoutSettings(), SnapshotRequest.Parse(new[] { "--snapshot", "out" })!.LayoutFor(null));
        Assert.Throws<ArgumentException>(() => SnapshotRequest.Parse(new[] { "--snapshot", "out", "--layout", "sideways" }));
    }
}
