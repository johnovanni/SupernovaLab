# Project 3 build ledger (condensed)

A condensed copy of the build's ledger (`.superpowers/sdd/2026-11-15-supernovalab-project3/progress.md`): the pre-flight scan, then one line per step of each task (dispatch, the implementer's report, each review, each fix round, deferred minors and the rulings, which are given in full in `project3-rulings.md`). Agent ids are left out.

Spec: docs/superpowers/specs/2026-11-15-supernovalab-project3-design.md (reachable). Branch: project3 (main checkout, not a worktree — as Projects 1–2 were built). Start HEAD cc9df21. Owner approved spec and plan 2026-11-15 ("Yes, start building").

## Pre-flight scan

| Tasks | Shared file / interface | Producer vs consumer | Finding |
|---|---|---|---|
| 1 → 2,3,5,7,9,10,11,12 | compact.md §C1–§C11 App blocks | 1 produces numbers; later tasks consume via test helper constants (C1, C5, C8, C9) | OK — each consuming task encodes its section's numbers in a test helper class |
| 2 ↔ 13 | TourBuilder.cs / TourCaptions.Expected | 2 adds Expected prefix; 13 adds close-up stops using it | OK, sequential |
| 2 ↔ 5 | presets.json | 2 edits sn1987a whatWasSeen; 5 adds kick/inclination fields | OK, different keys |
| 3 → 4,6,10,11 | BlackHoleOptics.TraceRay, Vec3 | GLSL mirror in 4 must match C# in 3 | OK; plan P3-7 fixes one algorithm |
| 4 → 6,7,9,10,11,12 | CloseUpScene / CloseUpState | 4 creates with None + placeholder BH; later tasks add eras/fields | OK |
| 4 ↔ 6 | SceneController.cs (BeamClock), ExplosionScene | 4 swaps _wall for BeamClock; 6 adds offset/tilt/width | OK |
| 4 ↔ 8 ↔ 13 | CloseUpWindow.xaml, CloseUpViewModel | 4 shell; 8 sound/trace; 13 outline highlight | OK |
| 4 ↔ 10 ↔ 11 | lensing.frag | 4 tracer; 10 grid sphere; 11 disk annulus | OK |
| 5 ↔ 7 | NeutronStarModel.HasCrust | 5 declares field; 7 fills from CrustFormationTimeS | OK — 5 sets HasCrust=false placeholder? Ruling S1 |
| 5 → 6,8,11 | Magnetosphere, KickDrift | signatures consistent across tasks | OK |
| 9 → 10 | CoreCollapseHistory (formation) | 10 uses Forming from t_BH | OK |
| 10 ↔ 11 | BlackHoleModel | 10 creates; 11 fills HasDisk/DiskOuterGm + fallback | OK |
| 12 ↔ 7,14 | Glossary.cs | additive | OK |
| 14 | all | validation re-asserts | OK |
| Task 1 self | docs only | no tests | OK |
| Task 2 self | tests vs code | "Over: expected" short form vs OverWords full "Over: expected to be detected" | consistent |
| Task 3 self | M87/SgrA tests reference C9 constants | helper must exist | Ruling S2 |
| Task 4 self | ShowCloseUp_OpensOnce asserts 2 calls on the fake | name vs assertion | Ruling S3 |
| Task 5 self | Crab label "Shown 60× slower" from 2/0.0334=59.9 rounded to 2 s.f. | consistent | OK |
| Tasks 6–14 self | tests match interfaces | checked names (SpeedLabel, SurfaceColor, ShowBeams, Readout labels) | OK |

Rulings:
- Ruling S1: Task 5 sets `HasCrust` from a temporary rule (false before NeutronStarModel.EarlyStartS·0+ crust time unknown → use §C5's CrustFormationTimeS if Task 1 gives it, via NeutronStarLayers being created in Task 7) — Task 5 computes HasCrust = sourceTimeS ≥ §C5 crust time read from a constant in NeutronStarModel; Task 7 moves the constant into NeutronStarLayers.CrustFormationTimeS — why: keeps Task 5 self-contained — cost if wrong: one small refactor.
- Ruling S2: tests needing research numbers use one shared test helper `tests/SupernovaLab.Tests/CompactResearch.cs` (static classes C1, C5, C8, C9) created by the first task that needs it (Task 3), extended later — why: one place mirrors compact.md — cost: none.
- Ruling S3: Task 4's `ShowCloseUp_OpensOnce` tests the view-model side (two commands → two service calls); "opens at most once" is asserted in CloseUpWindowTests against the real WindowService if the existing InstrumentsWindowTests pattern allows, else noted — why: plan test name over-promises — cost: none.

Models: T1 opus (research), T3/T4/T10 opus (GR, GL integration), others sonnet; task reviewers sonnet (opus for T3/T4/T10); final review opus.

## Progress
Task 1: dispatched (opus) at base cc9df21 aa5f937fc9b457049
Task 1: implementer DONE_WITH_CONCERNS (d1cedc9). Concerns ruled:
- Ruling R-P3-1: Cas A — research K1 is right (Heinke & Ho's 2.0–2.1 MK is local; T_s^∞ ≈ 1.4–1.5 MK), so the lab is ~20–30% WARM; spec criterion 3/§4.1/§10/C13 corrected (spec commit after d1cedc9); still disclosed not tuned — why: research is normative and read from the source — cost if wrong: one wording flip. Owner must be told the direction flipped (final report).
- Ruling R-P3-2: Task 3's M87/Sgr A* tests use §C9's replacement tests (Sgr A* off by 2σ is #55, disclosed) — why: plan's test would fail on true physics — cost: none.
- Ruling R-P3-3: Task 12's "period reaches seconds" test applies to the slow-rotation magnetar only; fast/extreme magnetars get a test that their readout tooltip/note discloses #57 and that the label is "Shown N× slower" — why: research K5 — cost: none.
- Ruling R-P3-4: beam ρ = 5.6° P^-1/2 (§C4, #59); Crab tilt 62° via preset magneticInclinationDeg (§C4, K3) — research wins over plan examples.
- Ruling R-P3-5: §C1 early rows (<30 ms) are [APPROX] stand-ins; accepted with labels — first second is "From simulations" anyway.
- Note: plan's "R-P3-1 for the inset fallback" — execution rulings are numbered sequentially; the inset ruling, if needed, takes the next free number.
Task 1: review 1 (opus) Needs fixes: I1 GM☉/c² constant 1.47670 should be 1.47700 (lab constants) / computed from PhysicalConstants; I2 no rule for fallback BH 1 s→t_BH; ⚠️ confirmed gap: position-angle direction convention (east of north) vs scene angle.
Task 1: minor (deferred): casa kick 158° heliocentric vs 151° corrected — mention choice
Task 1: minor (deferred): Shternin numbers differ (1.53 vs 1.55 M☉; logT 6.23 vs 6.22; 1.39–1.44 vs 1.45 MK)
Task 1: minor (deferred): §C6 2004 flare spike energy rounding / distance mix
Task 1: minor (deferred): #58 "about 1.5×" should be 1.6–1.8×
Task 1: minor (deferred): §C10 neutrino "*" BlackHole triple also marks fallback first 3 s — say deliberate
Task 1: minor (deferred): §C10 add M31-2014-DS1 [R] as failed-SN candidate
Task 1: fix round 1/5 (3 addressed, 0 open — GM☉/c², fallback BH rule, PA convention; commits 0ed0826..6d6e096)
Task 1: minor (deferred): §C2 fallback reasoning quotes "minutes to hours" but lab t_BH = 3 s (FallbackFormationTime, Speculative) — candidate inconsistency entry in Task 14
Task 1: complete (commits cc9df21..6d6e096, review clean)
Task 2: dispatched (sonnet) at base 6d6e096
Task 2: implementer DONE_WITH_CONCERNS (3b03519): tour test rewritten (GW stop merges into neutrino stop); citation inline in whatWasSeen; DUNE neutrino badge says Expected; stash used for RED demo (stash list empty after). Controller ran full suite: 2269/2269 pass.
Task 2: review (sonnet) Approved, spec ✅.
Task 2: minor (deferred): merged GW/neutrino tour stop — Expected sentence mid-caption
Task 2: minor (deferred → Task 14 polish): Expected badges lack the §C10 reason tooltip (compact.md §C10 App line ~765)
Task 2: minor (deferred): badge judges selected instrument, so lum_nue/lum_nux sub-tabs show Detected on non-DUNE instruments — known limitation
Task 2: minor (deferred): whatWasSeen citation inline (no sources mechanism)
Task 2: complete (commits 6d6e096..3b03519, review clean)
Task 3: dispatched (opus) at base 3b03519
Task 3: implementer DONE_WITH_CONCERNS (6d60b0b): 2300/2300. Concerns: compact §C7 outline table off ≤1.3e-3 (test tol 0.002); far cameras exceed P3-7 budget (>~250 GM/c²) → rule needed for Task 4; StepLimit rim at shadow edge; time-reversal via spin −a; internal Trace with bigger budget for tests; spin shadow diameter uses edge-on mean; surface hit within one step.
Task 3: review (opus) Approved, spec ✅ (time-reversal = past-directed tracing in outgoing KS(+a), handedness verified).
Task 3: minor (deferred → carried into Task 4): doc wording "outgoing KS(+a)"; add Kerr test at shader 400-step budget (a=0.9, i=90°, camera 50); NeverNaN far case a=0.5 cam 1000 R_esc 1200
Task 3: minor (deferred → Task 14): log compact.md §C7 outline-table error (α max 6.8323 vs 6.8316 etc.) as a research correction
Task 3: minor (deferred): spinning ApparentShadowDiameterRad uses edge-on Johannsen–Psaltis mean — add to calls list
Task 3: minor (deferred): pole-on circle test checks a range, not §C7 value
Task 3: complete (commits 3b03519..6d60b0b, review clean)
- Ruling R-P3-6: far cameras — a ray starts at the camera when r_cam ≤ 200 GM/c²; otherwise it is advanced along its straight line to the sphere r = 200 (rays missing that sphere get the weak-field deflection 4/b toward the hole, applied analytically, and escape); R_esc = 200 for far cameras. Mirrored in C# TraceRay and GLSL — why: P3-7's 400×2.0 budget reaches only ~250 GM/c² and the close-up frames up to ~677 — cost if wrong: <0.5° residual bending beyond 200 GM/c² neglected ([APPROX], noted in code).
- Ruling R-P3-7: StepLimit rays (thin rim at the shadow edge) are shaded by sampling the environment along their final momentum (they are photon-ring rays, so the rim reads as the bright photon ring) — why: physically they are near-orbiting light — cost: none.
- Ruling R-P3-8: scene/camera coordinates for the lensing pass are the tracer's chart (outgoing KS of spin +a); φ differs from Boyer–Lindquist by ~a/r (≈1° at r=60, a=0.9), irrelevant to drawing; documented.
Task 4: dispatched (opus) at base 6d60b0b (carries R-P3-6..8 and Task 3 minors)
Task 4: implementer DONE_WITH_CONCERNS (f4ae536, 98f7582, a7ac91d): 2343/2343. SPEED TEST on Iris Xe: lensing 1/2 → 40.1 fps (1366) / 37.9 (1080p); 1/4 → 62.4 / 53.4; 1/1 → 23.0 / 21.5. Plan threshold (≥50 at 1/2) missed; spec §2.5 threshold ('after reduced-resolution lensing') met at 1/4. Per spec: reported to owner before Task 5. Env ignores cutaway/quality triggers (implementer's call).
Task 4: review (opus) Approved with 2 Important: I1 perf notes state the ~11 ms cause (WPF composition) as fact — unmeasured, label hypothesis + propose baseline run; I2 camera can zoom inside horizon/ergosphere/NS surface — clamp ≥ max(3 GM/c², 1.1 R_surface) + test.
Task 4: minor (deferred → ruling at end): P3-6 deviation (cutaway/quality triggers omitted; jets & SN1987A torus rings missing from env) — to ratify
Task 4: minor (deferred): far-camera seam at b≈200 (~1.15° jump) — optional analytic tail beyond R_esc
Task 4: minor (deferred): SurroundingsNote "bent exactly" → "computed from general relativity"
Task 4: minor (deferred): owned-window hiding when main minimized may not stop drawing — check
Task 4: minor (deferred): vm.Dispose twice (idempotent)
Task 4: fix round 1/5 (2 addressed, 0 open — perf hypothesis + --bench-baseline flag; zoom clamp ≥ max(3GM/c², 1.1R); commits a7ac91d..707b56f). 2345/2345.
Task 4: complete (commits 6d60b0b..707b56f, review clean). Launches used: 1.
PAUSED: awaiting owner's decision on speed-test outcome (window + speed-ups / inset / accept 1/4) per spec §2.5. Next: if speed-ups → one --bench-baseline launch, then a targeted speed-up task before Task 5.
OWNER DECISION (2026-11-15, in chat): "Try the speedups. Also, let the user set the speed" (because users' graphics differ).
- Ruling R-P3-9: keep the Close-up window (no inset); add Task 4b (speed-ups + user setting Close-up ▸ Light-bending detail: Auto/Full/Half/Quarter, persisted in layout.json). Spec §2.5 and plan amended — why: owner's decision; "speed" read as the light-bending detail/performance setting — cost if wrong: menu wording.
- Ruling R-P3-10: cache the traced ray map (depends on camera/spin/viewport/level, not time); retrace only on change; refine to the user's level when the camera stops — why: tracing is the dominant GPU cost and is static while playing — cost: one extra render target.
Task 4b: dispatched (opus) at base 94c0bef
Task 4b: implementer DONE_WITH_CONCERNS (c96cbee): 2368/2368, 2 launches. Full still 77.7 fps (1366) / 71.9 (1080p); Auto orbiting 51.5 avg / 58.5 second half (1366), 48.9 / 57.1 (1080p). Baseline: ~10–11 ms UI-thread WPF render pass; GL controls block UI thread on GPU. Proposed R-P3-11 on-demand drawing, R-P3-12 Auto steps down below 50 fps, R-P3-13 Auto refines to full when camera stops. Concerns: refinement after stop not observed in-app (0 bands/120 frames) — cause unknown; governor ~6 s to reach 1/4; main-view fixed cost untouched.
Task 4b: review (opus) Needs fixes: I1 refinement-after-stop unverified in app, no controller-path test (FakeHost never reaches DrawLensing) — add seam/ILensingPass test + diagnose (0 frames vs band failures) + confirm on one launch; I2 governor 6 s to reach 1/4 → predictive two-level step.
- Ruling R-P3-11 approved: Close-up drawn on demand; CONDITION for Task 6/8: beams, field lines, light cylinder, pulse trace must join IsAnimating/TimeStepNeedsFrame, and ShowFieldLines/ShowLightCylinder added to _drawnSwitches.
- Ruling R-P3-12 approved: Auto also steps down when interval > 20 ms and GPU > 2 ms.
- Ruling R-P3-13 approved: Auto refines to full when the camera stops; the note shows the displayed level (transiently the previous level right after a change) — acceptable.
- Ruling R-P3-14: governor predicts the next level's interval (meanInterval − meanGpu·(1 − 1/4)) and steps two levels when that is still > 20 ms or meanGpu/4 > 12 ms — why: reaches 1/4 in 3 s, not 6 — cost: may overshoot to 1/4 on a borderline GPU (Auto can't step back up in-session; user can pick Half).
- Ruling R-P3-15: Task 4b may use 1 extra launch (3 total) to confirm refinement — logged as over budget.
Task 4b: minor (fold into fix round): GPU timer Begin before RefreshEnvironment charges cube capture to rays.
Task 4b: minor (deferred): IsAnimating drops _envPending while playing — fine today.
Task 4b: fix round 1 (75de4a6): 2375/2375; seam tests via ILensingPass; cause found — GLWpfControl 4.3.6 honours RenderContinuously only after hide/show → GlHost now runs its own per-frame loop (affects main view too, unmeasured); R-P3-14 implemented; capture frames excluded from cost; field-lines/light-cylinder switches added. Launches 3/3 used.
- Ruling R-P3-16: one more launch (4th, over budget, logged) combining the refinement confirmation AND a bench of both views (incl. main view alone vs Project 2's 67–70 fps) — why: GlHost loop change touches Project 2's view; a regression must not ship unmeasured — cost: one extra antivirus-visible launch.
Task 4b: R-P3-16 done (a32cc08): refinement confirmed in app (1/1, 13 bands, 0 failed, host not continuous); main view alone 70.6–78.2 fps (1366), 75 (1080p) — no regression; both views Full still 77.8 / Auto orbit 53.8 avg (1366); 1080p Auto orbit 48.0 avg (report-only per criterion 5). Launches 4 (one 9 s aborted start on a stale build).
Task 4b: fix round 1/5 (3 addressed, 0 open — seam+refinement confirmed, R-P3-14, capture frames tagged; commits c96cbee..a32cc08)
Task 4b: minor (deferred → carry into Task 6): GlHost owed frames stuck while hidden (idle composition loop); no _shutdown guard so Animating/RequestRender after Shutdown resubscribes static CompositionTarget.Rendering (leak); an always-failing band loops forever
Task 4b: minor (deferred → carry into Task 6): SceneController.CaptureFrameAsync lacks RunContinuationsAsynchronously fix; log label "composition frames" counts NextFrame waits
Task 4b: complete (commits 94c0bef..a32cc08, review clean). Launches 4 (over budget by 2; logged).
Task 5: dispatched (sonnet) at base a32cc08 (carries R-P3-1, R-P3-4, PA mapping, S1)
Task 5: implementer DONE (dd86360): 2418/2418. Notes: casa sceneDisclosures [46,54]; ρ from birth period for Earth placement; 'jets' = non-NS remnant.
Task 5: review (sonnet) Approved with Important 1: CasAComparison prints at any age (meaningless at 10 yr) and keys on Discloses(54) — restrict to a window around ~340 yr + test.
Task 5: minor (deferred → Task 6): NeutronStarModel.At returns bare doubles — Task 6 must wrap as Quantities with Confidence+Source
Task 5: minor (deferred): SurfaceTemperature before 60 s returns held 60-s value without a marker
Task 5: fix round 1/5 (3 addressed, 0 open — CasA window 300–380 yr, local label, ToPinSource; commits dd86360..bd52bb4). 2419/2419.
Task 5: minor (deferred): CasA window hard-coded, not from preset age
Task 5: complete (commits a32cc08..bd52bb4, review clean)
Task 6: dispatched (opus) at base bd52bb4 (carries R-P3-11 condition, Task 4b GlHost minors, Task 5 Quantity wrap)
Task 6: implementer DONE_WITH_CONCERNS (b3ce1b7): 2455/2455, 1 launch. Retuned visuals after launch (unverified). BeamClock accumulates phase (flash-safe with changing period). Overlays not lensed (noted). Cas A titled pulsar though no pulsations observed; Cas A kick 348 vs measured 433 km/s.
- Ruling R-P3-17: Cas A — disclose, don't tune: close-up says the lab's model makes it a pulsar but no pulses have been observed from Cas A's central compact object (new inconsistency #60); kick speed lab 348 vs measured 433 ± 89 km/s (#61, within ~1σ) in the kick tooltip for casa — to be done in Task 6's fix round.
- Ruling R-P3-18: BeamClock accumulates phase frame by frame (dφ = 2π dt / DrawnPeriodS) — why: keeps ≤ 1 crossing/s when the period changes — cost: none.
- Ruling R-P3-19: overlays (field lines, beams, light cylinder, arrow) are not lensed; a note says so — why: spec does not require it; lensing geometry overlays would need the tracer per vertex — cost: slight visual inaccuracy near the star, disclosed.
Task 6: review (opus) Needs fixes (small): I1 2.5 R framing puts camera (~11 R) inside outer field-line shells (Crab L=12R, magnetar 24/48R) → cap shells / more planes; verify in a launch.
Task 6: minor (fold into fix round): title test passes by construction; silent direct-tracing fallback (add note); GlHost._controlVisible initial true; "To Earth" label vanishes when arrow tip off-screen
Task 6: minor (deferred): BeamPhaseRad getters mutate shared clock (UI-thread only; document); ExplosionScene.RunNumber mutable setter
Task 6: fix round 1 (c24ce1f): 2459/2459, 1 launch (2 total for Task 6). Shells capped ≤3.75 R; Cas A title/kick disclosures #60/#61. NOTE: implementer attempted a PowerShell Add-Type (to crop a PNG) — denied by permission check, nothing ran. Future dispatches must forbid Add-Type for any purpose.
Task 6: minor (deferred): disc still bright (grid/polar-cap faint); beam cyan close to surroundings' light blue; #60 CCO weak-field claim [R]
Task 6: fix round 1/5 (6 addressed, 0 open; commits b3ce1b7..c24ce1f)
Task 6: minor (deferred): EdgeDip behind-camera branch may mirror the side (unverified)
Task 6: minor (deferred): NoPulsesSeenNote shows during proto-NS phase before beams form
Task 6: minor (deferred → final review): open polar-cap lines run to the light cylinder (~1,600 km) and pass the camera's radius — check for streaks in a later launch
Task 6: complete (commits bd52bb4..c24ce1f, review clean). Launches 2.
Task 7: dispatched (sonnet) at base c24ce1f
Task 7: implementer DONE_WITH_CONCERNS (7e5efe7): 2484/2484; 1 launch but pre-crust times only — crust cutaway never seen; cutaway = unlensed overlay, lensed disc shaded black (dark ring around, noted); field lines/beams cut inside notch.
Task 7: review (sonnet) Needs fixes (small): I1 drip ring / boundary widths are fractions of R → hairline at default zoom; add pixel floor (≥2 px drip ring); + verification launch at a post-crust time with checks (a)–(f).
Task 7: minor (fold into fix round): notch-only black instead of blackening the lensed disc (removes dark ring); cutaway toggle overwrites user's pitch; Glossary CloseUpLabels formatting slip
Task 7: minor (deferred): DripDepth/Crust Quantities without mass-range band; cutaway silhouette not anti-aliased
Task 7: fix round 1 (f622edb): 2486/2486; launches 2 total. Drip ring ≥2 px but at default zoom (star ~65 px) it is a rim band — physical; the magnified strip carries it. Label/key moved after launch (unverified).
- Ruling R-P3-20: accept the drip as a rim band on the faces at default zoom (2 px below the surface is the true scale); the "Magnified ×5" strip is where it reads; zooming in separates it — why: to-scale is required by spec §4.2 — cost: none; label/key layout re-checked in Task 14's visual launch.
Task 7: fix round 1/5 (4 addressed, 1 partial→ruled: layout re-check deferred to Task 14 launch at 900x640 sn1054@1000000; commits 7e5efe7..f622edb)
Task 7: minor (deferred): nscut.frag duplicates LineWidth floors as literals (pin via ShaderSourceTests); Home during cutaway then toggle restores older pitch
Task 7: minor (deferred → Task 14 visual): drip label/key at 900x640 borderline overlap
Task 7: complete (commits c24ce1f..f622edb, review clean)
Task 8: dispatched (sonnet) at base f622edb
Task 8: implementer DONE (fca041b): 2526/2526, no launch. Deviations: IPulseAudio.Problem; 1 kHz carrier under 2 ms raised-cosine; miss-Earth reported before hidden; second mark only if opposite beam reaches Earth; PulseTrace code-only; sound not persisted.
Task 8: review (sonnet) Needs fixes: I1 failed audio device retried every tick (latch failure); I2 RateText rounding prints "3 a second" for 2.5 (decimals from scale; check SpeedLabel).
Task 8: minor (fold): Glossary should call the click "a schematic click"; "shown N× slower" vs received period differs by (1+z) — divide by TimeDilation; PlaybackStopped not handled (device unplugged); Disable via Pause leaves stale queued click
Task 8: minor (deferred): volume zipper noise; TraceText "Sound:" when off; recorder List allocation per frame
Task 8: fix round 1/5 (6 addressed, 0 open; commits fca041b..6f157a7). 2545/2545.
Task 8: minor (deferred): CloseUpScene "never faster than once in 2 s" note may contradict "Shown at real speed" under redshift; _problem cleared after Play() race (message could be lost); 1/0.2 test case mislabelled
Task 8: complete (commits f622edb..6f157a7, review clean)
Task 9: dispatched (opus) at base 6f157a7
Task 9: implementer DONE_WITH_CONCERNS (4cb0efb): 2584/2584, 1 launch. Shock glow/shape retuned after launch (unverified); env from inside unexploded star = washed pale grey from ~1 s; infall √(GM/r) 0.3–0.5c unflagged; failed test uses tbh−0.5 ms; logged #62 (O-Ne-Mg uses iron-core radii), #63 (BH size jump ~3× at t_BH, for Task 10).
- Ruling R-P3-21: before shock breakout (EjectaKinematics.BreakoutTimeS) the lensing environment is not the star sphere captured from inside; it is a dim uniform warm haze (schematic) with the note "Inside the star until the shock breaks out ({time}): opaque hot gas surrounds the core; drawn as a dim haze (schematic)" — why: physically the core is surrounded by opaque stellar gas; the pale grey wash is an artefact and black would be wrong — cost: one note, one env mode.
- Ruling R-P3-22: the infall-speed readout is evaluated at the shock radius (§C1 "infall speed at the shock"); where it exceeds 0.3c a note says the Newtonian estimate is not accurate there (as Project 2's 0.3c note) — why: accuracy, follows R28/R-P2-18 precedent — cost: none.
Task 9: review (opus) Needs fixes: I1 R-P3-21 haze + R-P3-22 >0.3c note not implemented; I2 retuned shock/distortion unverified — launch owed.
- Ruling R-P3-23: the PNS brightness uses LogBrightness(L_total, peak) at the Close-up's fixed BaseExposure rather than ExposureFor — accepted; the Close-up has its own exposure and the brightness still follows the neutrino total — cost: none.
- Ruling R-P3-24: a step-7b magnetar (e.g. sn2015bn) uses the Exploding track — accepted (spec item 6 concerns black-hole outcomes).
Task 9: minor (fold): drawn distortion rms below the stated 10% (raise SHAPE_SCALE or fix comment); horizon must use gravitational mass (6.50 km for 2.2 M☉) not baryonic 2.563; particles drawn under surfaces (far particles, surfaces, near particles); ShockAlpha comment inverted; CoreMoves redraws during fallback Cooling; distortion amplitude ModelDependent (shape schematic); failed-track infall 30% fast early — disclose with #63
Task 9: fix round 1 (f4d1112): 2589/2589; launch 2 for Task 9. Haze dimmed 5× after launch (unverified). Failed SN: inside-star haze until BH forms, then usual surroundings. Light cylinder edge-on bright line at 1–2 s (Task 6 origin).
Task 9: fix round 1/5 (7 addressed, 1 open — post-launch haze dimming/limb unverified; commits 4cb0efb..f4d1112). 2589/2589.
- Ruling R-P3-25: Task 9's remaining visual check (haze 5× dimmer, stronger shock limb; n6946bh1 across t_BH: haze → near-black change) is carried into Task 10's visual launch rather than a third Task-9 launch — why: the owner's antivirus favours few launches and Task 10 renders the same outcomes — cost: a Task-9 look problem is found one task later. Task 10's dispatch must include these checks.
Task 9: minor (deferred → Task 10): after breakout the environment is the ejecta shell seen from inside — check its look
Task 9: minor (deferred): light cylinder edge-on bright line at 1–2 s (Task 6 origin)
Task 9: complete (commits 6f157a7..f4d1112, 1 check carried to Task 10)
Task 10: dispatched (opus) at base f4d1112 (carries R-P3-25 check, #63)
Task 10: implementer DONE_WITH_CONCERNS (42c06c5): 2632/2632, 1 launch (38 PNGs). Post-launch unverified: wind haze dropped from env capture for all outcomes (cause of failed-SN grey wash after t_BH); grid dimmer; labels on leader lines. New #64 (spin 0.998 shadow 1.2% wide), #65 (coordinate camera ~5% larger than static observer at 19 GM/c², noted). 24-step spin quantisation (≤25 retraces/settling).
Task 10: review (opus) Needs fixes: I1 #64's cause is wrong (it's the RK4 step floor near a→1, not the capture margin — reviewer's scratch check: step 0.005 → 0% error); I2 wind-haze omission not disclosed on screen; I3 post-launch changes unverified.
- Ruling R-P3-26: Task 10's visual verification (haze-free surroundings after t_BH, dimmer grid, leader-line labels, mass/spin stepping) is carried into Task 11's launch, as R-P3-25 did — why: fewer launches; Task 11 renders the same black holes — cost: findings one task later.
Task 10: minor (fold): Shadow/From Earth readouts use outcome mass confidence instead of the growing mass's (Speculative); ergosphere label at spin 0.1 points ~0.005 GM/c² from horizon — label only above spin ~0.3
Task 10: minor (deferred): left-label x offset heuristic; grid lines 4 px at quarter res; brief Step 3 shrink handled by Task 9 collapse; Spin readout shown in all eras (justified)
Task 10: fix round 1/5 (4 addressed, 0 open; #64 fixed via step floor clamp(16·(r_ph+−r₊),0.02,0.5); commits 42c06c5..e2294c3). 2639/2639.
Task 10: minor (deferred): GLSL step floor lacks abs(spin) (only matters for spin < −0.987, never produced); BlackHoleFixRound1Tests inserted under LensOverlayTests' doc comment
Task 10: complete (commits f4d1112..e2294c3, review clean; visual check carried to Task 11 per R-P3-26)
Task 11: dispatched (opus) at base e2294c3 (carries Task 9 + Task 10 visual checks)
Task 11: implementer DONE_WITH_CONCERNS (e5a3e4f): 2661/2661, 1 launch (29 PNGs). Task 10 carried check confirmed (black surroundings after t_BH, labels apart). Post-launch brightness cuts (jets/disk/grid) unverified; shock limb (Task 9) weak, left. New #66 (thin disk vs MW99 thick torus), #67 (the two views' jet timing differ for SN 1998bw).
Task 11: review (opus) Approved with fixes: I1 #67 misstates the burst (T90 23.3 s, main view draws 3×T90; real mismatch = MW99 engine stops ~4.5 s after lab onset 11.5 s) and the on-screen note must be built from Core values; I2 post-launch level cuts unverified.
- Ruling R-P3-27: Task 11's visual check (sn1998bw @6 normal and -disk, @14.5; disk outer edge visible at level 0.6; jets no longer saturated) carried into the next launch (Task 12 or 14) — why: fewer launches — cost: one task later.
Task 11: minor (fold): #66 camera distance text vs logged 67 GM/c² (~1.5%); −z jet shows through the opaque disk (clip or caveat); u^t citation (BPT eq. 2.12 gives E, L; cite Page & Thorne 1974 or derive); DiskEndedNote shown forever — limit to early settled era
Task 11: minor (deferred): exact z=0 crossing missed (measure zero) — note in class docs
Task 11: fix round 1/5 (5 addressed, 0 open; commits e5a3e4f..f5121a9). 2664/2664.
Task 11: minor (deferred): collapsar with JetGrb.Applies false gets no jets note while the disk shows (check if possible)
Task 11: complete (commits e2294c3..f5121a9, review clean; visual check carried per R-P3-27)
Task 12: dispatched (sonnet) at base f5121a9 (carries R-P3-3, R-P3-27 check)
Task 12: implementer DONE_WITH_CONCERNS (6d2da04): 2715/2715, 1 launch. Task 11 carried check OK (jets translucent, disk edge visible, −z jet hidden). Flare peak blows the Close-up white with bloom (one flash). Gamma burst-tab spike not captured. #68 (rate from old magnetars), #69 (assumed Band E_p 500 keV).
Task 12: review (sonnet) Needs fixes: I1 flare cap applies to the 0..1 request, then ×7 surface / ×5 beam gain pre-bloom → full white-out; cap output luminance (gains ~2 / ~1.5, bloom bounded, tone-mapped peak ≤0.85 test, bounded per-frame step); I2 FlashLimiter.Reset never called.
Task 12: minor (fold): slew the limiter's fall (≥0.15 s); select the GRB tab (or a pointer) when flares apply for a magnetar; RatePerYear tests with tolerance; note "flare dimmed by the flash-safety limit" above ~1 decade/s
Task 12: fix round 1/5 (5 addressed, 1 new Important — Reset on every Outcome recompute zeroes and re-allows an immediate rise → slider drag during a flare can flicker unspaced; note shown only in lensed branch; commits 6d2da04..5b7a481)
Task 12: fix round 2/5 (4 addressed, 0 open; commits 5b7a481..253758a). 2724/2724.
Task 12: minor (deferred): held flare output can ghost ≤0.15 s into a new run without flares (bounded fade); tautological / weak assertions in two FlashLimiter tests; marker glow pin is string-matching
Task 12: complete (commits f5121a9..253758a, review clean; flare look checked in Task 14 launch)
Task 13: dispatched (sonnet) at base 253758a
Task 13: implementer DONE (4190abe): 2774/2774, no launch; tests after code (no RED). Bounce stop at 10 ms (merges with ν/GW stop); merged mixed stops never skipped; first flare only; pulse sentence repeats when merged.
Task 13: review (sonnet) Needs fixes: I1 Re-roll mid-tour leaves a stale flare stop (director listens only to OutcomeChanged); I2 outline unseen → Task 14 visual launch.
Task 13: minor (fold): Resume can land on a skipped close-up-only stop; add a test pinning the bounce merge (10 ms marker)
Task 13: minor (deferred): merged stops keep close-up sentences after the window is closed; GoTo doesn't skip close-up-only stops; pulse/bounce sentence repetition; first flare only
Task 13: fix round 1/5 (3 addressed, 0 open; commits 4190abe..be96b61). 2778/2778.
Task 13: minor (deferred): TourDirector subscriptions never removed (lifetime-owned; fine)
Task 13: complete (commits 253758a..be96b61, review clean; outline checked in Task 14 launch)
Task 14: dispatched (opus) at base be96b61 (carried items in task-14-carried.md)
Task 14: carried small fixes: Expected badges' §C10 reason tooltip (R-P3-30); compact.md K9 (the §C7 Kerr-outline table's α ends, tolerances unchanged); inconsistency #70 (fallback t_BH 3 s vs minutes to hours); nscut.frag widths and floors pinned in ShaderSourceTests; speed note under redshift (R-P3-31); NoPulsesSeenNote only once beams exist.
Task 14: Project3ValidationTests (criteria 1–4, 6; every readout's confidence and source over all eras; glossary keys) and FlashSafetyTests; R-P3-28 (uneven interpulse: the trace test asserts the 3 Hz criterion and the mean rate); glossary terms and their tooltips (R-P3-32).
Task 14: launch 1/2 `--sweep 40 --closeup` (14 presets + 14 lab outcomes, Close-up open, its errors counted; R-P3-29): 0 problems.
Task 14: launch 2/2 batched `--snapshot … --closeup-size 900x640,1366x768 --tour 2 --bench 30 --bench-preset sn1054,n6946bh1,sn1998bw --bench-detail auto --bench-camera still,orbit` (every carried visual check and criterion 5).
Task 14: visual check failures fixed after the launch (unverified on screen; no launch left): the drip label ran under the cutaway key at 900x640 (now on the star's left where it would; controller test); the tour's Close-up outline never lit (a local BorderBrush beat the style trigger; window test); the gamma lines tab's "Giant flares: see the burst tab." pointer was cut off by the note's trimming (now first; test).
Task 14: launch 2 results: visual checks 1 (900x640 drip label under the key), 3 (lines-tab pointer cut off) and 4 (tour outline never lit; the log said HighlightCloseUp True) FAILED and were fixed after the launch with tests (R-P3-33, R-P3-34; outline: style setter instead of a local BorderBrush); 2, 5 (limb faint), 6 and the rest passed. Criterion 5: N6946-BH1 63.8 fps still / 52.3 orbiting at 1366x768 (met); SN 1054 29.6 / 24.7 and SN 1998bw 33.9 / 31.1 (not met), with the Close-up hidden 34–39 fps: the main window's UI-thread render pass (23–37 ms) is the cost, not the Close-up (R-P3-35, open for the owner; this reading was wrong, see Task 14b).
Task 14: implementer DONE_WITH_CONCERNS (63c60bd, 098c611): 2859/2859, sweep 0 problems, 2 launches. Criterion 5 not met for sn1054 and sn1998bw; three carried checks failed and were fixed after the launch.
- Ruling R-P3-36 (renumbered from R-P3-28, which the build record uses for the uneven-interpulse test): add Task 14b to diagnose and fix the frame-time regression before the final review (per-subsystem bench timings, up to 2 launches, optional bisect in a temporary worktree outside the repo), and confirm the three post-launch fixes.
Task 14: review (sonnet) Needs fixes (docs only): ruling-number collision (ledger renumbered to R-P3-36); refresh the committed ledger and rulings after 14b; calls-i-made.md missing the owner's sound request and P3-4..P3-12, R-P3-33/34. Minors: K9 wording, README performance hedging, "the rest passed" vs the faint shock limb, the pulse-trace test's name, the README's ergosphere claim.
- Ruling R-P3-37: Task 14's doc fix round waits until Task 14b lands (both touch docs/perf-2026-11.md and the README).
Task 14b: implementer DONE (4d11aa7, e53ae82): 2868/2868, 2 launches. Cause: CloseUpViewModel replaced its bound Readouts list each time step, so WPF rebuilt the rows every frame (a hidden window is still laid out). Fix: rows updated in place (ReadoutRowView). 1366x768: main alone sn1054 72, sn1998bw 74, n6946bh1 75 fps; both views still 59.3 / 66.7 / 71.8; orbiting whole run 52.8 / 56.7 / 59.0 (sn1054 second half 47.0). 1920x1080 (report only): still 52.0 / 61.2 / 70.1, orbiting 48.1 / 49.4 / 58.0. The three post-launch fixes confirmed on screen.
Task 14: doc fix round 1: rulings R-P3-36/37 added and R-P3-35 marked superseded; calls doc covers the owner's sound request, P3-4..P3-12, S1–S3, R-P3-33..37; the performance story updated to Task 14b's result everywhere; K9 wording; ergosphere claim; PulseTrace test renamed.
