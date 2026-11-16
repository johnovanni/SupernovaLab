# SupernovaLab Project 3: the calls I made

You asked me to make every reversible call myself and to report them afterwards. Here they are, grouped by theme, in
plain words. Each has a number so you can find its full wording, reason and cost-if-wrong in `project3-rulings.md`
(plan rulings P3-1 to P3-13 are in the plan; the pre-flight rulings are S1–S3; execution rulings are R-P3-1 to R-P3-35).
Any of these can be changed.

## First, one correction you need to know about

- **Cas A's neutron star: the lab is about 20–30% too WARM, not 10% too cool (R-P3-1, inconsistency #54).** The spec
  first said the lab's 1.8 million K was "about 10% low" against Heinke & Ho's 2.0–2.1 million K. The research found
  that 2.0–2.1 million K is the temperature *at the star's surface*; seen from far away (redshifted, which is what the
  lab gives) it is about 1.4–1.5 million K. So the comparison had been apples to oranges, and the lab is warm, not cool.
  The spec was corrected, and the Close-up's temperature tooltip for Cas A gives both figures. It is disclosed, not
  tuned, because changing the cooling model would change the X-ray panel.

## The ones that change what you see most

- **The Close-up stays a window of its own (R-P3-9, your decision of 2026-11-15).** The first speed test missed 50 fps
  at half-resolution light bending (40 fps). You chose "try the speed-ups, and let the user set the speed". So there is
  no inset; instead the Close-up ▸ **Light-bending detail** menu (Auto, Full, Half, Quarter) is remembered between
  sessions.
- **The bent light is traced once and reused (R-P3-10 to R-P3-14).** The ray paths depend only on the camera and the
  spin, not on the time, so they are traced when the camera moves and cached. A still view costs almost nothing; a
  moving camera is traced at a coarser level and sharpened in bands when it stops. The Close-up only draws when
  something changes. Auto drops to a coarser level if the frames get slow, stepping two levels at once when it predicts
  one is not enough.
- **Far cameras (R-P3-6).** A ray from a camera more than 200 GM/c² away is moved straight to that distance first and
  given the small textbook bend (4GM/bc²) for the stretch it skips. The bend left out is under half a degree.
- **The thin bright rim at the black hole's shadow edge is the photon ring (R-P3-7):** rays that nearly orbit the hole
  run out of steps, and they are drawn as the ring they are.
- **Before the shock breaks out, the core is surrounded by a dim warm haze (R-P3-21),** because it is inside the
  opaque star. For a star that never explodes, the haze lasts until the black hole forms.
- **The neutron drip line is drawn to scale (R-P3-20).** At the default zoom it is a thin band two pixels below the
  surface (that is the true scale); the "Magnified ×N" strip below the view is where it reads clearly.
- **Overlays are not bent (R-P3-19):** field lines, beams, the light cylinder and the "To Earth" arrow are drawn
  straight; a note says so.
- **Beams keep a smooth rotation even while the period changes (R-P3-18):** the phase is added up frame by frame, so a
  magnetar scrubbed through its spin-down never suddenly spins faster.

## Physics calls

- **Units (P3-1, P3-2, P3-3).** The Close-up's scene is in km, centred on the remnant; black-hole code works in GM/c².
  The spin axis is "up" in both windows; Earth's direction is set from the lab's "beam toward Earth" checkbox.
- **The beam's width is 5.6° × P^(−1/2) (R-P3-4, #59),** a published fit (Biggs 1990) between the two published
  families; the plan's 5.4° was not a published value. **The Crab's magnetic tilt is the published 62° (R-P3-4)**; every
  other pulsar uses the schematic 45°, the same in both windows.
- **Black-hole spins until the research confirmed them (P3-13):** Slow 0.1, Fast 0.5, Extreme 0.9, any gamma-ray-burst
  outcome 0.9, a collapsar starting at 0.5. All Speculative. The research (§C8) confirmed them.
- **The first second's earliest rows (under 30 ms) are approximate stand-ins (R-P3-5),** labelled "From simulations",
  as the whole first second is.
- **The magnetar's period only reaches seconds for the slow-rotation magnetar (R-P3-3, #57).** Observed magnetars
  spin at 2–12 s; the lab's fast-born ones stay near 0.2 s for 1,000 years. The Close-up says so whenever a magnetar
  spins faster than 2 s.
- **The infall speed is measured at the shock (R-P3-22);** above 0.3c a note says the simple formula is not accurate
  there (as Project 2 does).
- **The newborn proto-neutron star's brightness follows the neutrino output (R-P3-23)** at the Close-up's own fixed
  exposure. **A magnetar-driven explosion uses the exploding track (R-P3-24).**
- **The tracer's coordinates (R-P3-8)** differ from the textbook ones by about a degree close in, which does not show.

## Honesty calls

- **Cas A is disclosed, not tuned (R-P3-17):** the Close-up calls its star "a pulsar in the lab's model" and says no
  pulses have ever been seen from it (#60); the kick tooltip compares the lab's 348 km/s with the measured 433 km/s
  (#61, within about 1σ).
- **Sgr A* is tested against the EHT's own shadow range, not against M87's ring calibration (R-P3-2, #55),** which it
  fails by 2σ. M87 is tested with its independent stellar-dynamics mass.
- **New inconsistencies #53–#70** are in `docs/research/scientific-inconsistencies.md`; the README's Known misses lists
  the ones that show. The last one, **#70 (Task 14)**: a fallback black hole forms at 3 s in the lab, but the fallback
  that makes it takes minutes to hours. Both views agree on the 3 s; changing it would change the neutrino panel, so it
  is flagged.
- **A research-table slip was logged, not hidden (Task 14, compact.md K9):** the Kerr-outline check table's α ends were
  sampled a little short (6.8316 against the exact 6.8323, and so on). The differences are about 1/40 of the test's
  tolerance; the tests' tolerances were not changed.
- **"Expected" badges now say why (R-P3-30):** hovering one gives the research's reason that the signal has never been
  observed (for example, no gravitational waves from any supernova; the 1987 bar claim is disputed).
- **The speed label and its note agree under redshift (R-P3-31):** a far-away star whose pulses arrive stretched to
  more than 2 s is "Shown at about real speed as received on Earth", and the note says why.
- **The pulse trace's marks (R-P3-28):** two beams give at most about one mark a second, but when Earth is off the
  star's equator the two marks of a turn are not exactly evenly spaced (for example 0.98 s and 1.02 s). That is far
  under the 3-a-second flash limit; the tooltip now says "about one mark a second" instead of "never more than once".

## Process calls

- **Visual checks were carried forward to save launches (R-P3-15, R-P3-16, R-P3-25, R-P3-26, R-P3-27):** when a task's
  look could be checked in the next task's launch, it was, rather than spending an extra launch.
- **App launches, per task:** Task 4: 1; **Task 4b: 4 (over its budget of 2, R-P3-15 and R-P3-16:** one to confirm the
  sharpening after the camera stops, one to make sure the main view had not slowed); Task 6: 2; Task 7: 2; Task 9: 2;
  Task 10: 1; Task 11: 1; Task 12: 1; Task 14: 2 (the sweep, then one batched launch for every carried visual check and
  the benchmark). Tasks 1, 2, 3, 5, 8 and 13 launched nothing. 16 launches in all.
- **No screen capture was ever run.** Every picture came from the app's own `--snapshot` read-back. In Task 6 a builder
  tried a PowerShell `Add-Type` command (to crop a PNG); the permission check blocked it and nothing ran. Every later
  task was told never to use `Add-Type`, P/Invoke or `CopyFromScreen` for any purpose.
- **The Task 14 diagnostics (R-P3-29):** `--closeup-size` (the Close-up at several sizes), `--bench-preset` (bench
  several presets in one launch), the sweep now also counts the Close-up's own errors and plays 14 of the lab's own
  outcomes (white dwarf, failed supernova, fallback, collapsar, magnetars, pair instability …) after the presets, and a
  magnetar's gamma-ray burst tab is captured.
- **The glossary (R-P3-32):** 9 new terms (event horizon, shadow, photon ring, ergosphere, frame dragging, ISCO, gain
  region, SASI, stalled shock). The Close-up's readout labels (Horizon, Shadow, Gain radius, …) start their tooltip with
  the term, and a note that names a term shows it on hover.

## Flagged for later, not built (spec §10)

Each of these would change a Project 1 or 2 panel, or needs research the lab does not have:

- a neutron star's thermal X-rays and a magnetar's persistent X-rays in the X-ray panel;
- a real birth-spin model for black holes (today's spin follows the Rotation input, Speculative);
- the cooling model: about 20–30% warm against Cas A (#54) and between theory's two levels (#58); its panel starts at
  30 years while the Close-up's early-cooling band starts at about a minute;
- Kerr light bending for a fast-spinning neutron star (its own spin is neglected in its bending, C11);
- a later fallback black-hole formation time (#70, new in Task 14);
- Project 2's two flagged items: the remnant panel's ejecta profile, and mixing in the gamma-ray model.

## Numbers at the end

- **Tests:** 2,859 passing, 0 build warnings (2,778 before Task 14).
- **Spec success criteria:** 1–4 and 6 pass in `Project3ValidationTests` and `FlashSafetyTests` over every preset and
  every outcome type (14,951 readouts checked for a confidence and a source); the sweep (14 presets and 14 lab outcomes ×
  40 times, Close-up open) found no errors.
- **Performance (criterion 5): not met in the final launch, and you need to know why it is open.** With the Close-up at
  its default size, both views held 63.8 fps for N6946-BH1 (52.3 fps with the camera orbiting), but only 29.6 fps for the
  Crab and 33.9 fps for SN 1998bw. The Close-up is not the cause: with it hidden, the main window alone ran at 34–39 fps
  for those two presets, where Task 4b measured 70–78 fps for SN 1998bw. The time is in the UI thread's WPF rendering
  (about 25 ms a frame against 10 ms then), not in the GPU. Either something in Tasks 5–13 made the main and Instruments
  windows dearer to draw for outcomes with debris, or the launch was disturbed; I could not tell with two launches. I
  propose one diagnostic launch (`docs/perf-2026-11.md`, "Project 3, Task 14") for your decision.
- **Visual checks:** three failed and were fixed after the launch, unverified on screen (no launch left): the drip label
  ran under the cutaway key at the default window size; the tour never lit the Close-up's outline (a WPF precedence
  slip); the gamma panel's "Giant flares: see the burst tab." pointer was cut off. The rest passed.
