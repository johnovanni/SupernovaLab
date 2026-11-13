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
| 1 | **SN 1998bw's mass budget.** The published ejecta (10.9 M☉, Iwamoto 1998) plus the lab's black hole (5.2 M☉) add up to 16.1 M☉, more than the lab's star holds (11.2 M☉ final mass). Iwamoto's own remnant is 2.9 M☉. SN 2015bn is also slightly over budget (9.82 vs 9.28 M☉). | The lab's star model, the published light-curve fit and the remnant come from different assumptions. How much mass a collapsar really swallows, and how heavy its progenitor really was, are both open questions. | Fixed for SN 1998bw (R53: Iwamoto's 2.9 M☉ black hole pinned); a mass-budget note shows whenever published ejecta exceed the lab's star. The underlying tension stays open. |
| 2 | **SN 1987A's neutron star mass.** The lab's 19 M☉ star makes a 1.63 M☉ neutron star, which radiates 3.9e53 erg in neutrinos. The 1987A burst implies 2–3e53. Blinnikov 2000's 1.6 M☉ mass cut doesn't say whether it is baryonic or gravitational: read as baryonic (1.44 M☉ gravitational), Kamiokande-II expects 18; read as gravitational, about 21. | It ties the neutrino count, the remnant mass and the progenitor model together. JWST's 2024 evidence for the neutron star may pin this down. | Pinned (R46, baryonic reading) |
| 3 | **Kamiokande-II: 18.45 expected vs 11 seen.** About a 4% Poisson chance, before the NS-mass and efficiency uncertainties. | Either the burst was on the low side, the detector efficiency is lower than modelled, or the neutron star is lighter. | Disclosed |

## Light

| # | What disagrees | Why it is interesting | Status |
|---|---|---|---|
| 4 | **Magnetar superluminous supernovae.** With extreme spin, the lab reaches about 1e46 erg/s (M_V −26), up to 5× the brightest supernova ever seen (ASASSN-15lh). Full trapping of a ~1 ms magnetar's spin-down is assumed, and the SLSN class ignored the research's ejecta and progenitor-mass conditions. | It shows where the simple magnetar model breaks: γ-ray leakage, and whether such a magnetar can form at all in light ejecta. | Label fixed (R53): the SLSN class now needs ≥ 2.2 M☉ ejecta and a > 20 M☉ star, which moved 392 of 684 grid outcomes to their base class. Peaks above 2×10⁴⁵ erg/s carry a disclosure. The light curves still assume full trapping (no γ-ray leakage): open. |
| 5 | **SN 2008D's X-ray flash.** The lab's bare-star breakout lasts about 2 s and carries about 200× less energy than the observed ~400 s, ~2e46 erg flash. | The research explains the real flash as breakout through a dense stellar wind, which the lab doesn't model. The lab's breakout speed (0.65c) is also past the formulas' range, so it is capped at 0.3c (R28). | Disclosed |
| 6 | **SN 1998bw's peak.** Lab V 14.1 at about 23 d vs the observed 13.62 at about 16 d. | Ic-BL light curves need asymmetric or two-component ejecta, which a one-zone Arnett model lacks. | Disclosed |
| 7 | **SN 1054's peak.** The lab gives about V −2.0, while the history says it was visible in daylight (about −4 or brighter). | The lab's low-energy (1e50 erg) electron-capture explosion may be too faint, or the Crab's progenitor was different. A long-running debate. | Disclosed |
| 8 | **SN 2015bn's peak.** It is 0.77× the published value (about 1.3σ low). | The same magnetar fit gives a different peak through the lab's Arnett kernel. | Disclosed |
| 9 | **SN 1987A's light-curve dome.** It peaks at day 83 vs about 85–90 observed. The research has no blue-supergiant recipe, so the lab extrapolates a plateau model. | It needs a real BSG model with mixing. | Accepted (κ 0.10 pin) |
| 10 | **The default 16 M☉ II-P peaks at M_V −17.9,** about 1σ brighter than the II-P mean, because the engine's red supergiant is large (886 R☉). | It is a question of how big real pre-supernova red supergiants are. | Noted in Details |
| 11 | **Type Ia decline rate.** Phillips Δm15(B) inverted from the lab's Arnett peak read too fast (1.41; SN 2011fe 1.51). Now measured from the drawn B curve, it reads 0.99 for every near-Chandrasekhar Ia, whatever its nickel; SN 2011fe's observed value is 1.103 ± 0.035 (Pereira et al. 2013). | The lab's Ia curve shape is fixed (constant κ_eff), so it cannot reproduce the width–luminosity (Phillips) relation itself. A real Ia's decline depends on its nickel and opacity. | Disclosed for SN 2011fe; open as physics |
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
| 27 | **The 30 M☉ core-mass step.** Switching from the Sukhbold 2016 cores to the Woosley 2019 fit at 30 M☉ drops the CO core from 9.1 to 6.5 M☉. This draws a false neutron-star island on the outcome map (Z 0.1, Full, Extreme). | Real explodability islands exist; this one doesn't. A smooth blend needs cores from one model set across the boundary. | Noted on the outcome map; open |
| 28 | **Spectrum velocities vs the 3D view.** Line velocities were type-typical, not scaled by the explosion's energy per mass, and differed by up to 8× from the 3D shell (SN 1054). | Real line velocities do scale with E/M, but with large scatter. | Fixed (R53): line velocities scale by √(E/M_ej) against a reference for each type, clamped to the observed ranges (Ic-BL references from Taddia 2019 and Cano 2017) |
| 29 | **Early II-P Hα velocity.** Extrapolating the research's t^−0.46 law back to an 8.5 d peak gives about 16,500 km/s; observed about a week in is 11,000–13,000. | It is held before day 15 (R45d). The law comes from Fe II on the plateau. | Calibrated |
| 30 | **Colour temperature** for the plateau family and IIn is fixed at 6,000 K after cooling. Early IIn spectra are much bluer. | The CSM interaction light is not modelled. | Noted in the CSM caveat |

## 3D structure

Found while compiling `structure.md` (2026-11-12). Section numbers (§S…) refer to that file.

| # | What disagrees | Why it is interesting | Status |
|---|---|---|---|
| 31 | **The Crab's missing outer shell.** The pulsar wind nebula (about 4.1 × 2.9 pc) should sit inside freely expanding ejecta bounded by a forward shock at a considerably larger radius (Chevalier 2005), but no fast shell or outer shock has ever been detected: a deep Hα halo turned out to be scattered light (Tziamtzis et al. 2009), and a fast 4 M☉ shell could still hide below the limits (Sollerman et al. 2000). The filaments' 4–5 M☉ and low kinetic energy also fall short of a normal explosion. | Either the Crab came from a very low-energy (electron-capture?) explosion whose fast ejecta are faint and expanding into a low-density bubble, or its mass and energy sit somewhere unseen. The 3D view will draw a predicted shell that has never been observed. | Open; drawn faint and labelled "predicted, never detected" (§S9) |
| 32 | **The ejecta's outer edge.** Project 1's `EjectaKinematics` uses v_ej = √(10E/3M), the edge of a uniform sphere. The published broken power-law profile that Project 2 uses for the particles puts 0.1% of the mass beyond 1.72× (red supergiant) to 2.0× (blue supergiant, Type Ia) that speed. | The spec asked for the particles' edge to agree with Project 1's radius; no published profile does. The real outermost layers are fast (they make the early radio and the breakout), so the uniform sphere understates the size of the debris in the first years. | Open: needs a ruling (the 3D view's particles reach beyond the radius readout and the director's framing) |
| 33 | **Project 1's blast wave assumes uniform ejecta (n = 0).** Real ejecta have steep outer layers (n = 7 for Type Ia, 10–12 for core collapse). With them, Truelove & McKee's forward shock is 2.6× Project 1's at 10 yr, +45% at 100 yr and equal by 1,000 yr (10 M☉, 10⁵¹ erg, n = 12); for a Type Ia (n = 7) +211% at 10 yr. | It decides how big a young remnant looks. The 3D view keeps Project 1's radius so it never contradicts the remnant panel (§S4.3). Switching the panel's model to the class's n is one code path (Tang & Chevalier's b and α) but changes a panel. | Disclosed in the overlay; switch flagged for spec §9 |
| 34 | **The remnant model's citation.** Signals §7.1–7.2 and `RemnantEvolutionModel` credit the uniform-ejecta interpolation (λ = 2.01, ξ = 2.026, α = 1.25; n = 7: 1.06, α = 20.3) and arXiv:1607.06391 to Leahy & Williams 2017 (AJ 153, 239). That arXiv paper is Tang & Chevalier 2017 (MNRAS 465, 3793), which holds those constants; Leahy & Williams (arXiv:1701.05942) use Truelove & McKee directly and contain none of them. | The numbers are right and agree with Truelove & McKee's n = 0 solution to within 4% (§S4.3); only the attribution is wrong. | Annotated in structure.md (C9); signals §7 and the code's Source strings still to be corrected |
| 35 | **The profile's slow inner ejecta.** The broken power law with δ = 1 puts about 0.5 M☉ of a red supergiant's ejecta below 1,000 km/s; 3D simulations put the innermost ≈ 2 M☉ there (Wongwathanarat et al. 2015). | The analytic inner slope (δ ≈ 1) comes from self-similar arguments; real explosions leave more slow material (fallback, the reverse shock from the He/H interface). It changes where hydrogen mixed inward can sit. | Open (§S1.4) |
| 36 | **When the reverse shock reaches the centre.** Truelove & McKee's formulas reach zero at t* = 2.58 (n = 0) and 2.69 (n = 7), past their stated validity (≈ 2.1–2.2); Tang & Chevalier's simulation of n = 0 gives 2.37. | An 8–10% uncertainty in when the remnant's centre is reheated; it is past the lab's 1,000-yr window for core collapse, inside it for some Type Ia. | Annotated (§S4.2) |
| 37 | **SN 1987A's nickel and progenitor.** Only one older single-star model (B15, with a 6 M☉ helium core that the progenitor's luminosity rules out) mixes the bulk of ⁵⁶Ni out to the observed 3,000 km/s; "no single-star progenitor" satisfies all constraints (Utrobin et al. 2019). Binary-merger progenitors do better (Ono et al. 2020). | The mixing the 3D view draws for SN 1987A is the observed one, which current single-star 3D models do not produce. Linked to #13 (the 847 keV line). | Open (§S3) |
| 38 | **SN 1987A's outer rings are not symmetric.** The line joining the two outer rings' centres misses the supernova by ≈ 0.4″, and the southern ring cannot be circular if it shares the others' 43° tilt (Burrows et al. 1995, via Sugerman et al. 2005). | The rings are usually drawn as a symmetric hourglass; the real geometry hints at a binary merger's lopsided mass loss. The 3D view draws them symmetric (schematic). | Disclosed in the overlay (§S8) |
| 39 | **The Crab nebula's size.** Growth laws for a pulsar wind nebula in freely expanding ejecta give 1.0–1.5 pc for the preset's pulsar with E ≈ 10⁵⁰ erg and M_ej ≈ 4.6 M☉; the nebula's mean radius is ≈ 1.7 pc. | Within the large uncertainty of the Crab's E and M_ej, but all three published laws come out small. The nebula's expansion is known to have accelerated. | Open (§S9) |
| 40 | **Layer masses for the lightest stars.** Ruling P2 sets the CO/Si layer to M_CO − remnant − Ni. For the lightest core-collapse stars (S16 9 M☉: M_CO 1.40 M☉, remnant ≈ 1.35–1.4) and for fallback black holes this is zero or negative. | Electron-capture and fallback explosions eject almost no metal core; the layers must be clamped and the deficit taken from the next layer out. | Annotated with a clamping rule (§S2) |
