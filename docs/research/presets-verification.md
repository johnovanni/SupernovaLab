# Preset values: verification against the papers

Checked 2026-11-11/12 (Plan Task 13, Step 1) by reading arXiv / ADS abstracts and full text (ar5iv), the ATNF Pulsar
Catalogue, and, where a paper itself could not be opened, a later paper quoting it (marked "via"). Paraphrased; no long
quotes.

Status:
- **verified**: the paper (or the catalogue) gives this value.
- **verified (via)**: confirmed through a later paper or review that quotes the original; the original was not opened.
- **corrected**: presets.md's value differed; the value here is the paper's.
- **unverifiable**: not found this session. Such values are never asserted by a test; where a preset still needs one
  (an observer's dust), it is labelled [APPROX] in the preset's source.
- **derived [D]**: arithmetic on verified values, formula given.
- **research file's [SOURCE]**: a value signals.md marks [SOURCE] ("taken directly from the cited paper"), not re-opened
  here.
- **ruling**: kept because a controller ruling sets it (named), although the paper could not be opened this session.

Only verified, verified (via), corrected, derived, [SOURCE] or ruling values are pinned in `presets.json` or asserted
in `PresetValidationTests` (review M4).

## Values the spec §8 validations assert (Task 13 brief, Step 1)

| Preset | Quantity | presets.md | Checked value | Citation | Status | Used as |
|---|---|---|---|---|---|---|
| SN 1987A | Peak V (apparent) | ~2.9 at ~85–90 d [R] | **V = 2.975 at day 84.5** | Hamuy & Suntzeff 1990, AJ 99, 1146, as quoted by Suntzeff 1997 (arXiv:astro-ph/9707324) | corrected (2.9 → 2.975; same within rounding) | asserted ±0.5 mag (spec) |
| SN 1987A | Kamiokande-II events | 11 over ~13 s [R] | **11 events, 7.5–36 MeV, ~13 s** | Hirata et al. 1987, PRL 58, 1490 (ADS listing and a conference summary of the 11-event signal); event table in signals §1.5 (Vissani 2015) | verified (via) | fact; spec band 6–20 (see report) |
| SN 1987A | Kam-II sensitive mass | 2.14 kt (presets table) | **2,140 t** | Fiorillo et al. 2023, PRD 108, 083040 (arXiv:2308.01403), Sec. III and Table 5 | verified (via) | `NeutrinoModel.KamiokandeIIKilotons` |
| SN 1987A | Kam-II efficiency | "7.5 MeV traditional" threshold (signals §1.5) | **η = 0.932 / √(1 + (34/(12 − 7x + x²))²), x = E_e+/MeV > 4** (0.39 at 7.5 MeV) | Fiorillo et al. 2023, Eq. 8 (a fit to Kam-II's published trigger-efficiency figure) | verified | `NeutrinoModel.KamiokandeIIEfficiency` |
| SN 1987A | ⁵⁶Ni mass | ~0.07 [R] | **0.078 M☉** (Blinnikov's model) | Blinnikov et al. 2000, ApJ 532, 1132 (arXiv:astro-ph/9911205), Sec. 6 | verified (one author's set; 0.07 is the round literature value) | pinned |
| SN 2011fe | Peak magnitude | B ~9.9–10.0 [R] | **V_max = 9.98 ± 0.02** (MJD 55816.25), B_max = 9.94 | Pereira et al. 2013, A&A 554, A27 (arXiv:1302.1292) | verified | asserted ±0.3 mag (spec) |
| SN 2011fe | Peak magnitude (second source) | — | V_max = 9.99 ± 0.01, B_max = 10.00 ± 0.02; A_V = 0.08 ± 0.02 | Richmond & Smith 2012, JAAVSO (arXiv:1203.4013), Table 5 | verified | A_V of the preset |
| SN 2011fe | Distance | 6.4 Mpc [V] | **μ = 29.04 (6.43 Mpc)** | Shappee & Stanek 2011, ApJ 733, 124 (via Pereira 2013 and later papers); Nugent et al. 2011 use 6.4 Mpc | verified (via) | observer |
| Crab | P | 33.5 ms [R] | **P = 33.3924123 ms** at MJD 48442.5 | ATNF Pulsar Catalogue v2.6.1 (Manchester et al. 2005, AJ 129, 1993) | corrected (33.5 → 33.39) | pinned (via P0); asserted as a **consistency check** (ruling R47d): P0 and B are derived from this P and Ṗ, so the model returns them by construction; not independent agreement |
| Crab | Ṗ | ~4.2e-13 [R] | **4.20972e-13** | ATNF v2.6.1 | verified | pinned (via P0, B) |
| Crab | B, Ė, τ_c, S1400, d | 3.8e12 G, 4.5e38, ~1,260 yr [R] | B = 3.79e12 G, Ė = 4.5e38 erg/s, τ_c = 1.26e3 yr, **S1400 = 14 ± 3 mJy, d = 2.0 kpc** | ATNF v2.6.1 | verified | B pinned; L_1400 = 14 × 2.0² = **56 mJy kpc² [D]** pinned |
| SN 2008D | L_X | ~6e43 [R] | **L_X,p ≈ 6.1e43 erg/s** (0.3–10 keV), E_X ≈ 2e46 erg, ~400 s; unabsorbed peak flux 6.9e-10 erg cm⁻² s⁻¹ | Soderberg et al. 2008, Nature 453, 469 (arXiv:0802.1712) | verified | asserted ×3 (spec). **Ruling R47c: this tests the R28 0.3c cap at Soderberg's R*, not breakout physics** (the uncapped velocity is ~0.65c, so L_X does not depend on E or M_ej); the lab's 2 s bare-star flash misses the observed ~400 s wind breakout, said in the preset's Summary |
| Betelgeuse | Distance | 168 (+27/−15) pc [V] | **168 (+27/−15) pc**, parallax 5.95 mas | Joyce et al. 2020, ApJ 902, 63 (arXiv:2006.09837) | verified | observer; asserted at 168 pc |
| Betelgeuse | Distance (alternative) | 222 (+48/−34) pc | not re-opened | Harper et al. 2017 | unverifiable this session | not used |

## Preset pins (ruling R44: one author's consistent set)

| Preset | Pins | Checked value | Citation | Status |
|---|---|---|---|---|
| SN 1987A | E, M_ej, M_Ni, R | **E = (1.1 ± 0.3)e51 erg, M_ej = 14.7 M☉ (Table 1: 14.67), M_Ni = 0.078 M☉, R0 = 48.5 R☉** (= 3.374e12 cm [D]) | Blinnikov et al. 2000, ApJ 532, 1132 (model 14E1), Abstract, Sec. 3, 6, 7.2 | verified. E is the paper's best estimate (abstract and Sec. 8), cited as such (review M5; the reviewer reports Table 1's 14E1 itself at 1.03e51) |
| SN 1987A | NS mass (ruling R46) | — | **mass cut M_c = 1.6 M☉** (Sec. 3, "the mass cut is located at M_r = M_c = 1.6 M☉", an interior mass coordinate, so baryonic) → **1.444 M☉ gravitational [D]** by Lattimer & Yahil 1989, M_g = (−1 + √(1 + 0.3 × 1.6)) / 0.15 = 1.44369 (the engine's own conversion, outcomes §1.5) | Blinnikov et al. 2000, ApJ 532, 1132 (arXiv:astro-ph/9911205), Sec. 3 | verified (1.6 M☉); the gravitational value is derived, assuming the paper's mass coordinate is baryonic (it does not say) |
| SN 1987A | ⁴⁴Ti | 1.5e-4 [R, "verify"] | **(1.5 ± 0.3)e-4 M☉** | Boggs et al. 2015, Science 348, 670 (via a 2017 paper quoting it; a 2025 reanalysis finds 1.6 ± 0.5e-4) | verified (via) |
| SN 1987A | opacity κ | — | 0.10 cm²/g (the slider's top; Task 9 note: dome peaks ~83–86 d, closer to the observed 84.5 d) | — | choice, not a published value |
| Crab | P0, B, L_1400 | — | P0 = √(P² − 2PṖt) = **16.845 ms** (n = 3, t = 936.97 yr from 1054 July 4 (Julian) to MJD 48442.5); B = 3.2e19 √(PṖ) = **3.794e12 G**; L = **56 mJy kpc²** | ATNF v2.6.1 values [D] | derived |
| Cas A | ⁴⁴Ti | 1.25e-4 [V-ish] | 1.25 ± 0.3e-4 M☉ | Grefenstette et al. 2014, Nature 506, 339 (arXiv:1403.4978): the abstract gives no number; the value is the research file's (signals §5.3 [SOURCE]) and ruling R35's | unverifiable this session in the paper itself; kept per ruling R35 (consistent with Grefenstette 2017's 1.54 ± 0.21e-4 and INTEGRAL's 1.37 ± 0.19e-4 in signals §5.3) |
| SN 2011fe | M_Ni | 0.53 ± 0.11 [R] | **0.53 ± 0.11 M☉ by Arnett's rule (α = 1)**; 0.44 ± 0.08 with α = 1.2 | Pereira et al. 2013 | verified |
| SN 2014J | M_Ni | 0.62 ± 0.13 [V] / ~0.6 | **0.6 ± 0.1 M☉** (abstract); 10,000 ± 3,000 km/s | Churazov et al. 2014, Nature 512, 406 (arXiv:1405.3332) | verified |
| SN 2014J | 847 keV flux (assert) | — | (2.34 ± 0.74)e-4 photons cm⁻² s⁻¹, days 50–100 | Churazov et al. 2014 as in signals §5.3 [SOURCE] (abstract gives no fluxes) | research file's [SOURCE] value; used as the spec §8 target |
| SN 1998bw | E, M_ej, M_Ni | E 3e52, M_ej ~10, Ni 0.7 [V via summary] | **CO138: M_CO 13.8 M☉, E = 3e52 erg, M_Ni = 0.7 M☉, remnant 2.9 M☉ → M_ej = 10.9 M☉ [D]**, M_ms ~40 M☉ | Iwamoto et al. 1998, Nature 395, 672 (arXiv:astro-ph/9806382), full text | verified |
| GRB 980425 | E_p,i | 55 keV [APPROX, verify] (R36) | **55 ± 21 keV**; E_iso = (1.0 ± 0.2)e48 erg in the same table | Amati 2006, MNRAS 372, 233 (arXiv:astro-ph/0601553), Table 1 | verified (E_iso pinned at R36's 8.5e47 from Galama 1998 / signals §9.4; Amati's 1.0e48 agrees within its error) |
| GRB 980425 | E_iso, T90 | — | 8.5e47 erg, 23.3 s | Galama et al. 1998 as in signals §9.4 [SOURCE]; ruling R36 | research file's [SOURCE]; ruling R36 |
| SN 2008D | E, M_ej, M_Ni | E ~6e51, M_ej 3–5, Ni ~0.07 [R] | **Mazzali: E ≈ 6e51 erg, M_ej ≈ 7 M☉, M_Ni ≈ 0.09 M☉** (progenitor ~30 M☉; a black hole formed in their model); Soderberg: E 2–4e51, M_ej 3–5, Ni 0.05–0.1 | Mazzali et al. 2008, Science 321, 1185 (arXiv:0807.1695), abstract and text; Soderberg et al. 2008 | corrected (presets.md mixed the two papers; the preset takes Mazzali's set) |
| SN 2008D | R | ~1e11? [R] | **R* ≈ 7e10 cm** in the text, from the early UV/optical cooling-envelope emission (the Fig. 3 caption says ~1e11); the X-rays instead give a breakout radius ≳ 7e11 cm (a dense wind) | Soderberg et al. 2008 | verified (text value used). The mix of Mazzali's E/M_ej/Ni with Soderberg's R* is accepted and disclosed in the pin's Source (ruling R47c) |
| SN 2015bn | M_ej, P, B, E | 8.4, 2.1 ms, 0.9e14 G, ~1 foe [V] | **69-day rise fit, κ = 0.2: M_ej 8.4 M☉, P 2.1 ms, B 0.9e14 G; E_K ~1e51 erg; L_peak 2.3 ± 0.4e44 erg/s; z = 0.1136; Galactic E(B−V) = 0.022** | Nicholl et al. 2016, ApJ 826, 39 (arXiv:1603.04748), Table 1 and abstract | verified |

## Observer distances and dust

| Preset | Value | Citation | Status |
|---|---|---|---|
| SN 1987A | 51.4 ± 1.2 kpc (ring) | Panagia 2005, IAU Colloq. 192 (arXiv:astro-ph/0309416) | verified |
| SN 1987A | E(B−V) = 0.15 → A_V = 0.47 [D] | Blinnikov et al. 2000 (after Wampler 1988); others: 0.16 (Fitzpatrick & Walborn 1990, via), 0.17 ± 0.02 (Walker & Suntzeff 1990, via), Suntzeff 1997 uses 0.16 | verified; A_V ≈ 0.47–0.53 across them (the brief's 0.5–0.6 is the top of this) |
| Crab | 2.0 kpc; E(B−V) = 0.52 → A_V = 1.61 [D] | ATNF v2.6.1; Sollerman et al. 2000 (arXiv:astro-ph/0002374) | verified |
| Cas A | 3.4 kpc | Reed et al. 1995, ApJ 440, 706 (via later papers) | verified (via) |
| Cas A | A_V ~5 | presets.md [R] | unverifiable; labelled [APPROX] |
| SN 2011fe | 6.43 Mpc; A_V 0.08 | Shappee & Stanek 2011 (via); Richmond & Smith 2012 | verified |
| SN 2014J | 3.53 Mpc | Dalcanton et al. 2009 as in signals §5.3 [SOURCE] | research file's value |
| SN 2014J | A_V = 1.85 ± 0.11 (R_V = 1.4 ± 0.1; E(B−V) = 1.37 ± 0.03) | Amanullah et al. 2014, ApJL 788, L21 (arXiv:1404.2595) | verified |
| SN 1993J | 3.63 ± 0.34 Mpc | Freedman et al. 1994 (NASA ADS reprint record; adopted by later SN 1993J papers) | verified (via) |
| SN 1993J | A_V ~0.5 | presets.md [R] | unverifiable; labelled [APPROX] |
| SN 1998bw | 38 Mpc (H0 = 65) | Kulkarni et al. 1998 (arXiv:astro-ph/9807001) | verified |
| SN 1998bw | Galactic E(B−V) = 0.06 → A_V = 0.19 [D]; host negligible | Patat et al. 2001 (via later papers) | verified (via) |
| SN 2008D | 27 Mpc; A_V ≈ 1.9 | Soderberg et al. 2008 (Mazzali uses 31 Mpc, E(B−V) 0.65) | verified |
| SN 2023ixf | 6.85 ± 0.15 Mpc | Riess et al. 2022 (via several SN 2023ixf papers) | verified (via) |
| SN 2023ixf | E(B−V) = 0.039 (MW 0.008 + host 0.031) → A_V = 0.12 [D] | arXiv:2306.10284 and other SN 2023ixf papers (via search) | verified (via) |
| SN 2015bn | comoving distance 474 Mpc (D_L 528 Mpc) [D] from z = 0.1136; A_V = 0.07 [D] | Nicholl et al. 2016 | derived |
| Tycho | 2.8 ± 0.4 kpc; A_V = 1.86 ± 0.12 | Ruiz-Lapuente 2004 (arXiv:astro-ph/0309009, via search summary); Tian & Leahy 2011 give 2.5–3.0 kpc | verified (via) |
| Kepler | 5.1 (+0.8/−0.7) kpc | Sankrit et al. 2016 (arXiv:1512.05730) | corrected (presets.md's "4.8 (+1.4/−1.1), Sankrit 2005": Sankrit 2005 gave 3.9; 2016 gives 5.1) |
| Kepler | A_V = 2.7 ± 0.1 | Ruiz-Lapuente 2017 (via search summary) | verified (via) |
| N6946-BH1 | 7.72 ± 0.32 Mpc | Anand, Rizzi & Tully 2018 (TRGB; recommended by arXiv:1903.00173) | corrected (7.7 [R] → 7.72) |
| N6946-BH1 | Galactic E(B−V) = 0.303 → A_V = 0.94 [D] | Schlafly & Finkbeiner 2011 as adopted by Adams et al. 2017 (via) | verified (via) |
| N6946-BH1 | ~25 M☉ RSG; ~1e6 L☉ outburst in 2009 | Adams et al. 2017, MNRAS 468, 4968 (arXiv:1609.01283), abstract | verified |
| Betelgeuse | 168 pc; initial mass 18–21 M☉ | Joyce et al. 2020, Table 2 and Sec. IV | verified |
| Betelgeuse | A_V = 0.62 | Levesque & Massey 2020 (arXiv:2002.10463), MARCS fits with a Cardelli law (via search summary) | verified (via) |

## Preset inputs (best-match choices, review M6)

| Preset | Input | Basis | Status |
|---|---|---|---|
| sn1987a | 19 M☉, Z = 0.1, κ = 0.10 | initial mass 18–20 [R] (He core ~6 M☉ in Blinnikov's 14E1); Z = 0.1 is the lab's only route to a blue supergiant (the LMC is ~0.5 Z☉, said in the Summary); κ is the slider's top, a free choice that sets the dome's peak day | choice |
| sn1054 | 9.1 M☉ (electron capture) | ~8–10 M☉ progenitor, electron-capture interpretation (presets §2 [R]) | choice |
| casa | 18 M☉, Partial | 15–25 M☉ (Young et al. 2006 [R]); Partial gives IIb | choice |
| sn1993j | 15 M☉, Partial | primary 13–16 M☉ (Woosley 1994 / Stancliffe & Eldridge 2009, via) | verified (via) range |
| sn1998bw | 40 M☉, Z = 0.3, Full, Extreme | ~40 M☉ (Iwamoto 1998); Z = 0.3 is the lab's collapsar ceiling (host metallicity not verified) | verified mass; Z choice |
| sn2008d | 30 M☉, Full | ~30 M☉ (Mazzali 2008 abstract) | verified |
| sn2023ixf | 17 M☉ | 17 ± 4 M☉ (Jencson et al. 2023, presets §9 [V]) | verified (presets.md) |
| sn2015bn | 30 M☉, Full, Fast, Strong | "≳ 10 M☉ stripped" (Nicholl 2016); 30 M☉ Full gives a 9.3 M☉ He star ≈ M_ej 8.4 + a neutron star | choice |
| n6946bh1 | 25 M☉ | ~25 M☉ (Adams et al. 2017) | verified |
| betelgeuse | 19 M☉ | initial 18–21 M☉ (Joyce et al. 2020); the S16 rows at 20 and 22 M☉ fail to explode | verified range; choice inside it |
| Ia presets | near-Chandrasekhar | the lab's default Ia channel | choice |

## Known misses (disclosed in the presets' text, ruling R47b)

| Preset | Observed | Lab | Why |
|---|---|---|---|
| SN 2008D | X-ray flash ~400 s, ~2e46 erg; Swift caught it | bare-star flash ~2 s, ~100× less energy; the lab's Swift does not catch it | the real breakout was most likely through a dense wind (R30), not modelled |
| SN 1998bw | 45 mJy at 4.8 GHz on day 12 (Kulkarni 1998) | 3.7 mJy at 5 GHz on day 12 (9–12× fainter) | the lab's CSM shock formulas are Newtonian, capped at Γβ = 1 |
| SN 1987A | 847 keV 1.0e-3 ph cm⁻² s⁻¹ over days 159–250 (SMM) | 4.56e-5 averaged over the same days (~22× fainter) | no outward ⁵⁶Co mixing in the lab |

## Other values the presets show (facts)

| Preset | Fact | Value | Citation | Status |
|---|---|---|---|---|
| SN 1987A | IMB events | 8 in 5.6 s | Bionta et al. 1987, via Vissani 2015 (signals §1.5) | research file's [SOURCE] |
| SN 1987A | Baksan events | 5 in 9.1 s (11 + 8 + 5 = 24, spec §2.3) | Alekseev et al. 1987, via Vissani 2015 (signals §1.5) | research file's [SOURCE] |
| SN 1987A | 847 keV line (SMM) | 1.0 ± 0.25e-3 photons cm⁻² s⁻¹, 1 Aug–31 Oct 1987 | Matz et al. 1988, Nature 331, 416 (signals §5.3) | research file's [SOURCE] |
| SN 1987A | Ring 0.5–2 keV plateau | 8e-12 erg cm⁻² s⁻¹ from ~9,500 d | Frank et al. 2016, ApJ 829, 40 (signals §6.3) | research file's [SOURCE] |
| SN 1998bw | Radio peak | **45 mJy (4.8 GHz) and 49 mJy (8.6 GHz) on day 12**; 29 mJy at 1.38 GHz on day 45 | Kulkarni et al. 1998, Nature 395, 663 (arXiv:astro-ph/9807001), full text | verified (not in presets.md) |
| SN 1998bw | V peak; GRB peak flux | 13.62; 3.0 ± 0.3e-7 erg cm⁻² s⁻¹ | Galama et al. 1998 (signals §9.4) | research file's [SOURCE] |
| SN 1993J | He core, H envelope | 4.0 ± 0.5 M☉, 0.20 ± 0.05 M☉; primary 13–16 M☉ | Woosley et al. 1994, ApJ 429, 300 (abstract listing); Stancliffe & Eldridge 2009 | verified (via); not pinnable (no field), informs the 15 M☉ input |
| SN 1993J | E, M_Ni | ~1 foe, 0.07–0.1 | Woosley et al. 1994 | unverifiable; not pinned |
| Cas A | E, M_ej, M_Ni | ~2 foe, 2–4 M☉, ~0.06 | Laming & Hwang 2003; Willingale 2003; Eriksen 2009 | unverifiable; not pinned |
| SN 1054 | E; peak magnitude | 1e49–1e50 erg; −4 to −7 | Yang & Chevalier 2015; Collins et al. 1999 | unverifiable; not pinned or listed |
