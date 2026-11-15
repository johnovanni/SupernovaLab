# Global constraints for every Project 2 task (copied from the plan; rulings from the ledger)

## Global Constraints

Everything in Project 1's Global Constraints still holds: cgs units, `PhysicalConstants`, source time vs observer time
(× (1 + z)), `Quantity`/`Confidence`/`Source` on every shown number, `[APPROX]` + `Confidence.Speculative` for values
the research lacks, formula tests against quoted or hand-computed values, embedded data, glossary tooltips, and no
network. In addition:

- **No screen capture.** Visual checks use only the app's own diagnostics:
  - `--snapshot <out-dir> [--preset <id>] [--time <s>] [--size <WxH>] [--window …]`, with the new `--cutaway` (Task 7)
    and `--tour <stop>` (Task 13) options;
  - `--bench <s>` and `--sweep <steps>`.
  
  Never PowerShell `Add-Type`, P/Invoke or `CopyFromScreen`. Batch captures into one or two launches per task.
- **Draw only.** No change to any `ISignalModel`, `OutcomeEngine` or panel view model, except two things: the
  panel/tile highlight property (Task 12), and new read-only accessors that expose values a model already computes. Mixing in the gamma-ray model is spec §9's flagged item: do not do it.
- **Determinism.** Particle sets use a fixed seed (1987, as Project 1's `EjectaRenderer`), never the run number, so the
  picture is the same every run.
- **Performance:** ≥ 50 fps for the 3D view at 1366×768 with the Instruments window open, on the Intel Iris Xe, measured
  with `--bench`. Also report the 1920×1080 rate.
- **OpenGL 4.1 core** remains the floor. HDR needs an RGBA16F colour attachment; without it, draw the no-bloom path.
- **Element colours** (schematic; spec §3.5): hydrogen `#D85A30`, helium `#378ADD`, oxygen/silicon (CO core) `#639922`,
  nickel/iron `#EF9F27`, Type Ia intermediate-mass elements `#7F77DD`, Type Ia unburned carbon/oxygen `#888780`.
- **Overlay notes** keep Project 1's honesty labels. Every new schematic element adds its note, worded as the task says.
- **Commits** end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Commit on `project2`; never push.
- **Tests:** `dotnet test tests/SupernovaLab.Tests` passes with 0 warnings at every task's end.
- **Inconsistencies list.** Any new model/research/observation mismatch goes into
  `docs/research/scientific-inconsistencies.md` in the task that finds it (owner's standing request).

## Plan rulings (calls made under the standing instruction, for the owner's later review)

- **P1, particle velocity range.** Particles follow the research's broken power-law density profile with the
  outcome's E and M_ej, truncated at the speed that encloses 99.9% of the mass (§S1). So the fastest outer layers run
  ahead of Project 1's uniform-ball edge, as its existing note says they do. The radius readout keeps Project 1's
  value and label. The director frames the radius enclosing 99% of the particle mass, or Project 1's framing radius,
  whichever is larger.
- **P2, layer masses.** From the star:
  - H = `HydrogenEnvelopeMass`;
  - He = `HeliumEnvelopeMass`;
  - CO/Si = `CoCoreMass` − remnant baryonic mass − Ni (floored at 0);
  - Ni = `NickelMass`.
  
  When the sum differs from `EjectaMass` (pinned presets: inconsistency #1), the non-nickel layers are scaled by one
  common factor so the total equals `EjectaMass`. A scene note is added when the factor is outside 0.9–1.1:
  "Layer masses scaled by ×{f:0.00} to match the published ejecta mass".
- **P3, particle-to-layer assignment.** Particles sit at stratified mass coordinates q = (i + 0.5)/N, so layer masses
  are exact to 1/N. Layers are ordered from the centre outward (Ni, CO/Si, He, H; for a Type Ia: Ni, IME, CO).
  Directions are seeded and isotropic.
- **P4, particle radius.** Before any interaction:

  ```
  r = R*·(v / v_max) + v·(t − t_bo)
  ```

  This is continuous with the star at breakout and homologous later. Once `ShockStructureModel` gives shocks,
  particles faster than the reverse shock's Lagrangian speed v_r = R_r/(t − t_bo) are placed between R_r and R_c in
  order of speed. One C# function holds this rule and the vertex shader mirrors it (Task 8).
- **P5, director camera.** The view's physical half-height H is always V/0.9, where V is a 1-2-5 ring value: the
  outermost ring then sits exactly on the 90% step line.
  - **Step out:** when the framing radius reaches V, step two rungs up (1→5, 2→10, 5→20, so ×4–5).
  - **Step in:** when it falls below 0.8 × the value two rungs down.
  - **Jump** (new outcome or preset, timeline click, Home): take the smallest V with framing radius ≤ 0.6V.
  - **Ease:** log H moves with a smoothstep over 0.3 s of real time.
  
  So "the camera steps back when the debris reach the outermost ring".
- **P6, ring labels** are WPF overlay text positioned from the GL projection, not GL text. The ring circles are GL line
  loops.
- **P7, cut faces.** With the wedge on, particles in the removed quarter are discarded. Particles within 3% of the
  framing radius of either cut plane take their element colour at full opacity. The rest keep the thermal colour.
- **P8, surfaces** (shocks, swept-up shell, nebula, wind haze, dense shell) are camera-facing impostors that integrate
  an analytic column density along each pixel's line of sight. That gives true limb brightening with no meshes.
- **P9, the HDR pipeline is built in Task 4** (before the layered particles), because the spec's milestone-1
  performance test needs bloom. Task 11 adds the flash and the remaining looks.
- **P10, tour lines** live in `presets.json` as a new optional `tourLines` array on each preset.

## Rulings

- Ruling R-P2-1: Task 2's ring test uses H = 5/0.9 AU (outer rung 5 AU), expecting 5, 2, 1, 0.5 AU with 0.5 AU at
  opacity 0.8 (it sits between 0.05H and 0.1H). — The plan's H = 10/0.9 contradicts ruling P5 (outer rung at 0.9H). —
  Cost if wrong: one test value.
- Ruling R-P2-2: if HoldFraction at 0.675 dec/s lands just under 0.66, shorten the ease to 0.25 s (the spec says "about
  0.3 s", tuned against criterion 1) rather than loosen the criterion. — Criterion 1 is the spec's; the ease is the
  tunable. — Cost if wrong: a slightly quicker camera step.
- Ruling R-P2-3: the mixing test compares the moved-nickel fraction with the measured union coverage of the cones
  (computed from the same cone set over a fine direction grid), ± 0.03, not with the nominal CoveredFraction; and the
  cone half-angle is chosen so the union coverage matches CoveredFraction to 0.02 (iterate if needed). — Overlapping
  random cones cover less than K × cap area. — Cost if wrong: a test tolerance.
- Ruling R-P2-4: ShockStructureModel starts at the CSM model's start for outcomes it applies to; otherwise at
  EjectaKinematics.BlendStartS (3 yr), with the forward shock = EjectaKinematics.RadiusCm and §S4.2 for the rest.
  Confinement (ruling P4) is applied with a weight w = clamp(log10(t / t_start), 0, 1), r = lerp(free, confined, w), so
  nothing jumps when shocks appear. — Avoids a visible jump and a missing CSM-era value for Type Ia. — Cost if wrong:
  a decade of time with partly confined particles.
- Ruling R-P2-5: the TM99 ejecta index per class comes from §S4.2 (Task 1 must state it: e.g. n = 7 for Type Ia, the
  steep-envelope case for core collapse); tests exercise each trajectory function directly. — Research decides
  physics. — Cost if wrong: reverse-shock timing.
- Ruling R-P2-6: TourStop times are observer times; the first stop is at observer time 1e-3 s (the timeline's start). —
  The timeline is in observer time. — Cost if wrong: none visible below 1 ms.


## Rulings added during execution
- Ruling R-P2-7: Task 2 runs in parallel with Task 1. — Task 1 touches only docs/research files and Task 2 only Rendering/test files, so no file overlap; each stages its own files by name; the spec's milestone 1 puts rings beside research. — Cost if wrong: a git index.lock retry.
- Ruling R-P2-8: at unit boundaries the unit-consistent ring table skips rungs (e.g. 1e8 km → 1 AU, ×3); those cycles hold 0.800 at the default 1/3 dec/s, meeting the spec's criterion 1 (≥ 2/3 at the default speed), and 0.595 at the 0.675 bench speed. The plan's extra 0.66-at-bench-speed bound applies to ladder steps (ratio ≥ 3.9) only; boundary steps must still meet ≥ 2/3 at the default speed. — The spec criterion is at the default speed; one rare boundary step per unit switch is a cosmetic quick step at 2× speed. — Cost if wrong: a slightly hurried camera step once per unit change at fast playback.
- Ruling R-P2-9: a timeline click while the scale is frozen by a wheel zoom keeps the frozen scale; only Home or a new outcome hands back to the director. — Spec §2.2 says the wheel freezes the scale and Home hands back; the user chose that scale. — Cost if wrong: one extra Home press.
- Ruling R-P2-10: in the remnant era, forward, contact and reverse shock all use the n = 0 (uniform-ejecta) solutions, the forward being Project 1's radius; the class index of §S4.2 is disclosed via #33, not drawn. — One consistent model family; matches Project 1's blast wave and the panels. — Cost if wrong: early-remnant shock radii up to 2.6× small for steep ejecta (already disclosed).
- Ruling R-P2-11: contact and reverse radii are drawn as ratios to their own paper's forward shock (R_c/R_b from TC17, R_r/R_b from TM99) times Project 1's forward radius, with R_r := min(R_r, R_c); from the wind era (s = 2 ratios) to the remnant era the ratios are blended geometrically over 3–10 yr (EjectaKinematics' window). — Removes the inverted interval and the 13% jump (R-P2-4's intent). — Cost if wrong: < 1% radius error near t* ≈ 0.15.
- Ruling R-P2-12: §S2 and §S1 App blocks follow plan rulings P2 and P3 exactly (fields HydrogenEnvelopeMass/HeliumEnvelopeMass/CoCoreMass; remnant baryonic mass from RemnantModel.BaryonicMass(Remnant.Mass) for a neutron star, Remnant.Mass for a black hole; only non-Ni layers scaled, note outside 0.9–1.1; v_i = v(0.999·q_i), layers on q_i). — The plan's rulings stand; research supplies numbers. — Cost if wrong: none beyond P2/P3's own.
- Ruling R-P2-13: mixing redirects a fraction F_Ni of nickel particles (chosen by seed) into the finger cones (direction re-drawn inside the nearest cone) and pushes them outward with v' = v + (NickelMax − v)·u², NickelMax being the TIP value (SN 1987A 4,700 km/s); F_Ni comes from §S3 (0.5 if that is the research's value, with its tag). Supersedes R-P2-3's test: moved fraction = F_Ni ± 0.03 and every moved particle inside a cone. — Plumes concentrate nickel; isotropic selection would move only f ≈ 0.1. — Cost if wrong: finger brightness.
- Ruling R-P2-14: correct the Leahy & Williams → Tang & Chevalier 2017 mis-attribution (C9, #34) in signals.md §7 and RemnantEvolutionModel's comments/Source text as part of Task 8. — A citation fix, not a physics change; Task 8 touches shock structure. — Cost if wrong: none.
- Ruling R-P2-15: spec §3.1 ("outer edge agrees with EjectaKinematics") and §3.4 ("forward shock must agree") are corrected to plan ruling P1 and R-P2-10 wording in Task 1's fix round. — The research shows both are impossible as written (C8, C10). — Cost if wrong: none.
- Ruling R-P2-17 (PISN: no mixing fingers; JW11) and R-P2-18 (note above 0.3c, no clamp) — see ledger.
- Ruling R-P2-19 (failed-SN shell starts at EnvelopeShellLaunchS) — see ledger.
- Ruling R-P2-21 (cut faces = analytic opaque quads + depth buffer + bloom mask; replaces P7's slabs) — see ledger.
- Rulings R-P2-22 (wedge yaw φ chosen to cross finger cones; camera yaw offset) and R-P2-23 (face alpha from density profile; faces after bloom) — see ledger.
- Ruling R-P2-24 (shocks: wind era = CSM model's shock + s=2 class-n ratios; remnant era = TM99/TC17 class-n; geometric blend 3–10 yr; running-minimum reverse-shock speed; remnant readout = drawn forward shock, P1 value in a note; supersedes R-P2-10 and part of R-P2-11) — see ledger.
- Ruling R-P2-25 (two-line readout when shocks are drawn; frame blends to include R_f over 3–10 yr) — see ledger.
- Ruling R-P2-26 (R_f = running maximum of the blended radius; 3–10 yr readout label 'join ... schematic'; log join artefacts and Cas A R_r/R_f) — see ledger.
- Ruling R-P2-27 (SN 2023ixf confined shell as a pass-through preset pin, schematic flare; wind note only while frame ≤ 100 R*) — see ledger.
- Ruling R-P2-28 (geometry-only pins don't set UsesPublishedValues; confined shell lit until swept) — see ledger.
- Ruling R-P2-29 (Crab outer shell = the drawn forward-shock shell, labelled predicted-never-detected) — see ledger.
- Ruling R-P2-30 (governor on GL timer-query GPU time, off if unsupported; notes backdrop ≈ 0.8 alpha, light text, collapsible) — see ledger.
- Ruling R-P2-31 (tour accuracy wording folded in; presets.md 653 → 642 [D]) — see ledger.
- Ruling R-P2-32 (on-screen notes for #38, #49, #46, #47/#39; tour orbit keeps the view animating) — final fix wave.
- Ruling R-P2-33 (final wave: mixing tour stop; clamp confined radius; IIn peak-before-breakout logged and its stop dropped; remnant readout relabel; Crab flag) — see final-findings.md.
- Ruling R-P2-35 (two-scale mixing drawing on the cut faces: small RT fingers on every boundary + broad big plumes within 2× the cone chord; physics unchanged) — see ledger.
- Ruling R-P2-36 (downflows and sheaths fade with density, floor 0.5; nickel opaque; undulation only for mixing outcomes) — see ledger.
