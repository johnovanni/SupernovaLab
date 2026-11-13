# SupernovaLab Project 2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Project 1's simple 3D view with the showpiece: distance rings and a director camera, layered
particles with mixing and a wedge cutaway, shocks and surroundings, HDR glow, and a guided tour for every explosion.

**Architecture:**
- New physics for the view lives in `SupernovaLab.Core/Structure/`: pure functions of an `Outcome` and source time,
  with no OpenGL.
- `ExplosionScene` (Rendering) stays the single source of what is drawn at each moment.
- New GL renderers draw it through an HDR pipeline.
- Camera and tour logic are plain classes, testable without a screen.
- The instrument panels and every `ISignalModel` are untouched (spec §1, ground rules; D8).

**Tech Stack:** as Project 1. .NET 8, WPF, CommunityToolkit.Mvvm 8.3.2, OpenTK 4.9.4, OpenTK.GLWpfControl 4.3.6,
ScottPlot.WPF 5.1.59, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-12-supernovalab-project2-design.md`, on top of
`docs/superpowers/specs/2026-11-11-supernovalab-design.md`.

**Research:** normative, as in Project 1:
- `docs/research/outcomes.md`, `signals.md` and `presets.md`;
- the new `docs/research/structure.md`, written by Task 1. Its section numbers (§S1–§S10, defined in Task 1) are cited
  by later tasks. Read the cited sections in full before coding.

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

## Review Focus

1. **Outcomes with no debris** (not dead yet, white dwarf, failed supernova, pulsational pair instability): the scene,
   director, rings and tour must still work. They frame the star or the black-hole marker and draw no particles. Tests
   in Tasks 5, 6 and 12.
2. **Extreme explosions:** 0.1 M☉ at 1e52 erg (fast, decelerating within years) and a 300 M☉ pair-instability
   supernova. Velocities, radii and particle positions must stay finite, shock radii ordered R_r ≤ R_c ≤ R_f, and the
   director must never produce NaN. Sweep tests over the outcome-map strip in Tasks 5 and 8.
3. **Redshifted observers** (z > 0): the director and tour run in observer time on the timeline while the models take
   source time; captions show observer time. Tests in Tasks 3 and 12.
4. **Changing the outcome mid-ease or mid-tour:** re-frame instantly; the tour exits (with a "Tour ended: new
   explosion" note). Tests in Tasks 3 and 13.
5. **Hostile GL conditions:** a zero-size or minimized viewport, no RGBA16F support, a frame rate below 50 fps. No
   exception; the no-bloom path is used; quality steps down. Tests in Tasks 4 and 11.

---

## Milestone 1: research, rings, director, performance

### Task 1: The structure research file

**Files:**
- Create: `docs/research/structure.md`
- Modify: `docs/research/scientific-inconsistencies.md`

**Interfaces:**
- Produces sections §S1–§S10. Later tasks cite them by number. Each item is tagged [V] verified against the source,
  [R] recalled, [D] derived, or [APPROX] with its reasoning, and gives a citation (author, year, journal or arXiv id).
  Each section ends with an **App implementation** block: the exact formula or numbers the code uses.

- [ ] **Step 1: Research and write each section**
  - **§S1 Ejecta density profile.**
    - The broken power law: inner slope δ, outer slope n, per class (red supergiant, blue supergiant, stripped
      envelope, Type Ia).
    - The transition velocity v_t(E, M_ej, δ, n) (Chevalier & Soker 1989; Matzner & McKee 1999).
    - The enclosed-mass function M(<v), and v_max at 99.9% of the mass (ruling P1).
  - **§S2 Layer composition.**
    - Core collapse: how the presupernova masses map to the layers (ruling P2), checked against published
      presupernova models.
    - Type Ia: masses of Ni, intermediate-mass elements and unburned CO for a near-Chandrasekhar explosion, as fractions
      of the ejecta, with the nickel from the outcome (Mazzali et al. 2007 or the like).
  - **§S3 Mixing reach**, per class:
    - the fastest nickel and the slowest hydrogen, from 3D simulations (Hammer, Janka & Müller 2010; Wongwathanarat,
      Müller & Janka 2015) and SN 1987A and Cas A observations;
    - the angular scale (count) of fingers;
    - the fraction of the sphere they cover. Give SN 1987A's numbers explicitly.
  - **§S4 Shocks.**
    - 4.1: the self-similar wind-interaction solution (Chevalier 1982; s = 2): R_forward/R_c and R_reverse/R_c for the
      outer slopes of §S1.
    - 4.2: Truelove & McKee 1999 forward and reverse shock trajectories in uniform gas, for n = 0 and n = 7, in their
      characteristic units, and the time the reverse shock reaches the centre.
    - 4.3: how 4.2's forward shock compares with the Leahy & Williams 2017 blast wave that Project 1 uses
      (`RemnantEvolutionModel`).
  - **§S5 The shock inside the star:** the shock radius against time between bounce and breakout, consistent with the
    Kistler et al. delay `ShockBreakoutModel` uses. [APPROX] is acceptable, with the form stated.
  - **§S6 Layer radii inside the presupernova star:** the radii of the He core and the CO core for a red supergiant,
    a blue supergiant and a Wolf–Rayet star, as fractions of the stellar radius.
  - **§S7 Surroundings:**
    - the wind's outer extent (wind speed × wind lifetime, or a published bubble radius);
    - dense-shell extents by class (IIn; SN 2023ixf; SN 1998S), replacing or confirming Project 1's 10¹⁵ cm.
  - **§S8 SN 1987A's rings:** the equatorial ring's radius and inclination; the two outer rings' radii and offsets
    along the axis; the hotspot timing (with signals §6.3).
  - **§S9 Pulsar wind nebula:** the radius growth law inside freely expanding ejecta (Chevalier & Fransson 1992;
    van der Swaluw et al. 2001) in terms of E, M_ej and the spin-down luminosity; the Crab's measured size as a check;
    the fact that the Crab's outer shell is undetected.
  - **§S10 Failed-supernova envelope ejection:** the ejected mass, speed and energy (Lovegrove & Woosley 2013;
    Fernández et al. 2018), and whether it applies to the lab's failed-supernova range.

- [ ] **Step 2: Cross-check numbers the spec quoted from memory.** These are SN 1987A's ring radius ≈ 0.6 ly and
  inclination ≈ 43°, its nickel reach of 3,000–4,000 km/s and hydrogen below 1,000 km/s, and the nebula's
  R ∝ t^1.2. Correct them in `structure.md` where the sources differ, and list every correction at the top of the file
  under "Corrections to the spec".

- [ ] **Step 3: Update the inconsistencies list.** Add the Crab's undetected outer shell under a new "3D structure"
  table, and any mismatch found in Steps 1–2, in the existing table format.

- [ ] **Step 4: Commit**

```bash
git add docs/research/structure.md docs/research/scientific-inconsistencies.md
git commit -m "Research: ejecta structure, mixing, shocks, surroundings (structure.md)"
```

### Task 2: Ring ladder and director camera (pure logic)

**Files:**
- Create: `src/SupernovaLab.Rendering/DistanceRings.cs`, `src/SupernovaLab.Rendering/CameraDirector.cs`
- Modify: `src/SupernovaLab.Rendering/ScaleBar.cs` (expose the unit choice and label)
- Test: `tests/SupernovaLab.Tests/DistanceRingsTests.cs`, `tests/SupernovaLab.Tests/CameraDirectorTests.cs`

**Interfaces:**
- Produces:
  - `ScaleBar.UnitFor(double spanCm) -> (double UnitCm, string Name)`, the same thresholds `Choose` uses;
    `ScaleBar.Label(double value, string unitName) -> string` (now public). `Choose` keeps its behaviour.
  - `record Ring(double RadiusCm, string Label, float Opacity)`.
  - `DistanceRings.Rungs(double halfHeightCm) -> IReadOnlyList<Ring>`: the 1-2-5 values in the unit for
    `halfHeightCm`, from 0.9 × halfHeight down to 0.05 × halfHeight. Opacity is 1 from 0.1H up, ramping linearly to 0
    at 0.05H.
  - `DistanceRings.Rung(double cm, int steps, string unitName) -> double`: moves `steps` rungs up (+) or down (−) the
    1-2-5 ladder.
  - `sealed class CameraDirector`:
    - `CameraDirector(double easeSeconds = 0.3)`;
    - `double HalfHeightCm { get; }`, `bool IsEasing { get; }`, `double OuterRingCm { get; }`;
    - `void Jump(double frameRadiusCm)`;
    - `void Update(double frameRadiusCm, double wallDeltaS)`;
    - constants `StepOutAt = 1.0` (framing radius ÷ outer ring), `StepInBelow = 0.8`, `JumpFraction = 0.6`,
      `RingLine = 0.9`.

- [ ] **Step 1: Write the failing tests**
  - `Rungs_At10AuView_Are_5_2_1_AndHalfAu`: H = 10/0.9 AU → ring labels "5 AU", "2 AU", "1 AU", "0.5 AU", all
    opacity 1 except where below 0.1H.
  - `Rungs_UseTheScaleBarsUnitSwitches`: H at 2e4 AU uses light-years ("0.2 light-years"); H at 2 pc uses "pc".
  - `Rung_TwoUp_From1_2_5`: 1→5, 2→10, 5→20 (in one unit).
  - `Jump_PicksSmallestRungAboveFrameOver0_6`: frame radius 3 AU → outer ring 5 AU, H = 5/0.9 AU.
  - `Update_StepsOutWhenTheDebrisReachTheOuterRing_ThenEases`: from outer ring 5 AU, radius 5.0 AU → target ring 20
    AU; after 0.15 s, H is between the two (log-midway ± 10%); after 0.3 s, exactly 20/0.9 AU and `IsEasing` is false.
  - `Update_StepsInBelow0_8OfTwoRungsDown_WithHysteresis`: outer ring 20 AU; radius 4.1 AU → no step; 3.9 AU → step
    to 5 AU. Then 4.5 AU does not step back out.
  - `HoldFraction_AtDefaultSpeed_IsAtLeastTwoThirds` (spec criterion 1): simulate playback at 1/3 decade per second
    in 1/60 s frames with radius ∝ t from 1 day to 1 yr; the fraction of frames with `IsEasing == false` is ≥ 0.80.
    The same at 0.675 decades per second is ≥ 0.66.
  - `Update_WithNaNOrZeroRadius_KeepsTheLastScale` (Review Focus 2/5).

- [ ] **Step 2: Run them to see them fail.** `dotnet test tests/SupernovaLab.Tests --filter "DistanceRings|CameraDirector"`

- [ ] **Step 3: Implement** to ruling P5. Easing interpolates log H with smoothstep(u) = u²(3 − 2u). A new step
  requested mid-ease starts from the current H.

- [ ] **Step 4: Run the tests to see them pass**, then the full suite.

- [ ] **Step 5: Commit** ("Distance rings and director camera logic").

### Task 3: Rings and director in the 3D view

**Files:**
- Create: `src/SupernovaLab.Rendering/RingRenderer.cs`, `src/SupernovaLab.Rendering/Shaders/ring.vert`,
  `src/SupernovaLab.Rendering/Shaders/ring.frag`
- Modify: `src/SupernovaLab.App/Controllers/SceneController.cs`, the main window's 3D overlay XAML (the scale bar is
  replaced by ring labels), `src/SupernovaLab.Core/Text/Glossary.cs`
- Test: `tests/SupernovaLab.Tests/SceneControllerTests.cs` (extend)

**Interfaces:**
- Consumes: `CameraDirector`, `DistanceRings` (Task 2).
- Produces:
  - `SceneController.Director` (`CameraDirector`);
  - `SceneController.RingLabels`: `IReadOnlyList<RingLabel>` with `record RingLabel(string Text, double XDip,
    double YDip, double Opacity)`, the label at each ring's top;
  - `[ObservableProperty] bool ShowRings = true`;
  - `UnitCm` is now `Director.HalfHeightCm`, or the frozen value after a wheel zoom;
  - the camera distance makes one scene unit equal the view's half-height:
    `SceneController.AutoDistance = 1 / tan(fov/2)`.

- [ ] **Step 1: Write the failing tests** (fake `IRenderHost` and a fake wall clock, as in Project 1's tests).
  - `Playing_StepsTheDirector_AndTheLabelsFollow`.
  - `NewOutcome_DuringAnEase_ReframesAtOnce` (Review Focus 4).
  - `TimelineClick_IsAJump_NotAnEase`. A change of more than 0.05 decades in one tick, or a marker jump, counts as a
    jump.
  - `Zoom_FreezesTheScale_HomeHandsBackToTheDirector`.
  - `RedshiftedObserver_UsesSourceTimeForTheRadius` (Review Focus 3): z = 1, observer time 2 days → frame radius equals
    the radius at a source time of 1 day.
  - `ZeroSizeView_DoesNotThrow` (Review Focus 5).

- [ ] **Step 2: Run them to see them fail.**

- [ ] **Step 3: Implement.**
  - The `SceneController` render loop calls `Director.Update` with the wall time since the last frame. Set
    `_host.Animating` while `Director.IsEasing`.
  - `RingRenderer` draws camera-facing circles: 128-segment line loops, grey `#888780`, 1 px, using each ring's
    opacity.
  - Glossary key `DistanceRings`: "Measuring circles, not part of the supernova: when the debris reach a ring, they are
    that far from the centre."
  - Remove the scale bar from the overlay; keep the radius readout. Ring labels are an ItemsControl on a Canvas.

- [ ] **Step 4: Run the tests to see them pass**, then the full suite.

- [ ] **Step 5: Visual check.** One `--snapshot` launch: the default preset at 1 d, 3 d, 10 d, 30 d and 100 d; SN 1987A
  at 1 yr and 30 yr. Report what the rings read and whether a step is visible between the frames.

- [ ] **Step 6: Commit** ("3D view: distance rings and director camera").

### Task 4: Particle renderer and HDR pipeline (with the performance test)

**Files:**
- Create: `src/SupernovaLab.Rendering/ParticleRenderer.cs`, `Shaders/particle.vert`, `Shaders/particle.frag`,
  `src/SupernovaLab.Rendering/HdrPipeline.cs`, `Shaders/fullscreen.vert`, `Shaders/bloom_down.frag`,
  `Shaders/bloom_up.frag`, `Shaders/tonemap.frag`
- Modify: `SceneController.cs` (draw through the pipeline; Project 1's `EjectaRenderer` is retired in Task 6),
  `docs/perf-2026-11.md`
- Test: `tests/SupernovaLab.Tests/HdrPipelineTests.cs`

**Interfaces:**
- Produces:
  - **The particle vertex layout:** location 0 is `vec3` velocity in units of `v_max` (|v| ≤ 1); location 1 is a
    `float` layer index (0–5, enum order of `ElementLayer`, Task 5); location 2 is a `float` seed in [0, 1).
  - `ParticleRenderer`:
    - `ParticleRenderer(int capacity)`;
    - `void Upload(float[] velocity, float[] layer, float[] seed, int count)`;
    - `void Render(OrbitCamera cam, Size viewportPx, double pxPerDip, ParticleUniforms u)`;
    - `record struct ParticleUniforms(...)`, with fields added by later tasks: time terms, colours, brightness, wedge.
  - `HdrPipeline`:
    - `static bool IsSupported(Func<bool> tryCreateRgba16f)`;
    - `void Begin(Size viewportPx)`;
    - `void End(float exposure, float bloomStrength)`;
    - `bool Enabled { get; }`;
    - a no-bloom pass-through when disabled.
  - `QualityLevel` enum: `High = 200_000`, `Medium = 100_000`, `Low = 50_000`.

- [ ] **Step 1: Write the failing tests**
  - `HdrPipeline_IsSupported_FalseWhenTheFormatFails`.
  - `Tonemap_IsMonotonic_AndMapsZeroToZero`: tests `HdrPipeline.Tonemap(float x)`, the C# mirror of the shader's
    curve. Narkowicz's ACES fit: (x(2.51x + 0.03))/(x(2.43x + 0.59) + 0.14), clamped to [0, 1].
  - `BloomLevels_ForViewport`: 1366×768 → 5 levels, none smaller than 8 px.

- [ ] **Step 2: Run them to see them fail.**

- [ ] **Step 3: Implement.**
  - The pipeline: an RGBA16F scene target; a 5-level dual-filter (Kawase-style) bloom, down then up; tone map and
    gamma to the default framebuffer. Particles are point sprites with additive blending, size 2.5 dip with a soft
    Gaussian falloff (as Project 1).
  - For this task only, `SceneController` uploads a temporary isotropic set: |v| uniform in [0.7, 1], the old shell.

- [ ] **Step 4: Run the tests to see them pass.**

- [ ] **Step 5: Performance test.** `--bench 20` at 1366×768 with the Instruments window open, at each quality level.
  Record the results in `docs/perf-2026-11.md` under a new "Project 2" heading, plus one High run at 1920×1080.
  - If High is ≥ 50 fps, it is the default.
  - Otherwise make the highest passing level the default, and record the ruling in the ledger.

- [ ] **Step 6: Commit** ("HDR pipeline and particle renderer; Project 2 performance test").

---

## Milestone 2: inside the explosion

### Task 5: Ejecta structure (Core)

**Files:**
- Create: `src/SupernovaLab.Core/Structure/ElementLayer.cs`, `EjectaStructure.cs`, `EjectaStructureModel.cs`,
  `ParticleSampler.cs`, `MixingModel.cs`
- Test: `tests/SupernovaLab.Tests/EjectaStructureTests.cs`, `tests/SupernovaLab.Tests/ParticleSamplerTests.cs`

**Interfaces:**
- Consumes: `Outcome`, `PresupernovaStar`, `Remnant.Mass`, `EjectaKinematics.HasEjecta`.
- Produces:
  - `enum ElementLayer { NickelIron, CarbonOxygenCore, Helium, Hydrogen, IntermediateMass, UnburnedCarbonOxygen }`.
  - `record LayerMass(ElementLayer Layer, double MassMsun)`.
  - `record MixingReach(double NickelMaxCmS, double HydrogenMinCmS, int FingerCount, double CoveredFraction,
    Confidence Confidence, Source Source)`.
  - `record EjectaStructure(IReadOnlyList<LayerMass> Layers, double EjectaMassMsun, double EnergyErg, double Delta,
    double N, double TransitionCmS, double MaxCmS, double Frame99CmS, MixingReach Mixing, double LayerScale,
    IReadOnlyList<string> Notes)`.
  - `static class EjectaStructureModel`:
    - `EjectaStructure? For(Outcome o)`, null without ejecta;
    - `double EnclosedMassFraction(EjectaStructure s, double vCmS)`;
    - `double VelocityAtMassFraction(EjectaStructure s, double q)`.
  - `record ParticleSet(float[] Velocity, float[] Layer, float[] Seed, int Count, double MaxCmS)`. Velocity is
    interleaved xyz in units of `MaxCmS`.
  - `static class ParticleSampler`: `ParticleSet Sample(EjectaStructure s, int count, int seed = 1987)`.

- [ ] **Step 1: Write the failing tests**
  - `LayerMasses_SumToEjectaMass_WithinOnePercent` for the default 16 M☉ star, a stripped Ib and Ic, SN 1987A, SN 1998bw
    (pinned, scaled), a Type Ia and a pair-instability supernova (spec criterion 2).
  - `Nickel_EqualsTheOutcomesNickel`.
  - `StrippedStars_HaveNoHydrogen_IcHasNoHelium`.
  - `TypeIa_HasNickelIntermediateAndUnburned_InThatOrder`: fractions as in §S2.
  - `ScaledLayers_AddANote_OutsidePoint9To1Point1` (ruling P2; SN 1998bw).
  - `Profile_EnclosesAllMassAtVmax_AndHalfAtTheMedian`: `EnclosedMassFraction(MaxCmS)` = 0.999 ± 1e-6;
    `VelocityAtMassFraction` inverts it to 1e-9.
  - `TransitionVelocity_MatchesStructureS1WorkedExample`: hand-computed from the §S1 formula in the test comment, for
    1e51 erg and 10 M☉.
  - `Sampler_LayerCountsMatchMasses_ToOneParticle` and `Sampler_IsDeterministic`.
  - `Mixing_NickelReachesTheResearchSpeed_HydrogenFallsToIt`: SN 1987A preset; max Ni speed within 5% of the §S3 value,
    min H speed within 5%. The fraction of Ni particles moved = `CoveredFraction` ± 0.03.
  - `NoEjecta_GivesNull` for not dead yet, white dwarf, failed supernova and pulsational pair instability (Review
    Focus 1). Exception: if §S10 says failed supernovae eject an envelope, `For` returns a hydrogen-only structure with
    §S10's mass and speed, and this test asserts that instead.
  - `OutcomeMapStrip_AllFinite` (Review Focus 2): every outcome in `OutcomeMap.Strip` at Z = 1 and Z = 0.1, plus
    0.1 M☉ at 1e52 erg, gives finite speeds and positive layer masses.

- [ ] **Step 2: Run them to see them fail.**

- [ ] **Step 3: Implement**, following §S1, §S2, §S3 and rulings P1–P3.
  - **Mixing (`MixingModel`):**
    - Draw `FingerCount` seeded directions. Each finger is a cone whose half-angle is set so all cones together
      cover `CoveredFraction` of the sphere.
    - Ni particles whose direction lies in a cone are moved outward: v' = v + (NickelMax − v)·u^2, with u the
      particle's seed, so a few reach the full reach.
    - H particles in a cone with seed < 0.5 are moved inward: v' = HydrogenMin + (v − HydrogenMin)·seed.
    - The same `seed` value is uploaded as the vertex seed.
  - `Frame99CmS` is `VelocityAtMassFraction(0.99)` after mixing.

- [ ] **Step 4: Run the tests to see them pass**, then the full suite.

- [ ] **Step 5: Commit** ("Ejecta structure: layers, profile, mixing, particle sampling").

### Task 6: Layered particles in the scene

**Files:**
- Modify: `src/SupernovaLab.Rendering/ExplosionScene.cs`, `SceneController.cs`, `ParticleRenderer.cs` and its
  shaders; delete `EjectaRenderer.cs` and `ejecta.vert`/`ejecta.frag`
- Create: `src/SupernovaLab.Core/Structure/ParticleKinematics.cs`
- Test: `tests/SupernovaLab.Tests/ExplosionSceneTests.cs` (extend), `tests/SupernovaLab.Tests/ParticleKinematicsTests.cs`

**Interfaces:**
- Consumes: `EjectaStructureModel`, `ParticleSampler` (Task 5); `ParticleRenderer` (Task 4).
- Produces:
  - `ExplosionScene.Structure` (`EjectaStructure?`) and `ExplosionScene.Particles` (`ParticleSet?`), built once per
    outcome;
  - `SceneState` gains `double SinceBreakoutS`, `double MaxRadiusCm` (v_max term) and `double Frame99RadiusCm`;
  - `FrameRadiusCm` becomes max(Project 1's frame radius, `Frame99RadiusCm`) (ruling P1);
  - `record ShockRadii(double ForwardCm, double ContactCm, double ReverseCm)` in
    `src/SupernovaLab.Core/Structure/ShockRadii.cs`. It is defined here so the rule below compiles; Task 8 fills it.
  - `static class ParticleKinematics`:
    `double RadiusCm(double vCmS, double sinceBreakoutS, double starRadiusCm, double maxCmS, ShockRadii? shocks)`,
    the ruling P4 rule. Until Task 8, callers pass null and the confinement branch is a stub that the tests do not
    reach.

- [ ] **Step 1: Write the failing tests**
  - `ParticleRadius_AtBreakout_IsInsideTheStar_AndHomologousLater`: v = v_max at t_bo → R*; at t_bo + 1e6 s, the
    ratio of two particles' radii equals the ratio of their speeds within 1e-3.
  - `Scene_FrameRadius_IsAtLeastTheFrame99Radius`.
  - `Scene_NoEjecta_HasNoParticles_AndStillFrames` (Review Focus 1).
  - `Scene_Particles_AreBuiltOnce_PerOutcome`.

- [ ] **Step 2: Run them to see them fail.**

- [ ] **Step 3: Implement.**
  - The vertex shader mirrors `ParticleKinematics.RadiusCm`. Uniforms: `sinceBreakoutS`, `starRadiusCm`, `maxCmS`,
    `unitCm`, plus shock uniforms set to "none" until Task 8.
  - The outside colour is Project 1's light-curve colour temperature and brightness.
  - Remove `EjectaRenderer`.
  - Overlay note when mixing is drawn: "Mixing fingers: shapes schematic; their reach is from 3D simulations
    (structure §S3)".

- [ ] **Step 4: Run the tests to see them pass**, then the full suite.

- [ ] **Step 5: Visual check.** One launch: the default preset at 1 d and 1 yr; SN 1987A at 100 d; a Type Ia at 20 d;
  SN 1998bw at 10 d.

- [ ] **Step 6: Commit** ("3D view: layered particles from the ejecta structure").

### Task 7: Wedge cutaway and the shock inside the star

**Files:**
- Create: `src/SupernovaLab.Core/Structure/StarInterior.cs`, `src/SupernovaLab.Rendering/LayerColors.cs`
- Modify: `ParticleRenderer` and its shaders, `StarRenderer` and `star.frag` (wedge, layer discs, shock front),
  `ExplosionScene.cs`, `SceneController.cs`, the 3D overlay XAML (cutaway toggle, legend), `SnapshotRenderer.cs`
  (`--cutaway`), `Glossary.cs`
- Test: `tests/SupernovaLab.Tests/StarInteriorTests.cs`, `ExplosionSceneTests.cs`, `SnapshotRequestTests.cs`

**Interfaces:**
- Produces:
  - `static class StarInterior`:
    - `IReadOnlyList<(ElementLayer Layer, double OuterRadiusCm)> Layers(Outcome o)`, from §S6;
    - `double ShockRadiusCm(Outcome o, double sourceTimeS)`, from §S5: 0 at bounce, R* at
      `ShockBreakoutModel.ShockAtSurfaceS`, NaN after.
  - `SceneState`: `IReadOnlyList<(ElementLayer, double)> StarLayers`, `double ShockInStarRadiusCm`.
  - `SceneController`: `[ObservableProperty] bool IsCutaway`, `IReadOnlyList<LegendItem> Legend` with
    `record LegendItem(string Name, string ColorHex)`.
  - `LayerColors.For(ElementLayer)`, using the Global Constraints colours.
  - The wedge: the removed quarter is x > 0 and z > 0 in scene coordinates. It turns with the scene, so the camera can
    swing it.

- [ ] **Step 1: Write the failing tests**
  - `ShockInStar_ReachesTheSurface_AtTheBreakoutModelsTime`: within 1e-9 of R* at `ShockAtSurfaceS`; monotonic before.
  - `StarLayers_AreNested_AndEndAtTheStarsRadius`: for a red supergiant, a blue supergiant and a stripped star,
    against the §S6 fractions.
  - `Legend_ListsOnlyTheLayersPresent`: Ic → nickel/iron, oxygen/silicon; Ia → nickel/iron, intermediate,
    unburned C/O.
  - `SnapshotRequest_ParsesCutaway`.

- [ ] **Step 2: Run them to see them fail.**

- [ ] **Step 3: Implement**, following rulings P7 and P8.
  - Before breakout, with the wedge on, the star's cut faces show the layer discs (§S6). The shock front is a bright
    ring at `ShockInStarRadiusCm`, and the gas inside it is drawn brighter.
  - Overlay notes: "Cutaway: colours show elements (schematic)" and "The shock is crossing the star: it reaches the
    surface {time} after core bounce" (the Project 1 note, kept).
  - Glossary keys: `Cutaway`, `MixingFingers` ("Rayleigh–Taylor fingers: heavy elements from the core punch out
    through the lighter layers as the shock decelerates"), `ShockInStar`.

- [ ] **Step 4: Run the tests to see them pass**, then the full suite.

- [ ] **Step 5: Visual check.** One launch with `--cutaway`: the default preset at a third and two-thirds of the way to
  breakout, then 30 d; SN 1987A at 1 yr; a Type Ia at 20 d.

- [ ] **Step 6: Commit** ("3D view: wedge cutaway, element layers and the shock inside the star").

---

## Milestone 3: shocks and surroundings

### Task 8: Shock structure

**Files:**
- Create: `src/SupernovaLab.Core/Structure/ShockStructureModel.cs`,
  `src/SupernovaLab.Rendering/ShellRenderer.cs`, `Shaders/shell.vert`, `Shaders/shell.frag`,
  `src/SupernovaLab.Rendering/ColumnDensity.cs`
- Modify: `ParticleKinematics.cs` and the particle vertex shader (confinement), `ExplosionScene.cs`,
  `SceneController.cs`, `Glossary.cs`
- Test: `tests/SupernovaLab.Tests/ShockStructureTests.cs`, `tests/SupernovaLab.Tests/ColumnDensityTests.cs`,
  `ParticleKinematicsTests.cs`

**Interfaces:**
- Produces:
  - Uses `ShockRadii` (Task 6) and completes `ParticleKinematics`' confinement branch.
  - `static class ShockStructureModel`: `ShockRadii? At(Outcome o, double sourceTimeS)`.
    - Null before `CsmInteractionModel.StartTimeS`, or when neither the CSM nor the remnant model applies.
    - In the CSM era (< 3 yr): the forward shock is the lab's CSM shock radius (`CsmInteractionModel.Of(o)`'s speed ×
      t). The contact and reverse shocks come from §S4.1's ratios.
    - From 10 yr: the forward shock is `EjectaKinematics.RadiusCm` (Project 1's blast wave). The reverse shock and
      contact come from §S4.2.
    - From 3 to 10 yr: each radius is blended geometrically, as `EjectaKinematics` does.
  - `static class ColumnDensity`, the C# mirror of the shell shader. b is the impact parameter:
    - `double Shell(double b, double rIn, double rOut)` = 2(√(r_out² − b²) − √(max(r_in² − b², 0))) for b < r_out,
      else 0;
    - `double Ball(double b, double r)`;
    - `double Wind(double b, double rIn, double rOut)` for ρ ∝ 1/r², the integral of 1/r² along the chord.
  - `ShellRenderer.Render(OrbitCamera cam, Size viewportPx, ShellKind kind, double rInUnits, double rOutUnits,
    (float R, float G, float B) color, float intensity)`, with `enum ShellKind { Shell, Ball, Wind }`.
  - `SceneState.Shocks` (`ShockRadii?`).

- [ ] **Step 1: Write the failing tests**
  - `Shocks_AreOrdered_ReverseContactForward`, across the outcome-map strip and the extremes (Review Focus 2).
  - `Shocks_AtTenYears_ForwardEqualsProject1BlastWave` (1e-9 relative).
  - `Shocks_InCsmEra_ForwardEqualsTheCsmShock`.
  - `ReverseShock_ReachesCentre_AtStructureS4_2Time` (n = 0 case, hand-computed in the comment).
  - `ColumnDensity_Shell_AtCentre_IsTwiceTheThickness`; `ColumnDensity_Ball_AtCentre_IsDiameter`;
    `ColumnDensity_Wind_MatchesNumericalIntegral` (1,000-step midpoint rule in the test, 1e-3).
  - `Particles_PastTheReverseShock_LieBetweenReverseAndContact` (ruling P4).

- [ ] **Step 2: Run them to see them fail.**

- [ ] **Step 3: Implement.** Draw the forward-shock shell (between the contact and forward radii) in a pale blue-white,
  and the reverse shock as a thin shell at R_r. Shocked particles get a hot-gas tint (the colour at 10⁷ K from
  `BlackbodyColor`, capped) and 1.5× brightness. Overlay note: "Shocks: radii from Chevalier 1982 and Truelove & McKee
  1999 (structure §S4)". Glossary keys `ForwardShock`, `ReverseShock`, `ContactSurface`.

- [ ] **Step 4: Run the tests to see them pass**, then the full suite.

- [ ] **Step 5: Visual check.** One launch: the default preset at 1 yr, 30 yr and 300 yr; the `casa` and `tycho` presets at their ages.

- [ ] **Step 6: Commit** ("3D view: forward and reverse shocks, swept-up shell").

### Task 9: Wind haze and dense shells

**Files:**
- Create: `src/SupernovaLab.Core/Structure/CircumstellarStructure.cs`
- Modify: `ExplosionScene.cs` (replaces Project 1's fixed gas shell), `SceneController.cs`, `Glossary.cs`
- Test: `tests/SupernovaLab.Tests/CircumstellarStructureTests.cs`, `ExplosionSceneTests.cs`

**Interfaces:**
- Produces:
  - `record CircumstellarStructure(double WindAStar, double WindInnerCm, double WindOuterCm, double? ShellInnerCm,
    double? ShellOuterCm, Confidence ShellConfidence, Source Source)`.
  - `static class CircumstellarStructureModel`:
    - `CircumstellarStructure? For(Outcome o)`: null for a Type Ia and for no star;
    - `float Glow(Outcome o, double sourceTimeS)`: 0–1, `CsmInteractionModel.ThermalXrayLuminosity` mapped
      logarithmically over 4 decades below its maximum over the CSM model's range.
  - `SceneState.Surroundings` (`CircumstellarStructure?`) and `SceneState.SurroundingsGlow` (`float`).

- [ ] **Step 1: Write the failing tests**
  - `RedSupergiantWind_IsThickerThanWolfRayet`: the column at b = 2R* is higher for the default 16 M☉ star than for a
    stripped Ic.
  - `DenseShell_ExtentFromStructureS7`; falls back to 1e15 cm with `Confidence.Speculative` where §S7 gives none.
  - `Glow_IsZeroBeforeTheShockArrives_AndPeaksAtOne`.
  - `TypeIa_HasNoSurroundings`.

- [ ] **Step 2: Run them to see them fail.**

- [ ] **Step 3: Implement.**
  - The haze uses `ShellKind.Wind`, from R* to the wind's outer extent (§S7), faint grey-blue, intensity ∝ A_* on a
    log scale.
  - The dense shell uses `ShellKind.Shell`.
  - Inside the forward shock the haze is not drawn. At the forward shock it is lit by `SurroundingsGlow`.
  - Notes keep Project 1's `GasNote` wording when the extent is the 1e15 cm fallback; otherwise "Gas around the star:
    extent from {source}".

- [ ] **Step 4: Run the tests to see them pass**, then the full suite.

- [ ] **Step 5: Visual check.** One launch: the default preset at 1 h, 10 d and 1 yr; SN 2023ixf at 1 d, 5 d and 30 d.

- [ ] **Step 6: Commit** ("3D view: the star's wind and dense shells").

### Task 10: Pulsar wind nebula and SN 1987A's rings

**Files:**
- Create: `src/SupernovaLab.Core/Structure/PulsarWindNebulaModel.cs`, `src/SupernovaLab.Core/Structure/Sn1987aRings.cs`,
  `src/SupernovaLab.Rendering/TorusRingRenderer.cs` with shaders
- Modify: `ExplosionScene.cs`, `SceneController.cs`, `Glossary.cs`, `docs/research/scientific-inconsistencies.md`
- Test: `tests/SupernovaLab.Tests/PulsarWindNebulaTests.cs`, `tests/SupernovaLab.Tests/Sn1987aRingsTests.cs`

**Interfaces:**
- Produces:
  - `PulsarWindNebulaModel`:
    - `static double RadiusCm(Outcome o, double sourceTimeS)`: NaN unless `PulsarModel.Applies(o)`; the §S9 growth law
      using `PulsarModel.Of(o)`'s spin-down luminosity;
    - `static bool Applies(Outcome o)`.
  - `Sn1987aRings`:
    - `static IReadOnlyList<RingGeometry> Rings`, with `record RingGeometry(double RadiusCm, double AxisOffsetCm,
      double InclinationRad, bool Equatorial)` from §S8;
    - `static float HotspotBrightness(double sourceTimeS)`, from signals §6.3 via
      `CsmInteractionModel.RingXrayLuminosityErgS`, normalised to its peak.
  - `SceneState`: `double NebulaRadiusCm`, `bool ShowRings1987A`, `float RingBrightness`.

- [ ] **Step 1: Write the failing tests**
  - `Nebula_RadiusFollowsTheGrowthLaw_AndMatchesTheCrab`: the Crab preset at its age gives a radius within §S9's stated
    tolerance of the measured size.
  - `Nebula_OnlyWithAPulsar`.
  - `Rings_OnlyWhenTheOutcomeHasTheEquatorialRing` (`Outcome.HasEquatorialRing`).
  - `Rings_HotspotsRiseFromDay3000_PeakNearDay8000`.

- [ ] **Step 2: Run them to see them fail.**

- [ ] **Step 3: Implement.**
  - The nebula: a `ShellKind.Ball` in a synchrotron blue-white, inside the ejecta. Note: "Pulsar wind nebula: size
    from {source}".
  - The rings: thin tori. The equatorial one gets hotspot beads, 24 evenly spaced, brightening with
    `RingBrightness`. Note: "SN 1987A's rings: observed geometry (preset), not predicted by the lab".
  - Glossary keys `PulsarWindNebula`, `Sn1987aRings`.
  - Add the Crab's undetected outer shell to the inconsistencies list if Task 1 did not.

- [ ] **Step 4: Run the tests to see them pass**, then the full suite.

- [ ] **Step 5: Visual check.** One launch: SN 1987A at 1,000 d, 3,000 d, 8,000 d and 12,000 d; `sn1054` (the Crab) at its age.

- [ ] **Step 6: Commit** ("3D view: pulsar wind nebula and SN 1987A's rings").

---

## Milestone 4: spectacle

### Task 11: Breakout flash, limb darkening, jets, quality step-down

**Files:**
- Modify: `ExplosionScene.cs`, `SceneController.cs`, `StarRenderer`/`star.frag`, `JetRenderer`/`cone.frag`,
  `HdrPipeline.cs`, `docs/perf-2026-11.md`
- Create: `src/SupernovaLab.Rendering/QualityGovernor.cs`
- Test: `tests/SupernovaLab.Tests/SpectacleTests.cs`

**Interfaces:**
- Produces:
  - `SceneState.FlashIntensity` (`float`): 0 outside breakout. During breakout it is
    1 + log10(L_bo / L_peak) clamped to [0, 8], where L_bo is the breakout model's luminosity at t and L_peak is the
    light curve's peak. Its colour comes from the flash temperature of the breakout model (`ShockBreakoutModel`). Expose it as a new read-only `public double FlashTemperatureK(Outcome o)` (the Global Constraints allow this).
  - `SceneState.Exposure` (`float`): Project 1's `Brightness` mapped to HDR exposure.
  - `QualityGovernor`:
    - `QualityGovernor(QualityLevel start)`;
    - `QualityLevel Level { get; }`;
    - `bool Update(double frameTimeS)`: true when the level changes. It steps down once the mean frame rate over 3 s is
      below 50 fps, never steps up automatically, and has a minimum of `Low`.
  - The star shader applies limb darkening I(μ) = 1 − 0.6(1 − μ) (linear law, u = 0.6) [APPROX, Speculative].

- [ ] **Step 1: Write the failing tests**
  - `Flash_IsZeroOutsideBreakout_AndPositiveDuring`.
  - `Flash_RedSupergiant_OutshinesThePeak`: default preset FlashIntensity > 1.
  - `Governor_StepsDownAfterThreeSlowSeconds_NotOnASpike`; `Governor_NeverBelowLow`.
  - `NoHdr_FlashStillShows_AsWhiteOverlay` (Review Focus 5): with `HdrPipeline.Enabled == false`, the scene still
    reports the flash and the controller draws it as a full-screen additive quad at min(1, intensity/8).

- [ ] **Step 2: Run them to see them fail.**

- [ ] **Step 3: Implement.** The GRB jets render into the HDR target at 4× intensity so they bloom. Notes: "Breakout
  flash: brightness from the breakout model, mostly UV and X-rays in reality" and "Limb darkening: a generic law
  (schematic)".

- [ ] **Step 4: Run the tests to see them pass**, then the full suite.

- [ ] **Step 5: Visual check and benchmark.**
  - Snapshots: the default preset just before, during and just after breakout; SN 1998bw during the burst.
  - `--bench 20` at the default quality with every Milestone 3 layer on. Update `docs/perf-2026-11.md`.

- [ ] **Step 6: Commit** ("3D view: breakout flash, limb darkening, jet glow, quality governor").

---

## Milestone 5: the guided tour

### Task 12: Tour model and instrument highlight

**Files:**
- Create: `src/SupernovaLab.App/Tour/TourStop.cs`, `TourBuilder.cs`, `TourCaptions.cs`
- Modify:
  - `src/SupernovaLab.Core/Presets/Preset.cs` and `PresetCatalog.cs` (`TourLines`);
  - `src/SupernovaLab.Core/Data/Embedded/presets.json` (lines for SN 1987A, SN 1054/Crab, SN 2011fe, SN 1998bw,
    SN 2023ixf, Cas A and Tycho where present);
  - `InstrumentPanelViewModel.cs`, `InstrumentTileViewModel.cs`, `InstrumentPanel.xaml`, `InstrumentStrip.xaml`,
    `MainViewModel.cs`.
- Test: `tests/SupernovaLab.Tests/TourBuilderTests.cs`, `PresetCatalogTests.cs`, `InstrumentPanelTests.cs`

**Interfaces:**
- Consumes: `EventMarkers.For(Outcome, ObserverSetup)`; `UnitFormat`.
- Produces:
  - `record TourLine(double SourceTimeS, string Text, Source Source, Messenger? Highlight)`; `Preset.TourLines`
    (`IReadOnlyList<TourLine>`, empty by default).
  - `record TourStop(double ObserverTimeS, string Caption, bool Cutaway, double DwellS, double OrbitDegPerS,
    Messenger? Highlight)`.
  - `static class TourBuilder`: `IReadOnlyList<TourStop> For(Outcome o, ObserverSetup obs, Preset? preset)`.
    - Stops come from the event markers in time order. Rules:
      - a stop between bounce and breakout has `Cutaway = true` and an orbit of 10°/s;
      - a stop at breakout highlights `Messenger.XRaysUv`;
      - a stop at the neutrino burst highlights `Messenger.Neutrinos`;
      - a stop at peak light highlights `Messenger.VisibleLight`.
    - Preset lines are merged in by time.
    - Each stop dwells for 4 s.
    - The first stop is always 1 ms with the caption "Core collapse" (or "Ignition" for a Type Ia).
  - `static class TourCaptions`: templates whose numbers are formatted with `UnitFormat` from model values. For
    example, "The shock breaks out {UnitFormat.Time(t_bo·(1+z))} after core bounce".
  - `[ObservableProperty] bool IsHighlighted` on `InstrumentPanelViewModel` and `InstrumentTileViewModel`.
    `MainViewModel.Highlight(Messenger? m)` sets exactly one panel and its tile, or none. A highlighted panel's outline
    frame is 2 px in the accent brush, replacing its normal 1 px border.

- [ ] **Step 1: Write the failing tests**
  - `EveryOutcomeType_GetsATour_StartingAt1ms_InTimeOrder`: one outcome per `SupernovaType`, plus each no-debris fate
    (Review Focus 1), plus every preset (spec criterion 4).
  - `CaptionNumbers_EqualTheModelValues`: for the default preset, the breakout caption contains
    `UnitFormat.Time(EjectaKinematics.BreakoutTimeS(o))` exactly (criterion 4).
  - `RedshiftedObserver_TourTimesAreObserverTimes` (Review Focus 3): z = 1 → each stop's time is 2× the source time.
  - `PresetLines_LoadFromJson_WithSources`.
  - `Highlight_SetsOnePanelAndItsTile_OrNone`.

- [ ] **Step 2: Run them to see them fail.**

- [ ] **Step 3: Implement.** Write the preset lines from `docs/research/presets.md` facts only, each carrying that
  fact's source. Mark recalled ([R]) facts as such in the line's `Source`.

- [ ] **Step 4: Run the tests to see them pass**, then the full suite.

- [ ] **Step 5: Commit** ("Guided tour: stops, captions, preset lines, instrument highlight").

### Task 13: Tour director and UI

**Files:**
- Create: `src/SupernovaLab.App/Tour/TourDirector.cs`, `src/SupernovaLab.App/ViewModels/TourViewModel.cs`, the tour
  overlay XAML in the main window's 3D area
- Modify: `MainViewModel.cs` (owns the `TourViewModel`), `SceneController.cs` (`IsCutaway`, orbit, a user-interaction
  event), `TimelineViewModel.cs` (a user-interaction event; the playback speed can be driven), `SnapshotRenderer.cs`
  (`--tour <stop>`), `Glossary.cs`
- Test: `tests/SupernovaLab.Tests/TourDirectorTests.cs`, `SnapshotRequestTests.cs`

**Interfaces:**
- Consumes: `TourBuilder`, `TourStop` (Task 12); `MainViewModel.Highlight`; `SceneController.IsCutaway`;
  `TimelineViewModel`.
- Produces:
  - `TourDirector`:
    - `TourDirector(MainViewModel vm, SceneController? scene)`;
    - `enum TourState { Off, Playing, Paused, Dwelling }`;
    - `void Start()`, `Pause()`, `Resume()`, `Next()`, `Previous()`, `Exit()`;
    - `void Tick(TimeSpan elapsed)`;
    - `int StopIndex`, `TourStop? Current`, `string? Note`.
  - Between stops, the timeline plays at `TimelineViewModel.DefaultDecadesPerSecond`, slowed to `MinDecadesPerSecond`
    within 0.2 decades of the next stop. At a stop it pauses for `DwellS`, shows the caption, sets the cutaway and
    highlight, and orbits the camera at `OrbitDegPerS`.
  - Any user input to the camera (drag, wheel, Home) or the timeline (scrub, click, play) pauses the tour, with "Resume
    tour" shown.
  - A new outcome exits the tour with the note "Tour ended: new explosion" (Review Focus 4).
  - Exit restores the user's cutaway setting and clears the highlight.
  - `TourViewModel`: `TourCommand`, `PauseCommand`, `NextCommand`, `PreviousCommand`, `ExitCommand`, `ResumeCommand`,
    `Caption`, `IsActive`, `ShowResume`.

- [ ] **Step 1: Write the failing tests**
  - `Tour_RunsStartToEnd_WithNoError` for every preset (criterion 4). Drive `Tick` in 1/60 s steps until `Off`; assert
    every stop was visited in order.
  - `Tour_SlowsNearAStop_AndDwells`.
  - `UserScrub_PausesTheTour_ResumeContinues`.
  - `NewOutcome_EndsTheTour_WithANote`.
  - `Exit_RestoresCutaway_AndClearsHighlight`.
  - `SnapshotRequest_ParsesTour`.

- [ ] **Step 2: Run them to see them fail.**

- [ ] **Step 3: Implement.** The Tour button sits beside Play on the timeline bar. The caption is a bottom-centre panel
  over the 3D view, with pause, previous, next and exit icons. Glossary key `GuidedTour`.

- [ ] **Step 4: Run the tests to see them pass**, then the full suite.

- [ ] **Step 5: Visual check.** One launch with `--tour 0..N` for SN 1987A: a caption frame at each stop, with the main
  and Instruments windows.

- [ ] **Step 6: Commit** ("Guided tour: director, controls and captions").

### Task 14: Finish: sweep, docs, validation

**Files:**
- Modify: `README.md`, `docs/perf-2026-11.md`, `docs/research/scientific-inconsistencies.md`, `SnapshotRenderer.cs`
  (if the sweep needs the new scene counted)
- Test: `tests/SupernovaLab.Tests/Project2ValidationTests.cs`

- [ ] **Step 1: Write the validation tests**, one per spec success criterion:
  - 1: `HoldFraction` at default speed (reuse Task 2's simulation through `SceneController`);
  - 2: masses (all presets);
  - 3: research ranges (mixing, shocks, nebula, rings);
  - 4: tours for every preset and type.
  - Criterion 5 is the benchmark in Step 3.

- [ ] **Step 2: Sweep.** `--sweep 40` must load every preset and step the timeline with no error, now counting errors
  in the 3D scene. This closes Project 1's parked minor: errors caught inside the timeline markers or the 3D scene were
  not counted.

- [ ] **Step 3: Final benchmark** at 1366×768 and 1920×1080, with the tour running. Record in `docs/perf-2026-11.md`.

- [ ] **Step 4: Docs.** Add to the README: what the showpiece shows, the rings, the director, the cutaway, the tour, the
  quality setting, and the new `--cutaway` and `--tour` options. Bring the inconsistencies list's statuses up to date.

- [ ] **Step 5: Run the full suite** (0 warnings). **Commit** ("Project 2: validation, sweep, docs").
