# SupernovaLab

A Windows desktop app for setting off supernovas and watching them through every kind of instrument at once.

You pick a star, or load a real supernova, and press **Explode**. The app works out what happens: what kind of
supernova it is and what is left behind. It then plays the event on one timeline, from the first millisecond to a
thousand years, as each instrument would record it: gravitational waves, neutrinos, gamma rays, X-rays and UV,
visible light, and radio. A 3D view shows the star, its layers and the expanding debris, from the first millisecond to a
remnant light-years across.

## What it is

SupernovaLab is a "what if" lab. It is not a simulation. A real supernova simulation takes a supercomputer months. The
app uses the analytic and fitted models that astronomers publish and use to read their observations. The numbers,
formulas and citations are in `docs/research/`.

It is built in three projects:

- **Project 1.** The physics core, the lab, the six instrument panels and a simple 3D explosion view.
- **Project 2 (built on Project 1).** The 3D showpiece of the explosion: distance rings and a director
  camera, layered debris with mixing and a cutaway, shocks, the gas around the star, a pulsar wind nebula, SN 1987A's
  rings, the breakout flash, and a guided tour. See "The 3D view" below. It draws only: the instrument panels still use
  Project 1's models.
- **Project 3 (this version).** The **Close-up window**: the remnant from close by, on the same timeline. The first
  second at the core (the stalled shock and the proto-neutron star), the neutron star with its layers, spin, field and
  pulsar beams, a magnetar's giant flares, and a black hole bending the light of the explosion behind it. See "The
  Close-up window" below. Signals never observed from any supernova now say "Expected" instead of "Detected".

All views read from one physics core, and shared quantities (the explosion's energy and ejecta, the remnant, the
colour temperature, the magnetar's and the plateau's light) are defined once. Where a view still simplifies (the 3D
view's schematic colours and mixing fingers, the schematic spectrum's type-scaled velocities), it says so.

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

**Known performance gap (Project 3).** On the recommended Intel Iris Xe the final benchmark met 50 fps with the Close-up
open for a black hole without debris (N6946-BH1: 64 fps, 52 fps while turning the Close-up), but not for outcomes with
debris (the Crab: 30 fps; SN 1998bw: 34 fps). The main window alone was also slow there (34–39 fps, against 70–78 fps
measured earlier in Project 3), so the cause is in the main and Instruments windows' drawing, not the Close-up; it is not
yet identified. If the Close-up is slow on your machine, View > Light-bending detail > Quarter makes it cheapest.

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
- **Centre.** The 3D view (next section). Drag to turn, wheel to zoom, Home to auto-frame. It shows the time, the
  radius, the distance rings and notes on what is schematic.
- **Timeline** (bottom). A log scale from 1 ms to 1,000 years, in observer time. Play, pause, drag the scrubber, set the
  speed, or click an event marker.
- **Instrument strip.** Six tiles, one per messenger, each with a sparkline and a detection badge. Click one to open the
  **Instruments window**: six panels with plots against log time, sub-tabs, real instruments with their published
  sensitivities, and a badge saying whether that instrument would see the event now.

Hover over any term for a plain-language explanation.

**The 3D view.** It follows the timeline, so it shows the same explosion and the same moment as the panels.

- **Distance rings** (grey, labelled "50 AU", "2 light-years"). Three or four rings on a 1-2-5 ladder, centred on the
  explosion. When the debris touch a ring, they have that radius. They replace Project 1's scale bar.
- **The director camera.** The camera holds its scale while the debris grow, then eases back about 4 to 5 times when they
  reach the outermost ring. At the default playback speed it is still for at least two thirds of each pull-back. Drag
  to turn (the director keeps running). The wheel zooms and freezes the scale; **Home** hands it back to the director. A
  click on the timeline, a new preset or a new outcome re-frames at once.
- **Layers and mixing.** About 200,000 particles carry the star's element layers by mass: hydrogen, helium, oxygen and
  silicon, nickel and iron (a Type Ia: nickel, intermediate-mass elements, unburned carbon and oxygen). Their speeds
  follow the published density profile of the ejecta. Rayleigh-Taylor "fingers" carry nickel outward and hydrogen inward,
  to the speeds the research gives (SN 1987A's are the observed 4,700 and 700 km/s). Finger shapes are schematic.
- **Cutaway** (View > Cutaway). Removes a quarter wedge so you can see inside the star before breakout, and the layers in
  the debris after it. The colour key at the bottom left names the elements. The cut faces are drawn from the layer
  masses; the colours are schematic.
- **Shocks.** The shock running out through the star, then, after breakout, the forward shock, the contact surface and the
  reverse shock, with the gas they sweep up. Debris behind the reverse shock is drawn hot. The shock radii join two models
  between 3 and 10 years; that stretch is labelled schematic.
- **Surroundings.** The progenitor's wind as a haze, dense shells (for example SN 2023ixf's confined gas), and the swept-up
  remnant.
- **A pulsar wind nebula** inside the debris of every outcome that leaves a pulsar, grown from the pulsar's power. For the
  Crab it comes out small (the lab's SN 1054 has more debris than the Crab is thought to have), and its outer shell is
  predicted but has never been detected; for SN 1987A one is drawn although none has been confirmed. The view says each of
  these in its notes.
- **SN 1987A's rings,** at their measured size and tilt (drawn as symmetric circles, which the real outer rings are not).
  The hotspots' brightness follows the observed X-ray light curve from about day 3,000; the drawn spherical shock reaches
  the ring only about 17 years after the explosion, and the view says so.
- **The breakout flash** and a glow around bright gas (HDR with bloom). The flash's brightness comes from the breakout
  model.
- **The guided tour** (the Tour button beside Play). It stops at each event of this explosion, and for a preset also at
  the dates in its history, and shows a caption. Every number in a caption is the lab's own; history lines are marked
  "Observed", with the source on hover. The tour moves the timeline, turns on the cutaway before breakout, brightens the
  instrument that sees the event, and pauses when you touch the timeline, the camera or Explode ("Resume tour"
  continues). A tour lasts well under two and a half minutes. Previous, Next and Exit are on the caption.

**The Close-up window** (the **Close-up** button beside the remnant, or View > Close-up). It follows the main window's
outcome, observer, run and timeline, and can sit on a second monitor. The view is on the left; the numbers, each with its
confidence and source, and the notes are on the right; its own View menu holds the switches below. "Camera where no telescope
could be": from Earth every object here is far too small to picture.

- **Before 1 ms:** the iron (or O-Ne-Mg) core before it collapses. **The first second**, labelled "From simulations": the
  bounce, the shock stalling 100–200 km out, the gain region, the sloshing (SASI) and the proto-neutron star, until the
  shock revives (or never does). Only SN 1987A's neutrinos have ever been seen of this. Inside the unexploded star the
  surroundings are a dim haze.
- **A neutron star**, contracting to 12 km in the first minute, cooling, spinning down. Its light is bent by its gravity
  (exact for a non-spinning star), its colour is its temperature (tone-mapped), and its field lines, light cylinder and
  radio beams are drawn. The spin is shown at most one turn in 2 s ("Shown 60× slower" for the Crab), so nothing flashes.
  View > Cutaway opens it: the crust, the neutron drip line, nuclear pasta and the core, to scale, with a magnified strip
  of the crust. The pulse trace at the bottom marks each beam crossing Earth; View > Sound plays the real pulse rate as
  clicks (off by default; nothing depends on hearing it). The kick moves the star, in both windows.
- **A magnetar**: its stronger field and its rare giant flares (drawn at random from the run number: Re-roll redraws
  them), each a single modest brightening, also shown as a spike on the gamma-ray panel's burst tab.
- **A black hole** bends the main view's real scene behind it (general relativity, traced ray by ray). Its shadow,
  photon ring, horizon and, once it has settled and spins, the flattened Kerr shadow; View > Grid adds a bent measuring
  grid with the horizon, photon orbits and ergosphere labelled. A collapsar has its accretion disk (inner edge at the
  ISCO) and jets. A newborn hole is drawn growing and settling; its spin fades in.
- **No compact remnant** (a Type Ia, a pair-instability supernova, a white dwarf): the window says why.
- **View > Light-bending detail** (Auto, Full, Half, Quarter, remembered): Auto keeps the frame rate up on slower graphics.
- The guided tour has Close-up stops (bounce, stall, revival, the crust, the pulses, the black hole forming and settling,
  a giant flare); it opens the window and brightens its outline at those stops.

**View menu.** Instruments, Outcome details, Preset info, **Distance rings** (on or off), **Cutaway** (on or off) and
**Quality**: High (200,000 particles), Medium (100,000) or Low (50,000). Quality steps down by itself if the 3D view
renders slower than 50 frames per second (measured on the GPU); a choice in the menu starts it over, and it never steps
up by itself. Without GPU timer queries it stays where you set it.

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

**The Close-up's checks (Project 3, spec §1 criteria 2 and 3).** Each is a unit test (`Project3ValidationTests`).

| Check | Exact or observed | Lab | Result |
|---|---|---|---|
| Non-spinning shadow radius | √27 GM/c² (exact) | the ray tracer's edge within 0.5% | Pass |
| Kerr shadow outline, spin 0.9, side-on and from 17° | Bardeen 1973's analytic curve | the tracer within 1% of the shadow radius | Pass |
| ISCO and ergosphere | Bardeen, Press & Teukolsky 1972 | the formulas, the disk's inner edge and the drawn outline | Pass |
| Neutron star's visible surface | the exact Schwarzschild integral (75% for 1.4 M☉, 12 km) | the tracer within 0.01 | Pass |
| Crab's period and light cylinder | 33.39 ms; c P / 2π ≈ 1,600 km | 33.4 ms at its 1991 age; 1,594 km | Pass (consistency check, as above) |
| M87*'s ring | 42 ± 3 µas (EHT 2019) | 39.7 µas from the stellar-dynamics mass | Pass |
| Sgr A*'s shadow | 44.2–53.8 µas (EHT 2022, VLTI prior) | 53.3 µas | Pass (but see #55 below) |
| SN 1987A's neutrinos | 2–3 × 10⁵³ erg; 11 events in 12.4 s at Kamiokande-II | 3.0 × 10⁵³ erg; 18.4 expected, 99% of them in 12.4 s | Pass (count high, as above) |
| Cas A's neutron star, seen from afar | about 1.4–1.5 million K at 340 yr (Heinke & Ho 2010, redshifted) | about 1.8 million K | **Miss, disclosed** (#54) |

### Known misses

These are places where a preset's lab result visibly disagrees with what was observed. They are disclosed, not tuned
away. Each is also stated in that preset's own summary in the app.

| Preset | What was observed | What the lab gives | Why |
|---|---|---|---|
| SN 2008D | An X-ray flash of about 400 s and 2e46 erg. Swift caught it. | A bare-star flash of about 2 s and about 200x less energy. The lab's Swift does not catch it. | The real breakout was most likely through a dense wind. The lab does not model that. |
| SN 1998bw | Radio of 45 mJy at 4.8 GHz on day 12 (Kulkarni et al. 1998) | About 3.7 mJy at 5 GHz on day 12, 9 to 12 times too faint | The lab's shock formulas are Newtonian, capped at the speed of light. The real shock was mildly relativistic (E ~1e50 erg in ejecta at Γβ ~1 to 2; Kulkarni et al. 1998, Nature 395, 663, via `docs/research/presets.md`). |
| SN 1998bw | Peak V 13.62 near day 16 (Galama et al. 1998) | V 14.1 on day 23 | A one-zone (Arnett) light curve over all 10.9 solar masses of ejecta. |
| SN 1987A | Radio peaked near 130 mJy at 843 MHz within days, and faded below detection after September 1987 | About 350 mJy at 1.4 GHz near day 18, still about 90 mJy on day 100 | The research has no wind for a blue supergiant. The lab uses a Wolf-Rayet-like wind and a red-supergiant shock speed. No single paper giving both was verified, so nothing is pinned. |
| SN 1987A | 847 keV line of 1.0e-3 ph/cm²/s over days 159-250 (SMM) | 4.6e-5 over the same days, about 22 times too faint | The lab's gamma-ray model has no outward mixing of the cobalt. The 3D view draws the observed mixing, but the gamma-ray panel does not use it (spec §9). |
| SN 1987A | No pulsar has been seen | From 10 years, the 3D view draws a pulsar with sweeping beams | The lab's pulsar model gives every neutron star one. JWST found indirect evidence of a hidden neutron star in 2024 (Fransson et al.). |
| SN 1054 | Peak roughly -4 to -7, visible in daylight for 23 days, from the historical records | Peak V about -2.0, behind dust | The lab's weak electron-capture explosion. Not tuned. |
| SN 2011fe | INTEGRAL/SPI did not detect its gamma-ray lines (Isern et al. 2013) | Not detected, but the margin is thin: at best 0.76 of the threshold | It rests on SPI's resolution (taken as 2 keV; the ratio would be about 0.93 at 3 keV) and on 0.53 solar masses of nickel (about 0.70 would reach the threshold). The limit used is preliminary. |
| SN 2011fe | B-band decline Δm15(B) = 1.10 ± 0.04 (Pereira et al. 2013) | 0.99, measured on the drawn curve | Every near-Chandrasekhar Type Ia in the lab has one light-curve shape (the nickel sets only its brightness), and its colour after the peak is a simple blend toward the Lira relation. |
| SN 2015bn | Peak 2.3 ± 0.4e44 erg/s after a 69-day rise (Nicholl et al. 2016) | 1.8e44 erg/s (0.77 of it, about 1.3σ low) at about day 62 | The published magnetar goes through the lab's one-zone diffusion, not the fit's own model. |
| SN 1998bw, SN 2015bn | Published ejecta and remnant (1998bw: 10.9 + 2.9 solar masses, Iwamoto et al. 1998) | Both exceed what the lab's star holds when it dies (11.2 and 9.3 solar masses) | The lab's star models are lighter than the real stars. The published values are kept, and a note says so. |

The 3D view has its own misses. These are stated in the view's notes: SN 1987A's rings drawn symmetric (#38), a nebula
drawn for SN 1987A though none is confirmed (#49), Cas A's reverse shock at 0.85 of the forward shock against about 0.62
observed (#46), the Crab's nebula drawn small (#47, #39) and its undetected outer shell (#31), and the hotspots' observed
timing against the drawn shock (#48). These are documented in `docs/research/scientific-inconsistencies.md` only, not on
screen:

- **#35, the slow inner debris.** The debris profile puts about 0.5 solar masses of a red supergiant's debris below
  1,000 km/s; 3D simulations put about 2 solar masses there.
- **#36, when the reverse shock reaches the centre.** The published formulas disagree by 8 to 10%; this matters inside
  the lab's 1,000-year window only for some Type Ia remnants.
- **#37, SN 1987A's nickel and progenitor.** The 3D view draws the observed mixing reach, which current single-star
  models do not produce.
- **#41, mixing in pair-instability supernovae.** None is drawn; two red-supergiant models in the research mix
  vigorously.
- **#44, the 3 to 10 year shock join.** The reverse shock moves outward through the debris across the join. Shocked
  debris stay shocked, and since the final review they are never drawn ahead of free expansion (SN 1987A's fastest
  particles were up to 1.72 times too far out at 10 years before that fix).

**The Close-up's misses (Project 3).** Each is disclosed in the Close-up's notes or tooltips unless marked "list only";
the full entries are in `docs/research/scientific-inconsistencies.md`.

- **#53, SN 1987A's disputed gravitational-wave claim.** Bar detectors reported signals in 1987 that would need about
  1,000 solar masses turned into gravitational waves. SN 1987A's preset note mentions it, marked disputed, and the
  gravitational-wave badge says "Expected".
- **#54, Cas A's neutron star reads about 20–30% warm** (1.8 against 1.4–1.5 million K seen from afar). The spec first
  had this the wrong way round (10% cool). The temperature tooltip gives both; the cooling model is not tuned, since
  that would change the X-ray panel.
- **#55, Sgr A*'s ring** comes out 2σ larger than M87's calibration predicts; the shadow is tested against the EHT's own
  shadow range instead (list only). **#56, M87's two published masses** differ by 1.8×; the test uses the independent
  one (list only).
- **#57, the lab's fast-born magnetars** spin at about 0.2 s for 1,000 years; real magnetars spin at 2–12 s.
- **#58, the cooling curve** sits between theory's two levels (list only; changing it changes the X-ray panel).
- **#60, Cas A is drawn as a pulsar,** but no pulses have ever been seen from it. **#61, its kick** is 348 km/s in the
  lab against 433 km/s measured (about 1σ).
- **#62, an O-Ne-Mg core's shock** is drawn from the iron cores' track (it revives earlier).
- **#63, the newborn black hole** is drawn growing from 2.2 solar masses; the lab has no accretion history, so the
  timing is schematic.
- **#65, the black-hole picture is a "coordinate camera's"**: close in it is a few percent larger than a real camera at
  rest would see.
- **#66, the collapsar's disk is drawn thin;** the simulation's is a thick torus. **#67, SN 1998bw's engine** stops a
  few seconds into its 23 s burst in the simulation the lab follows.
- **#68, #69, giant flares:** their rate comes from old magnetars, and their spectrum (for the gamma-ray panel's
  verdict) is assumed.
- **#70, a fallback black hole forms at 3 s,** while fallback takes minutes to hours. Both views agree on the 3 s; the
  time is labelled Speculative (list only; changing it would change the neutrino panel).

Fixed or annotated during Project 3: #59 (the beam-width coefficient), #64 (a 1% shadow error at the highest spin).

**Flagged for later (spec §10; not built, because each would change a panel or needs new research):** a neutron star's
thermal X-rays and a magnetar's persistent X-rays in the X-ray panel; a real birth-spin model for black holes (today's
spin follows the Rotation input, Speculative); the cooling model (#54, #58; the X-ray panel's curve starts at 30 years,
the Close-up's early band at about a minute); Kerr light bending for a fast-spinning neutron star (its own spin is
neglected); a later fallback black-hole time (#70); and Project 2's two flagged items (the remnant panel's ejecta profile,
and mixing in the gamma-ray model).

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
- **The 3D view is partly schematic.** Its colours, the finger shapes, the wedge's cut faces, the hotspots' fade and the
  3-to-10-year shock join are schematic, and the gas around a star with no published extent is drawn out to a made-up
  1e15 cm. The view says so in its notes. Layer masses, speeds and shock radii come from the research files
  (`docs/research/structure.md`). Known disagreements between the model, the research and observations are listed in
  `docs/research/scientific-inconsistencies.md`.
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
                     [--window <list>] [--cutaway] [--tour <stops>] [--quality <list>]
                     [--sweep <steps>] [--bench <seconds>] [--bench-tour]
```

- `--snapshot <out-dir>`: the output folder. Required.
- `--preset <list>`: preset ids, comma-separated or repeated (for example `sn1987a,sn1998bw`). An entry can carry its
  own time, `id@seconds`, in observer time (`sn1987a@86400`). `default` (or `default@seconds`) is the lab as launched;
  list it before any preset. Add `+densecsm` to an entry to turn on the lab's dense-gas input after the preset loads,
  or `+cutaway` to turn on the 3D view's cutaway for that entry only, or `+tour` to capture `--tour`'s stops for that entry
  only (the other entries are then captured plainly). Without this option the lab's defaults are used.
- `--time <seconds>`: the observer time for every preset that has no `@seconds`. Positive number.
- `--size <WxH list>`: the main window size, for example `1366x768,1920x1080`. Default 1366x768.
- `--window <list>`: which windows to render: `main`, `main-advanced`, `instruments`, `instrument-tabs`, `details`,
  `presetinfo`, `closeup`. Default `main`. For a magnetar with giant flares, `instruments` and `instrument-tabs` also
  capture the gamma-ray panel's burst tab (`instruments-burst…png`).
- `--cutaway`: turns on the 3D view's wedge cutaway for every capture.
- `--tour <stops>`: starts the guided tour of each preset (or of each `+tour` entry, if any has it) and captures the requested windows at each listed stop
  (`0,3,last`; stop 0 is the first). The caption, cutaway and instrument highlight are those of that stop. Nothing ticks
  the tour, so the picture is the stop itself.
- `--quality <list>`: the particle quality, `high`, `medium` or `low` (comma-separated for several; the bench then runs
  each at every size). The automatic step-down is off for snapshots and benchmarks. Default High.
- `--sweep <steps>`: loads every preset in the catalog and steps the timeline through that many log-spaced times from
  1 ms to 1,000 years (2 to 2000), with the 3D view and Instruments window running. Any exception, panel "Model error",
  3D-view failure, or model error that the 3D scene or the timeline markers caught for themselves goes to `sweep.log`.
  The exit code is 1 if there was a problem.
- `--bench <seconds>`: plays the whole timeline once over that many seconds (above 0, at most 600) with the main and Instruments
  windows open. It writes the frame rate, the 3D view's frame rate, the GPU name and the mean GPU render time (the quality
  governor's own measurement) to `bench.log`.
- `--bench-tour`: with `--bench`, plays the guided tour instead of the plain timeline, once for each listed preset at each
  size and quality (`default` is the lab as launched). The tour sets the length of the run; the seconds only cap it.
  `bench.log` then also gives the 3D draw rate while travelling and while dwelling on a stop.

The Close-up (Project 3):

- `--closeup`: opens the Close-up window (off-screen) before the captures, so `--bench` measures both 3D views and
  `--sweep` also counts the Close-up's own model errors (and then plays 14 of the lab's own outcomes after the presets).
- `--window closeup`: captures it (its GL frame read back), with extra captures of a neutron star's light cylinder
  (`-wide`) and a collapsar's disk (`-disk`), and a moving view (`-moving`). An entry's `+cutaway` also opens the
  Close-up's cutaway; `+grid` turns on its grid.
- `--closeup-size <WxH list>`: the Close-up's sizes for its captures (default: its own 900x640).
- `--bench-preset <list>`: with `--bench`, plays the whole timeline once for each listed preset (`default` is the lab).
- `--bench-detail <auto,full,half,quarter>` and `--bench-camera <still,still-forced,orbit>`: the Close-up's light-bending
  detail and camera in each bench run (still is drawn on demand, as the app; orbit turns the camera 30° a second). With
  either, each preset and size also gets a run with the Close-up hidden, before and after.
- `--bench-baseline`, `--bench-spin <a>` and `--lab-mass <M>`: Task 4's diagnostics (the per-frame cost's baseline runs,
  a black hole's spin for the bench, and the lab's initial mass for `default` entries).

An example, one launch for several checks:

```
SupernovaLab.App.exe --snapshot out --preset sn1987a@1e7,sn1998bw@86400 --window main,instruments --sweep 54 --bench 20
SupernovaLab.App.exe --snapshot out --preset default,sn1987a --tour 0,2,last --size 1100x700
SupernovaLab.App.exe --snapshot out --preset sn1987a@86400+cutaway --quality medium
SupernovaLab.App.exe --snapshot out --preset default,sn1987a --size 1366x768,1920x1080 --bench 20 --bench-tour
SupernovaLab.App.exe --snapshot out --sweep 40 --closeup
SupernovaLab.App.exe --snapshot out --preset sn1054@1e6+cutaway --window closeup --closeup-size 900x640,1366x768 --bench 30 --bench-preset sn1054,n6946bh1 --bench-detail auto --bench-camera still,orbit
```

The windows are placed off-screen. Tooltips cannot be rendered this way, so they are checked by unit tests instead.

Performance measurements are in `docs/perf-2026-11.md`.

## Sources

The explodability table comes from the published per-model data of Sukhbold et al. 2016 (MPA Garching archive). Its
provenance is in `tools/explodability/README.md`. Every formula and constant is cited in `docs/research/`.
