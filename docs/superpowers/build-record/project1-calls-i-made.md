# SupernovaLab Project 1: the calls I made

You asked me to make every reversible call myself and to report them afterwards. Here they are, grouped by theme.
Each call has a ruling number (R1–R53) so you can find it in `project1-rulings.md`, which gives the full wording,
the reason and the cost if wrong. The spec's own decisions D1–D10 are in spec §12. Any of these can be changed;
none is locked in.

## Layout and app behaviour

- **Multi-window layout (your request).** Inputs, 3D view, summary and timeline in the main window. Six instrument
  panels in their own window, with an always-visible strip of six tiles. Outcome details and Preset info get their
  own windows.
- **No screen-capture scripts (R19, your antivirus).** Every visual check used the app's own `--snapshot`, which
  renders itself to PNG. `--bench` and `--sweep` measure in the same way. Launches were batched, one or two per task.
- **Observer edits keep a preset's published explosion (R49).** You can watch "SN 1987A from Andromeda". Changing
  the star drops the published values. Re-picking the preset restores everything.
- **Badges show the state now (R50).** For example "Over: it was detected" months after a 23-second burst. The
  panel badge judges the selected view; the strip tile judges the whole messenger.
- **Stated peaks come from the drawn curve (R34).** The number in the summary always matches the curve you see.

## Honesty rules

- **Every number carries a confidence label and its source (R24, R27).** Anything marked [APPROX] in the research,
  or invented to fill a gap, is labelled Speculative in the data, not just in comments. The whole gravitational-wave
  panel is Speculative.
- **Misses are disclosed, never tuned away (R47b).** When the lab disagrees with what was observed, the preset says
  so in plain words. No physics constant was changed to make a check pass.
- **One definition per quantity.** The magnetar peak (R32), the plateau (R33), the colour temperature (shared by the
  light curve, spectrum and 3D view) and the ejecta velocity (one helper) are each defined once.
- **A list of scientific inconsistencies** is kept in `docs/research/scientific-inconsistencies.md` (your request).

## Physics calls where the research was silent or contradicted itself

- **Stars and fates.**
  - The default star is 16 M☉, because 15.0 M☉ fails to explode in the published model (R8).
  - Explosion-table rows are used only where they apply: unstripped stars up to 30 M☉ (R1, R16).
  - Neutron stars are at least 1.2 M☉ (R13).
  - Upper-metallicity limits for pair instability (R12).
  - Stripped-star cores (R14, R20).
- **Nickel.** Calibrated nickel is the smaller of theory and the observed median, and at most half the ejecta (R15).
  Type Ia is exempt (R17).
- **Neutrinos.** Failed-supernova accretion saturates at 2 s, so the energy stays inside the published model range
  (R23). Super-K triggers on events above 17 MeV, and DUNE sees oscillated neutrinos (R25).
- **Gravitational waves.** Advanced LIGO's range is about 5.5 kpc. Spec §8's example was changed from 8 to 5 kpc,
  because 8 kpc is a different metric (R26).
- **Shock breakout.**
  - Breakout speed is capped at 0.3c and flagged when exceeded (R28).
  - X-ray detection uses one photon-count rule (R29).
  - SN 2008D-like flashes carry a dense-wind caveat (R30).
- **Light curves.**
  - Type Ia uses a fixed effective opacity of 0.19, matching the observed 17–19 day rise. The opacity slider applies
    to core collapse only (R31).
  - A superluminous label needs at least 2.2 M☉ of ejecta and a star above 20 M☉, the research's own conditions.
    Anything brighter than the brightest supernova ever seen says so (R53).
- **Gamma rays.**
  - ⁴⁴Ti is 0.2% of the nickel, from the two observed cases (R35).
  - Line photons escape later than the energy that heats the debris (Klein–Nishina, R37). This matches SN 2014J.
  - The NuSTAR threshold is anchored to its real detections (R38).
  - Broad Type Ia lines raise the detection threshold (R52). INTEGRAL now correctly misses SN 2011fe.
- **Gamma-ray bursts.** Presets can pin a burst's energy (R36). Afterglows are drawn only where the formulas hold
  (R39).
- **Radio.** Thresholds are per view: continuum or pulsar timing (R40). The radio spectrum is normalised so its
  peak lands where the published relations put it (R42).
- **Spectrum.** Each line has its own measured velocity ratio. Hydrogen speeds fall with time from day 15. Type Iax
  is slow (R43, R45). Velocities scale with the explosion's energy per mass (R53).

## Presets

- **Each preset pins one paper's values** (R44). Pins are trusted, and are ignored with a note if the star doesn't
  explode.
- **Added pins.**
  - Crab: birth period, field and radio brightness (R41).
  - SN 1987A: neutron-star mass 1.44 M☉, from Blinnikov 2000's 1.6 M☉ read as baryonic (R46). Read as gravitational,
    the Kamiokande-II count would be about 21 instead of 18.
  - SN 1998bw: black hole 2.9 M☉ (Iwamoto, R53).
  - SN 1987A: opacity 0.10.
- **SN 1987A uses 0.1 × solar metallicity.** It is the lab's only route to a blue supergiant; the real LMC is about
  half solar. The preset says so.
- **SN 2008D** combines two papers' values (Mazzali's explosion, Soderberg's radius), disclosed (R47c).
- **The Crab check is labelled a consistency check (R47d).** Its inputs come from the measured spin, so it cannot
  fail.

## Process calls

- **Stronger models where accuracy mattered.** Most tasks were built and reviewed on the stronger model; quick
  re-reviews used a lighter one.
- **Fixes folded into the current task when cheap.** Small items found during a task were fixed there, or carried
  to the next task that touched the same code.
- **The last re-review's text mismatches** (five small ones) I fixed myself rather than run another round.
- **Parked** (minor, not user-visible): the `--snapshot` sweep doesn't count errors caught inside the timeline
  markers or 3D scene. The deferred minors are listed in `project1-ledger.md`.

## Numbers at the end

- **Tests:** 1,710 passing, with 0 build warnings.
- **Performance:** about 80–95 fps for the 3D view on the Intel Iris Xe, and about 87 fps for the panels while
  playing.
- **Presets:** all 14 load and play from 1 ms to 1,000 years with no errors.
- **Spec §8 validations:** all pass. SN 1987A's Kamiokande-II count is 18.4 expected (6–20), against 11 actually
  seen.
