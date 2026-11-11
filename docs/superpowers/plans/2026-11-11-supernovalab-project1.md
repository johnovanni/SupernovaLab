# SupernovaLab Project 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A WPF app where you set up a star (or load a real supernova), press Explode, and watch the outcome on one
log-time timeline through six instrument panels and a simple 3D view, all driven by one physics core built from
published models.

**Architecture:**
- `SupernovaLab.Core` (no UI) holds three pure, stateless parts:
  - `OutcomeEngine`, which turns lab inputs into an `Outcome`;
  - one `ISignalModel` per signal, which turns an `Outcome` plus an `ObserverSetup` and a time into an observed
    value;
  - the presets.
- `SupernovaLab.Rendering` draws the 3D view with OpenTK.
- `SupernovaLab.App` is WPF/MVVM. It samples the models onto log-time grids and draws them with ScottPlot.

**Tech Stack:** .NET 8, WPF, CommunityToolkit.Mvvm 8.3.2, OpenTK 4.9.4, OpenTK.GLWpfControl 4.3.6,
ScottPlot.WPF 5.1.59, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-11-supernovalab-design.md`. The research files are **normative**: every
number, formula, threshold and citation comes from them:
- `docs/research/outcomes.md`
- `docs/research/signals.md`
- `docs/research/presets.md`

Each task names the sections it implements. Read those sections in full before coding.

## Global Constraints

- **Platform:** Windows 10 22H2+ / 11, 64-bit; **OpenGL 4.1 core minimum**; .NET 8 (`net8.0` for Core and Tests
  where possible, `net8.0-windows` for Rendering, App and any project that needs WPF types).
- **Units:** cgs internally, with constants from signals §0.2 (`PhysicalConstants`). Masses in M☉, energies in erg,
  times in seconds, distances in cm. Convert only at the UI boundary.
- **t = 0** is core bounce (core collapse) or ignition (Type Ia). Models take **source time**. The timeline shows
  **observer time** = source time × (1 + z).
- **Timeline range:** 10⁻³ s to 3.156×10¹⁰ s (1,000 yr), log scale. Panels sample **400 log-spaced points**.
- **Every number shown** is a `Quantity` with a `Confidence` (Established / ModelDependent / Speculative) and a
  `Source` (citation and research-file section). Nothing is shown without one.
- **Determinism:** `OutcomeEngine` and every `ISignalModel` are pure. Randomness exists only in `EventSampler`
  (displayed events), seeded by the run number.
- **No invented numbers.** A value missing from the research files is an approximation labeled `[APPROX]` in code
  comments and `Confidence.Speculative` in data, with the reasoning in a comment.
- **Formula tests** assert against a worked example or published value quoted in the research files, or a value
  computed by hand in the test's comment. They never recompute the expected value with the code under test.
- **Code copied from Universe3D** (`C:\repos\Universe3D`) is copied with its namespace changed, not referenced:
  `Cosmology`, `OrbitCamera`, `GlHost`, `ShaderProgram`, `Directory.Build.props` (build folder renamed
  `SupernovaLab.build`), and the screenshot helper scripts.
- **Every `ISignalModel`** is a public sealed class with a public parameterless constructor, so the contract and
  sweep tests can find and construct all of them by reflection.
- **Data files** are embedded resources under `src/SupernovaLab.Core/Data/Embedded/`. No network at run time.
- **Glossary:** every astronomy term in the UI has a plain-language tooltip from `Glossary`.
- **Commit messages** end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Commit locally only;
  never push.
- **Tests:** `dotnet test tests/SupernovaLab.Tests` passes with 0 warnings at every task's end.

## Review Focus

1. **Lab inputs at their extremes** (0.8 and 300 M☉, Z = 0.001 and 2, every stripping, rotation and field
   combination): a reasonable person expects an explained outcome, never an exception or NaN. Pinned by the
   sweep test in Task 5, and the model × observer sweep in Task 13.
2. **Times at or before t = 0 and beyond a model's valid range:** expect "not yet" or "not modeled", never a
   thrown exception or a negative or NaN plot value. Pinned in Task 6 (`SignalContract` tests run for every
   model).
3. **Very far or very near observers** (10 pc, 1 Gpc, A_V = 30): expect a fractional event count to show
   "Too faint", not "Detected (0.3 events)", and no overflow in magnitudes. Pinned in Task 6 (neutrinos) and
   Task 13 (sweep).
4. **Changing an input while the timeline plays:** the current time stays, the panels redraw, nothing crashes.
   Pinned in Task 14 (view-model test).
5. **Editing an input after loading a preset:** the pins clear and the card stops showing "Published value".
   Pinned in Task 13.

---

### Task 1: Solution scaffold, units, observer

**Files:**
- Create: `SupernovaLab.sln`, `Directory.Build.props`, and four projects:
  - `src/SupernovaLab.Core/SupernovaLab.Core.csproj` (net8.0)
  - `src/SupernovaLab.Rendering/SupernovaLab.Rendering.csproj` (net8.0-windows, OpenTK 4.9.4, GLWpfControl 4.3.6;
    empty for now)
  - `src/SupernovaLab.App/SupernovaLab.App.csproj` (WinExe, net8.0-windows, WPF, CommunityToolkit.Mvvm 8.3.2,
    ScottPlot.WPF 5.1.59; an empty MainWindow titled "SupernovaLab")
  - `tests/SupernovaLab.Tests/SupernovaLab.Tests.csproj` (xUnit; net8.0-windows, UseWPF true)
- Create in Core:
  - `Units/PhysicalConstants.cs`, `Units/Confidence.cs`, `Units/Source.cs`, `Units/Quantity.cs`,
    `Units/UnitFormat.cs`
  - `Observer/Cosmology.cs` (copied from Universe3D `src/Universe3D.Core/Cosmology`)
  - `Observer/ObserverSetup.cs`, `Observer/Extinction.cs`
- Test: `tests/SupernovaLab.Tests/UnitsTests.cs`, `ObserverTests.cs`

**Interfaces (Produces):**
```csharp
namespace SupernovaLab.Core.Units;
public enum Confidence { Established, ModelDependent, Speculative }
public sealed record Source(string Citation, string ResearchRef);   // ("Sukhbold et al. 2016, ApJ 821, 38", "outcomes §1.2")
public sealed record Quantity(double Value, string Unit, Confidence Confidence, Source Source,
                              double? Low = null, double? High = null, bool Published = false);
public static class PhysicalConstants { /* C, G, MSun, RSun, Pc, Kpc, Mpc, Day, Year, MeV, LSun, MbolZeroPointLum = 3.0128e35 */ }
public static class UnitFormat {
  public static string Mass(double msun); public static string Energy(double erg);   // "1.2 × 10⁵¹ erg"
  public static string Time(double seconds);    // "3 ms", "2.5 h", "40 days", "320 years"
  public static string Distance(double cm);     // "51 kpc (166,000 light-years)"
}
namespace SupernovaLab.Core.Observer;
public sealed record ObserverSetup(double DistancePc, double Av, bool BeamTowardEarth) {
  public static ObserverSetup Default { get; }     // 10,000 pc, Av 0, true
  public double Redshift { get; }                  // 0 below 50 Mpc; above, z at comoving distance (Cosmology)
  public double LuminosityDistanceCm { get; }      // D·(1+z)
  public double TimeDilation { get; }              // 1+z
  public double DistanceModulus { get; }           // 5 log10(D_L / 10 pc)
  public double LightTravelYears { get; }
}
public static class Extinction {
  public static double A(Band band, double av);    // ratios from signals §4.7
  public static double NhFromAv(double av);         // 2.2e21 × Av cm⁻²
}
public enum Band { U, B, V, R, I, Uv, SoftXray }
```

- [ ] **Step 1: Write failing tests**
  - `UnitsTests`: `UnitFormat.Time(0.003) == "3 ms"`, `Time(86400*40) == "40 days"`,
    `Time(3.156e7*320) == "320 years"`; `PhysicalConstants.MSun == 1.989e33`.
  - `ObserverTests`:
    - `Default.DistanceModulus` ≈ 15.0 (10 kpc);
    - at 1 Gpc, Redshift is about 0.25 (compare to Universe3D's z = 0.1 → 418.5 Mpc comoving within 0.5%, using
      that table);
    - `TimeDilation == 1` below 50 Mpc;
    - `Extinction.A(Band.V, 1) == 1`.
- [ ] **Step 2:** Run `dotnet test`. Expected: FAIL (types missing).
- [ ] **Step 3:** Implement. `Directory.Build.props` is copied from Universe3D, with the folder renamed
  `SupernovaLab.build`.
- [ ] **Step 4:** Run `dotnet test`. Expected: PASS, 0 warnings. `dotnet build` builds all four projects.
- [ ] **Step 5:** Commit: `feat(core): scaffold solution, units and observer setup`.

### Task 2: Explodability table (per-mass outcomes at solar metallicity)

Implements spec §3 "Per-mass explodability table" and outcomes §1.2–§1.4.

**Files:**
- Create:
  - `tools/explodability/README.md` (exact provenance and steps)
  - `src/SupernovaLab.Core/Data/Embedded/explodability-s16.csv`
  - `src/SupernovaLab.Core/Outcomes/ExplodabilityTable.cs`
- Test: `tests/SupernovaLab.Tests/ExplodabilityTableTests.cs`

**Interfaces (Produces):**
```csharp
namespace SupernovaLab.Core.Outcomes;
public enum CollapseResult { NeutronStar, FallbackBlackHole, DirectBlackHole }
public sealed record ExplodabilityRow(double InitialMass, bool Explodes, CollapseResult Result,
    double RemnantBaryonicMass, double ExplosionEnergyErg, double NickelMass, double? Compactness25,
    double PreSnMass, double HeCoreMass, double CoCoreMass, double HydrogenEnvelopeMass, double RadiusCm);
public sealed class ExplodabilityTable {
  public static ExplodabilityTable LoadEmbedded();
  public ExplodabilityRow Lookup(double initialMass);   // nearest row; clamps to the table range
  public string SourceDescription { get; }              // from the CSV header line
  public IReadOnlyList<ExplodabilityRow> Rows { get; }
}
```

- **CSV format:**
  - The first line is `# source: <citation and which data>`.
  - The header row is: `InitialMass,Explodes,Result,RemnantBaryonicMass,ExplosionEnergyErg,NickelMass,Compactness25,PreSnMass,HeCoreMass,CoCoreMass,HydrogenEnvelopeMass,RadiusCm`.
  - Masses are 9.0–120 M☉.
- **Data acquisition (Step 1):**
  1. Try the Sukhbold et al. 2016 per-model results at the MPA Garching archive (doi:10.17617/1.b; look under
     `wwwmpa.mpa-garching.mpg.de/ccsnarchive/`). Use the N20 engine for M > 12 and Z9.6 for M ≤ 12, per
     outcomes §8.2 step 5.
  2. If the per-model table cannot be downloaded and parsed within reasonable effort, build the CSV at 0.1 M☉
     steps from the band rules and structure grid in outcomes §1.2–§1.4, interpolating §1.3 for structure. Write
     `# source: band rules from outcomes.md §1.2–1.4 (S16 per-model table not obtained)`.
  3. Record which path was taken, with exact URLs or sections, in `tools/explodability/README.md` and in the
     report.

- [ ] **Step 1:** Obtain or build the CSV, as above.
- [ ] **Step 2: Write failing tests**
  - `Lookup(23.0).Explodes == false` and `.Result == DirectBlackHole`;
  - `Lookup(12.0).Explodes` and `Lookup(26.0).Explodes` are true;
  - every row with `Explodes == true` has 0.1e51 ≤ E ≤ 2.5e51 erg and NS baryonic mass 1.2–2.0;
  - `Lookup(5)` clamps to the first row;
  - `SourceDescription` is non-empty.
- [ ] **Step 3:** Run. Expected: FAIL.
- [ ] **Step 4:** Implement `ExplodabilityTable` (embedded resource, invariant-culture parsing).
- [ ] **Step 5:** Run. Expected: PASS.
- [ ] **Step 6:** Commit: `feat(core): per-mass explodability table`.

### Task 3: Lab inputs and the presupernova star

Implements outcomes §8.2 steps 0–3 (has it died, low-mass branch, presupernova structure, pair-instability
regime) and spec §2.1.

**Files:**
- Create:
  - `src/SupernovaLab.Core/Progenitors/LabInputs.cs`
  - `Progenitors/PresupernovaStar.cs`
  - `Progenitors/ProgenitorModel.cs`
- Test: `tests/SupernovaLab.Tests/ProgenitorModelTests.cs`

**Interfaces (Produces):**
```csharp
namespace SupernovaLab.Core.Progenitors;
public enum StarKind { MassiveStar, WhiteDwarf }
public enum Stripping { None, Partial, Full, HydrogenAndHelium }
public enum Rotation { Slow, Fast, Extreme }
public enum CoreField { Normal, Strong }
public enum TypeIaChannel { NearChandrasekhar, SubChandrasekhar, Iax }
public enum NickelMixing { WellMixed, Weak }
public enum NickelYield { Calibrated, Theory }
public sealed record LabInputs(StarKind Kind, double InitialMass, double Metallicity, Stripping Stripping,
    Rotation Rotation, CoreField Field, TypeIaChannel IaChannel, double WhiteDwarfMass,
    NickelMixing Mixing, NickelYield Yield, bool DenseCsm, double Opacity) {
  public static LabInputs Default { get; }   // MassiveStar, 15, 1.0, None, Slow, Normal, NearChandrasekhar, 1.38, WellMixed, Calibrated, false, 0.07
  public LabInputs Clamped();                // InitialMass 0.8–300, Metallicity 0.001–2, WhiteDwarfMass 0.85–1.38 (1.38 if NearChandrasekhar), Opacity 0.05–0.10
}
public sealed record PresupernovaStar(Quantity FinalMass, Quantity HeCoreMass, Quantity CoCoreMass,
    Quantity HydrogenEnvelopeMass, Quantity HeliumEnvelopeMass, Quantity RadiusCm, Quantity SurfaceTemperatureK,
    Quantity MassLossRateMsunPerYr, Quantity WindSpeedKmS, bool ChemicallyHomogeneous);
public enum EarlyFate { NotDeadYet, WhiteDwarf, ElectronCapture, CoreCollapse, PulsationalPairInstability, PairInstability, DirectCollapseAbovePisn }
public static class ProgenitorModel {
  public static (EarlyFate Fate, PresupernovaStar Star, IReadOnlyList<string> Notes) Evolve(LabInputs inputs, ExplodabilityTable table);
}
```
- Surface temperature and wind (Ṁ, v_w) follow outcomes §4.3 / signals §0.4. Where the research gives only
  typical values (an RSG wind of 10⁻⁶ M☉/yr at 10 km/s; a WR wind of 10⁻⁵ at 1000 km/s), use those by envelope
  state, labeled ModelDependent.
- `ChemicallyHomogeneous` follows the CHE option in step 7a (extreme rotation, Z ≤ 0.3 Z☉, 20–60 M☉).

- [ ] **Step 1: Write failing tests** (`Evolve(LabInputs.Default with { ... }, table)`):
  - mass 0.85 gives NotDeadYet;
  - mass 3 gives WhiteDwarf, with FinalMass from the Cummings 2018 IFMR (outcomes §8.2 step 1) within 0.05 M☉ of
    the value computed by hand in the test comment;
  - mass 9.1 at Z = 1 gives ElectronCapture;
  - mass 15, no stripping: CoreCollapse, H envelope ≥ 2 M☉, radius > 300 R☉;
  - mass 15, Full stripping: H envelope = 0, radius < 50 R☉;
  - mass 200 at Z = 0.001 gives PairInstability;
  - mass 300 at Z = 0.001 gives DirectCollapseAbovePisn or PairInstability, per the He-core rule in step 3 (assert
    the rule's own boundary: He core > 133 gives DirectCollapseAbovePisn);
  - every Quantity has a non-empty Source.Citation.
- [ ] **Step 2:** Run. Expected: FAIL.
- [ ] **Step 3:** Implement per outcomes §8.2 steps 0–3 and §2.2. Mark the §2.2 metallicity scheme as Speculative.
- [ ] **Step 4:** Run. Expected: PASS.
- [ ] **Step 5:** Commit: `feat(core): lab inputs and presupernova star`.

### Task 4: Outcome engine, core-collapse path

Implements outcomes §8.2 steps 4, 5, 6 and 8 (non-rotating, normal field) and spec §3.

**Files:**
- Create:
  - `src/SupernovaLab.Core/Outcomes/Outcome.cs`, `Remnant.cs`, `SupernovaType.cs`
  - `Outcomes/SpectralTyper.cs`, `Outcomes/RemnantModel.cs`, `Outcomes/OutcomeEngine.cs`
- Test: `tests/SupernovaLab.Tests/OutcomeEngineTests.cs`

**Interfaces (Produces):**
```csharp
namespace SupernovaLab.Core.Outcomes;
public enum Fate { NotDeadYet, WhiteDwarf, Supernova, FailedSupernova, PairInstabilitySupernova, PulsationalPairInstability }
public enum SupernovaType { None, IIP, IIL, IIb, IIn, Ib, Ic, IcBL, Ibn, Icn, SlsnI, SlsnII, Ia, Ia91T, Ia91bg, Iax, Pisn }
public enum RemnantKind { None, WhiteDwarf, NeutronStar, Magnetar, BlackHole }
public sealed record Remnant(RemnantKind Kind, Quantity? Mass, Quantity? RadiusKm, Quantity? SpinPeriodS,
    Quantity? MagneticFieldG, Quantity? KickKmS, Quantity? FormationTimeS);  // FormationTimeS = t_BH for black holes
public sealed record PresetPins(double? ExplosionEnergyErg, double? EjectaMass, double? NickelMass, double? RadiusCm, Source Source);
public sealed record Outcome(LabInputs Inputs, Fate Fate, SupernovaType Type, bool HasGrb, Remnant Remnant,
    PresupernovaStar? Star, Quantity? ExplosionEnergyErg, Quantity? EjectaMass, Quantity? NickelMass,
    Quantity? PeakLuminosityErgS, Quantity? MagnetarEnergyErg, Quantity? MagnetarTimescaleS,
    IReadOnlyList<string> Notes, bool UsesPublishedValues);
public sealed class OutcomeEngine {
  public OutcomeEngine(ExplodabilityTable table);
  public Outcome Compute(LabInputs inputs, PresetPins? pins = null);
}
```
- Pins replace E, M_ej, M_Ni and R. Those Quantities get `Published = true` and the pin's Source, and
  `UsesPublishedValues` is set. Pins are implemented here and exercised in Task 13.
- **t_BH** (`Remnant.FormationTimeS`):
  - From `Compactness25` with t_ff = 0.241 ξ^-1.5 s (signals §1.3) when the row has it; otherwise 1.0 s
    `[APPROX]`, Speculative.
  - For fallback black holes, 3.0 s `[APPROX]`, inside O'Connor & Ott's 0.23–3.4 s range, Speculative.
- **Notes** are plain sentences, for example "A star of 23 M☉ collapses straight to a black hole in this model:
  its core is too compact for neutrinos to restart the shock."

- [ ] **Step 1: Write failing tests:**
  - 23 M☉ at Z = 1, no stripping: FailedSupernova, BlackHole, Type None.
  - 12 M☉: Supernova, IIP, NeutronStar of 1.2–1.6 M☉ gravitational, E between 0.1e51 and 1.0e51.
  - 26 M☉: Supernova.
  - 15 M☉ with Partial stripping: IIb. Full: Ib. HydrogenAndHelium: Ic.
  - 15 M☉, Full, with `Mixing = Weak`: Ib or Ic per the Dessart threshold. Assert the threshold rule from
    outcomes §8.2 step 6.
  - `DenseCsm = true` with an H envelope: IIn. Stripped: Ibn.
  - 9.1 M☉: Supernova, IIP, NickelMass 0.003, E = 1e50 (electron-capture values from step 1).
  - 3 M☉: WhiteDwarf, RemnantKind.WhiteDwarf, Type None.
  - `NickelYield.Theory` gives more Ni than `Calibrated` for 15 M☉.
  - A neutron star above 2.2 M☉ gravitational becomes a BlackHole (step 8); unit-test `RemnantModel` directly with
    such a mass.
  - Every non-null Quantity has a Source.
- [ ] **Step 2:** Run. Expected: FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run. Expected: PASS.
- [ ] **Step 5:** Commit: `feat(core): outcome engine for core collapse`.

### Task 5: Outcome engine, rotation, magnetic field, Type Ia, outcome map

Implements outcomes §8.2 steps 7 and 9 and §3 (pair instability through the engine), and spec §3 "outcome map".

**Files:**
- Modify: `src/SupernovaLab.Core/Outcomes/OutcomeEngine.cs`
- Create:
  - `Outcomes/TypeIaModel.cs`
  - `Outcomes/MagnetarModel.cs` (KB10 E_p, t_p, per outcomes §5.3)
  - `Outcomes/OutcomeMap.cs`
- Test: `tests/SupernovaLab.Tests/OutcomeEngineExtremeTests.cs`, `OutcomeMapTests.cs`, `OutcomeSweepTests.cs`

**Interfaces (Produces):**
```csharp
public sealed record MapCell(double InitialMass, Fate Fate, RemnantKind Remnant, SupernovaType Type);
public static class OutcomeMap {
  public static IReadOnlyList<MapCell> Strip(OutcomeEngine engine, LabInputs template, int samples = 400); // log-spaced 0.8–300 M☉
}
```

- [ ] **Step 1: Write failing tests:**
  - Extreme rotation, Z = 0.1, Full stripping, 40 M☉: IcBL, HasGrb, BlackHole.
  - Strong field + Fast rotation + Full stripping, 20 M☉: SlsnI, Magnetar with P₀ 1–5 ms and B 10¹⁴–10¹⁵ G;
    MagnetarEnergyErg ≥ 1e51.
  - Strong field + Slow rotation, 15 M☉: IIP with a Magnetar remnant, and a note that spin-down energy is too small
    to change the light curve.
  - Strong field at 23 M☉ (black hole): a note that the field has no effect.
  - Type Ia:
    - NearChandrasekhar: Ia, NickelMass 0.6, EjectaMass 1.38, RemnantKind.None.
    - PeakLuminosityErgS = 2.0e43 × M_Ni within 1% (outcomes §8.2 step 9).
    - SubChandrasekhar with WD mass 0.97: NickelMass ≈ 0.3.
    - Iax: RemnantKind.WhiteDwarf.
  - 200 M☉ at Z = 0.001: PairInstabilitySupernova, Pisn, RemnantKind.None.
  - `OutcomeMap.Strip(Default)`: has a BlackHole cell between 22 and 25 M☉, Supernova cells on both sides of it,
    and WhiteDwarf cells below 8.
  - `OutcomeSweepTests` (Review Focus 1):
    - for every combination of mass ∈ {0.8, 1, 5, 8.5, 9.1, 12, 18, 23, 26, 40, 80, 150, 300},
      Z ∈ {0.001, 0.1, 1, 2}, each Stripping, each Rotation, each CoreField, and DenseCsm ∈ {false, true};
    - plus each Ia channel at WD mass ∈ {0.85, 1.0, 1.38};
    - `Compute` returns without throwing, every Quantity value is finite and ≥ 0, every Quantity has a Source, and
      Notes is non-empty whenever Fate is FailedSupernova or the field or rotation was ignored.
- [ ] **Step 2:** Run. Expected: FAIL.
- [ ] **Step 3:** Implement. The step 7 branches are Speculative or ModelDependent exactly as outcomes §8.3 labels
  them.
- [ ] **Step 4:** Run. Expected: PASS.
- [ ] **Step 5:** Commit: `feat(core): rotation, magnetar, Type Ia and outcome map`.

### Task 6: Signal infrastructure and neutrinos

Implements spec §5 and §7 (`ISignalModel`), §2.4 (events), §4 (time axis), and signals §1 (all of it).

**Files:**
- Create:
  - `src/SupernovaLab.Core/Signals/ISignalModel.cs`, `Detection.cs`, `Detector.cs`, `Detectors.cs`,
    `EventSampler.cs`, `NeutrinoModel.cs`
  - `src/SupernovaLab.Core/Timeline/TimeAxis.cs`
- Test: `tests/SupernovaLab.Tests/SignalContractTests.cs`, `NeutrinoModelTests.cs`, `EventSamplerTests.cs`,
  `TimeAxisTests.cs`

**Interfaces (Produces):**
```csharp
namespace SupernovaLab.Core.Signals;
public enum Messenger { GravitationalWaves, Neutrinos, GammaRays, XRaysUv, VisibleLight, Radio }
public enum DetectionStatus { Detected, TooFaint, BlockedByDust, NotPointedAtUs, NotProduced, NotYet, NotModeled }
public sealed record Detection(DetectionStatus Status, string Reason);
public sealed record Detector(string Id, string Name, Messenger Messenger, double Threshold, string ThresholdUnit, Source Source);
public interface ISignalModel {
  string Id { get; }                          // "neutrino"
  Messenger Messenger { get; }
  IReadOnlyList<SignalChannel> Channels { get; }
  Confidence Confidence { get; }
  IReadOnlyList<Source> Sources { get; }
  bool Applies(Outcome outcome);
  (double StartS, double EndS) ValidRange(Outcome outcome);   // source time
  double Sample(Outcome outcome, ObserverSetup observer, double sourceTimeS, string channelId); // NaN outside ValidRange or when !Applies
  Detection Detect(Outcome outcome, ObserverSetup observer, double sourceTimeS, Detector detector);
}
public sealed record SignalChannel(string Id, string Label, string Unit, bool LogScale);
public static class Detectors { public static IReadOnlyList<Detector> All { get; } public static Detector Get(string id); }
public static class EventSampler {
  public static IReadOnlyList<double> SampleTimes(Func<double, double> ratePerSecond, double t0, double t1, int seed); // inhomogeneous Poisson by thinning
}
namespace SupernovaLab.Core.Timeline;
public static class TimeAxis {
  public const double MinS = 1e-3, MaxS = 3.156e10; public const int Samples = 400;
  public static double[] LogGrid();                        // 400 log-spaced source-independent observer times
  public static double ToSource(double observerS, ObserverSetup obs);  // ÷ (1+z)
  public static double ToFraction(double observerS); public static double FromFraction(double f);  // 0..1 on the log axis
}
```
- **Neutrino channels:**
  - `rate` (events/s in the selected detector) and `cumulative` (events);
  - `lum_nue`, `lum_anue`, `lum_nux` (erg/s, at the source);
  - `meanE_anue` (MeV).
- **Detector-dependent channels:** `Sample` uses a `detectorId` suffix, `rate@superk` and `cumulative@superk`, for
  each neutrino detector in `Detectors`: superk, hyperk, icecube, juno, dune.
- **Models:** the Pagliaroli two-component fit plus the neutronization Gaussian (signals §1.2), scaled by binding
  energy; the black hole cutoff at t_BH (§1.3); counts from §1.4.
- **Neutrino detection** is `Detected` when the expected cumulative count is ≥ 2 within 20 s for Super-K-like
  detectors (§1.4), otherwise `TooFaint`. IceCube uses its significance rule (§11).

- [ ] **Step 1: Write failing tests**
  - `SignalContractTests` (a theory over every ISignalModel in the assembly, found by reflection, so later tasks'
    models are covered automatically), with outcomes for 12 M☉, 23 M☉ (failed), Ia and 3 M☉ (white dwarf), and
    observers Default, 10 pc, 1 Gpc with A_V = 30:
    - `Sample` at t = −1, 0 and `MaxS`·2 returns NaN or a finite value, never throws, never returns a negative
      finite value for luminosities, counts or fluxes;
    - `Detect` never throws;
    - `!Applies` implies `NotProduced`.
  - `NeutrinoModelTests`:
    - the neutronization burst energy (∫L_burst dt) = 2.2e51 erg within 5% (signals §1.2d);
    - at t = 0.5 s, `meanE_anue` lies within 11–16 MeV;
    - for the 23 M☉ failed supernova, all luminosities are 0 at t_BH + 0.01 s;
    - Super-K cumulative at 10 kpc for a 12 M☉ explosion lies within 2,900–9,800 (signals §1.4);
    - at 770 kpc (M31), Super-K gives `TooFaint` or `Detected` consistently with the 2-event rule; at 2 Mpc,
      `TooFaint` (Review Focus 3).
  - `EventSamplerTests`: the same seed gives the same times; a constant rate of 100/s over 10 s gives 1000 ± 100
    events.
  - `TimeAxisTests`: `FromFraction(ToFraction(t)) == t` within 1e-12 relative; `LogGrid()[0] == MinS`,
    `LogGrid()[399] == MaxS`.
- [ ] **Step 2:** Run. Expected: FAIL.
- [ ] **Step 3:** Implement. `Detectors.All` lists every instrument named in spec §5's table with thresholds from
  signals §11 (§1.4, §2.3, §3, §4.7, §5.4, §6, §8, §9). Later tasks add none; they use these Ids. Id list:
  - `superk`, `hyperk`, `icecube`, `juno`, `dune`;
  - `aligo`, `et`, `ce`;
  - `gbm`, `bat`, `spi`, `cosi`, `nustar`;
  - `swift-xrt`, `einstein-probe`, `ultrasat`, `chandra`, `xmm`;
  - `eye`, `ztf`, `rubin`, `spectrograph`;
  - `vla`, `meerkat`, `fast`, `ska`.
- [ ] **Step 4:** Run. Expected: PASS.
- [ ] **Step 5:** Commit: `feat(core): signal infrastructure and neutrino model`.

### Task 7: Gravitational waves

Implements signals §2 and spec §5 (Gravitational waves panel).

**Files:** Create `src/SupernovaLab.Core/Signals/GravitationalWaveModel.cs`. Test: `GravitationalWaveModelTests.cs`.

**Interfaces (Produces):** `GravitationalWaveModel : ISignalModel`, with:
- channels `strain` (dimensionless h, at the observer) and `snr@aligo|et|ce`;
- a method used by the panel:
  `double[,] Spectrogram(Outcome o, ObserverSetup obs, int timeBins, int freqBins, double maxTimeS = 2.0, double maxFreqHz = 2048)`,
  returning power in arbitrary units, normalized to a maximum of 1.

The waveform:
- an energy-normalized stochastic chirp (signals §2.4), with a fixed internal seed so it's deterministic;
- the rotating-bounce component when `Inputs.Rotation` is Fast or Extreme (§2.2), labeled Speculative;
- strain cut off at t_BH for black holes.

- [ ] **Step 1: Write failing tests:**
  - peak |h| for a 12 M☉ slow-rotating collapse at 10 kpc lies within 1e-23–1e-21 (signals §2.1);
  - h scales as 1/D: at 20 kpc it's half;
  - aLIGO detects a non-rotating collapse at 5 kpc and not at 50 kpc; Extreme rotation is detected at 50 kpc
    (signals §11);
  - spectrogram power at f > 1500 Hz in the first 50 ms is below that at 100–1000 Hz;
  - the Type Ia model returns `NotProduced`.
- [ ] **Step 2:** Run. FAIL. **Step 3:** Implement. **Step 4:** Run. PASS.
- [ ] **Step 5:** Commit: `feat(core): gravitational-wave model`.

### Task 8: Shock breakout and shock cooling

Implements signals §3.

**Files:** Create `Signals/ShockBreakoutModel.cs`. Test: `ShockBreakoutModelTests.cs`.

**Interfaces (Produces):** `ShockBreakoutModel : ISignalModel` (Messenger.XRaysUv), with:
- channels `lum` (erg/s), `temp` (K) and `flux@swift-xrt|einstein-probe|ultrasat`;
- `double BreakoutTimeS(Outcome o)` (the delay after bounce, §3.1), used for the timeline marker.

- [ ] **Step 1: Write failing tests:**
  - the breakout delay for a red supergiant progenitor (15 M☉, no stripping) lies between 0.3 and 2 days, and for a
    Full-stripped star between 10 s and 1 hour (§3.1);
  - the SN 2008D-like case (Ib, R ≈ 1e11 cm, E = 6e51, M_ej = 7) gives a peak X-ray luminosity within a factor of
    3 of 6e43 erg/s;
  - at 1 Gpc it's TooFaint for Swift XRT;
  - with A_V = 30, UV detection is BlockedByDust;
  - Type Ia gives NotProduced.
- [ ] **Steps 2–4:** FAIL, implement, PASS.
- [ ] **Step 5:** Commit: `feat(core): shock breakout and cooling`.

### Task 9: Optical light curves

Implements signals §4 (4.1–4.7) and spec §5 (Visible light, light curve).

**Files:**
- Create `Signals/LightCurveModel.cs`, `Signals/BolometricCorrection.cs`, `Text/Comparisons.cs`
- Test: `LightCurveModelTests.cs`, `ComparisonsTests.cs`

**Interfaces (Produces):** `LightCurveModel : ISignalModel` (Messenger.VisibleLight), with:
- channels `lbol` (erg/s), `absV` (mag), `appV` (mag; includes A_V), and `detect@eye|ztf|rubin`;
- `double PeakTimeS(Outcome o)` and `double? PlateauEndS(Outcome o)`, used for markers;
- `string Comparisons.ForApparentMagnitude(double m)`: "brighter than the full Moon" (m < −12.7), "visible in
  daylight" (m < −4), "as bright as Venus" (−4.9 to −3.5), "as bright as the brightest stars" (−1.5 to 1.5),
  "visible to the naked eye" (≤ 6), "needs binoculars" (6–9), "needs a telescope" (> 9).

The light curve combines:
- shock cooling (from Task 8's cooling phase, re-used through `ShockBreakoutModel`);
- Arnett or plateau, by type;
- the Ni/Co tail with gamma-ray trapping;
- magnetar heating when `MagnetarEnergyErg` is set.

- [ ] **Step 1: Write failing tests:**
  - Ni/Co heating per M☉ of Ni at 10 days = 3.40e43 and at 100 days = 5.91e42 erg/s within 2%. Computed by hand
    from Nadyozhin 1994 (6.45e43 e^(−t/8.8 d) + 1.45e43 e^(−t/111.3 d)); check that signals §4.1 quotes the same
    coefficients, and adjust the comment if it quotes refined ones;
  - Arnett: an Ia with 0.6 M☉ Ni peaks at 1.2e43 erg/s within 15% and rises in 15–21 days (§4.2);
  - the II-P plateau for 15 M☉ lasts 80–140 days, with L50 within the range of signals §4.3;
  - SN 1987A-like pinned values (Ni 0.07, BSG radius from presets.md, distance 51.4 kpc, A_V from presets.md) peak
    at apparent V within 0.5 mag of +2.9;
  - a magnetar SLSN-I (from Task 5's test case) peaks at absolute V brighter than −20;
  - `Comparisons.ForApparentMagnitude(-4.5) == "as bright as Venus"`.
- [ ] **Steps 2–4:** FAIL, implement, PASS.
- [ ] **Step 5:** Commit: `feat(core): optical light curves`.

### Task 10: Gamma-ray lines and gamma-ray bursts

Implements signals §5 and §9.

**Files:** Create `Signals/GammaLineModel.cs`, `Signals/GrbModel.cs`. Test: `GammaLineModelTests.cs`,
`GrbModelTests.cs`.

**Interfaces (Produces):**
- `GammaLineModel` has channels `line158`, `line812`, `line847`, `line1238`, `ti44_68`, `ti44_78` and `ti44_1157`
  (photons cm⁻² s⁻¹), and `detect@spi|cosi|nustar`.
- `GrbModel` has channels `prompt` (erg cm⁻² s⁻¹), `afterglowX` (erg cm⁻² s⁻¹), `afterglowOpt` (mag) and
  `afterglowRadio` (mJy), plus `detect@gbm|bat`. It uses `NotPointedAtUs` when `BeamTowardEarth` is false.
- `double GrbModel.T90S(Outcome o)`.

- [ ] **Step 1: Write failing tests:**
  - a SN 2014J-like Ia (Ni 0.6, distance 3.5 Mpc, pinned): the 847 keV flux at day 50–100 is within a factor of 2
    of the INTEGRAL value in presets.md / signals §5.3;
  - ⁴⁴Ti 1157 keV for a Cas A-like ⁴⁴Ti mass of 1.3e-4 M☉ at 3.4 kpc after 340 yr is about 1.7e-5 ph cm⁻² s⁻¹,
    within 30% (signals §11);
  - the GRB for the IcBL + HasGrb outcome: T90 > 2 s; prompt `Detected` by GBM at 38 Mpc when beamed, and
    `NotPointedAtUs` when not;
  - a non-GRB outcome gives `NotProduced`.
- [ ] **Steps 2–4:** FAIL, implement, PASS.
- [ ] **Step 5:** Commit: `feat(core): gamma-ray lines and gamma-ray bursts`.

### Task 11: Circumstellar interaction, remnant, pulsar and neutron-star cooling

Implements signals §6, §7 and §8.

**Files:**
- Create `Signals/CsmInteractionModel.cs` (radio + X-ray), `Signals/RemnantEvolutionModel.cs`,
  `Signals/PulsarModel.cs`, `Signals/NeutronStarCoolingModel.cs`
- Tests: one test file each

**Interfaces (Produces):**
- `CsmInteractionModel`:
  - channels `radio1_4`, `radio5`, `radio22` (mJy at the observer), `nuPeakGHz`, `lx` (erg/s), `fluxX`
    (erg cm⁻² s⁻¹ after N_H absorption);
  - detection for `vla`, `meerkat`, `ska`, `chandra`, `xmm`, `swift-xrt`.
- `RemnantEvolutionModel`: channels `radiusPc`, `angularSizeArcsec`, `radio1GHzJy`. Plus
  `double ResolvableTimeS(Outcome, ObserverSetup)` (when the angle reaches 0.5″).
- `PulsarModel`:
  - channels `periodS`, `edot`, `fluxMjy`;
  - `double EmergenceTimeS(Outcome)` (when the ejecta become transparent, §8.3);
  - `IReadOnlyList<double> PulseTimes(Outcome o, double windowStartS, double windowLengthS)`, for drawing pulses;
  - Applies only for NeutronStar or Magnetar;
  - `NotPointedAtUs` when `BeamTowardEarth` is false.
- `NeutronStarCoolingModel`: channels `tempK` and `lum`, with detection for `chandra` and `xmm`.

- [ ] **Step 1: Write failing tests:**
  - a Crab-like pulsar (P = 33 ms, B from presets.md) has Ė ≈ 4.5e38 erg/s within 10%, and its characteristic
    age P/(2Ṗ) is within 50% of 970 yr;
  - the Sedov radius for E = 1e51, n = 1 cm⁻³ at 1,000 yr matches the Truelove–McKee value given in signals §7.2
    within 10%;
  - the Chevalier peak frequency for an RSG wind (A* = 10) at 30 days is within the range implied by signals §6.1
    (hand-computed in the comment);
  - the radio luminosity of a stripped-envelope SN exceeds that of a II-P at equal distance and time (signals §6
    typical luminosities);
  - the NS surface temperature at 300 yr is within 1.5–3 MK (signals §8.4);
  - pulsar and cooling models give NotProduced for a black hole.
- [ ] **Steps 2–4:** FAIL, implement, PASS.
- [ ] **Step 5:** Commit: `feat(core): interaction, remnant, pulsar and cooling models`.

### Task 12: Schematic spectrum

Implements signals §10 and spec §5 (Visible light spectrum).

**Files:** Create `Signals/SpectrumModel.cs`. Test: `SpectrumModelTests.cs`.

**Interfaces (Produces):**
```csharp
public sealed record SpectralLine(string Label, double RestWavelengthAngstrom, double Strength);  // "Hα", 6563
public sealed record Spectrum(double[] WavelengthsAngstrom, double[] RelativeFlux, IReadOnlyList<SpectralLine> Lines,
                              double PhotosphereVelocityKmS, double ColorTemperatureK, bool Nebular);
public sealed class SpectrumModel {
  public Spectrum At(Outcome o, double sourceTimeS);   // 3,000–10,000 Å, 700 points; null-equivalent (empty arrays) before 1 day or after 1 yr
}
```
- Blackbody at the color temperature, plus P-Cygni-shaped lines from the §10.1 line list, blueshifted by the §10.2
  photospheric velocity.
- Not an ISignalModel; the panel calls it directly.

- [ ] **Step 1: Write failing tests:**
  - II-P at 20 days contains Hα, and its absorption minimum is blueshifted by 6,000–10,000 km/s (§10.2);
  - Ia at 0 days relative to peak contains Si II 6355;
  - Ic contains neither H nor He I 5876;
  - Ib contains He I 5876 but not Hα;
  - IcBL line widths exceed 15,000 km/s;
  - before 1 day, the arrays are empty.
- [ ] **Steps 2–4:** FAIL, implement, PASS.
- [ ] **Step 5:** Commit: `feat(core): schematic spectrum`.

### Task 13: Presets and validation

Implements spec §2.3 and §8 (presets, validation), and Review Focus 3 and 5.

**Files:**
- Create:
  - `src/SupernovaLab.Core/Presets/Preset.cs`, `Presets/PresetCatalog.cs`
  - `Data/Embedded/presets.json`
  - `docs/research/presets-verification.md`
- Test: `tests/SupernovaLab.Tests/PresetValidationTests.cs`, `ModelObserverSweepTests.cs`

**Interfaces (Produces):**
```csharp
namespace SupernovaLab.Core.Presets;
public sealed record ObservedFact(Messenger Messenger, string ChannelId, double? SourceTimeS, double Value, string Unit, string Description, Source Source);
public sealed record Preset(string Id, string Name, string Summary, string WhatWasSeen, LabInputs Inputs,
    ObserverSetup Observer, PresetPins? Pins, IReadOnlyList<ObservedFact> Facts);
public static class PresetCatalog { public static IReadOnlyList<Preset> LoadEmbedded(); public static Preset Get(string id); }
```
- **Preset Ids**, in this order: `sn1987a`, `sn1054`, `casa`, `sn2011fe`, `sn2014j`, `sn1993j`, `sn1998bw`,
  `sn2008d`, `sn2023ixf`, `sn2015bn`, `tycho`, `kepler`, `n6946bh1`, `betelgeuse`.
- **One author's consistent parameter set per preset**, as presets.md advises. Name it in the Source.

- [ ] **Step 1: Verify values.** Every [R] value that a validation test asserts must be checked against the cited
  paper (web: arXiv / ADS abstracts and full text):
  - SN 1987A: peak V; the Kamiokande-II count; Ni mass.
  - SN 2011fe: peak magnitude, distance.
  - Crab: P, Ṗ.
  - SN 2008D: L_X.
  - Betelgeuse: distance.
  Record each value, the citation, and verified / corrected / unverifiable in `presets-verification.md`.
  Unverifiable values are not asserted.
- [ ] **Step 2: Write failing tests:**
  - every preset loads; Ids are unique and in the listed order;
  - `OutcomeEngine.Compute(p.Inputs, p.Pins)` gives the preset's expected Type (sn1987a → IIP [peculiar, noted],
    casa → IIb, sn2011fe → Ia, sn1993j → IIb, sn1998bw → IcBL + HasGrb, sn2008d → Ib, sn2015bn → SlsnI,
    n6946bh1 → FailedSupernova, betelgeuse → IIP);
  - the spec §8 validations, with tolerances exactly as the spec states (SN 1987A K-II 6–20 and peak V +2.9 ± 0.5;
    SN 2014J 847 keV within ×2; SN 2011fe peak within 0.3 mag; Crab P = 33 ms and age within 50%; SN 2008D L_X
    within ×3; Betelgeuse Super-K 1e6–1e8 and peak V < −8);
  - Review Focus 5: an outcome from `Compute(p.Inputs with { InitialMass = p.Inputs.InitialMass + 1 }, null)` has
    `UsesPublishedValues == false`;
  - `ModelObserverSweepTests` (Review Focus 3): for each preset × observers {10 pc, 1 Gpc, A_V = 30} × every
    ISignalModel × 50 log-spaced times, Sample is NaN or finite and non-negative where physical, and every
    `Detect` returns a status. Magnitudes are finite, in −30…+60.
- [ ] **Step 3:** Run. FAIL. **Step 4:** Implement `presets.json` and `PresetCatalog`. **Step 5:** Run. PASS.
- [ ] **Step 6:** Commit: `feat(core): real-supernova presets and validation`.

### Task 14: App shell: lab panel, outcome summary and details, outcome map, timeline, windows

Implements spec §2 (all inputs), §3 (summary, details window and map), §4 (time), §5.0 (window layout, except the
Instruments window's contents and the strip, which are Task 15) and §9 (input errors). Review Focus 4.

**Files:**
- Create in `src/SupernovaLab.App/`:
  - `ViewModels/MainViewModel.cs`, `LabViewModel.cs`, `OutcomeViewModel.cs`, `TimelineViewModel.cs`
  - `Views/LabPanel.xaml`, `Views/OutcomeSummary.xaml`, `Views/OutcomeMapStrip.xaml`, `Views/TimelineBar.xaml`
  - `Windows/OutcomeDetailsWindow.xaml`, `Windows/PresetInfoWindow.xaml`, `Windows/InstrumentsWindow.xaml` (an
    empty shell; Task 15 fills it)
  - `Services/IWindowService.cs`, `Services/WindowService.cs`
  - `Resources/Theme.xaml` (dark, copied from Universe3D's palette)
- Create `src/SupernovaLab.Core/Text/Glossary.cs` and `Text/OutcomeTexts.cs`
- Modify `MainWindow.xaml`, `App.xaml.cs` (composition: construct the view models and `WindowService`, show
  MainWindow)
- Test: `tests/SupernovaLab.Tests/MainViewModelTests.cs`, `GlossaryTests.cs`, `FakeWindowService.cs`. The test
  project references the App project.

**Main window layout** (minimum 1366×768):
- **Left column (300 px):** the lab panel. The preset dropdown sits at the top with an ⓘ button
  (`ShowPresetInfo`), then Star, Observer and the Advanced expander (spec §2), then the Explode and Re-roll buttons.
  The run number is shown as "Run #n".
- **Right column (300 px):** the outcome summary (spec §3): type, fate and remnant in large text, a one-sentence
  summary, and 3–4 key numbers with confidence badges colored Established green, Model-dependent amber,
  Speculative red, with the source as a tooltip. A **Details…** button calls `ShowOutcomeDetails`. The outcome map
  strip sits beneath it.
- **Center:** the 3D view (a placeholder in this task).
- **Above the timeline:** a 64 px row reserved for the instrument strip (a placeholder in this task; Task 15).
- **Bottom:** the timeline bar: Play/Pause, a log scrubber with event markers, a speed slider (decades per second,
  0.1–3, default 1/3), the observer-time readout via `UnitFormat.Time`, and the "light left … ago" line.
- **A View menu** with Instruments, Outcome details and Preset info.

**Other windows** (spec §5.0): non-modal, owned by MainWindow, at most one instance each. Asking again activates
the existing one. They bind to the same view-model instances as MainWindow (`DataContext` = the shared
`MainViewModel`, or a child of it).
- `OutcomeDetailsWindow`: every non-null Quantity of the current outcome, with its value (UnitFormat), range,
  confidence badge and full citation plus ResearchRef, and all the Notes. It updates live when the outcome changes.
- `PresetInfoWindow`: the selected preset's Name, Summary and WhatWasSeen, and its Facts with citations. It shows
  "No preset selected" otherwise.

**Interfaces (Produces):**
```csharp
public partial class MainViewModel : ObservableObject {
  public LabViewModel Lab { get; } public OutcomeViewModel Outcome { get; } public TimelineViewModel Timeline { get; }
  public Outcome CurrentOutcome { get; }           // recomputed on any input change (Review Focus 4: time is kept)
  public ObserverSetup CurrentObserver { get; }
  public int RunNumber { get; }                    // seed for EventSampler; Re-roll increments it
  public event EventHandler? OutcomeChanged;       // panels resample on this
}
public partial class TimelineViewModel : ObservableObject {
  public double ObserverTimeS { get; set; } public bool IsPlaying { get; set; } public double DecadesPerSecond { get; set; }
  public IReadOnlyList<EventMarker> Markers { get; }
  public void Tick(TimeSpan elapsed);              // advances on the log axis; stops at MaxS
}
public sealed record EventMarker(string Label, double ObserverTimeS, string GlossaryKey);
public interface IWindowService {
  void ShowInstruments(Messenger? focus = null);   // focus: maximize that panel (Task 15 handles focus)
  void ShowOutcomeDetails();
  void ShowPresetInfo();
}
// MainViewModel exposes ShowInstrumentsCommand (parameter Messenger?), ShowOutcomeDetailsCommand and
// ShowPresetInfoCommand, which call the injected IWindowService. View models never construct windows.
```
- Markers come from the models' marker methods: `BreakoutTimeS`, `PeakTimeS`, `PlateauEndS`, `T90S`,
  `EmergenceTimeS`, `ResolvableTimeS`, `Remnant.FormationTimeS`, plus fixed neutrino and GW markers at 0.01 s.
  "Gamma-ray lines peak" and "Radio peak" are the times of the maximum of `line847` and `radio5` on the 400-point
  grid. A marker is omitted when its model doesn't apply.
- Above 50 Mpc, the observer section shows: "Beyond 50 Mpc the shift of light between bands (K-correction) is not
  modeled."
- `Glossary` holds keys and texts for every term in the UI. `GlossaryTests` asserts that every key referenced from
  XAML (`{x:Static}` or binding keys) exists. It collects keys by scanning the App's XAML files for
  `Glossary.`-prefixed keys.

- [ ] **Step 1: Write failing tests:**
  - changing `Lab.InitialMass` while `IsPlaying` keeps `ObserverTimeS` and raises `OutcomeChanged` once;
  - selecting the preset `sn1987a` sets the inputs, and `CurrentOutcome.UsesPublishedValues` is true; changing
    metallicity then makes it false and sets a status message;
  - `Tick(1 s)` at 1/3 decade per second multiplies the time by 10^(1/3);
  - `ShowOutcomeDetailsCommand`, `ShowPresetInfoCommand` and `ShowInstrumentsCommand(Messenger.Neutrinos)` each
    call the matching `FakeWindowService` method once, with the right argument;
  - `OutcomeViewModel.DetailRows` lists every non-null Quantity of the current outcome (count it for the default
    outcome) and changes when the outcome changes;
  - Glossary completeness.
- [ ] **Step 2:** Run. FAIL. **Step 3:** Implement. **Step 4:** Run. PASS.
- [ ] **Step 5:** Run the app (`dotnet run --project src/SupernovaLab.App`) and take screenshots with the copied
  helper, `tools/shot.ps1 -ProcessName SupernovaLab.App`:
  - the main window at 1366×768 and 1920×1080, checking that nothing is clipped or cramped;
  - the Outcome details and Preset info windows open beside it.
- [ ] **Step 6:** Commit: `feat(app): lab panel, outcome summary and details, outcome map, timeline, windows`.

### Task 15: The six instrument panels and the instrument strip

Implements spec §5.1 in full, and the instrument strip and Instruments window of §5.0.

**Files:**
- Modify: `Windows/InstrumentsWindow.xaml` (the 3×2 grid of panels; maximize-on-double-click within the window;
  Esc restores; the `focus` argument maximizes that panel) and `MainWindow.xaml` (the strip replaces its
  placeholder)
- Create `Views/InstrumentStrip.xaml` and `ViewModels/InstrumentTileViewModel.cs`. There are six tiles; each shows
  the panel's title, a sparkline of its main series, the time cursor and `CurrentDetection` as a badge. Clicking a
  tile runs `ShowInstrumentsCommand` with its Messenger.
- Create in `src/SupernovaLab.App/`:
  - `ViewModels/InstrumentPanelViewModel.cs` (base class), plus one subclass per panel:
    `GravitationalWavePanelViewModel`, `NeutrinoPanelViewModel`, `GammaPanelViewModel`, `XrayUvPanelViewModel`,
    `VisiblePanelViewModel`, `RadioPanelViewModel`
  - `Views/InstrumentPanel.xaml` (shared chrome: title, sub-tab strip, instrument picker, detection badge,
    confidence badge, ScottPlot `WpfPlot`)
- Test: `tests/SupernovaLab.Tests/InstrumentPanelTests.cs`

**Interfaces (Produces):**
```csharp
public abstract partial class InstrumentPanelViewModel : ObservableObject {
  public abstract Messenger Messenger { get; }
  public IReadOnlyList<string> SubTabs { get; } public string SelectedSubTab { get; set; }
  public IReadOnlyList<Detector> Instruments { get; } public Detector SelectedInstrument { get; set; }
  public Detection CurrentDetection { get; }        // at the timeline's current time
  public Confidence Confidence { get; }
  public IReadOnlyList<PlotSeries> Series { get; }  // resampled on OutcomeChanged / observer / instrument / sub-tab change
  public IReadOnlyList<ObservedFact> Facts { get; } // preset observations drawn as markers
  public void OnTimeChanged(double observerTimeS);  // moves the cursor and updates CurrentDetection only
}
public sealed record PlotSeries(string Label, double[] ObserverTimesS, double[] Values, string Unit, bool LogY);
```
- **Sub-tabs, channels and instruments per panel:** exactly spec §5's table.
  - Neutrino events are drawn as dots from `EventSampler.SampleTimes` (seed = RunNumber), using the `rate@<detector>`
    channel.
  - The GW spectrogram uses ScottPlot's heatmap.
  - The spectrum sub-tab plots `SpectrumModel.At(...)` against wavelength (not time), and labels its lines. The
    title says "Schematic spectrum".
  - The pulsar sub-tab shows 1 s of pulses from `PulseTimes` at the current time.
- **Interaction:** in the Instruments window, double-clicking a panel maximizes it; Esc restores the grid. The
  panel view models are created once by MainViewModel (`IReadOnlyList<InstrumentPanelViewModel> Panels`) and shared
  by the strip tiles and the Instruments window, so closing and reopening the window loses nothing.
- **Main series per tile** (its sparkline):
  - Gravitational waves: strain;
  - Neutrinos: `rate@superk`;
  - Gamma rays: `line847`, or the GRB prompt flux when HasGrb;
  - X-rays & UV: breakout `lum`;
  - Visible light: `appV`, inverted axis;
  - Radio: `radio5`.
- **Values** outside a model's valid range are not plotted. The panel shows the "not modeled here" reason.
- **Performance:** sampling happens only on changes. The cursor moves at playback rate without resampling.
- **Model failure:** an exception or non-finite value from a model during sampling is caught in the base class,
  logged with `Debug.WriteLine`, and shown as "model error" in that panel only (spec §9). Test it with a throwing
  fake model.
- The Visible light panel shows `Comparisons.ForApparentMagnitude` for the peak and the current magnitude.

- [ ] **Step 1: Write failing tests:**
  - for each panel with the default outcome, `Series` are non-empty and contain no NaN (NaNs are removed by
    splitting into segments);
  - with preset `sn1987a`, the neutrino panel's Facts include the Kamiokande-II event count;
  - moving time does not change `Series` (reference equality);
  - changing distance does;
  - each `InstrumentTileViewModel` exposes the main series named above, and its badge equals its panel's
    `CurrentDetection`;
  - `MainViewModel.Panels` is the same instance list before and after `ShowInstruments` is called twice on the
    fake window service.
- [ ] **Step 2:** Run. FAIL. **Step 3:** Implement. **Step 4:** Run. PASS.
- [ ] **Step 5:** Take screenshots of the Instruments window (the grid, and one maximized panel) and of the main
  window's strip, for sn1987a, sn1998bw and betelgeuse; check they read clearly. Measure the redraw rate while
  playing, with the Instruments window open (≥ 30 fps on this machine).
- [ ] **Step 6:** Commit: `feat(app): instrument panels, Instruments window and instrument strip`.

### Task 16: Simple 3D explosion view

Implements spec §6.

**Files:**
- Create in `src/SupernovaLab.Rendering/`:
  - `GlHost.cs`, `ShaderProgram.cs`, `OrbitCamera.cs` (copied from Universe3D)
  - `ExplosionScene.cs`, `StarRenderer.cs`, `EjectaRenderer.cs`, `JetRenderer.cs`, `RemnantRenderer.cs`
  - `BlackbodyColor.cs`
  - `Shaders/*.vert|frag`
- Create `src/SupernovaLab.Core/Signals/EjectaKinematics.cs`
- Modify: `src/SupernovaLab.App/MainWindow.xaml` (replace the placeholder) and `Controllers/SceneController.cs`
- Test: `tests/SupernovaLab.Tests/EjectaKinematicsTests.cs`, `BlackbodyColorTests.cs`

**Interfaces (Produces):**
```csharp
namespace SupernovaLab.Core.Signals;
public static class EjectaKinematics {
  public static double OuterVelocityCmS(Outcome o);                   // from v_ej = sqrt(10E/(3M_ej)) (signals §0.4) and the outer-layer factor stated there, if any
  public static double RadiusCm(Outcome o, double sourceTimeS);       // R* before breakout; free expansion; Sedov per RemnantEvolutionModel later
  public static double PhotosphereTemperatureK(Outcome o, double sourceTimeS);  // from LightCurveModel lbol and radius (Stefan–Boltzmann), floor 3,000 K
}
namespace SupernovaLab.Rendering;
public static class BlackbodyColor { public static (float R, float G, float B) FromTemperature(double kelvin); }  // normalized sRGB
```
- About 50,000 ejecta points, in a deterministic random spherical shell distribution, with a radius from
  `EjectaKinematics.RadiusCm`.
- Jets are two cones when `HasGrb`.
- The remnant:
  - a pulsar is a point with two sweeping beam cones (period slowed for visibility, labeled "slowed down");
  - a black hole is a black disk with a thin bright ring;
  - a white dwarf is a small white point.
- Auto-framing keeps the ejecta at about 60% of the view height. The scale bar uses km, AU, ly or pc (whichever
  gives a 1–999 value).
- The overlay shows the observer time and the current physical radius (`UnitFormat.Distance`).
- When OpenGL fails, a message names the requirement. The rest of the app keeps working (spec §9).

- [ ] **Step 1: Write failing tests:**
  - v_ej for E = 1e51, M_ej = 3 M☉ equals 7.5e8 cm/s within 1% (signals §0.4 worked example);
  - the radius before breakout equals the star's radius;
  - the radius at 1 yr equals v·t within 1%;
  - BlackbodyColor at 5,800 K is near white (each channel > 0.85), at 3,000 K is red-dominant, and at 30,000 K is
    blue-dominant.
- [ ] **Step 2:** Run. FAIL. **Step 3:** Implement. **Step 4:** Run. PASS.
- [ ] **Step 5:** Screenshots of the star before the explosion, at 1 day and at 100 yr for sn1987a, and the jets for
  sn1998bw. Check ≥ 50 fps with all panels visible.
- [ ] **Step 6:** Commit: `feat(rendering): simple 3D explosion view`.

### Task 17: Polish, README and final checks

**Files:**
- Create `README.md` with these sections: What it is; System requirements (spec §1 table); How to use (the lab,
  presets, timeline, panels); How accurate is it (the confidence labels, the research files, the known
  limitations from signals §12 and outcomes §8.3); Building.
- Create `docs/perf-2026-11.md`.
- Modify: any glossary gaps found.

- [ ] **Step 1:** Run the full suite: `dotnet test` passes, 0 warnings.
- [ ] **Step 2:** Manual run-through with screenshots:
  - each preset loads and plays to 1,000 yr without errors;
  - the outcome map visibly shows the islands;
  - the tooltips appear.
- [ ] **Step 3:** Record measured fps (3D view, panels while playing) in `docs/perf-2026-11.md`, noting that the
  minimum class was not measured if no such hardware is available.
- [ ] **Step 4:** Commit: `docs: README and performance notes`.
