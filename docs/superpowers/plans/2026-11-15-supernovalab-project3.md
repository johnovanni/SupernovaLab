# SupernovaLab Project 3 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A Close-up window, beside the main window and on the same timeline, that shows:
- the first second at the core;
- the neutron star (with its crust and neutron drip), its spin, field, beams, pulse trace and optional sound;
- the magnetar and its giant flares;
- the black hole bending its real surroundings, with a grid switch and spin once it settles.

It also adds the "Expected" wording for signals that have never been observed.

**Architecture:**
- New physics lives in `SupernovaLab.Core/Compact/`: pure functions of an `Outcome`, an `ObserverSetup`, the run
  number and source time, with no OpenGL.
- The Rendering layer has a `CloseUpScene`, the single source of what the close-up draws at each moment (the
  counterpart of `ExplosionScene`), and a second GL host.
- Shared geometry (beam tilt, Earth direction, drawn rotation period, kick offset) comes only from Core, so the two views
  cannot disagree.
- The App adds the window, its view model, a pulse trace, a pulse-audio service, tour stops and the §7 changes to
  Projects 1 and 2.

**Tech Stack:** as Projects 1–2: .NET 8, WPF, CommunityToolkit.Mvvm 8.3.2, OpenTK 4.9.4, OpenTK.GLWpfControl 4.3.6,
ScottPlot.WPF 5.1.59 and xUnit, plus NAudio 2.2.1 (MIT) for the optional pulse sound (ruling P3-10).

**Spec:** `docs/superpowers/specs/2026-11-15-supernovalab-project3-design.md`, on top of the Project 1 and Project 2
specs in the same folder. Executors read the spec in full. This plan cites it as "spec §n".

**Research:** normative, as before:
- `docs/research/outcomes.md`, `signals.md`, `presets.md`, `structure.md`;
- the new `docs/research/compact.md`, written by Task 1. Its sections are §C1–§C11, defined in Task 1. Later tasks cite
  them. **Read the cited sections in full before coding.** Where this plan gives a number "until §Cn confirms it", the
  research's App-implementation block wins.

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

## Review Focus

1. **Changing the outcome while the Close-up is open:** a new preset, an input change, Re-roll, or an outcome with no
   remnant. The window retitles, the era and readouts change, the environment re-captures and the audio stops, all
   without an exception or a stale frame. Tests in Tasks 4 and 14.
2. **A redshifted observer** (z > 0):
   - The close-up's physics runs in source time.
   - The pulse sound and the trace's "real period" are the *received* period P·(1 + z).
   - Flares and event markers sit at observer times.

   Tests in Tasks 8 and 12.
3. **Hiding, minimizing, closing and reopening the Close-up mid-play, and closing the main window while it is open.**
   - Drawing stops while hidden.
   - Both GL contexts release their resources (`GlReleased`).
   - Audio stops when the window closes.
   - Reopening makes a fresh context with no leaked state.

   Tests in Task 4 (scene controller with a fake host) and Task 8 (audio).
4. **Extreme compact objects:**
   - a 2.2 M☉, 12 km neutron star (photon sphere 9.75 km, still inside the surface);
   - a 1 ms, 10¹⁶ G magnetar;
   - a 300 M☉ black hole;
   - spin 0.998;
   - a camera at 3 GM/c².

   The tracer, readouts and outlines stay finite: no NaN and no infinite loop (the step cap holds). Tests in Tasks 3, 5
   and 10.
5. **Scrubbing quickly back and forth across a giant flare or a beam crossing.** Flash safety must hold under user
   scrubbing, not just playback: a flare's drawn brightness also passes through a limiter that allows at most one rise
   per 0.5 s of wall time. Test in Task 12.

---

## Milestone 1: research, wording, optics, the speed test

### Task 1: The compact-remnant research file

**Files:**
- Create: `docs/research/compact.md`
- Modify: `docs/research/scientific-inconsistencies.md`

**Interfaces:**
- Produces §C1–§C11. Each item is tagged [V] verified against the source, [R] recalled, [D] derived, or [APPROX] with
  its reasoning, with a citation. Each section ends with an **App implementation** block giving the exact numbers or
  formula the code uses.

- [ ] **Step 1: Research and write each section**
  - **§C1 The first second.**
    - Mean radii vs time from bounce to about 1 s for the shock, the gain radius and the proto-neutron star (the
      neutrinosphere), from published 1D/3D simulations, as a table at t = 1, 3, 10, 30, 100, 200, 300, 500 ms and 1 s.
    - One track for an exploding core and one for a failed core.
    - Revival time.
    - Any published dependence on compactness or remnant mass.
    - The amplitude of SASI or convective distortion (as a fraction of R_shock).
    - Infall speed at the shock.
    - The pre-collapse iron-core radius, and the O-Ne-Mg core radius for electron-capture stars.
  - **§C2 Proto-neutron-star contraction** from about 1 s to about 1 minute, to the final radius (12 km).
  - **§C3 Early cooling.** T_s^∞ as a band from about a minute to 30 yr. It must join
    `NeutronStarCoolingModel`'s band at 30 yr. Cite the source of each segment.
  - **§C4 Pulsars:**
    - the beam half-opening angle vs period (for example Rankin 1993 / Kramer et al.: ρ ≈ 5.4° P^(−1/2));
    - when the magnetosphere forms;
    - published magnetic inclinations for the preset pulsars (the Crab, and any others);
    - measured proper-motion position angles for the preset neutron stars (the Crab pulsar, Cas A's CCO, others), with
      references.
  - **§C5 The neutron star's layers** for 1.2–2.2 M☉:
    - crust thickness;
    - the neutron-drip density (Baym, Pethick & Sutherland 1971, about 4 × 10¹¹ g/cm³) and its depth;
    - the outer/inner crust boundary;
    - the pasta region;
    - the core's composition, and the inner core as unknown;
    - crust crystallization time;
    - the evidence (glitches; crust cooling, e.g. KS 1731−260, MXB 1659−29).
  - **§C6 Magnetars:**
    - observed periods (expect about 2–12 s);
    - field decay;
    - the giant-flare rate per magnetar, derived from the 1979, 1998 and 2004 flares and the known magnetar count;
    - flare energies, spike duration and tail.
  - **§C7 Black-hole optics (exact results the tests use):**
    - the Schwarzschild shadow √27 GM/c² and the photon sphere 3GM/c²;
    - Bardeen's (1973) Kerr shadow curve;
    - the Bardeen–Press–Teukolsky ISCO;
    - the ergosphere and horizon;
    - weak-field deflection 4GM/(bc²);
    - the exact visible-surface fraction for a compact star.
  - **§C8 Black-hole spin:**
    - the spin values for Slow, Fast and Extreme rotation, and for gamma-ray-burst outcomes, with ranges;
    - observed spins (Cygnus X-1, A0620−00, the LVK catalogue);
    - the collapsar's starting spin and its spin-up over about 15 s of accretion (MacFadyen & Woosley 1999);
    - the fallback settling time;
    - the collapsar disk's outer extent.
  - **§C9 Observational checks:**
    - the EHT M87* and Sgr A* ring diameters, masses and distances, with the ring-to-shadow factor and a tolerance for
      the tests;
    - OGLE-2011-BLG-0462's mass estimates (Sahu et al. 2022; Lam et al. 2022; later analyses);
    - EHT's resolution (≈ 20 µas);
    - the Cas A neutron star's temperature (Heinke & Ho 2010).
  - **§C10 The "never observed" audit.** For each signal model and channel, either "observed in at least one supernova
    (cite)" or "never observed (reason)". Expected candidates:
    - gravitational waves (all);
    - neutrinos from a failed supernova;
    - the neutronization burst;
    - pair-instability signals;
    - pulsar pulses for SN 1987A-like outcomes.

    The App-implementation block lists `(modelId, channelId or "*", condition on Outcome)` triples.
  - **§C11 The 1987 bar detectors:**
    - the Rome and Maryland bars' strain sensitivity;
    - the energy a detectable signal would need;
    - the Galeotti et al. 2016 claim, and the standard objection.

- [ ] **Step 2: Log mismatches.** Add each new mismatch as #55 onwards. Update #53 and #54 with what §C9/§C11 confirm.

- [ ] **Step 3: Commit**
```bash
git add docs/research/compact.md docs/research/scientific-inconsistencies.md
git commit -m "Research: compact remnants (Project 3 §C1–§C11)"
```

### Task 2: "Expected" wording, and SN 1987A's disputed claim

**Files:**
- Create: `src/SupernovaLab.Core/Signals/NeverObserved.cs`
- Modify:
  - `src/SupernovaLab.App/ViewModels/InstrumentPanelViewModel.Detection.cs` (`Badge`, `OverWords`)
  - `src/SupernovaLab.App/Tour/TourCaptions.cs`
  - `src/SupernovaLab.App/Tour/TourBuilder.cs`
  - `src/SupernovaLab.Core/Data/Embedded/presets.json` (sn1987a `whatWasSeen`)
- Test: `tests/SupernovaLab.Tests/ExpectedWordingTests.cs`

**Interfaces:**
- Consumes: §C10's triples.
- Produces: `public static class NeverObserved { public static bool Applies(string modelId, string? channelId, Outcome o); public const string BadgeWord = "Expected"; }`.

- [ ] **Step 1: Write the failing tests**
```csharp
[Fact] public void GravitationalWaveBadge_SaysExpectedNotDetected()
{   var vm = Panels.ForPreset("betelgeuse").GravitationalWaves;   // 0.2 kpc: LIGO detects
    Assert.Equal("Expected", vm.BadgeText); Assert.Equal("Expected", vm.StripBadgeText); }
[Fact] public void GravitationalWaveBadge_TooFaintUnchanged()   // 50 kpc, non-rotating: too faint for Advanced LIGO
    => Assert.Equal("Too faint at this distance", Panels.AtDistanceKpc(50).GravitationalWaves.BadgeText);
[Fact] public void GravitationalWaveOver_SaysExpected()          // after the signal ends
    => Assert.Equal("Over: expected", Panels.ForPreset("betelgeuse").AtSourceTime(10).GravitationalWaves.StripBadgeText);
[Fact] public void NeutrinoBadge_ObservedSupernova_StillSaysDetected()
    => Assert.Equal("Detected", Panels.ForPreset("sn1987a").Neutrinos.BadgeText);
[Theory, MemberData(nameof(AuditTriples))] public void EveryAuditedNeverObservedSignal_SaysExpected(string model, string channel, string presetOrCase) { /* from §C10 */ }
[Fact] public void TourGravitationalWaveCaption_StartsExpected()
    => Assert.All(TourBuilder.For(o, obs, null).Where(s => s.Highlight == Messenger.GravitationalWaves),
                  s => Assert.StartsWith("Expected:", s.Caption));
[Fact] public void Sn1987aStory_MentionsDisputedBarClaim()
    => Assert.Contains("disputed", PresetCatalog.Get("sn1987a").WhatWasSeen);
```
`Panels` is a small test helper building the panel view models from a preset or an outcome (reuse `InstrumentPanelTests`' setup).

- [ ] **Step 2: Run and see them fail.** Run: `dotnet test tests/SupernovaLab.Tests --filter FullyQualifiedName~ExpectedWordingTests`. Expected: FAIL.

- [ ] **Step 3: Implement**
  - **`NeverObserved.Applies`** comes from §C10's table. The gravitational-wave model is always included.
  - **The badge:** in `Badge`, `DetectionStatus.Detected` maps to `("Expected", "Expected")` when
    `NeverObserved.Applies(model.Id, primaryChannel, Outcome)`.
  - **The "Over" words:** `OverWords` gains a `neverObserved` parameter giving `("Over: expected to be detected",
    "Over: expected")`.
  - **The tour:** `TourCaptions.Expected(string text)` returns `"Expected: " + text`. `TourBuilder` uses it in place of
    the "In the lab," form for any stop whose highlight messenger's model is never observed for this outcome.
  - **SN 1987A's note:** append to sn1987a `whatWasSeen`: "In 1987, gravitational-wave bar detectors in Rome and
    Maryland reported signals matching an earlier blip in the Mont Blanc neutrino detector; the claim is disputed,
    because those detectors were far too insensitive." Give it a source, from §C11.

- [ ] **Step 4: Run all tests.** Run: `dotnet test tests/SupernovaLab.Tests`. Expected: all pass. Existing tests that
  asserted "Detected" for gravitational waves are updated to "Expected", and each such change is listed in the ledger.

- [ ] **Step 5: Commit** — `git commit -m "Panels: never-observed signals say Expected (spec §7.1)"`.

### Task 3: Black-hole optics in Core, with the C# ray tracer

**Files:**
- Create:
  - `src/SupernovaLab.Core/Compact/Vec3.cs`
  - `src/SupernovaLab.Core/Compact/BlackHoleOptics.cs`
- Test: `tests/SupernovaLab.Tests/BlackHoleOpticsTests.cs`

**Interfaces:**
- Produces (lengths in GM/c² unless named otherwise):
```csharp
public readonly record struct Vec3(double X, double Y, double Z);   // + - * Dot Cross Length Normalized
public readonly record struct RayResult(RayFate Fate, Vec3 ExitPosition, Vec3 ExitDirection, int Steps);
public enum RayFate { Captured, Escaped, HitSurface, StepLimit }
public static class BlackHoleOptics
{
    public const double SchwarzschildShadowRadius = 5.196152422706632;   // √27
    public const double PhotonSphereRadius = 3;
    public const int MaxSteps = 400;
    public static double GravitationalRadiusCm(double massMsun);
    public static double HorizonRadius(double spin);                     // 1 + √(1 − a²)
    public static double IscoRadius(double spin, bool prograde = true);  // Bardeen–Press–Teukolsky
    public static double ErgosphereRadius(double spin, double polarAngleRad);
    public static IReadOnlyList<(double Alpha, double Beta)> KerrShadowOutline(double spin, double inclinationRad, int points);
    public static double WeakFieldDeflection(double impactParameter);    // 4/b
    public static RayResult TraceRay(double spin, Vec3 cameraPosition, Vec3 direction, double escapeRadius,
                                     double surfaceRadius = 0);          // ruling P3-7
    public static double VisibleSurfaceFraction(double massMsun, double radiusKm);    // exact, by quadrature
    public static double ApparentShadowDiameterRad(double massMsun, double distanceCm, double spin = 0);
}
```

- [ ] **Step 1: Write the failing tests** (values from §C7; check them against §C7's App block)
```csharp
[Fact] public void SchwarzschildShadow_TracerMatchesRoot27()
{   double b = BisectCaptureEdge(spin: 0, cameraR: 1000);   // largest captured impact parameter
    Assert.InRange(b, 5.196 * 0.995, 5.196 * 1.005); }
[Fact] public void TenSolarMasses_ShadowDiameter154Km()
    => Assert.InRange(2 * BlackHoleOptics.SchwarzschildShadowRadius * BlackHoleOptics.GravitationalRadiusCm(10) / 1e5, 152.9, 154.0);
[Theory, InlineData(0.9, 90), InlineData(0.9, 17)]
public void KerrShadow_TracerMatchesBardeen(double a, double incDeg)   // criterion 2: 1% of the shadow radius
{   foreach (var (alpha, beta) in BlackHoleOptics.KerrShadowOutline(a, incDeg * Math.PI / 180, 72))
        Assert.True(Math.Abs(EdgeAlongRay(a, incDeg, alpha, beta) - Math.Sqrt(alpha*alpha + beta*beta)) < 0.01 * 5.2); }
[Fact] public void Isco_KnownValues()
{   Assert.Equal(6, BlackHoleOptics.IscoRadius(0), 6);
    Assert.Equal(2.3209, BlackHoleOptics.IscoRadius(0.9), 3);
    Assert.Equal(1, BlackHoleOptics.IscoRadius(1), 3); }
[Fact] public void Ergosphere_EquatorIs2_PoleIsHorizon()
{   Assert.Equal(2, BlackHoleOptics.ErgosphereRadius(0.9, Math.PI / 2), 9);
    Assert.Equal(BlackHoleOptics.HorizonRadius(0.9), BlackHoleOptics.ErgosphereRadius(0.9, 0), 9); }
[Fact] public void WeakField_TracerMatches4OverB()   // b = 1000: 0.004 rad within 1%
    => Assert.InRange(DeflectionAt(spin: 0, b: 1000), 0.004 * 0.99, 0.004 * 1.01);
[Fact] public void NeutronStar_1p4_12km_VisibleFractionNear0p76()
{   double f = BlackHoleOptics.VisibleSurfaceFraction(1.4, 12);
    Assert.InRange(f, 0.74, 0.78);
    Assert.Equal(f, TracerVisibleFraction(1.4, 12), 2); }     // tracer agrees with the quadrature
[Fact] public void M87_ShadowLawWithinEhtTolerance()          // §C9: ~39.7 µas shadow vs 42 ± 3 µas ring
    => Assert.InRange(MicroArcsec(BlackHoleOptics.ApparentShadowDiameterRad(6.5e9, 16.8 * Mpc)) * C9.RingToShadow, 42 - C9.TolM87, 42 + C9.TolM87);
[Fact] public void SgrA_ShadowLawWithinEhtTolerance() { /* 4.297e6 M☉, 8.277 kpc, 51.8 ± 2.3 µas, per §C9 */ }
[Theory, InlineData(0.998, 3.0), InlineData(0, 1.2), InlineData(0.5, 1e6)]   // Review Focus 4
public void Tracer_NeverNaN_AlwaysTerminates(double a, double camR)
{   foreach (var dir in GridDirections(64)) { var r = BlackHoleOptics.TraceRay(a, new(camR, 0, 0.1), dir, 60);
        Assert.True(r.Steps <= BlackHoleOptics.MaxSteps); Assert.True(double.IsFinite(r.ExitDirection.X)); } }
```
`BisectCaptureEdge`, `EdgeAlongRay`, `DeflectionAt` and `TracerVisibleFraction` are test helpers that fire rays from
far away (r = 1000) with given screen coordinates and bisect between captured and escaped.

- [ ] **Step 2: Run and see them fail.** Run: `dotnet test tests/SupernovaLab.Tests --filter FullyQualifiedName~BlackHoleOpticsTests`. Expected: FAIL (types missing).

- [ ] **Step 3: Implement.** Follow ruling P3-7 exactly. Use the Kerr–Schild geodesic equation in Hamiltonian form,
  H = ½ g^{μν} p_μ p_ν, integrated in x and p.
  - `KerrShadowOutline`: Bardeen's parametric curve over photon-orbit radii r ∈ [r_ph−, r_ph+], with
    ```
    ξ = −(r³ − 3r² + a²r + a²)/(a(r − 1))
    η = r³(4a² − r(r − 3)²)/(a²(r − 1)²)
    α = −ξ/sin i
    β = ±√(η + a²cos²i − ξ²cot²i)
    ```
    Keep only real β. Use the a → 0 limit (a circle of radius √27) when a < 10⁻⁴.
  - `VisibleSurfaceFraction`: (1 − cos ψ_max)/2, where ψ_max = ∫ from R to ∞ of dr / (r² √(1/b_c² − (1 − r_s/r)/r²)),
    with b_c = R/√(1 − r_s/R). Use adaptive Simpson with the endpoint singularity substituted away.

- [ ] **Step 4: Run and see them pass.** Same command. Expected: PASS.

- [ ] **Step 5: Commit** — `git commit -m "Core: black-hole optics and ray tracer (spec §6.6, criterion 2)"`.

### Task 4: The Close-up window, its GL host, the lensing pass, and the speed test

**Files:**
- Create:
  - `src/SupernovaLab.App/Windows/CloseUpWindow.xaml(.cs)`
  - `src/SupernovaLab.App/ViewModels/CloseUpViewModel.cs`
  - `src/SupernovaLab.App/Controllers/CloseUpSceneController.cs`
  - `src/SupernovaLab.App/Services/BeamClock.cs`
  - `src/SupernovaLab.Rendering/CloseUpScene.cs`
  - `src/SupernovaLab.Rendering/EnvironmentCapture.cs`
  - `src/SupernovaLab.Rendering/LensingPass.cs`
  - `src/SupernovaLab.Rendering/Shaders/lensing.frag`
- Modify:
  - `IWindowService.cs` and `WindowService.cs` (`ShowCloseUp()`)
  - `tests/SupernovaLab.Tests/FakeWindowService.cs`
  - `MainViewModel.cs` (`ShowCloseUpCommand`)
  - `MainWindow.xaml` (View ▸ Close-up)
  - `Views/OutcomeSummary.xaml` (Close-up button beside the remnant)
  - `Services/SnapshotRenderer.cs` (`--closeup`, `--window closeup`, `--bench` measuring both)
  - `SupernovaLab.Rendering.csproj` (embed the shader)
- Test:
  - `tests/SupernovaLab.Tests/CloseUpSceneTests.cs`
  - `CloseUpWindowTests.cs`
  - additions to `SnapshotRequestTests.cs`
  - `ShaderSourceTests.cs` (lensing.frag compiles under the existing source checks)

**Interfaces:**
- Consumes: `BlackHoleOptics` (Task 3); `ExplosionScene` and its renderers (Project 2); `GlHost`/`IRenderHost`;
  `GpuTimer`; `QualityGovernor`.
- Produces:
```csharp
public enum CloseUpEra { None, PreCollapse, FirstSecond, NeutronStar, BlackHoleForming, BlackHoleSettling, BlackHole }
public sealed record Readout(string Label, Quantity Value, string? ToolTip = null);
public sealed record CloseUpState
{   public CloseUpEra Era { get; init; } public string Title { get; init; } = "";
    public IReadOnlyList<Readout> Readouts { get; init; } = Array.Empty<Readout>();
    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
    public double FrameRadiusKm { get; init; } public string? NoRemnantMessage { get; init; } }
public sealed class CloseUpScene
{   public CloseUpScene(Outcome outcome, ObserverSetup observer, int runNumber);
    public CloseUpState At(double sourceTimeS);
    public const string NoTelescopeNote = "Camera where no telescope could be";
    public const string DestroyedMessage = "No compact remnant: the star was destroyed completely.";
    public const string WhiteDwarfMessage = "This star leaves a white dwarf. The white-dwarf close-up is not part of the lab."; }
// App
void IWindowService.ShowCloseUp();
public sealed partial class CloseUpViewModel : ObservableObject   // binds to MainViewModel's Outcome, Observer, Timeline, RunNumber
{   public string Title; public IReadOnlyList<Readout> Readouts; public IReadOnlyList<string> Notes; public string? NoRemnantMessage;
    public bool ShowGrid, ShowFieldLines, ShowLightCylinder, IsCutaway, ShowLabels = true, IsFocusView; }
```
In this task, `CloseUpScene` handles `None`, plus a placeholder black-hole era (mass only, spin 0) so the lensing pass
has something to draw. Later tasks fill in the eras.

- [ ] **Step 1: Write the failing tests**
```csharp
[Theory, InlineData("sn2011fe"), InlineData("tycho")]        // Type Ia: destroyed
public void NoRemnant_Destroyed(string id) => Assert.Equal(CloseUpScene.DestroyedMessage, SceneFor(id).At(1e7).NoRemnantMessage);
[Fact] public void WhiteDwarfOutcome_GetsWhiteDwarfMessage() => Assert.Equal(CloseUpScene.WhiteDwarfMessage, SceneFor(Lab(mass: 5)).At(1).NoRemnantMessage);
[Fact] public void ShowCloseUp_OpensOnce() { vm.ShowCloseUpCommand.Execute(null); vm.ShowCloseUpCommand.Execute(null); Assert.Equal(2, fake.CloseUpCalls); /* service brings to front */ }
[Fact] public void CloseUpViewModel_FollowsOutcomeChange_NoException()      // Review Focus 1
{   var cu = OpenCloseUp(preset: "n6946bh1"); main.LoadPreset("sn2011fe"); Assert.NotNull(cu.NoRemnantMessage); main.Reroll(); }
[Fact] public void CloseUpController_StopsDrawingWhenHidden_ReleasesOnClose()   // Review Focus 3, with a fake IRenderHost
{   var c = Controller(fakeHost); c.SetVisible(false); fakeHost.Tick(); Assert.Equal(0, c.FramesDrawn);
    c.Dispose(); Assert.True(c.GlReleased); }
[Fact] public void Snapshot_ParsesCloseUpOptions()
{   var r = SnapshotRequest.Parse(new[] { "--snapshot", "out", "--closeup", "--window", "closeup" })!;
    Assert.True(r.OpenCloseUp); Assert.Contains(SnapshotWindow.CloseUp, r.Windows); }
[Fact] public void EnvironmentCapture_RefreshRule()   // ruling P3-6, as a pure function
{   Assert.False(EnvironmentCapture.NeedsRefresh(prevLogT: 3.000, logT: 3.004, sameOutcome: true, wallSinceLast: 1));
    Assert.True(EnvironmentCapture.NeedsRefresh(3.000, 3.006, true, 1));
    Assert.False(EnvironmentCapture.NeedsRefresh(3.0, 4.0, true, wallSinceLast: 0.2)); }   // ≤ 4 per s
```

- [ ] **Step 2: Run and see them fail.** Run: `dotnet test tests/SupernovaLab.Tests --filter "FullyQualifiedName~CloseUp|FullyQualifiedName~SnapshotRequest"`. Expected: FAIL.

- [ ] **Step 3: Implement the window and the plumbing**
  - **The window:** 900×640 DIP by default. Its layout is spec §2.1's sketch: the view on the left, the readouts
    column (with `Notes ▾`) on the right, and the pulse-trace row at the bottom, collapsed until Task 8.
  - **Its menu:** Grid, Field lines, Light cylinder, Cutaway, Labels, Focus view (F). Sound is added in Task 8.
  - **Opening and closing:** `WindowService.ShowCloseUp` opens it at most once, remembers its size and position for the
    session, and closes it with the main window.
  - **The GL host:** `CloseUpSceneController` follows `SceneController`'s pattern, with its own `GlHost`. It skips
    drawing when `Visibility != Visible` or the window is minimized.
  - **The beam clock:** `BeamClock` is registered once and handed to both controllers. Replace
    `SceneController.cs:841`'s `_wall` with it.
  - **The camera (spec §2.2):** the controller reuses `OrbitCamera`, `CameraDirector` and `DistanceRings`. The ring
    ladder is extended downward to metres (1-2-5 rungs in m below 1 km). Drag rotates, the wheel zooms, Home hands the
    camera back to the director. `CloseUpState.FrameRadiusKm` is what the director frames.
- [ ] **Step 4: Implement the lensing pass**
  - `EnvironmentCapture` renders Project 2's scene into the cube map from the remnant's position (ruling P3-6).
  - `LensingPass` runs `lensing.frag`: the GLSL mirror of `TraceRay`. Its uniforms are spin, camera position and basis,
    R_esc, the surface radius and the grid flag. It renders at the P3-9 level and upsamples.
  - Add the `"Camera where no telescope could be"` note.

- [ ] **Step 5: Run the tests and see them pass.** Run: `dotnet test tests/SupernovaLab.Tests`. Expected: all pass.

- [ ] **Step 6: The speed test (one app launch)**
  - Run:
    ```
    SupernovaLab.exe --snapshot out\p3t4 --preset n6946bh1,sn1998bw --closeup --window main,closeup --size 1366x768 --bench 20
    ```
    The placeholder black hole is given spin 0.9 for the bench, through a `--bench-spin 0.9` diagnostic flag.
  - Record, at 1366×768 + 900×640 (and again at 1920×1080):
    - both views' fps;
    - GPU ms per frame;
    - the lensing level reached.

    Write them into `docs/perf-2026-11.md`.
  - **The decision (spec §2.5):**
    - **≥ 50 fps for both at level 1/2 or better:** keep the window.
    - **Otherwise:** write ruling R-P3-1 for the inset fallback, and **stop and report to the owner before Task 5**.
- [ ] **Step 7: Commit** — `git commit -m "Close-up window, lensing pass and speed test (spec §2, §6.6)"`.

### Task 4b: Speed-ups and the user's light-bending detail setting (added 2026-11-15, owner's decision)

**Files:** `CloseUpSceneController.cs`, `LensingPass.cs`, `lensing.frag`, `CloseUpWindow.xaml(.cs)`, `CloseUpViewModel.cs`,
`Services/LayoutStore.cs` (`CloseUpLensingDetail`), `docs/perf-2026-11.md`; tests in `CloseUpWindowTests.cs`,
`CloseUpSceneTests.cs`, `SnapshotRequestTests.cs`.

**Interfaces:** Produces `public enum LensingDetail { Auto, Full, Half, Quarter }`, `CloseUpViewModel.LensingDetail` (bound
to Close-up menu ▸ Light-bending detail, radio items), `LayoutSettings.CloseUpLensingDetail` (default Auto), and the note
`"Light bending: {full|half|quarter} resolution ({your setting|automatic})"`.

- [ ] **Step 1: Baseline (launch 1).** Run the `--bench-baseline` command recorded in `docs/perf-2026-11.md` and attribute
  the fixed per-frame cost to its real cause. Record the numbers.
- [ ] **Step 2: Speed-ups, aimed by Step 1.** At minimum, ruling R-P3-10 (**cache the traced rays**): the ray map (each
  pixel's fate and exit direction, or surface hit point) depends only on the camera, the spin, the viewport and the
  level — not on time. Trace into a cached RGBA16F/RGBA32F target only when those change; every other frame only samples
  the environment through the cache. While the camera moves, trace at the governed level; when it stops, refine to the
  user's level over the next frames. Add whatever Step 1 shows is the fixed cost's fix.
- [ ] **Step 3: The user's setting.** The Close-up menu ▸ Light-bending detail (Auto / Full / Half / Quarter), persisted in
  layout.json, honoured by the controller (Auto = governor), with the note.
- [ ] **Step 4: Tests.** Cache invalidation (camera, spin, viewport, level change → retrace; time change → no retrace);
  the setting round-trips through LayoutStore and an unknown stored value falls back to Auto; the note text per level.
- [ ] **Step 5: Bench again (launch 2).** Both views at 1366×768 + 900×640 and 1920×1080, playing, camera still and camera
  orbiting, at each level. Record in `docs/perf-2026-11.md`. Target ≥ 50 fps at Full with a still camera, and at the
  Auto level while orbiting.
- [ ] **Step 6: Commit.**

---

## Milestone 2: the neutron star

### Task 5: Neutron-star state, early cooling, magnetosphere geometry, kick drift

**Files:**
- Create:
  - `src/SupernovaLab.Core/Compact/NeutronStarModel.cs`
  - `src/SupernovaLab.Core/Compact/Magnetosphere.cs`
  - `src/SupernovaLab.Core/Compact/KickDrift.cs`
- Modify:
  - `Outcomes/Outcome.cs` (`KickPositionAngleDeg`, `MagneticInclinationDeg`, ruling P3-4)
  - `Presets/Preset.cs` and `PresetCatalog.cs`
  - `presets.json` (only §C4 values)
  - `docs/research/scientific-inconsistencies.md` (#54 status)
- Test:
  - `tests/SupernovaLab.Tests/NeutronStarModelTests.cs`
  - `MagnetosphereTests.cs`
  - `KickDriftTests.cs`

**Interfaces:**
- Consumes: `PulsarModel.Spin`, `PulsarModel.EmergenceTimeS`, `NeutronStarCoolingModel`, `Remnant`, `ObserverSetup`.
- Produces:
```csharp
public sealed record NeutronStarState(double SourceTimeS, double MassMsun, double RadiusKm, Quantity SurfaceTemperatureK,
    double PeriodS, double PeriodDerivative, double FieldG, bool IsMagnetar, bool HasCrust, double AgeS, Vec3 OffsetCm);
public static class NeutronStarModel
{   public const double EarlyStartS = 60;                                  // §C3
    public static NeutronStarState? At(Outcome o, ObserverSetup obs, int runNumber, double sourceTimeS);
    public static Quantity SurfaceTemperature(Outcome o, double sourceTimeS);   // early band (§C3) < 30 yr; model's band 30–100 yr; value after
    public static string? CasAComparison(Outcome o, double sourceTimeS);       // the #54 tooltip text for the casa preset, else null
}
public static class Magnetosphere
{   public const double MinDrawnPeriodS = 2; public const double DefaultTiltDeg = 45;
    public static double DrawnPeriodS(double realPeriodS);       // max(real, 2)
    public static string SpeedLabel(double realPeriodS);         // "Shown {N}× slower" (N rounded to 2 significant figures) / "Shown at real speed"
    public static double LightCylinderKm(double periodS);        // c P / 2π
    public static double BeamHalfAngleRad(double periodS);       // §C4
    public static double TiltRad(Outcome o);
    public static double EarthAngleRad(Outcome o, ObserverSetup obs);    // ζ, ruling P3-3 / spec §4.3
    public static Vec3 EarthDirection(Outcome o, ObserverSetup obs);
    public static bool BeamCanCrossEarth(Outcome o, ObserverSetup obs);  // |ζ − α| ≤ ρ
    public static double MagnetosphereFormsS(Outcome o); }               // §C4
public static class KickDrift
{   public static Vec3 Direction(Outcome o, ObserverSetup obs, int runNumber);   // preset PA in the sky plane, else seeded isotropic
    public static Vec3 OffsetCm(Outcome o, ObserverSetup obs, int runNumber, double sourceTimeS);
    public const string LineOfSightNote = "Kick: direction measured on the sky; its line-of-sight part is unknown and set to zero"; }
```

- [ ] **Step 1: Write the failing tests**
```csharp
[Fact] public void Crab_PeriodAtAge_And_LightCylinder()      // criterion 3
{   var ns = NeutronStarModel.At(Crab, CrabObs, 1, CrabAgeS)!;
    Assert.InRange(ns.PeriodS, 0.0334 * 0.95, 0.0334 * 1.05);    // Project 1's tolerance; use its existing constant
    Assert.InRange(Magnetosphere.LightCylinderKm(0.0334), 1590, 1600); }
[Fact] public void DrawnPeriod_NeverUnder2s_CrabAbout60x()
{   Assert.Equal(2, Magnetosphere.DrawnPeriodS(0.0334)); Assert.Equal("Shown 60× slower", Magnetosphere.SpeedLabel(0.0334));
    Assert.Equal("Shown at real speed", Magnetosphere.SpeedLabel(5)); }
[Fact] public void CasA_TemperatureComparisonDisclosed()     // #54
{   var t = NeutronStarModel.SurfaceTemperature(CasA, 340 * Year);
    Assert.InRange(t.Value, 1.75e6, 1.9e6);
    Assert.Contains("2.0", NeutronStarModel.CasAComparison(CasA, 340 * Year)); }
[Fact] public void Temperature_BandBefore100yr_JoinsAt30yr()
{   var early = NeutronStarModel.SurfaceTemperature(Default, 29.9 * Year); var model = NeutronStarModel.SurfaceTemperature(Default, 30.1 * Year);
    Assert.NotNull(early.Range); Assert.NotNull(model.Range); Assert.InRange(early.Value / model.Value, 0.97, 1.03);
    Assert.Null(NeutronStarModel.SurfaceTemperature(Default, 200 * Year).Range); }
[Fact] public void Temperature_FiniteFromOneMinute() => Assert.True(double.IsFinite(NeutronStarModel.SurfaceTemperature(Default, 60).Value));
[Theory, InlineData(true), InlineData(false)]
public void EarthAngle_HonoursBeamCheckbox(bool toward) => Assert.Equal(toward, Magnetosphere.BeamCanCrossEarth(Default, Obs(beamTowardEarth: toward)));
[Fact] public void Kick_400kms_1000yr_About0p41pc()
    => Assert.InRange(KickDrift.OffsetCm(Kicked(400), Obs(), 1, 1000 * Year).Length() / Pc, 0.40, 0.42);
[Fact] public void Kick_DirectionDependsOnlyOnRunNumber()
{   Assert.Equal(KickDrift.Direction(o, obs, 3), KickDrift.Direction(o, obs, 3));
    Assert.NotEqual(KickDrift.Direction(o, obs, 3), KickDrift.Direction(o, obs, 4)); }
[Fact] public void Kick_PresetDirection_LiesInSkyPlane()     // only if §C4 pins one
{   var d = KickDrift.Direction(CasA, CasAObs, 1); Assert.Equal(0, d.Dot(Magnetosphere.EarthDirection(CasA, CasAObs)), 9); }
[Fact] public void DirectCollapseBlackHole_NoKick() => Assert.Equal(0, KickDrift.OffsetCm(FailedSn, obs, 1, 1e9).Length());
[Theory, InlineData(2.2, 1e-3, 1e16)]                        // Review Focus 4
public void Extremes_Finite(double m, double p0, double b) { /* NeutronStarModel.At on a pinned outcome: every field finite */ }
```

- [ ] **Step 2: Run and see them fail.** Run: `dotnet test tests/SupernovaLab.Tests --filter "FullyQualifiedName~NeutronStarModel|FullyQualifiedName~Magnetosphere|FullyQualifiedName~KickDrift"`. Expected: FAIL.

- [ ] **Step 3: Implement the three classes** from §C3/§C4 and the interfaces.
  - `EarthAngleRad`:
    - checkbox on → α for a pulsar, 0 for jets;
    - checkbox off → α + ρ + 10°, reflected if it exceeds π.
  - Magnetars use `PulsarModel.Spin` exactly as `PulsarModel` does (their t_p convention).

- [ ] **Step 4: Run and see them pass.** Same command, then the full suite. Expected: all pass.

- [ ] **Step 5: Commit** — `git commit -m "Core: neutron-star state, magnetosphere geometry, kick drift (spec §4)"`.

### Task 6: Drawing the neutron star; the main view's matching changes

**Files:**
- Create:
  - `src/SupernovaLab.Rendering/NeutronStarRenderer.cs`
  - `FieldLineRenderer.cs`
  - `Shaders/nsurface.frag` (shading inside the lensing pass's surface hit)
  - `Shaders/fieldline.vert/.frag`
- Modify:
  - `CloseUpScene.cs` (the `NeutronStar` era: title, readouts per spec §4.5, notes)
  - `ExplosionScene.cs` (`SceneState.RemnantOffsetCm`, `BeamHalfAngleRad`, `BeamTiltRad`)
  - `RemnantRenderer.cs` (use them instead of the 45° and 10° constants)
  - `SceneController.cs` (pass the offset)
- Test:
  - `CloseUpSceneTests.cs`
  - `ExplosionSceneTests.cs` (additions)
  - `ViewsAgreeTests.cs` (new)

**Interfaces:**
- Consumes: Task 5's types, `BlackbodyColor`, `LensingPass` (`surfaceRadius` = the star's radius in GM/c², spin 0).
- Produces: `CloseUpState.NeutronStar` (a `NeutronStarState?`), `CloseUpState.BeamPhaseRad`,
  `CloseUpState.SpeedLabel`, `CloseUpState.LightCylinderKm`, `CloseUpState.SurfaceColor` (an `(R, G, B)` float
  tuple), `CloseUpState.ShowBeams`, and `SceneState.RemnantOffsetCm` (a `Vec3`).

- [ ] **Step 1: Write the failing tests**
```csharp
[Fact] public void BothViews_SameRemnantPosition()           // criterion 4
{   foreach (var t in LogTimes(1, 3.15e10, 40)) Assert.Equal(KickDrift.OffsetCm(o, obs, run, t), MainScene.At(t).RemnantOffsetCm); }
[Fact] public void BothViews_SameBeamTiltAndWidth()
{   var s = MainScene.At(CrabAgeS); Assert.Equal(Magnetosphere.TiltRad(Crab), s.BeamTiltRad);
    Assert.Equal(Magnetosphere.BeamHalfAngleRad(s.PulsarPeriodS), s.BeamHalfAngleRad); }
[Fact] public void BothViews_SamePhaseFromBeamClock() { /* both controllers fed one fake BeamClock read identical phase */ }
[Fact] public void NeutronStarEra_ReadoutsAndNotes()
{   var st = SceneFor("sn1054").At(CrabAgeS);
    Assert.Equal(CloseUpEra.NeutronStar, st.Era); Assert.StartsWith("Close-up: Neutron star (pulsar, 33", st.Title);
    Assert.Equal(new[] { "Mass", "Radius", "Spin", "Field", "Surface", "Age", "Kick" }, st.Readouts.Select(r => r.Label));
    Assert.Contains(st.Notes, n => n.Contains("mostly in X-rays")); Assert.Contains("Shown 60× slower", st.SpeedLabel); }
[Fact] public void NeutronStar_ColourIsBlackbodyOfSurfaceTemperature() { /* CloseUpState.SurfaceColor == BlackbodyColor.Of(T) */ }
```

- [ ] **Step 2: Run and see them fail.**

- [ ] **Step 3: Implement**
  - **The surface:** shaded where `TraceRay` returns `HitSurface`, with the blackbody colour of T_s^∞, tone-mapped (note
    C12). The view shows the star's far side past its limb because the tracer bends the rays.
  - **The field lines:** dipole lines r = L sin²θ in the magnetic frame, with L values spaced so their number grows
    with log₁₀ B (schematic). Lines whose L exceeds R_L are drawn only to R_L and labelled open.
  - **The light-cylinder ring:** a labelled ring, switchable.
  - **The beams:** use `JetRenderer`'s cones with `BeamHalfAngleRad`.
    - `ShowBeams` is true from `Magnetosphere.MagnetosphereFormsS`.
    - The main view keeps its emergence rule, and a close-up note explains the difference: "Beams exist from birth; the
      main view draws them once the pulses escape the debris" (spec C6).
  - **The Earth arrow:** labelled "To Earth".
  - **The readouts:** each `Readout` carries its `Quantity` (confidence and source shown by the existing badge
    template).

- [ ] **Step 4: Run all tests.** Expected: pass.

- [ ] **Step 5: Visual check (one launch).** Run:
  ```
  --snapshot out\p3t6 --preset sn1054,casa,sn1987a --closeup --window main,closeup --time 3e10
  ```
  Check the surface, the limb, the field lines, the light cylinder, the beams, the arrow, and that the main view's marker
  is off-centre.

- [ ] **Step 6: Commit** — `git commit -m "Close-up: the neutron star; main view drift and beams follow (spec §4, §7.2)"`.

### Task 7: The neutron star's layers and the neutron drip line

**Files:**
- Create:
  - `src/SupernovaLab.Core/Compact/NeutronStarLayers.cs`
  - `src/SupernovaLab.Rendering/NeutronStarCutawayRenderer.cs`
  - `Shaders/nscut.frag`
- Modify:
  - `CloseUpScene.cs` (layers; `HasCrust`; the crust-forms event)
  - `NeutronStarModel.cs` (`HasCrust` from `CrustFormationTimeS`)
  - `Text/Glossary.cs` (neutron drip, nuclear pasta, inner crust, superfluid)
- Test: `tests/SupernovaLab.Tests/NeutronStarLayersTests.cs`

**Interfaces:**
- Produces:
```csharp
public sealed record StarLayer(string Name, double InnerRadiusKm, double OuterRadiusKm, Confidence Confidence, Source Source, string Label);
public static class NeutronStarLayers
{   public static IReadOnlyList<StarLayer> For(double massMsun, double radiusKm);   // centre outward: inner core, outer core, pasta, inner crust, outer crust, atmosphere
    public static double DripDensityGcm3 { get; }                                  // §C5 (≈ 4.3e11)
    public static double DripDepthKm(double massMsun, double radiusKm);
    public static double CrustFormationTimeS(Outcome o);                            // §C5
    public static int Magnification(double crustKm, double radiusKm);              // the strip's ×N, so the crust fills ≥ 40% of the strip
    public const string UnknownCoreLabel = "Inner core: unknown";
    public const string DripConfidenceText = "Established theory, observed indirectly"; }
```

- [ ] **Step 1: Write the failing tests**
```csharp
[Fact] public void Layers_OrderedContiguous_FromCentreToSurface()
{   var L = NeutronStarLayers.For(1.4, 12); Assert.Equal(0, L[0].InnerRadiusKm); Assert.Equal(12, L[^1].OuterRadiusKm, 6);
    for (int i = 1; i < L.Count; i++) Assert.Equal(L[i - 1].OuterRadiusKm, L[i].InnerRadiusKm, 9); }
[Fact] public void Drip_DensityAndDepthFromResearch()
{   Assert.InRange(NeutronStarLayers.DripDensityGcm3, 4.0e11, 4.4e11);
    Assert.InRange(NeutronStarLayers.DripDepthKm(1.4, 12), C5.DripDepthLowKm, C5.DripDepthHighKm); }
[Fact] public void Crust_About8PercentOfRadius() => Assert.InRange(CrustKm(1.4, 12) / 12, 0.05, 0.11);
[Fact] public void InnerCore_LabelledUnknown_PastaModelDependent()
{   var L = NeutronStarLayers.For(1.4, 12); Assert.Equal(NeutronStarLayers.UnknownCoreLabel, L[0].Label);
    Assert.Equal(Confidence.ModelDependent, L.Single(l => l.Name == "Nuclear pasta").Confidence); }
[Fact] public void NoCrust_BeforeCrystallization_ThenCrust()
{   double tc = NeutronStarLayers.CrustFormationTimeS(Default);
    Assert.False(NeutronStarModel.At(Default, obs, 1, tc * 0.9)!.HasCrust); Assert.True(NeutronStarModel.At(Default, obs, 1, tc * 1.1)!.HasCrust); }
[Theory, InlineData(1.2, 12), InlineData(2.2, 12)] public void Layers_ValidAcrossMassRange(double m, double r) { /* all radii finite, ordered */ }
```

- [ ] **Step 2: Run and see them fail.**

- [ ] **Step 3: Implement**
  - The layers come from §C5.
  - **The cutaway:** Project 2's wedge idea on a sphere, with flat analytic faces in `nscut.frag`. The layer colours are
    schematic, with a legend.
  - **The magnified crust strip:** an inset band labelled "Magnified ×N". The drip line is drawn and labelled with its
    density.
  - **Before the crust forms:** a uniform hot interior, labelled "Proto-neutron star: hot and liquid, no crust yet".
  - The tooltip text follows spec §4.2.

- [ ] **Step 4: Run all tests.** Expected: pass.

- [ ] **Step 5: Commit** — `git commit -m "Close-up: neutron-star layers and the neutron drip line (spec §4.2)"`.

### Task 8: The pulse trace and the optional pulse sound

**Files:**
- Create:
  - `src/SupernovaLab.Core/Compact/PulseSchedule.cs`
  - `src/SupernovaLab.App/Views/PulseTrace.xaml(.cs)`
  - `src/SupernovaLab.App/Services/IPulseAudio.cs`
  - `NAudioPulseAudio.cs`
  - `tests/SupernovaLab.Tests/FakePulseAudio.cs`
- Modify:
  - `CloseUpViewModel.cs` (`IsSoundOn` = false, `Volume`, `TraceText`, `SpeakerState`)
  - `CloseUpWindow.xaml` (menu ▸ Sound; the trace row)
  - `SupernovaLab.App.csproj` (NAudio 2.2.1)
  - `WindowService.cs` (stop the audio on close)
- Test:
  - `tests/SupernovaLab.Tests/PulseScheduleTests.cs`
  - `PulseAudioTests.cs`

**Interfaces:**
- Produces:
```csharp
public enum PulseVisibility { Received, HiddenByDebris, BeamMissesEarth, NotAPulsar }
public static class PulseSchedule
{   public static PulseVisibility Visibility(Outcome o, ObserverSetup obs, double sourceTimeS);  // agrees with PulsarModel.EmergenceTimeS and Magnetosphere.BeamCanCrossEarth
    public static double ReceivedPeriodS(Outcome o, ObserverSetup obs, double sourceTimeS);     // P(t)·(1+z)
    public static string TraceText(Outcome o, ObserverSetup obs, double sourceTimeS);           // e.g. "Sound: real rate, 30 a second · Picture: shown 60× slower"; or the reason text
    public const string HiddenText = "To Earth: hidden by debris"; public const string MissesText = "Beam misses Earth"; }
public interface IPulseAudio : IDisposable { bool Enabled { get; set; } float Volume { get; set; } void SetPulsePeriod(double? receivedPeriodS); }
```
The view model calls `SetPulsePeriod(period)` only when `Visibility == Received` and the timeline is playing, and
`SetPulsePeriod(null)` otherwise.

- [ ] **Step 1: Write the failing tests**
```csharp
[Fact] public void HiddenUntilEmergence_ThenReceived()
{   Assert.Equal(PulseVisibility.HiddenByDebris, PulseSchedule.Visibility(Crab, CrabObs, PulsarModel.EmergenceYears * Year * 0.9));
    Assert.Equal(PulseVisibility.Received, PulseSchedule.Visibility(Crab, CrabObs, CrabAgeS)); }
[Fact] public void BeamOff_Misses() => Assert.Equal(PulseVisibility.BeamMissesEarth, PulseSchedule.Visibility(Crab, Obs(beamTowardEarth: false), CrabAgeS));
[Fact] public void Redshift_StretchesReceivedPeriod()       // Review Focus 2
    => Assert.Equal(0.0334 * 1.1, PulseSchedule.ReceivedPeriodS(CrabPinned, Obs(z: 0.1), CrabAgeS), 4);
[Fact] public void Sound_OffByDefault_AndSilentWhenHidden()
{   var cu = CloseUpFor("sn1054", fakeAudio); Assert.False(cu.IsSoundOn);
    cu.IsSoundOn = true; Play(cu, CrabAgeS); Assert.Equal(PulseSchedule.ReceivedPeriodS(...), fakeAudio.LastPeriod);
    Scrub(cu, 5 * Year); Assert.Null(fakeAudio.LastPeriod); Assert.Equal(PulseSchedule.HiddenText, cu.TraceText); }
[Fact] public void Sound_StopsOnPause_AndOnWindowClose() { /* Pause → null; Close → fake disposed */ }   // Review Focus 3
[Fact] public void Trace_PulsesAtDrawnRate_NeverOver1Hz() { /* trace marks per wall second ≤ 1 for every preset pulsar */ }
```

- [ ] **Step 2: Run and see them fail.**

- [ ] **Step 3: Implement**
  - **`PulseTrace`:** a small WPF control. It draws a tick each time `BeamClock` phase crosses the Earth arrow
    (two beams), scrolling over the last 10 s. Show the speaker symbol (on, or crossed out) and the volume.
  - **`NAudioPulseAudio`:** ruling P3-10. A click train is regenerated whenever the received period changes by
    more than 0.1%. Periods under about 1 ms become a continuous tone at 1/P, which is what a click train is at that
    rate.

- [ ] **Step 4: Run all tests.** Expected: pass.

- [ ] **Step 5: Commit** — `git commit -m "Close-up: pulse trace and optional pulsar sound (spec §4.4)"`.

---

## Milestone 3: the first second

### Task 9: Core collapse history and its drawing

**Files:**
- Create:
  - `src/SupernovaLab.Core/Compact/CoreCollapseHistory.cs`
  - `src/SupernovaLab.Rendering/CoreCollapseRenderer.cs`
  - `Shaders/coreshock.frag`
- Modify:
  - `CloseUpScene.cs` (the `PreCollapse` and `FirstSecond` eras; neutrino brightness)
  - `CloseUpSceneController.cs` (the director camera over 1–10,000 km)
- Test: `tests/SupernovaLab.Tests/CoreCollapseHistoryTests.cs`

**Interfaces:**
- Consumes: `NeutrinoModel.Sample(o, obs, t, "lum_nue" | "lum_anue" | "lum_nux")` (total L = ν_e + ν̄_e + 4 ν_x);
  `Remnant.FormationTimeS`; `CameraDirector` and `DistanceRings` (Project 2).
- Produces:
```csharp
public enum CoreTrack { Exploding, Failed, Magnetorotational }
public enum CorePhase { PreCollapse, Bounce, Stalled, Revived, Collapsing, BlackHole, Cooling }
public sealed record CoreState(double SourceTimeS, CorePhase Phase, double ShockRadiusKm, double GainRadiusKm,
    double ProtoNsRadiusKm, double InfallSpeedKmS, double DistortionFraction, bool DistortionSchematicOnly);
public static class CoreCollapseHistory
{   public static CoreTrack? TrackFor(Outcome o);       // spec §3.1 items 1–6; null when no core collapse
    public static CoreState At(Outcome o, double sourceTimeS);
    public static double PreCollapseCoreRadiusKm(Outcome o);   // §C1
    public static double StageEndS(Outcome o);                 // when the close-up leaves the first-second era
    public const string FromSimulationsLabel = "From simulations";
    public const string MagnetorotationalNote = "A magnetar-driven explosion revives the shock differently (Speculative)"; }
```

- [ ] **Step 1: Write the failing tests**
```csharp
[Theory, MemberData(nameof(CollapseOutcomes))] public void Tracks_InsideResearchRanges(Outcome o)
{   foreach (var (t, lo, hi) in C1.ShockTable(CoreCollapseHistory.TrackFor(o)!.Value)) Assert.InRange(CoreCollapseHistory.At(o, t).ShockRadiusKm, lo, hi); }
[Fact] public void Failed_TrackEndsExactlyAtFormationTime()   // criterion 4
{   double tbh = FailedSn.Remnant.FormationTimeS!.Value;
    Assert.Equal(CorePhase.Collapsing, CoreCollapseHistory.At(FailedSn, tbh * 0.999).Phase);
    Assert.Equal(CorePhase.BlackHole, CoreCollapseHistory.At(FailedSn, tbh * 1.001).Phase);
    Assert.Equal(0, NeutrinoTotal(FailedSn, tbh * 1.001)); }
[Fact] public void Collapsar_UsesFailedTrack() => Assert.Equal(CoreTrack.Failed, CoreCollapseHistory.TrackFor(Preset("sn1998bw")));
[Fact] public void PulsationalPairInstability_UsesFailedTrack() { /* a PPISN lab outcome */ }
[Fact] public void Magnetorotational_UsesExplodingTrack_WithNote() { /* Strong B + Extreme rotation */ }
[Fact] public void Exploding_ShockStallsThenRevives_ProtoNsShrinksTo12km()
{   Assert.Equal(CorePhase.Stalled, CoreCollapseHistory.At(Default, 0.05).Phase);
    Assert.Equal(CorePhase.Revived, CoreCollapseHistory.At(Default, 0.8).Phase);
    Assert.InRange(CoreCollapseHistory.At(Default, 60).ProtoNsRadiusKm, 11.9, 12.1); }
[Fact] public void ProtoNsBrightness_FollowsNeutrinoTotal() { /* CloseUpState brightness == ExplosionScene.ExposureFor-rule(log L) at t */ }
[Fact] public void NoCollapse_NoTrack() => Assert.Null(CoreCollapseHistory.TrackFor(Preset("sn2011fe")));
```

- [ ] **Step 2: Run and see them fail.**

- [ ] **Step 3: Implement**
  - **The tracks:** from §C1/§C2, interpolated in log t.
  - **A failed track** is scaled in time so its collapse lands at `FormationTimeS` (spec §3.2).
  - **Drawing:**
    - the proto-neutron star as a sphere;
    - the shock as a translucent surface, with schematic low-order distortion at `DistortionFraction` (no stated
      amplitude when `DistortionSchematicOnly`);
    - the gain radius as a faint shell;
    - infalling particles at `InfallSpeedKmS` (schematic paths).
  - **Labels:** the `"From simulations"` label, and notes per spec §3.2.

- [ ] **Step 4: Run all tests, then one visual check.** Run:
  ```
  --snapshot out\p3t9 --preset sn1987a,n6946bh1 --closeup --window closeup --time 0.003,0.05,0.3,1
  ```

- [ ] **Step 5: Commit** — `git commit -m "Close-up: the first second at the core (spec §3)"`.

---

## Milestone 4: the black hole

### Task 10: Black-hole state, settling and spin; shadow, ring, grid, ergosphere

**Files:**
- Create:
  - `src/SupernovaLab.Core/Compact/BlackHoleModel.cs`
  - `src/SupernovaLab.Rendering/LensOverlayRenderer.cs` (shadow outline, ergosphere, horizon and photon-sphere labels)
- Modify:
  - `CloseUpScene.cs` (the black-hole eras, readouts, captions)
  - `lensing.frag` (the grid sphere, ruling P3-8)
  - `CloseUpSceneController.cs` (the camera starts side-on to +z)
- Test: `tests/SupernovaLab.Tests/BlackHoleModelTests.cs`

**Interfaces:**
- Consumes: `BlackHoleOptics` (Task 3), `KickDrift` (Task 5), `CoreCollapseHistory` (Task 9).
- Produces:
```csharp
public sealed record BlackHoleState(double MassMsun, double Spin, Quantity SpinQuantity, double SettledFraction,
    bool Forming, bool HasDisk, double DiskOuterGm, Vec3 OffsetCm);
public static class BlackHoleModel
{   public static double SettledSpin(Outcome o);             // ruling P3-13 until §C8
    public static double SettleStartS(Outcome o);            // t_BH; collapsar: t_BH; fallback: §C8
    public static double SettleEndS(Outcome o);              // direct: t_BH·10^(1/3) (P3-11); collapsar: t_BH + spin-up time (§C8)
    public static BlackHoleState? At(Outcome o, ObserverSetup obs, int runNumber, double sourceTimeS);
    public static string FromEarthText(double massMsun, double distanceCm);   // "From Earth this shadow would be about X nano-arcseconds across, about N times too small for the Event Horizon Telescope"
    public const string SettledCaption = "Settled: spinning black hole"; }
```

- [ ] **Step 1: Write the failing tests**
```csharp
[Fact] public void Spin_FromRotationInput()
{   Assert.Equal(C8.Slow, BlackHoleModel.SettledSpin(FailedSn with Slow)); Assert.Equal(C8.Extreme, BlackHoleModel.SettledSpin(Preset("sn1998bw")));
    Assert.Equal(Confidence.Speculative, BlackHoleModel.At(FailedSn, obs, 1, 1e3)!.SpinQuantity.Confidence); }
[Fact] public void DirectCollapse_FadesOverOneThirdDecade()
{   double tbh = FailedSn.Remnant.FormationTimeS!.Value;
    Assert.Equal(0, BlackHoleModel.At(FailedSn, obs, 1, tbh * 1.0001)!.SettledFraction, 2);
    Assert.Equal(1, BlackHoleModel.At(FailedSn, obs, 1, tbh * Math.Pow(10, 1.0 / 3) * 1.001)!.SettledFraction, 6); }
[Fact] public void Collapsar_SpinClimbsDuringSpinUp()
{   var a = BlackHoleModel.At(Grb, obs, 1, tbh + 1)!.Spin; var b = BlackHoleModel.At(Grb, obs, 1, tbh + 10)!.Spin;
    Assert.True(b > a); Assert.Equal(BlackHoleModel.SettledSpin(Grb), BlackHoleModel.At(Grb, obs, 1, BlackHoleModel.SettleEndS(Grb) * 1.01)!.Spin, 6); }
[Fact] public void FromEarthText_10Msun_10kpc()
    => Assert.Matches(@"about 0\.1 nano-arcseconds.*about 200,000 times", BlackHoleModel.FromEarthText(10, 10 * Kpc));
[Fact] public void SettledEra_CaptionAndReadouts()
{   var st = SceneFor("n6946bh1").At(1e6); Assert.Equal(CloseUpEra.BlackHole, st.Era); Assert.Contains(BlackHoleModel.SettledCaption, st.Notes);
    Assert.Equal(new[] { "Mass", "Horizon", "Shadow", "Spin", "Kick", "From Earth" }, st.Readouts.Select(r => r.Label));
    Assert.Contains("OGLE-2011-BLG-0462", st.Readouts[0].ToolTip); }
[Theory, InlineData(300), InlineData(2.6)] public void ExtremeMasses_Finite(double m) { /* every readout finite; spin 0.998 also */ }   // Review Focus 4
```

- [ ] **Step 2: Run and see them fail.**

- [ ] **Step 3: Implement**
  - **The lensing pass:** gets spin = `Spin × SettledFraction`. The fade blends the a = 0 image into the spinning
    one, so it is continuous.
  - **The overlays:**
    - the exact shadow outline (`KerrShadowOutline` at the camera's inclination);
    - the ergosphere (`ErgosphereRadius` over θ), drawn only with the grid on;
    - the horizon and photon-sphere labels.
  - **Formation** (`Forming`): the proto-neutron star's sphere shrinks behind r₊ within 1 ms of source time.
  - **Readouts:** per spec §6.1. The mass tooltip gives §C9's OGLE range.

- [ ] **Step 4: Run all tests, then one visual check:** n6946bh1 and sn1998bw at 1 s, 10 s, 1 yr and 100 yr, grid on and
  off.

- [ ] **Step 5: Commit** — `git commit -m "Close-up: the black hole, settling and spin (spec §6.1–§6.5)"`.

### Task 11: The collapsar disk, jets, and the fallback black hole

**Files:**
- Create:
  - `src/SupernovaLab.Rendering/DiskRenderer.cs`
  - `Shaders/disk.frag`
- Modify:
  - `CloseUpScene.cs` (`HasDisk` while accreting; the disk fades with the spin-up's end)
  - `lensing.frag` (rays hitting the equatorial disk annulus [ISCO, DiskOuterGm] return the disk's colour)
  - `BlackHoleModel.cs` (fallback settling from §C8; fallback kick through `KickDrift`)
- Test: `BlackHoleModelTests.cs` (additions)

**Interfaces:**
- Consumes: `BlackHoleOptics.IscoRadius`; §C8's `DiskOuterGm` and accretion duration.
- Produces: `BlackHoleState.HasDisk` and `DiskOuterGm` filled in; the disk's note
  `"Accretion disk: inner edge at the ISCO (exact); size from simulations; brightness schematic"`.

- [ ] **Step 1: Write the failing tests**
```csharp
[Fact] public void Collapsar_HasDiskDuringAccretion_NotAfter()
{   Assert.True(BlackHoleModel.At(Grb, obs, 1, tbh + 5)!.HasDisk); Assert.False(BlackHoleModel.At(Grb, obs, 1, BlackHoleModel.SettleEndS(Grb) * 2)!.HasDisk); }
[Fact] public void Disk_InnerEdgeAtIsco() { /* CloseUpState disk inner radius == IscoRadius(current spin) */ }
[Fact] public void FailedSn_NoDisk() => Assert.False(BlackHoleModel.At(FailedSn, obs, 1, 10)!.HasDisk);
[Fact] public void Fallback_DriftsWithMomentumScaledKick()
{   var o = FallbackOutcome; Assert.True(BlackHoleModel.At(o, obs, 1, 1e10)!.OffsetCm.Length() > 0);
    Assert.Equal(KickDrift.OffsetCm(o, obs, 1, 1e10), BlackHoleModel.At(o, obs, 1, 1e10)!.OffsetCm); }
[Fact] public void Jets_AlongSpinAxis_SameAsMainView() { /* close-up jet axis == +z == main view's jet axis */ }
```

- [ ] **Step 2: Run and see them fail.**

- [ ] **Step 3: Implement.** The thin disk is drawn in the lensing shader as an equatorial annulus. Apply the exact
  redshift factor g = 1/(u^t(1 − Ω ℓ)) for circular Kerr orbits, with brightness ∝ g⁴ × a schematic radial profile. Its
  colour is schematic. The jets reuse `JetRenderer` along +z.

- [ ] **Step 4: Run all tests.** Expected: pass.

- [ ] **Step 5: Commit** — `git commit -m "Close-up: the collapsar disk and jets; fallback settling (spec §6.2, §6.4)"`.

---

## Milestone 5: magnetars, tours, validation

### Task 12: The magnetar and its giant flares (with the gamma-ray panel spike)

**Files:**
- Create:
  - `src/SupernovaLab.Core/Compact/MagnetarFlares.cs`
  - `src/SupernovaLab.Core/Signals/MagnetarFlareModel.cs` (an `ISignalModel` for the gamma panel)
  - `src/SupernovaLab.App/Services/FlashLimiter.cs`
- Modify:
  - `CloseUpScene.cs` (magnetar title, readouts and field comparison; field decay per §C6; the flare's drawn
    brightness)
  - `Text/Comparisons.cs` (field vs a fridge magnet)
  - `GammaPanelViewModel.cs` (add the flare model to the GRB tab's models)
  - `ViewModels/EventMarkers.cs` ("Giant flare")
  - `Text/Glossary.cs`
- Test:
  - `tests/SupernovaLab.Tests/MagnetarFlaresTests.cs`
  - `FlashLimiterTests.cs`

**Interfaces:**
- Produces:
```csharp
public sealed record GiantFlare(double SourceTimeS, double EnergyErg, double PeakLuminosityErgS, double SpikeS);
public static class MagnetarFlares
{   public static double RatePerYear { get; }                          // §C6
    public static IReadOnlyList<GiantFlare> For(Outcome o, int runNumber);   // Poisson over the timeline, seeded; empty unless a magnetar
    public static double DrawnIntensity(GiantFlare f, double sourceTimeS);   // single rise ≥ 0.15 s playback-equivalent, then fade; capped at 1
    public const string Comparison2004 = "For comparison: SGR 1806−20's flare of 27 December 2004"; }
public sealed class FlashLimiter { public double Apply(double intensity, double wallTimeS); }   // ≤ 1 rise per 0.5 s of wall time
```

- [ ] **Step 1: Write the failing tests**
```csharp
[Fact] public void Flares_OnlyForMagnetars_SeededByRun()
{   Assert.Empty(MagnetarFlares.For(Default, 1)); Assert.Equal(MagnetarFlares.For(Mag, 5), MagnetarFlares.For(Mag, 5));
    Assert.NotEqual(MagnetarFlares.For(Mag, 5), MagnetarFlares.For(Mag, 6)); }
[Fact] public void Flare_MeanCountMatchesRate() { /* mean over runs 1..2000 within 5% of RatePerYear × 1000 yr */ }
[Fact] public void Flare_SingleRiseNoFlicker() { /* DrawnIntensity sampled finely: exactly one local maximum, rise ≥ 0.15 s equiv */ }
[Fact] public void FlashLimiter_RapidScrubbing_AtMostTwoRisesPerSecond()          // Review Focus 5
{   var lim = new FlashLimiter(); int rises = CountRises(ScrubBackAndForth(lim, hz: 10, seconds: 3)); Assert.True(rises <= 6); }
[Fact] public void GammaPanel_ShowsFlareSpikeAtFlareTime() { /* GammaPanel samples > 0 at the flare's observer time; detection logic runs */ }
[Fact] public void Flare_ObserverTimeStretchedByRedshift() { /* event marker at SourceTimeS·(1+z) */ }   // Review Focus 2
[Fact] public void Magnetar_PeriodReachesSeconds_ShownAtRealSpeed()
{   var st = SceneFor(MagOutcome).At(1000 * Year); Assert.InRange(Spin(st), 1, 20); Assert.Equal("Shown at real speed", st.SpeedLabel); }
[Fact] public void Magnetar_FieldComparisonFromModelValue() { /* Comparisons text uses the model's B, ≈ "a trillion" for 1e14–1e15 G per Comparisons' rule */ }
```

- [ ] **Step 2: Run and see them fail.**

- [ ] **Step 3: Implement** from §C6.
  - **`MagnetarFlareModel`:**
    - `Applies` only for a magnetar outcome;
    - `Confidence.Speculative`;
    - a spike of `SpikeS` at each flare's time, in the gamma panel's GRB-tab units;
    - detection through the panel's existing instruments (Fermi GBM, Swift BAT);
    - `NeverObserved` does not apply: giant flares have been observed.
  - **The drawn flare:** passes through `FlashLimiter` in both the close-up and the main view's marker glow.

- [ ] **Step 4: Run all tests.** Expected: pass.

- [ ] **Step 5: Commit** — `git commit -m "Magnetar: field, spin and giant flares; gamma-panel spike (spec §5, §7.3)"`.

### Task 13: Tour stops in the close-up

**Files:**
- Modify:
  - `src/SupernovaLab.App/Tour/TourBuilder.cs`
  - `TourStop.cs` (`bool CloseUp`)
  - `TourDirector.cs` and `TourViewModel.cs` (opens the window at the first close-up stop; skips close-up stops if it
    is closed)
  - `CloseUpWindow.xaml` (outline highlight, as Project 2's panel outline)
- Test: `tests/SupernovaLab.Tests/TourBuilderTests.cs`, `TourDirectorTests.cs` (additions)

**Interfaces:**
- Consumes: `CoreCollapseHistory`, `NeutronStarLayers.CrustFormationTimeS`, `BlackHoleModel.SettleEndS`,
  `PulsarModel.EmergenceTimeS`, `MagnetarFlares.For`, and `IWindowService.ShowCloseUp`.
- Produces: `TourStop.CloseUp` (a `bool`). The stops are bounce, stall, revival or black-hole formation, crust forms,
  black hole settles, pulses emerge, and giant flare.

- [ ] **Step 1: Write the failing tests**
```csharp
[Theory, MemberData(nameof(AllPresetsAndTypes))] public void EveryTour_HasCloseUpStopsWhereARemnantExists(Outcome o, Preset? p)
{   var stops = TourBuilder.For(o, obs, p); Assert.Equal(o.Remnant.Kind is RemnantKind.NeutronStar or RemnantKind.Magnetar or RemnantKind.BlackHole, stops.Any(s => s.CloseUp)); }
[Fact] public void FirstCloseUpStop_OpensWindow() { /* director reaches the stop → fake.CloseUpCalls == 1 */ }
[Fact] public void ClosingWindow_SkipsRemainingCloseUpStops_TourContinues() { /* next stop is the first non-close-up stop */ }
[Fact] public void CloseUpCaptionNumbers_EqualModelValues() { /* every number in a close-up caption equals its model's value (Project 2 criterion 4 style) */ }
[Fact] public void CloseUpStop_HighlightsWindowOutline() { /* TourViewModel.HighlightCloseUp true at a close-up stop */ }
```

- [ ] **Step 2: Run and see them fail.**

- [ ] **Step 3: Implement.** Captions use Project 2's templates and prefixes. The first-second captions use
  `"From simulations"` and the gravitational-wave captions use `"Expected:"`.

- [ ] **Step 4: Run all tests, then one visual check** with `--tour` on sn1987a and n6946bh1 at the close-up stops.

- [ ] **Step 5: Commit** — `git commit -m "Tour: close-up stops (spec §2.4)"`.

### Task 14: Validation, performance, glossary, README and the build record

**Files:**
- Create:
  - `tests/SupernovaLab.Tests/Project3ValidationTests.cs`
  - `FlashSafetyTests.cs`
  - `docs/superpowers/build-record/project3-calls-i-made.md`
- Modify:
  - `README.md` (Project 3 section; known misses)
  - `docs/perf-2026-11.md`
  - `Text/Glossary.cs` (every new term has a tooltip)
  - `docs/research/scientific-inconsistencies.md`
  - `private/SupernovaLab-issues.xlsx` (any open issue; git-excluded)
- Test: as listed.

- [ ] **Step 1: Write the validation tests**
```csharp
[Theory, MemberData(nameof(AllPresetsAndOutcomeTypes))] public void Criterion1_CloseUpPlaysWholeTimeline(Outcome o)
{   var s = new CloseUpScene(o, obs, 1); foreach (var t in LogTimes(1e-3, 3.156e10, 40)) { var st = s.At(t); AssertAllFinite(st); } }
[Fact] public void Criterion2_ExactResults() { /* re-asserts Task 3's shadow, Kerr, ISCO, ergosphere, visible fraction */ }
[Fact] public void Criterion3_ObservedChecks() { /* Crab P and R_L; Cas A disclosed comparison; EHT M87*, Sgr A*; SN 1987A neutrino energy/duration (existing assertions) */ }
[Fact] public void Criterion4_ViewsAgree() { /* kick position, beam phase/tilt/width, failed-SN cutoff == FormationTimeS */ }
[Fact] public void Criterion6_NoPeriodicBrightnessOver3Hz()
{   foreach (var (o, t) in AllPulsarCases) Assert.True(2 / Magnetosphere.DrawnPeriodS(PeriodAt(o, t)) <= 1.0); /* + flares single-rise + limiter */ }
[Fact] public void EveryReadout_HasConfidenceAndSource() { /* over all eras */ }
[Fact] public void EveryNewGlossaryKey_Exists() { /* neutron drip, nuclear pasta, light cylinder, ergosphere, photon ring, shadow, ISCO, SASI, gain region, proto-neutron star, giant flare, frame dragging */ }
```

- [ ] **Step 2: Run them; fix any failures in the task that owns the code** (record a ruling for each).

- [ ] **Step 3: Sweep and bench (one launch, plus one if a fix needs it).** Run:
  ```
  --sweep 40 --closeup
  --bench 30 --closeup --preset sn1054,n6946bh1,sn1998bw --size 1366x768,1920x1080
  ```
  Criterion 5 is ≥ 50 fps for both views at 1366×768. Write the results into `docs/perf-2026-11.md`.

- [ ] **Step 4: Write the build record**
  - `project3-calls-i-made.md`: the plan rulings and execution rulings in plain words, grouped as in
    `project2-calls-i-made.md`.
  - Update the README's known misses with #53 and #54 and any new entries.

- [ ] **Step 5: Run the full suite and record the count.** Run: `dotnet test tests/SupernovaLab.Tests`. Expected: all
  pass, 0 warnings.

- [ ] **Step 6: Commit** — `git commit -m "Project 3: validation, performance, glossary and build record"`.

---

## Spec coverage map

| Spec | Task |
|---|---|
| §1 ground rules, criteria 1–6 | Global Constraints; 3 (2), 5/6 (3, 4), 4/14 (5), 12/14 (6), 14 (all) |
| §2.1–2.3 window, opening, no-remnant messages | 4 |
| §2.4 tour | 13 |
| §2.5 inset fallback | 4 (decision), R-P3-1 if needed |
| §3 first second | 1 (§C1–C2), 9 |
| §4.1 surface, temperature, bending | 3, 5, 6 |
| §4.2 layers, neutron drip | 1 (§C5), 7 |
| §4.3 spin, field, beams, Earth | 5, 6 |
| §4.4 pulse trace, sound | 8 |
| §4.5 readouts / §4.6 kick | 5, 6 |
| §5 magnetar, flares | 1 (§C6), 12 |
| §6.1–6.5 black hole, spin, checks | 3, 10, 11 |
| §6.6 light-bending method | 3, 4 |
| §7.1 Expected, SN 1987A note | 1 (§C10–C11), 2 |
| §7.2 main view drift, beams | 5, 6 |
| §7.3 gamma-panel flare spike | 12 |
| §8.1 research | 1 |
| §10 flagged items | not built; listed in Task 14's README and calls doc |
