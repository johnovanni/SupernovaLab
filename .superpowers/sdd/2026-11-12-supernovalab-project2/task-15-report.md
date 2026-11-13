# Task 15 report: tour control, decluttering, steady GW order, R1

Status: DONE_WITH_CONCERNS. Branch project2. Commits (on top of 7c97ab5):
- 685cfb2 tour: no pause on drag/zoom/Home; sway (TourDirector, TourStop, TourBuilder, SceneController, TourDirectorTests)
- d6bc0a1 panels: folds, Focus view, layout.json, caption placement/collapse, snapshot --layout
- f51d981 steady GW/neutrino label rows, fixed detector order, Task15Tests
- 2b28c94 docs: inconsistency #51 corrected

Full suite once before committing: 2,212 passed, 0 failed (2,157 before + 55 new); build 0 warnings, 0 errors. Never pushed.

## Per brief item

### 1. The tour stops fighting the user
- `SceneController` no longer raises `UserInteracted` (event removed from Rotate/Zoom/Home); `TourDirector` subscribes only to `Timeline.UserInteracted` (scrub, marker click, Play/Pause, Explode via `MainViewModel.Explode`) -> `TourDirector.cs` ctor and `Scene` setter. A new outcome still exits.
- Drag: `SceneController.IsUserDragging/BeginUserDrag/EndUserDrag` (SceneController.cs, ~line 360); `MainWindow.xaml.cs` calls them (after the 3 DIP slop, on mouse up, and on `LostMouseCapture`). While dragging the sway is suspended; on release the sway recentres on the user's yaw with phase 0 (no jump).
- Wheel zoom: still freezes the scale; `SceneController.HandBackScale()` (new) is called by `TourDirector.ApplyStop` at every stop entry (Next/Previous/GoTo and travel arrival) so the next stop's jump hands the scale back (camera distance, target, auto-framing, Director.Jump; angles untouched).
- Home: restores framing and the default view and does not pause; the director sees the yaw change and recentres the sway there.
- Caption click: `TourViewModel.OnCaptionClicked()` (resumes only when paused); `MainWindow.OnTourPanelMouseDown` calls it. The Resume button stays.

### 2. The sway
- `TourDirector.Sway/StartSway/Recentre/SwayYawDeg`: yaw = centre + A sin(2 pi t / 12 s), A = 60 deg (`SwayAmplitudeDeg`, `SwayPeriodS`). Centre = `SceneController.NotchFacingYawDeg` (new; computed fresh with the cutaway wedge, R-P2-22) on cutaway stops, the entering yaw otherwise. Between two stops of the same kind the sway runs on with no jump (test). `IsTourOrbiting` (view animates each frame) kept for the sway.
- `TourStop.OrbitDegPerS` is kept as is: now only a flag (not 0 = sway; its value no longer sets a speed). TourBuilder and its tests are unchanged (only doc comments updated); `CutawayOrbitDegPerS = 10` is now only the flag value.
- **Deviation (see Concerns): the amplitude is 60 deg only where that keeps both cut faces exposed.** `SceneController.SwayAmplitudeFor(centre, wanted)` reduces it in 1 deg steps; `TourDirector.CurrentSwayAmplitudeDeg` exposes it.

### 3. Caption off the slice
- MainWindow.xaml: one overlay grid (rows: readout/notes, caption-top, spacer, caption-bottom, key/hint). The caption border's Style puts it in row 3 (bottom) and moves it to row 1, top-aligned (just below the readout/notes row) when `TourViewModel.CaptionAtTop` (= current stop is a cutaway).
- Collapse: `TourViewModel.IsCollapsed/IsExpanded/ToggleCaptionCommand/CollapsedText/CollapseToggleText` ("▾" full, "▸" bar; the bar shows the counter and the first 6 words with an ellipsis). State is `LayoutViewModel.IsCaptionCollapsed` (kept across launches).

### 4. Panels
- New `ViewModels/LayoutViewModel.cs`: IsLabFolded, IsOutcomeFolded, IsFocusView, IsKeyFolded, IsNotesExpanded, IsCaptionCollapsed; column widths, arrows ("◂"/"▸"), tooltips "Hide/Show the lab panel", "Hide/Show the outcome column"; Focus toggle "Focus view (F)" with the shortcut in the text and tooltip. Focus view is a separate flag, so leaving it brings back exactly what was open.
- Each column is `[panel | 20 px strip with the arrow button]` (lab) / `[strip | panel]` (outcome); folded = just the 20 px strip; Focus = width 0. The code-behind sets the two `ColumnDefinition` widths from the layout view model (a ColumnDefinition cannot bind to the data context); the instrument strip is bound to `ShowInstrumentStrip`. The GL host resizes through the existing `SceneGrid.SizeChanged -> SetViewSize`.
- Focus view: button next to Auto-frame (bottom right of the 3D view), View menu check item "_Focus view" with `InputGestureText="F"`, and key F via `MainWindow.OnPreviewKeyDown`, which ignores the key unless `LayoutViewModel.FocusShortcutApplies(Keyboard.FocusedElement)` (not for TextBoxBase, ComboBox, PasswordBox), no modifiers, not a repeat.
- Key: "Key ▾/▸" tab on the 3D view (same style as "{n} more notes"); the notes' expanded state is synced to the layout both ways (`SceneController.OnLayoutChanged`).
- Persistence: `Services/LayoutStore.cs` (`LayoutSettings` record; `layout.json` in a folder passed in; `ForCurrentUser()` = `%LOCALAPPDATA%\SupernovaLab`). Missing/corrupt/unreadable files give defaults, write failures are ignored. `MainViewModel(engine, windows, LayoutStore? = null)`: null (tests, snapshots) keeps it in memory. `App.xaml.cs` passes the store except in snapshot mode.
- Snapshot tool: `--layout focus,lab-folded,outcome-folded,key-folded,notes-open,caption-folded` (hyphens optional) and per-entry flags `+focus`, `+labfolded`, `+outcomefolded`, `+keyfolded`, `+notesopen`, `+captionfolded` (file suffix "-focus" etc.), so one launch can show several states. Snapshots never read or write layout.json (the layout is applied per entry via `vm.Layout.Apply`).

### 5. Steady order
- `TimelineViewModel.LayoutMarkerLabels`: when both markers exist, the neutrino burst takes row 0 and the gravitational waves row 1, reserved before any other label is placed (`LabelSpan` helper). If only one applies, normal placement.
- `InstrumentPanelViewModel.Detection.cs` `JudgeNow`: the OrderBy-by-verdict is gone; the list keeps the catalogue's order; the badges pick the best verdict via `Best(...)` (best Rank, ties to a model in the selected sub-tab, then first in list: the same results as the old sort). New `JudgedModelIds` exposes the list order. Note: the only verdict-sorted list in the code was this internal per-instrument model list; the instrument pickers and the "users" list were already in catalogue order.

### 6. R1
- `docs/research/scientific-inconsistencies.md` #51 cause rewritten as the brief says. Verified first: `LightCurveModel.cs` lines 113-114 ("an interacting supernova is drawn without the collision's light") and `LightCurve.cs` line 89 (`start = ... EarliestStartS`, 3600 s at line 32). Status column left as it was.

## Tests (new: Task15Tests.cs, 55 tests incl. theories; plus TourDirectorTests changes)
TDD evidence: I wrote the implementation and tests together in small steps, so most tests did not have a separate red run. The one real red was the cut-face test: it failed for several presets (faces seen from behind at about 1.7 s into the sway) and led to the amplitude reduction. Earlier, tests asserting the old behaviour failed against the new code until updated (below).
- Drag/zoom/Home keep Playing and Dwelling; scrub, marker, Play, Explode pause; caption click resumes (and does nothing when not paused); Resume button.
- Sway: formula over 12 s with 1/60 s ticks; no jump across two cutaway stops (max 0.52 deg/frame); both cut faces exposed (`WedgeOrientation.FaceExposed`) over one period for every preset; drag suspends then carries on from the user's yaw; Home recentres; wheel zoom frozen until the next stop hands it back.
- Caption at top on cutaway stops, bottom otherwise; collapse state and text.
- Layout: folds, widths, arrows, tooltips, Focus save/restore, key fold, notes <-> layout sync, F shortcut guard (STA thread, TextBox/ComboBox/PasswordBox), round trip in a temp folder, corrupt files (5 variants) give defaults, missing/unwritable folder, no store = no file.
- Timeline rows across all 14 presets x 3 widths and across lab masses; detector order at 1 ms, 1 s, 1 d for 3 presets x all panels x all instruments; snapshot `--layout` parsing.

### Old tests changed (TourDirectorTests.cs)
1. `AtAStop_TheCutaway_Orbit_AndHighlight_AreApplied`: asserted a 10 deg/s turn after 1 s; now asserts the sway (enters at the notch-facing yaw, +amplitude after a quarter period).
2. `UserInput_PausesTheTour_ResumeContinues`: removed the "rotate", "zoom", "home" cases (they must not pause now; covered by `DragZoomAndHome_DoNotPauseTheTour`), added "explode".
3. `DetachingTheScene_StopsItsInputPausingTheTour` renamed `DetachingTheScene_LeavesTheTourRunning` (the scene no longer pauses the tour at all; it now also ticks once).
TourBuilderTests unchanged. `TheScenesUserInput_DoesNotPauseAnInactiveTour` unchanged and passing.

## Visual check (one launch, exit 0; output `C:\Users\johna\AppData\Local\Temp\claude\32343adde489ae69\d0017080-5649-4385-9d5d-6f90568af0a9\scratchpad\task15`)
Command: `--snapshot <dir> --window main --size 1366x768 --preset "sn1987a+tour,sn1987a+tour+labfolded+keyfolded,sn1987a+tour+focus+captionfolded" --tour 0,4,last` (9 PNGs, snapshot.log).
- `main-sn1987a-tour0-1366x768.png` (cutaway stop 1 of 12, "Core collapse"): caption panel at the top of the 3D view, below the readout and notes row; the orange hydrogen slice and the grey star are below it and visible; key bottom left ("Key ▾" with Oxygen/silicon, Helium, Hydrogen, Shock front); "Focus view (F)" button bottom right; lab panel and outcome column open with 20 px strips holding "◂"/"▸".
- `main-sn1987a-tour4-1366x768.png` (non-cutaway stop 5 of 12, 3 h): caption at the bottom of the 3D view above the hint, nothing on the debris cloud.
- `main-sn1987a-labfolded-keyfolded-tour0-...png`: lab panel folded to a 20 px strip with "▸", 3D view wider (1223 px readback), key folded to a small "Key ▸" tab, outcome column still open.
- `main-sn1987a-focus-captionfolded-tour0-...png`: Focus view: only the 3D view (1648x707 readback) and the timeline; instrument strip, both columns and strips hidden; the button reads "Exit focus (F)"; caption collapsed to a one-line bar ("▸ Core collapse  Stop 1 of 12"). The `-tour4` PNG of the same layout shows the bar "▸ Observed: About three hours after the... Stop 5 of 12" at the bottom.
- Timeline in all of them: "Neutrino burst" on row 0 and "Gravitational waves" under it on row 1 at the 10 ms mark.
The sway itself cannot be seen in snapshots (nothing ticks the tour there); it is covered by the unit tests. I did not open the live app.

## Files changed
New: `src/SupernovaLab.App/Services/LayoutStore.cs`, `src/SupernovaLab.App/ViewModels/LayoutViewModel.cs`, `tests/SupernovaLab.Tests/Task15Tests.cs`.
Modified: `App.xaml.cs`, `Controllers/SceneController.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs`, `Services/SnapshotRenderer.cs`, `Tour/TourBuilder.cs`, `Tour/TourDirector.cs`, `Tour/TourStop.cs`, `ViewModels/InstrumentPanelViewModel.Detection.cs`, `ViewModels/MainViewModel.cs`, `ViewModels/TimelineViewModel.cs`, `ViewModels/TourViewModel.cs` (all under `src/SupernovaLab.App`), `tests/SupernovaLab.Tests/TourDirectorTests.cs`, `docs/research/scientific-inconsistencies.md`.

## Self-review
- No sound added. No timers or background processes left running (the app launch exited by itself; no tour clock in snapshot mode).
- Layout writes happen on each change (tiny file); no debounce. A drag does not write anything.
- XAML was only exercised through the one snapshot launch (all 9 rendered without binding errors visible in the pictures); the live-mouse paths (drag begin/end, F key, caption click, fold buttons) are not run in a real window, only by unit tests of the view models and director.
- The overlay was merged into a single grid, so the ring labels now sit under the readout/notes/caption (they were above the readout before); they are non-interactive text, no visible change in the pictures.

## Concerns
1. **Sway amplitude below 60 deg for presets with a turned wedge.** The brief says +-60 deg around the notch-facing yaw keeps the faces exposed, but the wedge is turned about the scene's y axis while the camera yaw turns about the vertical axis, so with the turned wedge (R-P2-22) 9 of 14 presets (those with finger cones) would show a cut face from behind at +-60. I reduce the amplitude per case to the largest whole degree that keeps both faces exposed (an edge-on instant at the extreme counts as exposed): 60 deg where the wedge is not turned (sn2011fe, sn2014j, tycho, kepler, n6946bh1), 59 for sn1987a, casa and sn1993j, 47 for sn1054, sn2023ixf and betelgeuse, 45 for sn1998bw, sn2008d and sn2015bn. The test asserts the range 45-60. The alternative (full 60 deg, faces seen from behind) breaks the brief's own requirement. Owner may prefer another centre or axis.
2. At the 1,100 px minimum window width each side column is now 320 (300 + the 20 px strip), so the 3D view is about 40 px narrower than before (about 428 px); the bottom row (key, hint, Auto-frame, new Focus button) can overlap there (it already could at 1,100 with the key open). Fold a column or use Focus view; I did not change the minimum width.
3. A tour stop that follows a wheel zoom hands the scale back at every stop entry, including travel arrivals, not only at the Next/Previous jump.
4. If the user drags only vertically (pitch), the sway amplitude is recomputed on release (recentre), so it may shrink or grow slightly then.

## Fix round (review of Task 15)

1. Bottom overlay overlap (MainWindow.xaml, LayoutViewModel.cs:23). Side columns are now 300 = panel 280 + 20 px strip (`PanelWidth` 280; LabColumn/OutcomeColumn 300), so the 3D view is about 40 px wider again (the lab panel lays out fine at 280). The colour key moved out of the shared bottom cell into its own overlay row (row 2, above the bottom caption) with a header row plus a ScrollViewer for the list (it scrolls instead of being cut off when the view is short); its lines wrap (DockPanel). The hint panel is alone in row 4 with a WrapPanel and MaxWidth 260, so Auto-frame, Focus view and the hint text wrap. Rows instead of two star columns: separate rows remove any possible overlap, including with the caption. Test: LayoutViewModel column width test (300). Not captured: the Auto-frame-visible state (snapshots always start auto-framed); it is structurally safe (the hint wraps inside 260 px in its own row).
2. Sway R-P2-34. `SceneController.SwayAmplitudesFor` returns (Plus, Minus), each the largest whole degree <= 60 that keeps both faces exposed on that side; `TourDirector.SwayYawDeg(centre, plus, minus, t)` uses plus while sin >= 0, minus otherwise (continuous); `CurrentSwayAmplitudesDeg`. Tests (Task15Tests, TourDirectorTests): both faces exposed over a full period for all 14 presets; the roomy side is exactly 60 for every preset; each capped side is the largest (one more degree breaks it); the sway formula, drag and Home tests use the asymmetric form.
3. F shortcut (LayoutViewModel.FocusShortcutApplies): also false for ComboBoxItem, MenuItem, and any element with a ComboBox, ComboBoxItem, Popup, MenuItem, ContextMenu or PopupRoot ancestor. Tested (ComboBoxItem, MenuItem, a button in a Popup, a TextBlock in a ComboBox; a plain Button still true).
4. LayoutStore.Save now writes `layout.json.<pid>.tmp` then `File.Move(overwrite)`; temp is deleted on failure. LayoutViewModel writes the first change at once, then at most one write per 500 ms (`SaveInterval`): later changes are held and written by a one-shot System.Threading.Timer, or by `Flush()`, which MainWindow.Closing calls. Test with a fake clock: immediate first write, held writes not on disk until Flush, write after the interval, no leftover .tmp. The round-trip test now flushes before reading.
5. Dead orbit constants removed: `TourBuilder.CutawayOrbitDegPerS` deleted; `TourStop.OrbitDegPerS` renamed to `bool Sways` (true exactly on cutaway stops); callers and tests updated; stale "orbit" comments fixed.

Tests: `dotnet test -c Debug` full suite once: Passed 2213, Failed 0 (2212 + 1 new); `dotnet build --no-incremental`: 0 warnings, 0 errors.
Visual: `--snapshot <dir> --window main --size 1100x700,1366x768 --preset "sn1987a+tour,tycho+tour" --tour 0,4,6` into C:\Users\johna\AppData\Local\Temp\claude\32343adde489ae69\d0017080-5649-4385-9d5d-6f90568af0a9\scratchpad\task15-fix (the launch had to be repeated twice: a first run showed the key cut off at the bottom and the hint hidden under it on a long caption, which led to the scrolling key row; one run used a stale build after a XAML typo). Final PNGs:
- main-sn1987a-tour6-1100x700.png (non-cutaway, long caption at the bottom, shell key items open): caption at about y 135-305, the key below it (Key, swept-up gas, contact surface, reverse shock, fingers) fully visible, "Focus view (F)" + hint on the last row; no overlap; 3D view about 480 px wide.
- main-sn1987a-tour0-1100x700.png / tour4: cutaway stop with the caption at the top, key bottom left, hint bottom right on the same visual band; no overlap (taken before the final row layout but same widths).
- main-sn1987a-tour0-1366x768.png: caption at the top below the notes, key tab "Key ▾ / Shock front" bottom left, Focus view and hint bottom right; columns and strips as before.
