# Supernova Lab: Explosion Outcomes from Published Models

Research notes for the app's outcome engine. The engine uses fitted or tabulated results from the literature; it does not run hydrodynamics.

**How to read this document**

- Units: masses in M☉; energies in B (1 B = 1 bethe = 10^51 erg); radii in R☉ or cm (1 R☉ = 6.957×10^10 cm); Z☉ is the solar metallicity.
- Every number is cited as (Author Year, Journal volume, page / arXiv id).
- **APPROXIMATION** marks a value that is not taken directly from a paper. Each one gives its reasoning. Treat these as placeholders the owner may replace.
- **NOT FOUND** marks a value I could not find in the sources I checked.
- Confidence labels, used in the final section:
  - **[ESTABLISHED]**: observationally anchored, or agreed across groups.
  - **[MODEL-DEPENDENT]**: one group's calibrated model; other groups differ in detail.
  - **[SPECULATIVE]**: a plausible mechanism with weak or indirect support.

---

## 0. Key references (abbreviations used below)

| Abbrev. | Reference |
|---|---|
| S16 | Sukhbold, Ertl, Woosley, Brown & Janka 2016, ApJ 821, 38 (arXiv:1510.04643) |
| SWH18 | Sukhbold, Woosley & Heger 2018, ApJ 860, 93 (arXiv:1710.03243) |
| E16 | Ertl, Janka, Woosley, Sukhbold & Ugliano 2016, ApJ 818, 124 (arXiv:1503.07522) |
| E20 | Ertl, Woosley, Sukhbold & Janka 2020, ApJ 890, 51 (arXiv:1910.01641) |
| OO11 | O'Connor & Ott 2011, ApJ 730, 70 (arXiv:1010.5550) |
| M16 | Müller, Heger, Liptai & Cameron 2016, MNRAS 460, 742 (arXiv:1602.05956) |
| PS20 | Patton & Sukhbold 2020, MNRAS 499, 2803 (arXiv:2005.03055) |
| H03 | Heger, Fryer, Woosley, Langer & Hartmann 2003, ApJ 591, 288 (astro-ph/0212469) |
| HW02 | Heger & Woosley 2002, ApJ 567, 532 (astro-ph/0107037) |
| W17 | Woosley 2017, ApJ 836, 244 (arXiv:1608.08939) |
| W19 | Woosley 2019, ApJ 878, 49 (arXiv:1901.00215) |
| F19 | Farmer, Renzo, de Mink, Marchant & Justham 2019, ApJ 887, 53 (arXiv:1910.12874) |
| Sch21 | Schneider, Podsiadlowski & Müller 2021, A&A 645, A5 (arXiv:2008.08599) |
| F12 | Fryer, Belczynski, Wiktorowicz, Dominik, Kalogera & Holz 2012, ApJ 749, 91 (arXiv:1110.1726) |
| GBP19 | Goldberg, Bildsten & Paxton 2019, ApJ 879, 3 (arXiv:1903.09114) |
| KW09 | Kasen & Woosley 2009, ApJ 703, 2205 |
| KB10 | Kasen & Bildsten 2010, ApJ 717, 245 (arXiv:0911.0680) |
| N17 | Nicholl, Guillochon & Berger 2017, ApJ 850, 55 (arXiv:1706.00825) |
| L16 | Lyman, Bersier, James, Mazzali, Eldridge, Fraser & Pian 2016, MNRAS 457, 328 (arXiv:1406.3667) |
| T19 | Taddia et al. 2019, A&A 621, A71 (arXiv:1811.09544) |
| Sm11 | Smith, Li, Filippenko & Chornock 2011, MNRAS 412, 1522 (arXiv:1006.3899) |
| Li11 | Li et al. 2011, MNRAS 412, 1441 (LOSS II; arXiv:1006.4612) |
| FGK06 | Faucher-Giguère & Kaspi 2006, ApJ 643, 332 (astro-ph/0512585) |

The S16 per-model data (presupernova models, explosion results, light curves) is public in the MPA-Garching archive, doi:10.17617/1.b (cited in S16 §2.3). **Recommendation:** build the app's single-star table from that machine-readable data, not from the ranges below. Section 1.4 explains why: I could not reliably extract per-model explosion values from the PDF tables.

---

## 1. Fate vs ZAMS mass: single stars at solar metallicity

### 1.1 Lower mass limits

| ZAMS mass | Outcome | Source and notes |
|---|---|---|
| < ~0.9 | Still on the main sequence; no stellar death within the age of the Universe. | **APPROXIMATION**: main-sequence lifetime t_MS ≈ 10 Gyr × (M/M☉)^-2.5, a textbook scaling not taken from the papers here. It gives t_MS ≈ 13 Gyr at 0.9 M☉, so 0.8 M☉ stars have not yet died. |
| ~0.9 to M_up | White dwarf (CO WD; ONe WD near the top of the range). Use the initial–final mass relation (IFMR) below. | Cummings et al. 2018, ApJ 866, 21 (arXiv:1809.01673) |
| M_up ≈ 8 (range ~7–9) | Upper limit for WD formation | H03 adopts ~9 M☉. The lowest observed SN II-P progenitor mass is m_min = 8.5 (+1/−1.5) M☉ (Smartt et al. 2009, MNRAS 395, 1409). Super-AGB stars span 7.5–9.25 M☉ (Poelarends et al. 2008, ApJ 675, 614, arXiv:0705.4643). |
| Narrow window just above M_up | Electron-capture SN (ECSN) from an O-Ne-Mg core | Poelarends et al. 2008: **9.0–9.25 M☉** for their fiducial solar-metallicity model; about 4% of all local SNe, upper limit ~20%. Doherty et al. 2015 (MNRAS 446, 2599, arXiv:1410.5431): the single-star ECSN window is **at most ≈0.2 M☉ wide**, and its position depends on mass loss and metallicity (models for Z = 0.02–0.0001). |
| ≥ 9.0 | Iron-core collapse | S16: 9.0 M☉ is the lightest model to reach iron-core collapse in KEPLER (S16 §1). |

**White-dwarf mass (IFMR).** The MIST-based fit (Cummings et al. 2018, Table/§5):

- 0.83 < M_i < 2.85: M_WD = 0.080 M_i + 0.489
- 2.85 < M_i < 3.60: M_WD = 0.187 M_i + 0.184
- 3.60 < M_i < 7.20: M_WD = 0.107 M_i + 0.471

The PARSEC-based fit, valid to 8.2 M☉, is: 0.0873 M_i + 0.476 (0.87–2.80); 0.181 M_i + 0.210 (2.80–3.65); 0.0835 M_i + 0.565 (3.65–8.20). The most massive WD in the fit is ~1.26 M☉ (GD50).

- **APPROXIMATION** for M_i from 7.2 up to M_up: extrapolate the PARSEC fit (≈1.17–1.25 M☉).

**ECSN properties.**

| Property | Value | Source |
|---|---|---|
| Explosion energy | ~0.1 B | Kitaura, Janka & Hillebrandt 2006, A&A 450, 345 |
| ⁵⁶Ni | ~0.003 M☉ (mostly from the neutrino-driven wind) | Wanajo et al. 2011, as quoted in S16 §4.2 |
| NS mass | ~1.25 M☉ gravitational | S16 §5.1; Schwab, Podsiadlowski & Rappaport 2010 (ApJ 719, 722) put a separate peak near 1.25 M☉. S16 notes that light Fe-core progenitors also produce ~1.23–1.25 M☉ NSs. |
| Binary effects | Close binaries may widen the ECSN channel | Podsiadlowski et al. 2004, ApJ 612, 1044. Qualitative; the exact range is **NOT FOUND**. |

### 1.2 The explodability picture (neutrino-driven, 1D calibrated)

Explodability is set by core structure, not by ZAMS mass alone. The outcome is **not a simply connected set** of masses (OO11; S16; SWH18; E16; M16).

**OO11 compactness.**

- ξ_2.5 = (2.5) / (R(M_bary = 2.5 M☉) / 1000 km), evaluated at core bounce (OO11 eq. 10).
- Explosion is likely for ξ_2.5 ≲ 0.45. BH formation is likely for ξ_2.5 ≳ 0.45.
- Both statements hold only for LS180/LS220-like equations of state. OO11 state that ξ_2.5 alone "is not always sufficient".

**E16 two-parameter criterion.**

- M4 is the enclosed mass (in M☉) where the entropy s = 4. μ4 = dm/dr at that point, in M☉ per 1000 km.
- A line in the (M4μ4, μ4) plane separates exploding from non-exploding models, with only ~1–2.5% misclassified across 621 models.
- Both criteria need the presupernova density profile, which the app will not have. Use them only to explain behavior, not as app inputs.

**S16 results (solar Z, central engines calibrated to SN 1987A and the Crab).**

| ZAMS range (M☉) | Outcome (S16 §1, §4.1) |
|---|---|
| 9–12 | All explode ("Crab-like"): low energy, low ⁵⁶Ni (Table 7). |
| 12–15 | Always explode (ξ_2.5 rises from ~0 to ~0.2). |
| 15–22 | Variable; "highly variable for even small changes in the initial mass", especially near 20 M☉. |
| 22–25 | "Very few or no successful explosions" for every engine: **BH by direct collapse**. |
| 25–27 | Island that explodes again. |
| 27–30 | Rarely explode. A few fallback BHs (W18 engine: 27.2 and 27.3 M☉ give BHs of 3.2 and 6.2 M☉; S19.8/N20 engines: BHs of 4.1–7.3 M☉ at slightly higher masses). |
| 30–~60 | Nothing explodes except with the strongest engines: BH. |
| ≳60 | Winds have stripped the star to a ~6–7 M☉ WR star with a more compact core. Some explode with the strong engines as SN Ib/c; results are "sensitive to the mass loss prescription". |

**IMF-weighted statistics (S16 Table 4, Salpeter IMF).**

| Engine | ⟨E⟩ (B) | ⟨M_NS,bary⟩ | ⟨M_NS,grav⟩ | ⟨M_BH⟩ He-core only / whole star | ⟨M_Ni⟩ low–high | SN fraction |
|---|---|---|---|---|---|---|
| W15 | 0.68 | 1.55 | 1.40 | 8.40 / 13.3 | 0.040–0.049 | 66% |
| W18 | 0.72 | 1.56 | 1.40 | 9.05 / 13.6 | 0.043–0.053 | 67% |
| W20 | 0.65 | 1.54 | 1.38 | 7.69 / 13.2 | 0.036–0.044 | 55% |
| N20 | 0.81 | 1.56 | 1.41 | 9.23 / 13.8 | 0.047–0.062 | 74% |

- About half of all successful SNe come from stars below 12 M☉ (S16 §4.2).
- SN II-P explosion energies range 0.1–2 B, typically ~0.7 B (S16 §7.1).
- NS gravitational mass range (W18): 1.23–1.68 M☉. Another passage in S16 gives a maximum of 1.77 M☉.

**Other groups agree qualitatively, not in detail.**

- **M16** (2120 solar-metallicity KEPLER models, 10–32.5 M☉, semi-analytic engine):
  - energies from a few 10^49 erg to >2 B;
  - NS masses 1.15–2.0 M☉;
  - iron-group yields up to 0.15 M☉;
  - "islands of explodability" above ~15 M☉, a drop in explosion probability near 20 M☉, and delayed BH formation between 27 and 30 M☉.
- **Sch21** (different code, single stars):
  - NSs from single stars up to ≈21.5 M☉ and ≈23.5–34 M☉;
  - BHs of ≈13 M☉ from ≈22–24 M☉;
  - all single stars >35 M☉ form BHs.
- **The location of the BH islands is model-dependent at the ±1–3 M☉ level.**
- SWH18 find multi-valued presupernova structure (different outcomes at the same mass) for 14–19 and 22–24 M☉.

**Observational cross-check.**

- SN II-P progenitors span 8.5 (+1/−1.5) to 16.5 ± 1.5 M☉ (Smartt et al. 2009, MNRAS 395, 1409). The upper limit was revised to ~18 M☉ (Smartt 2015, PASA 32, e016).
- The missing high-mass RSG progenitors (the "RSG problem") are consistent with BH formation at 18–25 M☉. The statistical significance is debated (Davies & Beasor 2018, MNRAS 474, 2116; 2020, MNRAS 493, 468).
- A failed-SN candidate is N6946-BH1 (Adams et al. 2017, MNRAS 468, 4968). Its interpretation is disputed.
- **Observed ⁵⁶Ni is lower than theory for SN II.**
  - Anderson 2019 (A&A 628, A7, arXiv:1906.00761) medians: SN II **0.032 M☉** (N=115); IIb **0.102**; Ib **0.163**; Ic **0.155**; Ic-BL **0.369**.
  - S16's 1987A-calibrated N20/W18 engines give ~0.05–0.15 M☉ for 12–27 M☉ stars and "nearly constant 0.07 M☉" above 13 M☉ (S16 Fig. 17).
  - **The theoretical ⁵⁶Ni for SN II is therefore about 2× the observed median.** Section 8 recommends a fix.

### 1.3 Presupernova structure grid (solar Z, single stars)

These rows are read from **S16 Table 2** (KEPLER, non-rotating, solar composition).

- M_f: final (presupernova) mass.
- M_He: helium-core mass, at the point where X(H) = 0.2.
- M_CO: CO-core mass, at the point where X(He) = 0.2.
- log R_f: presupernova radius in cm.

The derived columns were computed from those values:

- M_env = M_f − M_He: the H-rich envelope mass.
- ΔM_He = M_He − M_CO: the He-rich shell mass.
- R in R☉.

The mass-loss rates used were Nieuwenhuijzen & de Jager 1990 and Wellstein & Langer 1999 (S16 §2).

| M_ZAMS | M_f | M_He | M_CO | log R_f (cm) | R (R☉) | M_env (H-rich) | ΔM_He (He shell) | Wind loss M_ZAMS − M_f |
|---|---|---|---|---|---|---|---|---|
| 9.0 | 8.746 | 1.571 | 1.402 | 13.46 | 415 | 7.18 | 0.17 | 0.25 |
| 9.5 | 9.208 | 2.041 | 1.501 | 13.46 | 415 | 7.17 | 0.54 | 0.29 |
| 10 | 9.675 | 2.476 | 1.608 | 13.55 | 510 | 7.20 | 0.87 | 0.33 |
| 10.5 | 10.194 | 2.672 | 1.689 | 13.58 | 546 | 7.52 | 0.98 | 0.31 |
| 11 | 10.688 | 2.819 | 1.781 | 13.60 | 572 | 7.87 | 1.04 | 0.31 |
| 11.5 | 10.754 | 2.806 | 1.781 | 13.60 | 572 | 7.95 | 1.03 | 0.75 |
| 12 | 10.902 | 3.116 | 2.086 | 13.65 | 642 | 7.79 | 1.03 | 1.10 |
| 12.5 | 11.188 | 3.342 | 2.254 | 13.68 | 688 | 7.85 | 1.09 | 1.31 |
| 13 | 11.567 | 3.516 | 2.416 | 13.69 | 704 | 8.05 | 1.10 | 1.43 |
| 14 | 12.079 | 3.950 | 2.765 | 13.74 | 790 | 8.13 | 1.19 | 1.92 |
| 15 | 12.603 | 4.352 | 3.144 | 13.77 | 846 | 8.25 | 1.21 | 2.40 |
| 16 | 13.145 | 4.749 | 3.510 | 13.79 | 886 | 8.40 | 1.24 | 2.86 |
| 17 | 14.301 | 5.018 | 3.801 | 13.80 | 907 | 9.28 | 1.22 | 2.70 |
| 18 | 14.936 | 5.401 | 4.176 | 13.83 | 972 | 9.54 | 1.22 | 3.06 |
| 19 | 15.503 | 5.804 | 4.541 | 13.85 | 1018 | 9.70 | 1.26 | 3.50 |
| 20 | 15.848 | 6.207 | 4.970 | 13.87 | 1066 | 9.64 | 1.24 | 4.15 |
| 21 | 16.119 | 6.617 | 5.335 | 13.89 | 1116 | 9.50 | 1.28 | 4.88 |
| 22 | 16.218 | 7.026 | 5.776 | 13.91 | 1168 | 9.19 | 1.25 | 5.78 |
| 23 | 16.468 | 7.419 | 6.174 | 13.94 | 1252 | 9.05 | 1.24 | 6.53 |
| 24 | 16.062 | 7.848 | 6.601 | 13.96 | 1311 | 8.21 | 1.25 | 7.94 |
| 25 | 15.849 | 8.251 | 7.011 | 13.99 | 1405 | 7.60 | 1.24 | 9.15 |
| 26 | 15.462 | 8.688 | 7.385 | 14.00 | 1437 | 6.77 | 1.30 | 10.54 |
| 27 | 15.397 | 9.159 | 7.896 | 14.02 | 1505 | 6.24 | 1.26 | 11.60 |
| 28 | 15.120 | 9.521 | 8.289 | 14.03 | 1540 | 5.60 | 1.23 | 12.88 |
| 29 | 15.087 | 9.867 | 8.662 | 14.03 | 1540 | 5.22 | 1.21 | 13.91 |
| 30 | 13.805 | 10.359 | 9.101 | 14.03 | 1540 | 3.45 | 1.26 | 16.20 |
| 31 | 13.634 | 10.821 | 9.611 | 14.01 | 1471 | 2.81 | 1.21 | 17.37 |
| 32 | 13.414 | 11.270 | 10.070 | 13.99 | 1405 | 2.14 | 1.20 | 18.59 |
| 33 | 13.239 | 11.599 | 10.396 | 13.96 | 1311 | 1.64 | 1.20 | 19.76 |
| 35 | 13.663 | 12.561 | 11.426 | 12.16 | 21 | 1.10 | 1.13 | 21.34 |
| 40 | 15.336 | 14.682 | 13.581 | 11.91 | 12 | 0.65 | 1.10 | 24.66 |
| 45 | 13.018 | 13.018 | 13.018 | 10.83 | 1 | 0 | 0 (He-poor surface) | 31.98 |
| 50 | 9.817 | 9.817 | 9.817 | 11.58 | 5 | 0 | 0 | 40.18 |
| 55 | 9.380 | 9.380 | 9.380 | 11.72 | 8 | 0 | 0 | 45.62 |
| 60 | 7.289 | 7.289 | 7.289 | 10.49 | 0.4 | 0 | 0 | 52.71 |
| 70 | 6.408 | 6.408 | 6.408 | 10.63 | 0.6 | 0 | 0 | 63.59 |
| 80 | 6.368 | 6.368 | 6.368 | 10.62 | 0.6 | 0 | 0 | 73.63 |
| 100 | 6.036 | 6.036 | 6.036 | 10.58 | 0.5 | 0 | 0 | 93.96 |
| 120 | 6.160 | 6.160 | 6.160 | 11.55 | 5 | 0 | 0 | 113.84 |

**Notes on the grid**

- **Mass coverage.** S16 Table 2 has 0.25 M☉ steps from 9 to 13 M☉, 0.1 M☉ steps from 13.1 to 30 M☉, then 31, 32, 33, 35, 40, … 120 M☉. Interpolate linearly between rows, but **do not interpolate explodability** (§1.4).
- **Hydrogen mass vs envelope mass.** M_env is the mass of the H-rich layer, not the mass of hydrogen. Envelope helium mass fractions X_He in these models range **0.30–0.53** (S16 §7.1.1). So the hydrogen mass is M_H ≈ X_H × M_env, with X_H ≈ 0.45–0.68.
  - **APPROXIMATION**: use X_H ≈ 0.6 for 9–20 M☉ and ≈ 0.5 for 20–33 M☉; the envelope is more He-enriched in heavier stars.
- **Total helium mass.** M_He,total ≈ ΔM_He + X_He × M_env. For example, at 15 M☉: 1.21 + 0.35 × 8.25 ≈ 4.1 M☉.
- **35 and 40 M☉ rows.** These are compact (12–21 R☉) stars with ≤1.1 M☉ of H-rich material. They are IIb/IIL-like or, after a little more loss, Ib progenitors. Note that M_f rises from 35 to 40 M☉: wind mass loss is non-monotonic.
- **Rows at 45 M☉ and above.** M_f = M_He = M_CO means the surface He fraction fell below the 20% tag. These are WC/WO-like stars and Ic candidates.
- **Mass-loss sensitivity above 30 M☉.** At solar Z the final masses from 30 to 120 M☉ depend strongly on the adopted WR and LBV rates (S16 §2.3.3). Other grids give larger final masses for 60–120 M☉ stars, for example Limongi & Chieffi 2018 (ApJS 237, 13) and the lower WR rates tested in W19.
  - **[MODEL-DEPENDENT]**: treat the S16 masses for 60–120 M☉ as one realization.
- **S16 power-law fits** (9–20 M☉): R ∝ M_ZAMS^(3/4) and M_env ∝ M_ZAMS^(2/3) (S16 §7.1.3).

**Fits from W19 (useful when the table is not available).** He-core mass at He ignition (solar Z, W19 §2):

- M_He,i ≈ 0.0385 M_ZAMS^1.603 for M_ZAMS < 30
- M_He,i ≈ 0.50 M_ZAMS − 5.87 for M_ZAMS ≥ 30

This agrees with the HW02 pair-instability regime relation, M_He ≈ (13/24)(M_ZAMS − 20 M☉), which applies near 140–260 M☉ (HW02 eq. 1).

### 1.4 Explosion properties by mass band (S16, solar Z)

S16 Tables 7 and 8 give, for each progenitor, the ⁵⁶Ni mass (KEPLER | P-HOTB), E_exp, and the baryonic remnant mass.

- **Table 7 (Z9.6 engine, 9–12 M☉).** The PDF text extraction was offset by one row; I realigned it using S16's text, which says the 9.25 M☉ model gives 0.003 M☉ ⁵⁶Ni at 1.4×10^50 erg in KEPLER and 0.00659 M☉ in P-HOTB.
- **Table 8 (N20/W18 engines, 12.25–120 M☉).** The extraction was misaligned. **Only band ranges are reported below for Table 8.**
- **Action for the owner:** take per-model values from the electronic data (doi:10.17617/1.b).

**S16 Table 7: Z9.6 (Crab-calibrated) engine, solar Z.** M_rem is baryonic. The gravitational mass comes from §6.3.

| M_ZAMS | M_Ni (KEPLER / P-HOTB) | E_exp (B) | M_rem,bary |
|---|---|---|---|
| 9.0 | 0.004 / 0.006 | 0.11 | 1.35 |
| 9.25 | 0.003 / 0.007 | 0.14 | 1.39 |
| 9.5 | 0.006 / 0.009 | 0.18 | 1.41 |
| 9.75 | 0.014 / 0.018 | 0.35 | 1.45 |
| 10.0 | 0.027 / 0.031 | 0.60 | 1.45 |
| 10.25 | 0.029 / 0.034 | 0.63 | 1.47 |
| 10.5 | 0.018 / 0.027 | 0.46 | 1.48 |
| 10.75 | 0.031 / 0.046 | 0.69 | 1.47 |
| 11.0 | 0.018 / 0.019 | 0.31 | 1.54 |
| 11.25 | 0.019 / 0.022 | 0.36 | 1.53 |
| 11.5 | 0.025 / 0.038 | 0.64 | 1.50 |
| 11.75 | 0.018 / 0.018 | 0.26 | 1.59 |
| 12.0 | 0.034 / 0.043 | 0.66 | 1.53 |

**Band summary for the N20 engine.** Ranges are min–max over S16 Table 8 rows in each band. The labels track the S16 §4.1 bands.

| Band (M_ZAMS) | Explodes? | Remnant | E_exp (B) | M_Ni (KEPLER–P-HOTB) | M_rem,bary |
|---|---|---|---|---|---|
| 12–15 | yes | NS | 1.07–1.51 (W18: 1.01–1.38) | 0.055–0.079 to 0.085–0.104 | 1.56–1.78 |
| 15–22 | mostly yes, with failures | NS (some BH by direct collapse) | 0.45–1.96, typically ~1.0–1.3 | 0.05–0.12 to 0.07–0.15 | 1.37–1.92 |
| 22–25 | no | BH, direct collapse | none | 0 | BH ≈ M_He (see §1.5) |
| 25–27 | yes | NS | 0.7–1.7 | 0.07–0.15 to 0.09–0.17 | 1.58–1.88 |
| 27–30 | mostly no; a few fallback cases | BH (direct) or fallback BH 3.2–7.3 M☉ | fallback cases ~0.5 | ~0 for fallback | fallback BH 3.2–7.3 |
| 30–~60 | no (strongest engines only) | BH, direct | none | 0 | BH |
| ≳60 (WR, M_f ≈ 6–7) | some, with strong engines (W18/N20) | NS ~1.7–2.0 bary, or BH | ~0.4–1.0 | ~0.07–0.10 | **[MODEL-DEPENDENT]** |

**Ejecta mass:** M_ej = M_f − M_rem,bary. This neglects the small neutrino mass loss, which goes into the gravitational mass instead.

### 1.5 Remnant mass formulas

**Baryonic to gravitational NS mass.**

- Lattimer & Yahil 1989 (ApJ 340, 426), as used in F12 eq. 13: M_bary − M_grav = 0.075 M_grav².
- Inverse: M_grav = (−1 + √(1 + 0.3 M_bary)) / 0.15.
- Check: M_bary = 1.56 gives M_grav = 1.41, which matches S16 Table 4.
- S16 eq. 10 uses an equivalent radius-dependent form: BE / M_grav = 0.6β / (1 − 0.5β), with β = G M_grav / (R c²) and R = 12 km. Using R = 10 km lowers masses by ~0.02–0.04 M☉.

**Black hole, direct collapse (S16 §5.2).**

- The whole core collapses. The hydrogen envelope is loosely bound (net binding energy ~10^47 erg), so it can be ejected by the neutrino-mass-loss weak shock (Nadezhin 1980; Lovegrove & Woosley 2013, ApJ 769, 109).
- Bracket: M_BH = M_He (lower) to M_f (upper).
- Neutrino losses before the horizon forms are "unlikely to exceed … about 0.3 M☉".
- IMF averages: ⟨M_BH⟩ ≈ 9 M☉ (He core only) vs ≈ 14 M☉ (whole star) (S16 abstract).
- S16 judge the He-core mass the better match to observed BH masses.

**Black hole, fallback.** In S16 the fallback BH mass is always well below M_He:

- W18: 3.2 and 6.2 M☉;
- S19.8/N20: 4.1–7.3 M☉;
- in all cases less than the He-core mass of 9.2–10.2 M☉.

**Generic parametric alternative (F12).** This is useful for non-solar Z and stripped stars because it is keyed on the CO-core mass M_CO and the final mass M.

Rapid prescription:

- M_proto = 1.0
- M_CO < 2.5: M_fb = 0.2
- 2.5 ≤ M_CO < 6.0: M_fb = 0.286 M_CO − 0.514
- 6.0 ≤ M_CO < 7.0: f_fb = 1
- 7.0 ≤ M_CO < 11.0: f_fb = a1 M_CO + b1, with a1 = 0.25 − 1.275/(M − M_proto) and b1 = −11 a1 + 1
- M_CO ≥ 11: f_fb = 1

Delayed prescription:

- M_proto = 1.2 (M_CO < 3.5), 1.3 (3.5–6), 1.4 (6–11), 1.6 (≥ 11)
- M_CO < 2.5: M_fb = 0.2
- 2.5–3.5: M_fb = 0.5 M_CO − 1.05
- 3.5–11: f_fb = a2 M_CO + b2, with a2 = 0.133 − 0.093/(M − M_proto) and b2 = −11 a2 + 1
- ≥ 11: f_fb = 1

Then:

- M_fb = f_fb (M − M_proto) wherever f_fb is used;
- M_rem,bary = M_proto + M_fb;
- NS: M_grav from Lattimer & Yahil;
- BH: M_grav = 0.9 M_rem,bary.

Limits: F12 is monotonic in M_CO, so it does **not** reproduce the S16/Sch21 explodability islands. It is smooth and fast. Use it only as a fallback.

---

## 2. Metallicity effects

### 2.1 Winds and the H03 fate map

- **H03 premise.** Fate depends mainly on the He-core mass and the H-envelope mass at death. For single stars these are set by winds, which depend chiefly on Z. H03 assume a wind mass-loss rate Ṁ ∝ (Z/Z☉)^0.5 (H03 §2).
- **Hot-star line-driven winds.** Ṁ ∝ Z^0.85 (Vink, de Koter & Lamers 2001, A&A 369, 574).
- **WR winds (as used in W19).**
  - WC/WO: log Ṁ = −9.2 + 0.85 log(L/L☉) + 0.44 log Y_s + 0.25 log(X_Fe/X_Fe,☉).
  - WNE: log Ṁ = −11.32 + 1.18 log(L/L☉) + 0.6 log(X_Fe/X_Fe,☉).
  - So WR Ṁ ∝ Z^0.25 (WC) to Z^0.6 (WNE) through iron (W19 §2, from Yoon 2017, MNRAS 470, 3970).
  - Vink (2017) gives rates ~10× lower for a 6 M☉ He star (W19 §2). WR mass loss is uncertain by a factor of a few.

**H03 boundaries (H03 text and Table 1).** The figure's metallicity axis is schematic ("roughly logarithmic"), so the boundaries are qualitative in Z.

| Quantity | H03 value |
|---|---|
| WD / collapse boundary | ~9 M☉. O-Ne cores (ECSN or WD) at ~9–10 M☉. |
| BH by fallback | M_He ≳ 8 M☉ (M_ZAMS ≳ 25 M☉ "high" case; 20 M☉ "low" case) |
| BH by direct collapse | M_He ≳ 15 M☉ (~40 M☉ ZAMS without mass loss). At low Z, all stars >40 M☉ form direct BHs, up to the pair-instability regime. |
| SN subtypes | II-P needs an H envelope ≳ 2 M☉. II-L/IIb need ≲ 2 M☉ (a thin strip near total H loss). Ib/c have no H. |
| Single-star Ib/c | Non-rotating single stars become Ib/c above ~34 M☉ at solar Z; occurs only at about solar Z or above. |
| PPISN | M_He ≈ 40–65 M☉ (~100–140 M☉ ZAMS at Z ≈ 0) |
| PISN | M_He ≈ 65–133 M☉ (~140–260 M☉ ZAMS) |
| Direct BH | M_He > 133 M☉ (>260 M☉ ZAMS) |

### 2.2 How to vary the presupernova star with Z (APPROXIMATION)

No single paper provides a closed-form M_f(M_ZAMS, Z) consistent with S16. A defensible scheme:

1. **Core masses.** Take M_He,i and M_CO from S16 Table 2 (or the W19 fits). Treat them as **independent of Z** for M_ZAMS ≲ 30.
   - **APPROXIMATION**: low-Z cores are slightly larger, by roughly ≲10% in most grids. **NOT FOUND** as a single cited coefficient.
2. **Wind loss.** ΔM(Z) = ΔM☉(M_ZAMS) × (Z/Z☉)^α, where ΔM☉ = M_ZAMS − M_f from the §1.3 table.
   - α = 0.5 follows the H03 assumption.
   - α = 0.85 follows Vink et al. 2001. Use it where hot-star winds dominate, i.e. M_ZAMS ≳ 30.
3. **Final mass.** M_f(Z) = M_ZAMS − ΔM(Z).
   - If M_f(Z) > M_He: M_env = M_f − M_He.
   - Otherwise the star is bare. Erode the He star with the W19 relation (§3.3), scaled by (Z/Z☉)^0.25–0.6.
4. **Z = 0 limit.** No winds: M_f = M_ZAMS (Woosley, Heger & Weaver 2002, RMP 74, 1015). Many low-Z stars die as blue rather than red supergiants (S16 §2: the SN 1987A blue-supergiant models).
   - **APPROXIMATION** for the radius: R ~ 30–100 R☉ for Z ≲ 0.1 Z☉, vs 400–1500 R☉ for RSGs at Z☉. Basis: SN 1987A's progenitor (Sk −69°202) had R ≈ 2–4×10^12 cm ≈ 30–60 R☉ (S16 §2.2). **NOT FOUND** as a Z-dependent fit.
5. **Consequences.**
   - At low Z, stars above ~30 M☉ keep massive He cores. Most collapse directly to BHs with large masses (up to the pair-instability gap).
   - At Z☉ and above, winds strip stars ≳35–40 M☉ to WR stars. They make Ib/c SNe or lower-mass BHs (S16; H03).

### 2.3 Pulsational pair instability, pair instability, and the BH mass gap

| He-core mass at death | Outcome | Source |
|---|---|---|
| ≲ 30–35 | Normal core collapse | W17 |
| ~30 to ~64 (H03: ~40–65) | **PPISN**: pulses eject mass, then Fe-core collapse to a BH (~70–140 M☉ ZAMS). Transients last weeks to millennia, at L ~10^41 to >10^44 erg s⁻¹. No non-rotating model radiates more than 5×10^50 erg or has KE above 5×10^51 erg. | W17 abstract and talk table |
| ~64 to ~133 | **PISN**: complete disruption, **no remnant** (140–260 M☉ ZAMS) | HW02; W17 |
| > 133 | Direct collapse to a BH (≳100 M☉) | HW02 |

**PISN ⁵⁶Ni vs He-core mass** (HW02 Table 3, "Fe56" is ⁵⁶Ni):

| M_He | 70 | 80 | 90 | 100 | 110 | 120 | 130 | 133.3 |
|---|---|---|---|---|---|---|---|---|
| M_Ni | 0.0117 | 0.129 | 1.31 | 5.82 | 14.2 | 24.6 | 39.6 | 57.0 |

**PISN kinetic energy.** It reaches 93.5 B (9.35×10^52 erg) at M_He = 133.3 (HW02 text). Values at lower M_He are only in HW02 Fig. 1: **NOT FOUND** in text.

- **APPROXIMATION**: interpolate log E linearly from ~4 B at M_He = 65 to 93.5 B at 133. The ~4 B lower anchor is my reading of the trend, not a quoted value.

**BH mass gap.**

- Lower edge (maximum BH mass below the gap) ≈ **45 M☉** (F19).
  - It is robust to Z (shift ≈ 3 M☉), to mixing (≈ 1 M☉), and to winds (≈ 4 M☉).
  - It is sensitive to the ¹²C(α,γ)¹⁶O rate: it moves between **40 and 56 M☉** within 1σ (F19).
- W19 gives a maximum PPISN BH of **46 M☉**. W17: "No black holes between 52 and 133 M☉ are expected" from binaries.
- See also Marchant et al. 2019 (ApJ 882, 36) and Renzo et al. 2020 (A&A 640, A56) for PPISN ejecta.
- Pulses begin to strip mass seriously above a final He-star mass of ~37 M☉ (W19, standard mass loss). That corresponds to an initial He core of ~75 M☉ (~160 M☉ ZAMS); with halved mass loss, ~52 M☉.

**Metallicity ceiling for PISN.**

- PISN from H-rich very massive stars could occur up to **Z ≈ Z☉/3**, giving ~1 PISN per 1000 SNe locally and ~1 per 100 at z = 5 (Langer et al. 2007, A&A 475, L19, arXiv:0708.1970).
- PISN from WC-type progenitors need much lower Z.
- This threshold depends on uncertain wind rates **[MODEL-DEPENDENT]**.

**APPROXIMATION for the PPISN BH mass** (needed by the app):

- M_BH ≈ min(M_He,final, ~45) for M_He,final in 35–64. The ejected mass is the difference.
- **NOT FOUND**: a published closed-form M_BH(M_He). F19 and Marchant et al. 2019 show numerical curves only.

---

## 3. Binary stripping and spectral type

### 3.1 Observed type fractions

**Core-collapse SNe** (volume-limited LOSS sample; Sm11 Table 1, percent of all CCSNe):

| Type | II-P | II-L | IIn | IIb | Ib | Ic | Ibc-pec | All Ibc | Ibc + IIb |
|---|---|---|---|---|---|---|---|---|---|
| % | 48.2 (+5.7/−5.6) | 6.4 (+2.9/−2.5) | 8.8 (+3.3/−2.9) | 10.6 (+3.6/−3.1) | 7.1 (+3.1/−2.6) | 14.9 (+4.2/−3.8) | 4.0 (+2.0/−2.4) | 26.0 (+5.1/−4.8) | 36.5 (+5.5/−5.4) |

- Broad-lined Ic (Ic-BL) are included in Ic and make up ~1–2% of CCSNe (Sm11).
- All SNe (volume-limited): **57% II, 24% Ia, 19% Ibc** (Li11).
- Within SN II: II-L 10%, IIb 12%, IIn 9% (Li11).
- Sm11 conclude that single-star evolution with a standard IMF cannot reproduce the stripped-envelope fraction. Binary stripping is required.

### 3.2 Hydrogen and helium thresholds for the spectral class

| Class | Criterion | Source |
|---|---|---|
| II-P | H-rich envelope ≳ 2 M☉ | H03 |
| II-L | H-rich envelope ≲ 2 M☉ | H03. Light curves form a continuum, IIP → IIL → IIb, as H-envelope mass decreases (Hiramatsu et al. 2021, ApJ 913, 55). Short-plateau events are fit with H-rich envelopes of ~0.9–2.1 M☉ (Hiramatsu et al. 2021). |
| IIb | A little H. **≥ ~0.001 M☉** of H (outer mass fraction ≳ 0.01) already gives IIb spectra at early times (Dessart et al. 2011, MNRAS 414, 2985; arXiv:1102.5160). Envelope H masses of ~0.03–0.5 M☉ are typical of models (Woosley et al. 1994; Claeys et al. 2011, A&A 528, A131). | |
| IIb subtypes | Compact (cIIb) vs extended (eIIb) progenitors divide at an envelope mass of **~0.1 M☉** (Chevalier & Soderberg 2010, ApJL 711, L40) | |
| Ib | H below the IIb limit: ≲ 0.001–0.03 M☉. The ~0.03 M☉ upper limit for SNe Ib is from Hachinger et al. 2012 (IAU Symp. proceedings version). He I lines present. | |
| Ic | No visible He I. As little as **0.06–0.14 M☉ of He** suffices for optical He lines (Hachinger, Mazzali, Taubenberger et al. 2012, MNRAS 422, 70; arXiv:1201.1506). So a "true" Ic has M_He ≲ 0.06–0.14 M☉ unless the ejecta are strongly asymmetric. | |
| Ic (alternative view) | **Helium deficiency is not a prerequisite for SNe Ic.** Non-thermal excitation by ⁵⁶Ni is needed to show He I. Models with ~1.6 M☉ He show no optical He I after ~10 d if ⁵⁶Ni is unmixed or weakly mixed (Dessart et al. 2012, MNRAS 424, 2139; arXiv:1205.5349). | |
| Binary Ib/c progenitors | At solar Z: final masses up to ~7 M☉, retaining **0.16–1.5 M☉ of He** in the envelope (Yoon, Woosley & Langer 2010, ApJ 725, 940) | |

**Uncertainty: the Ib/Ic boundary is unsettled.** Hachinger 2012 (spectral NLTE) and Dessart 2012 (time-dependent NLTE with mixing) disagree on how much helium an SN Ic can hide: ~0.1 M☉ vs ≳1 M☉. The app can expose a "⁵⁶Ni mixing" toggle, or use the default in §8.

### 3.3 Presupernova masses of stripped stars

**W19 standard-mass-loss helium stars** (solar Z, Table 4). Initial He-star mass → final mass and CO core:

| M_He,i | 3 | 4 | 6 | 8 | 10 | 14 | 20 | 30 | 40 | 60 | 80 | 120 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| M_final | 2.45 | 3.15 | 4.45 | 5.64 | 6.75 | 8.04 | 10.39 | 14.81 | 19.55 | 29.53 | 39.83 | 60.78 |
| M_CO | 1.61 | 2.12 | 3.15 | 4.11 | 5.07 | 6.11 | 8.01 | 11.63 | 15.67 | 24.31 | 33.38 | 52.05 |

**W19 fits.**

- M_final ≈ 0.939 M_He,i^0.872 (M_He,i ≲ 10).
- M_final ≈ 0.463 M_He,i + 1.49 (above 10).
- Combined with §1.3: **M_final ≈ 0.0548 M_ZAMS^1.40** for M_ZAMS < 30 (stripped at He ignition, Case B).

**W19 radii and black holes.**

- He stars with M_He,i < 3.2 M☉ expand after He depletion. 1.6–2.4 M☉ stars exceed 10^13 cm, which matters for ultra-stripped and "Ib with extended envelope" events.
- Heavier He stars stay compact (~10^11–10^12 cm). **APPROXIMATION** for the heavier stars' radii: the only numbers given are 7×10^12 cm (2.5 M☉) to 1×10^12 cm (3.2 M☉) at Si ignition.
- Most stars with final masses of **7–30 M☉ make BHs**; the commonest binary BH mass is ~9 M☉; there is a possible gap at 10–12 M☉ (W19).

**Explosions of these He stars (E20).**

- Common Ib/c progenitors have final masses ~2.5–5.6 M☉. Collapse of heavier stars frequently gives BHs, with an "island" of explodability near M_final ≈ 10 M☉.
- Median NS 1.35–1.38 M☉. Median BH 9–11 M☉. BHs below 5 M☉ are rare but present: the lower mass gap is sparsely populated, not empty.
- Even generous ⁵⁶Ni gives peak L < 10^42.6 erg s⁻¹ (median 10^42.0–42.3), which is fainter than the brighter observed Ib/c. E20 propose magnetars as an extra power source.

**Stripping changes explodability (Sch21).**

- Single stars and Case C stripped stars: compactness peaks at **M_CO ≈ 7** (BHs from ≈ 22–24 M☉ ZAMS) and rises again at M_CO ≳ 11. All single stars ≳ 35 M☉ make BHs.
- **Case A/B stripped stars:** the peak shifts to **M_CO ≈ 8** (BHs of ≈ 9 M☉ from ≈ 32–35 M☉ ZAMS), with the rise at M_CO ≳ 15. These stars make BHs reliably only for M_ZAMS ≳ 70.
- NS-forming ZAMS ranges (Sch21 Table 1):

  | Case | NS-forming ranges (M_ZAMS) |
  |---|---|
  | Single | ≲ 21.5 and 23.5–34.0 |
  | Case A | ≲ 31.5 and 36.0–72.5 |
  | Case B | ≲ 31.5 and 34.0–67.5 |
  | Case C | ≲ 21.5 and 23.5–36.0 |

- NS masses: single average 1.46 M☉ (max 1.87); stripped average 1.42 M☉, effectively truncated near 1.6 M☉.
- Explosion energies: IIP 0.69 ± 0.17 B (calibration target); Ib/c 0.88 ± 0.31 B.
- Average ⁵⁶Ni: IIP 0.039 M☉; Ib/c 0.059 M☉.
- Stripping removed material until the surface H fraction fell below 1% (Sch21 §2.2). Residual H masses are **NOT FOUND** in Sch21.

**Core structure of stripped vs single stars.** Laplace et al. 2021 (A&A 656, A58; arXiv:2102.05036; 11–21 M☉, solar Z):

- binary-stripped stars have systematically more carbon at collapse;
- in about half the models the Si- and O-burning layers merge.

PS20 tabulate explodability on a grid of 3496 bare CO cores, vs (M_CO, initial central carbon fraction X_C). This is the natural lookup table for stripped stars, and S16-consistent.

---

## 4. Type II-P vs II-L vs IIn, and plateau scalings

### 4.1 What decides the subtype

**II-P vs II-L.** H-envelope mass (§3.2): a long plateau needs ≳ 2 M☉ of H-rich envelope (H03). Lower envelope masses, from strong RSG winds or partial binary stripping, give shorter, steeper plateaus (II-L), then IIb (Hiramatsu et al. 2021).

**IIn.**

- Interaction with dense circumstellar material (CSM) ejected shortly before core collapse.
- Inferred progenitor mass-loss rates are mostly ≳ 10^-3 M☉ yr⁻¹ in the decades before explosion (Moriya et al. 2014, MNRAS 439, 2917; arXiv:1401.4893).
- LBV-like eruptions or binary interaction are the proposed causes (Smith 2014, ARA&A 52, 487).
- IIn are not a single progenitor mass range. **No progenitor-property threshold** reliably predicts IIn from (M, Z) **[SPECULATIVE]**.
- IIn are 8.8% of CCSNe (Sm11).

### 4.2 Plateau luminosity and duration scalings

Definitions:

- E51 = E/10^51 erg.
- M10 = M/(10 M☉). In S16 and Popov this is the H-envelope mass; in GBP19 it is M_ej.
- R500 = R/(500 R☉).
- L50 = bolometric luminosity at day 50.
- t_p = plateau duration.

**Popov 1993 (ApJ 414, 712), normalized by S16 (T_eff = 6300 K, κ = 0.34 cm² g⁻¹; S16 eq. 13–14):**

- L50 = 1.82×10^42 E51^(5/6) M10^(−1/2) R500^(2/3) erg s⁻¹
- t_p,0 = 96 d × E51^(−1/6) M10^(1/2) R500^(1/6), with no ⁵⁶Ni

**S16 recommended refits** (181 KEPLER II-P models):

- **L50 = 1.85×10^42 E51^(5/6) M10^(−1/2) R500^(2/3) erg s⁻¹** (eq. 15)
- **t_p,0 = 88 d × E51^(−1/6) M10^(1/2) R500^(1/6)** (eq. 17)
- ⁵⁶Ni extension (eq. 19): **t_p = t_p,0 × (1 + C_f M_Ni E51^(−1/2) M10^(−1/2) R500^(−1))^(1/6)**
  - analytic C_f ≈ 21;
  - S16 best fit **C_f ≈ 50**;
  - poor for M_ZAMS < 12, where it overestimates.

**KW09, as corrected in S16 §7.1.1:**

- L50 = 1.26×10^42 E51^(5/6) M10^(−1/2) R500^(2/3) X_He,0.33^(−1) erg s⁻¹
- t_p,0 = 122 d × E51^(−1/4) M10^(1/2) R500^(1/6) X_He,0.33^(−1/2)
- X_He,0.33 is the envelope helium mass fraction divided by 0.33.

**GBP19 (MESA + STELLA; M_ej ≈ 9–21 M☉, E = 0.2–2 B, R = 433–1140 R☉, M_Ni ≤ 0.075):**

- log L50 = 42.16 − 0.40 log M10 + 0.74 log E51 + 0.76 log R500 (eq. 8; M10 uses M_ej)
- log t_p = 2.184 + 0.134 log M_Ni + 0.411 log M10 − 0.282 log E51 (eq. 12; for M_Ni ≳ 0.03; t_p in days)
- Inverse relations (eq. 22), where L42 = L50/10^42 and t_p,2 = t_p/100 d:
  - log E51 = −0.728 + 2.148 log L42 − 0.280 log M_Ni + 2.091 log t_p,2 − 1.632 log R500
  - log M10 = −0.947 + 1.474 log L42 − 0.518 log M_Ni + 3.867 log t_p,2 − 1.120 log R500
- Forward-fit RMS is 5–8%.
- Light curves alone are degenerate in (M_ej, E, R). That does not matter for the app, which runs in the forward direction.

**Plateau brightness range in S16.** Plateau luminosities span **10^41.5–10^43 erg s⁻¹**; common events have envelope masses 7–10 M☉ and radii 3–8×10^13 cm (S16 §7.1).

### 4.3 Red-supergiant radius

**Models.**

- S16 presupernova RSGs: log R = 13.46–14.03 cm, i.e. **~415–1540 R☉** (§1.3), rising with mass as R ∝ M^(3/4) from 9 to 20 M☉.
- GBP19 progenitors: 433–1140 R☉.

**Observations.** Galactic RSGs reach ~1500 R☉ (Levesque et al. 2005, ApJ 628, 973).

**APPROXIMATION** for the default: R ≈ 500 R☉ at 10 M☉ to ~1000 R☉ at 20 M☉. KEPLER radii are at the high end of the model range.

---

## 5. Rotation and magnetic fields

### 5.1 Collapsars and long GRBs

**Collapsar picture.**

- A rotating massive core forms a BH plus an accretion disk, which powers relativistic jets (Woosley 1993, ApJ 405, 273).
- MacFadyen & Woosley 1999 (ApJ 524, 262; astro-ph/9810274), for a 14 M☉ He core:
  - average accretion rate ~0.07 M☉ s⁻¹ for ~15 s;
  - neutrino-annihilation energy deposited along the axis (1–14)×10^51 erg;
  - jets beamed into ~1.5% of the sky;
  - jets take 5–10 s to break out.

**Required conditions.**

1. **BH formation:** a core too compact to explode by neutrinos, or one with large fallback.
2. **Enough angular momentum** in the core to form a disk: specific angular momentum above that of the last stable orbit of a few-M☉ BH.
   - **APPROXIMATION**: j ≳ 10^16 cm² s⁻¹. This is my order-of-magnitude reading of MacFadyen & Woosley 1999 and Woosley & Heger 2006 (ApJ 637, 914). The exact threshold is **NOT FOUND** in the abstracts.
3. **No H envelope,** so the jet can escape. Observed GRB-SNe are all Ic-BL.
4. **Low Z**, so that winds do not drain angular momentum.

**Chemically homogeneous evolution (CHE).** Yoon, Langer & Norman 2006 (A&A 460, 199; astro-ph/0606637):

- With very rapid initial rotation, CHE avoids the giant phase and keeps the angular momentum.
- GRB production is limited to **Z ≲ 0.004**. That is ~Z☉/5 for Z☉ = 0.02, or ~0.3 Z☉ for Z☉ = 0.0134 (Asplund et al. 2009, ARA&A 47, 481).
- Predicted GRB/SN ratio: ~1/200 globally and **~1/1250 at low redshift**.
- Minimum ZAMS mass ≈ 20 M☉ (slow semiconvection) or ≈ 12 M☉ (fast semiconvection), as summarized by later work (Cantiello et al. 2007).
- The initial rotation threshold for CHE is **NOT FOUND** exactly. **APPROXIMATION**: of order 0.4–0.5 of the critical (Keplerian) velocity.

**Observed GRB-SN properties** ("average" GRB-SN; Cano et al. 2017, Adv. Astron. 2017, 8929054):

- E_K ≈ 2.5×10^52 erg (σ 1.8×10^52)
- M_ej ≈ 6 M☉ (σ 4)
- M_Ni ≈ 0.4 M☉ (σ 0.2)

### 5.2 Broad-lined Ic / hypernovae (no GRB required)

Explosion parameters by subtype (Arnett-model fits):

| Sample | M_ej (M☉) | E_K (B) | M_Ni (M☉) | v_ph (km s⁻¹) |
|---|---|---|---|---|
| L16 Ic-BL | 2.9 ± 2.2 | 6.0 ± 5.0 | 0.32 ± 0.15 | 19 100 ± 5000 |
| T19 Ic-BL (34 objects, PTF/iPTF) | 4 ± 3 | 7 ± 6 | 0.31 ± 0.16 | – |
| L16 Ic | 3.0 ± 2.8 | 1.9 ± 1.3 | 0.22 ± 0.16 | 10 400 ± 1200 |
| L16 Ib | 2.6 ± 1.1 | 1.6 ± 0.9 | 0.17 ± 0.16 | 9900 ± 1400 |
| L16 IIb | 2.2 ± 0.8 | 1.0 ± 0.6 | 0.11 ± 0.04 | 8300 ± 750 |

Notes:

- L16 is Table 6, 38 stripped-envelope SNe.
- T19 conclude that all the SNe are strongly mixed.
- These observed Ib/Ic ejecta masses (~2–3 M☉) are lower than many single-star WR models predict. This is one reason binary stripping of lower-mass stars is favored (Sm11; L16; E20).

### 5.3 Magnetars and magnetar-powered superluminous SNe

**Spin-down formulas (KB10 eq. 1, 2, 14).** Assumptions: I = 10^45 g cm², R_NS = 10 km, sin²α = 1/2.

- Rotational energy: **E_p = 2×10^50 P10^(−2) erg = 2×10^52 P_ms^(−2) erg**.
- Spin-down time: **t_p = 1.3 yr × B14^(−2) P10^2**, i.e. ≈ 4.7 d × B14^(−2) P_ms^2.
- Luminosity, dipole spin-down: **L_p(t) = (E_p/t_p) / (1 + t/t_p)^2**.
- Peak estimate when t_p ≪ t_d: L_peak ≈ E_p t_p / t_d².
- Diffusion time: t_d = [3 M_ej κ / (4π v c)]^(1/2), with v = [(E_p + E_SN) / (2 M_ej)]^(1/2) and κ ≈ 0.2 cm² g⁻¹ (KB10 eq. 12).
- Definitions: P10 = P/10 ms; P_ms = P/1 ms; B14 = B/10^14 G.
- KB10 consider B ~ 5×10^14 G and P ~ 2–20 ms. The brightest events reach L > 10^44 erg s⁻¹. Magnetar activity may affect "a few percent" of CCSNe.
- Woosley 2010 (ApJL 719, L204; arXiv:0911.0698): the brightest SNe come from relatively modest fields, ~10^14 G. The light-curve peak is most sensitive to B.

**SLSN-I magnetar parameters** (N17, 38 SLSNe-I; medians with 1σ ranges):

| Parameter | Median | 1σ range |
|---|---|---|
| P | **2.4 ms** | 1.2–4 |
| B | **0.8×10^14 G** | 0.2–1.8×10^14 |
| M_ej | **4.8 M☉** | 2.2–12.9 |
| E_K | **3.9 B** | 1.9–9.8 |

- Engine power at peak is ~10^44 erg s⁻¹.
- The progenitors need to be among the fastest-rotating ~10% of massive stars, with M > 20 M☉.

**Rates.**

- SLSNe-I are ~**1/1700** (+1800/−720) of all CCSNe, and ~1/300 of stripped-envelope SNe (Frohmaier et al. 2021, MNRAS 500, 5142; arXiv:2010.15270).
- Other surveys find lower fractions, down to ~0.002%, depending on the luminosity cut.

**Magnetar births among NSs.**

- Fraction born as magnetars: **0.4 (+0.6/−0.28)**. Typical birth fields 3×10^14–10^15 G, decaying over ~10^4 yr (Beniamini et al. 2019, MNRAS 487, 1426; arXiv:1903.06718).
- A 2025 simulation-based population study finds a lower birth rate, ~1.8 (+2.6/−0.6) per kyr, implying a fraction of ≳12% (arXiv:2503.11875). **Uncertain by ~×3.**

### 5.4 Magnetorotational (MR) explosions

- Mösta et al. 2015 (Nature 528, 376; arXiv:1512.00838): 3D GRMHD of rapidly rotating core collapse. A large-scale dynamo builds a magnetar-strength field (**≥ 10^15 G**) quickly.
- 2D models produce MR-driven jets. 3D shows kink instabilities, so whether the jets survive is uncertain.
- Energies of MR explosions are **[SPECULATIVE]** for app purposes. Burrows et al. 2007 (ApJ 664, 416) obtained ~10^51–10^52 erg in 2D for very rapidly rotating cores.
- Proto-magnetars with P ~ 1 ms and B ~ 10^15–10^16 G have been proposed as GRB engines (Metzger et al. 2011, MNRAS 413, 2031) **[SPECULATIVE]**.

### 5.5 What is uncertain here

- The spin of presupernova cores depends on angular-momentum transport (Spruit–Tayler dynamo or not). Models with magnetic transport give slow pulsars (S16 §2: the W18 rotating model's inner 1.47 M☉ implies P ≈ 12 ms). Models without it give ms rotation.
- The fraction of massive stars that become collapsars, SLSNe or magnetars is set by the uncertain initial rotation distribution and binary interaction. Observed rates are the safest anchors:
  - GRB-SN: ~1/1250 of SNe locally (model; Yoon et al. 2006)
  - Ic-BL: ~1–2% of CCSNe (Sm11)
  - SLSN-I: ~1/1700 of CCSNe (Frohmaier et al. 2021)
  - magnetars: 12–40% of NS births

---

## 6. Neutron-star birth properties

| Property | Distribution | Source |
|---|---|---|
| Birth mass (theory, single stars) | IMF-weighted mean ≈ 1.40 M☉ (gravitational); W18 range 1.23–1.68; weak bimodality at ~1.25 and ~1.4 | S16 §5.1, Table 4 |
| Birth mass (theory, stripped) | Average 1.42, truncated near 1.6 (Sch21); median 1.35–1.38 (E20) | Sch21; E20 |
| Observed masses | Gaussian fits: double NSs 1.33 ± 0.09; recycled 1.54 ± 0.23; slow pulsars 1.49 ± 0.19. Recycled MSPs may be bimodal (1.388 and 1.814). | Özel & Freire 2016, ARA&A 54, 401 (arXiv:1603.02698) |
| Maximum NS mass | ≥ 2.08 ± 0.07 (PSR J0740+6620) | Fonseca et al. 2021, ApJL 915, L12 |
| Radius | R_1.4 = 12.33 (+0.76/−0.81) km (piecewise-polytrope EOS) or 12.18 (+0.56/−0.79) km (speed-of-sound EOS), 95% credible, NICER + multimessenger | Raaijmakers et al. 2021, ApJL 918, L29 |
| Radius (older spectroscopic) | 9.9–11.2 km | Özel & Freire 2016 (superseded by NICER). **Use 12 km.** |
| Initial spin, model 1 | Normal distribution: ⟨P0⟩ = 300 ms, σ = 150 ms (negative draws rejected); braking index n = 3 | FGK06 optimal model |
| Initial spin, model 2 | Log-normal from 56 NS–SNR associations: ⟨log10(P0/s)⟩ = −1.04 (+0.15/−0.2), σ = 0.53 (+0.12/−0.08); median ≈ 91 ms | Igoshev et al. 2022, MNRAS 514 (arXiv:2205.06823) |
| Magnetic field, model 1 | Log-normal: ⟨log10(B/G)⟩ = 12.65, σ = 0.55 | FGK06 |
| Magnetic field, model 2 | Log-normal: ⟨log10(B/G)⟩ = 12.44, σ = 0.44 | Igoshev et al. 2022 |
| Magnetar fields | ~3×10^14–10^15 G at birth | Beniamini et al. 2019 |
| Kick, model 1 | Single Maxwellian, σ = 265 km s⁻¹ (1D); mean 3D speed 400 ± 40 km s⁻¹ (pulsars < 3 Myr old) | Hobbs, Lorimer, Lyne & Kramer 2005, MNRAS 360, 974 (astro-ph/0504584) |
| Kick, model 2 | Two Maxwellians: weight 0.42 with σ1 = 77 km s⁻¹, plus 0.58 with σ2 = 320 km s⁻¹ (proceedings version: σ1 = 75, σ2 = 316) | Verbunt, Igoshev & Cator 2017, A&A 608, A57 (arXiv:1709.07448) |
| Kick, model 3 | Double-sided exponential per component: ⟨v_1D⟩ = 180 km s⁻¹ (3D mean 380 +40/−60) | FGK06 |

**Recommended kick model:** model 2 (Verbunt et al. 2017).

- About 5% of pulsars are born with v < 60 km s⁻¹ (Verbunt et al. 2017 A&A abstract, via search results).
- ECSN and ultra-stripped NSs are thought to receive smaller kicks (Podsiadlowski et al. 2004). **APPROXIMATION**: draw from the σ1 = 77 km s⁻¹ component only.

**BH kicks.** **APPROXIMATION**: momentum-conserving scaling v_BH ≈ v_NS × (1.4 M☉ / M_BH), and zero for direct collapse with no ejecta. This is commonly used (for example in F12-based population synthesis), but it is not a measured distribution.

---

## 7. Type Ia supernovae (white dwarf in a binary)

### 7.1 Progenitor channels (brief)

- **Single degenerate (SD).** A CO WD accretes from a non-degenerate companion and approaches the Chandrasekhar mass (~1.38–1.4 M☉ at ignition).
- **Double degenerate (DD).** Two WDs merge, or one disrupts the other. Includes sub-Chandrasekhar double detonations.
- Both channels probably contribute. Their relative share is unsettled (Maoz, Mannucci & Nelemans 2014, ARA&A 52, 107) **[MODEL-DEPENDENT]**.

### 7.2 Peak luminosity, ⁵⁶Ni, and the Phillips relation

**Arnett's rule** (Arnett 1982, ApJ 253, 785), as used by Stritzinger et al. 2006 (A&A 460, 793; astro-ph/0609232):

- **L_peak = 2.0×10^43 (M_Ni/M☉) erg s⁻¹**, for a 19-day bolometric rise.

**⁵⁶Ni range** (Stritzinger et al. 2006, 17 SNe Ia):

- from **~0.05–0.08 M☉** (SN 1999by, SN 1991bg; 91bg-like)
- to **~0.87–0.94 M☉** (SN 1991T)
- Normal SNe Ia: ~0.4–0.8 M☉.
- M_Ni correlates strongly with M_B and Δm15(B).

**Radioactive power** (Nadyozhin 1994, ApJS 92, 527):

- L(t) = M_Ni[M☉] × (6.45×10^43 e^(−t/8.8 d) + 1.45×10^43 e^(−t/111.3 d)) erg s⁻¹
- This is the instantaneous deposition for full trapping. It applies to all SN types.

**Phillips relation.**

- Phillips 1993 (ApJL 413, L105): brighter SNe Ia decline more slowly.
- Quadratic form (Phillips et al. 1999, AJ 118, 1766; astro-ph/9907052, Table 3; valid for 0.85 < Δm15(B) < 1.70):

  | Band | a | b | Scatter |
  |---|---|---|---|
  | B | 0.786 | 0.633 | 0.11 mag |
  | V | 0.672 | 0.633 | 0.09 mag |
  | I | 0.422 | 0.633 | 0.13 mag |

  M_max(band) = M_max(band; Δm15 = 1.1) + a [Δm15(B) − 1.1] + b [Δm15(B) − 1.1]²

- **Zero point (APPROXIMATION, derived):** the Phillips et al. 1999 H0 relation implies M_B(Δm15 = 1.1) ≈ 5 log10 H0 − 28.671. That gives −19.66 for H0 = 63.3 and **−19.36 for H0 = 73**. A modern calibration of the standardized SN Ia magnitude is M_B ≈ −19.25 (Riess et al. 2022, ApJL 934, L7; SALT2 standardization, not Δm15). **Use M_B,0 ≈ −19.3 ± 0.1.**
- Typical Δm15(B): ~0.85–1.0 for 91T-like, ~1.1 for normal, ~1.8–2.0 for 91bg-like. **APPROXIMATION** for 91bg-like: the quadratic fit is not valid above 1.7.

### 7.3 Ejecta mass

- **Chandrasekhar (near-M_Ch) models:** M_ej ≈ 1.38–1.4 M☉. The classic W7 deflagration has ⁵⁶Ni ≈ 0.6 M☉ and E_K ≈ 1.3 B (Nomoto, Thielemann & Yokoi 1984, ApJ 286, 644; Iwamoto et al. 1999, ApJS 125, 439). These W7 values are from memory: **[verify]**.
- **Measured from light curves:** **0.9–1.4 M☉**. Most fast-declining normal SNe Ia are significantly sub-Chandrasekhar, and M_ej correlates strongly with decline rate (Scalzo et al. 2014, MNRAS 440, 1498; arXiv:1402.6842).
- **Sub-Chandrasekhar detonations, WD mass → ⁵⁶Ni:**
  - Sim et al. 2010 (ApJL 714, L52; arXiv:1003.2917): WDs of 0.97–1.15 M☉ give **0.3–0.8 M☉ of ⁵⁶Ni**.
  - Shen et al. 2018 (ApJ 854, 52; arXiv:1706.01898): a **1.0 M☉** WD gives a median-brightness SN Ia.
  - **APPROXIMATION** for the app: linearly interpolate M_Ni from 0.3 at 0.97 M☉ to 0.8 at 1.15 M☉. Extrapolate below 0.97 toward ~0.05 at ~0.9 M☉; Sim et al. 2010 show the yield drops steeply. Ejecta mass = WD mass.

### 7.4 Subtypes and observed fractions

Fractions are from the Li11 volume-limited sample.

| Subtype | Share of SNe Ia | Properties | Remnant |
|---|---|---|---|
| Normal | ~70% | M_Ni ~0.4–0.8 | None |
| 91T-like | 9% | Overluminous, slow decline, M_Ni up to ~0.9 | None |
| 91bg-like | 15% | Subluminous, fast decline, red; M_Ni ~0.05–0.1 | None |
| 02cx-like (Iax) | 5% (Li11); **31 (+17/−13) Iax per 100 SNe Ia** (Foley et al. 2013) | Peak −14.2 > M_V > −18.9; low velocities 2000–8000 km s⁻¹; low ejecta mass | **Bound remnant possible** |

Notes:

- Li11's conclusions also quote 18% for 91bg-like and 12% for 91T + 02cx-like. The §3.2 values above appear to be the volume-limited ones.
- Foley et al. 2013 is ApJ 767, 57 (arXiv:1212.2209).
- Iax are commonly modeled as failed or pure deflagrations of near-M_Ch WDs that leave a bound WD remnant. For example, the N5def model ejects ~0.37 M☉ (including ~0.16 M☉ of ⁵⁶Ni) and leaves ~1.03 M☉ bound (Kromer et al. 2013, MNRAS 429, 2287). These model numbers are from memory: **[verify]**.
- Iax ⁵⁶Ni spans roughly 0.003–0.3 M☉ (review: Jha 2017, arXiv:1707.01110). **APPROXIMATION** for the range end-points.

**Remnant.** Normal, 91T-like and 91bg-like SNe Ia leave **no compact remnant** **[ESTABLISHED in models]**. A surviving companion is expected in SD scenarios.

---

## 8. Recommended model for the app

### 8.1 Inputs and outputs

**Inputs**

- M (ZAMS)
- Z/Z☉
- Stripping: none / partial / full / He-stripped
- Rotation: slow / fast / extreme
- Core B: normal / strong
- Separate Ia path: WD mass and channel

**Outputs**

- Fate
- SN type
- Remnant (NS / BH / none / WD)
- E_exp, M_Ni, M_ej
- M_H, M_He
- R_progenitor
- NS: P0, B, kick; or BH: mass
- Optional: L50, t_p, peak L

### 8.2 Decision procedure

**Step 0: has the star died yet?**

- If M < ~0.9: no death (main sequence) **[ESTABLISHED; threshold APPROXIMATION]**.

**Step 1: low-mass branch** **[ESTABLISHED, boundaries ±1 M☉]**

- 0.9 ≤ M < M_up = 8.0: WD. M_WD from the Cummings et al. 2018 IFMR (MIST fit; PARSEC fit beyond 7.2). No SN.
- M_up ≤ M < M_up + 0.2 (default 9.0–9.25 at Z☉, after Poelarends et al. 2008): ECSN **[MODEL-DEPENDENT]**.
  - Type II-P (low luminosity)
  - E = 0.1 B; M_Ni = 0.003
  - NS 1.25 M☉ (gravitational); small kick
  - M_ej = M_f − 1.36
  - Envelope and radius from the 9 M☉ row of §1.3
- Optional: when stripping ≠ none, allow the ECSN window to widen to ~8–11 (Podsiadlowski et al. 2004) **[SPECULATIVE]**.

**Step 2: presupernova structure** (M ≥ ~9)

- *Single star at Z☉:* interpolate the §1.3 table (S16 Table 2) for M_f, M_He, M_CO and R **[MODEL-DEPENDENT; ESTABLISHED in trend]**.
- *Other Z:* §2.2 scheme. Cores fixed; wind loss scaled as (Z/Z☉)^0.5 for M < 30 and (Z/Z☉)^0.85 for M ≥ 30; low-Z stars compact to BSG radii below ~0.1 Z☉ **[APPROXIMATION]**.
- *Stripping input.* The He-core and CO masses come from M via the W19 fit (§1.3).

  | Stripping | M_H (H mass) | Radius | M_He, M_CO |
  |---|---|---|---|
  | partial | 0.1 M☉ (allowed range 0.001–0.5) | ≈ 10^13 cm (eIIb) if M_H,env > 0.1 M☉; else ~10^11–10^12 cm (cIIb), after Chevalier & Soderberg 2010 | from W19, final mass from §3.3 |
  | full | 0 | ~10^11–10^12 cm, or larger for M_He,i < 3.2 (W19) | W19 Table 4 via M_final ≈ 0.0548 M^1.40 (M < 30); He shell ΔM_He = M_final − M_CO |
  | He-stripped | 0 | as for full | M_He,env = 0.05 M☉ (below the Hachinger limit) |

  **APPROXIMATION** for the partial-stripping defaults.

**Step 3: pair-instability regime** (any Z; from He-core mass at death) **[ESTABLISHED physics; edges ±5 M☉]**

| M_He,final | Fate |
|---|---|
| 64–133 | PISN (§2.3): no remnant; M_Ni from HW02 table; E from 4 to 93.5 B (**APPROXIMATION** interpolation); M_ej = M_f. Type: I (if stripped) or II (if H retained; Langer 2007 allows ≲ Z☉/3). |
| > 133 | Direct BH, M_BH = M_f; no SN. |
| ~35–64 | PPISN, then BH. M_BH ≈ min(M_He,final, 45) (F19; **APPROXIMATION** for the shape). Display ejected pulse mass = M_f − M_BH. Optional interacting transient (IIn-like if H present, Ibn/Icn-like if not) **[MODEL-DEPENDENT]**. |

**Step 4: core-collapse explodability (neutrino-driven, non-rotating baseline)**

- **H-rich single stars (stripping = none), M ≤ 30:** use the S16 N20 bands keyed on ZAMS mass, which stand in for core structure **[MODEL-DEPENDENT; the existence of BH islands is ESTABLISHED in theory]**.
  - Explode: 9–~22 (with stochastic failures between 15 and 22) and 25–27.
  - BH: 22–25 (direct) and 27–30 (direct, with occasional fallback BHs of 3–7 M☉).
  - **Recommended implementation:** a per-mass lookup from the S16 electronic table at 0.1 M☉ resolution. It reproduces the islands and is deterministic.
  - Alternative: mark 15–22 as "probabilistic" and show the user the odds.
  - For other Z, key the lookup on M_CO, not M_ZAMS. Use the §1.3 M_CO column as the conversion.
- **H-rich stars, M > 30:**
  - final He core ≳ 7.5 M☉: BH (direct);
  - wind-stripped WR with final mass ≲ 7.3 M☉ (S16 60–120 M☉ stars at Z☉): explosion as Ib/c is possible but engine-dependent. Default to explode with E ≈ 0.6 B, M_Ni ≈ 0.08 **[MODEL-DEPENDENT]**.
- **Stripped stars (partial, full, He-stripped; Case A/B):** use Sch21.
  - NS for M ≲ 31.5 and 34–~70.
  - BH (≈ 9 M☉) for 31.5–34 and for ≳ 70 (which at low Z becomes PPISN/PISN territory).
  - Cross-check with W19/E20: "most final masses 7–30 → BH", with an explodable island near M_final ≈ 10. The two disagree in detail **[MODEL-DEPENDENT]**.
  - Alternative: PS20 (M_CO, X_C) lookup.

**Step 5: explosion properties (successful neutrino-driven SN)** **[MODEL-DEPENDENT]**

| Quantity | Rule |
|---|---|
| E_exp, M ≤ 12 | S16 Table 7 (Z9.6 engine) |
| E_exp, M > 12 | S16 N20/W18 per-model values: typically 0.8–1.5 B (IMF mean 0.7–0.8 B) |
| E_exp, stripped | Sch21 Ib/c mean 0.88 ± 0.31 B |
| M_Ni, theory | S16 values: 0.003–0.046 (M ≤ 12), ~0.07–0.15 (M > 12) |
| M_Ni, calibrated default | **APPROXIMATION**: multiply the S16 M > 12 ⁵⁶Ni by ~0.5, or clip it to the observed medians (II: 0.032; Ib/c: ~0.16; Anderson 2019). Show both "theory" and "observed-calibrated" values if accuracy is the priority. |
| M_rem,bary | S16 table (NS 1.35–1.9 bary) or F12 delayed |
| M_grav | Lattimer & Yahil inversion (§1.5) |
| M_ej | M_f − M_rem,bary |
| Fallback BH | E ≈ 0.5 B, M_Ni ≈ 0, M_BH 3–7 M☉ (S16) |
| Failed SN | M_BH between M_He (default, after S16) and M_f − 0.3; optional weak envelope ejection (Lovegrove & Woosley 2013) |

**Step 6: spectral type (from the presupernova envelope)** **[ESTABLISHED in trend; thresholds MODEL-DEPENDENT]**

| Type | Condition |
|---|---|
| II-P | M_H,env ≥ 2 M☉ (H03) |
| II-L | 0.5 < M_H,env < 2 (APPROXIMATION for the lower edge) |
| IIb | M_H between 0.001 and ~0.5 M☉ of hydrogen (Dessart 2011; models) |
| Ib | M_H < 0.001 and M_He,visible ≥ 0.14 M☉ |
| Ic | M_He < 0.06–0.14 M☉ (Hachinger 2012) |
| IIn (override) | Only if the user enables "pre-SN eruption / dense CSM", or by drawing with the observed ~9% probability among H-rich explosions **[SPECULATIVE as a progenitor mapping]** |

- "He-stripped" forces Ic.
- **Helium-visibility switch for Ib vs Ic:** "⁵⁶Ni well mixed" (default; Hachinger threshold) vs "weak mixing", where Ic is allowed up to ~1.6 M☉ of He (Dessart 2012) **[uncertain]**.
- **Light-curve outputs.**
  - II-P/II-L: L50 and t_p from S16 eq. 15, 17 and 19 (C_f = 50), with M10 = M_H,env / 10, or GBP19 with M_ej.
  - Stripped SNe and SNe Ia: L_peak from Arnett's rule (§7.2).
  - Radioactive tail from Nadyozhin 1994 **[ESTABLISHED as formulas]**.

**Step 7: rotation and magnetic branches** (applied after Step 4)

**a) Collapsar / GRB** **[MODEL-DEPENDENT conditions; SPECULATIVE thresholds]**

- Condition: rotation = extreme, AND Z ≤ 0.004 (Z/Z☉ ≲ 0.3), AND (no H envelope via CHE or stripping), AND Step 4 says BH (or M_CO ≳ 7).
- Result: Ic-BL + long GRB, BH remnant.
- E_K ≈ 25 B (σ 18); M_Ni ≈ 0.4 (σ 0.2); M_ej ≈ 6 (σ 4) (Cano 2017).
- M_BH ≈ M_f − M_ej.
- *CHE option:* if rotation = extreme and Z ≤ 0.004 and 20 ≲ M ≲ 60, treat the star as chemically homogeneous. Set M_H = 0 even without a companion (Yoon et al. 2006). The upper mass bound is an **APPROXIMATION**.

**b) Magnetar** (core B = strong) **[MODEL-DEPENDENT]**

- Requires a successful explosion or NS formation; otherwise ignore B.
- B = 10^14–10^15 G: default 0.8×10^14 if fast/extreme (N17); 5×10^14 otherwise (KB10; Beniamini 2019).
- P0 by rotation: extreme = 1–2 ms (N17 1σ low end: 1.2); fast = ~2–5 ms (N17 median 2.4; upper 1σ 4); slow = from the normal-NS distribution (§6).
- Compute E_p and t_p (§5.3).
  - If stripped and E_p ≳ 10^51 erg with t_p ≈ days to weeks: **SLSN-I**. Use M_ej from the stripped star; E_K ≈ E_SN + E_p (capped by N17 typical 1.9–9.8 B). Show L(t) from KB10.
  - If H-rich: luminous II or SLSN-II **[SPECULATIVE]**.
  - If slow rotation: a normal SN with a magnetar remnant; no light-curve effect (E_p ≲ 10^49 erg for P ≳ 100 ms).

**c) Fast or extreme rotation with B = strong and BH-forming core**

- Magnetorotational explosion / hypernova, Ic-BL if stripped (Mösta 2015; Burrows 2007).
- E ~ 5–10 B and M_Ni ~ 0.3 (use the L16/T19 Ic-BL means) **[SPECULATIVE]**.

**d) Fast rotation without strong B**

- **APPROXIMATION**: rotational mixing enlarges cores. Raise M_He and M_CO by ~10–25% before Step 4 (Heger, Langer & Woosley 2000, ApJ 528, 368; Limongi & Chieffi 2018 give much larger cores at 300 km s⁻¹). **NOT FOUND** as a single coefficient.
- Flag the result as model-dependent.

**Step 8: NS and BH properties** **[ESTABLISHED as observed distributions; birth vs observed differences MODEL-DEPENDENT]**

- NS mass: from Step 5 (theory). Display "observed population" context from Özel & Freire 2016.
- NS radius: 12 km (Raaijmakers 2021: 12.2–12.3 km).
- P0: Igoshev 2022 log-normal (median 91 ms) or FGK06 normal (300 ± 150 ms), unless the magnetar branch applies.
- B: Igoshev 2022 log-normal (⟨log B⟩ = 12.44, σ = 0.44) or FGK06 (12.65, 0.55).
- Kick: Verbunt 2017 two-Maxwellian mix.
- BH mass: from Steps 3–5 and 7. BH kick: momentum-scaled **[APPROXIMATION]**.
- Respect the NS maximum mass: if M_grav > ~2.2 (Fonseca 2021 lower bound 2.08), convert to a BH.

**Step 9: Type Ia path (separate input: WD mass and channel)** **[ESTABLISHED for luminosity relations; channel MODEL-DEPENDENT]**

| Channel | M_ej | M_Ni | Subtype |
|---|---|---|---|
| Near-Chandrasekhar (SD) | 1.38 | 0.4–0.8 (default 0.6) | normal; 91T-like if M_Ni ≥ 0.8 (rare, 9%) |
| Sub-Chandrasekhar (DD/double detonation) | = M_WD | Sim et al. 2010 interpolation (0.97 → 0.3; 1.15 → 0.8); falls steeply below 0.97 | 91bg-like if M_Ni ≲ 0.1 (APPROXIMATION boundary) |
| Iax option | ~0.3–0.4 | 0.003–0.3 | Iax; bound WD remnant ~1 M☉ (Kromer 2013 **[verify]**) |

- Peak: L_peak = 2.0×10^43 M_Ni erg s⁻¹.
- M_B ≈ M_bol, with **APPROXIMATION** BC_B ≈ 0 at peak, where M_bol = 4.74 − 2.5 log10(L_peak / 3.828×10^33).
- Δm15(B): from the inverted Phillips 1999 quadratic with M_B,0 = −19.3. Clamp to 0.85–1.7 and flag extrapolation.
- E_K ≈ 1.2–1.5 B (**APPROXIMATION**, from W7-type models).
- Remnant: none, except Iax.

### 8.3 Confidence summary

| Component | Status |
|---|---|
| WD below ~8 M☉; IFMR | ESTABLISHED (±1 M☉ on M_up) |
| ECSN window position and width | MODEL-DEPENDENT (≤0.2 M☉ wide; may be absent) |
| BH islands at 22–25 and 27–30 M☉ (Z☉) | MODEL-DEPENDENT (locations differ by 1–3 M☉ between groups; existence broadly supported) |
| E_exp ~0.1–2 B, NS 1.2–1.9 M☉ | ESTABLISHED range; per-mass values MODEL-DEPENDENT |
| ⁵⁶Ni for SN II | Theory exceeds the observed median ×2: MODEL-DEPENDENT |
| II-P plateau scalings | ESTABLISHED (5–10% fits within model grids) |
| H/He thresholds for IIb/Ib/Ic | IIb/Ib ESTABLISHED to an order of magnitude; Ib/Ic **contested** |
| Final masses above 30 M☉ at Z☉; Z scaling | MODEL-DEPENDENT (WR/LBV rates uncertain ×2–10) |
| PISN / PPISN He-core ranges | ESTABLISHED in theory (±5 M☉); no unambiguous PISN observed |
| BH mass-gap edge ~45 (40–56) M☉ | MODEL-DEPENDENT on ¹²C(α,γ) |
| Collapsar / GRB conditions | MODEL-DEPENDENT; rotation thresholds SPECULATIVE |
| Magnetar SLSN parameters (N17 fits) | ESTABLISHED as fits; the mapping from progenitor to P and B is SPECULATIVE |
| MR explosions | SPECULATIVE |
| NS spin, B and kick distributions | ESTABLISHED as population fits (two competing forms each) |
| SN Ia Arnett rule, Phillips relation | ESTABLISHED; zero point depends on H0 |
| SD vs DD split; sub-Ch Ni yields | MODEL-DEPENDENT |
