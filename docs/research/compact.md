# Supernova Lab: compact remnants (the close-up)

Research for Project 3's Close-up window (spec `2026-11-15-supernovalab-project3-design.md` §3–§7, §8.1). Compiled
2026-11-15. Normative for the `Compact/` code and its tests: every later task takes its numbers from here, by section
number (§C1–§C11).

## READ THIS FIRST: confidence tags

- **[V]** = read this session in the cited paper's own text (arXiv full text or abstract, or the journal PDF).
  "[V via X]" = read this session in paper X, which quotes the cited paper; the cited paper itself was not opened.
- **[R]** = recalled, or read only as a search-result summary; the paper exists but the number was not opened this
  session. Verify before a test pins it.
- **[D]** = derived here by stated arithmetic, or by a short numerical calculation described in place (the scripts were
  run this session; the method is given so it can be repeated).
- **[APPROX]** = no published value found; a stated stand-in with its reasoning. Shown to the user as approximate or
  schematic.

Units: cgs unless stated. G = 6.674 × 10⁻⁸, c = 2.99792458 × 10¹⁰ cm/s, M☉ = 1.989 × 10³³ g (signals §0.2), so
GM☉/c² = 1.47670 km. 1 yr = 3.156 × 10⁷ s; 1 pc = 3.0857 × 10¹⁸ cm. "t" is time after core bounce (the lab's t = 0).
Black-hole lengths are in units of M = GM/c² unless stated. T_s is the **local** surface temperature; T_s^∞ =
T_s √(1 − r_g/R) the temperature seen from afar (r_g = 2GM/c²).

---

## Corrections to the spec and plan

| # | Spec or plan said | Sources say | Section |
|---|---|---|---|
| K1 | Spec §1 criterion 3 and inconsistency #54: Cas A's neutron star is "about 2.0–2.1 × 10⁶ K", so the lab's 1.8 MK is "about 10% low" | **Wrong frame, and the sign flips.** Heinke & Ho's 2.12 → 2.04 MK are the **local** (unredshifted) T_s of a carbon atmosphere for M = 1.648 M☉, R = 10.3 km (their Table 1 note; L_bol is the redshifted one) [V]; Shternin et al. 2023 call the fitted T_s "(non-redshifted)" [V]. Seen from afar that is T_s^∞ = **1.48–1.54 MK** [D]; Shternin et al. 2023's joint fit (M = 1.53 M☉, R = 13.5 km, log T_s ≈ 6.23–6.25) gives **1.39–1.44 MK** [D]. The lab's T_s^∞ at 340 yr is 1.60–1.89 MK for 2.0–1.2 M☉ (1.82 for 1.4 M☉, 12 km) [D], so the lab is **about 20–30% warm**, not 10% cool. Its local T_s (2.25 MK) is also above the observed local 1.70–2.12 MK. | §C9 |
| K2 | Plan §C4 example: ρ ≈ 5.4° P^(−1/2) "(Rankin 1993 / Kramer et al.)" | Published 10%-intensity half-widths at ≈ 1.4 GHz: **bimodal 4.9° and 6.3° P^(−1/2)** (Gil, Kijak & Seiradakis 1993; Kramer et al. 1994; 4.9° preferred for 80% of pulsars with P < 0.7 s), **5.6° P^(−0.5)** (Biggs 1990), 5.8° P^(−0.33) (Lyne & Manchester 1988 scaled) [all V via Kolonko, Gil & Maciesiak 2004]. 5.4° is not a published coefficient. Use 5.6° (single fit, inside the bimodal pair). | §C4 |
| K3 | Spec §4.3: the Crab's tilt is schematic 45° unless published | A published model value exists: **α = 62°** (Romani & Yadigaroglu 1995, tabulated by Ng & Romani 2004 Table 2 [V via NR04]); other light-curve models give 40°–70°. The torus gives the Crab's viewing angle **ζ = 61.3°** (inner torus) and its spin-axis position angle **124.0°** [V, NR04 Tables 1–2]. | §C4 |
| K4 | Spec §4.2: crust "about 1 km thick, about 8% of the radius" | True for **1.4 M☉** at 12 km (0.99 km, 8.3%) [D]. At the lab's fixed 12 km it is 1.22 km (10.2%) at 1.2 M☉ and **0.46 km (3.9%)** at 2.2 M☉ [D]. The drip line lies 0.43 km deep at 1.4 M☉ (0.19–0.53 km over 2.2–1.2 M☉) [D]. | §C5 |
| K5 | Spec §5: magnetar periods reach "about 2–12 s ... within the timeline" | Observed periods 2–12 s confirmed (no magnetar above 12 s; Olausen & Kaspi 2014 [V]). But the lab's fast/extreme magnetars (N17 field 0.8 × 10¹⁴ G, KB10 convention) are at **0.22 s** at 1,000 yr; only the slow-rotation magnetar (5 × 10¹⁴ G) reaches **1.39 s** [D]. The youngest known magnetar, Swift J1818.0−1607, spins at 1.36 s with a characteristic age of 240 yr (Esposito et al. 2020 [V]). See #57. | §C6 |
| K6 | Plan Task 3 test: Sgr A* ring 51.8 ± 2.3 µas from the shadow law × one ring-to-shadow factor | With M87's calibration (ring/shadow = 11.0/6√3 = 1.06 [D from V]) and GRAVITY's mass and distance, Sgr A*'s ring comes out **56.4 µas**, 2σ above 51.8 ± 2.3 [D]. EHT's own Sgr A* result is stated as a shadow deviation **δ = −0.08 ± 0.09 (VLTI prior)**, consistent with Kerr [V]. Test the shadow against δ, not the ring against one factor. See #55. | §C9 |
| K7 | Spec §6.4: collapsar starting spin and settled spin "about 0.9" | **Confirmed:** MacFadyen & Woosley 1999 take a_init ≈ 0.5 from the iron core's angular momentum and find a = 0.86–0.985 at 20 s across disk models ("to a ≳ 0.9") [V]. | §C8 |
| K8 | Spec §3.1: "a shock forms about 10–20 km out ... stalls at 100–200 km ... proto-neutron star 50–80 km at first" | **Confirmed in kind:** the bounce shock forms at the edge of the ≈ 0.6 M☉ inner core (≈ 11 km [D]); it peaks at 120–150 km at 40–80 ms (O'Connor & Ott 2011; O'Connor et al. 2018; Bollig et al. 2021) [V]; "100–200 km a few tens of ms after bounce" (Müller 2020) [V]. The neutrinospheres start **outside** the shock and are overtaken within milliseconds (O'Connor & Ott 2011) [V], which the drawing must respect (§C1 App). | §C1 |

---

## §C1 The first second

### C1.1 What the simulations agree on

- **Shock formation.** The bounce shock forms at a baryonic mass coordinate of ≈ 0.6 M☉, "roughly the edge of the inner
  core" (O'Connor & Ott 2011, s40WH07, LS180) [V]. Müller 2020 gives an inner core of "about 0.5 M☉" [V]. At a mean
  density of ≈ 2 × 10¹⁴ g cm⁻³ that is a radius of ≈ 11 km [D: r = (3M/4πρ̄)^(1/3)]. So the shock forms 10–15 km out.
- **The neutrinospheres start outside the shock.** "The neutrinospheres ... are initially exterior to the shock but are
  surpassed by the latter in a matter of milliseconds after bounce, leading to the ν_e deleptonization burst" (O'Connor
  & Ott 2011) [V]. The lab's burst peaks at ≈ 5 ms (signals §1.2d), so the shock crosses the ν_e-sphere at ≈ 3–5 ms.
- **Maximum and stall.**
  - 1D, s20 (six codes): maximum 141–150 km at 74–81 ms; it then recedes; the Si/O interface reaches the shock at
    ≈ 220 ms and gives a transient expansion (O'Connor et al. 2018) [V].
  - 1D, s40 (GR1D): maximum ≈ 120 km at 38 ms, "there it stalls, then slowly recedes" (O'Connor & Ott 2011) [V].
  - 3D, 18.88 M☉: "In all models the shock reaches a maximum radius around 150 km at 70 ms post bounce", a second
    expansion when the Si/O interface falls in at ≈ 200 ms (Bollig et al. 2021) [V].
  - General: the accretion shock "typically reaches a radius between 100–200 km a few tens of milliseconds after bounce
    and then recedes again" (Müller 2020) [V]; "the stalled shock at 150 km" (Burrows et al. 2020) [V].
- **Gain radius.** Before revival "the ratio of the shock and gain radius typically lies in the range R_sh/R_g = 1.5–2"
  (Müller 2020) [V]. The gain region forms once neutrinos start heating behind the stalled shock (Kitaura et al. 2006
  describe it forming after the shock has moved out) [V]; it does not exist in the first few ms [APPROX: no time given].
  After revival, accreted matter turns around at R_ret ≈ R_g ≈ 30 km (Bollig et al. 2021) [V].
- **Proto-neutron star radius** (density 10¹¹ g cm⁻³, the definition of O'Connor et al. 2018 and Bollig et al. 2021)
  [V]:
  - A PNS that "stays unrealistically large (50–60 km) during the whole simulated postbounce evolution (up to 1 s)" was an
    artefact of simplified transport (Hanke et al. 2013) [V]: real PNSs shrink from ≈ 50–80 km.
  - 22.9–36.2 km at the end of the 3D runs (0.40–0.74 s) (Bollig et al. 2021, Table 1) [V].
  - "only ≈ 25 km by the end of the simulations" (0.4–1.0 s); "many more seconds to one minute to cool and deleptonize"
    (Burrows et al. 2020) [V].
- **Revival (explosion) time.**
  - 3D 18.88 M☉: 0.30–0.43 s, defined as the time the maximum shock radius reaches 400 km (Bollig et al. 2021, Table 1)
    [V].
  - 3D Fornax, 9–60 M☉: 11 of 14 models explode within the first second; at their end times (0.40–1.04 s) the mean
    shock radii are 735–12,400 km, moving at 5,200–16,300 km/s; the three non-exploding models (13, 14, 15 M☉) have
    shocks at 69–90 km at 0.77–0.99 s (Burrows et al. 2020, Tables 2–3) [V; the table columns were re-aligned by hand].
  - Electron-capture (O-Ne-Mg) cores explode in 1D: heating beats advection at ≈ 60–80 ms and a wind sets in after
    ≈ 200 ms (Kitaura et al. 2006) [V].
- **Failed supernova.** In s40 the shock "has receded to ≈ 20 km" at 408 ms; "at BH formation the shock is typically at a
  distance of ≈ 20 km and there is very little mass between the shock and the PNS"; dynamical collapse takes ≈ 1 ms, in
  which the shock recedes by ≈ 8 km to ≈ 12 km (O'Connor & Ott 2011) [V].
- **Dependence on the progenitor.**
  - Black-hole formation time t_BH ∝ ξ_2.5^(−1.5) (O'Connor & Ott 2011; signals §1.3) [V there]; the lab already uses it
    (`RemnantModel.DirectCollapseTime`).
  - Quasi-stationary shock radius R_sh ∝ (L_ν ⟨E_ν²⟩)^(4/9) R_g^(16/9) Ṁ^(−2/3) M^(−1/3) (Janka 2012; Müller & Janka 2015,
    as Müller 2020 Eq. 45) [V; exponents read from a garbled extraction, R for their exact placement].
  - **NOT FOUND:** a published track of R_sh(t) as a function of compactness or remnant mass usable in the app. Bollig et
    al. find the first maximum "around 150 km at 70 ms" in all their models [V]. The tracks are labelled **"typical"**.
- **Shock distortion.**
  - SASI: in a 3D 27 M☉ model, bipolar sloshing and then a spiral mode with ℓ = 1 amplitudes and rms shock deformation
    up to ≈ 15–20 km on a mean shock of ≈ 100–200 km (Hanke et al. 2013, Fig. 2 axis ranges only; text gives no
    number) [V for the description, APPROX for the amplitude]. "In 3D models the SASI oscillations tend to be of smaller
    amplitude" than in idealised setups (Müller 2020) [V].
  - Burrows et al. 2020: the shock dipole grows with an e-folding time of ≈ 20 ms in the first 200 ms; it saturates "a
    factor of a few larger" in exploding than in failed models [V].
  - **NOT FOUND** as a single published fraction. [APPROX] distortion amplitude ≈ 0.1 R_sh (Hanke's axis ranges).
- **Infall ahead of the shock:** "|v_r| ≈ √(GM/r) (i.e. a large fraction of the free-fall velocity)" (Müller 2020)
  [V; the square-root sign was lost in extraction and is restored from "a large fraction of the free-fall velocity"].
  For M = 1.3–1.5 M☉ at 150 km: 33,900–36,400 km/s; at 1,000 km: 13,100–14,100 km/s [D].
- **The pre-collapse core.**
  - Iron core: "for the core derived from the 25 M☉ presupernova ... the iron core was 1.78 M☉. At the time Woosley &
    Weaver (1995) defined as the 'presupernova' (collapse velocity equal 1000 km s⁻¹), the radius of this core was
    still 2300 km" (MacFadyen & Woosley 1999) [V]. Iron cores grow to 1.2–2 M☉ (search summary) [R].
  - O-Ne-Mg core (electron capture, 8.8 M☉ star): 1.3 M☉ O-Ne-Mg plus ≈ 0.08 M☉ C-O, "the core radius of about
    1100 km" at a central density of 4 × 10¹⁰ g cm⁻³ (Kitaura et al. 2006, Nomoto's model) [V].
  - **NOT FOUND:** a radius law across iron-core masses. [APPROX] use 2,300 km for every iron core.

### C1.2 Typical tracks [APPROX: assembled from the anchors above; the [V] anchors are marked ✓]

Radii in km. "Typ" is the drawn value; low–high is the range a test checks. Times after bounce.

**Exploding core** (neutrino-driven; revival ≈ 0.3 s):

| t | R_sh low–high (typ) | R_gain low–high (typ) | R_PNS (ρ = 10¹¹) low–high (typ) |
|---|---|---|---|
| 1 ms | 10–25 (15) | — | 50–90 (70) |
| 3 ms | 25–60 (40) | — | 50–90 (70) |
| 10 ms | 60–110 (85) | — | 50–85 (68) |
| 30 ms | 90–140 (120) | 50–90 (70) | 45–80 (62) |
| 100 ms | 110–170 (145) ✓ | 60–110 (85) ✓ | 35–65 (50) |
| 200 ms | 100–220 (155) ✓ | 55–120 (85) | 28–50 (40) |
| 300 ms | 150–500 (300) ✓ | 40–100 (65) | 24–45 (35) |
| 500 ms | 400–3,000 (900) ✓ | 28–70 (45) ✓ | 22–37 (29) ✓ |
| 1 s | 1,400–13,000 (3,000) ✓ | 25–60 (35) ✓ | 20–30 (25) ✓ |

**Failed core** (shock never revives). Up to 100 ms as the exploding track. After that the times are given as a fraction
of t_BH, because the track is stretched to end at `FormationTimeS` (spec §3.2). Reference: t_BH ≈ 0.4–1 s.

| t | R_sh low–high (typ) | R_gain low–high (typ) | R_PNS low–high (typ) |
|---|---|---|---|
| 1, 3, 10, 30, 100 ms | as exploding | as exploding | as exploding |
| 0.4 t_BH | 70–150 (110) ✓ | 40–100 (63) | 30–50 (38) |
| 0.7 t_BH | 40–110 (70) ✓ | 25–70 (40) | 22–40 (28) |
| 0.95 t_BH | 20–90 (30) ✓ | — (gain layer gone) | 15–30 (20) ✓ |
| t_BH − 1 ms | 12–25 (20) ✓ | — | 12–20 (16) |
| t_BH (horizon forms) | — | — | horizon r₊ (§C7) |

(Failed 3D models at 0.77–0.99 s still sit at 69–90 km [V]; 1D models recede to ≈ 20 km [V]. The table spans both.)

**Revival time:** typical 0.3 s (0.2–0.45 s) for iron cores [V anchors]; 0.07 s (0.06–0.08 s) for an O-Ne-Mg core [V].

### App implementation (§C1)

- **Tracks:** the tables above, interpolated linearly in log t and log R between rows. Each track is
  `ModelDependent` and labelled "From simulations" and "typical".
- **The failed track** uses the exploding rows up to 100 ms. After that it uses the t/t_BH rows, with
  t_BH = `Remnant.FormationTimeS`. If t_BH < 0.25 s, the failed rows begin at 0.4 t_BH instead of 100 ms (monotone join).
  At t_BH: phase = BlackHole. Collapse from the "t_BH − 1 ms" row to the horizon is drawn over the last 1 ms.
- **Drawn proto-neutron star** = min(R_PNS, 0.9 R_sh). While the shock is inside the neutrinosphere (first ≈ 5 ms), the
  hot core is bounded by the shock.
- **Drawn gain shell:** none before 30 ms. In the failed track it is drawn only while R_gain > 1.1 R_PNS.
- **Revival:** phase = Stalled for 30 ms ≤ t < t_rev and Revived from t_rev, with t_rev = 0.3 s for iron cores and
  0.07 s for an electron-capture outcome. A magnetorotational outcome uses the exploding track with its note.
- **The stage ends** when the typical R_sh passes 3,000 km (≈ 1 s), or at t_BH for the failed track.
- **DistortionFraction** = 0.1 [APPROX], labelled approximate (`DistortionSchematicOnly = false`, with the source text
  "SASI and convection: about 10% of the shock radius (Hanke et al. 2013, figure ranges)"). Shapes stay schematic.
- **InfallSpeedKmS** = √(G M_PNS / R_sh), with M_PNS the remnant's baryonic mass (≈ 34,000 km/s at 150 km).
- **PreCollapseCoreRadiusKm:** 2,300 km for an iron core; 1,100 km for an O-Ne-Mg (electron-capture) core [V anchors,
  one value each, APPROX across masses].
- **Test ranges:** the low–high columns.

---

## §C2 Proto-neutron-star contraction (≈ 1 s to ≈ 1 minute)

- At the end of the 3D runs (0.4–1 s) the PNS radius is ≈ 25 km (Burrows et al. 2020) and 23–36 km (Bollig et al. 2021)
  [V].
- "By two seconds into the simulation, material at a density of 10⁻⁵ fm⁻³ is at 17 km. This is fairly close to the cold
  neutron star radius for GM3 (13.5 km)"; "between thirty and forty seconds the PNS becomes transparent to neutrinos"
  (Roberts 2012) [V].
- "It will require many more seconds to one minute to cool and deleptonize into the 'cold, catalyzed' state" (Burrows et
  al. 2020) [V]. Neutron stars "about twenty seconds after the birth ... become fully transparent for the neutrinos"
  (Gnedin et al. 2001) [V]; "the neutron star becomes transparent to neutrinos at t ~ 10–100 seconds" (Metzger et al.
  2011) [V].
- The star "contracts from about 30 km to its final radius of about 12–13 km" on ≈ 10 s timescales (Camelio thesis, via
  a search summary) [R].
- **NOT FOUND:** a published closed-form R(t). [APPROX] a power law in t fitted to 25 km at 1 s and Roberts' ≈ 17 km at
  2 s (scaled to the lab's 12 km final radius):

      R(t) = R_f [1 + (25 km / R_f − 1) (t / 1 s)^(−1.7)],   R_f = Remnant.RadiusKm (12 km)

  This gives 25.0 km (1 s), 16.0 (2 s), 14.0 (3 s), 12.8 (5 s), 12.3 (10 s), 12.04 (30 s), 12.01 (60 s) [D].

### App implementation (§C2)

- For 1 s ≤ t ≤ 60 s, a neutron-star outcome's PNS radius = the formula above. At 1 s it equals the §C1 typical 25 km,
  so the tracks join. From 60 s the radius is `Remnant.RadiusKm`.
- Test: R(60 s) within 1% of R_f (the plan's 11.9–12.1 km). Confidence `ModelDependent`.

---

## §C3 Early cooling (≈ 1 minute to 30 yr)

### C3.1 What theory gives

- **Initial state.** Gnedin, Yakovlev & Potekhin 2001 start their cooling runs isothermal at T^∞ = 10¹⁰ K, in the
  neutrino-transparent stage that begins ≈ 20 s after birth [V].
- **Thermal relaxation.** "The initial thermal relaxation stage lasts for t < 10–100 yrs; the crust remains thermally
  decoupled from the core, and the surface temperature reflects the thermal state of the crust" (Yakovlev & Pethick
  2004) [V]. "During this thermal relaxation the effective temperature stays almost constant at about 250 eV. When the
  cooling wave reaches the surface, the effective temperature drops sharply by as much as an order of magnitude in the
  fast cooling scenario, and by a factor of 2–3 in the slow cooling scenario"; "from 250 eV to 30 or 100 eV" (Gnedin et
  al. 2001) [V]. 250 eV is 2.9 MK [D]. These are cooling-curve temperatures, which in this group's papers are the
  redshifted T^∞ [R: the figure axis was not readable].
- **First weeks.** "The outer crust cools to 10⁹ K in less than a month, while the inner parts remain much hotter"; the
  surface temperature in the first 100 yr "is rather independent of the equation of state, stellar mass, or the core
  neutrino luminosity" (Gnedin et al. 2001) [V].
- **Core law.** In slow-cooling stars "T(0, t) ∝ t^(−1/6) throughout the neutrino cooling era" (Gnedin et al. 2001) [V],
  which is the law `NeutronStarCoolingModel` uses (T_i = 10⁹ K (t/yr)^(−1/6); signals §8.4).
- **Relaxation time:** crust relaxation 10–70 yr depending on the crust's physics (Gnedin et al. 2001, Table 2) [V].

### C3.2 The lab's own law, extended backwards [D]

`NeutronStarCoolingModel`'s formula (T_i law, Gudmundsson envelope with g₁₄ = 2.4, redshift for the remnant's M and R)
evaluated before 30 yr, for 1.4 M☉ and 12 km:

| t | 60 s | 1 h | 1 d | 1 wk | 30 d | 1 yr | 3 yr | 10 yr | 30 yr | 100 yr |
|---|---|---|---|---|---|---|---|---|---|---|
| T_s^∞ (MK) | 10.38 | 7.14 | 5.33 | 4.46 | 3.91 | 3.11 | 2.81 | 2.52 | **2.276** | 2.038 |

At 60 s this is ≈ 10 MK, the same order as Gnedin's T^∞ = 10¹⁰ K interior through the envelope relation (local
T_s ≈ 13 MK) [D]. Through 1–3 yr it passes Gnedin's ≈ 2.9 MK plateau. At 30 yr it **is** the model's own value, so the
join is exact. The model's band at 30 yr is [2.04, 2.28] MK (1.4 M☉; the 100-yr value to the 30-yr value) [D].

For other masses at 12 km [D]: 30-yr value / 100-yr value = 2.36/2.11 (1.2 M☉), 2.19/1.96 (1.6), 2.00/1.79 (2.0),
1.90/1.71 (2.2) MK.

### App implementation (§C3)

For 60 s ≤ t < 30 yr (`NeutronStarModel.EarlyStartS = 60`), a band labelled `ModelDependent`, source "Gnedin, Yakovlev &
Potekhin 2001; Yakovlev & Pethick 2004; the lab's slow-cooling law extended [APPROX]":

- **Value** = the cooling model's own formula at t: `SurfaceTemperatureK(InteriorTemperatureK(t), 2.4) × √(1 − r_g/R)`.
  It is continuous with the model at 30 yr by construction.
- **Low** = the model's 100-yr value (its own band floor before 100 yr).
- **High** = max(Value, P(t)), where P(t) is Gnedin's plateau, **2.9 MK** for t ≤ 10 yr. From 10 to 30 yr, P falls
  linearly in log t, from 2.9 MK to the model's 30-yr value. So at 30 yr the band is exactly the model's band.
- Check values (1.4 M☉, 12 km): 60 s: 10.38 [2.04, 10.38]; 1 yr: 3.11 [2.04, 3.11]; 3 yr: 2.81 [2.04, 2.90];
  10 yr: 2.52 [2.04, 2.90]; 30 yr: 2.276 [2.04, 2.276] MK [D].
- The X-ray panel is unchanged (spec C14). The difference from Gnedin's slow-cooling level after relaxation is logged
  (#58).

---

## §C4 Pulsars

### C4.1 Beam half-opening angle

- Lyne & Manchester 1988: ρ₁₀ ≈ 6.5° P^(−1/3) at 408 MHz, ≈ 5.8° P^(−0.33) at 1.4 GHz. Biggs 1990 (same data):
  ρ₁₀ ≈ 5.6° P^(−0.5). Rankin 1993a,b (≈ 1 GHz) found a bimodal distribution. Gil, Kijak & Seiradakis 1993 (≈ 1.4 GHz) and
  Kramer et al. 1994 give ρ₁₀ = 6.3° P^(−0.5) or 4.9° P^(−0.5); 4.9° is preferred in 80% of pulsars with P < 0.7 s
  [all V via Kolonko, Gil & Maciesiak 2004]. Rankin's own coefficients (outer and inner cones) were not opened [R].
- The P^(−1/2) form follows from a fixed emission height: ρ = √(9π h_em / (2Pc)) (Johnston & Karastergiou 2019, Eq. 1)
  [V]. ρ = 5.6° P^(−1/2) corresponds to h_em ≈ 200 km [D].
- Caveat: 600 Parkes pulsars give W₁₀ ∝ P^(−0.3) with large scatter, flatter than P^(−0.5) (Johnston & Karastergiou
  2019) [V]. The relation was built on ordinary pulsars (P ≳ 0.1 s). The Crab's radio comes from high in the
  magnetosphere [R].
- Values for ρ = 5.6° P^(−1/2) [D]: the Crab (33.4 ms) 30.6°; 0.1 s 17.7°; 1 s 5.6°; 5 s 2.5°. It reaches 90° at
  P = 3.9 ms.

### C4.2 When the magnetosphere forms

- A proto-neutron star's wind is mass-loaded by neutrino heating. The wind of a proto-magnetar "becomes
  ultra-relativistic and Poynting-flux dominated (σ₀ ≫ 1)" as the star cools; "the jet baryon loading decreases abruptly
  when the neutron star becomes transparent to neutrinos at t ~ 10–100 seconds" (Metzger et al. 2011) [V]. Search
  summaries of related work: σ ≈ 1 around 1 s, σ ≳ 10 by ≈ 3–10 s for millisecond magnetars [R].
- **NOT FOUND:** a formation time for an ordinary pulsar's force-free magnetosphere. [APPROX] **30 s**, mid-range of
  "neutrino-transparent at 10–100 s" (Metzger) and Gnedin's ≈ 20 s. Fallback could bury the field for much longer
  (not modelled; search summaries only [R]).

### C4.3 Magnetic inclinations for the preset pulsars

- **The Crab (sn1054):**
  - α = 62° (Romani & Yadigaroglu 1995 outer-gap fit), listed by Ng & Romani 2004, Table 2 [V via NR04].
  - Other light-curve models use 45°; the annular-gap model fits for 40°–65° at ζ = 63°; other authors give 50°–70°
    (search summaries) [R].
  - The viewing angle from the inner torus: **ζ = 61.3° ± 0.1°**; outer torus 63.03° (Ng & Romani 2004, Tables 1–2) [V].
  - The inclination grows by 0.62° ± 0.03° per century (Lyne et al. 2013, from the main-pulse–interpulse separation)
    [V]. No absolute α is given there.
- **Cas A's central compact object:** no pulsations (pulsed fraction < 12%; Heinke & Ho 2010) [V], so no inclination.
- **Other presets:** none observed as pulsars. SN 1987A has no detected pulsar (signals §8.3).

### C4.4 Proper-motion directions of the preset neutron stars

- **The Crab pulsar.**
  - Proper motion μ_α = −11.7 ± 0.4 ± 0.5 mas/yr, μ_δ = +4.2 ± 0.4 ± 0.5 mas/yr. The projected misalignment with the
    nebula's axis is 14° ± 2° ± 9° (Kaplan et al. 2008) [V].
  - That is a position angle of **289.7°** and 12.4 mas/yr, or 118 km/s at 2.0 kpc [D].
  - Earlier: PA 292° ± 10° (Caraveo & Mignani 1999) [V via NR04]. The spin axis lies at PA 124.0° (and 304°) (NR04)
    [V].
- **Cas A's central compact object** (Holland-Ashford, Slane & Long 2024) [V]:
  - Gaia-corrected astrometry: 436 ± 89 km/s at **158° ± 12°**.
  - Uncorrected astrometry: 275 ± 121 km/s at 177° ± 22°.
  - Both match the explosion-centre estimate of ≈ 350 km/s [V]; its direction, 168° ± 8°, is from a search summary [R].
  - Earlier: 570 ± 260 km/s (Mayer & Becker 2021) [R].
- **SN 1987A:** no neutron star detected directly, so no proper motion. **NOT FOUND.**

### App implementation (§C4)

- `BeamHalfAngleRad(P) = min(5.6° × (P / 1 s)^(−1/2), 90°)`. Source: "Biggs 1990, ρ₁₀ = 5.6° P^(−1/2); published fits
  4.9°–6.3° (Gil, Kijak & Seiradakis 1993; Kramer et al. 1994)". The Crab gets 30.6°.
- `LightCylinderKm(P) = cP / 2π`: 1,593.6 km at 33.4 ms [D].
- `MagnetosphereFormsS = 30 s` [APPROX] (range 10–100 s).
- Preset pins (ruling P3-4):
  - **sn1054**:
    - `magneticInclinationDeg = 62`, source "Romani & Yadigaroglu 1995 (outer-gap fit), via Ng & Romani 2004, ApJ 601,
      479, Table 2; other models 40°–70°".
    - `kickPositionAngleDeg = −14.3` (about 345.7°), measured from the projected spin axis's 304° end, because the lab's
      scene "north" is the projected spin axis (P3-3). Source "Kaplan et al. 2008, ApJ 677, 1201 (PA 289.7° [D]); spin
      axis PA 124°/304° (Ng & Romani 2004)".
    - With the beam checkbox on, ζ = α = 62°, consistent with the torus's measured ζ = 61.3°.
  - **casa**: `kickPositionAngleDeg = 158`, source "Holland-Ashford, Slane & Long 2024, ApJ 962, 82 (Gaia-corrected,
    436 ± 89 km/s at 158° ± 12°)". The CCO's spin axis is unknown, so this angle is measured from a schematic scene north;
    the readout says so.
  - No other preset gets a pin.

---

## §C5 The neutron star's layers (1.2–2.2 M☉)

### C5.1 Densities

- **Neutron drip:** "ρ_ND ≈ 4 × 10¹¹ g cm⁻³" (Chamel & Haensel 2008) [V]. With modern HFB functionals
  ρ_ND(ground state) = 4.3–4.4 × 10¹¹ g cm⁻³ (Chamel et al. 2015, via Zdunik et al. 2017) [V via Zdunik]. SLy gives
  ≈ 4 × 10¹¹, FPS ≈ 6 × 10¹¹ (Chamel & Haensel 2008) [V]. BPS 1971: 4.3 × 10¹¹ [R, via search summaries]. One
  Skyrme-HF survey gives 2.9–4.6 × 10¹¹ [R].
- **Inner crust:** from ρ_ND to ≈ ρ₀/3 ≈ 10¹⁴ g cm⁻³ (Chamel & Haensel 2008) [V]. "Nuclei disappear at a density
  ≈ 0.5ρ₀" (ρ₀ ≈ 2.8 × 10¹⁴) (Yakovlev & Pethick 2004) [V].
- **Pasta:** "The pasta phases cover a small range of densities near the crust-core interface with ρ ≈ 10¹⁴ g cm⁻³",
  "about 48% of the crust mass" for the FPS crust, absent in SLy (Chamel & Haensel 2008) [V]. Across crust models
  "more than 50% of the crust by mass and 15% of the crust by thickness comprises pasta, with medians of around 62% and
  30%" (Balliet et al. 2021) [V].
- **Core:** outer core n, p, e, μ up to ≈ 2ρ₀. The inner core (> 2ρ₀) is uncertain: "nucleon matter", "hyperonic matter",
  pion or kaon condensates, quark matter. "In massive stars the radius of the inner core may reach several kilometers ...
  in low-mass stars the inner core may be absent" (Yakovlev & Pethick 2004) [V].
- Thickness, as written in the sources: outer crust "a few hundred meters", inner crust "typically about one kilometer"
  (Yakovlev & Pethick 2004) [V]. Outer crust 0.44–0.47 km for a 1.4 M☉ star whose inner region is 12 km (Hempel &
  Schaffner-Bielich 2008, Table I) [V].

### C5.2 Depths for the lab's 12 km stars [D]

Zdunik, Fortin & Haensel 2017, Eq. 8 [V]: a crust layer at baryon chemical potential μ lies at

    1 − r_g/r = (1 − r_g/R) (μ₀/μ)²,   r_g = 2GM/c²

- μ₀ = 930.4 MeV (⁵⁶Fe at the surface) [V].
- μ at drip = m_n c² = 939.565 MeV [R: drip is where the neutron chemical potential reaches the neutron rest mass].
- μ at the crust–core boundary = 953.3 MeV (DH) or 954.6 MeV (NL3) (Zdunik et al. 2017, Table 1) [V].

Accuracy: < 1% of the crust thickness for M > 1 M☉ [V].

| M (M☉) | drip depth (km) | crust (km) | crust / R |
|---|---|---|---|
| 1.2 | 0.531 | 1.220 | 10.2% |
| 1.4 | 0.427 | 0.993 | 8.3% |
| 1.6 | 0.348 | 0.817 | 6.8% |
| 1.8 | 0.286 | 0.676 | 5.6% |
| 2.0 | 0.236 | 0.560 | 4.7% |
| 2.2 | 0.194 | 0.464 | 3.9% |

NL3's boundary adds ≈ 5% to the crust [D]. The 1.4 M☉ drip depth (0.43 km) matches Hempel & Schaffner-Bielich's
0.44–0.47 km [V/D].

### C5.3 When the crust forms

- Melting: T_m = Z²e²/(R_cell k_B Γ_m) ≈ 1.3 × 10⁵ Z² (ρ₆/A)^(1/3) (175/Γ_m) K, with Γ_m ≈ 175 (Chamel & Haensel 2008,
  Eq. 15) [V]. That gives ≈ 2 × 10⁸–3 × 10⁸ K at 10⁹ g cm⁻³ (Ni), ≈ 2.5 × 10⁹ K near drip (Z ≈ 36–40), and ≈ 5 × 10⁹ to
  10¹⁰ K at the crust's base [D, with Negele–Vautherin-like Z and cell A; R for the compositions].
- In the slow-cooling crust, e⁺e⁻ pair emission controls the temperature for t < 3 × 10⁻³ yr, until "the temperature
  drops below 5 × 10⁹ K"; the outer crust reaches 10⁹ K "in less than a month" (Gnedin et al. 2001) [V].
- Neutrons in the inner crust may become superfluid before the matter crystallises (Chamel & Haensel 2008) [V].
- **NOT FOUND:** a stated crystallisation time. [D/APPROX] The inner crust and the deep outer crust freeze when they fall
  below ≈ 5 × 10⁹ K, at ≈ 3 × 10⁻³ yr ≈ **1 day**. The shallow outer crust (T_m ≲ 3 × 10⁸ K) freezes over weeks to
  months. The surface ocean stays liquid.

### C5.4 Evidence

- **Glitches:** Vela's glitches need "at least 1.4% of the star's moment of inertia" in a reservoir, naturally the
  inner-crust neutron superfluid (Link, Epstein & Lattimer 1999) [V]. (Later work on crustal entrainment suggests the
  core may also take part [R].)
- **Crust cooling after accretion outbursts:** KS 1731−260 and MXB 1659−29 cool back to equilibrium temperatures of
  70.0 ± 1.6 eV and 51.6 ± 1.4 eV. "The crust in both sources cools very rapidly"; KS 1731−260 cools faster by a factor
  of ≈ 2 (Cackett et al. 2006) [V].

### App implementation (§C5)

Layers centre outward, for mass M (1.2–2.2 M☉) and the remnant's R. Radii from Eq. 8 above [D]. Each boundary is
r_b = r_g / (1 − (1 − r_g/R)(μ₀/μ_b)²).

| Layer | Inner edge | Outer edge | Label | Confidence |
|---|---|---|---|---|
| Inner core | 0 | 0.5 R_cc [APPROX: "may reach several km"] | "Inner core: unknown" | Speculative |
| Outer core (n, p, e, μ) | 0.5 R_cc | R_cc (μ = 953.3 MeV) | "Outer core: neutrons, protons, electrons, muons" | Established |
| Nuclear pasta | R_cc | R_cc + 0.30 (R_ND − R_cc) | "Nuclear pasta" | ModelDependent |
| Inner crust | top of pasta | R_ND (μ = 939.565 MeV) | "Inner crust: nuclei in a sea of free (superfluid) neutrons" | Established |
| Outer crust | R_ND | R | "Outer crust: a lattice of nuclei and electrons" | Established |
| Atmosphere | R | R (a labelled line) | "Atmosphere: centimetres thick" | Established |

- The pasta's 0.30 of the inner crust's thickness is the Balliet et al. median (30% of the crust, applied to the inner
  crust) [APPROX]. The range is 0–43%, and the pasta is absent in some models.
- `DripDensityGcm3 = 4.3e11` [V via Zdunik, Chamel et al. 2015: 4.3–4.4 × 10¹¹]. It is inside the plan's 4.0–4.4 × 10¹¹.
- `DripDepthKm(1.4, 12) = 0.427`; test range 0.40–0.47 km (Eq. 8 to Hempel & Schaffner-Bielich).
- Crust/R at 1.4 M☉, 12 km = 0.083. The plan's 0.05–0.11 holds for 1.4 M☉ only; at 2.2 M☉ it is 0.039.
- `CrustFormationTimeS = 86,400 s` (1 day) [D/APPROX], the same for every neutron-star outcome (range 1 h – 1 month).
- Drip line tooltip: "Neutron drip, about 4.3 × 10¹¹ g/cm³ (Baym, Pethick & Sutherland 1971; modern models 4.3–4.4 ×
  10¹¹). Established theory, observed indirectly: pulsar glitches (Link et al. 1999) and crusts cooling after
  accretion outbursts (KS 1731−260, MXB 1659−29; Cackett et al. 2006)."

---

## §C6 Magnetars

- **Periods:** "the reason for the lack of magnetar spin periods longer than 12 s is not well established", and
  magnetar periods span a "small range" (Olausen & Kaspi 2014, McGill catalogue of 26 magnetars and candidates) [V].
  Typical 2–12 s; the youngest, Swift J1818.0−1607, is at 1.36 s, B ≈ 7 × 10¹⁴ G, characteristic age 240 yr (Esposito et
  al. 2020) [V].
- **Field decay:** typical birth fields 3 × 10¹⁴–10¹⁵ G decaying on **≈ 10⁴ yr**, which implies a maximum period of
  ≈ 13 s (Beniamini et al. 2019) [V]. That is outside the lab's 1,000-yr timeline. **Not modelled**; the readout notes
  it.
- **The lab's magnetar spin at 100 / 1,000 yr** [D: P = P₀ (1 + t/t_p)^(1/2) with KB10's t_p, as `PulsarModel`]:

  | Rotation | P₀ | B | P(100 yr) | P(1,000 yr) |
  |---|---|---|---|---|
  | Extreme | 1.2 ms | 0.8 × 10¹⁴ G | 0.070 s | 0.22 s |
  | Fast | 2.4 ms | 0.8 × 10¹⁴ G | 0.070 s | 0.22 s |
  | Slow | 91 ms | 5 × 10¹⁴ G | 0.45 s | 1.39 s |

  At late times P depends only on B. Only the slow-rotation magnetar reaches seconds within the timeline (#57).
- **The three giant flares** (Mereghetti 2008, Table 2) [V]:

  | | 5 Mar 1979, SGR 0526−66 (LMC, 55 kpc) | 27 Aug 1998, SGR 1900+14 (15 kpc) | 27 Dec 2004, SGR 1806−20 (15 kpc) |
  |---|---|---|---|
  | Spike duration | 0.25 s | 0.35 s | 0.5 s |
  | Spike peak luminosity | 3.6 × 10⁴⁴ erg/s | > 8.3 × 10⁴⁴ | (2–5) × 10⁴⁷ |
  | Spike energy (isotropic) | 1.6 × 10⁴⁴ erg | > 1.5 × 10⁴⁴ | (1.6–5) × 10⁴⁶ |
  | Tail duration | 200 s | 400 s | 380 s |
  | Tail energy | 3.6 × 10⁴⁴ erg | 1.2 × 10⁴⁴ | 1.3 × 10⁴⁴ |
  | Tail pulse period | 8.1 s | 5.15 s | 7.56 s |

  Spikes rise in "smaller than a few milliseconds" and last "a few tenths of second" (Mereghetti 2008) [V]. The 2004
  energies scale as D²; at a later distance estimate of ≈ 8.7 kpc the spike is ≈ 2 × 10⁴⁶ erg [R for the distance].
- **Rate per magnetar [D]:**
  - 3 giant flares from the Galaxy and the LMC in the ≈ 47.7 yr since March 1979.
  - ≈ 30 known magnetars and candidates (26 in 2014, Olausen & Kaspi [V]; ≈ 30 in today's online catalogue [R]).
  - Rate = 3 / (47.7 × 30) = **2.1 × 10⁻³ per magnetar per year**, about 1 per 480 yr. Gehrels 1σ for n = 3 gives
    0.96–4.1 × 10⁻³.
  - Cross-check: the extragalactic rate of flares above 4 × 10⁴⁴ erg, 3.8 (+4.0/−3.1) × 10⁵ Gpc⁻³ yr⁻¹ (Burns et al.
    2021 [V]), with 0.01 Milky-Way-like galaxies per Mpc³ [R] and 30 magnetars each, gives 1.3 × 10⁻³ [D]. That
    agrees within errors.
  - Burns et al.: some magnetars "produce multiple" giant flares [V].
- Caveat: the observed magnetars are 10³–10⁵ yr old. Newborn magnetars may flare more often; not known.

### App implementation (§C6)

- `MagnetarFlares.RatePerYear = 2.1e-3` (Speculative; range 0.96–4.1 × 10⁻³). It gives a Poisson mean of 2.1 flares
  in 1,000 yr.
- Flare energy: the spike's energy is drawn log-uniform between 1.5 × 10⁴⁴ and 2 × 10⁴⁶ erg [APPROX: the observed
  range], seeded by the run. `SpikeS = 0.35 s` (0.25–0.5); peak luminosity = energy / spike duration.
  - A tail of ≈ 1.3 × 10⁴⁴ erg over ≈ 300 s (200–400 s) is pulsed at the magnetar's own period. The gamma panel may
    show it; it is not needed for the flash.
- The comparison caption uses the 2004 flare: (1.6–5) × 10⁴⁶ erg in a 0.5 s spike, at 15 kpc in Mereghetti's table.
- Field decay: not modelled. Readout note: "Magnetar fields decay over about 10,000 years (Beniamini et al. 2019);
  not shown".

---

## §C7 Black-hole optics (exact results the tests use)

Kerr, spin a = Jc/GM² (0 ≤ a < 1), lengths in M = GM/c².

- **Schwarzschild:**
  - Photon sphere r = 3M [R, textbook].
  - Shadow (critical impact parameter) b_c = √27 M = 5.196152 M [R].
  - Horizon r = 2M.
  - Shadow diameter / horizon diameter = √27/2 = 2.598 [D].
  - 10 M☉: horizon diameter 59.08 km, shadow diameter 153.49 km [D].
- **Horizon:** r₊ = M (1 + √(1 − a²)) [R]. a = 0.9 gives 1.4359 [D].
- **Ergosphere:** r_E(θ) = M (1 + √(1 − a² cos²θ)), with θ measured from the spin axis [R]. It is 2M at the equator for
  every a, and r₊ at the poles.
- **ISCO (Bardeen, Press & Teukolsky 1972):**

      Z₁ = 1 + (1 − a²)^(1/3) [(1 + a)^(1/3) + (1 − a)^(1/3)],   Z₂ = √(3a² + Z₁²),
      r_ISCO = M [3 + Z₂ ∓ √((3 − Z₁)(3 + Z₁ + 2Z₂))]   (− prograde, + retrograde)

  r_ISCO and Z₁ as quoted by MacFadyen & Woosley 1999 [V via MW99]; Z₂ [R]. Values [D]:

  | a | 0 | 0.1 | 0.5 | 0.9 | 0.95 | 0.998 | 1 |
  |---|---|---|---|---|---|---|---|
  | r_ISCO prograde | 6.0000 | 5.6693 | 4.2330 | **2.3209** | 1.9372 | 1.2370 | 1.0000 |
  | r_ISCO retrograde | 6.0000 | 6.3229 | 7.5546 | 8.7174 | 8.8590 | 8.9944 | 9.0000 |
  | r₊ | 2.0000 | 1.9950 | 1.8660 | 1.4359 | 1.3122 | 1.0632 | 1.0000 |

- **Kerr shadow outline (Bardeen 1973)** [R for the book chapter; the formulas below are standard and were checked
  numerically: a → 0 gives a circle of radius √27]. The spherical photon orbits r ∈ [r_ph+, r_ph−] are

      r_ph± = 2M [1 + cos(⅔ arccos(∓a))]

  The prograde orbit is r_ph+ (1.5579 at a = 0.9) and the retrograde r_ph− (3.9103). Their constants of motion are

      ξ(r) = −[r³ − 3Mr² + a²r + a²M] / [a(r − M)],
      η(r) = r³ [4a²M − r(r − 3M)²] / [a²(r − M)²]

  For an observer at inclination i from the spin axis, the outline in screen coordinates (α across, β along the projected
  spin axis) is

      α = −ξ / sin i,   β = ±√(η + a² cos² i − ξ² cot² i)

  keeping the points where the root is real. Check values at a = 0.9 [D, 20,000 points]:

  | i | α range | β max | flattening 1 − (half-width/β_max) | centre shift |
  |---|---|---|---|---|
  | 90° | −2.8444 … 6.8316 | 5.1962 | 6.9% | +1.994 |
  | 60° | −3.0957 … 6.5892 | 5.1355 | 5.7% | +1.747 |
  | 17° | −4.3025 … 5.5023 | 4.9450 | 0.9% | +0.600 |

  At a = 0.998 and i = 90° the flattening is 12.4% [D]. Sample outline points (a = 0.9, i = 90°), (r; α, β):
  (2.0; −1.7444, 3.4996), (2.5; −0.2148, 4.7349), (3.0; 1.8000, 5.1962), (3.5; 4.3422, 4.4754) [D].
  EHT: the Kerr shadow's radius "ranges from 4.8 GM/c² to 5.2 GM/c²" over spin and inclination (EHT Sgr A* Paper VI)
  [V].
- **Weak-field deflection:** Δφ = 4GM/(bc²) = 4M/b [R, Einstein 1915; textbook]. b = 1,000 M gives 0.004 rad.
- **Visible surface fraction of a non-rotating star** (Schwarzschild, exact) [D]:
  - The grazing ray has b_max = R/√(1 − u), with u = r_g/R = 2GM/(Rc²).
  - Its total bending angle is

        ψ_max = ∫₀¹ dy / √((1 − u) − y²(1 − u y))

    (y = R/r), integrated with y = 1 − t² to remove the endpoint singularity, 200,000 points.
  - Visible fraction f = (1 − cos ψ_max)/2, or 1 if ψ_max ≥ π.
  - Beloborodov 2002's approximation is cos ψ_max ≈ −u/(1 − u) [R].

  | M (M☉), R = 12 km | 1.2 | 1.4 | 1.6 | 1.8 | 2.0 | 2.2 |
  |---|---|---|---|---|---|---|
  | u = r_g/R | 0.2954 | 0.3446 | 0.3939 | 0.4431 | 0.4923 | 0.5416 |
  | ψ_max | 114.23° | 120.65° | 128.36° | 137.93° | 150.29° | 167.39° |
  | f exact | 0.7052 | **0.7549** | 0.8103 | 0.8711 | 0.9343 | 0.9879 |
  | f Beloborodov | 0.7096 | 0.7629 | 0.8249 | 0.8978 | 0.9849 | 1.0907 |

  The photon sphere (1.5 r_g) is 9.75 km at 2.2 M☉, still inside 12 km [D].

### App implementation (§C7)

- Constants: `SchwarzschildShadowRadius = √27 = 5.196152422706632`; `PhotonSphereRadius = 3`;
  `GravitationalRadiusCm(M) = 1.47670 × 10⁵ cm × M/M☉` [D from signals §0.2 constants].
- `HorizonRadius`, `ErgosphereRadius`, `IscoRadius`, `KerrShadowOutline`: the formulas above. Test values: the ISCO
  table, the a = 0.9 outline table (1% of 5.2), ergosphere 2 at the equator and r₊ at the pole.
- `WeakFieldDeflection(b) = 4/b`.
- `VisibleSurfaceFraction(1.4, 12) = 0.7549` (test 0.74–0.78 and agreement with the tracer).
- Ten solar masses: shadow diameter 153.49 km (test 152.9–154.0).

---

## §C8 Black-hole spin

- **Birth spins in theory:** black holes from single stars with efficient angular-momentum transport are born with
  "a ~ 10⁻²", "regardless of initial rotation rate". Homogeneous evolution or tidal spin-up in binaries can give
  moderate or high spins (Fuller & Ma 2019) [V].
- **Observed** [V]:
  - Cygnus X-1: a* > 0.9985 (3σ; Zhao et al. 2021).
  - A0620−00: a* = 0.12 ± 0.19 (Gou et al. 2010).
  - LIGO–Virgo–KAGRA GWTC-3: "half of spin magnitudes below χ_i ≈ 0.25", "concentrated below χ_i ≈ 0.4"; effective
    inspiral spins have a mean χ_eff ≈ 0.06 (+0.04/−0.05) (Abbott et al. 2023, PRX 13, 011048).
- **Collapsar (MacFadyen & Woosley 1999)** [V]:
  - "The angular momentum in the iron core of the presupernova star corresponds to a_init ≈ 0.5", the value "more
    consistent with the angular momentum distribution assumed for the mantle".
  - The initial black-hole mass is the iron core's baryon mass, 2.03 M☉.
  - Average accretion 0.07 M☉/s for ≈ 15 s, "maintained for at least 20 s".
  - "The hole mass and normalized Kerr parameter grow rapidly in the first second"; a disk forms after ≈ 1 M☉ has been
    accreted (≈ 1 s).
  - a at 20 s: 0.9849 (thin disk), 0.9752 (neutrino-dominated, a_init = 0.5), 0.8591 (advection-dominated), 0.9274
    (neutrino-dominated, a_init = 0). Abstract: "spun up by the accretion (to a ≳ 0.9)".
- **Collapsar disk extent** [V, MW99]: "an accretion shock is located at about 200 km where a disk forms"; "the
  centrifugally supported torus has a radius of 200 km"; a shock bounds the disk at 250–350 km; the inner boundary of the
  grid is 50 km. For 2.9 M☉ (the sn1998bw preset), 200 km = 46.7 M [D].
- **Fallback:** "about 0.1 to 5 solar masses falls back ... over a period of minutes to hours", at 10⁻³–10⁻² M☉/s
  (MacFadyen, Woosley & Heger 2001) [V].
- **Ring-down:** milliseconds (spec §6.4). The quasi-normal-mode period is ~ 0.5 ms for 10 M☉ [R]. Not drawn (spec §9).

### App implementation (§C8)

All `Confidence.Speculative` (no birth-spin model; spec §6.4). Plan placeholders P3-13 are **confirmed**:

| Case | Settled a* | Range shown | Basis |
|---|---|---|---|
| Rotation Slow | 0.1 | 0.01–0.25 | Fuller & Ma ~0.01; GWTC-3 half below 0.25 |
| Rotation Fast | 0.5 | 0.25–0.7 | MW99's a_init ≈ 0.5 for a rotating core [APPROX mapping] |
| Rotation Extreme | 0.9 | 0.86–0.985 | MW99 a at 20 s |
| Any `HasGrb` outcome | 0.9 | 0.86–0.985 | MW99 |
| Collapsar starting spin | 0.5 | 0–0.5 | MW99 a_init |

- **Collapsar spin-up** [APPROX: MW99's shape, fast early, slow later], with Δt = t − t_BH:

      a(Δt) = a₀ + (a₁ − a₀) · ln(1 + Δt/0.3 s) / ln(1 + 15 s/0.3 s)   for 0 ≤ Δt ≤ 15 s;  a₁ after

  Here a₀ = 0.5 and a₁ = 0.9. Check values: 0.65 at 1 s, 0.79 at 5 s, 0.86 at 10 s.
- `SettleEndS`:
  - collapsar: t_BH + 15 s [V: MW99's ≈ 15 s];
  - fallback: **1,000 s** [APPROX: "minutes to hours", log-middle]; `SettleStartS` = t_BH;
  - direct collapse: P3-11's 1/3 decade (a visual transition).
- **Disk:** `DiskOuterGm = 200 km / (GM_BH/c²)` [V: 200 km torus], 46.7 for 2.9 M☉. The inner edge is the ISCO. The disk
  exists for t_BH + 1 s ≤ t ≤ t_BH + 15 s and fades over the last 20% [APPROX].

---

## §C9 Observational checks

### C9.1 The Event Horizon Telescope

- **M87*** [V, EHT 2019 Paper VI]:
  - Ring ("crescent") diameter 42 ± 3 µas; θ_g = GM/(Dc²) = 3.8 ± 0.4 µas; M = 6.5 ± 0.2 (stat) ± 0.7 (sys) × 10⁹ M☉
    at D = 16.8 (+0.8/−0.7) Mpc.
  - Calibration factor α = ring diameter / θ_g = 10.67–11.56 (Tables 4, 6).
  - Prior masses at 16.8 Mpc: stellar dynamics (Gebhardt et al. 2011) M = 6.14 (+1.07/−0.62) × 10⁹, θ_g = 3.62; gas
    dynamics (Walsh et al. 2013) M = 3.45 (+0.85/−0.26) × 10⁹, θ_g = 2.05 (Paper VI's table) [V].
- **Sgr A*** [V, EHT 2022 Papers I and VI]:
  - Ring 51.8 ± 2.3 µas.
  - θ_g = 5.125 ± 0.009 ± 0.020 µas (VLTI) and 4.92 ± 0.03 ± 0.01 µas (Keck).
  - GRAVITY's M = 4.297 ± 0.012 ± 0.040 × 10⁶ M☉ at R₀ = 8,277 ± 9 ± 33 pc; Keck's M = 3.951 ± 0.047 × 10⁶ at 7,935 pc.
  - Fractional shadow deviation δ = d_sh/d_sh,Sch − 1: **−0.08 ± 0.09 (VLTI)**, −0.04 (+0.09/−0.10) (Keck). The image
    size is "within ~10% of the Kerr predictions".
- **Resolution:** "a 20 µas beam, the nominal resolution of the EHT" (Sgr A* Paper VI) [V].
- **The shadow law against them** [D, d_sh = 2√27 θ_g]:

  | Case | θ_g (µas) | Shadow d_sh (µas) | Ring = d_sh × 11.0/(6√3) (µas) | Observed |
  |---|---|---|---|---|
  | M87, EHT mass | 3.820 | 39.70 | 42.02 | ring 42 ± 3 (circular: the mass came from the ring) |
  | M87, stellar-dynamics mass | 3.608 | 37.50 | **39.69** | ring 42 ± 3 ✓ (independent) |
  | M87, gas-dynamics mass | 2.027 | 21.07 | 22.30 | ring 42 ± 3 ✗ (#56) |
  | Sgr A*, GRAVITY | 5.126 | **53.27** | 56.38 | ring 51.8 ± 2.3 ✗ by 2σ; shadow (1 + δ)·53.27 = 49.0 ± 4.8 ✓ within 0.9σ |
  | Sgr A*, Keck | 4.916 | 51.09 | 54.08 | shadow (1 − 0.04)·51.09 = 49.0 (+4.6/−5.1) ✓ |

  Ring-to-shadow factor from M87: 11.0/10.392 = **1.06** (range 1.03–1.11) [D from V].
- **From Earth:** a 10 M☉ hole at 10 kpc has a 0.103 nano-arcsecond shadow, 1.95 × 10⁵ times smaller than 20 µas [D].
  The spec's "about 0.1" and "about 200,000" are confirmed.

### C9.2 OGLE-2011-BLG-0462 (the isolated black hole)

| Analysis | Mass (M☉) | Distance | Tag |
|---|---|---|---|
| Sahu et al. 2022 | 7.1 ± 1.3 | 1.58 ± 0.18 kpc; transverse speed ≈ 45 km/s | [V] |
| Lam et al. 2022 | 1.6–4.4 ("a neutron star or a low-mass black hole") | — | [V] |
| Mróz, Udalski & Gould 2022 | 7.88 ± 0.82 | — | [R] |
| Lam & Lu 2023 (supersedes Lam 2022) | 6.0 (+1.2/−1.0) | — | [V] |
| Sahu et al. 2025 (11 yr of HST) | 7.15 ± 0.83 | 1.52 ± 0.15 kpc; 51.1 ± 7.5 km/s | [V] |

### C9.3 The Cas A neutron star

- Heinke & Ho 2010 [V]:
  - Carbon atmosphere, local T_s from 2.12 ± 0.01 MK (2000) to 2.04 ± 0.01 MK (2009), for M = 1.648 M☉, R = 10.3 km,
    3.4 kpc.
  - L_bol (1 eV–10 keV) 7.95 → 6.90 × 10³³ erg/s, the redshifted luminosity [D: 4πR²σT⁴(1 − r_g/R) reproduces 6.97 ×
    10³³].
  - Age ≈ 330 yr at the time.
- Shternin et al. 2023 (all Chandra ACIS-S to 2020) [V]:
  - M = 1.55 ± 0.25 M☉, R = 13.5 ± 1.5 km.
  - Fitted log T_s ≈ 6.22–6.25 (the "(non-redshifted) surface temperature").
  - Cooling 2.2 ± 0.3% per 10 yr (N_H free) or 1.6 ± 0.2% (N_H fixed).
- **Seen from afar** [D]: T_s^∞ = 1.48–1.54 MK (Heinke & Ho's M, R); 1.39–1.44 MK (Shternin's).
- **The lab at 340 yr** [D, 12 km]: T_s^∞ = 1.89 (1.2 M☉), 1.82 (1.4), 1.75 (1.6), 1.60 (2.0) MK; local T_s 2.25 MK
  for every mass (the lab's g₁₄ is fixed at 2.4).

### App implementation (§C9)

- **M87 test (independent):** the ring from the stellar-dynamics mass, 6.14 × 10⁹ M☉ at 16.8 Mpc, times RingToShadow =
  1.06 is 39.7 µas. The test passes within 42 ± 3 µas (tolerance 3 µas, EHT's 1σ). The EHT-mass version (42.0) may also
  be shown; it is circular.
- **Sgr A* test:** the shadow from GRAVITY's θ_g (4.297 × 10⁶ M☉, 8.277 kpc) is 53.27 µas. It must lie within the EHT's
  inferred shadow 6√3 θ_g (1 + δ) for δ ∈ [−0.17, +0.01] (VLTI 1σ), i.e. 44.2–53.8 µas. **Do not** test Sgr A*'s ring
  against 51.8 ± 2.3 with M87's factor: it fails by 2σ (#55).
- `FromEarthText`: resolution 20 µas.
- OGLE tooltip: "OGLE-2011-BLG-0462, the only known isolated stellar-mass black hole, found by microlensing: 7.15 ±
  0.83 M☉ (Sahu et al. 2025); other analyses give 6.0 (Lam & Lu 2023) to 7.9 M☉ (Mróz et al. 2022); an early analysis
  allowed 1.6–4.4 M☉ (Lam et al. 2022)."
- **Cas A comparison (#54, corrected):** compare **T_s^∞ with T_s^∞**. Text: "Observed (Heinke & Ho 2010): 2.04–2.12
  million K at the surface, about 1.5 million K seen from afar; the lab gives 1.8 million K seen from afar, about 20%
  warm." The text still contains "2.0". The model value stays 1.75–1.9 MK (the plan's test range is unchanged).

---

## §C10 The "never observed" audit

Rule (spec §7.1): a signal says "Expected" when no instance of it has been observed from **any** supernova. The audit
is by physical signal, not by detector: the anti-ν_e burst was seen in 1987 by detectors that are not on today's
panel.

| Model (Id) | Channel | Status | Source / reason |
|---|---|---|---|
| gw | all | **never observed** | no gravitational waves from any supernova; the 1987 bar claim is disputed (§C11) |
| neutrino | lum_anue, meanE_anue, rate@*, cumulative@* (not dune) | observed | SN 1987A: 24 anti-ν_e events, Kamiokande-II, IMB, Baksan (signals §1.5) |
| neutrino | lum_nue | **never observed** | No ν_e signal has been identified, including the neutronization burst. Expected burst count in Kamiokande-II ≈ 0.03 events [D] (2 × 10⁵¹ erg at 12 MeV, 51.4 kpc, ν–e scattering on 7.2 × 10³² electrons) |
| neutrino | lum_nux | **never observed** | never separately identified |
| neutrino | rate@dune, cumulative@dune | **never observed** | ν_e charged-current on argon: no supernova ν_e ever detected |
| neutrino | all | **never observed** when the remnant is a black hole | the only supernova neutrinos (SN 1987A) came from a collapse that left a neutron star (JWST evidence, Fransson et al. 2024; signals §8.3); no failed or BH-forming collapse has been seen in neutrinos |
| breakout | lum, temp | observed | SN 2008D's X-ray flash, interpreted as breakout (signals §3; inconsistency #5) [R]; KSN 2011d in the optical [R] |
| lightcurve | all | observed | every supernova type the lab draws |
| gammalines | line847, line1238 | observed | ⁵⁶Co: SN 1987A (SMM), SN 2014J (INTEGRAL) (signals §5.3) |
| gammalines | line158, line812 | observed | ⁵⁶Ni: SN 2014J (INTEGRAL) (signals §5.3) [R for the citation] |
| gammalines | ti44_* | observed | Cas A, SN 1987A (signals §5.3) |
| csm | all | observed | radio and X-rays from many supernovae; SN 1987A's ring |
| remnant | all | observed | young remnants (Cas A, Tycho, the Crab) |
| pulsar | all | observed | the Crab pulsar in SN 1054's remnant |
| pulsar | all | **never observed** for SN 1987A | no pulsar seen; JWST evidence is indirect (Fransson et al. 2024) |
| nscooling | all | observed | the Cas A neutron star (§C9) |
| grb | all | observed | GRB 980425 / SN 1998bw (signals §9.4) |
| all models | all | **never observed** for a pair-instability supernova | no confirmed event; candidates SN 2007bi and SN 2018ibb [R] |
| all models | all | **never observed** for a pulsational pair-instability outcome | no confirmed event [R] |
| lightcurve | all | **never observed** for a failed supernova | candidate only: N6946-BH1's fading star (presets) [R] |
| (Task 12 flare model) | all | observed | three giant flares; SGR 0526−66 lies in the remnant N49 [R] |

Note on time: no pulsar younger than ≈ 500 yr has been seen pulsing (the Crab is 972 yr; Kes 75's pulsar ≈ 500 yr [R]).
`NeverObserved.Applies` takes no time, so the pulsar panel keeps "Detected" for other outcomes; the note can say so.

### App implementation (§C10)

`NeverObserved.Applies(modelId, channelId, o)` is true for any of these triples ("*" = any channel):

    ("gw",         "*",                  true)
    ("neutrino",   "lum_nue",            true)
    ("neutrino",   "lum_nux",            true)
    ("neutrino",   "rate@dune",          true)
    ("neutrino",   "cumulative@dune",    true)
    ("neutrino",   "*",                  o.Remnant.Kind == RemnantKind.BlackHole)
    ("pulsar",     "*",                  o.HasEquatorialRing)          // the sn1987a preset's pass-through pin (R41)
    ("lightcurve", "*",                  o.Fate == Fate.FailedSupernova)
    ("*",          "*",                  o.Fate == Fate.PairInstabilitySupernova)
    ("*",          "*",                  o.Fate == Fate.PulsationalPairInstability)

- Each gets the badge word "Expected". Sources for the tooltip: the table's reason column.
- If a cleaner SN 1987A flag is wanted than `HasEquatorialRing`, add a pass-through preset flag (a ruling).

---

## §C11 The 1987 bar detectors

- **What was claimed** (Galeotti & Pizzella 2016, EPJC; Galeotti, Pallottino & Pizzella 2008) [V]:
  - The Mont Blanc LSD recorded five events at 2h52m UT on 23 February 1987. The 2008 paper writes "2h56min36sec";
    the IAU Circular 4323 time is 2:52:36.8 UT [R].
  - The room-temperature resonant bars in Rome (GEOGRAV) and Maryland "recorded several signals in time coincidence
    between them and with the event detected by LSD".
  - This "unusual activity preceded the LSD signals by 1.1–1.2 s" (2016); "anticipating the neutrino signals by
    1.4 seconds" (2008).
  - Kamiokande-II, IMB and Baksan saw the main burst at 7h35m UT, ≈ 4.7 h later.
  - The 2016 paper claims correlations "over a long period of time (one or two hours) centered at the Mont Blanc time".
- **The authors' own statement of the sensitivity problem** [V]:
  - GEOGRAV "was not sensitive enough for a possible g.w. according to classical estimation of the cross-section".
  - "With this cross-section the signals observed with the g.w. detectors require a total conversion into g.w. of at
    least one thousand solar masses, which is impossible, provided we exclude that the g.w. have been produced in a
    beam" (2008).
  - The 2016 paper appeals to "cooperative effects" in the detector and notes that the bars "are also sensitive to
    particles".
- **The standard objection:**
  - With the standard cross-section the claimed events need ≳ 10³ M☉c² in gravitational waves; a book chapter puts it
    as "an emission of energy too large by a factor of about 10⁴" [R, search summary].
  - The whole collapse releases ≈ 3 × 10⁵³ erg ≈ 0.17 M☉c² (signals §1.1). So 10³ M☉c² (1.8 × 10⁵⁷ erg) is ≈ 6,000
    times the total energy and ≈ 2 × 10¹² times the lab's default E_GW of 10⁴⁵ erg [D].
  - The larger Kamiokande-II saw no burst at the LSD time (Galeotti et al. argue the LSD events, 5.8–7.8 MeV, were at the
    limit of its threshold [V]).
  - The statistics are a posteriori.
- **The bars' strain sensitivity [D]:**
  - A burst carrying 10³ M☉c² at 51.4 kpc and f₀ ≈ 865 Hz has h_rss = √(G E/(π² c³)) / (D f₀) ≈ 4.9 × 10⁻¹⁸ Hz^(−1/2),
    i.e. h ≈ 1.5 × 10⁻¹⁶ for a 1 ms burst.
  - This uses signals §2.4's burst relation and the bars' implied threshold. GEOGRAV's resonance near 865 Hz is [R].
  - **NOT FOUND:** a strain figure read in the original 1987–1989 papers (Amaldi et al. 1987; Aglietta et al. 1989 were
    not opened).
- For comparison, the lab's gravitational-wave panel gives h_rss ≈ 3 × 10⁻²³ Hz^(−1/2) for 2 × 10⁴⁵ erg at 10 kpc
  (signals §2.4). At 51.4 kpc that is ≈ 6 × 10⁻²⁴, about 10⁶ below the bars' level [D].

### App implementation (§C11)

- SN 1987A's note (Task 2), with the source: "Galeotti, Pallottino & Pizzella 2008, arXiv:0810.3759; Galeotti &
  Pizzella 2016, Eur. Phys. J. C 76, 426 (doi:10.1140/epjc/s10052-016-4277-4). With the standard cross-section the
  signals would need at least 1,000 solar masses converted to gravitational waves (the authors' own estimate), about
  6,000 times the collapse's whole energy."
- Figures for any caption: required E_GW ≥ 10³ M☉c² = 1.8 × 10⁵⁷ erg [V/D]; implied bar strain ≈ 1.5 × 10⁻¹⁶ (1 ms) [D];
  the lab's SN 1987A expectation h_rss ≈ 6 × 10⁻²⁴ Hz^(−1/2) [D].

---

## References read this session

Balliet, Newton, Cantu & Budimir 2021, arXiv:2009.07696 (abstract). Beniamini et al. 2019, MNRAS 487, 1426,
arXiv:1903.06718 (abstract). Bollig et al. 2021, ApJ 915, 28, arXiv:2010.10506. Burns et al. 2021, ApJL 907, L28,
arXiv:2101.05144 (abstract). Burrows et al. 2020, MNRAS 491, 2715, arXiv:1909.04152. Cackett et al. 2006, MNRAS 372,
479, arXiv:astro-ph/0605490 (abstract). Chamel & Haensel 2008, Living Rev. Rel. 11, 10, arXiv:0812.3955. EHT
Collaboration 2019, ApJL 875, L6 (M87 Paper VI), arXiv:1906.11243. EHT Collaboration 2022, ApJL 930, L12 (Sgr A* Paper
I), arXiv:2311.08680 (abstract); ApJL 930, L17 (Sgr A* Paper VI; accepted manuscript, OSTI 1868459). Esposito et al.
2020, ApJL 896, L30, arXiv:2004.04083 (abstract). Fuller & Ma 2019, ApJL 881, L1, arXiv:1907.03714 (abstract). Galeotti,
Pallottino & Pizzella 2008, arXiv:0810.3759. Galeotti & Pizzella 2016, EPJC 76, 426, arXiv:1603.05076. Gnedin, Yakovlev
& Potekhin 2001, MNRAS 324, 725, arXiv:astro-ph/0012306. Gou et al. 2010, ApJL 718, L122, arXiv:1002.2211 (abstract).
Abbott et al. (LVK) 2023, PRX 13, 011048, arXiv:2111.03634 (GWTC-3 population). Hanke et al. 2013, ApJ 770, 66,
arXiv:1303.6269. Heinke & Ho 2010, ApJL 719, L167, arXiv:1007.4719. Hempel & Schaffner-Bielich 2008, arXiv:0707.2740.
Holland-Ashford, Slane & Long 2024, ApJ 962, 82, arXiv:2310.19879 (abstract). Johnston & Karastergiou 2019, MNRAS,
arXiv:1902.03996. Kaplan et al. 2008, ApJ 677, 1201, arXiv:0801.1142 (abstract). Kitaura, Janka & Hillebrandt 2006, A&A
450, 345, arXiv:astro-ph/0512065. Kolonko, Gil & Maciesiak 2004, arXiv:astro-ph/0412159. Lam et al. 2022,
arXiv:2202.01903 (abstract). Lam & Lu 2023, ApJ 955, 116, arXiv:2308.03302 (abstract). Link, Epstein & Lattimer 1999,
PRL 83, 3362, arXiv:astro-ph/9909146 (abstract). Lyne et al. 2013, Science 342, 598, arXiv:1311.0408 (abstract).
MacFadyen & Woosley 1999, ApJ 524, 262, arXiv:astro-ph/9810274. MacFadyen, Woosley & Heger 2001, ApJ 550, 410,
arXiv:astro-ph/9910034 (abstract). Mereghetti 2008, A&ARv 15, 225, arXiv:0804.0250. Metzger et al. 2011, MNRAS 413,
2031, arXiv:1012.0001 (abstract). Müller 2020, Living Rev. Comput. Astrophys. 6, 3, arXiv:2006.05083. Ng & Romani 2004,
ApJ 601, 479, arXiv:astro-ph/0310155. O'Connor & Ott 2011, ApJ 730, 70, arXiv:1010.5550. O'Connor et al. 2018, J. Phys.
G 45, 104001, arXiv:1806.04175. Olausen & Kaspi 2014, ApJS 212, 6, arXiv:1309.4167. Roberts 2012, ApJ 755, 126,
arXiv:1205.3228. Sahu et al. 2022, ApJ 933, 83, arXiv:2201.13296 (abstract). Sahu et al. 2025, ApJ (in press),
arXiv:2503.07820 (abstract). Shternin et al. 2023, MNRAS 518, 2775, arXiv:2211.02526. Yakovlev & Pethick 2004, ARA&A 42,
169, arXiv:astro-ph/0402143. Zdunik, Fortin & Haensel 2017, A&A 599, A119, arXiv:1611.01357. Zhao et al. 2021, ApJ 908,
117, arXiv:2102.09093 (abstract).

Not reached (cited as [R]): Bardeen 1973 and Bardeen, Press & Teukolsky 1972 (formulas checked numerically and via
MW99); Baym, Pethick & Sutherland 1971; Rankin 1993a,b; Biggs 1990; Gil, Kijak & Seiradakis 1993 and Kramer et al. 1994
(via Kolonko et al.); Romani & Yadigaroglu 1995 and Caraveo & Mignani 1999 (via Ng & Romani); Mróz, Udalski & Gould
2022; Mayer & Becker 2021; Gebhardt et al. 2011 and Walsh et al. 2013 (via EHT Paper VI); Amaldi et al. 1987 and
Aglietta et al. 1989; Beloborodov 2002; the pair-instability candidates (SN 2007bi, SN 2018ibb).
