# SupernovaLab

A Windows desktop app for setting off supernovas and watching them through every kind of instrument at once.

You pick a star, or load a real supernova, and press **Explode**. The app works out what happens: what kind of
supernova it is and what is left behind. It then plays the event on one timeline, from the first millisecond to a
thousand years, as each instrument would record it: gravitational waves, neutrinos, gamma rays, X-rays and UV,
visible light, and radio. A simple 3D view shows the star and the expanding debris.

## What it is

SupernovaLab is a "what if" lab. It is not a simulation. A real supernova simulation takes a supercomputer months. The
app uses the analytic and fitted models that astronomers publish and use to read their observations. The numbers,
formulas and citations are in `docs/research/`.

It is built in three projects:

- **Project 1 (this one).** The physics core, the lab, the six instrument panels and a simple 3D explosion view. It is
  a complete app on its own.
- **Project 2.** A full 3D showpiece of the explosion. Not started.
- **Project 3.** A close-up of the remnant: the neutron star, pulsar beams, a magnetar, a black hole bending light.
  Not started.

All views read from one physics core, and shared quantities (the explosion's energy and ejecta, the remnant, the
colour temperature, the magnetar's and the plateau's light) are defined once. Where a view still simplifies (the 3D
view's uniform ball, the schematic spectrum's type-typical velocities), it says so.

## System requirements

| | Minimum | Recommended |
|---|---|---|
| OS | Windows 10 (22H2) or 11, 64-bit | Windows 11, 64-bit |
| Runtime | .NET 8 Desktop Runtime | same |
| GPU | OpenGL 4.1 (Intel UHD 620 class or better) | Intel Iris Xe / Radeon 680M or any dedicated GPU of the last ~8 years |
| CPU / RAM | 2 cores / 8 GB | 4 cores / 16 GB |
| Display | 1366×768 | 1920×1080 or larger |
| Disk | 150 MB | same |
| Network | not used | same |

If the GPU cannot give OpenGL 4.1, the 3D view shows a message that says so. The lab and the instrument panels keep
working.

These are the design targets. The minimum class has not been measured (see `docs/perf-2026-11.md`).

## How to use

**The main window is the lab.**

- **Left column.**
  - Pick a **preset** (a real supernova) or set the star yourself: initial mass, metal content, stripping by a
    companion, rotation and core magnetic field. Or choose a white dwarf in a binary (Type Ia).
  - Set the **observer**: distance, dust, and whether a jet or pulsar beam points at Earth.
  - **Advanced (uncertain physics)** holds nickel mixing and yield, dense gas around the star, and the ejecta opacity.
- **Explode** computes the outcome and starts the timeline at t = 0. Changing an input recomputes it at once.
  **Re-roll** draws new random events (individual neutrino and photon counts). Everything else is deterministic.
- **Right column.** The outcome: type, fate, remnant and a few key numbers. **Details...** opens every quantity with its
  range and full citation. Below it, the **outcome map** colours fate against initial mass. Its islands, where a star
  collapses to a black hole between masses that explode, are visible at a glance. Click it to set the mass.
- **Centre.** The 3D view. Drag to turn, wheel to zoom, Home to auto-frame. It shows the time, the radius and a scale
  bar.
- **Timeline** (bottom). A log scale from 1 ms to 1,000 years, in observer time. Play, pause, drag the scrubber, set the
  speed, or click an event marker.
- **Instrument strip.** Six tiles, one per messenger, each with a sparkline and a detection badge. Click one to open the
  **Instruments window**: six panels with plots against log time, sub-tabs, real instruments with their published
  sensitivities, and a badge saying whether that instrument would see the event now.

Hover over any term for a plain-language explanation.

**Presets.** A preset sets the lab to its best match and pins the published explosion values (energy, ejected mass,
nickel mass, radius, distance, dust). Pinned numbers carry a **Published value** badge with the citation. Change any
star or explosion input and the pins drop, with a message. Change only the observer and the pins stay, so you can see
the same event from somewhere else. The **i** button opens "what was actually seen".

## How accurate is it

Short answer: it follows published models and observations, and it says where it does not. It is good for seeing how
the signals relate and roughly how big they are. It is not a prediction tool, and many results are only good to a
factor of a few.

### Confidence labels

Every number on screen carries one of three labels and a source. Hover to see it.

- **Established.** Measured, or well-tested physics. Small uncertainty.
- **Model-dependent.** From a published model that matches observations, but it depends on that model's assumptions.
- **Speculative.** An approximation, an extrapolation, or physics that is still debated. It could be off by a factor of
  a few.

Where the research files had no value, the code uses an approximation marked `[APPROX]`. Such values are labelled
Speculative.

A separate blue **Published value** badge means the number is a published measurement for the chosen real supernova,
not the lab's model. The tooltip names the paper.

The research files are in `docs/research/`:

- `outcomes.md`: from a star's properties to its fate. Its section 8.3 is the confidence table.
- `signals.md`: what each instrument records over time. Its section 12 lists the key uncertainties.
- `presets.md`: the real supernovas.
- `presets-verification.md`: which preset values were checked against the papers, which could not be, and the known
  misses.

### Checks against real supernovas

The spec asks each preset to reproduce a few observations. These are the results.

| Preset | Check | Observed | Lab | Result |
|---|---|---|---|---|
| SN 1987A | Kamiokande-II neutrino count within 6-20 | 11 seen (Hirata et al. 1987) | 18.45 | Pass, but above the 11 seen |
| SN 1987A | Peak V within 0.5 mag | 2.975 at day 84.5 (Hamuy & Suntzeff 1990) | 3.22 at day 86.4 | Pass (0.24 mag) |
| SN 2014J | 847 keV line, days 50-100, within x2 | 2.34e-4 ph/cm²/s (Churazov et al. 2014) | 2.97e-4 | Pass (1.27x) |
| SN 2011fe | Peak V within 0.3 mag | 9.98 (Pereira et al. 2013) | 10.20 | Pass (0.22 mag) |
| Crab | Pulse period 33 ms | 33.3924 ms (ATNF catalogue) | 33.39 ms at the catalogue epoch, 33.83 ms at 970 yr | Pass, consistency check |
| Crab | Spin-down age within 50% of true age | about 970 yr | 1,290 yr (1.33x) | Pass, consistency check |
| SN 2008D | X-ray luminosity within x3 | 6.1e43 erg/s (Soderberg et al. 2008) | 4.28e43 | Pass (0.70x), see note |
| Betelgeuse | Super-Kamiokande count 1e6 to 1e8 | none (not exploded) | 4.0e7 at 168 pc | Pass |
| Betelgeuse | Peak V brighter than -8 | none | -10.37 | Pass |

Notes on these:

- **The Crab check is a consistency check, not independent agreement.** The lab's initial spin period and field are
  worked out from the catalogue's own period and period derivative. The model therefore gives the catalogue period
  back by construction. The check shows the arithmetic is consistent. It does not show the physics is right.
- **SN 2008D** passes only because the lab caps the shock speed at 0.3c (the cap is an approximation), evaluated at the
  published star radius. The check tests that cap. It does not test shock-breakout physics. The preset also mixes
  two papers: energy, ejected mass and nickel from Mazzali et al. 2008, and the star radius from Soderberg et al. 2008.
  The preset's source says so.
- **SN 1987A's neutrino count** is in the allowed band, but 18 is well above the 11 events seen.
  The lab's value depends on the assumed neutron star mass and on the detector's
  efficiency curve. Seeing 11 or fewer when 18.45 are expected has a Poisson probability of about 4.5%. The
  Kamiokande-II fact in the app says so.
- Betelgeuse has no observation to compare with. Its two checks are only plausibility bounds.

### Known misses

These are places where a preset's lab result visibly disagrees with what was observed. They are disclosed, not tuned
away. Each is also stated in that preset's own summary in the app.

| Preset | What was observed | What the lab gives | Why |
|---|---|---|---|
| SN 2008D | An X-ray flash of about 400 s and 2e46 erg. Swift caught it. | A bare-star flash of about 2 s and about 200x less energy. The lab's Swift does not catch it. | The real breakout was most likely through a dense wind. The lab does not model that. |
| SN 1998bw | Radio of 45 mJy at 4.8 GHz on day 12 (Kulkarni et al. 1998) | About 3.7 mJy at 5 GHz on day 12, 9 to 12 times too faint | The lab's shock formulas are Newtonian, capped at the speed of light. The real shock was mildly relativistic (E ~1e50 erg in ejecta at Γβ ~1 to 2; Kulkarni et al. 1998, Nature 395, 663, via `docs/research/presets.md`). |
| SN 1998bw | Peak V 13.62 near day 16 (Galama et al. 1998) | V 14.1 on day 23 | A one-zone (Arnett) light curve over all 10.9 solar masses of ejecta. |
| SN 1987A | Radio peaked near 130 mJy at 843 MHz within days, and faded below detection after September 1987 | About 350 mJy at 1.4 GHz near day 18, still about 90 mJy on day 100 | The research has no wind for a blue supergiant. The lab uses a Wolf-Rayet-like wind and a red-supergiant shock speed. No single paper giving both was verified, so nothing is pinned. |
| SN 1987A | 847 keV line of 1.0e-3 ph/cm²/s over days 159-250 (SMM) | 4.6e-5 over the same days, about 22 times too faint | The lab has no outward mixing of the cobalt. |
| SN 1987A | No pulsar has been seen | From 10 years, the 3D view draws a pulsar with sweeping beams | The lab's pulsar model gives every neutron star one. JWST found indirect evidence of a hidden neutron star in 2024 (Fransson et al.). |
| SN 1054 | Peak roughly -4 to -7, visible in daylight for 23 days, from the historical records | Peak V about -2.0, behind dust | The lab's weak electron-capture explosion. Not tuned. |
| SN 2011fe | INTEGRAL/SPI did not detect its gamma-ray lines (Isern et al. 2013) | Not detected, but the margin is thin: at best 0.76 of the threshold | It rests on SPI's resolution (taken as 2 keV; the ratio would be about 0.93 at 3 keV) and on 0.53 solar masses of nickel (about 0.70 would reach the threshold). The limit used is preliminary. |
| SN 2011fe | B-band decline Δm15(B) = 1.10 ± 0.04 (Pereira et al. 2013) | 0.99, measured on the drawn curve | Every near-Chandrasekhar Type Ia in the lab has one light-curve shape (the nickel sets only its brightness), and its colour after the peak is a simple blend toward the Lira relation. |
| SN 2015bn | Peak 2.3 ± 0.4e44 erg/s after a 69-day rise (Nicholl et al. 2016) | 1.8e44 erg/s (0.77 of it, about 1.3σ low) at about day 62 | The published magnetar goes through the lab's one-zone diffusion, not the fit's own model. |
| SN 1998bw, SN 2015bn | Published ejecta and remnant (1998bw: 10.9 + 2.9 solar masses, Iwamoto et al. 1998) | Both exceed what the lab's star holds when it dies (11.2 and 9.3 solar masses) | The lab's star models are lighter than the real stars. The published values are kept, and a note says so. |

### Other known limits

These come from the research files (`signals.md` section 12 and `outcomes.md` section 8.3).

- **Neutrinos.** Models differ by a factor of about 2 in the signal; the Super-K total varies by about 5 times across
  published models. Flavour conversion is not settled.
- **Gravitational waves.** The amplitude spans about three orders of magnitude in energy across 3D models. Without
  rotation, current detectors reach only a few kpc.
- **Shock breakout.** It depends on the outer layers of the star and on any dense wind.
- **Visible light.** The opacity and nickel mixing set the rise time. Plateau fits scatter by 0.3 to 0.4 mag.
- **Dust** often decides whether a supernova is visible at all.
- **Radio and X-rays** depend on microphysics parameters as much as on the mass-loss rate. Free-free absorption is not
  in the radio model.
- **Outcomes.** The black-hole islands exist in published models, but their positions differ by 1 to 3 solar masses
  between groups. Whether Ib and Ic differ in helium is contested. Rotation thresholds for gamma-ray bursts are
  speculative. The mapping from a star to a magnetar's spin and field is speculative. At 30 solar masses the lab
  switches star models and rules, so the cores jump there; the outcome map says so.
- **Magnetar-powered supernovae.** A superluminous class needs a star of more than 20 solar masses and at least 2.2
  solar masses of ejecta (Nicholl et al. 2017, as the research gives them); outside these the lab keeps the ordinary
  class and calls it magnetar-boosted. The lab assumes the ejecta trap all of the magnetar's power, so with extreme
  rotation some outcomes peak above 2e45 erg/s, brighter than any supernova ever observed (ASASSN-15lh; Dong et al.
  2016). Such outcomes say so in their notes and in the visible-light panel.
- **Type Ia peak B magnitude** is estimated from the engine's peak luminosity (taking B and bolometric magnitudes as
  equal). It does not shape the light curve. The decline rate Δm15(B) is measured on the drawn curve.
- **Above 50 Mpc** the shift of light between bands (K-correction) is not modelled. The app says so.
- **The 3D view is schematic.** Its colours are approximate, the ejecta are drawn as a uniform ball, and the gas around
  the star is drawn out to a made-up 1e15 cm. The view says so.
- **No spectra** are bundled. The spectrum panel draws a schematic blackbody with the identifying lines. Its line
  velocities are typical values for each type, scaled by the explosion's energy per unit mass and kept inside the
  observed range. The caption says so.
- **Some values are marked `[APPROX]`** in the code and research files and should be checked before relying on them. They
  include the titanium-44 rule for outcomes without a published titanium-44 mass (2e-3 of the nickel mass; SN 1987A's
  and Cas A's own masses are published values), the N_H/A_V ratio, some detector thresholds, the gamma-ray line widths
  used to scale thresholds, and the spectrum's velocity scaling.

## Building

You need the .NET 10 SDK (or any SDK that can build `net8.0` projects) and a Windows machine. The app itself targets
.NET 8.

```
dotnet build SupernovaLab.sln
dotnet test tests/SupernovaLab.Tests
dotnet run --project src/SupernovaLab.App
```

Use `-c Release` for a Release build. The projects are `SupernovaLab.Core` (physics, no UI), `SupernovaLab.Rendering`
(OpenTK 3D view), `SupernovaLab.App` (WPF) and `SupernovaLab.Tests` (xUnit).

### Diagnostic options

The app has options for checking itself without a screen capture. `--snapshot` renders the app's own windows to PNG
files with WPF's `RenderTargetBitmap` (the 3D view is read back from OpenGL). It writes `snapshot.log` and exits. The
other options need `--snapshot`.

```
SupernovaLab.App.exe --snapshot <out-dir> [--preset <list>] [--time <seconds>] [--size <WxH list>]
                     [--window <list>] [--sweep <steps>] [--bench <seconds>]
```

- `--snapshot <out-dir>`: the output folder. Required.
- `--preset <list>`: preset ids, comma-separated or repeated (for example `sn1987a,sn1998bw`). An entry can carry its
  own time, `id@seconds`, in observer time (`sn1987a@86400`). Add `+densecsm` to an entry to turn on the lab's
  dense-gas input after the preset loads. Without this option the lab's defaults are used.
- `--time <seconds>`: the observer time for every preset that has no `@seconds`. Positive number.
- `--size <WxH list>`: the main window size, for example `1366x768,1920x1080`. Default 1366x768.
- `--window <list>`: which windows to render: `main`, `main-advanced`, `instruments`, `instrument-tabs`, `details`,
  `presetinfo`. Default `main`.
- `--sweep <steps>`: loads every preset in the catalog and steps the timeline through that many log-spaced times from
  1 ms to 1,000 years (2 to 2000), with the 3D view and Instruments window running. Any exception, panel "Model error"
  or 3D-view failure goes to `sweep.log`. The exit code is 1 if there was a problem.
- `--bench <seconds>`: plays the whole timeline once over that many seconds (above 0, at most 600) with the main and Instruments
  windows open. It writes the frame rate, the 3D view's frame rate and the GPU name to `bench.log`.

An example, one launch for several checks:

```
SupernovaLab.App.exe --snapshot out --preset sn1987a@1e7,sn1998bw@86400 --window main,instruments --sweep 54 --bench 20
```

The windows are placed off-screen. Tooltips cannot be rendered this way, so they are checked by unit tests instead.

Performance measurements are in `docs/perf-2026-11.md`.

## Sources

The explodability table comes from the published per-model data of Sukhbold et al. 2016 (MPA Garching archive). Its
provenance is in `tools/explodability/README.md`. Every formula and constant is cited in `docs/research/`.
