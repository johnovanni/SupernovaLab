# Scientific inconsistencies: a running list

Places where the lab, its research files, or the published record disagree with each other. Each one is either
fixed, disclosed in the app, annotated in the research, or still open. Many are worth investigating further, in
Project 2 or 3 or on their own.

Status key:
- **Fixed**: the lab now agrees with the record.
- **Disclosed**: the lab still differs, and the app says so plainly.
- **Annotated**: the research file is corrected or annotated.
- **Open**: not yet resolved.

Ruling numbers (R…) refer to the build ledger's global constraints.

## Mass, energy and remnants

| # | What disagrees | Why it is interesting | Status |
|---|---|---|---|
| 1 | **SN 1998bw's mass budget.** The published ejecta (10.9 M☉, Iwamoto 1998) plus the lab's black hole (5.2 M☉) add up to 16.1 M☉, more than the lab's star holds (11.2 M☉ final mass). Iwamoto's own remnant is 2.9 M☉. SN 2015bn is also slightly over budget (9.82 vs 9.28 M☉). | The lab's star model, the published light-curve fit and the remnant come from different assumptions. How much mass a collapsar really swallows, and how heavy its progenitor really was, are both open questions. | Being fixed (R53: remnant pin plus a mass-budget note) |
| 2 | **SN 1987A's neutron star mass.** The lab's 19 M☉ star makes a 1.63 M☉ neutron star, which radiates 3.9e53 erg in neutrinos. The 1987A burst implies 2–3e53. Blinnikov 2000's 1.6 M☉ mass cut doesn't say whether it is baryonic or gravitational: read as baryonic (1.44 M☉ gravitational), Kamiokande-II expects 18; read as gravitational, about 21. | It ties the neutrino count, the remnant mass and the progenitor model together. JWST's 2024 evidence for the neutron star may pin this down. | Pinned (R46, baryonic reading) |
| 3 | **Kamiokande-II: 18.45 expected vs 11 seen.** About a 4% Poisson chance, before the NS-mass and efficiency uncertainties. | Either the burst was on the low side, the detector efficiency is lower than modelled, or the neutron star is lighter. | Disclosed |

## Light

| # | What disagrees | Why it is interesting | Status |
|---|---|---|---|
| 4 | **Magnetar superluminous supernovae.** With extreme spin, the lab reaches about 1e46 erg/s (M_V −26), up to 5× the brightest supernova ever seen (ASASSN-15lh). Full trapping of a ~1 ms magnetar's spin-down is assumed, and the SLSN class ignored the research's ejecta and progenitor-mass conditions. | It shows where the simple magnetar model breaks: γ-ray leakage, and whether such a magnetar can form at all in light ejecta. | Being fixed (R53) |
| 5 | **SN 2008D's X-ray flash.** The lab's bare-star breakout lasts about 2 s and carries about 200× less energy than the observed ~400 s, ~2e46 erg flash. | The research explains the real flash as breakout through a dense stellar wind, which the lab doesn't model. The lab's breakout speed (0.65c) is also past the formulas' range, so it is capped at 0.3c (R28). | Disclosed |
| 6 | **SN 1998bw's peak.** Lab V 14.1 at about 23 d vs the observed 13.62 at about 16 d. | Ic-BL light curves need asymmetric or two-component ejecta, which a one-zone Arnett model lacks. | Disclosed |
| 7 | **SN 1054's peak.** The lab gives about V −2.0, while the history says it was visible in daylight (about −4 or brighter). | The lab's low-energy (1e50 erg) electron-capture explosion may be too faint, or the Crab's progenitor was different. A long-running debate. | Disclosed |
| 8 | **SN 2015bn's peak.** It is 0.77× the published value (about 1.3σ low). | The same magnetar fit gives a different peak through the lab's Arnett kernel. | Being disclosed (R53) |
| 9 | **SN 1987A's light-curve dome.** It peaks at day 83 vs about 85–90 observed. The research has no blue-supergiant recipe, so the lab extrapolates a plateau model. | It needs a real BSG model with mixing. | Accepted (κ 0.10 pin) |
| 10 | **The default 16 M☉ II-P peaks at M_V −17.9,** about 1σ brighter than the II-P mean, because the engine's red supergiant is large (886 R☉). | It is a question of how big real pre-supernova red supergiants are. | Being noted in Details (R53) |
| 11 | **Type Ia decline rate.** Phillips Δm15(B) inverted from the lab's Arnett peak (about −19.0) reads too fast (1.41; SN 2011fe 1.51 vs about 1.1–1.2 observed). | Arnett peaks for Ia run about 0.3 mag faint against the Phillips zero point. | Being fixed (R53: measured from the drawn curve) |
| 12 | **Type Ia rise time.** With the research's stripped-envelope opacity (0.05–0.1), Arnett gives an 11–14 d rise; observed is 17–19 d. | The lab uses an effective κ = 0.19 for Ia (R31). Constant-opacity Arnett models are known to underpredict Ia rises. | Calibrated and labelled |

## Gamma rays

| # | What disagrees | Why it is interesting | Status |
|---|---|---|---|
| 13 | **SN 1987A's 847 keV line is about 22× below SMM's** days 159–250 average. | The lab has no outward ⁵⁶Co mixing. The early emergence of 1987A's gamma lines was itself the evidence for mixing. | Disclosed |
| 14 | **SN 2011fe's gamma lines.** The narrow-line sensitivity in the research made INTEGRAL "detect" a line it did not. | Ia lines are 20–40 keV wide. With line-width scaling (R52), 2011fe sits just under detection (best ratio 0.76), a thin margin. | Fixed (R52); margin disclosed |
| 15 | **Gamma-ray line escape vs energy deposition.** The research used one T0 for both. | Line photons are lost on any Compton scatter (Klein–Nishina), so lines emerge later. With that (R37), SN 2014J matches Churazov's 0.60 escape fraction. | Fixed (R37) |
| 16 | **NuSTAR's ⁴⁴Ti threshold.** It is anchored to its own detections of Cas A and SN 1987A (R38), not to a published sensitivity. | It needs a real narrow-line sensitivity at 68/78 keV. | Open |
| 17 | **⁴⁴Ti yields.** The research gives no yield rule. The lab uses 2×10⁻³ of the ⁵⁶Ni mass from two observed cases (R35), and none for Type Ia. | Observed ⁴⁴Ti/⁵⁶Ni ratios sit above most models'. | Open (Speculative) |

## Radio and X-ray from the surroundings

| # | What disagrees | Why it is interesting | Status |
|---|---|---|---|
| 18 | **SN 1987A's radio.** The lab gives about 350 mJy at 1.4 GHz peaking near day 18, still about 90 mJy at day 100. The real radio was about 130 mJy at 0.84 GHz within days and gone by September 1987 (Ball et al. 2001, citing Turtle et al. 1987). | The lab gives the blue supergiant a Wolf–Rayet-like wind and a red-supergiant shock speed. Matching needs a wind about 40× thinner and a shock about 5× faster. No paper checked gave both numbers. | Disclosed |
| 19 | **SN 1998bw's radio is 9–12× faint** (4.8 vs 45 mJy, Kulkarni 1998). | The real shock was mildly relativistic (Γβ about 1–2); the lab's CSM model is Newtonian. | Disclosed |
| 20 | **Population radio peaks.** Model peaks sit +1.1σ (II) and +1.8σ (Ib/c) above the Bietenholz 2021 means. | The shock speeds come from detected supernovae, while the population mean includes non-detections (only 31% detected): a selection bias. | Open |
| 21 | **The research's SSA spectrum join** peaked at 0.55 L_p at 1.30 ν_p. | It was rescaled so the peak is L_p at ν_p (R42). | Annotated and fixed |

## Neutrinos and gravitational waves

| # | What disagrees | Why it is interesting | Status |
|---|---|---|---|
| 22 | **Failed-supernova neutrinos.** A flat accretion luminosity until black-hole formation gave up to 3.45e53 erg in anti-ν_e, 2.3× the research's model compilation. | Saturated at t = 2 s (R23). How accretion luminosity evolves in black-hole-forming collapses is still uncertain. | Calibrated |
| 23 | **DUNE counts for failed supernovae are low.** After flavour conversion, DUNE sees mostly ν_x, and the research gives no ν_x accretion luminosity. | Heavy-lepton neutrinos from black-hole-forming collapse are not well known. | Open |
| 24 | **The rapid-rotation gravitational-wave horizon.** The research says LIGO sees extreme models to M31–M82, but its own bounce amplitude cap (a few hundred cm) only reaches about 0.3 Mpc. | The M31–M82 figure comes from bar-mode models the lab doesn't include (R26). | Accepted |

## Research-file inconsistencies (annotated in place)

| # | What disagrees | Status |
|---|---|---|
| 25 | **Red-supergiant breakout energy.** The research says "about 3e46 erg", but its own Waxman & Katz formula gives about 2e48 bolometric (signals §3.2). | Annotated |
| 26 | **GRB afterglow normalization.** The research's 3.5e32 is per z = 1 observer day; read at z ≈ 0 it is 1.41× too bright (signals §9.3). | Annotated (R39) |

## Model structure

| # | What disagrees | Why it is interesting | Status |
|---|---|---|---|
| 27 | **The 30 M☉ core-mass step.** Switching from the Sukhbold 2016 cores to the Woosley 2019 fit at 30 M☉ drops the CO core from 9.1 to 6.5 M☉. This draws a false neutron-star island on the outcome map (Z 0.1, Full, Extreme). | Real explodability islands exist; this one doesn't. A smooth blend needs cores from one model set across the boundary. | Being noted (R53); open |
| 28 | **Spectrum velocities vs the 3D view.** Line velocities were type-typical, not scaled by the explosion's energy per mass, and differed by up to 8× from the 3D shell (SN 1054). | Real line velocities do scale with E/M, but with large scatter. | Being fixed (R53) |
| 29 | **Early II-P Hα velocity.** Extrapolating the research's t^−0.46 law back to an 8.5 d peak gives about 16,500 km/s; observed about a week in is 11,000–13,000. | It is held before day 15 (R45d). The law comes from Fe II on the plateau. | Calibrated |
| 30 | **Colour temperature** for the plateau family and IIn is fixed at 6,000 K after cooling. Early IIn spectra are much bluer. | The CSM interaction light is not modelled. | Being noted (R53) |
