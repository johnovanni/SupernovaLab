# SupernovaLab — Design (Project 3: the remnant close-up)

**Date:** 2026-11-15
**Status:** Designed with the owner, section by section, on 2026-11-15. Each section was approved. Written for the
owner's review before the implementation plan. My own calls are marked **[my call]** and listed in §13.
**Builds on:** Project 1 (`2026-11-11-supernovalab-design.md`) and Project 2 (`2026-11-12-supernovalab-project2-design.md`),
both merged into `main` (96f8424). Everything in those specs still holds unless this one says otherwise:
- The research files are normative.
- Every number carries a confidence label and a source.
- Misses are disclosed and never tuned away (ruling R47b).
- Shapes the models don't compute are labelled schematic.
- Visual checks use the app's own `--snapshot`, `--bench` and `--sweep`, never screen capture (R19).

## 1. Purpose

Project 1's spec (line 29) promised a third project: "the remnant close-up (neutron star, pulsar beams, magnetar, black
hole bending light)". Project 2 deferred to it the first second at the core: the stalled shock and the proto-neutron star
(spec §3.3, §8). Project 3 delivers both, in a **Close-up window** that sits beside the main window and follows the same
timeline and the same physics core.

### What the owner wants (agreed 2026-11-15)
- **Scientific accuracy, matching observations as well as we reasonably can.** Accuracy comes first. Wherever an observed
  object can check a number, it does, and the check is a test.
- Nothing may depend on sound (the owner is hard of hearing). Pulsars are often presented as sounds. Here they
  **can** have that sound (the owner's request, §4.4), and the pulse rhythm is **always** shown on screen as well.
  Everything the sound conveys is also on screen.

### Ground rules
- **The physics core is the single source.** New physics (the core's first second, the neutron star at each moment, the
  magnetar's flares, the black hole's spin and optics) goes in Core as pure functions. The close-up and the main window
  read the same values, so they can never contradict each other.
- **Changes to Projects 1 and 2 are only the ones listed in §7.** Anything else that would change a panel is flagged
  (§10), as in Project 2.
- **Labels that say how a thing is known:**
  - The first second is marked **"From simulations"**: nothing in it is observed directly except SN 1987A's
    neutrinos.
  - Shapes the models don't compute (the churning behind the shock, the field lines' number, the disk's brightness)
    are **schematic**.
  - The close-up says **"Camera where no telescope could be"**: as seen from Earth, every object here is far too small
    to picture (§6.5).
- **Flash safety:** nothing in either view flashes or flickers faster than **3 times a second** (WCAG 2.3.1, the
  general flash threshold). That includes pulsar beams, flares and the pulse trace.

### Success criteria
1. Every outcome type (and every preset) opens the Close-up window and plays the whole timeline with no error. Outcomes
   with no compact remnant say why.
2. Every exact relativity result drawn on screen matches its formula:
   - Non-spinning: shadow radius √27 GM/c² (0.5%) and photon sphere 3GM/c².
   - Spinning: the Kerr shadow outline against Bardeen's (1973) analytic curve (1% of the shadow radius, at spin 0.9,
     seen side-on and from 17°), the ISCO (Bardeen, Press & Teukolsky 1972) and the ergosphere.
   - The neutron star's visible surface fraction against the exact Schwarzschild ray result.
3. The observed checks pass:
   - The Crab's period at its age (33.4 ms, Project 1's tolerance) and its light cylinder (≈ 1,600 km).
   - The Cas A neutron star's surface temperature as seen from afar, T_s^∞: about 1.4–1.5 × 10⁶ K at about 340 yr
     (Heinke & Ho 2010's local 2.0–2.1 × 10⁶ K, redshifted for their best-fit mass and radius; `compact.md` §C9, K1).
     - The lab's cooling model (`NeutronStarCoolingModel`, Project 1) gives about 1.8 × 10⁶ K there, **about 20–30% warm**.
       (Corrected 2026-11-15: this line first compared the lab's far-away temperature with the measured local one, and
       called the lab 10% cool.)
     - Under R47b this is **disclosed, not tuned**: inconsistency #54, and the temperature readout's tooltip says so for
       the Cas A preset. The test checks that the comparison is shown with the right figures.
     - Changing the cooling model would change the X-ray panel, so it is flagged (§10).
   - SN 1987A's neutrino energy and duration, as the neutrino panel already tests.
   - The shadow law against the Event Horizon Telescope's M87* and Sgr A* rings, scaled by mass (within the published
     uncertainties).
4. The two views agree:
   - The neutron star's position (kick drift) is the same in both windows.
   - A failed supernova's black hole forms at exactly the time the neutrino panel cuts off.
   - Pulsar beams share one rotation phase and one tilt.
5. On a recommended system (Iris Xe class), both 3D views hold ≥ 50 fps together: the main window at 1366×768 and the
   Close-up at its default size. If they can't, §2.5's fallback applies.
6. No element in either view changes brightness periodically faster than 3 Hz (tested, §8.3).

## 2. The Close-up window

### 2.1 Layout (text sketch, approved)
```
┌ Close-up: Neutron star (pulsar, 33.4 ms) ─────────────────────────┐
│                                         │ Mass        1.41 M☉  ● │
│                                         │ Radius      12 km    ● │
│         3D close-up                     │ Spin        33.4 ms  ● │
│   (distance rings, own scale)           │ Field       3.8e12 G ● │
│                                         │ Surface     1.8 MK   ● │
│   "Shown 60× slower"                    │ Kick  400 km/s, 0.4 pc ●│
│   "Camera where no telescope could be"  │ Notes ▾                │
├─────────────────────────────────────────┴────────────────────────┤
│ Pulses  ▌    ▌    ▌    ▌   real period 33.4 ms · To Earth: hidden by debris │
└──────────────────────────────────────────────────────────────────┘
```
- **The title** names the object and its state: proto-neutron star, neutron star, pulsar, magnetar, black hole
  (forming / settling / settled).
- **The readouts** (right column) change with the object (§3–§6). Each has its confidence badge and its source on hover,
  like every other number in the lab. The Notes expander holds the scene notes (the same mechanism as the main view's
  notes overlay).
- **The pulse trace** (bottom strip) appears only for a pulsar or a magnetar (§4.4).

### 2.2 Opening it, and the timeline
- It opens from **View ▸ Close-up**, and from a **Close-up** button beside the remnant in the outcome summary.
- It opens at most once, through `IWindowService.ShowCloseUp()`, like the other windows. It is non-modal and can move to a
  second monitor. Its size and position are remembered for the session. It closes with the main window.
- It has **no timeline of its own.** The main window's timeline drives it, as it drives the Instruments window. It binds
  to the same view-model instances.
- **Its menu:**
  - Grid (§6.3)
  - Field lines
  - Light cylinder
  - Cutaway (§4.2)
  - Labels
  - Focus view (F)
- **Its camera:**
  - It is centred on the compact object and moves with it (the kick, §4.6).
  - It reuses Project 2's director camera (hold, step back, Home hands control back), with its own distance rings.
    The ring ladder starts in metres, for the crust strip, then kilometres.
  - Drag rotates and the wheel zooms, as in the main view.

### 2.3 Outcomes with no compact remnant
- **A Type Ia, or a pair-instability supernova:** "No compact remnant: the star was destroyed completely."
- **No supernova, or the star becomes a white dwarf:** "This star leaves a white dwarf. The white-dwarf close-up is not
  part of the lab."
- **A Type Iax** leaves a bound white dwarf, if the outcome engine gives it one. It gets the same white-dwarf message.

### 2.4 The guided tour
- Tours gain close-up stops, built from the outcome's own events:
  - bounce;
  - the shock stalling;
  - the shock reviving (or, for a failed supernova, the black hole forming);
  - the crust forming;
  - the black hole settling;
  - the pulsar's pulses emerging;
  - a giant flare, if the run has one.
- **At the first close-up stop the tour opens the window.** If the owner closes it, the remaining close-up stops are
  skipped, and the tour carries on.
- A close-up stop brightens the Close-up window's outline frame, as the tour already does for an instrument panel
  (Project 2 D7).
- Captions follow Project 2's rules: numbers come from the models through templates. Lines begin "Observed:", "In the
  lab," or (new, §7.1) "Expected:".

### 2.5 The fallback: an inset (approach C)
- The first milestone (§8.5) measures both 3D views together, including the spinning-black-hole light bending.
- **If they can't hold 50 fps** on Iris Xe class graphics at the sizes in criterion 5 (after the reduced-resolution
  lensing of §6.6), the close-up becomes a **picture-in-picture inset** in a corner of the main 3D view. The inset has the
  same scene and readouts in a compact form.
- That decision is made at milestone 1 and reported to the owner before work continues.
- **When the Close-up window is minimized or fully hidden, it stops drawing.**
- **Milestone 1 result (owner's decision, 2026-11-15).** On Iris Xe both views held 62 fps with the light bending at
  quarter resolution and 40 fps at half. The owner chose to **keep the window and try speed-ups** (Task 4b), and to **let
  the user set the light-bending detail**, because other people's graphics will be faster or slower:
  - **Close-up menu ▸ Light-bending detail:** Auto (the governor, default), Full, Half, Quarter. Remembered between
    runs in `%LOCALAPPDATA%\SupernovaLab\layout.json`.
  - The chosen level is shown in the window's notes (for example "Light bending: half resolution (your setting)").
  - Auto keeps the governor of §6.6, stepping down to hold 50 fps.

## 3. The first second at the core

This stage applies to core collapse. t = 0 is core bounce, as on the timeline. The timeline starts at 1 ms. At the
default playback speed (1/3 decade per second), 1 ms to 1 s takes about 9 s, so no automatic slow-down is needed.

**Before t = 1 ms** (Explode not yet pressed, or the scrubber at its left end): the close-up shows the iron core, or the
O-Ne-Mg core for an electron-capture star, before collapse. Its size comes from the research (§8.1) **[my call]**.

### 3.1 What happens (all sizes and times from the research, §8.1; the figures here are placeholders)
1. **Bounce to about 0.1 s.**
   - The core rebounds and a shock forms about 10–20 km out.
   - It moves out to about 100–200 km and **stalls**, held back by the infalling matter.
   - Inside is the **proto-neutron star**: hot and swollen, about 50–80 km in radius at first.
2. **About 0.1–0.5 s.**
   - Neutrinos heat the gain region behind the shock, which churns (convection) and sloshes (the standing accretion-shock
     instability, SASI).
   - In a successful explosion the shock **revives** and moves out.
3. **0.5 s to about 1 minute.**
   - The revived shock leaves the close-up's frame. Project 2's cutaway follows it from there to breakout (`structure.md`
     §S5).
   - The proto-neutron star cools and shrinks towards the remnant's radius (12 km).
4. **A failed supernova:**
   - The shock never revives.
   - The proto-neutron star grows by accretion and collapses to a black hole at `Remnant.FormationTimeS`. That is the
     same t_BH at which the neutrino panel's signal stops (signals §1.3).
   - The collapse takes ≲ 1 ms (O'Connor & Ott 2011).
5. **A fallback black hole:** the explosion succeeds as in 2–3. The black hole forms later, at the lab's existing
   formation time.
6. **Other black-hole outcomes [my call; the research may revise]:**
   - **A collapsar (gamma-ray-burst outcome):** the code makes this a supernova whose black hole forms at
     `DirectCollapseTime`.
     - It uses the **failed track**: the shock stays stalled and the track ends at `FormationTimeS`.
     - The disk and jets then take over (§6.2), and the ~15 s spin-up runs from `FormationTimeS`.
     - The neutrino panel's curve before t_BH is accretion luminosity, so a stalled shock does not contradict it.
   - **Pulsational pair instability followed by a black hole:** the failed track.
   - **A magnetar-driven (magnetorotational) explosion:** the exploding track, labelled "a magnetar-driven explosion
     revives the shock differently (Speculative)".

### 3.2 How it is drawn
- **The proto-neutron star:** a sphere of the research's radius at each moment. Its brightness follows the neutrino
  panel's total luminosity at that moment (`NeutrinoModel`), on Project 2's logarithmic brightness rule.
- **The shock:** a translucent surface at the research's mean radius.
  - Its distortion (convection plumes, SASI sloshing) is schematic.
  - The amplitude of that distortion is from the research where it gives one, otherwise labelled schematic with no
    stated amplitude.
- **The gain radius:** a faint shell.
- **Infalling matter:** a stream of particles falling in at the research's speeds (schematic paths).
- **Labels and notes:**
  - "From simulations" on the stage.
  - The confidence is **ModelDependent** for the radii and times, and **Schematic** for the shapes.
  - A note names SN 1987A's neutrinos as the only observation of this stage.
- **The tracks for each outcome:**
  - The research gives a typical shock-radius and proto-neutron-star radius track for an exploding core and for a
    failed one.
  - If it finds a published dependence on the progenitor (for example on core compactness or remnant mass), the tracks
    follow it. Otherwise they are labelled "typical".
  - For a failed supernova, the track must end at `FormationTimeS`. The track is scaled in time to fit; the remnant's
    time is not changed.
- **Not drawn:**
  - the neutrino-driven wind;
  - neutrino flavour physics;
  - the electron-capture star's lighter core structure, beyond its size.

## 4. The neutron star

### 4.1 The star
- **Size:** the remnant's radius (`Remnant.RadiusKm`, 12 km).
- **Colour:**
  - The blackbody colour of the observed surface temperature T_s^∞ from `NeutronStarCoolingModel` (signals §8.4),
    which is blue-white.
  - A note says it shines mostly in X-rays.
  - Its brightness is tone-mapped so the surface reads. It does not show how bright it would look from any real
    distance, and a note says so.
- **Temperature readout:**
  - **Before 30 yr** (where `NeutronStarCoolingModel` starts) the close-up uses an early-cooling track from the
    research (§8.1). It runs from the proto-neutron star at about a minute to 30 yr. It is shown as a band, labelled
    ModelDependent, and joins the cooling model's band at 30 yr.
  - This track is used by the close-up only. The X-ray panel keeps its "not modelled before 30 yr", so the two do not
    disagree, and the panel change is flagged (§10) **[my call]**.
  - **From 30 to 100 yr** it is the cooling model's band, as signals §8.4 requires: the crust has not relaxed.
  - **After 100 yr** it is the model's value.
  - **Check:** the Cas A neutron star (criterion 3: about 20–30% warm, disclosed as #54).
- **Light bending at the surface:**
  - Light leaving the star follows the same Schwarzschild light paths as the black hole's optics (§6.6), so part of the
    star's far side is visible past its limb.
  - It is exact for a non-rotating star. The star's own rotation is neglected in the bending; for 33 ms it is negligible
    **[my call]**.

### 4.2 The cutaway: the layers and neutron drip (added at the owner's request)
A wedge cutaway (the Close-up menu's Cutaway switch, as in Project 2) shows the star's layers. Depths and densities
come from the research (§8.1), with these labels:
- **Atmosphere:** centimetres thick. Shown only as a labelled line.
- **Outer crust:** a lattice of nuclei and electrons.
- **The neutron drip line**, labelled with its density, about 4 × 10¹¹ g/cm³ (Baym, Pethick & Sutherland 1971; to be
  confirmed). It lies a few hundred metres below the surface.
  - Its confidence is **Established theory, observed indirectly**.
  - Its tooltip names the two lines of indirect evidence:
    - pulsar glitches, explained by superfluid neutrons in the inner crust;
    - the crust cooling of neutron stars after accretion outbursts.
- **Inner crust:** nuclei bathed in free neutrons, which become superfluid as the star cools.
- **"Nuclear pasta"** near the crust's base. Labelled ModelDependent.
- **Outer core:** neutrons, protons, electrons and muons.
- **Inner core:** labelled **"unknown"**: what lies at the centre is not settled.

Two more rules:
- **The crust is about 1 km thick, about 8% of the radius,** so the cutaway has a **magnified strip** of the crust,
  labelled "magnified ×N". The rest of the cutaway is to scale.
- **Timing:**
  - The proto-neutron star has no crust; it is hot and liquid.
  - **The crust, and the drip line with it, appear as the star cools,** at the crystallization time the research gives.
  - Until then the cutaway shows a hot, uniform proto-neutron star, labelled so.
  - The tour has a "crust forms" stop at that time (§2.4).

### 4.3 Spin, field and beams
- **The spin period:**
  - It comes from the lab's pulsar model (`PulsarModel`, n = 3, signals §8.1), and changes over the years.
  - **Check:** the Crab preset reads 33.4 ms at its age (criterion 3).
- **The drawn rotation is illustrative and labelled:**
  - **One rule for both views:** drawn period = max(real period, 2 s). The 2 s is the main view's existing
    `ExplosionScene.ShownPulsarPeriodS`.
  - The label reads "Shown N× slower" (N = drawn period ÷ real period; about 60× for the Crab), or "Shown at real
    speed" for a period of 2 s or more (a magnetar at seconds).
  - With two beams, nothing crosses Earth more than once a second (1 Hz, under the 3 Hz flash limit).
  - It is independent of playback speed. It keeps turning while the timeline is paused, as Project 2's beams do.
  - **It shares one phase with the main view's beams** (criterion 4).
- **The magnetic field:**
  - Dipole field lines, the accepted shape close to the star.
  - The number of lines drawn is schematic (more for a stronger field).
  - The lines that reach the **light cylinder** (R_L = cP/2π, about 1,600 km for the Crab) open out. The light
    cylinder is a labelled ring (switchable).
- **The magnetic tilt:**
  - The angle between the magnetic and rotation axes is usually unknown, so it is schematic: Project 2's 45°, so both
    views match.
  - If the research finds a published inclination for a preset's pulsar (the Crab), that preset uses it in both views
    **[my call]**.
- **The beams:**
  - They come out along the magnetic poles.
  - Their half-opening angle is from the research's published relation with period (`compact.md` §C4: ρ = 5.6° P^(−1/2), Biggs 1990; #59).
- **The direction to Earth:**
  - An arrow labelled "To Earth".
  - The lab's existing "Jet or beam toward Earth" checkbox decides whether a beam crosses it.
  - **The geometry contract [my call]:**
    - The spin axis is the scene's z axis in both views. Earth is a scene direction at angle ζ from the spin axis, set
      per outcome by `Magnetosphere` from the checkbox.
    - **Checkbox on:**
      - A pulsar gets ζ = the magnetic tilt α, so the beam centre sweeps Earth.
      - A burst's jets get ζ = 0 (along the jet).
    - **Checkbox off:** ζ is placed outside the beam cone (|ζ − α| > ρ + 10°).
    - The azimuth of Earth about the spin axis is fixed (schematic).
    - The main view draws no Earth arrow. Its beams use the same ρ(P) half-width as the close-up (§7.2), so both views
      agree on whether a beam crosses Earth.
- **When the beams exist:**
  - The close-up draws the beams from the time the research gives for the magnetosphere to form.
  - The main view keeps its rule: beams drawn once the pulses escape the debris (`PulsarModel.EmergenceTimeS`).
  - The close-up's note explains the difference **[my call]**.

### 4.4 The pulse trace
- A strip under the view records a pulse each time a beam crosses the Earth arrow, as a radio telescope's trace would.
  - It shows the pulses at the drawn rate, in step with the drawn beams.
  - It is labelled with the real period.
- **Until the pulses escape the debris** (`PulsarModel.EmergenceTimeS`), the trace reads "To Earth: hidden by debris", in
  step with the radio panel.
- With the beam not toward Earth, it reads "Beam misses Earth".
- **The pulsar's sound (added at the owner's request).** Pulsars are usually presented as radio-telescope pulses
  played as sound, and the lab can do the same:
  - **What it plays:**
    - One short click per pulse at the **real** period, as in the usual radio-astronomy recordings.
    - A slow pulsar ticks. The Crab's 30 a second blurs into a buzz. A millisecond pulsar becomes a tone. A magnetar
      gives one click every few seconds.
    - The clicks are synthesised in code, so no recordings are bundled and no licence is needed.
    - The click's shape is schematic.
  - **When it plays:** only when a radio telescope on Earth would receive pulses, so it agrees with the pulse trace and
    the radio panel:
    - the pulsar's beam is toward Earth;
    - the pulses have escaped the debris;
    - the timeline is playing.
    - Otherwise it is silent, and the trace's on-screen words say why ("hidden by debris", "beam misses Earth").
  - **The sound and the picture keep separate rates, both labelled.**
    - The sound plays at the real rate. Sound has no flash limit, and the real rate is the accurate one.
    - The picture stays slowed for flash safety (§4.3).
    - The trace reads, for example, "Sound: real rate, 30 a second · Picture: shown 60× slower".
  - **The switch:**
    - Close-up menu ▸ Sound, **off by default** **[my call]**: the lab has never made a sound, and a sudden noise
      shouldn't surprise anyone.
    - The pulse trace shows a speaker symbol with its state (on, or crossed out) and the volume, so you can see whether
      it is sounding.
  - **Nothing depends on it:** the period, the rhythm and every reason for silence are on screen.
  - **Implementation:** a small audio-output library (NAudio, MIT) or Windows' own audio API. The builder decides,
    keeping to permissive licences and no network.
  - **Tests:** the click schedule equals the model's period at each time, and it is silent exactly when the trace says
    "hidden by debris" or "beam misses Earth".

### 4.5 Readouts
- mass (gravitational);
- radius;
- spin period, with Ṗ;
- surface field;
- surface temperature (or the band);
- age;
- kick speed, and distance from the explosion's centre;
- for the cutaway, the drip density.

### 4.6 The kick
- The neutron star moves at the remnant's kick speed (`Remnant.KickKmS`): at 400 km/s, about 0.4 pc in 1,000 yr.
- **The direction:**
  - A preset uses the measured direction where the research finds one (for example Cas A's neutron star and the Crab
    pulsar).
    - Measured proper motions are 2D on the sky. They are placed in the plane perpendicular to the Earth direction
      (§4.3), with the position angle measured from a fixed scene "north" (schematic).
    - The unknown line-of-sight part is set to zero and the readout says so **[my call]**.
  - Otherwise the direction is drawn from the run's seed. It changes with Re-roll and is labelled as such. This is
    display randomness, not physics, under Project 1's D4.
- **Both views draw the same position** (§7.2, criterion 4).

## 5. The magnetar

- **The star:** the same neutron star (§4), with the outcome's magnetar field (10¹⁴–10¹⁵ G).
  - The field lines are denser (schematic).
  - The field readout has a comparison, about a trillion times a fridge magnet (exact figure in `Comparisons`, from the
    model's value).
- **Spin:**
  - From the lab's magnetar spin-down convention (Kasen & Bildsten 2010's t_p, as `PulsarModel` already uses for
    magnetars).
  - Observed magnetars spin at about 2–12 s (`compact.md` §C6, K5). Only the lab's slow-rotation magnetar reaches
    seconds within the timeline; its fast-born magnetars stay near 0.2 s at 1,000 yr. That mismatch is disclosed on
    screen as #57, not tuned. (Corrected 2026-11-15: this line first said every magnetar reaches seconds.)
  - At real periods of a second or more, the rotation is shown at real speed (§4.3).
- **Field decay:** if the research gives a decay law with a timescale inside 1,000 yr, the field readout follows it.
  Otherwise the readout notes the decay is not modelled.
- **Giant flares:**
  - They are rare display events, like the lab's neutrino events: Poisson-drawn from the run's seed, changed by Re-roll,
    at the per-magnetar rate the research derives.
  - That rate is anchored on the three seen from our galaxy and the LMC: 5 March 1979 (SGR 0526−66), 27 August 1998
    (SGR 1900+14) and 27 December 2004 (SGR 1806−20).
  - **Each flare is one flash with no flicker:** a rise and a fade over about half a second of playback, brightness
    capped.
  - Each flare appears on the timeline as an event marker. Its caption compares it with the 2004 flare.
  - **It also appears on the gamma-ray panel** (§7.3), so the views agree.
  - The flare's rate and energy are **Speculative**; the observed flares are the checks.

## 6. The black hole

### 6.1 What you see
- **The shadow:** the dark region where light falls in, about 2.6 times the horizon's size for a non-spinning hole.
  - Its size comes from general relativity and the lab's black-hole mass. For example, a 10 M☉ hole has a horizon about
    59 km across and a shadow about 154 km across.
  - The horizon itself is never visible. The grid's labels say where it is.
- **The photon ring:** a thin bright ring at the shadow's edge, made of light that has looped round the hole.
- **Readouts:**
  - mass;
  - horizon diameter;
  - shadow diameter;
  - spin, once settled (§6.4);
  - kick;
  - apparent size from Earth (§6.5).

### 6.2 What it bends: its real surroundings (the owner's choice A)
What surrounds the camera at each moment, as the lab's scene gives it, is rendered from the remnant's position and bent
exactly as relativity says:
- **The first seconds:**
  - The collapsing core around a failed supernova's hole.
  - For a gamma-ray-burst outcome, also the **accretion disk** and the base of the jets.
    - The disk is labelled schematic. Its inner edge is at the ISCO (exact for the hole's spin), its outer extent comes
      from the research, and its brightness is schematic. Gravitational redshift and the Doppler brightening of its
      approaching side are applied, since they are exact.
    - The jets point along the spin axis, which is the main view's rotation axis.
    - The disk fades out when accretion ends (§6.4).
- **Later:** the inside of the debris shell, glowing all around. Its colour and brightness come from the main view's
  scene (`ExplosionScene`: the light curve's colour temperature and brightness). The camera sits inside the shell, so
  the hole bends it from every direction. The remnant's nebula and shocked shell are included when they exist.
- **A failed supernova:** only its weak envelope ejection (`structure.md` §S10), if drawn. Little bending shows, and the
  grid is how you see it.

### 6.3 The grid (the owner's choice B, a switch)
- A spherical latitude-longitude grid at a fixed distance around the hole (for example 50 GM/c²), bent exactly like the
  surroundings.
- It is labelled a measuring aid, as Project 2's distance rings are.
- **For a spinning hole:**
  - The grid lines visibly twist near the hole, because spacetime is dragged round (frame dragging).
  - The **ergosphere** is drawn as a labelled outline (r = GM/c² [1 + √(1 − a² cos²θ)]).
- The grid also labels the horizon and the photon sphere.

### 6.4 Formation, settling and spin (added at the owner's request)
- **Formation (a failed supernova):** the proto-neutron star disappears behind its horizon in ≲ 1 ms at `FormationTimeS`,
  at the same instant the neutrino panel cuts off.
- **Settling:**
  - A new black hole rings down within milliseconds to a smooth spinning (Kerr) state, set entirely by its mass and
    spin.
  - The view shows the formation, then **fades** into the settled, spinning hole, with the caption "Settled: spinning
    black hole". The fade runs:
    - **Direct collapse:** over about one second of playback after formation, labelled as a visual transition.
    - **A gamma-ray-burst outcome:** the disk keeps feeding the hole for about 15 s (MacFadyen & Woosley 1999), which
      spins it up. The fade follows that spin-up as it happens, and the spin readout climbs, from the research's
      starting spin to the settled one.
    - **A fallback black hole:** it settles once the fallback ends, at the time the research gives.
- **The spin value**, a* (the fraction of the maximum), comes from the lab's existing Rotation input:
  - **Slow:** low spin, like the merging black holes LIGO measures.
  - **Fast:** moderate spin.
  - **Extreme, or any gamma-ray-burst outcome:** high spin (about 0.9, which a burst needs).
  - The values and their ranges come from the research.
  - **Confidence: Speculative.** The lab has no birth-spin model, and published birth spins depend on the simulation.
  - **Checks (shown in the tooltip):** measured spins of Cygnus X-1 (above 0.95), A0620−00 (about 0.1), and the low
    spins of LIGO–Virgo–KAGRA's catalogue. To be confirmed by the research.
- **What spin changes on screen:**
  - The shadow shifts and flattens on one side (up to about 7%), most visibly seen side-on, so the camera starts
    side-on to the spin axis.
  - The photon ring becomes lopsided.
  - With the grid on, the twisting and the ergosphere appear (§6.3).
- **The spin axis** is the main view's rotation axis, the same as the jets'.

### 6.5 Checks against observations
- **The shadow's size law:** checked against the Event Horizon Telescope's M87* and Sgr A* rings (giant black holes, but
  the same law, scaled by mass; criterion 3).
- **A comparison readout:**
  - "From Earth this shadow would be about X nano-arcseconds across, about N times too small for the Event Horizon
    Telescope" (computed from the mass, distance and EHT's ≈ 20 µas resolution).
  - For a 10 M☉ hole at 10 kpc, X ≈ 0.1 and N ≈ 200,000.
  - This is why the view says "Camera where no telescope could be".
- **The one known isolated stellar-mass black hole:** OGLE-2011-BLG-0462, found by microlensing. Its mass is about 7 M☉,
  but published estimates differ, so the research gives the range. Shown in the mass readout's tooltip as a comparison.
- **The kick:**
  - A fallback black hole drifts by the lab's momentum-scaled kick (outcomes §6), as a neutron star does (§4.6).
  - A direct-collapse hole gets none, as the lab already has it.

### 6.6 How the light bending is computed
- **Non-spinning (Schwarzschild) fast path:**
  - Light paths are planar, so the bending angle depends only on the impact parameter.
  - A table computed once from the exact integral is used per pixel to look up where each ray came from.
  - The neutron star's surface bending (§4.1) uses the same table.
- **Spinning (Kerr):**
  - Each ray is traced through the Kerr metric in the fragment shader, at a resolution the GPU can afford. It can run
    at a reduced resolution and be smoothed up; the shadow's outline is computed exactly, so its size and shape stay
    exact.
  - OpenGL 4.1 has no compute shaders, so this is done in the fragment shader into an offscreen target. It is measured
    in milestone 1 (§8.5).
- **A C# mirror** of both calculations lives in Core, so tests can check the formulas (criterion 2), and `--snapshot`
  output can be compared with the mirror on a test pattern (the grid).
- **The surroundings [my call]:**
  - They are Project 2's **actual scene** (its particles, at a reduced count, and its shells, nebula and rings), not a
    simplified stand-in.
  - The scene is rendered from the remnant's position into a low-resolution environment cube map in the Close-up's GL
    context.
  - The map is refreshed only when the scene has changed noticeably (time moved, outcome changed), not every frame.
  - The bent rays sample it.
  - This pass is part of milestone 1's speed test (§8.5), so the window-or-inset decision is made with it included.

## 7. Changes to Projects 1 and 2

### 7.1 Signals that have never been observed: "Expected" (owner's request)
- **Gravitational waves:**
  - No gravitational waves from a supernova have ever been confirmed.
  - On the gravitational-wave panel and its tile in the instrument strip, the badge word **"Detected" becomes
    "Expected"**. The other badges stay as they are ("Too faint at this distance" and so on).
  - The owner asked for the short wording because the panel is small. There is **no banner**.
- **Tour captions** about gravitational waves begin **"Expected:"** instead of "In the lab,".
- **The same one-word change applies to every other signal of which no instance has ever been observed from any
  supernova.**
  - The research (§8.1) audits each signal and lists which qualify, with the reason.
  - Likely candidates:
    - neutrinos from a failed supernova;
    - the neutronization burst (SN 1987A's detectors couldn't resolve it);
    - a pair-instability supernova's signals (no confirmed event);
    - a pulsar in SN 1987A.
  - The audit decides. Each change is reported in the build record.
- **SN 1987A's disputed gravitational-wave claim:**
  - In 1987, room-temperature bar detectors in Rome and Maryland reported signals coinciding with the Mont Blanc
    detector's five events at 02:52 UT. That was nearly five hours before the main neutrino burst seen at about
    07:35 UT.
  - The claim is still defended by its authors (Galeotti et al. 2016, Eur. Phys. J. C, doi:10.1140/epjc/s10052-016-4277-4) and widely doubted: the bars were
    far too insensitive.
  - It is added to SN 1987A's "What was actually seen" note, marked **disputed**.
  - It is logged as inconsistency #53.
  - The lab's own gravitational-wave panel already shows such bars could not have detected it. The research confirms
    the bars' sensitivity figure **[my call]**.

### 7.2 The main 3D view (Project 2)
- **The remnant marker drifts with the kick** (§4.6), so both views put the remnant in the same place.
- **The main view's beam half-width follows ρ(P)** (§4.3) instead of its fixed 10°, so both views agree on whether a
  beam crosses Earth.
- At 1,000 yr the drift is a fraction of the remnant's size: about 0.4 pc at 400 km/s.
- The director camera still frames the debris, not the marker.
- The main view's beams keep their phase and tilt in step with the close-up (§4.3).

### 7.3 The gamma-ray panel (Project 1)
- A magnetar's giant flare (§5) appears on the gamma-ray panel as a short spike with its event marker, at the flare's
  energy from the research. The panel's detection logic applies as for the burst.
- This is the one new panel signal. Without it the close-up would show a flare that no panel records.

## 8. How it is built

### 8.1 Research first
A new normative research file, **`docs/research/compact.md`**, with confidence tags and sources as in the earlier
files. Every mismatch found goes on `docs/research/scientific-inconsistencies.md`. It covers:
- **The first second:**
  - typical tracks of the mean shock radius, the gain radius and the proto-neutron star's radius, for exploding and
    failed cores (from published 1D/3D simulations);
  - revival times;
  - any published dependence on the progenitor;
  - the pre-collapse iron (and O-Ne-Mg) core size;
  - the amplitude of shock distortion (SASI);
  - infall speeds.
- **The proto-neutron star's contraction** to the final radius over seconds to a minute.
- **Early cooling:** the surface temperature from about a minute to 30 yr, as a band, joining
  `NeutronStarCoolingModel` at 30 yr (§4.1).
- **The neutron star's layers:**
  - crust thickness;
  - the neutron-drip density and depth;
  - the pasta region;
  - core composition;
  - crust crystallization time.
- **Pulsars:**
  - the beam-width relation;
  - magnetosphere formation time;
  - published inclination angles for presets;
  - measured kick directions for preset neutron stars.
- **Magnetars:**
  - observed periods;
  - field decay;
  - giant-flare rate per magnetar and energies, with the three observed flares.
- **Black holes:**
  - Schwarzschild and Kerr shadow and ISCO formulas;
  - spin values by rotation class, and their ranges;
  - observed spins (X-ray binaries, gravitational-wave catalogues);
  - the collapsar's starting spin and spin-up;
  - fallback settling time;
  - the collapsar disk's extent;
  - the EHT checks;
  - OGLE-2011-BLG-0462.
- **The "never observed" audit** (§7.1), and the 1987 bar-detector sensitivity.

### 8.2 Code, following the existing split
- **Core, new `Compact/` folder.** Pure functions of the outcome, the observer and time, with no OpenGL and no state:
  - `CoreCollapseHistory`: radii and phase over the first second, and the failed-supernova track ending at
    `FormationTimeS`.
  - `NeutronStarState`: radius, temperature or band, P, Ṗ, B, crust present, and position from the kick.
  - `NeutronStarLayers`
  - `Magnetosphere`: light cylinder, beam width, tilt and phase.
  - `MagnetarFlares`: seeded events.
  - `BlackHoleState`: mass, spin over time, settled flag.
  - `BlackHoleOptics`: Schwarzschild table, Kerr ray tracer, shadow, ISCO, ergosphere; the C# mirror.
  - `KickDrift`
  - Preset pins for kick directions and inclinations, where the research finds them.
- **Rendering:**
  - A second GL host in its own control, with a `CloseUpScene` as the single source of what is drawn at each moment
    (the counterpart of `ExplosionScene`).
  - The lensing pass, the neutron star surface and cutaway, field lines, beams, the disk, the grid, the first-second
    surfaces and particles.
  - Reuse of `OrbitCamera`, `CameraDirector`, `DistanceRings`, `HdrPipeline` and `BlackbodyColor`. Whether the two GL
    controls share resources is checked in milestone 1.
- **App:**
  - `CloseUpWindow`, `CloseUpViewModel` (bound to the same `TimelineViewModel` and outcome) and the pulse-trace
    control.
  - `IWindowService.ShowCloseUp()`.
  - The Close-up button in the outcome summary.
  - Tour stops and the window-outline highlight.
  - The §7 changes.

### 8.3 Testing
- **Formulas** against the research and exact results (criterion 2):
  - shadow and photon sphere;
  - the Kerr outline against Bardeen's curve;
  - ISCO and ergosphere;
  - weak-field bending 4GM/(bc²) far from the hole;
  - the neutron star's visible fraction;
  - the light cylinder.
- **Observed checks** (criterion 3):
  - the Crab's period and light cylinder;
  - Cas A's temperature;
  - the EHT shadow law;
  - the first-second tracks inside the research's ranges.
- **Agreement between the views** (criterion 4):
  - the neutrino cutoff equals `FormationTimeS`;
  - the kick position is equal in both scenes;
  - the beams share their phase and tilt.
- **Flash safety** (criterion 6): over every outcome and time, the drawn beam rate gives ≤ 3 Hz crossings, the pulse
  trace's rate matches it, and a flare is a single event.
- **Display randomness:** kick direction and flares depend only on the run's seed.
- **Every readout and caption number equals the model's value,** and every Quantity has a confidence and a source.
- **Sweep:** every preset and every outcome type, at 40 times, with the Close-up open: no errors.
- **Visual checks:**
  - `--snapshot` gains a Close-up option that renders the close-up to PNG, and `--bench` measures both views
    (criterion 5).
  - No screen capture. App launches are kept few, with a budget per task, as in Project 2.

### 8.4 When things fail
- **OpenGL missing or below 4.1:** the Close-up shows the requirement and its readouts keep working, as the main view
  does.
- **Lensing too slow:** reduced resolution first (§6.6). Then, at milestone 1 only, the inset fallback (§2.5).
- **A model error:** as before, shown in the close-up's notes. The other views carry on.

### 8.5 Order of work (each milestone leaves a working app)
1. **Research, the "Expected" wording, and a speed test.**
   - The research file (§8.1).
   - The §7.1 wording.
   - A speed test of two 3D views together with the Kerr ray tracer on Iris Xe class graphics, which decides window or
     inset (§2.5).
2. **The Close-up window and the neutron star in full:** surface, bending, cutaway with neutron drip, spin, field,
   beams, pulse trace, kick, and the main view's drift.
3. **The first second.**
4. **The black hole:** Schwarzschild, then Kerr settling, the grid and ergosphere, the collapsar disk.
5. **The rest:** magnetar flares (with the gamma-ray panel spike), the tour's close-up stops, and polish.

Built on the `project3` branch, subagent-driven as before. Reversible calls are made and logged in
`docs/superpowers/build-record/project3-*` for the owner. Pushing to GitHub only when the owner asks.

## 9. Out of scope for Project 3
- A white-dwarf close-up.
- The neutrino-driven wind and neutrino flavour physics in the close-up.
- A full magnetohydrodynamic magnetosphere: the field is a dipole, opening at the light cylinder.
- A neutron star's own rotation in the light bending (§4.1).
- Black-hole ring-down (quasi-normal modes) drawn as such: settling is a labelled fade.
- Sound beyond the pulsar's pulses (§4.4): no narration, no other sound effects.
- A starfield behind the black hole: made-up or Earth-sky stars would be decoration, and Earth's bright stars mostly lie
  in front of a remnant, not behind it.
- Kilonovas, mergers and other non-supernova transients, as before.

## 10. Flagged for later (changes Project 3 does not make)
- Project 2's §9 items stand: the remnant panel's ejecta profile, and mixing in the gamma-ray model.
- A neutron star's thermal X-rays and a magnetar's persistent X-rays in the X-ray panel, beyond what Project 1 already
  shows.
- A real birth-spin model for black holes (today's spin is from the Rotation input, Speculative).
- **The cooling model:**
  - Project 1's `NeutronStarCoolingModel` reads about 20–30% warm against Cas A's neutron star (#54).
  - Its panel starts at 30 yr, while the close-up's early-cooling track starts at about a minute.
  - Changing either would change the X-ray panel.
- Kerr light bending for a fast-spinning neutron star.

## 11. Approaches considered (chosen with the owner, 2026-11-15)
- **A. A Close-up window of its own (chosen).**
  - It shows both scales at once: the debris light-years across, and the 12 km neutron star. It can go on a second
    monitor.
  - Its drawing, including the light bending, is built for its own scale, so Project 2's tuned view (about 70 fps) is
    untouched.
  - **Cost:** two 3D views drawing at once is the heaviest load on Iris Xe class graphics. It is measured first, and the
    window stops drawing when hidden. The tour must reach a second window.
- **B. A mode of the existing 3D view,** or a fly-in from the debris to the core across about 13 powers of ten.
  - One view, and a good scale story.
  - **Rejected:** the two scales can't be seen together. Project 2's camera, particles and glow would need a second
    scene inside the same control. A continuous 13-decade zoom strains depth precision. It risks Project 2's tuned
    behaviour.
- **C. A picture-in-picture inset** in the main 3D view.
  - Both scales at once, without a window.
  - **Kept as the fallback** if A proves too demanding (§2.5). It is small at 1366×768, and it crowds a view that
    already folds panels to clear.

## 12. Decisions made with the owner (2026-11-15)
- **D1:** The goal is scientific accuracy, matched to observations as far as we reasonably can.
- **D2:** Approach A, a separate Close-up window, with the inset (C) as the fallback if A is too demanding.
- **D3:** The black hole bends its real surroundings (A), with a measuring grid as a switch (B). There is no starfield.
- **D4:** A black hole's spin is shown once it has settled, fading in (for a collapsar, following its spin-up).
- **D5:** Gravitational-wave signals are marked as never observed by changing the badge word "Detected" to "Expected".
  No banner: the panel is small. The same goes for every other never-observed signal.
- **D6:** The first second (§3), the neutron star and magnetar (§4, §5), the black hole (§6) and the window (§2, §8)
  were approved section by section.
- **D7:** The neutron star's layers are shown, including the neutron drip line, as a cutaway with a magnified crust strip.
- **D8:** The pulse rhythm is always shown on screen, slowed and labelled. Pulsars can also have their usual sound
  (the owner's request), but nothing depends on it.

## 13. My calls, for the owner's review
- **C1:** The disputed 1987 bar-detector claim goes in SN 1987A's preset note (marked disputed) and becomes inconsistency #53.
- **C2:** Flash safety. Nothing flashes faster than 3 Hz. Both views draw one rotation period: the real period, but
  never under 2 s (the main view's existing value), so the Crab is shown about 60× slower.
- **C3:** The drawn rotation continues while the timeline is paused and shares one phase with the main view.
- **C4:** The kick direction is measured where the research finds one, otherwise drawn from the run's seed (it changes
  with Re-roll). The main view's marker drifts with it.
- **C5:** The magnetic tilt is schematic at 45°, matching Project 2, unless a preset's inclination is published. The
  research found one: the Crab, 62° (`compact.md` §C4, K3), so that preset uses it in both views.
- **C6:** The close-up draws the beams from the time the magnetosphere forms. The main view keeps drawing them only once
  the pulses escape the debris, and a note explains the difference.
- **C7:** Giant flares also appear on the gamma-ray panel, so no view shows what no panel records (§7.3).
- **C8:** Before bounce, the close-up shows the pre-collapse core.
- **C9:** The settling fade for a direct collapse lasts about one second of playback and is labelled a visual transition.
- **C10:** A white-dwarf close-up is out of scope. Those outcomes get a message.
- **C11:** The neutron star's own rotation is neglected in its light bending.
- **C12:** The neutron star's drawn brightness is tone-mapped to read, not set to its real brightness, with a note.
- **C13:** The Cas A temperature check is disclosed, not tuned. The lab reads about 1.8 MK against about 1.4–1.5 MK observed (seen from afar; 20–30% warm)
  (#54), and the model change is flagged.
- **C14:** Before 30 yr the close-up shows an early-cooling band from new research. The X-ray panel is unchanged.
- **C15:** A collapsar and a pulsational pair-instability black hole use the failed (stalled) track. A magnetar-driven
  explosion uses the exploding track, labelled.
- **C16:** The geometry contract between the views:
  - the spin axis is scene z;
  - Earth is set per outcome at angle ζ from the checkbox;
  - the main view's beam width follows ρ(P).
- **C17:** Measured kick directions are placed in the sky plane, with the line-of-sight part set to zero and disclosed.
- **C18:** The black hole bends Project 2's actual scene, rendered into a cube map that is refreshed only when it
  changes. It is included in the milestone 1 speed test.
- **C19:** The pulsar sound is off by default and plays at the real period (the picture stays slowed), only when Earth
  would receive the pulses. Both rates are labelled on screen.
- **Builder check (2026-11-15):** a fresh reviewer read this spec before the owner. Its six questions are settled above
  (C2, C13–C18). None needed the owner.
