# SupernovaLab Project 2: the calls I made

You asked me to make every reversible call myself and to report them afterwards. Here they are, grouped by theme, in
plain words. Each has a number so you can find its full wording, reason and cost-if-wrong in `project2-rulings.md`
(plan rulings P1–P10 are in the plan; execution rulings are R-P2-1 to R-P2-33). Any of these can be changed.

## The ones that change what you see most

- **The camera steps back when the debris reach the outermost ring (P5).** The outer distance ring always sits on
  the 90% line, so a step happens exactly when the shell touches it. Steps are about 4–5×; at the default speed the
  camera holds still for at least 80% of each cycle (the worst cycle measured 0.814).
- **Where units switch (km → AU → light-years), a few steps are only ×3 (R-P2-8).** Those still hold still for 80%
  at the default speed; at the bench's double speed they dip to 60%.
- **Cut faces are solid surfaces, not particles (R-P2-21, R-P2-23).** My first plan (P7: colour the particles near
  the cut planes) didn't read at all: the notch filled in. The faces are now drawn analytically, fade with the
  debris density, and stay out of the bloom so their colours are the true element colours.
- **The cut is turned so it passes through the mixing fingers (R-P2-22).** SN 1987A has only four finger cones,
  and with a fixed cut none of them crossed it. The wedge now turns about the vertical axis to cut through the most
  fingers; the camera's starting view turns with it. The fingers themselves (the physics sampling) are unchanged.
- **Fingers are drawn only where material actually mixed:** nickel outward from its layer to its tip; hydrogen
  inward from its layer to its lowest speed (Task 7 fix).
- **The tour has one extra stop that shows the mixing (R-P2-33),** at peak light, with the cutaway on and a slow
  orbit — the spec asked for it and the first build missed it.

## Physics calls

- **The debris run ahead of Project 1's radius readout (P1).** The research shows the real density profile's
  fastest layers reach 1.7–2.0× Project 1's uniform-ball edge, so the particles do too; the readout keeps
  Project 1's number. The camera frames 99% of the debris mass.
- **Layer masses come from the star; when a preset's published ejecta mass disagrees, only the non-nickel layers
  are scaled, with a note (P2, R-P2-12).** SN 1998bw gets ×1.34.
- **Mixing: half of SN 1987A's nickel is pushed into fingers, tips at 4,700 km/s (R-P2-13);** pair-instability
  supernovae get no fingers, following Joggerst & Whalen 2011 (R-P2-17).
- **The shocks (R-P2-24, superseding my earlier R-P2-10/11).** I first had the drawn forward shock follow Project 1's
  uniform-ejecta blast wave, for consistency. That was wrong: it put 38% of the debris through the reverse shock at
  one year (physically ~0.003 M☉). Now the early shock is the radio panel's own shock, and the remnant era uses the
  steep-ejecta solutions (Truelove & McKee; Tang & Chevalier). Cas A's forward shock comes out at 2.33 pc against
  ≈ 2.5 pc observed. The remnant-era readout shows the drawn shock, with Project 1's smaller value in a note; Project
  1's remnant model itself is unchanged and flagged for later (spec §9).
- **The 3–10 year join between the two shock models never runs backwards (R-P2-26)** and is labelled schematic; for
  stripped stars it holds its radius for a few years. Shocked debris stay shocked (R-P2-24), and no shocked particle
  is drawn ahead of free expansion (R-P2-33).
- **A failed supernova's weak envelope ejection is drawn (§S10, R-P2-19).**
- **SN 2023ixf's confined gas and the Crab's undetected outer shell are drawn as preset-pinned geometry,** like
  SN 1987A's rings (R-P2-27, R-P2-29, R-P2-33). No instrument reads these pins, and they don't mark the outcome
  "Published" (R-P2-28).
- **A note appears when the fastest debris would exceed 0.3c (R-P2-18);** the lab's profile isn't relativistic.

## Honesty calls

- **Misses are on screen, not only in the list (R-P2-32):** SN 1987A's rings drawn symmetric; a nebula drawn for
  SN 1987A though none is confirmed; Cas A's reverse shock at 0.85 of the forward shock against ≈ 0.62 observed; the
  lab's Crab nebula 44% small. Less visible ones are in the README's "Known misses" list.
- **Tour captions say "Observed:" or "In the lab," and every caption's sources and confidence show on hover;**
  recalled facts say "(recalled)" (Task 12 fix, R-P2-31).
- **Research-file corrections:** SN 1054's "653 days" is 642 by the file's own dates; Project 1 credited its
  blast-wave constants to Leahy & Williams when they are Tang & Chevalier 2017 (R-P2-14). Both fixed and logged.
- **The spec was corrected where the research showed it couldn't hold as written** (the debris edge, the forward
  shock, the default playback speed) (R-P2-15).

## Process calls

- **Tasks ran in parallel where they shared no files** (research beside the camera work) (R-P2-7, R-P2-16).
- **The quality governor measures GPU render time, not frame spacing (R-P2-30),** so a slow display can't trick it
  into dropping quality. The notes overlay is collapsible and puts the most relevant note first.
- **App launches:** I set a budget of one or two per task for your antivirus. Three tasks went over (Task 11 used 6,
  because of a snapshot bug; Tasks 10 and 14 used 3). No screen capture was ever used, and nothing was flagged.

## Left for you to decide (residuals from the final review)

- **R1.** Inconsistency #51 names the wrong cause. The real one: when the breakout model gives no breakout (dense
  gas), Project 1's light curve starts at 1 hour, so its "peak" is its first point. A one-line docs fix.
- **R2.** On the tour's mixing stop at 1366×768, the caption sits over the slice it describes. The fix is to anchor
  the caption at the top on cutaway stops; checking it needs one app launch.

Smaller leftovers (polish) are listed in `project2-ledger.md`.

## Numbers at the end

- **Tests:** 2,157 passing, 0 build warnings.
- **Spec success criteria:** 1–4 pass in validation tests across all 14 presets; the sweep of 14 presets × 40 times
  found no errors.
- **Performance (criterion 5):** 67–70 fps at 1366×768 and 62–67 fps at 1920×1080 with 200,000 particles, bloom and
  the tour running (target 50). GPU time ≈ 1–2 ms per frame.
- **Scientific inconsistencies:** #31–#52 added, each fixed, disclosed or open.
