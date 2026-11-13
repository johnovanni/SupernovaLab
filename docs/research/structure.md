# Supernova Lab: 3D structure of the explosion and its surroundings

Research for Project 2's 3D view (spec `2026-11-12-supernovalab-project2-design.md` §3–§4). Compiled 2026-11-12.
Normative for the `Structure/` code: every later task takes its numbers from here, by section number (§S1–§S10).

## READ THIS FIRST: confidence tags

- **[V]** = read this session in the cited paper's own text (arXiv full text or abstract, or the journal PDF).
  "[V via X]" = read this session in paper X, which quotes the cited paper; the cited paper itself was not opened.
- **[R]** = recalled; the paper exists but the number was not re-opened this session. Verify before a test pins it.
- **[D]** = derived here by stated arithmetic, or by a short numerical calculation described in place (the scripts were
  run in this session; the method is given so it can be repeated).
- **[APPROX]** = no published value found; a stated stand-in with its reasoning. Shown to the user as schematic.

Units: cgs unless stated. E51 = E/10⁵¹ erg; masses in M☉ (1.989 × 10³³ g); 1 ly = 9.4607 × 10¹⁷ cm;
1 pc = 3.0857 × 10¹⁸ cm. "v_t" is the transition velocity of §S1 throughout.

---

## Corrections to the spec

The spec (§3.2, §4) quoted these from memory. What the sources say:

| # | Spec said | Sources say | Section |
|---|---|---|---|
| C1 | SN 1987A's equatorial ring radius ≈ 0.6 ly | **0.665 ly** (6.29 × 10¹⁷ cm) at the preset's 51.4 kpc: Sugerman et al. 2005's 3D fit gives a = 0.647 ly at 50 kpc (Table 3) [V], scaled by 51.4/50 [D]. Consistent with the 0.820″ semi-major axis (Table 2) [V] and Panagia's (6.23 ± 0.08) × 10¹⁷ cm [R]. | §S8 |
| C2 | Ring tilted ≈ 43° to our line of sight | **Confirmed:** 43.0° ± 0.1° (Sugerman et al. 2005, Table 2) [V]. | §S8 |
| C3 | SN 1987A's nickel reaches 3,000–4,000 km/s | **Refined:** the *bulk* of ⁵⁶Ni reaches only **≈ 3,000 km/s** (Colgan et al. 1994, [Ni II]/[Fe II] at day 640); one small fast clump (~10⁻³ M☉) moved at **4,700 km/s** (Utrobin, Chugai & Andronova 1995) [both V via Utrobin et al. 2019]. "3,500–4,000 km/s" is the older observed maximum quoted by Kifonidis et al. 2003 [V]. So: bulk to 3,000, a trace to 4,700. | §S3 |
| C4 | SN 1987A's hydrogen mixed down to under 1,000 km/s | **Confirmed and sharpened:** down to **≤ 700 km/s** (Kozma & Fransson 1998), 600 km/s (Chugai 1991), molecular hydrogen's lowest velocities 400–800 km/s (Larsson et al. 2019), line shapes suggesting mixing to near zero (Maguire et al. 2012) [all V via Utrobin et al. 2019]. | §S3 |
| C5 | Pulsar wind nebula R ∝ t^1.2 while the debris coast | **Confirmed for uniform-density inner ejecta** (van der Swaluw et al. 2001, R = 0.922 (L t/E)^{1/5} V₀ t) [V]. For an inner profile ρ ∝ r^−m the exponent is (6−m)/(5−m): t^1.25 for m = 1, t^1.254 for Matzner & McKee's m = 1.06 (Chevalier 2005) [V]. With §S1's δ = 1 core, the consistent law is t^1.25. | §S9 |
| C6 | Hotspots from about day 3,000; peak about day 8,000 | **Confirmed:** first hotspot 1995 (≈ day 3,000; signals §6.3); optical ring flux peaked ≈ day 7,000 (Chevalier & Fransson 2017) to a plateau in 2009 ≈ day 8,000 (Barlow 2025 review) [V]. | §S8 |
| C7 | Dense shell 10¹⁵ cm from SN 2023ixf | **Confirmed:** CSM confined to r = (0.5–1) × 10¹⁵ cm (Jacobson-Galán et al. 2023) [V]. Long-lasting IIn (SN 2010jl-like) need ≳ 3 × 10¹⁶ cm (§S7). | §S7 |
| C8 | (§3.1) particle velocities scaled "so the outer edge agrees with Project 1's EjectaKinematics" | **Not possible with a published profile.** EjectaKinematics' edge is v_ej = √(10E/3M), the edge of a *uniform* sphere. The broken power law's 99.9%-mass speed is 1.72× (red supergiant) to 2.0× (blue supergiant, Type Ia) that [D]. See §S1 and inconsistency #32. | §S1 |
| C9 | (§3.4) Project 1's blast wave is "Leahy & Williams (2017)" | **Mis-attributed.** The interpolation and constants Project 1 uses (λ_b 2.01, α = 1.25 for n = 0; b = 1.06, α = 20.3 for n = 7) and arXiv:1607.06391 are **Tang & Chevalier 2017** (MNRAS 465, 3793) [V for the arXiv text; journal ref R]. Leahy & Williams 2017 (AJ 153, 239; arXiv:1701.05942) is an SNR calculator that uses Truelove & McKee 1999 directly; it uses the Sedov constant ξ = 2.026 but not the interpolation's λ_b, α or b [V]. The ρ₀ = n₀ m_p convention signals §7.1 attributes to them is also Tang & Chevalier's; Leahy & Williams write ρ₀ = μ_H n₀ m_H [V, checked by the Task 1 reviewer]. The numbers are right; the label is wrong (inconsistency #34). | §S4.3 |
| C10 | (§3.4) the forward shock "must agree with Project 1's blast-wave radius" | **True only for uniform ejecta (n = 0).** Real ejecta have steep outer layers (n = 7–12). For those, Truelove & McKee's forward shock runs ahead of Project 1's n = 0 radius: for a 10 M☉, 10⁵¹ erg core collapse with n = 12, 2.6× at 10 yr, +45% at 100 yr and ≈ 0 at 1,000 yr; for n = 7, +280% at t* = 0.01 and +48% at t* = 0.1 [D]. Ruling R-P2-10: the drawn forward shock is Project 1's n = 0 radius and the gap is disclosed (inconsistency #33); the spec is corrected to say so. | §S4.3 |

---

## §S1 Ejecta density profile

### S1.1 The broken power law

In free expansion ρ(r, t) = t⁻³ f(v) with v = r/t (Chevalier & Fransson 2017, §2.1) [V]. The profile is flat-ish
inside and steep outside:

    ρ t³ = K (v/v_t)^−δ    for v ≤ v_t
    ρ t³ = K (v/v_t)^−n    for v ≥ v_t

(Chevalier & Soker 1989 [R]; written in this form by Kasen 2010, ApJ 708, 1025, arXiv:0909.0275, eq. 1–3 [V], who
calls it "originally derived for core collapse supernovae" and fits it to Type Ia models.)

**Outer slope n** (planar shock acceleration through a polytropic envelope; spherical expansion steepens it by 2):

- Radiative envelope (polytrope index 3: blue supergiants, Wolf–Rayet and other stripped stars): **n = 10.18**
  [V: Matzner & McKee 1999, ApJ 510, 379, arXiv:astro-ph/9807046, Table 3 exponents 1.418/0.1394 = 10.17 and Table 5
  "l₂ = 10.176, self-similar value"]. Chevalier & Fransson 2017 round it to 10.2 [V].
- Convective envelope (index 3/2: red supergiants): **n = 11.73** [V: MM99 Table 3, 1.344/0.1145 = 11.74; Table 4
  l₂ = 11.731]; "n = 12" in Chevalier & Fransson 2017 [V].
- Caveat [V, CF17 §2.1]: "the outer power law is only approached asymptotically so the power law index applicable to a
  given situation is smaller than the asymptotic value."

**Inner slope δ:** "physical arguments imply a roughly r⁻¹ density profile" (CF17 §2.1) [V]; MM99's fiducial radiative
model uses l₁ = 1.06 from Chevalier & Soker's self-similar rarefaction [V, MM99 Table 5]. For red supergiants MM99
fit l₁ as a function of the envelope mass fraction (Table 4; the extracted table is garbled, so no value is quoted).

**Type Ia:** Kasen 2010 finds δ = 1, n = 10 fit multi-dimensional delayed-detonation models "remarkably well", with
v_t = 6 × 10⁸ ζ_v (E51/M_c)^{1/2} cm/s, ζ_v = 1.69, M_c = M/M_Ch [V]. (Alternatives: TM99 note that n = 7 has been
used for Type Ia outer ejecta since Colgate & McKee 1969 [V]; an exponential profile fits Type Ia remnants
(Dwarkadas & Chevalier 1998), v_e = 2.44 × 10⁸ cm/s (M/1.4 M☉)^{−1/2} E51^{1/2} [V via Chomiuk et al. 2016,
arXiv:1510.07662, eq. 1–3]. The local slope of the exponential is n_eff = v/v_e: 7 at about 17,000 km/s [D].)

| Class (lab) | δ | n | Source |
|---|---|---|---|
| Red supergiant (H-rich, R ≥ 158 R☉, the breakout model's convective test) | 1 | **12** (MM99 11.73) | CF17 [V]; MM99 [V] |
| Blue supergiant (H-rich, compact) | 1 | **10.18** | MM99 [V] |
| Stripped envelope (Ib, Ic, Ic-BL, Ibn, Icn, SLSN-I; Wolf–Rayet). Type IIb counts as hydrogen-rich (as in `ShockBreakoutModel`) and is classed by radius | 1 | **10.18** | MM99 radiative [V]; n = 10.18 for Wolf–Rayet explosions (Chomiuk et al. 2016 citing Berger et al. 2002 and Chevalier & Fransson 2006) [V via Chomiuk] |
| Type Ia (and Iax, sub-Chandrasekhar) | 1 | **10** | Kasen 2010 [V] |
| Pair instability | by its envelope (as above) | | [APPROX] |

### S1.2 Transition velocity [D]

Integrating the profile, M = 4πK v_t³ (n−δ)/((3−δ)(n−3)) and E = 2πK v_t⁵ (n−δ)/((5−δ)(n−5)), so

    v_t = [ 2 (5−δ)(n−5) / ((3−δ)(n−3)) × E/M ]^{1/2}

For δ = 1: the coefficient under the root is 3.1111 (n = 12), 3.0836 (11.73), 2.8858 (10.18), 2.8571 (10) [D].
Check against Kasen 2010: δ = 1, n = 10, E = 10⁵¹ erg, M = 1.4 M☉ gives 10,140 km/s from this formula and
6 × 10⁸ × 1.69 = 10,140 km/s from Kasen's eq. 1 [D, agrees to 0.1%].

### S1.3 Enclosed mass, its inverse, and the 99.9% speed [D]

With m_t = (n−3)/(n−δ) (the mass fraction inside v_t):

    M(<v)/M = m_t (v/v_t)^(3−δ)                      v ≤ v_t
    M(<v)/M = 1 − (3−δ)/(n−δ) (v/v_t)^(3−n)          v ≥ v_t

Inverse (to give a particle with mass coordinate m ∈ [0, 1) its speed):

    v(m) = v_t (m/m_t)^{1/(3−δ)}                      m ≤ m_t
    v(m) = v_t [ (1−m)(n−δ)/(3−δ) ]^{−1/(n−3)}        m ≥ m_t

The speed enclosing a fraction F = 0.999 (ruling P1): **v_max = v_t [ (3−δ) / ((n−δ)(1−F)) ]^{1/(n−3)}**.
The kinetic-energy fraction above a speed v > v_t is (5−δ)/(n−δ) (v/v_t)^(5−n).

### S1.4 Worked example (red supergiant: E = 10⁵¹ erg, M_ej = 10 M☉, δ = 1, n = 12) [D]

- E/M = 10⁵¹ / 1.989 × 10³⁴ = 5.0277 × 10¹⁶ cm² s⁻²; coefficient 56/18 = 3.1111.
- **v_t = 3,955 km/s** (√(1.5642 × 10¹⁷) cm/s).
- m_t = 9/11 = **0.8182** of the mass is inside v_t.
- M(<1,000 km/s) = 0.0523; M(<2,000) = 0.2092; M(<5,000) = 0.9780; M(<6,000) = 0.9957.
- v(m = 0.1) = 1,383; v(0.5) = 3,092; v(0.9) = 4,227; v(0.99) = 5,459 km/s.
- **v_max(99.9%) = 7,050 km/s** (x = 181.82^{1/9} = 1.7827). The truncated 0.1% carries 0.64% of the energy.
- Check: the mean of v²/2 over 200,000 equal-mass shells drawn by the inverse is 5.0276 × 10¹⁶ (expected 5.0277) [D].
- For comparison, Project 1's v_ej = √(10E/3M) = 4,094 km/s: **v_max/v_ej = 1.72** (correction C8).

Other cases [D]:

| Case | v_t (km/s) | v_max 99.9% (km/s) | m_t | v_ej (P1) | v_max/v_ej |
|---|---|---|---|---|---|
| RSG, 10⁵¹ erg, 10 M☉, n = 12 | 3,955 | 7,050 | 0.818 | 4,094 | 1.72 |
| Same, n = 11.73 | 3,937 | 7,166 | 0.814 | 4,094 | 1.75 |
| BSG or stripped, 10⁵¹ erg, 10 M☉, n = 10.18 | 3,809 | 8,063 | 0.782 | 4,094 | 1.97 |
| SN 1987A pins (1.1 × 10⁵¹ erg, 14.7 M☉), n = 10.18 | 3,295 | 6,974 | 0.782 | 3,541 | 1.97 |
| Type Ia, 10⁵¹ erg, 1.38 M☉, n = 10 | 10,203 | 22,078 | 0.778 | 11,020 | 2.00 |

Cross-check (Type Ia): the profile puts 1.05 M☉ (Mazzali et al. 2007's burned mass) inside 10,090 km/s; Mazzali
measure the outer edge of the bulk of Si at 11,300 ± 650 km/s (excluding high-velocity-gradient events) [V] —
the profile is about 11% slow there [D].

Known weakness (inconsistency #35): δ = 1 puts little mass at low speed. 3D red-supergiant models put the innermost
≈ 2 M☉ below 1,000 km/s (Wongwathanarat et al. 2015) [V]; the profile puts ≈ 0.5 M☉ there for those models [D].

### App implementation (§S1)

- Class → (δ, n): RSG (1, 12); BSG (1, 10.18); stripped envelope (1, 10.18); Type Ia and Iax (1, 10). One class
  test, the breakout model's: hydrogen-rich per `ShockBreakoutModel.IsHydrogenRich` (every type except Ib, Ic, Ic-BL,
  Ibn, Icn and SLSN-I; pair instability and None by HydrogenEnvelopeMass > 0) and R ≥ √(500 × 50) R☉ = 158 R☉ → RSG;
  other hydrogen-rich (including a compact IIb) → BSG; not hydrogen-rich → stripped. Thermonuclear → Type Ia.
- v_t from §S1.2 with the outcome's E and M_ej.
- Particle i of N (i = 0 … N−1) sits at q_i = (i + 0.5)/N (ruling P3; no jitter) and gets v_i = v(0.999 q_i) from §S1.3
  (ruling P1). Layers are assigned on q_i (§S2). Each particle carries M_ej/N, so the drawn masses add to M_ej exactly.
  Directions are seeded and isotropic.
- Tests: the RSG worked example above (v_t 3,955 km/s, v_max 7,050 km/s, m_t 0.8182) to 0.1%; Σ particle mass = M_ej.

---

## §S2 Layer composition

### S2.1 Core collapse: ruling P2 mapping, checked

The lab's presupernova star (outcomes §1.3; S16 Table 2 definitions [V there]) gives M_f, the He-core mass M_He (where
X(H) = 0.2) and the CO-core mass M_CO (where X(He) = 0.2). Ruling P2:

| Layer (inside → out) | Mass | Check against published models |
|---|---|---|
| Ni (iron group) | M_Ni (outcome) | Iron-group elements are made in a dense shell just outside the neutron star and are the innermost ejecta (Kifonidis et al. 2003, A&A 408, 621, arXiv:astro-ph/0302239) [V]. |
| CO/Si ("metal core": O, Ne, Mg, Si, S) | M_CO − M_rem,bary − M_Ni | The Si/O interface lies inside the C+O core and the next interface out is (C+O)/He (Kifonidis 2003; Wongwathanarat et al. 2015 define the C+O/He and He/H interfaces where the He and H fractions fall to half their layer maxima) [V]. |
| He | M_He − M_CO | The He shell between the (C+O)/He and He/H interfaces [V, same papers]. |
| H envelope | M_f − M_He | **Confirmed with a label caveat:** this is the H-*rich* envelope, X_H ≈ 0.45–0.68, not pure hydrogen (outcomes §1.3) [V there]. |

Sum: M_Ni + (M_CO − M_rem − M_Ni) + (M_He − M_CO) + (M_f − M_He) = M_f − M_rem = M_ej (outcomes §8.2) [D]. The mapping
is confirmed. Two edge cases make CO/Si zero or negative before the floor [D]:

1. **Remnant larger than the CO core.** For the lightest stars the baryonic remnant can reach or exceed M_CO (S16
   9 M☉: M_CO = 1.40, remnant ≈ 1.35–1.4) [D from outcomes §1.3–1.4].
2. **Fallback black holes** have M_Ni ≈ 0 and remnants of 3–7 M☉ (outcomes §1.4) that can swallow the He shell too.

Ruling P2's floor at 0 plus its common scale factor on the non-nickel layers handles both: the excess comes out of He
and H in proportion, and the scene note fires when the factor leaves 0.9–1.1.

### S2.2 Type Ia (near-Chandrasekhar)

Mazzali et al. 2007 (Science 315, 825, arXiv:astro-ph/0702351), nebular and photospheric analysis of 23 events [V]:

- Stable iron-group (⁵⁴Fe + ⁵⁸Ni) innermost: **0.20 ± 0.05 M☉**, the same in all.
- ⁵⁶Ni outside it: 0.1–0.9 M☉; M(⁵⁶Ni) = 1.34 − 0.67 Δm15(B).
- IME (Si-dominated) outside the iron group; the inner Si edge coincides with the outer NSE edge.
- Total burned (NSE + IME) **≥ 1.05 ± 0.09 M☉** (1.01 ± 0.05 excluding high-velocity-gradient events), independent of
  Δm15. M_Ch ≈ 1.38 M☉.
- Unburned C+O outside: M_ej − 1.05 = **0.33 M☉** for the lab's M_ej = 1.38 [D].

So with the outcome's M_Ni [D]: stable Fe = 0.20; IME = max(0, 1.05 − M_Ni − 0.20); CO = M_ej − (0.20 + M_Ni + IME).
Examples: M_Ni = 0.6 → 0.20 / 0.60 / 0.25 / 0.33; M_Ni = 0.4 → 0.20 / 0.40 / 0.45 / 0.33; M_Ni = 0.9 → IME 0, CO 0.28.
Mixing: Ia ejecta are stratified (that is Mazzali's result), so no Rayleigh–Taylor fingers (§S3).

Sub-Chandrasekhar and Iax [APPROX]: no layer masses found this session. Both take the near-Ch fractions of M_ej
(stable iron group 0.20/1.38 = 0.145, unburned CO 0.33/1.38 = 0.24) with IME the remainder. Iax are really
deflagrations whose ejecta are mixed (Kromer et al. 2013 [R]); the layering is schematic for them.

### App implementation (§S2)

Core collapse and pair instability (ruling P2, R-P2-12), masses in M☉ from `Outcome.Star` and the outcome:

    H    = Star.HydrogenEnvelopeMass
    He   = Star.HeliumEnvelopeMass
    rem  = RemnantModel.BaryonicMass(Remnant.Mass)   for a neutron star (Outcome has no baryonic field)
         = Remnant.Mass                              for a black hole
         = 0                                         for no remnant (pair instability)
    Ni   = NickelMass
    CO   = max(0, Star.CoCoreMass − rem − Ni)
    s    = (EjectaMass − Ni) / (H + He + CO)          one common factor on the non-nickel layers
                                                      (if Ni ≥ EjectaMass: Ni = EjectaMass and s = 0)
    layers = Ni, s·CO, s·He, s·H                      (centre outward)
    if s < 0.9 or s > 1.1: scene note "Layer masses scaled by ×{s:0.00} to match the published ejecta mass"

Type Ia (ruling P3's three layers, centre outward; M = EjectaMass) [D from Mazzali 2007]:

    near-Chandrasekhar (M ≥ 1.3):  Fe+Ni = min(M, 0.20 + NickelMass)   (drawn as "nickel and iron")
                                   IME   = max(0, min(M − Fe+Ni, 1.05 − Fe+Ni))
                                   CO    = M − Fe+Ni − IME
    sub-Ch and Iax (M < 1.3):      Fe+Ni = min(M, 0.145 M + NickelMass);  CO = min(M − Fe+Ni, 0.24 M);  IME = M − Fe+Ni − CO

Layers are assigned on the particles' stratified q_i (§S1), then mixing (§S3) moves particles. Test: each layer's
drawn mass within 1% of these (spec success criterion 2).

---

## §S3 Mixing reach

Mixing is modelled (spec §3.2) as K finger cones covering a fraction f of the sphere: nickel moved outward up to
NickelMax, hydrogen inward down to HydrogenMin. Published reach, then the app numbers. Where velocities are also given
as multiples of v_t (§S1.2, computed with the model's E and M_ej) they are [D].

### S3.1 SN 1987A (blue supergiant), observed [V via Utrobin et al. 2019, A&A 624, A116, arXiv:1812.11083, §1]

- Bulk of ⁵⁶Ni to **≈ 3,000 km/s** (Colgan et al. 1994): 0.91 v_t for the preset's E and M_ej [D].
- A fast ⁵⁶Ni clump of ~10⁻³ M☉ at **4,700 km/s** (Utrobin, Chugai & Andronova 1995): 1.43 v_t [D].
- Hydrogen down to **≤ 700 km/s** (Kozma & Fransson 1998): 0.21 v_t [D]; 600 km/s (Chugai 1991); H₂ at 400–800 km/s
  (Larsson et al. 2019); peaked profiles suggest mixing to near zero (Maguire et al. 2012). Kozma & Fransson put
  ≈ 2.2 M☉ of hydrogen-rich gas inside 2,000 km/s.
- 3D neutrino-driven models of SN 1987A mix hydrogen to < 400 km/s, and to < 100 km/s in the innermost ejecta, but
  only one older model (B15, a 6 M☉ He core) reaches the observed 3,000 km/s for the bulk of ⁵⁶Ni; "no single-star
  progenitor" yet satisfies all constraints (Utrobin et al. 2019) [V]. Their B15-2 (E 1.40 B, 14.2 M☉): bulk 3,370,
  tail 3,500, H to 270 km/s [V, Table 2].
- Iron-nickel bubbles fill ~30% of the metal core's volume (Li, McCray & Sunyaev 1993, ApJ 419, 824) [R, secondary
  quote only].

### S3.2 3D simulations by class

- **Blue supergiants.** Hammer, Janka & Müller 2010 (B15, 1 B): Ni to ≈ 4,500 km/s, H to ≈ 300 km/s [V via WMJ15 §3].
  The same initial model re-run: Ni to 3,800–4,200, H to 500–700 km/s, with **two pronounced nickel plumes and two
  smaller nickel fingers** [V, WMJ15 §3.2]. WMJ15's own BSG runs: Ni ≤ 3,400 km/s (B15) and ≤ 2,200 km/s (N20, which
  stays round); no significant hydrogen below 1,000 km/s except a tail to 500 km/s (B15) [V]. In B15-1-pw **more than
  half the nickel mass is in elongated fingers faster than ≈ 1,100 km/s** [V, WMJ15 §5.3]. As multiples of v_t
  [D, M_ej assumed 13.6–14.9 M☉, APPROX]: 0.87 (B15-1-pw), 0.56 (N20-4), 1.38 (HJM10).
- **Red supergiants.** Wongwathanarat, Müller & Janka 2015 (A&A 577, A48, arXiv:1409.5431) [V]: "deep penetration of
  Ni fingers into the H-envelope (with maximum velocities of 4000–5000 km s⁻¹ for an explosion energy around 1.5
  bethe)"; Table 2 v_max(Ni) = 5,290, 4,200, 4,780, 5,010 km/s at 1.48–2.75 B; mixing "comprises almost the whole
  hydrogen envelope" in mass; hydrogen below 1,000 km/s, into the innermost 2 M☉; "pronounced clumpiness and
  substructure". As multiples of v_t [D, M_ej ≈ 11.5 M☉ APPROX, from a 15 M☉ star]: 0.82–1.18, mean ≈ 1.0;
  hydrogen < 0.22 v_t. Chevalier 2005: in II-P, "H-rich material is mixed back to a velocity of a few 100 km s⁻¹ or
  less" [V].
- **Stripped envelope.** Only one model found: Kifonidis et al. 2003's 2D "Type Ib-like" explosion (BSG with the H
  envelope removed, E ≈ 1.8 B) [V]: Ni, Si and Ti frozen at ≤ 3,500 km/s, O to 5,500 km/s; metals mixed through the
  inner 2.0 M☉ (of a 5.1 M☉ star). ≈ 0.42 v_t for Ni [D, M_ej ≈ 3.7 M☉ APPROX]. Early on, "about 10 cusps ... about
  20° apart" grow at the Si/O interface (2D) [V]. Cas A: clumps up to ≈ 6,000 km/s (van den Bergh 1971; Thorstensen
  et al. 2001) [V via Kifonidis 2003].
- **Type Ia:** stratified (§S2.2) — no fingers.
- **Pair instability.** Joggerst & Whalen 2011 (ApJ 728, 129, arXiv:1010.4360), 2D Z = 0 pair-instability explosions
  of 150–250 M☉ [V, read this session via ar5iv]: "we find no mixing in most 150 - 250 M⊙ pair-instability
  supernovae". Compact (blue, z-series) progenitors form no reverse shock and show no Rayleigh–Taylor mixing at all.
  Extended (red supergiant, u-series) progenitors all form a reverse shock, but it dies out before rapid growth in
  u150 and u175 and stalls in the linear phase in u250. Only u200 and u225 mix vigorously: the oxygen layer is
  disrupted and helium mixes into the hydrogen envelope, but the mixing never breaks through the silicon layer, so
  ⁵⁶Ni stays inside.

### S3.3 Finger count and covered fraction

- **K:** blue supergiants, 4 (two big plumes, two smaller fingers) [V, WMJ15/HJM10]. Red supergiants: not counted in
  the papers ("pronounced clumpiness and substructure", initial plumes fragment into smaller fingers) [V];
  **[APPROX] K = 8** (twice the BSG count, for the stronger fragmentation WMJ15 describe). Stripped: **[APPROX] K = 10**
  (Kifonidis 2003's ≈ 10 cusps, 20° apart, in 2D).
- **f (fraction of the sphere's solid angle):** **not found** in any source read. [APPROX] BSG f = 0.10 (four cones of
  half-angle 18°: 4 × (1 − cos 18°)/2 = 0.098); RSG and stripped f = 0.20 (more, narrower fingers). Schematic, labelled.
- **Fraction of nickel in the fingers, F_Ni:** 0.5 (WMJ15 B15-1-pw, > 50% of the nickel mass in fingers faster than
  ≈ 1,100 km/s) [V], the only published value. Used for the other core-collapse classes [APPROX]: red supergiants mix
  at least as strongly as blue supergiants (WMJ15), and no stripped-envelope value was found.

### App implementation (§S3)

One value per class (ruling R-P2-13). NickelMax is the finger-tip speed. v_t is §S1.2's for the outcome.

| Class | NickelMax (tip) | HydrogenMin | K | f | F_Ni |
|---|---|---|---|---|---|
| Red supergiant | 1.0 v_t [D, WMJ15's v_max(Ni) on the 3% isosurface: 0.82–1.18 v_t] | 0.2 v_t [D, WMJ15 < 0.22] | 8 [APPROX] | 0.2 [APPROX] | 0.5 [APPROX] |
| Blue supergiant (incl. compact IIb) | 1.4 v_t [D, SN 1987A's clump 1.43; HJM10 1.38] | 0.2 v_t [D, SN 1987A ≤ 0.21] | 4 [V] | 0.1 [APPROX] | 0.5 [V] |
| Stripped envelope | 0.4 v_t [D, one 2D model; APPROX for the class] | none (no hydrogen layer) | 10 [APPROX] | 0.2 [APPROX] | 0.5 [APPROX] |
| Type Ia, Iax | no mixing | — | 0 | 0 | 0 [V, stratified] |
| **SN 1987A-like (Outcome.HasEquatorialRing)** | **4,700 km/s** [V] | **700 km/s** [V] | 4 [V] | 0.1 [APPROX] | 0.5 [V] |
| **Pair instability (any envelope; ruling R-P2-17)** | no mixing | — | 0 | 0 | 0 [V, Joggerst & Whalen 2011] |

Rules (R-P2-13, R-P2-17):

- Pair instability takes its envelope's §S1 profile but no fingers, whatever the envelope. Joggerst & Whalen 2011
  found partial mixing in two extended (red) models, u200 and u225. It never carried nickel out, and it is disclosed
  (inconsistency #41) rather than drawn.

- K cone axes are seeded and isotropic; each cone's half-angle θ is set so the union of the K cones covers a fraction
  f of the sphere (ruling R-P2-3).
- **Nickel:** a fraction F_Ni of the nickel particles, chosen by seed, is redirected into the cones: the particle's
  direction is re-drawn uniformly inside the nearest cone, and its speed becomes v' = v + (NickelMax − v)·u², with u
  seeded and uniform in [0, 1). A particle already faster than NickelMax keeps its speed.
- **Hydrogen [APPROX, the mirror of the nickel rule]:** a hydrogen particle whose direction lies inside a cone gets
  v' = v − (v − HydrogenMin)·u², if v > HydrogenMin.
- Mixing changes directions and speeds only, never masses. Tests: the moved nickel fraction is F_Ni ± 0.03 and every
  moved particle lies inside a cone; max nickel speed ≤ max(NickelMax, unmoved speeds); every moved hydrogen particle
  has a speed ≥ HydrogenMin; the layer masses are unchanged.
- SN 1987A's observed bulk-nickel edge (3,000 km/s = 0.91 v_t) is the result to compare against, not a parameter:
  with u² weighting, the median moved particle travels a quarter of the way to NickelMax.

---

## §S4 Shocks

### S4.1 Self-similar interaction with a wind (Chevalier 1982; s = 2)

For ejecta ρ ∝ t^(n−3) r^−n meeting ambient ρ ∝ r^−s, R_c ∝ t^((n−3)/(n−s)) and the forward shock (R₁) and reverse
shock (R₂) sit at fixed ratios to the contact R_c (Chevalier 1982, ApJ 258, 790, Table 1). The original table could not
be read (scanned images only). The ratios below were **computed here** [D] by integrating the adiabatic self-similar
equations (γ = 5/3) from each strong shock to the contact (where the gas moves with the contact, U = λ). Validation:

- s = 0: R₁/R₂ = 1.386, 1.263, 1.214, 1.188, 1.171, 1.151, 1.139 for n = 6, 7, 8, 9, 10, 12, 14; Truelove & McKee 1999
  Table 6 lists l_ED (= R_b/R_r, "repeated from Chevalier 1982") 1.39, 1.26, 1.21, 1.19, 1.17, 1.15, 1.14 [V]. All agree.
- s = 0 absolute: R_b = 1.070, 1.147, 1.209 t*^((n−3)/n) for n = 7, 10, 12 (TM units); TM99 Table 7 gives 1.06 (n = 7)
  and Tang & Chevalier 2017 Table 3 give 1.06, 1.15, 1.21 [V].
- s = 2, n = 7: R₁/R₂ = 1.339; TM99 Table 9 (from Chevalier 1982): 1.19/0.890 = 1.337 [V].
- s = 2, n ≈ 10: R₁/R_c = 1.239, R₂/R_c = 0.984; Chomiuk et al. 2016 quote "R_FS/R_c ≈ 1.24" and "R_RS/R_c ≈ 0.98"
  from Chevalier 1982 Table 1 [V via Chomiuk]. (Also s = 0: 1.131/0.966 vs their 1.13/0.97.)

| s = 2 | n = 7 | n = 10 | n = 10.18 | n = 11.73 | n = 12 |
|---|---|---|---|---|---|
| R_forward/R_c | 1.2987 | 1.2389 | 1.2374 | 1.2272 | 1.2259 |
| R_reverse/R_c | 0.9700 | 0.9836 | 0.9840 | 0.9870 | 0.9874 |

(s = 0 for reference: n = 7: 1.1811/0.9350; n = 10: 1.1313/0.9661; n = 12: 1.1214/0.9743.) The thin-shell estimate
(CF17 eq. 7) agrees with the full solutions to within ≈ 30% [V, CF17]. Note: the absolute s = 2 radius computed here (0.81 t*^0.8 for
n = 7) matches Tang & Chevalier's Table 4 (0.83) but not TM99 Table 9 (1.19); the ratios agree with both. Only the
ratios are used below.

### S4.2 Truelove & McKee 1999: uniform ambient gas

Truelove & McKee 1999 (ApJS 120, 299; full text read this session) [V].

**Characteristic units** (TM99 eq. 1–3 and Table 2): R_ch = M_ej^{1/3} ρ₀^{−1/3}, t_ch = E^{−1/2} M_ej^{5/6} ρ₀^{−1/3}.
TM99 write ρ₀ = 1.4 m_H n₀, giving 3.07 pc and 423 yr [V]; the lab writes ρ₀ = n₀ m_p (signals §7.1), giving
**3.43 pc M_ej^{1/3} n₀^{−1/3} and 473 yr E51^{−1/2} M_ej^{5/6} n₀^{−1/3}** [D]. Use the lab's convention throughout.
R* = R/R_ch, t* = t/t_ch. Example [D]: 10⁵¹ erg, 10 M☉, n₀ = 1: t_ch = 3,223 yr, R_ch = 7.4 pc, so the lab's
10–1,000 yr window is t* = 0.003–0.31. Type Ia (1.38 M☉): t_ch = 619 yr, t* = 0.016–1.6.

**Which n applies:**

- **Type Ia → n = 7** (TM99's explicit Table 7). TM99: interest in "the n = 7 power-law envelope as a model for outer
  ejecta produced by Type Ia supernovae can be traced to Colgate & McKee (1969) and Chevalier (1981)" and realistic Ia
  remnants "may have n ≈ 7" [V]. During the remnant era the reverse shock runs through ≈ 10,000–20,000 km/s, where the
  exponential profile's local slope is 4–8 [D], so n = 7 suits the shocks even though §S1 draws the particles with
  Kasen's n = 10.
- **Core collapse → TM99's tabulated n = 12 (red supergiant) and n = 10 (blue supergiant, stripped)** (Table 6) [V];
  TM99 has no 10.18 or 11.73 row. n = 0 (uniform ejecta, Table 5) fits no real class; it is what Project 1 uses.

**Forward shock (blast wave):**

| n | Ejecta-dominated (t* ≤ t*_ST) | Sedov–Taylor (t* ≥ t*_ST) | t*_ST |
|---|---|---|---|
| 0 | 2.01 t* (1 + 1.72 t*^{3/2})^{−2/3} [V Table 5] | (1.42 t* − 0.254)^{2/5} [V] | 0.495 |
| 7 | 1.06 t*^{4/7} [V Table 7] | (1.42 t* − 0.312)^{2/5} [V] | 0.732 |
| 10 | 1.147 t*^{0.7} [D] | (1.42 t* − 0.2935)^{2/5} [D] | 0.481 [V] |
| 12 | 1.210 t*^{0.75} [D] | (1.42 t* − 0.2809)^{2/5} [D] | 0.424 [V] |

The n = 10 and 12 rows are TM99's direct coupling of the Chevalier–Nadezhin solution to the offset Sedov law (§9),
rebuilt here from Table 6 (b = R*_ST / t*_ST^((n−3)/n); offset = R*_ST^{5/2} − 2.026^{1/2} t*_ST); the same
construction reproduces Table 7's n = 7 row (1.053 vs 1.06; −0.3134 vs −0.312) [D]. Equivalent smooth form (Tang &
Chevalier 2017 Table 1, eq. 24) [V]: R_b* = [(b t*^((n−3)/n))^−α + (2.026 t*²)^(−α/5)]^(−1/α) with (b, α) = (1.06, 20.3),
(1.15, 8.91), (1.21, 7.11) for n = 7, 10, 12 (Table 3, s = 0); for n = 0, R_b* = [(2.01 t*)^−2.5 + (2.026 t*²)^−0.5]^−0.4.
This is exactly Project 1's code path.

**Reverse shock** (position in the lab frame; it reaches the centre where the formula reaches zero):

| n | Envelope / early | Core / late (t* up to ≈ 2.1–2.2) | t*_core |
|---|---|---|---|
| 0 | 1.83 t* (1 + 3.26 t*^{3/2})^{−2/3}, t* ≤ 0.495 [V Table 5] | t* (0.779 − 0.106 t* − 0.533 ln t*) [V] | — |
| 7 | 0.841 t*^{4/7} [V Table 7] | t* (0.815 − 0.116 t* − 0.511 ln t*) [V] | 0.363 |
| 10 | 0.980 t*^{0.7} [D] | t* (0.9506 − 0.192 t* − 0.4208 ln t*) [D] | 0.220 [V] |
| 12 | 1.052 t*^{0.75} [D] | t* (1.0468 − 0.251 t* − 0.3573 ln t*) [D] | 0.182 [V] |

The n = 10/12 rows use R_r = R_b/l_ED in the envelope and TM99 eq. 83 in the core, R_r* = t*[R*_r,core/t*_core −
ã(t* − t*_core) − (ṽ − ã t*_core) ln(t*/t*_core)], with Table 6's (R*_r,core, ṽ*_r,core, ã*_r,core) = (0.340, 0.463,
0.192) and (0.293, 0.403, 0.251); they are continuous at t*_core to 0.001, and the same rebuild of n = 7 gives
0.8164 − 0.116 t* − 0.5109 ln t*, matching Table 7 [D]. The early n = 0 and late n = 0 pieces meet at t* = 0.495 to
0.2% [D].

**Time for the reverse shock to reach the centre:** formula roots t* = 2.58 (n = 0), 2.69 (7), 2.74 (10), 2.74 (12)
[D]. These lie past TM99's stated validity (t* ≈ 2.1–2.2; "the bounce process begins at t* ≈ 1.3") [V]. Tang &
Chevalier's simulation of n = 0 gives **t*_c = 2.37** [V], 8% earlier than the formula. In years [D]: ≈ 8,800 yr for
10⁵¹ erg, 10 M☉, n₀ = 1; ≈ 1,660 yr for a Type Ia. Both are past the lab's 1,000-yr window, except the Ia's centre
approach.

**Contact discontinuity** (Tang & Chevalier 2017, Table 2 eq. 32 and Table 5, s = 0) [V]:
R_c* = [(ζ_c t*^((n−3)/n))^−α + (c t*^b)^−α]^(−1/α), valid to t*_lim:

| n | α | b | c | ζ_c | t*_lim |
|---|---|---|---|---|---|
| 0 | 0.89 | −0.37 | 1.66 | (10/3)^{1/2} = 1.826 and t*^1 in place of t*^((n−3)/n) [D, the uniform ejecta's edge] | 4 |

(n = 0 caveat [D]: this contact and TM99's n = 0 reverse shock come from different fits. Taken as raw radii, the
reverse shock lies up to 1.0% outside the contact for t* below ≈ 0.16; as ratios to their own forward shocks, up to
0.5% for t* below ≈ 0.08. The App block uses the ratios and caps R_r at R_c.)
| 7 | 4.28 | −0.11 | 1.09 | 0.89 | 4.5 |
| 10 | 2.92 | −0.12 | 1.07 | 1.01 | 4 |
| 12 | 2.35 | −0.16 | 1.11 | 1.08 | 4 |

(ζ_c checks: forward coefficient / (R₁/R_c from §S4.1) = 1.06/1.181 = 0.90; 1.147/1.131 = 1.01; 1.210/1.121 = 1.08 [D].)

### S4.3 Comparison with Project 1's blast wave

Project 1 (`RemnantEvolutionModel`) uses Tang & Chevalier's n = 0 form [λ = 2.01, α = 1.25] (mislabelled Leahy &
Williams; C9). Forward-shock radius R* [D]:

| t* | P1 (TC17, n = 0) | TM99 n = 0 | TM99 n = 7 | TM99 n = 12 |
|---|---|---|---|---|
| 0.003 (≈ 10 yr, CC example) | 0.0060 | 0.0060 (0.0%) | 0.0383 (+536%) | 0.0155 (+157%) |
| 0.01 | 0.0201 | 0.0201 (0.0%) | 0.0763 (+280%) | 0.0383 (+91%) |
| 0.031 (≈ 100 yr, CC example) | 0.0618 | 0.0619 (+0.2%) | 0.1456 (+136%) | 0.0894 (+45%) |
| 0.1 | 0.1916 | 0.1940 (+1.3%) | 0.2844 (+48%) | 0.2152 (+12%) |
| 0.2 | 0.3555 | 0.3654 (+2.8%) | 0.4226 (+19%) | 0.3619 (+2%) |
| 0.31 (≈ 1,000 yr, CC example) | 0.5046 | 0.5240 (+3.8%) | 0.5428 (+8%) | 0.5027 (−0.4%) |
| 0.4 | 0.6071 | 0.6319 (+4.1%) | 0.6279 (+3%) | 0.6086 (+0.2%) |
| 0.6 | 0.7910 | 0.8141 (+2.9%) | 0.7917 (+0.1%) | 0.7993 (+1%) |
| 1 | 1.0538 | 1.0634 (+0.9%) | 1.0419 (−1.1%) | 1.0535 (0%) |
| 3 | 1.7541 | 1.7421 (−0.7%) | 1.7320 (−1.3%) | 1.7375 (−0.9%) |
| 10 | 2.8838 | 2.8693 (−0.5%) | 2.8645 (−0.7%) | 2.8671 (−0.6%) |

(The Type Ia window, t* = 0.016–1.6, sees the n = 7 forward shock +211% (10 yr) to −1.4% (1,000 yr) against Project 1's [D].)

- For the same (uniform) ejecta, TM99 and Project 1 agree to within 4.1% at every t* [D].
- For realistic steep ejecta the forward shock is far ahead at early times and converges by t* ≈ 0.4–0.6 [D].

### App implementation (§S4)

Rulings R-P2-4, R-P2-10 and R-P2-11. One rule for all three radii:

    R_f(t) = EjectaKinematics.RadiusCm(o, t)          the drawn forward shock = Project 1's radius, always
    R_c(t) = q_c(t) · R_f(t)
    R_r(t) = min(q_r(t) · R_f(t), R_c(t))

**Start.** Shocks exist from t_start = the circumstellar model's start for outcomes `CsmInteractionModel` applies to,
otherwise (Type Ia, Iax) t_start = `EjectaKinematics.BlendStartS` (3 yr). Particle confinement (ruling P4) is applied
with weight w = clamp(log10(t / t_start), 0, 1), r = lerp(free, confined, w), so nothing jumps when shocks appear.

**Wind-era ratios** (Chevalier 1982, s = 2, §S4.1, with the class's n from §S1): q_c,wind = 1/q₁, q_r,wind = q₂/q₁:

| Class (§S1) | n | q_c,wind | q_r,wind |
|---|---|---|---|
| Red supergiant | 12 | 0.8157 | 0.8054 |
| Blue supergiant, stripped envelope | 10.18 | 0.8081 | 0.7952 |

**Remnant-era ratios** (uniform ejecta, n = 0, for every class: R-P2-10), each the ratio to its own paper's forward
shock, with t* = t / t_ch in the lab's units (§S4.2: 473 yr E51^{−1/2} M_ej^{5/6} n₀^{−1/3}, n₀ = 1 as in Project 1):

    q_c,rem(t*) = R_c,TC(t*) / R_b,TC(t*)
       R_c,TC = [ (1.826 t*)^−0.89 + (1.66 t*^−0.37)^−0.89 ]^(−1/0.89)        (TC17 eq. 32, Table 5)
       R_b,TC = [ (2.01 t*)^−2.5 + (2.026 t*²)^−0.5 ]^−0.4                   (TC17 Tables 1, 3 = Project 1's form)
       for t* > 4 (TC17's t*_lim) hold q_c,rem at its t* = 4 value, 0.4208 [APPROX]
    q_r,rem(t*) = R_r,TM(t*) / R_b,TM(t*)
       t* ≤ 0.495:  R_r,TM = 1.83 t* (1 + 3.26 t*^{3/2})^{−2/3};   R_b,TM = 2.01 t* (1 + 1.72 t*^{3/2})^{−2/3}
       t* > 0.495:  R_r,TM = max(0, t* (0.779 − 0.106 t* − 0.533 ln t*));   R_b,TM = (1.42 t* − 0.254)^{2/5}
                                                                            (TM99 Table 5)

Values [D]: t* = 0.01: q_c 0.906, q_r 0.910 (so the min() binds below t* ≈ 0.08, by < 0.5%); 0.1: 0.887, 0.884;
0.3: 0.866, 0.808; 1: 0.758, 0.633; 2: 0.602, 0.270; q_r = 0 from t* = 2.58.

**Blend** between the eras, over Project 1's own window (3 yr → 10 yr), geometric with the same smoothstep in log t as
`EjectaKinematics.RadiusCm`:

    t ≤ 3 yr:        q = q_wind
    3 yr < t < 10 yr: x = ln(t / 3 yr) / ln(10/3),  s = x²(3 − 2x),  ln q = (1 − s) ln q_wind + s ln q_rem(t*)
    t ≥ 10 yr:       q = q_rem(t*)

Outcomes with no wind era (t_start = 3 yr) use q_rem from the start, with no blend. The class's own n (§S4.2) is
**not** drawn; the gap is disclosed in the overlay (inconsistency #33). Tests: TM99 Tables 5 and 7 values at t* = 0.1,
0.495, 1, 2; zero crossings 2.58 (n = 0) and 2.69 (n = 7) ± 0.01; §S4.1 ratios to 0.001; R_r ≤ R_c ≤ R_f at every t;
q continuous at 3 yr and 10 yr.

---

## §S5 The shock inside the star (bounce to breakout)

Kistler, Haxton & Yüksel 2013 (ApJ 778, 81, arXiv:1211.6770) obtain the breakout delay by integrating Matzner &
McKee's shock speed through the star [V, eq. 3–4]:

    t(R) = 1.26 ∫ from R_NS to R  (m(r)/E)^{1/2} (ρ₀(r) r³ / m(r))^{0.19} dr

and fit polytropes with it (their eq. 5, the formula `ShockBreakoutModel.KistlerDelayS` uses). Re-integrating eq. 4
here on Lane–Emden polytropes (m = enclosed mass minus the neutron star) reproduces eq. 5 to within 1% for x =
M_NS/M_ej = 0–0.47 (n = 3/2: 7,222 vs 7,226 s at x = 0; n = 3: 6,890 vs 6,851 s) [D]. So the same integral gives the
shock's position at every time, and it reaches R* exactly at the delay the app already uses.

**[APPROX] form.** On a polytrope the neutron-star mass sits inside r_in = 0.29 R* (n = 3/2) or 0.14 R* (n = 3), but
in a real star that mass is in a core far smaller than 10⁻² R* (§S6), which the shock crosses in a small fraction of
the delay (B15: (C+O)/He at 1.8 s, He/H at ≈ 80 s, Kifonidis 2003 [V], against a delay of ≈ 6,400 s for that star
[D, Kistler eq. 5 with 1.8 B, 13.6 M☉, 56 R☉]). So the polytrope's r_in is mapped to the centre:
r'/R* = (r − r_in)/(R* − r_in). Result for x = 0.14 (the research's 1.4 M☉ / 10 M☉ example) [D]:

| τ = t/t_bo | 0 | 0.05 | 0.1 | 0.2 | 0.3 | 0.4 | 0.5 | 0.6 | 0.7 | 0.8 | 0.9 | 0.95 | 1 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| R_s/R*, n = 3/2 (RSG) | 0 | 0.110 | 0.180 | 0.291 | 0.385 | 0.470 | 0.551 | 0.630 | 0.709 | 0.790 | 0.879 | 0.930 | 1 |
| R_s/R*, n = 3 (BSG, WR) | 0 | 0.079 | 0.129 | 0.212 | 0.287 | 0.360 | 0.433 | 0.510 | 0.593 | 0.686 | 0.798 | 0.870 | 1 |

The table changes by ≤ 0.03 for x = 0.05–0.47 [D]. Cross-check: for B15 the table gives R_s ≈ 0.020 R* at τ = 80/6,400
= 0.0125, against the He/H interface at 0.018 R* crossed at 80 s (Kifonidis 2003) [D].

### App implementation (§S5)

R_s(t) = R* · F_n(t / t_bo) for 0 ≤ t ≤ t_bo, F_n by linear interpolation in the table, with n chosen exactly as
`ShockBreakoutModel` chooses it (3/2 for H-rich R ≥ 158 R☉, else 3) and t_bo = `EjectaKinematics.BreakoutTimeS`.
F(0) = 0, F(1) = 1, monotonic. Tests: R_s(t_bo) = R*; R_s increasing; the two table rows.

---

## §S6 Layer radii inside the presupernova star

| Star | C+O/He interface | He/H interface (He core edge) | R* | Fractions of R* | Source |
|---|---|---|---|---|---|
| RSG, 15 M☉ (RSG15) | — | 3.568 × 10¹⁰ cm (4.27 M☉, X_H = 1%) | ≈ 5 × 10¹³ cm | He ≈ 7 × 10⁻⁴ | Lovegrove & Woosley 2013 Table 1 and text [V]; fraction [D] |
| RSG, 25 M☉ (RSG25) | — | 1.807 × 10¹⁰ cm (8.20 M☉) | ≈ 5 × 10¹³ cm | He ≈ 4 × 10⁻⁴ | same [V]; [D] |
| BSG, 15 M☉ (B15, SN 1987A model) | 3.55 × 10⁹ cm (log r = 9.55) | 7.08 × 10¹⁰ cm (log r = 10.85) | 3.9 × 10¹² cm | CO 9.1 × 10⁻⁴; He 1.8 × 10⁻² | Kifonidis et al. 2003 [V]; [D] |
| BSG merger model b18.3 | 3.5 × 10⁹ cm | 2.9 × 10¹⁰ cm | 2.12 × 10¹² cm | CO 1.7 × 10⁻³; He 1.4 × 10⁻² | Ono et al. 2020, ApJ 888, 111, arXiv:1912.02234 [V]; [D] |
| BSG single model n16.3 | 6.1 × 10⁹ cm | 5.2 × 10¹⁰ cm | 3.39 × 10¹² cm | CO 1.8 × 10⁻³; He 1.5 × 10⁻² | same [V]; [D] |
| Wolf–Rayet | not found | the whole star is the He core | 0.4–20 R☉ in the lab's S16 grid | — | [APPROX] |

RSG CO core [APPROX]: no value found. MM99: core and mantle evolution "appears to be nearly independent of mass loss
... while a hydrogen envelope persists" [V], so the BSG cores' 3.5–6 × 10⁹ cm are taken for RSGs too (≈ 10⁻⁴ R*).
Wolf–Rayet CO core [APPROX]: the same absolute 4.2 × 10⁹ cm, capped at half the stellar radius.

At the star's scale an RSG's He core (≤ 10⁻³ R*) is far below a pixel: the cutaway will show the hydrogen envelope
only, and the inner layers become visible after expansion, in velocity space (§S1–§S3).

### App implementation (§S6)

- r_CO = min(4.2 × 10⁹ cm, 0.5 R*) [APPROX]: 4.2 × 10⁹ cm is the geometric mean of the three verified BSG values
  (3.55, 3.5 and 6.1 × 10⁹ cm) and is used for every class (see above).
- r_He = min(3.6 × 10¹⁰ cm, R*) for hydrogen-rich stars [APPROX]: 3.6 × 10¹⁰ cm is the geometric mean of the
  verified range 1.8–7.1 × 10¹⁰ cm and equals Lovegrove & Woosley's 15 M☉ red supergiant. The lab has no per-star He-core
  radius.
- For stripped stars r_He = R*.
- Draw nothing smaller than one pixel.

---

## §S7 Surroundings

### S7.1 The wind's outer extent

- A slow wind stops where its ram pressure meets the surrounding pressure (Chevalier 2005, ApJ 619, 839,
  arXiv:astro-ph/0409013, eq. 11) [V coefficient]: r_RSG = 5.0 pc (Ṁ / 5 × 10⁻⁵ M☉ yr⁻¹)^{1/2} (v_w / 15 km s⁻¹)^{1/2}
  (p/k / 10⁴ cm⁻³ K)^{−1/2}. The exponents follow from Ṁ v_w/(4π r²) = p; that relation gives 5.35 pc for the
  reference values [D].
- By type (Chevalier 2005) [V]: II-P red supergiants (Ṁ ≈ 1–2 × 10⁻⁶ M☉ yr⁻¹ at 10 km/s): the wind ends at **≈ 1 pc**
  inside a main-sequence wind bubble; IIL/IIb (≈ 3 × 10⁻⁵ at 15 km/s): **5 pc or more**; Ib/c (Wolf–Rayet, ≈ 10⁻⁵ at
  ≈ 10³ km/s for ≈ 2 × 10⁵ yr): the earlier RSG wind is swept into clumps at **≈ 10 pc**.
- Wind speeds (CF17 §2.2) [V]: red supergiants 10–50 km/s, Ṁ 10⁻⁶–10⁻⁵ (superwinds 10⁻⁴–10⁻³); Wolf–Rayet stars and
  blue supergiants 1,000–3,000 km/s, 10⁻⁶–10⁻⁴. SN 1987A's progenitor: ≈ 10⁻⁷ M☉ yr⁻¹ at ≈ 450 km/s, inside a
  photoionized H II region of ≈ 100 cm⁻³ [V, CF17 §6.5].
- SN 1987A's nebula (Sugerman et al. 2005, light echoes) [V]: an hourglass 1 ly in radius and 4 ly long; an outer
  "Peanut" (taken to be the RSG/main-sequence wind contact) extending 28 ly along the poles and 11 ly at the equator.

### S7.2 Dense shells by class

| Class / example | Dense material's extent | Source |
|---|---|---|
| II-P with confined CSM, SN 2023ixf | r = (0.5–1) × 10¹⁵ cm; Ṁ ≈ 10⁻² M☉ yr⁻¹ at 50 km/s; flash features to ≈ day 8 | Jacobson-Galán et al. 2023, ApJL 954, L42, arXiv:2306.04721 [V] |
| IIn, short-lived (SN 1998S-like; RSG superwind ≈ 10⁻⁴ M☉ yr⁻¹) | dense shell outer radius ≈ 10¹⁵ cm | Chugai 2001 (MNRAS 326, 1448, arXiv:astro-ph/0106234) abstract; Fassia et al. 2001 [R: read only as search-result excerpts]; class from CF17 §6.4 [V] |
| IIn, long-lasting (SN 2010jl-like; LBV-like ≈ 0.1 M☉ yr⁻¹, C ≈ 10³) | ≳ 3 × 10¹⁶ cm: shock at 4,000 km/s still in dense CSM near day 1,000 | CF17 §2.2, §3, §6.4 [V]; extent [D] = 4,000 km/s × 1,000 d |

### App implementation (§S7)

- Wind haze from the star's own A_* (unchanged), out to r_w = (Ṁ v_w / (4π p))^{1/2} with p/k = 10⁴ cm⁻³ K [APPROX
  ISM pressure], i.e. ≈ 0.6–2 pc for red supergiants and ≈ 20 pc for a Wolf–Rayet wind (Ṁ 10⁻⁵, 1,000 km/s) [D]. Beyond
  it, empty (bubble).
- Dense shell, only when `CsmInteractionModel.HasDenseCsm(o)` (the dense-CSM input, pulsational pair instability, or
  type IIn/Ibn/Icn). Outer radius:
  - `o.Type == SupernovaType.IIn`: **3 × 10¹⁶ cm** [D]. The lab's IIn is the class whose CSM is not modeled and whose
    radio is the brightest (signals §6.1: IIn mean 3 × 10²⁶ erg/s/Hz). That matches the long-lasting, SN 2010jl-like
    class (CF17 §6.4).
  - every other HasDenseCsm outcome: **10¹⁵ cm**. This is [V] for confined CSM around II-P stars (SN 2023ixf; the
    SN 2023ixf preset) and for 1998S-like IIn. It is [APPROX] for Ibn, Icn and pulsational-pair-instability shells,
    which were not researched.
  - Labelled with its source.

---

## §S8 SN 1987A's rings

| Quantity | Value | Source |
|---|---|---|
| Equatorial ring (ER) semi-major axis | 0.820″ ± 0.005″ (minor 0.600″) | Sugerman et al. 2005, ApJS 159, 60, arXiv:astro-ph/0502378, Table 2 [V] |
| ER radius | **6.29 × 10¹⁷ cm = 0.665 ly** at 51.4 kpc (Table 3's 0.647 ly × 51.4/50; the value the app uses) [D]. Cross-checks: 0.820″ × 51.4 kpc = 6.31 × 10¹⁷ cm [D]; Panagia: (6.23 ± 0.08) × 10¹⁷ cm (0.659 ly, 241 light-days) [R]; "0.65 light-years" (Barlow 2025, arXiv:2510.13629) [V]; "≈ 6 × 10¹⁷ cm" (CF17) [V] | |
| ER inclination | 43.0° ± 0.1° | Sugerman 2005 Table 2 [V] |
| ER major-axis position angle | 81.1° ± 0.8° E of N | same [V] |
| ER expansion | 10.5 ± 0.3 km/s | Crotts & Heathcote 2000, arXiv:astro-ph/9907367 [V] |
| Outer rings (semi-major axes) | north 1.77″ (b/a 0.678), south 1.84″ (b/a 0.513) | Burrows et al. 1995 [V via Sugerman 2005 §8.3] |
| Outer-ring radii (from the projected semi-major axes; superseded by the 3D fit below) | 1.36 × 10¹⁸ cm (1.44 ly), 1.42 × 10¹⁸ cm (1.50 ly) at 51.4 kpc [D]; "≈ 2 × 10¹⁸ cm" (CF17) [V] | |
| Offsets along the symmetry axis | north ring 1.3 ly, south ring 1.0 ly from the SN | Sugerman 2005 abstract [V]; Crotts & Heathcote's kinematic model: 1.32 and 1.04–1.24 ly [V via Sugerman] |
| Outer-ring inclinations (if circular) | north 43°, south 31° (so the south ring cannot be circular at 43°) | Burrows 1995 [V via Sugerman] |
| Outer-ring expansion; kinematic age | 26 km/s; ≈ 20,000 yr before the explosion | Crotts & Heathcote 2000 [V] |
| Asymmetry | the line joining the outer rings' centres misses the SN by ≈ 0.4″ | Burrows 1995 [V via Sugerman] |
| First hotspot | 1995 (≈ day 3,000), north-east side | signals §6.3; Barlow 2025 [V] |
| Ring brightening and peak | 26 hotspots by 2003; optical peak ≈ day 7,000 (CF17) to a plateau in 2009 (≈ day 8,000); fading since; R-band projected to reach zero ≈ 2035 (Larsson et al. 2019) | [V via CF17 and Barlow 2025] |
| Forward shock near the ring | 4,100–6,700 km/s; ≤ 500 km/s inside the dense clumps | Barlow 2025 [V] |
| **3D fit of all three rings (used by the app)** | at 50 kpc: ER a = 0.647 ly, z₀ = 0, P.A. 81.1°; north ring a = 1.42 ly, **z₀ = −1.36 ly**, P.A. 70.5°; south ring a = 1.59 ly, **z₀ = +1.00 ly**, P.A. −1.1° [V]. z₀ is "the distance to the SN along the axis" [V]; the table's remaining column (0.98, 0.94, 0.92) is read here as b/a, but the extracted header is ambiguous (no tag). | Sugerman 2005 Table 3 and §8.3 (extracted with pdftotext -raw) |
| Sign convention | "the standard orientation ... with z toward the observer", x west, y north; the rings' symmetry axis is that z axis inclined (rotated) south/north by i_x and east/west by i_y [V, Sugerman §5.3.1]. Positive z₀ therefore lies on the observer's side of the ER plane. Sugerman's Fig. 41 agrees: the north ring lies on the tube "opening away from the observer", the south ring on the one "opening toward the observer" [V]. The CS nebula's axis is inclined 41° south and 8° east of the line of sight (abstract) [V]. | |

### App implementation (§S8)

For SN 1987A-like outcomes only (`Outcome.HasEquatorialRing`). There is one geometry source, Sugerman et al. 2005
Table 3, with every length scaled by D/50 kpc, where D is the preset's distance [D]. At the preset's 51.4 kpc
(factor 1.028):

| Ring | Radius (semi-major axis) | Centre on the symmetry axis (signed; + = toward the observer) |
|---|---|---|
| Equatorial | 0.665 ly = 6.29 × 10¹⁷ cm | 0 (the SN) |
| North (far side, behind the ER plane) | 1.460 ly = 1.381 × 10¹⁸ cm | −1.398 ly = −1.323 × 10¹⁸ cm |
| South (near side, in front of the ER plane) | 1.635 ly = 1.546 × 10¹⁸ cm | +1.028 ly = +9.73 × 10¹⁷ cm |

- **Axis.** Sky coordinates are Sugerman's: x west, y north, z toward the observer. The axis's positive end points
  toward the observer, tilted 43.0° from the line of sight toward P.A. θ = 171.1°. That is the ER's minor axis, 90° from
  its 81.1° major axis, and lies just east of south [D]. With P.A. counted from north through east:

      â = sin 43.0° · (−sin θ, cos θ, 0) + cos 43.0° · (0, 0, 1) = (−0.1055, −0.6738, +0.7314)

  A ring with signed offset z₀ is centred at z₀·â. The scene's view direction is −z (looking from the observer
  toward the SN).
- Consequences, checkable in a render [D]: the south ring is in front of the ER plane and projects south of the SN; the
  north ring is behind it and projects north; the ER's north side is the near side (its point at P.A. 351° has line-of-sight coordinate +R sin 43° ≈ +0.45 ly).
- All three rings are drawn as circles in planes perpendicular to â.
- Schematic and labelled: the in-plane ellipticities, each ring's own position angle, the small centroid offsets
  (x₀, y₀ ≤ 0.26 ly) and Burrows' 0.4″ misalignment are not drawn.
- Hotspot brightness follows signals §6.3's ring component (logistic rise over 3,000–9,000 d, a plateau, and a decline
  after ≈ 10,000 d).

---

## §S9 Pulsar wind nebula

**Growth law in freely expanding ejecta** [V]:

- Uniform ejecta (van der Swaluw et al. 2001, A&A 380, 309, arXiv:astro-ph/0012440, eq. 12–13): **R_pwn = C (L₀ t /
  E₀)^{1/5} V₀ t, C ≈ 0.922** (γ_ej = 5/3, γ_pwn = 4/3), V₀ = (10 E₀ / 3 M_ej)^{1/2} — the same V₀ as
  EjectaKinematics' v_ej.
- General inner profile ρ = A t^{m−3} r^{−m} (Chevalier & Fransson 1992 eq. 2.6, as given by Chevalier 2005 eq. 26):
  **R_p = [ (5−m)³(3−m) / ((11−2m)(9−2m)) · L t^{6−m} / (4π A) ]^{1/(5−m)}**, so R ∝ t^{(6−m)/(5−m)}.
  For MM99's m = 1.06: R_p = 0.59 L₃₈^{0.254} E51^{0.246} (M_ej/10 M☉)^{−0.50} t₃^{1.254} pc (Chevalier 2005 eq. 27).
  For m = 0 this thin-shell law gives R = 0.824 (L t/E)^{1/5} V₀ t [D], 11% below van der Swaluw's 0.922.
- For §S1's δ = 1 core: m = 1, A = K v_t with K = M (3−δ)(n−3) / (4π v_t³ (n−δ)) [D], valid while R_p/t < v_t.

**The Crab as a check** [D]: from the preset's pins (P₀ = 16.845 ms, B = 3.794 × 10¹² G, I = 10⁴⁵, dipole n = 3):
Ṗ₀ = 8.35 × 10⁻¹³, τ₀ = 320 yr, Ė₀ = 6.9 × 10³⁹ erg/s; energy put out by 954 yr = 5.2 × 10⁴⁹ erg, an effective
constant L = 1.73 × 10³⁹ erg/s. Then for E = 10⁵⁰ erg and M_ej = 4.6 M☉ [R, typical Crab estimates]: van der Swaluw
1.51 pc; the m = 1 law with §S1's RSG profile 1.21 pc; Chevalier 2005's m = 1.06 law 0.96 pc. **Measured:** 7′ × 5′
at 2.0 kpc = 4.1 × 2.9 pc (signals §8.2) [D], a mean radius ≈ 1.7 pc; Hester 2008 gives 4.4 × 2.9 pc [R]. The models
are 10–45% small; E and M_ej of the Crab are uncertain by factors of a few (inconsistency #39).

**The Crab's outer shell is undetected.** Deep Hα imaging found a halo, but it was the instrument's scattered light,
which prevented detecting the predicted fast shell; their limit is ≈ 5 × 10⁻⁸ erg s⁻¹ cm⁻² sr⁻¹ (Tziamtzis et al. 2009,
A&A, arXiv:0902.1092) [R: read only as an excerpt of the abstract in a search result]. Sollerman et al. 2000 showed a
fast 4 M☉ shell would escape the deepest earlier Hα search (1.5 × 10⁻⁷) [R, same excerpt]. Chevalier 2005 notes the
Crab's forward shock "should be at a considerably larger radius" than the 2 pc nebula, probably in a low-density
bubble, and that for the Crab and 3C58 the interaction with the surroundings has not been observed [V]. On the
inconsistencies list as #31.

### App implementation (§S9)

R_pwn(t) from the general law with m = δ = 1 and A from §S1 (consistent with the drawn particles), L(t) = E_spun(t)/t
from the lab's pulsar spin-down (Ė₀ τ₀ t/(t + τ₀)); cap R_pwn at the reverse shock (§S4) and at v_t t. Draw the Crab's
outer shell faint and labelled "predicted, never detected". Test: R ∝ t^{5/4} at constant L; the formula's 128/63
coefficient for m = 1.

---

## §S10 Failed-supernova envelope ejection

When no shock is revived, the neutrino loss of 0.2–0.5 M☉ of gravitational mass from the core sends a pressure pulse
out through the star (Lovegrove & Woosley 2013, ApJ 769, 109, arXiv:1303.5055; Fernández et al. 2018, MNRAS 476,
2366, arXiv:1710.01735).

| Progenitor | Ejected mass | Energy | Speed | Source |
|---|---|---|---|---|
| Red supergiant (15 M☉) | most of the H envelope; 4.2–4.6 M☉ | ≈ 10⁴⁷ erg (KE at the envelope base 1.3–5.8 × 10⁴⁷ for TOV 2.0–2.5 M☉); 1.5–8.8 × 10⁴⁷ | 50–100 km/s; shock "a few 100 km s⁻¹" | LW13 abstract, Table 3, §4 [V]; F18 Table 2, §4.1 [V] |
| Red supergiant (25 M☉) | ejected only for TOV ≥ 2.3 M☉ (weaker shocks otherwise) | ≤ 2.8 × 10⁴⁷ | — | LW13 §4 [V] |
| Blue supergiant (25 M☉) | 0.049–0.11 M☉ (≈ 0.1 M☉) | 1.5–9.1 × 10⁴⁷ | breakout "a few 1000 km s⁻¹"; mean √(2E/M) ≈ 550–900 km/s [D] | F18 abstract, Table 2, §4.1 [V] |
| Wolf–Rayet (40 M☉) | 5 × 10⁻⁴ – 4.9 × 10⁻³ M☉ (≈ 10⁻³) | 2.3 × 10⁴⁶ – 3.0 × 10⁴⁷ | up to "a few 10⁴ km s⁻¹" at breakout; mean ≈ 2,200–2,500 km/s [D] | same [V] |

- The RSG transient: ≈ 10³⁹–10⁴⁰ erg/s for about a year, ≈ 3,000 K (LW13) [V]. Ejection fails for large envelopes
  and/or high core compactness, with fallback lasting hours to years (F18) [V].
- **Applies to the lab's failed-supernova range:** the lab's failed supernovae (outcomes §1.2: direct collapse at
  22–25 M☉ and most stars above 30 M☉) are red supergiants (22–33 M☉ in the S16 grid, 1,170–1,540 R☉), compact H-poor
  stars (35–40 M☉, 12–21 R☉) and Wolf–Rayet stars (≥ 45 M☉) — exactly the three classes simulated. LW13 expect such
  transients mainly from 20–25 M☉ [V].

### App implementation (§S10)

For a failed supernova, draw a shell labelled "weak envelope ejection, Lovegrove & Woosley 2013; Fernández et al.
2018". The class comes from §S1's test. Each value is the table's midpoint [D]; choosing one value is [APPROX].

| §S1 class | Shell mass M_s | Speed v_s |
|---|---|---|
| Red supergiant | min(4.5 M☉, HydrogenEnvelopeMass) | 75 km/s |
| Blue supergiant (compact, H-poor 35–40 M☉ stars) | 0.1 M☉ | 700 km/s |
| Stripped (Wolf–Rayet) | 10⁻³ M☉ | 2,300 km/s |

E_s = ½ M_s v_s². The shell starts at the star's surface when the pulse arrives:

    t_s = ShockBreakoutModel.KistlerDelayS(n, energyErg = E_s, ejectaMass = max(M_s, FinalMass − M_BH),
                                           radiusRsun = R*/R☉, remnantMass = M_BH)

Here n is chosen as in §S5, M_BH = Remnant.Mass, and "ejectaMass" is the mass the pulse crosses (the star above the
black hole), not the shell. Kistler's fit is for explosion shocks, so applying it to this weak pulse is [APPROX].
Check [D]: for the S16 22 M☉ red supergiant (M_f 16.2, M_BH = M_He = 7.0, 1,168 R☉, E_s = 2.5 × 10⁴⁷ erg) this gives
7.6 × 10⁶ s, the same order as Fernández et al.'s ≈ 10⁷ s ("shortly after the shock breaks out") [V].

After t_s the shell coasts: r = R* + v_s (t − t_s). The panels are unchanged (the lab's failed supernova has no ejecta).

---

## References read this session

Barlow 2025, arXiv:2510.13629 (review). Chevalier 2005, ApJ 619, 839, arXiv:astro-ph/0409013. Chevalier & Fransson 2017
(Handbook of Supernovae), arXiv:1612.07459. Chomiuk et al. 2016, ApJ 821, 119, arXiv:1510.07662. Crotts & Heathcote 2000,
arXiv:astro-ph/9907367 (abstract). Fernández et al. 2018, MNRAS 476, 2366, arXiv:1710.01735. Jacobson-Galán et al.
2023, arXiv:2306.04721 (abstract). Joggerst & Whalen 2011, arXiv:1010.4360 (read via ar5iv in Task 5's fix round; journal
ref ApJ 728, 129 from the reviewer [R]). Kasen 2010, ApJ 708, 1025, arXiv:0909.0275. Kifonidis et al. 2003, A&A 408, 621,
arXiv:astro-ph/0302239. Kistler, Haxton & Yüksel 2013, ApJ 778, 81, arXiv:1211.6770. Leahy & Williams 2017, AJ 153, 239,
arXiv:1701.05942 (checked for C9). Lovegrove & Woosley 2013, arXiv:1303.5055. Matzner & McKee 1999, ApJ 510, 379,
arXiv:astro-ph/9807046. Mazzali et al. 2007, Science 315, 825, arXiv:astro-ph/0702351. Ono et al. 2020, ApJ 888, 111,
arXiv:1912.02234. Sugerman et al. 2005, ApJS 159, 60, arXiv:astro-ph/0502378. Tang & Chevalier 2017, arXiv:1607.06391
(MNRAS 465, 3793 [R]). Truelove & McKee 1999, ApJS 120, 299 (journal PDF). Utrobin et al. 2019, A&A 624, A116,
arXiv:1812.11083. van der Swaluw et al. 2001, arXiv:astro-ph/0012440 (A&A 380, 309 [R]). Wongwathanarat, Müller &
Janka 2015, A&A 577, A48, arXiv:1409.5431.

Not reached: Chevalier 1982 (scanned pages only; its Table 1 was recomputed, §S4.1); Chevalier & Soker 1989; Chevalier &
Fransson 1992; Burrows et al. 1995 and Panagia 1991/2005 (quoted via Sugerman 2005 and search excerpts); Li, McCray &
Sunyaev 1993; Hester 2008; Tziamtzis et al. 2009, Sollerman et al. 2000 and Chugai 2001 (search excerpts of the
abstracts only); Kozma & Fransson 1998, Colgan et al. 1994, Chugai 1991, Larsson et al. 2019 (via Utrobin 2019); the
TM99 erratum (ApJS 128, 403): its correction is visible in Tang & Chevalier's Table 7 (0.762 in place of TM99 Table 4's
2.29 in the n < 3 reverse shock), and TM99 Table 5's explicit n = 0 forms used here agree with the corrected one to
0.2% [D].
