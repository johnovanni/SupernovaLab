# SupernovaLab — Design (Project 2: the 3D showpiece)

**Date:** 2026-11-12
**Status:** Designed with the owner section by section (2026-11-12); each section was approved. Written for the owner's
review before the implementation plan.
**Builds on:** Project 1 (`2026-11-11-supernovalab-design.md`), merged into `main` at 4a3bea5. Everything in that spec
still holds unless this one says otherwise. In particular: the research files are normative, every number carries a
confidence label and a source, misses are disclosed and never tuned away (ruling R47b), and visual checks use the app's
own `--snapshot`, `--bench` and `--sweep`, never screen capture (R19).

## 1. Purpose

Project 1's 3D view is deliberately simple: the camera auto-frames the debris, so the picture stays the same size and
the growth shows only in the scale bar. Project 2 replaces it with a showpiece, in the same place in the main window,
on the same timeline, driven by the same physics core.

### What the owner wants someone to come away with, in priority order (agreed 2026-11-12)
1. **Scale and time:** how a star-sized blast grows into a remnant light-years across. Seen, not just read.
2. **Inside the explosion:** the element layers, the mixing, the shocks, and how each type differs.
3. **Its surroundings:** the debris hitting the star's wind, dense shells, SN 1987A's rings, the swept-up remnant.
4. **Spectacle:** glow, bloom, the breakout flash. Where spectacle and accuracy conflict, accuracy wins.

Plus a **guided tour** for every explosion, alongside free play.

### Ground rules
- **Project 2 draws; it does not change the physics the instrument panels use.** New physics (layer structure, mixing,
  shock structure, nebula growth) feeds the 3D view only. Anything that would change a panel (for example, using
  mixing in the gamma-ray model) is a separate, flagged item (§9).
- **Schematic is labelled.** Shapes the models do not compute (the form of mixing fingers, the wedge's colours) are
  labelled schematic in the overlay, as in Project 1. Their *sizes and reach* come from the research.
- **The view can never contradict a panel.** Brightness, colour temperature, radii and remnant come from the same
  models the panels use; `ExplosionScene` stays the single source of what is drawn at each moment.

### Success criteria
1. At the default playback speed, the debris visibly grow on screen, and the camera holds still for at least two-thirds
   of each pull-back cycle (§2.2).
2. For every outcome type, the drawn particle masses add up to the outcome's ejecta mass, and each element layer's
   mass matches the outcome's (within 1%).
3. Mixing reach, shock radii, nebula radius and ring geometry fall inside the ranges in `docs/research/structure.md`.
4. Every outcome type, and every preset, gets a guided tour that runs start to end with no error, and every number in
   a caption equals the model's value.
5. On a recommended system (Intel Iris Xe class), the 3D view runs at ≥ 50 fps at 1366×768 with the panels open, as in
   Project 1; the 1920×1080 rate is measured and reported.

## 2. Scale and time

### 2.1 Distance rings
- Circles facing the camera, centred on the explosion. A camera-facing circle of radius R matches the outline of a
  sphere of radius R, so when the shell's edge touches a ring, the shell has that radius.
- Values on a 1-2-5 ladder in the scale bar's units, switching where it does: km below 1 AU; AU from 1 AU; light-years
  from 10,000 AU; parsecs from 1 pc (`ScaleBar`). Each ring is labelled ("50 AU", "2 light-years").
- Three or four rings at a time. A new ring fades in at the edge as the camera pulls back; a ring fades out when it
  shrinks below about 5% of the view's height.
- They are a measuring aid, not part of the supernova: thin, grey, switchable in the view menu. They replace the scale
  bar; the radius readout stays.
- Not real landmarks (planet orbits and the like), at the owner's request: landmarks are not part of the supernova.

### 2.2 The director camera
- **Hold:** the camera keeps its scale while the framing diameter (of the debris, or the star, or the gas around it,
  whichever Project 1 frames; Project 1 holds it at 60%) grows from about 22% to 90% of the view's height. This is
  where the growth is seen.
- **Step:** when the framing diameter reaches 90%, the camera eases back over about 0.3 s of real time until it is at
  about 22% again: a step of about 4×. The step lands so that the outermost ring is a 1-2-5 value.
- **Why these numbers:** Project 1's default playback speed is 1/3 decade of time per second
  (`TimelineViewModel.DefaultDecadesPerSecond`; corrected 2026-11-12 from an earlier "13.5 decades in 20 s", which was
  the `--bench` speed). While the debris coast, the radius grows in proportion to time, so a 4–5× step takes about
  1.8–2.1 s of playback; a 0.3 s ease leaves the camera still for over 80% of each cycle, and about two-thirds even at
  the bench's 0.675 decades per second (success criterion 1). Slower playback, and the tour's slow-downs, give longer
  holds. The exact values are tuned against criterion 1.
- **Backwards:** scrubbing back steps in the same way, with hysteresis so the camera does not flicker at a boundary.
- **Jumps:** a click on the timeline, a new preset or a new outcome re-frames at once, without easing.
- **Taking over:** the wheel zooms and freezes the scale, as in Project 1, so the debris can run off-screen; dragging
  rotates and leaves the director running; Home hands the camera back to the director.
- **What it looks like** (a debris edge at 5,000 km/s, before it slows): day 1, about 3 AU; month 1, about 90 AU;
  year 1, about 6 light-days (about 1,000 AU); a century, 1–2 light-years, by then slowing down.

## 3. Inside the explosion

### 3.1 Layers
- About 200,000 particles (§7.2 for the quality setting). Each is given a mass coordinate, an element layer and a
  velocity when the outcome is built.
- Velocities come from a published ejecta density profile (a flat inner core and a steep outer power law), scaled to
  the outcome's energy and ejecta mass and truncated at the speed that encloses 99.9% of the mass (plan ruling P1).
  That edge runs 1.7–2.0× ahead of Project 1's uniform-ball edge in `EjectaKinematics`, as its own note says the
  fastest layers do. The radius readout keeps Project 1's value, and the director frames whichever is larger
  (`structure.md` §S1 and correction C8; inconsistency #32). Corrected 2026-11-12: this line earlier asked for the
  outer edge to agree with `EjectaKinematics`, which no published profile allows.
- Layer masses come from the outcome: nickel (and iron), then silicon and oxygen (the CO core), helium, hydrogen
  envelope. Stripped stars lack the outer layers (Ib: no hydrogen; Ic: neither).
- **Type Ia:** stratified white-dwarf debris: a nickel core, then silicon and other intermediate-mass elements, then
  unburned carbon and oxygen outside. No star, no remnant.
- **Pair instability:** a large nickel core, as the outcome gives.
- **Failed supernova:** no debris, only the collapse to a black hole. If the research confirms the published weak
  envelope ejection (Lovegrove & Woosley 2013), it is drawn and labelled, with its mass and speed from the research.

### 3.2 Mixing
- Rayleigh–Taylor fingers at the layer boundaries: nickel plumes reaching out into the helium and hydrogen, hydrogen
  falling inward. Built into the particle velocities when the outcome is made.
- The fingers' *shapes* are schematic and labelled so. Their *reach* (the fastest nickel, the slowest hydrogen) comes
  from published 3D simulations and SN 1987A's observations, per progenitor type, in `structure.md`. For SN 1987A that
  is nickel to about 3,000–4,000 km/s and hydrogen down to under 1,000 km/s, to be confirmed by the research.
- Mixing is drawn only. Inconsistency #13 (SN 1987A's 847 keV line 22× too faint for want of mixing) stays open; using
  mixing in the gamma-ray model is the flagged item in §9.

### 3.3 The shock inside the star
- Between core bounce and breakout, the cutaway shows the shock front crossing the layers. Its position comes from the
  breakout model's shock speed and timing, so it reaches the surface exactly when Project 1's breakout does.
- The first second at the core (the stalled shock, the proto-neutron star) is too small to see at the star's scale;
  it belongs to Project 3.

### 3.4 Shocks after breakout
- Once the debris meet surrounding gas, the forward shock, the contact surface and the reverse shock are thin
  translucent surfaces. Radii from Truelove & McKee (1999) and Tang & Chevalier (2017), the source of the blast wave
  Project 1 uses (earlier mislabelled Leahy & Williams 2017: `structure.md` C9).
- The drawn shocks follow solutions consistent with the drawn steep-ejecta profile and with the panels where a panel
  models the same shock (ruling R-P2-24, `structure.md` §S4):
  - **First years** (outcomes with a wind): the forward shock is the radio model's own shock (R = v_s t). The contact and
    reverse shock come from Chevalier's (1982) wind ratios for the class's density slope.
  - **From 10 years:** all three come from Truelove & McKee (1999) and Tang & Chevalier (2017) for the class's steep
    ejecta (n = 7 Type Ia, 12 red supergiant, 10 otherwise).
  - **Between 3 and 10 years:** each radius blends smoothly from the first to the second.
  - **Readout:** from 10 years the radius readout is this forward shock, and a note gives Project 1's uniform-ejecta value,
    which the remnant panel keeps (up to about 2.6× smaller at 10 years for a red supergiant; correction C10;
    inconsistency #33).

  Corrected 2026-11-12: this line first said the forward shock "must agree" with Project 1's radius, then (ruling
  R-P2-10) drew Project 1's n = 0 radius; the research and Task 8 showed both misplace the shocks.
- Particles the reverse shock has passed pile up behind the contact surface and glow as hot gas. A particle once
  shocked stays shocked (the reverse shock's Lagrangian speed is a running minimum, ruling R-P2-24).
- While the debris coast, positions are velocity × time (homologous expansion), computed on the GPU. After that,
  positions follow the shock solutions.

### 3.5 Colour, and the cutaway
- The outside keeps Project 1's colour, from the light curve's colour temperature: what an eye would see.
- A **wedge cutaway** (a toggle; the camera can swing it to any angle) removes a quarter of the sphere. Its cut faces
  show the element layers in schematic colours, with a small legend: hydrogen, helium, oxygen and silicon, nickel and
  iron (and, for a Type Ia, carbon and oxygen, silicon, nickel). The mixing fingers show on the cut faces.
- Chosen with the owner over a slice plane alone or translucent shells (which turn muddy with this many particles).

## 4. The surroundings

- **The star's wind:** a faint haze whose density falls as 1/r², from the wind the lab already uses (A_*). A red
  supergiant's slow, dense wind looks thicker than a Wolf–Rayet star's fast, thin one. Where the forward shock meets
  it, it lights up, in step with the lab's circumstellar interaction model (the one behind the radio and X-ray panels).
- **Dense shells (IIn, SN 2023ixf-like):** a dense shell near the star that flares when the debris hit it, in step
  with the light curve. Extents from the research by class; where it finds none, the Project 1 example (10¹⁵ cm, from
  SN 2023ixf) stays and is labelled, as now.
- **SN 1987A's rings:** the equatorial ring (radius about 0.6 light-years, tilted about 43° to our line of sight) and
  the two outer rings, for SN 1987A-like presets only: it is observed geometry, pinned by the preset, not something
  the lab predicts. Hotspots from about day 3,000; peak about day 8,000; then fading (signals §6.3). Geometry to be
  confirmed by the research.
- **The remnant era:** interstellar gas swept into a thin shell, drawn brighter at the edge (limb brightening, as in
  Tycho's remnant). Radius from Project 1's blast-wave model.
- **A pulsar wind nebula,** when a pulsar forms: a bubble inflating inside the debris, with its growth law from the
  research (about R ∝ t^1.2 while the debris coast). The Crab preset at 1,000 years would otherwise show only a blast
  wave. The Crab's outer shell has never been detected; this goes on the inconsistencies list.

## 5. Spectacle

- **HDR and bloom:** the scene renders to a high-dynamic-range target; particles add their light; a tone map and a
  light bloom pass (a short blur chain) make bright regions glow.
- **Brightness follows the light curve**, as in Project 1 (logarithmic in the modelled luminosity).
- **Shock breakout:** a brief blue-white flash, its strength and colour from the breakout model. For a red supergiant,
  breakout outshines the later peak (mostly in UV and X-rays), so it may blow out the view for a moment. That is
  intended.
- **Gamma-ray burst jets:** brighter than in Project 1, and they bloom.
- **The star before the explosion:** limb darkening, so it reads as a sphere.
- **Background:** plain black. The rings are the reference; a made-up starfield would be decoration the physics did not
  produce. A real sky behind each preset is a later option (§10).

## 6. The guided tour

- A **Tour** button beside free play. A tour is a list of stops: a time, a camera move, the cutaway on or off, a
  caption, a dwell time, and optionally an instrument to highlight. The timeline plays between stops, slowing near
  events; the director camera and the rings work as in free play.
- **Every outcome gets a tour,** built from its own events (the timeline's event markers): bounce, the neutrino burst,
  breakout, peak light, the radioactive tail, the remnant. The cutaway opens while the shock crosses the star, and the
  camera turns slowly to show the mixing.
- **Presets add history:** a few stops carry hand-written lines, for example "23 February 1987, 07:35 UT:
  Kamiokande-II records 11 neutrinos". These lines are data in Core (beside the presets), each with its source.
- **Caption numbers come from the models** through templates, so a caption can never contradict a panel.
- **Instruments:** the Instruments window follows the timeline anyway. To draw attention to one, the tour brightens
  the outline frame of that instrument's panel, and of its tile in the instrument strip (the owner's choice), instead
  of pointing at it in a caption.
- **Controls:** pause, next, previous, exit. Touching the camera or the timeline pauses the tour and shows "Resume
  tour". A tour runs about one to two minutes.

## 7. How it is built

### 7.1 Research first
A new normative research file, `docs/research/structure.md`, with confidence tags and sources as in Project 1:
ejecta density profiles; layer positions by type (core collapse and Type Ia); mixing reach from 3D simulations and SN
1987A; Truelove & McKee shock radii; wind and dense-shell extents; SN 1987A's ring geometry and timing; pulsar wind
nebula growth; failed-supernova envelope ejection. Any mismatch found goes on `docs/research/scientific-inconsistencies.md`.

### 7.2 Code, following Project 1's split
- **Core, new `Structure/`:** the ejecta structure (layer masses to velocities, mixing statistics), the shock structure
  (forward, contact and reverse radii over time), the gas around the star, the pulsar wind nebula, SN 1987A's rings.
  Pure functions of the outcome and time, with no OpenGL. Preset tour lines live beside the presets.
- **Rendering:**
  - a particle field on the GPU: each particle's velocity, layer and seed uploaded once per outcome; positions
    computed in the shader from the time and the shock solutions;
  - shock and nebula surfaces, the wind haze, the distance rings, the wedge clip;
  - the HDR, bloom and tone-map pipeline.
- **App:** the director camera and the tour director as plain logic classes, testable without a screen; the tour's view
  model and overlay; the panel-outline highlight.
- `ExplosionScene` stays the single source of what is drawn at each moment and gains the new fields.

### 7.3 Testing
- Structure: particle masses add up to the ejecta mass; each layer's mass matches the outcome; mixing reach inside the
  research ranges; shock radii against Truelove & McKee's tables; the forward shock against Project 1's blast wave;
  the nebula radius against its growth law.
- The director: hold, step, hysteresis, re-frame on a jump; criterion 1 at the default playback speed.
- The ring ladder: values, units and switch points agree with `ScaleBar`.
- Tours: every outcome type and every preset gets a valid tour; every caption number equals the model's value.
- Visual checks: `--snapshot` frame series per preset; `--bench` for the frame rate; `--sweep` for errors. No screen
  capture.

### 7.4 When things fail
- OpenGL missing or below 4.1: as in Project 1.
- No HDR render target: a no-bloom path, as Project 1 draws.
- A quality setting (200,000, 100,000 or 50,000 particles), stepping down automatically when the frame rate stays under
  50 fps.
- A model error: as in Project 1, the 3D view shows the error in its notes and the panels carry on.

### 7.5 Order of work (each milestone leaves a working app)
1. The research file. Alongside it, the rings and the director camera (no new physics), and a quick performance test of
   200,000 particles with bloom on the Iris Xe.
2. Layered particles, mixing, the wedge cutaway, the shock crossing the star.
3. Shocks, surroundings, the pulsar wind nebula, SN 1987A's rings.
4. HDR, bloom, the breakout flash, limb darkening.
5. Guided tours, with the panel highlight.

Built on the `project2` branch, subagent-driven as Project 1 was, under the owner's standing instruction (reversible
calls made and reported later). Pushing to GitHub only when the owner asks.

## 8. Out of scope for Project 2
- The compact remnant close up: the neutron star, pulsar beams, magnetar and black hole bending light (Project 3).
- The first second at the core (Project 3).
- Light echoes (they need a dust model and depend on where the observer is).
- A Type Ia's companion star (whether one exists is unsettled).
- Narration audio and video export (the snapshot option stays).
- A real star background per preset.
- Asymmetric explosions beyond jets and mixing, as in Project 1.

## 9. Flagged for later (physics changes Project 2 does not make)
- **The remnant panel's ejecta profile.** Project 1's remnant model uses uniform-ejecta (n = 0) solutions, which lag
  steep-ejecta remnants early (2.6× at 10 yr for n = 12; C10, #33). Using the class-n solution would change the remnant
  panel.
- **Mixing in the gamma-ray model.** Project 2 draws mixing; using it in the gamma-ray line model would likely narrow
  inconsistency #13 (SN 1987A's 847 keV line). It changes a panel, so it is a separate decision.

## 10. Decisions made with the owner (2026-11-12)
- **D1:** The showpiece's priorities: scale and time, inside, surroundings, spectacle (in that order).
- **D2:** Free play plus a guided tour for every explosion.
- **D3:** A wedge cutaway with the mixing shown on its cut faces.
- **D4:** Particles plus a glow pass (over volume rendering, or particles alone).
- **D5:** Distance rings in the scale bar's units, not real landmarks (the owner's idea).
- **D6:** A director camera that holds while the debris grow and then steps back (the owner's idea); the step about 4×
  with a 0.3 s ease, tuned against success criterion 1.
- **D7:** The tour highlights an instrument by brightening its panel's outline frame (the owner's idea).
- **D8:** Project 2 draws only; physics changes that affect panels are flagged separately.
