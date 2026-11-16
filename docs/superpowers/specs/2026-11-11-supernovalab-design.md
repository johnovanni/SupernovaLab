# SupernovaLab — Design (Project 1: physics core, lab, instrument views, simple 3D view)

**Date:** 2026-11-11
**Status:** Written under the owner's standing instruction ("use your best judgement, make reversible calls
yourself, check in later"). Decisions made without the owner are listed in §12 for review.
**Siblings:** SolarSystem3D and Universe3D (`C:\repos\Universe3D`). SupernovaLab follows their patterns: MVVM,
OpenTK + GLWpfControl, a dark theme, plain-language glossary tooltips, and accuracy tests against reference data.

## 1. Purpose

A Windows desktop app (C#, WPF) for setting off supernovas and watching them through every kind of
instrument at once. You choose a star, or load a real supernova, and press **Explode**. The app works out what
happens (which kind of supernova, what is left behind) from published models. It then plays the event on one
shared timeline, from the first millisecond to a thousand years, as each instrument would record it:
gravitational waves, neutrinos, gamma rays, X-rays and UV, visible light, and radio.

### What the owner asked for, in priority order
1. **A "what if" lab:** set a star's mass, metal content, binary stripping, rotation and magnetic field, then see
   the outcome. The full range of options, including the extreme ones (hypernovas, gamma-ray bursts, magnetars).
2. **A visual showpiece:** the explosion in 3D. Project 1 has a simple 3D view; Project 2 makes it the showpiece.
3. **An educational explorer:** plain-language explanations, like Universe3D.
4. **Accuracy matters.** Outcomes and signals follow published models and observations. Where the science is
   uncertain, the app says so rather than hiding it.

### The three projects (agreed 2026-11-11)
- **Project 1 (this spec):** the physics core, the lab, the instrument views, and a simple 3D explosion view.
  A complete, usable app on its own.
- **Project 2:** the full 3D showpiece of the explosion.
- **Project 3:** the remnant close-up (neutron star, pulsar beams, magnetar, black hole bending light).
The physics core is the shared source for every view, so the views can never contradict each other.

### Approach to the physics
No hydrodynamics and no radiative transfer run in the app. A real supernova simulation takes a supercomputer
months. The app uses **analytic and fitted models from the published literature**, the same ones astronomers use
to interpret observations. The numbers, formulas and citations are collected in three research files, which are
**normative** for this spec:
- `docs/research/outcomes.md`: from a star's properties to its fate, supernova type and remnant (its §8 is the
  decision procedure).
- `docs/research/signals.md`: what each instrument records over time (its §11 is the signal set).
- `docs/research/presets.md`: real supernovas, with confidence tags [V] verified, [R] recalled, [D] derived.

Where this spec says "per outcomes §8.2 step 4", the implementation follows that section exactly, including its
labeled approximations.

### System requirements
The same as Universe3D:

| | Minimum | Recommended |
|---|---|---|
| OS | Windows 10 (22H2) or 11, 64-bit | Windows 11, 64-bit |
| Runtime | .NET 8 Desktop Runtime | same |
| GPU | OpenGL 4.1 (Intel UHD 620 class or better) | Intel Iris Xe / Radeon 680M or any dedicated GPU of the last ~8 years |
| CPU / RAM | 2 cores / 8 GB | 4 cores / 16 GB |
| Display | 1366×768 | 1920×1080 or larger |
| Disk | 150 MB | same |
| Network | not used | same |

### Success criteria
1. Every combination of lab inputs produces an outcome, with no exception thrown. Impossible or ignored inputs
   are explained in the outcome card (for example "Strong magnetic field has no effect: the core collapsed to a
   black hole").
2. The outcome map reproduces the published "islands" (outcomes §1.2). At solar metallicity, with no stripping:
   a 23 M☉ star forms a black hole, while 12 and 26 M☉ stars explode, so exploding masses lie on both sides of a
   black-hole island.
3. Each real-supernova preset reproduces its key observations within the tolerances of §8 (for example SN 1987A:
   about a dozen neutrinos in Kamiokande-II; peak apparent magnitude about +2.9).
4. Every number on screen carries a confidence label (Established, Model-dependent or Speculative) and its
   source on hover.
5. On a recommended system: the 3D view runs at ≥ 50 fps, and all instrument panels redraw at ≥ 30 fps while the
   timeline plays.

## 2. The lab (inputs)

### 2.1 Star
- **Kind:** *Massive star* (core collapse) or *White dwarf in a binary* (Type Ia).
- **Massive star:**
  - **Initial mass**: logarithmic slider from 0.8 to 300 M☉. Below about 8 M☉ the outcome is "no supernova:
    becomes a white dwarf" (outcomes §8.2 step 1). Below 0.9 M☉: "hasn't died yet; the universe isn't old enough".
  - **Metal content** (Z/Z☉): logarithmic slider from 0.001 to 2, with ticks at 0.01, 0.1, 0.3, 1 (the Sun) and 2.
  - **Companion stripping**: None / Partial (a little hydrogen left) / Full (hydrogen gone) / Hydrogen and helium
    gone.
  - **Rotation**: Slow / Fast / Extreme.
  - **Core magnetic field**: Normal / Strong (magnetar-forming).
- **White dwarf:**
  - **Channel**: Near-Chandrasekhar / Sub-Chandrasekhar / Type Iax.
  - **White dwarf mass**: 0.85–1.38 M☉. Fixed at 1.38 for Near-Chandrasekhar; the slider is disabled.
- **Advanced (uncertain physics)**, an expander labeled as such:
  - **Nickel mixing**: Well mixed (default) / Weak. This decides the Ib vs Ic helium threshold (outcomes §8.2
    step 6).
  - **Nickel yield**: Calibrated to observations (default) / Theory (outcomes §8.2 step 5).
  - **Dense gas around the star** (a pre-supernova eruption): off by default. On gives a Type IIn (or Ibn/Icn)
    and strong radio and X-ray emission.
  - **Ejecta opacity** κ: 0.05–0.10 cm²/g, default 0.07 (signals §4.2). It applies to core-collapse ejecta; Type Ia/Iax use a fixed effective κ = 0.19 [APPROX], calibrated to the observed 17–19 d rise (Hayden et al. 2010), and the slider is disabled for them (ruling R31).

### 2.2 Observer
- **Distance**: logarithmic slider from 10 pc to 1 Gpc, with landmark ticks: Betelgeuse (0.2 kpc), Galactic
  Center (8.2 kpc), Large Magellanic Cloud (50 kpc), Andromeda (0.78 Mpc), M51 (8.6 Mpc), Virgo Cluster
  (16.5 Mpc).
  - Above 50 Mpc the redshift is computed from distance with Universe3D's cosmology (flat ΛCDM, H₀ = 70,
    Ωm = 0.3). Observed times are stretched by (1 + z), and fluxes use the luminosity distance. The shift of
    light between bands (K-correction) is **not** modeled, and the app says so above 50 Mpc.
- **Dust** (A_V, magnitudes of visual extinction): slider 0–30, with presets: None (0), Looking out of the
  Galaxy (0.1), Across the Galactic disk (2), Toward the Galactic Center (30). Dust dims visible and UV light and
  absorbs soft X-rays. It does not affect gravitational waves, neutrinos, gamma rays or radio. Soft-X-ray
  absorption uses N_H = 2.2×10²¹ cm⁻² × A_V (signals §12, item 8; verify).
- **Jet or beam toward Earth**: a checkbox, on by default. It decides whether a gamma-ray burst or a pulsar beam
  is seen. The tooltip gives the real odds (signals §9.2, §8.3).

### 2.3 Presets
A dropdown loads a real supernova: SN 1987A, SN 1054 (Crab), Cassiopeia A, SN 2011fe, SN 2014J, SN 1993J,
SN 1998bw / GRB 980425, SN 2008D, SN 2023ixf, SN 2015bn, Tycho (SN 1572), Kepler (SN 1604), N6946-BH1 (a candidate
failed supernova), and Betelgeuse ("if it exploded today").
- A preset sets the lab inputs to its best match. It also **pins** the published explosion values (E, M_ej,
  M_Ni, R, distance, A_V) from presets.md, using one author's consistent set per object.
- Pinned values show a "Published value" badge with the citation. Changing any star or explosion input clears the
  pins and returns to the model, with a status message. Changing only the observer (distance, dust, beam) keeps the
  explosion pins, so you can see the same event from elsewhere ("SN 1987A seen from Andromeda"); the status message
  says the observer is no longer the real one (ruling R49).
- Each preset has a short "What was actually seen" note. Each instrument panel marks the real observation
  (for example the 24 neutrino events of SN 1987A) where one exists.

### 2.4 Explode
- **Explode** computes the outcome and starts the timeline at t = 0. Changing an input recomputes the outcome
  immediately, keeping the current time.
  - **Changed 2026-11-16 (Project 3, Task 15; ruling R-P3-41, at the owner's request):** a change that makes a new
    outcome (a star or Advanced input, or choosing a preset) now pauses playback and moves the timeline to t = 0, its
    left end, instead of keeping the current time. The observer's settings (distance, dust, beam toward Earth) and
    Re-roll still keep the time and playback. At t = 0 every view shows the initial condition (ruling R-P3-42).
- The physics is **deterministic**: the same inputs always give the same outcome. Randomness appears only in
  displayed **events** (individual neutrino and photon counts, drawn from Poisson statistics). It uses a seed
  shown as "Run #n", and **Re-roll** draws new events.

## 3. The outcome

The `OutcomeEngine` implements outcomes §8.2 steps 0–9 exactly. Its result, `Outcome`, holds:
- **Fate:** no death yet, white dwarf, supernova, failed supernova (black hole, no explosion), pair-instability
  supernova, or pulsational pair instability followed by a black hole.
- **Supernova type:** II-P, II-L, IIb, IIn, Ib, Ic, Ic-BL, Ibn/Icn, SLSN-I, SLSN-II, Ia (normal, 91T-like,
  91bg-like), Iax, PISN; plus whether a long gamma-ray burst occurs.
- **Remnant:** none, white dwarf, neutron star (mass, radius, spin period P₀, magnetic field B, kick), magnetar
  (P₀, B), or black hole (mass).
- **Explosion:** E (in 10⁵¹ erg), ejecta mass, ⁵⁶Ni mass, and peak luminosity.
- **Progenitor at death:** final mass, H and He envelope masses, CO core mass, radius, and surface temperature.
- **Wind:** mass-loss rate and wind speed, for the gas around the star (signals §0.4).
- **Notes:** plain-language explanations of each decision and of any input that was ignored.
- **Every quantity** is a `Quantity` holding: value, unit, `Confidence` (Established / ModelDependent /
  Speculative), source citation, and an optional range (low–high).

**Per-mass explodability table.** Step 4 needs explosion results per initial mass at 0.1 M☉ resolution
(Sukhbold et al. 2016). Plan Task 2 tries to obtain the published per-model table (doi:10.17617/1.b) and
converts it to an embedded CSV.
- If the table can't be obtained, the CSV is built from the band rules in outcomes §1.2–§1.4. Its Source column
  records which was used, and the outcome card's citation says so.
- Either way, `OutcomeEngine` reads only the CSV.

**The outcome summary** (top of the main window's right column) shows:
- the type, fate and remnant in large text, with a one-sentence plain-language summary;
- three or four key numbers with confidence badges;
- a **Details…** button, which opens the Outcome details window (§5.0).

**The Outcome details window** shows every quantity, each with its confidence badge, range and full citation,
plus all the notes.

**The outcome map** (beneath the card) is a strip chart of fate against initial mass (0.8–300 M☉, log scale)
for the current metal content and stripping, colored by fate. A marker shows the current mass, so the islands
are visible at a glance. Clicking the strip sets the mass.

## 4. Time

- **t = 0** is core bounce (core collapse) or ignition (Type Ia). The timeline runs on a log scale from 1 ms to
  1,000 years (10⁻³ to 3.156×10¹⁰ s), in observer time (stretched by 1 + z).
- Before Explode, or with the scrubber at its left end, the view shows the star before the explosion.
- **Playback:**
  - The speed is set in decades of time per second of playback (default 1 decade per 3 s, so the whole run takes
    about 40 s).
  - Controls: Play/Pause, a scrubber, a speed slider, and **event markers** on the scrubber, which jump there when
    clicked. Markers: neutrino burst, gravitational waves, black hole forms (if any), shock breakout, gamma-ray
    burst (if any), peak brightness, end of plateau, gamma-ray lines peak, radio peak, pulsar emerges, remnant
    becomes resolvable.
- A line under the scrubber says when the light **left** the supernova: "Light from this moment left the star
  163,000 years before it reached Earth."
- All signals travel at the speed of light. The neutrino delay from neutrino mass is under a second at 10 kpc,
  and is ignored and stated in the glossary.

## 5. Windows and instrument views

### 5.0 Window layout (decided with the owner, 2026-11-11)

The main window would be overcrowded with everything in it: at 1366×768, six graphs would get about 245×180 px
each. So the details live in their own **non-modal** windows. They stay open while the timeline plays, can move
to a second monitor, and bind to the same view-model instances as the main window, so everything stays in sync.

- **Main window (the lab):**
  - Left column: presets with an ⓘ button, Star and Observer inputs, the Advanced expander, and Explode / Re-roll.
  - Center: the 3D view.
  - Right column: the outcome summary (§3) and the outcome map.
  - Above the timeline: the **instrument strip**. Six small tiles, one per messenger, each with a sparkline of its
    main signal, a moving time cursor and the detection badge. Clicking a tile opens the Instruments window on
    that panel.
  - Bottom: the timeline.
- **Instruments window:** the six full panels described below, in a 3×2 grid. Double-clicking a panel maximizes it
  within the window; Esc restores the grid. Opened from the strip or the View menu. Its size and position are
  remembered for the session.
- **Outcome details window** (§3). Opened from the summary's Details… button.
- **Preset info window:** the preset's "What was actually seen" story, with citations. Opened from the ⓘ button.
- Each window opens at most once; asking again brings it to the front. Closing the main window closes the others.

### 5.1 Instrument panels

Six panels, one per messenger or band. Each panel has:
- a **plot against log time**, with the shared "now" cursor;
- **sub-tabs** where a band has several signals;
- an **instrument picker** (real instruments, with their published sensitivities);
- a **detection badge** at the current time: Detected / Too faint at this distance / Blocked by dust /
  Not pointed at us / Not produced by this supernova / Not yet;
- the **confidence badge** for the panel's model;
- a **glossary tooltip** on every term.

All models, validity ranges and thresholds follow signals §11.

| Panel | Sub-tabs (models from signals.md) | Instruments |
|---|---|---|
| **Gravitational waves** | Strain h(t) and spectrogram, first ~2 s (§2.4; the rotating-bounce signal when rotation is Fast or Extreme, §2.2) | Advanced LIGO, Einstein Telescope, Cosmic Explorer |
| **Neutrinos** | Event rate and cumulative events, with individual events drawn as dots (Poisson, seeded); luminosity and mean energy per flavor; the cutoff when a black hole forms (§1) | Super-Kamiokande, Hyper-Kamiokande, IceCube, JUNO, DUNE |
| **Gamma rays** | GRB prompt emission (§9.1); line fluxes ⁵⁶Ni 158/812 keV, ⁵⁶Co 847/1238 keV, ⁴⁴Ti over centuries (§5) | Fermi GBM, Swift BAT, INTEGRAL/SPI, COSI, NuSTAR (⁴⁴Ti) |
| **X-rays & UV** | Shock-breakout flash and cooling (§3); interaction with the gas around the star (§6.2); GRB afterglow (§9.3); neutron star cooling (§8.4) | Swift, Einstein Probe, ULTRASAT, Chandra, XMM-Newton |
| **Visible light** | Light curve in apparent V magnitude with the naked-eye line at +6, including extinction (§4); a **schematic spectrum** at the current time, blackbody plus the identifying lines at the current velocity (§10), labeled "schematic" | Naked eye, ZTF, Rubin, a typical spectrograph |
| **Radio** | Light curves at 1.4, 5 and 22 GHz from the interaction (§6.1); pulsar pulses, when a pulsar exists and its beam points at us, after the ejecta clear (§8.3); remnant radio brightness (§7) | VLA, MeerKAT, FAST, SKA |

- No template spectra are bundled; the Nugent templates have no stated license. The spectrum is drawn from the
  line list and velocities in signals §10.
- Peak brightness gets a plain-language comparison where it helps, for example "as bright as Venus" or "visible in
  daylight".

## 6. The 3D explosion view (simple, Project 1)

- **The star before the explosion:** a sphere sized to its radius, colored by surface temperature (blackbody
  color), with a translucent shell showing the gas around the star when it's significant.
- **After bounce:**
  - The shock and ejecta appear as an expanding particle shell (about 50,000 points). The outer speed comes from
    E and M_ej (signals §0.4), and the color from the model's temperature at that time (blackbody color, dimmed
    as luminosity falls).
  - Jets appear as two cones along the rotation axis when a GRB occurs.
  - The remnant appears as a marker: a small bright point for a neutron star (with sweeping beams if it's a
    pulsar), and a black disk with a thin ring for a black hole. Nothing is shown when no remnant is left.
- **Scale:** the size spans from a star (10⁶ km) to a remnant (parsecs). The camera **auto-frames** the ejecta by
  default, with a scale bar in km, AU, light-years or parsecs. The orbit camera, as in Universe3D, can take over:
  drag rotates, the wheel zooms, Home restores auto-framing.
- The overlay shows the time and the current physical radius.

## 7. Code structure

```
C:\repos\SupernovaLab
  SupernovaLab.sln, Directory.Build.props (the short-path rule from Universe3D, folder "SupernovaLab.build")
  src/SupernovaLab.Core        (net8.0; no UI)
    Units/        PhysicalConstants (cgs, from signals §0.2), Quantity, Confidence, Source, unit formatting
    Progenitors/  StarInputs, WhiteDwarfInputs, PresupernovaStar, ProgenitorModel (outcomes §8.2 steps 0–3)
    Outcomes/     OutcomeEngine (steps 0–9), Outcome, ExplodabilityTable (reads the embedded CSV),
                  RemnantModel, SpectralTyper, TypeIaModel, OutcomeMap (fate vs mass strips)
    Observer/     ObserverSetup (distance, A_V, beaming), Cosmology (copied from Universe3D), Extinction
    Signals/      ISignalModel and one class per model: NeutrinoModel, GravitationalWaveModel, ShockBreakoutModel,
                  LightCurveModel (Ni/Co decay, Arnett, plateau, magnetar), BolometricCorrection, GammaLineModel,
                  CsmInteractionModel (radio + X-ray), RemnantEvolutionModel, PulsarModel, NeutronStarCoolingModel,
                  GrbModel, SpectrumModel; Detectors (instrument sensitivities); EventSampler (seeded Poisson)
    Timeline/     TimeAxis (log mapping, observer time), EventMarkers
    Presets/      Preset, PresetCatalog (embedded JSON, from presets.md)
    Text/         Glossary, OutcomeTexts, Comparisons ("as bright as Venus")
    Data/Embedded/ explodability-s16.csv, presets.json
  src/SupernovaLab.Rendering   (net8.0-windows; OpenTK 4.9.4, OpenTK.GLWpfControl 4.3.6)
    GlHost, ShaderProgram, OrbitCamera (from Universe3D), StarRenderer, EjectaRenderer, JetRenderer,
    RemnantRenderer, ScaleBar, Shaders/*
  src/SupernovaLab.App         (net8.0-windows; WPF, CommunityToolkit.Mvvm 8.3.2, ScottPlot.WPF 5.1)
    ViewModels/ (MainViewModel, LabViewModel, OutcomeViewModel, TimelineViewModel, one per instrument panel),
    Views/ (panels, instrument strip, outcome summary, outcome map), Windows/ (InstrumentsWindow,
    OutcomeDetailsWindow, PresetInfoWindow), Services/IWindowService + WindowService,
    Controllers/SceneController, MainWindow.xaml
  tests/SupernovaLab.Tests     (xUnit)
```

- **Every signal model** implements `ISignalModel`:
  - `Applies(Outcome)`;
  - `Sample(Outcome, ObserverSetup, double tSeconds)`, returning the observed quantities at that time;
  - `ValidRange`;
  - `Confidence` and `Sources`;
  - `Detect(..., Detector)`, returning a `Detection` (status plus a reason).
  Models are pure functions of their inputs, with no UI and no state, so they are directly unit-testable.
- **Panels sample their models** on a fixed log-time grid (400 points per curve) when the outcome or observer
  changes, and only move the cursor while playing.
- **MVVM:**
  - Models are the Core types.
  - View models live in the App and are unit-tested.
  - Views are XAML with minimal code-behind.
  - View models never create windows. They call `IWindowService` (`ShowInstruments(Messenger?)`,
    `ShowOutcomeDetails()`, `ShowPresetInfo()`), and tests substitute a fake that records the calls.
- `OutcomeEngine` and the signal models depend only on Core types. The App depends on Core and Rendering.
- Code reused from Universe3D (`Cosmology`, `OrbitCamera`, `GlHost`, `ShaderProgram`, the short-path build rule)
  is **copied**, not referenced. Each repo stands alone.

## 8. Testing

xUnit tests in `SupernovaLab.Tests`.
- **Formulas:** each model formula is checked against a reference value computed independently from the research
  files. Examples:
  - ⁵⁶Ni/⁵⁶Co heating at 10 and 100 days;
  - Arnett peak luminosity for 0.6 M☉ of Ni (≈ 1.2×10⁴³ erg/s);
  - the Popov/Goldberg plateau for a 15 M☉ red supergiant;
  - the Sedov radius;
  - the Crab spin-down luminosity (≈ 4.5×10³⁸ erg/s);
  - the Chevalier peak frequency.
  Tolerance: 1% for exact formulas, and as stated for fits.
- **Outcome engine:**
  - the islands (§1, success criterion 2), and every row of the explodability CSV is reproduced at its own mass;
  - white dwarf below 8 M☉;
  - electron-capture supernova at 9.1 M☉;
  - pair-instability supernova for a 200 M☉ star at Z = 0.001;
  - a direct black hole above the pair-instability range;
  - Full + He-stripped stripping gives Ic, Full gives Ib, Partial gives IIb;
  - Extreme rotation at low Z with stripping gives Ic-BL + GRB;
  - Strong B + fast rotation + stripping gives SLSN-I with a magnetar;
  - the Type Ia channels.
  A property test sweeps every input combination on a coarse grid: no exceptions, every quantity finite, and
  every Quantity has a confidence and a source.
- **Presets** (validation against what was observed). Tolerances come from the uncertainties in presets.md; only
  [V] values, or [R] values re-verified by Plan Task 3, are tested:
  - SN 1987A: Kamiokande-II expected count 6–20 (11 or 12 seen); peak V of about +2.9 within 0.5 mag.
  - SN 2014J: ⁵⁶Co 847 keV flux at about 50–100 days within a factor of 2 of INTEGRAL.
  - SN 2011fe: peak apparent B or V within 0.3 mag.
  - Crab: P = 33 ms, and a spin-down age within 50% of its true age.
  - SN 2008D: X-ray breakout luminosity within a factor of 3 of 6×10⁴³ erg/s.
  - Betelgeuse: Super-K count of 10⁶–10⁸; peak apparent V brighter than −8.
- **Detection logic:** for example, a non-rotating collapse at 5 kpc is detected by Advanced LIGO but not at
  50 kpc, and Super-K detects to about 200 kpc but not at Andromeda (signals §11).
- **Timeline:** log mapping round trips and (1 + z) stretching.
- **Visual and performance checks:** screenshots of each panel for SN 1987A, SN 1998bw and Betelgeuse, and the
  fps targets of §1. Manual, as in Universe3D, using the screenshot helper.

## 9. Error handling

- **Inputs outside a model's validity range:** the model returns "not modeled here" with the reason, shown in
  the panel. Never a made-up value.
- **Inputs that have no effect** (for example a strong field when a black hole forms): a note in the outcome card.
- **NaN or infinity from a model:** caught at the panel boundary, logged, and shown as "model error" in that panel
  only. The property test (§8) aims to make this unreachable.
- **OpenGL failure or version below 4.1:** the 3D view shows a message naming the requirement; the lab and the
  instrument panels keep working without it.

## 10. Build order (detailed in the implementation plan)

1. Solution scaffold, units and constants, and `Quantity`/`Confidence`.
2. The explodability table (per-mass S16, or the fallback) and preset data, including verifying the [R] values
   used in tests.
3. The outcome engine and outcome map, with tests.
4. The signal models, one group at a time (neutrinos and gravitational waves; breakout and light curves; gamma
   lines; circumstellar interaction, remnant and pulsar; GRB; spectrum), with formula tests.
5. Presets and validation tests.
6. The app shell: lab panel, outcome summary and Outcome details window, outcome map, timeline, preset info
   window, window service.
7. The six instrument panels in the Instruments window, and the instrument strip.
8. The simple 3D view.
9. Glossary, polish, README, screenshots and performance checks.

## 11. Out of scope for Project 1

- The full 3D showpiece (Project 2) and the remnant close-up (Project 3).
- Hydrodynamic or radiative-transfer simulation.
- Asymmetric explosions, except jets.
- Kilonovas, neutron star mergers, tidal disruptions and other transients that aren't supernovas.
- K-corrections and other band-shift effects at large distance (stated in the UI above 50 Mpc).
- Saving or sharing lab setups (a later option: a small JSON file).
- Bundled template spectra (licensing); the spectrum is schematic.

## 12. Decisions made under the standing instruction (for review)

- **D1:** Name `SupernovaLab`; local repo `C:\repos\SupernovaLab`; no GitHub until asked.
- **D2:** Graphs use ScottPlot 5 (MIT, actively maintained, fast WPF control), not OxyPlot or a hand-written
  plotting control.
- **D3:** The research files are normative; the spec cites them rather than repeating ~2,500 lines of numbers.
- **D4:** Deterministic physics; randomness only in displayed events, with a visible seed and Re-roll.
- **D5:** "Jet or beam toward Earth" defaults to on, so a GRB or pulsar beam is visible in the lab. The tooltip gives
  the real odds.
- **D6:** Six panels by messenger or band, with sub-tabs, rather than eleven separate panels.
- **D7:** Distances up to 1 Gpc, so SN 2015bn (about 500 Mpc) can be shown, with (1 + z) time stretching and
  luminosity distance. No K-correction.
- **D8:** Default Ni yield calibrated to observations; theory values are available under Advanced.
- **D9:** Presets pin published values and show "Published value" badges. Changing any input returns to the model.
- **D10:** Code shared with Universe3D is copied, not referenced.
