# Global constraints for every Project 3 task (copied from the plan; rulings from the ledger)

## Global Constraints

Everything in Project 1's and Project 2's Global Constraints still holds:
- cgs units and `PhysicalConstants`;
- source time vs observer time (× (1 + z));
- `Quantity`/`Confidence`/`Source` on every shown number;
- `[APPROX]` + `Confidence.Speculative` for values the research lacks;
- formula tests against quoted or hand-computed values;
- embedded data, glossary tooltips, no network;
- OpenGL 4.1 core as the floor;
- the HDR / no-bloom paths.

In addition:

- **No screen capture.** Visual checks use only the app's own `--snapshot`, `--bench` and `--sweep`. Task 4 adds
  `--closeup` (open the Close-up window) and `--window closeup` (capture it). Never PowerShell `Add-Type`, P/Invoke or
  `CopyFromScreen`. App launches: a budget of **1–2 per task**, batched; going over is reported in the ledger.
- **Changes to Projects 1 and 2 are exactly spec §7:**
  - the "Expected" badge and caption wording (Task 2);
  - SN 1987A's "What was actually seen" note (Task 2);
  - the main view's remnant drift, beam width ρ(P) and shared beam phase (Task 6);
  - the gamma-ray panel's giant-flare spike (Task 12).

  Anything else that would change a panel or `ISignalModel` is flagged, not done (spec §10).
- **Flash safety:** nothing in either view changes brightness periodically faster than 3 Hz (spec §1, criterion 6;
  WCAG 2.3.1).
  - The drawn rotation period is `Magnetosphere.DrawnPeriodS` = max(real period, **2 s**) in both views.
  - A giant flare is one rise-and-fade with a rise of at least 0.15 s.
- **Sound:**
  - Only the pulsar's pulses, and **off by default** (spec §4.4, C19).
  - Everything it conveys is also on screen.
  - No other sound anywhere.
- **Display randomness** (kick direction, giant flares) is seeded from the run number (`MainViewModel.RunNumber`) and
  changes with Re-roll. The physics is deterministic (Project 1 D4). Particle sets keep Project 2's fixed seed 1987.
- **Labels, exact strings** (each is a `const string` in the class that shows it):
  - `"From simulations"`
  - `"Camera where no telescope could be"`
  - `"Shown {N}× slower"` / `"Shown at real speed"`
  - `"To Earth: hidden by debris"` / `"Beam misses Earth"`
  - `"Settled: spinning black hole"`
  - `"Magnified ×{N}"`
  - `"Expected"`
  - Schematic elements say "schematic" in their note, as in Project 2.
- **Performance:** with the main window at 1366×768 and the Close-up at its default **900×640 DIP**, both 3D views
  ≥ 50 fps on Iris Xe class graphics, measured with `--bench --closeup`. Also report 1920×1080. The Close-up stops
  drawing when minimized or fully hidden.
- **Commits** end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Commit on `project3`; **never push**.
- **Tests:** `dotnet test tests/SupernovaLab.Tests` passes with 0 warnings at every task's end. That is 2,242 tests
  before this plan.
- **Inconsistencies list:** any new mismatch goes into `docs/research/scientific-inconsistencies.md` (from #55) in the
  task that finds it.
- **Build record:** execution rulings go in `docs/superpowers/build-record/project3-rulings.md` as R-P3-n, progress in
  `project3-ledger.md`, and the owner's plain-words summary in `project3-calls-i-made.md` (Task 14).

## Plan rulings (my calls, for the owner's review)

- **P3-1, units and precision.**
  - The close-up's scene unit is **1 km**, centred on the compact object; the object never moves in its own scene.
  - Black-hole and lensing code works in units of GM/c², converted at the boundary.
  - `GravitationalRadiusCm(M) = G·M·M☉/c²` (1.4766 × 10⁵ cm per M☉).
- **P3-2, one vector type in Core.** `SupernovaLab.Core.Compact.Vec3` (a `readonly record struct` of doubles, with `+`,
  `*`, `Dot`, `Cross`, `Length`, `Normalized`). Core still references no OpenTK. Rendering converts to `Vector3d`.
- **P3-3, scene frame shared by both views.**
  - The spin axis is scene +z (Project 2's `RemnantRenderer` already turns beams about z).
  - Earth is `Magnetosphere.EarthDirection` at angle ζ from +z, with azimuth fixed at −y (schematic; spec §4.3).
  - The kick and preset proper motions use scene "north" = +z projected onto the sky plane (the plane perpendicular to
    Earth).
- **P3-4, pass-through preset fields.** Two new optional preset fields, carried into `Outcome` like `SceneDisclosures`
  and read by no signal model:
  - `kickPositionAngleDeg` → `Outcome.KickPositionAngleDeg`;
  - `magneticInclinationDeg` → `Outcome.MagneticInclinationDeg`.

  Each has a `…Source` citation string. Values come only from §C4 [V]/[R] entries.
- **P3-5, one beam clock.** A new `App/Services/BeamClock` (a wall-clock stopwatch) is injected into both scene
  controllers. Drawn phase = 2π · wall seconds / `DrawnPeriodS`. This replaces `SceneController`'s private `_wall` use at
  line 841.
- **P3-6, the lensing environment.**
  - `EnvironmentCapture` renders Project 2's scene (its existing renderers, at **Low** quality: 50,000 particles) from
    the remnant's position into a **256² RGBA16F cube map**.
  - It refreshes when log₁₀ source time has moved by > 0.005, the outcome or run number changed, or the main view's
    cutaway or quality changed, and at most **4 times per wall second**.
    *(Amended by R-P3-40: the environment is fixed at Low quality and ignores the main view's cutaway and quality, so
    only the time, outcome and run triggers apply.)*
  - The no-HDR path uses RGBA8.
- **P3-7, the ray tracer.**
  - One algorithm, written twice: GLSL in `lensing.frag` and C# in `BlackHoleOptics.TraceRay`.
  - It integrates null geodesics backwards from the camera in **Cartesian Kerr–Schild coordinates** (M = 1), which have
    no coordinate singularity at the horizon.
  - Classical RK4, step h = 0.05 · max(r − r₊, 0.5) · (1 + r/20), capped at 2.0, with at most **400 steps**.
  - **Captured** when r < r₊ + 0.01. **Escaped** when r > R_esc (= max(60, 1.2 × camera distance)): then sample the
    environment by the exit direction, and the grid by the exit position on the grid sphere (P3-8).
  - **Hits the neutron star** (spin 0 only) when r < R_ns/(GM/c²): then shade the surface at that point.
  - The `a = 0` case runs the same tracer. Spec §6.6's "fast path" is met by the tracer stopping early (no capture
    test needed at large b), and by Task 4 measuring it.
  - A lookup-table path is added **only if** Task 4's bench shows the tracer misses 50 fps at reduced resolution.
    Record that as an R-P3 ruling.
- **P3-8, the grid** is the sphere r = **50 GM/c²**: latitude/longitude lines every 10°, drawn where a ray crosses that
  sphere outward. The ergosphere outline, horizon label and photon-sphere label are overlays (Task 10).
- **P3-9, lensing resolution.**
  - The lensing pass renders at 1/1, 1/2 or 1/4 of the view's pixels, then upsamples bilinearly.
  - The level steps down when the Close-up's GPU time stays over 12 ms, using Project 2's `GpuTimer` and
    `QualityGovernor` pattern.
  - The shadow outline overlay is drawn at full resolution from `KerrShadowOutline`, so its size and shape stay exact.
- **P3-10, audio.**
  - `App/Services/IPulseAudio` with `NAudioPulseAudio` (a `WaveOutEvent` playing a generated 44.1 kHz click train: a 2 ms
    raised-cosine click per pulse) and `FakePulseAudio` for tests.
  - If NAudio can't be referenced cleanly on net8.0-windows, use `winmm` `waveOut*` through NAudio's own interop only.
    **Never** P/Invoke for screen capture (unrelated, and banned).
- **P3-11, the settling fade** for a direct-collapse black hole runs in log source time over **1/3 decade** after
  t_BH, so it lasts about 1 s at the default playback speed (spec C9). For a collapsar it follows the spin-up
  (§C8).
- **P3-12, eras.** `CloseUpScene` picks one era from the outcome and source time:
  - `None`: no compact remnant;
  - `PreCollapse`: t < 1 ms;
  - `FirstSecond`: until `CoreCollapseHistory.StageEndS`;
  - `NeutronStar`;
  - `BlackHoleForming`;
  - `BlackHoleSettling`;
  - `BlackHole`.

  The window's title, readouts and drawing all key off the era.
- **P3-13, default black-hole spins until §C8 confirms them:** Slow 0.1, Fast 0.5, Extreme 0.9; any `HasGrb` outcome
  0.9; the collapsar's starting spin 0.5. All `Confidence.Speculative`.

## Pre-flight rulings (before Task 1)

- Ruling S1: Task 5 sets `HasCrust` from a temporary rule (false before NeutronStarModel.EarlyStartS·0+ crust time unknown → use §C5's CrustFormationTimeS if Task 1 gives it, via NeutronStarLayers being created in Task 7) — Task 5 computes HasCrust = sourceTimeS ≥ §C5 crust time read from a constant in NeutronStarModel; Task 7 moves the constant into NeutronStarLayers.CrustFormationTimeS — why: keeps Task 5 self-contained — cost if wrong: one small refactor.
- Ruling S2: tests needing research numbers use one shared test helper `tests/SupernovaLab.Tests/CompactResearch.cs` (static classes C1, C5, C8, C9) created by the first task that needs it (Task 3), extended later — why: one place mirrors compact.md — cost: none.
- Ruling S3: Task 4's `ShowCloseUp_OpensOnce` tests the view-model side (two commands → two service calls); "opens at most once" is asserted in CloseUpWindowTests against the real WindowService if the existing InstrumentsWindowTests pattern allows, else noted — why: plan test name over-promises — cost: none.

## Rulings added during execution

Numbered in the order they were made. The ledger (`project3-ledger.md`) says which task each belongs to.

- Ruling R-P3-1: Cas A — research K1 is right (Heinke & Ho's 2.0–2.1 MK is local; T_s^∞ ≈ 1.4–1.5 MK), so the lab is ~20–30% WARM; spec criterion 3/§4.1/§10/C13 corrected (spec commit after d1cedc9); still disclosed not tuned — why: research is normative and read from the source — cost if wrong: one wording flip. Owner must be told the direction flipped (final report).
- Ruling R-P3-2: Task 3's M87/Sgr A* tests use §C9's replacement tests (Sgr A* off by 2σ is #55, disclosed) — why: plan's test would fail on true physics — cost: none.
- Ruling R-P3-3: Task 12's "period reaches seconds" test applies to the slow-rotation magnetar only; fast/extreme magnetars get a test that their readout tooltip/note discloses #57 and that the label is "Shown N× slower" — why: research K5 — cost: none.
- Ruling R-P3-4: beam ρ = 5.6° P^-1/2 (§C4, #59); Crab tilt 62° via preset magneticInclinationDeg (§C4, K3) — research wins over plan examples.
- Ruling R-P3-5: §C1 early rows (<30 ms) are [APPROX] stand-ins; accepted with labels — first second is "From simulations" anyway.
- Ruling R-P3-6: far cameras — a ray starts at the camera when r_cam ≤ 200 GM/c²; otherwise it is advanced along its straight line to the sphere r = 200 (rays missing that sphere get the weak-field deflection 4/b toward the hole, applied analytically, and escape); R_esc = 200 for far cameras. Mirrored in C# TraceRay and GLSL — why: P3-7's 400×2.0 budget reaches only ~250 GM/c² and the close-up frames up to ~677 — cost if wrong: <0.5° residual bending beyond 200 GM/c² neglected ([APPROX], noted in code).
- Ruling R-P3-7: StepLimit rays (thin rim at the shadow edge) are shaded by sampling the environment along their final momentum (they are photon-ring rays, so the rim reads as the bright photon ring) — why: physically they are near-orbiting light — cost: none.
- Ruling R-P3-8: scene/camera coordinates for the lensing pass are the tracer's chart (outgoing KS of spin +a); φ differs from Boyer–Lindquist by ~a/r (≈1° at r=60, a=0.9), irrelevant to drawing; documented.
- Ruling R-P3-9: keep the Close-up window (no inset); add Task 4b (speed-ups + user setting Close-up ▸ Light-bending detail: Auto/Full/Half/Quarter, persisted in layout.json). Spec §2.5 and plan amended — why: owner's decision; "speed" read as the light-bending detail/performance setting — cost if wrong: menu wording.
- Ruling R-P3-10: cache the traced ray map (depends on camera/spin/viewport/level, not time); retrace only on change; refine to the user's level when the camera stops — why: tracing is the dominant GPU cost and is static while playing — cost: one extra render target.
- Ruling R-P3-11 approved: Close-up drawn on demand; CONDITION for Task 6/8: beams, field lines, light cylinder, pulse trace must join IsAnimating/TimeStepNeedsFrame, and ShowFieldLines/ShowLightCylinder added to _drawnSwitches.
- Ruling R-P3-12 approved: Auto also steps down when interval > 20 ms and GPU > 2 ms.
- Ruling R-P3-13 approved: Auto refines to full when the camera stops; the note shows the displayed level (transiently the previous level right after a change) — acceptable.
- Ruling R-P3-14: governor predicts the next level's interval (meanInterval − meanGpu·(1 − 1/4)) and steps two levels when that is still > 20 ms or meanGpu/4 > 12 ms — why: reaches 1/4 in 3 s, not 6 — cost: may overshoot to 1/4 on a borderline GPU (Auto can't step back up in-session; user can pick Half).
- Ruling R-P3-15: Task 4b may use 1 extra launch (3 total) to confirm refinement — logged as over budget.
- Ruling R-P3-16: one more launch (4th, over budget, logged) combining the refinement confirmation AND a bench of both views (incl. main view alone vs Project 2's 67–70 fps) — why: GlHost loop change touches Project 2's view; a regression must not ship unmeasured — cost: one extra antivirus-visible launch.
- Ruling R-P3-17: Cas A — disclose, don't tune: close-up says the lab's model makes it a pulsar but no pulses have been observed from Cas A's central compact object (new inconsistency #60); kick speed lab 348 vs measured 433 ± 89 km/s (#61, within ~1σ) in the kick tooltip for casa — to be done in Task 6's fix round.
- Ruling R-P3-18: BeamClock accumulates phase frame by frame (dφ = 2π dt / DrawnPeriodS) — why: keeps ≤ 1 crossing/s when the period changes — cost: none.
- Ruling R-P3-19: overlays (field lines, beams, light cylinder, arrow) are not lensed; a note says so — why: spec does not require it; lensing geometry overlays would need the tracer per vertex — cost: slight visual inaccuracy near the star, disclosed.
- Ruling R-P3-20: accept the drip as a rim band on the faces at default zoom (2 px below the surface is the true scale); the "Magnified ×5" strip is where it reads; zooming in separates it — why: to-scale is required by spec §4.2 — cost: none; label/key layout re-checked in Task 14's visual launch.
- Ruling R-P3-21: before shock breakout (EjectaKinematics.BreakoutTimeS) the lensing environment is not the star sphere captured from inside; it is a dim uniform warm haze (schematic) with the note "Inside the star until the shock breaks out ({time}): opaque hot gas surrounds the core; drawn as a dim haze (schematic)" — why: physically the core is surrounded by opaque stellar gas; the pale grey wash is an artefact and black would be wrong — cost: one note, one env mode.
- Ruling R-P3-22: the infall-speed readout is evaluated at the shock radius (§C1 "infall speed at the shock"); where it exceeds 0.3c a note says the Newtonian estimate is not accurate there (as Project 2's 0.3c note) — why: accuracy, follows R28/R-P2-18 precedent — cost: none.
- Ruling R-P3-23: the PNS brightness uses LogBrightness(L_total, peak) at the Close-up's fixed BaseExposure rather than ExposureFor — accepted; the Close-up has its own exposure and the brightness still follows the neutrino total — cost: none.
- Ruling R-P3-24: a step-7b magnetar (e.g. sn2015bn) uses the Exploding track — accepted (spec item 6 concerns black-hole outcomes).
- Ruling R-P3-25: Task 9's remaining visual check (haze 5× dimmer, stronger shock limb; n6946bh1 across t_BH: haze → near-black change) is carried into Task 10's visual launch rather than a third Task-9 launch — why: the owner's antivirus favours few launches and Task 10 renders the same outcomes — cost: a Task-9 look problem is found one task later. Task 10's dispatch must include these checks.
- Ruling R-P3-26: Task 10's visual verification (haze-free surroundings after t_BH, dimmer grid, leader-line labels, mass/spin stepping) is carried into Task 11's launch, as R-P3-25 did — why: fewer launches; Task 11 renders the same black holes — cost: findings one task later.
- Ruling R-P3-27: Task 11's visual check (sn1998bw @6 normal and -disk, @14.5; disk outer edge visible at level 0.6; jets no longer saturated) carried into the next launch (Task 12 or 14) — why: fewer launches — cost: one task later.

### Task 14

- Ruling R-P3-28: the pulse trace's flash-safety test asserts the criterion (no two marks closer than 1/3 s; the mean rate no more than the drawn rate, two crossings per drawn turn), not "every gap ≥ 1 s": with Earth off the spin equator the two beams' crossings are not half a turn apart (sn2015bn at 60 s: 0.983 s and 1.017 s for a 2 s turn). The Close-up's pulse-trace tooltip now says "at most about one mark a second (far below the 3-a-second flash limit)" — why: the geometry is right and the uneven interpulse is real; the old wording over-promised — cost if wrong: one sentence.
- Ruling R-P3-29: Task 14's diagnostics: `--closeup-size WxH[,…]` (the Close-up captured at each size, then back to its default), `--bench-preset id[,…]` (the bench plays the timeline once per preset), an entry's own `+cutaway` now also sets the Close-up's cutaway (and its absence clears it), the Close-up log line lists every note, and with the Close-up open the sweep counts its model errors, a failed Close-up view and a no-remnant view that does not say why, and after the presets plays 14 of the lab's own outcomes (each set from the lab's defaults). A magnetar's burst tab (the gamma panel's first sub-tab, which the sub-tab captures skip) gets its own capture — why: one batched launch had to cover every carried check and criterion 5 — cost: none (diagnostics only).
- Ruling R-P3-30: an "Expected" badge's tooltip (panel and tile) ends with compact.md §C10's reason for that triple (`NeverObserved.Reason` / `ToolTip`), the most specific first: the fate (pair instability), then the channel (νe, νx), then the model — why: §C10 App "Sources for the tooltip: the table's reason column" — cost: wording.
- Ruling R-P3-31: the neutron star's speed note is built by `CloseUpScene.SpeedNote`: slowed, "{label}: the drawn spin never turns faster than once in 2 s, …"; not slowed only because redshift stretches the pulses past 2 s, "Shown at about real speed as received on Earth: the star turns once in P, the redshift stretches its pulses to once in P(1 + z), and …"; otherwise the label alone. The speed label itself is unchanged — why: the old note could read "Shown at real speed: the drawn spin never turns faster than once in 2 s" — cost: wording.
- Ruling R-P3-32: the glossary's new Close-up terms (event horizon, shadow, photon ring, ergosphere, frame dragging, ISCO, gain region, SASI, stalled shock; the drip line, pasta, light cylinder, proto-neutron star and giant flare already existed) reach the screen two ways: a readout whose label names a term starts its label tooltip with the term (`Glossary.CloseUpReadoutTerms`), and a note that names one shows it on hover (`Glossary.CloseUpNoteTerms`, through `CloseUpTermConverter`). The view's own GL labels are not hit-testable, so they rely on the Grid menu's tooltip — why: "every new term has a tooltip" without changing the notes' data type — cost: a phrase list to keep in step with the notes.
- Ruling R-P3-33: the drip label goes on the star's left (ending at a "►" leader, about 6.5 DIP a character, as the black hole's left-hand labels) when, on the right, its box would run under the cutaway key's top-right box (262 × 245 DIP, as Task 14's launch drew it at 900x640); otherwise it stays on the right. `CloseUpSceneController.DripLabelPlacement` — why: the launch showed it hidden under the key at the default size — cost if wrong: the label's estimated width. Confirmed on screen by Task 14b's launch 2.
- Ruling R-P3-34: the gamma panel's lines-tab pointer "Giant flares: see the burst tab." comes first in the panel note, not last — why: the launch showed the note trimmed before it — cost: none. Confirmed on screen by Task 14b's launch 2.
- Ruling R-P3-35 (superseded by R-P3-36 and Task 14b: the cause was the Close-up's readout rows rebuilt every frame, even while hidden; criterion 5 is now met at 1366x768): criterion 5 is reported as not met for SN 1054 and SN 1998bw in Task 14's launch (met for N6946-BH1); the main window alone ran at 34–39 fps there (Task 4b: 70–78 fps for SN 1998bw), the UI thread's WPF render pass at 23–37 ms. The cause is not identified within the two-launch budget; a diagnostic launch (`--bench-baseline` with SN 1998bw on a second launch of the build, then a bisection of Tasks 5–13) is left to the owner — why: the brief allows two launches, and guessing a fix without measuring would be worse — cost: the target stays open.

### Task 14b and the doc round (from the controller's ledger)

- Ruling R-P3-36 (renumbered from R-P3-28 — that number is used in the build record for the uneven-interpulse test): add Task 14b — diagnose and fix the main-view frame-time regression before the final review: instrument per-subsystem UI-thread timing in --bench (ExplosionScene.At, panel/VM updates, Close-up VM, layout/render), up to 2 launches (diagnose, confirm), optional bisect by building an older commit in a temporary detached worktree outside the repo folder (never switch this checkout's branch). Also confirm the three post-launch fixes in the confirming launch — why: Project 2's view must not regress and criterion 5 is a success criterion — cost: 2 more launches (logged).
- Ruling R-P3-37: Task 14's doc fix round waits until Task 14b lands (both touch docs/perf-2026-11.md / README) — why: no concurrent edits — cost: minutes.

- Ruling R-P3-39: keep the main view's giant-flare glow (bounded, flash-limited) — why: spec §1 "the views can never contradict" means a flare drawn in the Close-up must show at the remnant marker too, and plan Task 12 called for it — record it as a spec §7.2 bullet and in the calls doc for the owner to approve or veto — cost if wrong: one renderer change to remove.
- Ruling R-P3-40: ratify the P3-6 deviation — the Close-up environment is Project 2's scene at Low quality from the remnant's position, ignoring the main view's cutaway (a viewing aid, not physical) and quality; it omits the wind haze (disclosed), the jets (drawn separately along +z in the Close-up) and SN 1987A's rings (a simplification). `CloseUpScene.SurroundingsNote` names every omission; rulings and calls docs amended — why: honest record — cost: a slightly simpler sky than "the main view's scene".
- Ruling R-P3-38: Task 14's docs-only fix round is verified by the final whole-branch review instead of a separate scoped re-review — why: docs only, and the final review reads the whole branch — cost: none.

### Task 15 (the owner's first look, 2026-11-16)

- Ruling R-P3-41: "settings that affect the outcome" are the star inputs, the Advanced inputs and choosing a preset (anything that makes a new Outcome, dropping a preset's pins included). They pause playback and move the timeline to t = 0 (`TimelineViewModel.ResetToStart`, not user input on the timeline, so a running tour is not paused by it); the new outcome then ends a running guided tour with its existing note "Tour ended: new explosion". The observer settings (distance, dust, beam toward Earth) and Re-roll do not reset — why: they change what is seen, not what happens — cost: none. Replaces Project 1 spec §2.4's "keeping the current time" (dated note there).
- Ruling R-P3-42: "t = 0" is the timeline's left end (`TimelineViewModel.IsAtStart`: the log axis's 1 ms stands for 0). There the time reads "t = 0 · before core bounce" ("before ignition" for a Type Ia), the main 3D view shows the star before collapse with no shock yet (a Type Ia's white dwarf as its marker; `ExplosionScene.At(t ≤ 0)`, `BeforeExplosionNote`), every instrument badge says "Not yet" with the reason `InstrumentPanelViewModel.BeforeExplosionReason` (a signal the supernova never makes still says "Not produced") and no value is read out, and the Close-up shows the pre-collapse core (it already did: ruling P3-12's PreCollapse era up to 1 ms) — why: Project 1 spec §4 and §6 ("with the scrubber at its left end, the view shows the star before the explosion"); before this the views were drawn at 1 ms after bounce (shock in the star, neutrinos "Detected") — cost: the first millisecond itself is never drawn (one playback step later it is).
- Ruling R-P3-43: the Close-up first opens at the lower-right corner of the work area of the screen that holds the main window, 16 DIP in (`CloseUpPlacement.LowerRight`, pure and tested), unless the owner has moved it this session (the session bounds are remembered as before). WPF only, no P/Invoke: `SystemParameters.WorkArea` is the primary screen's; a main window whose centre is off it is on another screen, so the Close-up goes to the main window's own lower right, held inside the virtual screen (`SystemParameters.VirtualScreen*`; not held in a --snapshot run, whose windows stay off the screen; fix round) — why: the owner's request; no P/Invoke was a build constraint of this task (the owner's antivirus rule is about screen-capture scripts that compile code at run time, not this) — cost: on a second monitor the corner is the main window's, not the screen's; WinForms' `Screen.FromHandle` could give the exact monitor without P/Invoke in our code, if the owner wants it.
- Ruling R-P3-44 (the controller's, accepting the implementer's call): the pulsar sound also plays while the timeline is paused, at the pulsar's real received period at the shown age, matching the beams and the pulse trace, which keep going while paused (C3) — why: the owner could not get any sound; paused is when one listens — cost: one condition to restore if the owner prefers silence when paused (`&& Main.Timeline.IsPlaying` in `CloseUpViewModel.UpdatePulses`, with its `IsPlaying` listener, and the two tests in `PulseAudioTests` that said so). Note: the sound switch and volume are kept for the session (`WindowService`), so if the sound was on when the Close-up closed, reopening it (by the user or by the guided tour's first Close-up stop) resumes the clicks wherever pulses reach Earth; the status line at the top of the window shows the sound is on.
