# Supernova Lab: Signal Models for Each Instrument View

Research notes for implementing per-instrument signals on a shared log-time axis, from core
collapse (t = 0) to about 1,000 years. Every formula and number has a source. Labels used:

- **[SOURCE]**: taken directly from the cited paper.
- **[DERIVED]**: computed or rearranged here from cited formulas. The working is shown, so it can be checked.
- **[APPROX]**: a stand-in where no clean published value was found. Treat it as a placeholder
  and show it to the user as uncertain.

Compiled 2026-11-11. Where an arXiv id is given, it is the preprint behind the journal reference.

---

## 0. Conventions, constants and shared helpers

### 0.1 Time origin

- The app's t = 0 is **core bounce**. Bounce follows the start of collapse within a few hundred ms
  (Janka 2017, *Handbook of Supernovae*, arXiv:1702.08713). That gap does not matter on a log axis
  above about 1 s. Neutrino and gravitational-wave (GW) times are always quoted "after bounce".
- Observed time = source time + D/c. Every messenger shares the same D/c, so the app can ignore it,
  except that neutrinos and GWs arrive before the light from shock breakout (Section 3.1).

### 0.2 Constants (cgs)

| Symbol | Value | Note |
|---|---|---|
| c | 2.99792458e10 cm/s | exact |
| G | 6.674e-8 cm^3 g^-1 s^-2 | CODATA |
| M_sun | 1.989e33 g | |
| R_sun | 6.957e10 cm | IAU 2015 B3 nominal |
| pc | 3.0857e18 cm | kpc = 3.0857e21 cm; Mpc = 3.0857e24 cm |
| day | 86400 s | yr = 3.156e7 s |
| MeV | 1.602177e-6 erg | |
| m_p, m_n, m_u | 1.6726e-24, 1.6749e-24, 1.6605e-24 g | |
| L_sun | 3.828e33 erg/s | IAU 2015 Resolution B3 (Mamajek et al. 2015, arXiv:1510.07674) |
| M_bol zero point | M_bol = -2.5 log10(L / 3.0128e35 erg/s) | IAU 2015 Resolution B2 (Mamajek et al. 2015, arXiv:1510.06262). Gives M_bol,sun = 4.74 |

### 0.3 Distance scaling (applies to every view)

- Photon or neutrino flux and fluence: F = L / (4 pi D^2).
- GW strain: h proportional to 1/D. GW signal-to-noise ratio (SNR) is proportional to 1/D.
- Distance modulus: mu = 5 log10(D / 10 pc). Apparent magnitude m = M + mu + A_band.
- The app's D range is 1 kpc to 100 Mpc. At 100 Mpc, cosmological effects are small
  (z about 0.023 for H0 of about 70 km/s/Mpc). Set the luminosity distance equal to D.
  K-corrections can be ignored at the precision of this app. [APPROX]

### 0.4 Derived quantities from the inputs, used by several views

- Characteristic ejecta velocity, uniform-density sphere: v_ej = sqrt(10 E / (3 M_ej)). This is the
  velocity scale used in Arnett-type models; see Arnett 1982, ApJ 253, 785, and Valenti et al. 2008,
  MNRAS 383, 1485 (arXiv:0710.5173). Example: E = 1e51 erg, M_ej = 3 M_sun gives
  v_ej = 7.5e8 cm/s. [DERIVED]
- Wind density parameter (Chevalier & Li 2000, ApJ 536, 195, arXiv:astro-ph/9908272; Chevalier &
  Fransson 2006, ApJ 651, 381, arXiv:astro-ph/0607196):
  rho_w(r) = A / r^2, A = Mdot / (4 pi v_w), A_* = A / (5e11 g/cm).
  A_* = 1 corresponds to Mdot = 1e-5 M_sun/yr and v_w = 1000 km/s, a typical Wolf-Rayet wind [SOURCE].
  A red supergiant wind with Mdot = 1e-6 M_sun/yr and v_w = 10 km/s gives A_* of about 10 [DERIVED].

---

## 1. Neutrinos from core collapse

### 1.1 Phases (time after bounce)

| Phase | Time | Main features | Source |
|---|---|---|---|
| Neutronization (shock-breakout) burst | starts about 2 ms after bounce; FWHM 5-7 ms; about 2e51 erg released within about 20 ms | nu_e only. Peak L_nue of about 3.3-4e53 erg/s. Mean nu_e energy peaks at 12-13 MeV. Nearly progenitor-independent | Kachelriess et al. 2005, PRD 71, 063003 (arXiv:astro-ph/0412082); Janka 2017 (arXiv:1702.08713) |
| Accretion | about 0.02 to 0.3-0.75 s | L_nue roughly equals L_anue, each a few times 1e52 erg/s. nu_x lower. Can carry SASI-driven quasi-periodic modulation | Janka 2017; Tamborra et al. 2013/2014 as cited there |
| Kelvin-Helmholtz cooling | about 0.5 s to tens of s | All six species within about 10% of each other. Sum of "several 1e52 erg/s", falling. Mean energies turn over near 1 s and fall, at 10-20 MeV for most of this phase | Janka 2017 |
| Late tail | up to 30-100+ s | Detectable in Super-K at 10 kpc for more than 30 s (1.2 M_sun PNS) to more than 100 s (2.05 M_sun PNS) | Suwa et al. 2019, ApJ 881, 139 (arXiv:1904.09996) |

**Total energy.** The neutron star binding energy is E_b of about 3.6e53 erg (M_NS/1.4 M_sun)^2
(R_NS/10 km)^-1 (Janka 2017, Eq. 1; Newtonian estimate) [SOURCE]. Lattimer & Prakash 2001
(ApJ 550, 426) give the better fit BE/(M c^2) of about 0.6 beta/(1 - 0.5 beta), with beta = G M/(R c^2).
For M = 1.4 M_sun and R = 12 km this gives about 2.8e53 erg [DERIVED].
About 99% of the energy leaves as neutrinos, equipartitioned roughly 1/6 per species
(Abdikamalov et al. 2020, arXiv:2010.04356).

### 1.2 Usable parameterization (recommended)

Use the two-component fit of Pagliaroli, Vissani, Costantini & Ianni 2009 (Astropart. Phys. 31, 163;
arXiv:0810.0466). It was fitted to the SN 1987A events, so it is tied to data rather than to one
simulation. Add a neutronization burst on top.

**(a) Cooling component (anti-nu_e; apply to each of the 6 species during cooling).**
Number spectrum emitted per unit time:

dN/dE dt = (pi c / (hc)^3) * 4 pi R_c^2 * E^2 / (1 + exp(E / T_c(t))), with T_c(t) = T_c exp(-t / (4 tau_c)).

This is a Fermi-Dirac spectrum with zero degeneracy. The luminosity and mean energy follow
[DERIVED, using the Fermi integral of E^3 = 7 pi^4 T^4 / 120]:

- L_c(t) = L_c0 exp(-t / tau_c), with L_c0 = 4 pi R_c^2 * sigma_nu * T_c^4 and sigma_nu = 4.50e35 erg MeV^-4 cm^-2 s^-1 per species.
  This is the same constant as Janka 2017, Eq. 30 (greyness phi = 1).
- Mean energy <E> = 3.151 T_c(t) (Janka 2017).

**(b) Accretion component (anti-nu_e; nu_e is about equal during accretion, per Janka 2017).**
Pagliaroli et al. model it as positron capture on N_n(t) neutrons in a thermal e+ bath at T_a(t).
Since sigma is proportional to E^2, the T_a dependence cancels in the luminosity. The result is
[DERIVED from their Eqs.]:

L_a(t) = L_a0 * exp(-(t / tau_a)^2) / (1 + t / 0.5 s).

T_a(t) = T_a + (0.6 T_c - T_a)(t / tau_a)^2 sets the mean energy.

**(c) Combine.** L_total(t) = L_a(t) + (1 - exp(-(t/tau_a)^2)) * L_c(t - tau_a). This is their Eq. for
the total flux, with the cooling phase starting at about tau_a.

**Best-fit parameters** (Pagliaroli et al. 2009, Table 1, model III.3) [SOURCE]:

| Parameter | Value (1 sigma) |
|---|---|
| R_c | 16 (+9, -5) km |
| T_c | 4.6 (+0.7, -0.6) MeV |
| tau_c | 4.7 (+1.7, -1.2) s |
| M_a (accreting mass) | 0.22 (+0.68, -0.15) M_sun |
| T_a | 2.4 (+0.6, -0.4) MeV |
| tau_a | 0.55 (+0.58, -0.17) s |
| Energy: cooling, accretion, total | 1.8e53, 4.8e52, 2.2e53 erg |

**Numbers computed from these values** [DERIVED; numerical integration done for this document]:

- L_c0 (anti-nu_e) = 6.6e51 erg/s. Cooling energy per species = L_c0 * tau_c = 3.1e52 erg.
  Times 6 species = 1.84e53 erg, which matches their 1.8e53.
- Mean anti-nu_e energy at the start of cooling is 3.151 * 4.62 = 14.6 MeV. It falls as exp(-t/18.6 s).
- Accretion-phase L_a0 (anti-nu_e) is about 1.1e53 erg/s. The time integral of the shape is 0.324 s,
  so anti-nu_e accretion energy is about 3.7e52 erg. The mean energy of anti-nu_e emitted early in
  accretion is about 12.3 MeV.
  This is about 1.5 times larger than their quoted total E_a once nu_e is added. Renormalize L_a0 if
  you want to match E_a exactly.

**Scaling to the app's inputs** [APPROX, an app design choice]:

- Scale all luminosities by E_nu,tot / 2.2e53 erg, where E_nu,tot is taken from the binding energy (Section 1.1).
- For a heavier proto-neutron star (more massive progenitor), Janka 2017 notes higher accretion
  luminosity and harder spectra in high-compactness progenitors.

**(d) Neutronization burst (nu_e only)** [APPROX shape; anchors from SOURCE]:

L_burst(t) = L_pk exp(-(t - t_pk)^2 / (2 s^2)), with L_pk = 3.5e53 erg/s, t_pk of about 5 ms, and
s = FWHM/2.355 of about 2.5 ms.

Check: L_pk * s * sqrt(2 pi) = 2.2e51 erg, which matches the "about 2e51 erg" in Janka 2017.
Mean nu_e energy is 12-13 MeV at the peak.

**(e) Flavour hierarchy and spectral shape.**

- Accretion: <E_nue> < <E_nux> < <E_anue> (transient inversion). Cooling: nu_x slightly softer than anti-nu_e (Janka 2017).
- Use the Keil-Raffelt-Janka spectrum f(E) proportional to (E/<E>)^alpha exp(-(alpha+1)E/<E>)
  (Keil, Raffelt & Janka 2003, ApJ 590, 971).
  - alpha = 2.3 matches a zero-degeneracy Fermi-Dirac spectrum.
  - alpha is 2-3 for all species after about 200 ms.
  - alpha reaches up to 6-7 for nu_e around the burst (Janka 2017; Tamborra et al. 2012, PRD 86, 125031).
- Typical mean energies [APPROX, from the ranges in Janka 2017]: <E_nue> of 10-13 MeV,
  <E_anue> of 12-16 MeV, <E_nux> of 12-16 MeV.
- Flavor conversion is not settled (Janka 2017). For anti-nu_e at Earth, a common MSW-only choice is
  P = cos^2(theta12) cos^2(theta13), about 0.7 (normal ordering). Then
  F(anti-nu_e) = P F0(anti-nu_e) + (1 - P) F0(anti-nu_x) (Pagliaroli et al. 2009).

**Alternative data-driven option:** the Nakazato et al. 2013 database (ApJS 205, 2; arXiv:1210.6841)
covers 13-50 M_sun progenitors at Z = 0.02 and 0.004 through 20 s. It is distributed as ASCII spectra
versus time (Supernova Neutrino Database, http://asphwww.ph.noda.tus.ac.jp/snn/). IceCube publishes
fitted light curves, mean energies and pinch parameters derived from it
(github.com/IceCubeOpenSource/snfluxes-public). Licenses were not checked; check them before bundling.

### 1.3 Black-hole formation (failed or fallback supernova)

- The neutrino signal **stops abruptly** when the black hole horizon forms. Before that, accretion
  makes L and the mean energy rise (Sumiyoshi et al. 2006, PRL 97, 091101; Sumiyoshi et al. 2008,
  ApJ 688, 1176, arXiv:0809.5129; Fischer et al. 2009, A&A 499, 1; O'Connor & Ott 2011, ApJ 730, 70,
  arXiv:1010.5550).
- The dynamical collapse to a black hole takes 1 ms or less of coordinate time (O'Connor & Ott 2011).
  Model the cutoff as a step, or as an exponential with a time constant of about 1 ms or less [APPROX].
- **Time of black hole formation** (O'Connor & Ott 2011) [SOURCE]:
  - Compactness: xi_2.5 = (2.5 M_sun / M_sun) / (R(M_bary = 2.5 M_sun) / 1000 km), evaluated at bounce.
  - xi_2.5 above about 0.45 makes black hole formation likely (LS180/LS220 equations of state).
  - The simulated range is t_BH = 0.23 to 3.4 s after bounce.
  - Scaling: t_BH proportional to xi_2.5^-1.5, with free-fall estimate t_ff = 0.241 xi_2.5^-1.5 s
    (good only to about a factor of 2).
  - In the app, t_BH is a user input. Use it directly.
- **Model compilation** (Suwa et al. 2025, Open J. Astrophys. 8; arXiv:2504.19510, Table 1) [SOURCE]:

| Model (progenitor, equation of state) | t_BH (s) | E(anti-nu_e) (1e53 erg) | <E anti-nu_e> (MeV) |
|---|---|---|---|
| Sumiyoshi 2007, 40 M_sun, LS180 | 0.56 | 0.467 | 19.5 |
| Sumiyoshi 2007, 40 M_sun, Shen-TM1 | 1.34 | 1.35 | 23.2 |
| Fischer 2009, 40 M_sun, LS180 | 0.435 | 0.376 | 14.6 |
| Nakazato 2010, 40 M_sun, LS220 | 0.780 | 0.627 | 20.1 |
| Hüdepohl 2014, 40 M_sun (WHW02), LS220 | 1.93 | 0.796 | 18.8 |
| Nakazato 2021, 30 M_sun, Togashi | 0.533 | 0.533 | 20.4 |
| Choi 2025, 23 M_sun, SFHo | 6.23 | 0.736 | 14.7 |
| Choi 2025, 100 M_sun, SFHo | 0.44 | 0.462 | 15.1 |

  The full table (28 models) gives t_BH = 0.34-6.2 s, E(anti-nu_e) = 0.27-1.53e53 erg and
  <E> = 14.6-23.2 MeV. Their simple Super-K estimate is N of about
  3.48 (<E>/15 MeV)(E_anue/1e53 erg) events at 770 kpc (M31). It assumes no cuts, no oscillations and
  a thermal spectrum. Scale by (770 kpc / D)^2 [SOURCE].
- **Recommended app model** [APPROX]:
  - For t < t_BH, use the accretion component (1.2b) with L_a held roughly flat or slowly rising, and
    mean energy rising linearly from about 12 MeV to about 20 MeV (anti-nu_e) at t_BH.
  - At t_BH, set every flavor to zero.
  - If a black hole forms by fallback after a successful explosion (t_BH larger than the cooling start),
    truncate the cooling curve at t_BH instead.

### 1.4 Detection: channels, counts and per-event energies

**Inverse beta decay (IBD), anti-nu_e + p -> e+ + n.** Threshold E_nu = 1.806 MeV.
Positron energy E_e is about E_nu - 1.293 MeV.

- Zeroth-order cross-section: sigma is about 9.52e-44 (E_e p_e / MeV^2) cm^2 (Vogel & Beacom 1999,
  PRD 60, 053003).
- A more accurate form is in Strumia & Vissani 2003, Phys. Lett. B 564, 42.
- Pagliaroli et al. 2009 use 4.8e-44 E_nu^2 / (1 + E_nu/260 MeV) cm^2, valid for 5-40 MeV.

**Generic event-count formula** [DERIVED]:

N_IBD = N_p * (E_anue / <E>) / (4 pi D^2) * <sigma>.

- Free protons per kiloton: N_p = 6.69e31 per kt in water [DERIVED: 2 H per H2O]. About 7e31 per kt
  for typical liquid scintillator [APPROX].
- Spectrum-averaged <sigma> for a Fermi-Dirac spectrum [DERIVED]: 1.5e-41 cm^2 (<E> = 12 MeV),
  2.1e-41 (14 MeV), 2.4e-41 (15 MeV), 2.8e-41 (16 MeV).
- Worked check [DERIVED]: Super-K at 32 kt, D = 10 kpc, E_anue = 5e52 erg, <E> = 12-16 MeV
  gives N = 7,000-9,800. The published model-based numbers below are about 0.5-1 times this.
  They include thresholds, oscillations, and models with lower anti-nu_e energy.

**Mean detected positron energy** (Suwa et al. 2019, Table 2) [SOURCE]:

- With no threshold, <E_e+> = 5.07 kT, about 1.61 <E_nu>.
- With a 5 MeV threshold and kT = 3-4 MeV, <E_e+> is about 12.7-17.8 MeV.
- Individual events range from a few MeV to about 40 MeV. See the SN 1987A table in Section 1.5.

**Expected counts at 10 kpc**:

| Detector | Type, mass | Main channel | Events at 10 kpc | Source |
|---|---|---|---|---|
| Super-Kamiokande | water Cherenkov, 32 kt | IBD (about 90%), plus nu-e elastic scattering (about 120-250), plus 16O (about 80-250) | 4,000-7,800 total (SNOwGLoBES, 11.2-27 M_sun, both mass orderings). IBD alone: 2,900-7,300 in 32.48 kt over 20 s (Nakazato models) | Al Kharusi et al. 2021 (SNEWS 2.0), NJP 23, 031201 (arXiv:2011.00035), Table 4 and Sec. 6.1.1; Suwa et al. 2019, Table 1 |
| Hyper-Kamiokande | water, 220 kt (217 kt fiducial for supernovae) | IBD | 28,000-53,000 (SNEWS 2.0). The Design Report gives 54,000-90,000 total including 50,000-75,000 IBD (arXiv:1805.04163). "Tens of events" from M31 (Abdikamalov et al. 2020) | as cited |
| IceCube | 1 km^3 ice, 5,160 optical modules | collective rise in optical-module noise from IBD positrons; no individual events | 3.2e5-8.2e5 photon "hits" (SNEWS 2.0, Table 4); 1e5-1e6 photoelectrons (Sec. 6.1.3). About 20 sigma at 30 kpc, about 6 sigma at the LMC (50 kpc). 2 ms time binning | SNEWS 2.0; Abbasi et al. 2011, A&A 535, A109 (arXiv:1108.0171) |
| JUNO | liquid scintillator, 20 kt | IBD (5,000), nu-p elastic scattering (2,000), nu-e elastic scattering (300+), 12C charged and neutral current | about 5,000 IBD for a "typical" supernova; 3,800-7,200 total in SNEWS 2.0 Table 4 | SNEWS 2.0 Sec. 6 |
| DUNE | liquid argon TPC, 40 kt (4 x 10 kt fiducial) | nu_e + 40Ar -> e- + 40K* (sensitive to the nu_e burst) | about 3,000 nu_e charged-current events; 2,700-5,800 in Table 4. Threshold 5-10 MeV, about 20% energy resolution | SNEWS 2.0 Sec. 6 |

**Distance scaling and horizons.**

- Counts scale as (10 kpc / D)^2.
- Super-K detects with 100% efficiency to about 200 kpc, with 3% misidentification (Abdikamalov
  et al. 2020, citing Pagliaroli/Vissani work).
- Super-K's distant-supernova trigger needs 2 events at 17 MeV or above within 20 s. M31
  (750-770 kpc) is then detected with about 8% probability, or about 35% with a GW coincidence
  (Abdikamalov et al. 2020) [SOURCE].
- Hyper-K can tell supernova models apart out to 60-100 kpc (SNEWS 2.0).
- At 1 Mpc and beyond, expect 1 event or fewer even in Hyper-K. Display "no detection" there [DERIVED].

**Pointing from neutrinos** (SNEWS 2.0): Super-K 3-5 deg (elastic scattering), Hyper-K 1-1.5 deg,
DUNE "several deg", JUNO about 10 deg, IceCube only by triangulation (10-100 deg).

### 1.5 SN 1987A (LMC, 51.2 +/- 3.1 kpc, Panagia et al. 1991 as cited in Suwa et al. 2019)

Event table as compiled by Vissani 2015 (J. Phys. G 42, 013001; arXiv:1409.4710). Originals:
Hirata et al. 1987, PRL 58, 1490; Bionta et al. 1987, PRL 58, 1494; Alekseev et al. 1987,
JETP Lett. 45, 589. Times are relative to each detector's first event. Absolute clocks differ:
the Kamiokande clock had about a 1 minute uncertainty.

| Detector (threshold) | Events used | Time span | Energies (MeV) |
|---|---|---|---|
| Kamiokande-II (7.5 MeV traditional) | 11 above threshold (K1-K12 minus K6 at 6.3 MeV). K13-K16 (17.6-23.8 s, 4.6-6.5 MeV) are usually treated as background | 0 to 12.439 s | 20.0, 13.5, 7.5, 9.2, 12.8, (6.3), 35.4, 21.0, 19.8, 8.6, 13.0, 8.9 |
| IMB (about 15 MeV efficiency scale) | 8 | 0 to 5.582 s | 38, 37, 28, 39, 36, 36, 19, 22 |
| Baksan (10 MeV) | 5 | 0 to 9.099 s | 12.0, 17.9, 23.5, 17.5, 20.3 |

Exact times in ms. Kamiokande-II: 0, 107, 303, 324, 507, 686, 1541, 1728, 1915, 9219, 10433, 12439.
IMB: 0, 412, 650, 1141, 1562, 2684, 5010, 5582. Baksan: 0, 435, 1710, 7687, 9099.
Energy errors are about 10-25%; see Vissani 2015.

Inferred: about 2.2e53 erg total in neutrinos (Pagliaroli et al. 2009). Expected backgrounds in a
30 s window: about 5.6 events (Kamiokande-II) and about 1 (Baksan).

### 1.6 SNEWS early warning

- SNEWS has run as an automated coincidence network since 2005. SNEWS 2.0 adds lower latency,
  triangulation and a pre-supernova alert (Al Kharusi et al. 2021).
- Super-K's own trigger [SOURCE]:
  - Selection: 22.5 kt fiducial volume, visible energy above 7 MeV, 20 s window.
  - "Golden" alert at 60 or more events, plus a volume-like vertex distribution. This gives 100%
    efficiency at the LMC and needs no other detector.
  - "Normal" alert at 25-59 events. This gives 100% efficiency at the SMC.
- IceCube sends its alert to SNEWS with about 5 minutes latency.
- A pre-supernova alert from silicon-burning neutrinos, hours before collapse, is possible only within
  about 600 pc (KamLAND; Abdikamalov et al. 2020) [SOURCE].
- App timeline: show the SNEWS alert at about t = +1 min (network latency) [APPROX]. Light from shock
  breakout follows minutes to days later (Section 3.1).

---

## 2. Gravitational waves from core collapse

### 2.1 Non-rotating or slowly rotating (neutrino-driven): the common case

- **Mechanism:** convection and the standing accretion-shock instability (SASI) excite oscillations
  of the proto-neutron star. Most power comes from the quadrupolar g-mode, which becomes the f-mode
  after about 0.4 s (Abdikamalov, Pagliaroli & Radice 2020, arXiv:2010.04356; Morozova et al. 2018,
  ApJ 861, 10, arXiv:1801.01914; Radice et al. 2019, ApJL 876, L9, arXiv:1812.07703).
- **Time structure** (Abdikamalov et al. 2020) [SOURCE]:
  - Quiet for about 10 ms after bounce (no rotation).
  - Prompt-convection signal at about 10-50 ms, a track near 100 Hz.
  - Quiescent for about 100 ms.
  - Then a stochastic rising-frequency signal for several hundred ms to more than 1 s.
  - Explosion asymmetry adds a low-frequency "memory" offset, with hD up to about 3 cm.
- **Frequency track:**
  - Example 3D model (25 M_sun): 400 Hz at 150 ms rising to 1,100 Hz at 600 ms (Abdikamalov et al.
    2020). Late times reach up to about 2 kHz (same review).
  - SASI-related power: about 100-200 Hz (as summarized in Morozova et al. 2018), weak compared with
    the main track.
  - Physical estimate (Müller, Janka & Marek 2013, ApJ 766, 43, arXiv:1210.6984, Eq. 17) [SOURCE]:
    f_peak = (1/(2 pi)) (G M / R^2) sqrt(1.1 m_n / <E_anue>) (1 - G M/(R c^2))^2.
    Here M and R are the proto-neutron star mass and radius. It somewhat overestimates f at late times.
  - Morozova et al. 2018 fit f = A t^2 + B t + C over 0 to 1.5 s, good to about 30%. The coefficients
    are printed only inside their Fig. 14 and were not transcribed here.
- **Energy and amplitude:**

| Quantity | Value | Source |
|---|---|---|
| E_GW, 3D models (9-60 M_sun) | 7.2e43 erg (9 M_sun) to about 2e45 erg (13 M_sun, 1e-9 M_sun c^2) | Abdikamalov et al. 2020 (Radice et al. 2019 models) |
| E_GW, 2D models | 3.2e42 to 2.0e46 erg | Müller et al. 2013, Table 2 |
| E_GW crude upper bound | about 1e47 erg | Abdikamalov et al. 2020 |
| Strain amplitude hD, 2D | maximum A20 of 13-253 cm, i.e. hD of about 4-70 cm (h D = 0.27 A20 for an equatorial observer) | Müller et al. 2013 [DERIVED conversion] |
| Strain at 10 kpc | h of about 1e-23 to 2e-21 for hD of 0.3-70 cm. 3D models sit at the low end. Note 2D waveforms are about 4 times louder than 3D (Srivastava et al. 2019) | [DERIVED: h = hD / D, with D = 3.086e22 cm] |
| Duration | about 0.3 to more than 1 s; the 9 M_sun model saturates by about 0.3 s | Radice et al. 2019 |

### 2.2 Rapidly rotating collapse: the bounce signal

- **Mechanism:** centrifugal flattening makes the proto-neutron star oblate at bounce. It rings for
  10-20 ms. The signal is deterministic (not stochastic) and seen best from the equatorial plane;
  there is no signal along the spin axis (Dimmelmeier et al. 2008, PRD 78, 064056; Richers et al.
  2017, PRD 95, 063019; Abdikamalov et al. 2020).
- **Peak frequency:** f_peak is about (1/(2 pi)) sqrt(G rho_c), roughly 750 Hz for a typical proto-neutron
  star (Abdikamalov et al. 2020, Eq. 21). Example models give 800, 750 and 372 Hz for slow, rapid and
  extreme rotation [SOURCE].
- **Amplitude:** |h_max| D grows linearly with the rotation parameter T/|W| (inner core at bounce) up
  to T/|W| of about 0.06-0.09. It saturates at a few hundred cm and falls for T/|W| above about 0.17
  (Abdikamalov et al. 2020, Fig. 9, axis up to about 6e2 cm). Scaling (their Eq. 20):
  hD ~ (G M)^2 / (c^4 R) * (T/|W|).
  - Worked example [DERIVED/APPROX]: M = 0.6 M_sun (inner core), R = 15 km and T/|W| = 0.05 give
    hD of about 260 cm. At 10 kpc that is h of about 8e-21.
- **Non-axisymmetric (bar-mode) instability** (Abdikamalov et al. 2020, Eq. 23) [SOURCE]:
  h ~ 6e-21 (epsilon/0.1)(f/500 Hz)^2 (D/10 kpc)^-1 (M/M_sun)(R/12 km)^2.
  The SNR grows as N^(1/2) over N cycles. Third-generation detectors could see this out to about 1 Mpc.
- **App trigger:** the app has no pre-collapse rotation input. Use the "rapid rotation" waveform when
  jet/GRB = true, or when the NS spin period is 5 ms or less. This is an app heuristic, not a
  published rule [APPROX].

### 2.3 Detectability

| Detector | Neutrino-driven (non-rotating) | Rapidly rotating | Source |
|---|---|---|---|
| Advanced LIGO/Virgo (design) | about 5.5 kpc (3-detector network). Optimal SNR at 10 kpc of 1.5 (9 M_sun) to 11.5 (19 M_sun). Average optimal distance for 3D waveforms about 8 kpc | about 50 kpc (LMC). Extreme models to M31 (0.77 Mpc) to M82 (3.5 Mpc) | Gossan et al. 2016, PRD 93, 042002 (arXiv:1511.02836); Radice et al. 2019; Srivastava et al. 2019, PRD 100, 043026 (arXiv:1906.00084) |
| LIGO-Virgo-KAGRA O4/O5 | average about 10 kpc | over 100 kpc | Szczepańczyk et al. 2021, PRD 104, 102002 (arXiv:2104.06462) |
| Einstein Telescope (ET-D) | optimal SNR at 10 kpc of 20-110, so a range of about 25-140 kpc for SNR 8 [DERIVED] | up to about 1 Mpc for bar-mode | Radice et al. 2019; Abdikamalov et al. 2020 |
| Cosmic Explorer, supernova-optimized | "only up to about a hundred kpc", i.e. Galactic events, about 2 per century | | Srivastava et al. 2019 |

Current all-sky searches reach 50% efficiency at 10 kpc for h_rss of (1-10)e-22 Hz^-1/2, depending on
frequency. In the 100-200 Hz band this corresponds to E_GW of about 1e-9 M_sun c^2 (Abdikamalov et al.
2020) [SOURCE].

### 2.4 Waveform and spectrogram recipe

1. **Energy normalization.** Choose E_GW (default 1e45 erg; range 1e43-1e46 for non-rotating cases).
   Use the burst relation for a quasi-monochromatic signal at f0, assuming isotropic emission
   (Sutton 2013, arXiv:1304.0210):
   E_GW = (pi^2 c^3 / G) D^2 f0^2 h_rss^2, so h_rss = sqrt(G E_GW / (pi^2 c^3)) / (D f0).
   - Worked example [DERIVED]: E_GW = 2e45 erg, f0 = 800 Hz, D = 10 kpc gives h_rss of about 2.9e-23 Hz^-1/2.
2. **SNR** (single detector, optimal orientation, narrow-band) [DERIVED from the standard matched
   filter]: rho is about h_rss sqrt(2 / S_n(f0)), where S_n is the one-sided noise power spectral density.
   - Representative amplitude spectral densities [APPROX; use the official curves]:
     - Advanced LIGO design: about 4e-24 Hz^-1/2 near 200-300 Hz, rising roughly as f to about
       1e-23 at 1 kHz. Official curve: LIGO-T1800044.
     - ET-D and CE: about 10 times lower.
   - The example above with aLIGO at 800 Hz gives rho of about 4, consistent with Radice et al. 2019.
3. **Time-frequency track (non-rotating)** [APPROX; piecewise-linear through the anchors above]:
   - t = 10-50 ms: noise-like packet near 100 Hz (prompt convection).
   - t = 50-150 ms: low amplitude.
   - From 150 ms: f(t) = 400 Hz + 1,556 Hz/s * (t - 0.15 s), capped at 1.1-2 kHz.
   - If SASI is active: add a weaker band at 100-200 Hz (for example 0.15-0.4 s).
   - Draw the strain as band-limited Gaussian noise. Use a bandwidth of about 20-30% of f(t), and
     scale it so that the integral of h^2 dt equals h_rss^2.
   - For a black hole case, end the signal at t_BH with a short ring-down of a few kHz
     (Abdikamalov et al. 2020 describe about 1 kHz then black hole quasi-normal modes at a few kHz).
4. **Rotating bounce.** One damped sinusoid at f_peak of about 750 Hz (range 400-1,000 Hz), damping
   time about 3-5 ms (ring-down lasts 10-20 ms) [APPROX on the damping], with the peak hD from 2.2.
   A negative spike at bounce followed by ringing reproduces the Dimmelmeier et al. 2008 morphology.

---

## 3. Shock breakout and shock cooling

### 3.1 When breakout happens (delay after core bounce)

Kistler, Haxton & Yüksel 2013 (ApJ 778, 81; arXiv:1211.6770), Eq. 5. Polytropes, accurate to 10-20%
compared with full progenitor models [SOURCE]:

- n = 3/2 (red supergiant, convective envelope):
  dt = 7,226 s (E/1e51 erg)^-1/2 (M_ej/10 M_sun)^1/2 (R/50 R_sun)
       [1 - 0.738 x^0.80 + 0.467 x^1.20], with x = M_NS/M_ej.
- n = 3 (blue supergiant, Wolf-Rayet):
  dt = 6,851 s (...)(same scalings)[1 - 0.407 x^0.81 + 0.285 x^1.12].

Worked examples [DERIVED; M_NS = 1.4 M_sun, E = 1e51 erg]:

| Progenitor | R | M_ej | Breakout delay |
|---|---|---|---|
| Red supergiant (n = 3/2) | 500 R_sun | 10 M_sun | 6.4e4 s, about 18 h. 3D models give about 1 day with up to 1 day of direction dependence (arXiv:2511.11796) |
| Blue supergiant (n = 3), SN 1987A-like | 50 R_sun | 14 M_sun | 7.8e3 s, about 2.2 h |
| Wolf-Rayet (n = 3) | 3 R_sun | 3 M_sun | about 200 s, about 3.4 min |

### 3.2 Breakout flash: duration, temperature, energy

**Duration.** The observed duration is the larger of the light-travel time R/c and the diffusion time
t_bo (Waxman & Katz 2017, *Handbook of Supernovae*, arXiv:1607.01293; Nakar & Sari 2010, ApJ 725,
904, arXiv:1004.2496):

| Progenitor | R/c | t_bo (intrinsic) | Observed T_bo | Band | Source |
|---|---|---|---|---|---|
| Red supergiant, 500 R_sun, 15 M_sun | about 20 min | about 5 min | about 25 eV (thermal breakout) | extreme UV / soft X-ray | Nakar & Sari 2010 |
| Blue supergiant, 50-70 R_sun | about 2 min | about 10 s | about 80 eV thermal, up to about 700 eV non-thermal | soft X-ray | Nakar & Sari 2010 |
| Wolf-Rayet, 5 R_sun | about 10 s | about 0.1 s | about 2 keV (non-thermal; Comptonized) | X-ray | Nakar & Sari 2010 |

**Scaling formulas** (Nakar & Sari 2010). E51 = E/1e51 erg; M15 = M/15 M_sun; R500 = R/500 R_sun;
R5 = R/5 R_sun [SOURCE]:

- Red supergiant: T_bo of about 25 eV M15^-0.3 R500^-0.65 E51^0.5.
- Wolf-Rayet: T_bo of about 2 keV M15^-1.7 R5^-1.5 E51^1.8 (strongly non-thermal regime).
- Breakout energy, red supergiants and blue supergiants: about 3e46 erg in the soft X-ray flash.
  [CONTROLLER NOTE 2026-11-11: inconsistent with the Waxman & Katz E_BO formula below, which gives about 2e48 erg
  bolometric for a 500 R_sun, 15 M_sun, 1e51 erg red supergiant (L_peak about 1.6e45 erg/s over R/c). The 3e46 figure is
  at most the soft-X-ray-band part of a ~25-40 eV flash. The app follows the formula.]

**Energy and velocity** (Waxman & Katz 2017) [SOURCE]:

- E_BO is about 2.2e47 R13^2 v_bo,9 kappa_0.34^-1 erg (R13 = R/1e13 cm; v_bo,9 = v_bo/1e9 cm/s).
- t_bo = 90 s kappa_0.34^-1 rho_-9^-1 v_bo,9^-2.
- Breakout velocity (v_* = (E/M)^1/2, v_*,8.5 = v_*/10^8.5 cm/s):
  - Blue supergiant (n = 3): v_bo/v_* = 13 M10^0.16 v_*,8.5^0.16 R12^-0.32 kappa_0.34^0.16.
  - Red supergiant (n = 3/2): v_bo/v_* = 4.5 M10^0.13 v_*,8.5^0.13 R13^-0.26 kappa_0.34^0.13.
- Breakout density:
  - Red supergiant: rho_bo = 2.2e-9 M10^0.32 v_*,8.5^-0.68 R13^-1.64 g/cm^3.
  - Blue supergiant: rho_bo = 8e-9 M10^0.13 v_*,8.5^-0.87 R12^-1.26 g/cm^3.
- Peak photon energy: log10(h nu_peak / eV) = 1.4 + v_bo,9^(1/2) + (0.25 - 0.05 v_bo,9^(1/2)) log10(rho_bo,-9).
  About half of E_BO is radiated per log-frequency around the peak.

**Peak luminosity for the app** [DERIVED]: L_peak is about E_BO / max(R/c, t_bo).
Example: red supergiant E_BO of about 3e46 erg over about 1,200 s gives about 2.5e43 erg/s. [CONTROLLER NOTE: with the
formula's E_BO of about 2e48 erg this example becomes about 1.6e45 erg/s; see the note above.]
Nakar & Sari's planar-phase luminosity at 1 h is about 1e44 erg/s for a red supergiant, so allow
1e43-1e45 erg/s for red supergiants. [APPROX range]

**Observed example: SN 2008D / XRO 080109** (Type Ib, NGC 2770, 27 Mpc; Soderberg et al. 2008,
Nature 453, 469, arXiv:0802.1712) [SOURCE]:

- Swift XRT, 0.3-10 keV. Peak L_X = 6.1e43 erg/s; peak flux 6.9e-10 erg/cm^2/s; E_X of about 2e46 erg.
- Light curve fit as fast-rise exponential-decay: peak 63 +/- 7 s after the start of observation,
  e-folding 129 +/- 6 s. Total duration about 5 minutes.
- No gamma-rays: fluence below 8e-8 erg/cm^2 (BAT 15-150 keV).
- Interpreted as breakout in a dense Wolf-Rayet wind: R_sbo of about 7e11 cm (stellar radius about
  1e11 cm) and Mdot of about 1e-5 M_sun/yr.
- Later X-rays: about 2e40 erg/s at 0.2 d, and 1e39 erg/s at about 10 d (inverse Compton).
- Ejecta: E_K of about (2-4)e51 erg, M_ej of about 3-5 M_sun, M_Ni of about 0.05-0.1 M_sun.

### 3.3 Shock cooling (hours to days)

**Rabinak & Waxman 2011** (ApJ 728, 63), as summarized in Waxman & Katz 2017, Eqs. 70-71.
Constant opacity, hydrogen envelope; t5 = t/1e5 s; R*,13 = R/1e13 cm; M in M_sun; f_rho of order 1
[SOURCE]:

- n = 3/2: L = 8.5e42 E51^0.92 R*,13 / (f_rho^0.27 M^0.84 kappa_0.34^0.92) t5^-0.16 erg/s.
- n = 3/2: T_ph = 1.6 eV f_rho^-0.037 E51^0.027 R*,13^(1/4) / (M^0.054 kappa_0.34^0.28) t5^-0.45.
- n = 3: L = 9.9e42 E51^0.85 R*,13 / (f_rho^0.16 M^0.69 kappa_0.34^0.85) t5^-0.31 erg/s.
- n = 3: T_ph = 1.6 eV (...) t5^-0.47.
- Colour temperature T_col is about 1.2 T_ph for t up to about 1 day.
- Helium envelope: L = 3.3e42 E51^0.84 R*,12^0.85 / (f_rho^0.15 M^0.67) t5^-0.03 erg/s and
  T_ph = 1.33 eV R*,12^0.20 t5^-0.38 (valid while T_ph is above 1.07 eV).
- Validity: 1.2 M^0.4 E51^-0.5 R*,13^1.3 h < t < 1.2 kappa_0.34^0.5 M E51^-0.5 d.

**Sapir & Waxman 2017** (ApJ 838, 130; arXiv:1607.03700). Extends to late times; v_s*,8.5 = v_s*/10^8.5
cm/s, with v_s* about 1.05 f_rho^-beta sqrt(E/M) [SOURCE]:

- L_RW = 2.0 [2.1] e42 (v_s*,8.5 t_d^2 / (f_rho M kappa_0.34))^-eps2 v_s*,8.5^2 R13 / kappa_0.34 erg/s,
  with eps2 = 0.086 [0.175] for n = 3/2 [3].
- T_ph = 1.61 [1.69] (v_s*,8.5^2 t_d^2 / (f_rho M kappa_0.34))^eps1 R13^(1/4) / (kappa_0.34^(1/4) t_d^(1/2)) eV,
  with eps1 = 0.027 [0.016].
- Late suppression: L/L_RW = A exp(-(a t / t_tr)^alpha), with A = 0.94 [0.79], a = 1.67 [4.57],
  alpha = 0.8 [0.73].
- t_tr = 19.5 d (kappa_0.34 M_env/M_sun / v_s*,8.5)^(1/2). Alternatively t_tr = 13 d (M_env/M_sun)^(3/4)
  (M/M_env)^(1/4) E51^(-1/4).
- Valid while T is above 0.7 eV, i.e. t < 7.4 (R13/kappa_0.34)^0.55 d.
- Use M_env = H envelope mass for Type II progenitors and the He envelope mass for Ib.

**Bands.** Cooling emission peaks in the far-UV in the first day, then moves into the optical.
Instruments:
- ULTRASAT (230-290 nm): 22.5 AB mag at 5 sigma in 900 s for a 20,000 K blackbody; 204 deg^2
  field of view (Shvartzvald et al. 2024, ApJ 964, 74; arXiv:2304.14482).
- Swift UVOT.
For breakout X-rays: Einstein Probe WXT, about (2-3)e-11 erg/cm^2/s in 1,000 s over 0.5-4 keV, field of
view of about 3,600 deg^2 (EP calibration papers, e.g. arXiv:2505.18939); Swift XRT.

---

## 4. Optical light curves

### 4.1 Radioactive heating: 56Ni -> 56Co -> 56Fe

| Quantity | Value | Source |
|---|---|---|
| 56Ni half-life, mean life | 6.075 d; tau_Ni = 8.76-8.8 d | Junde et al. 2011, Nucl. Data Sheets 112, 1513 (ENSDF); Arnett, Fryer & Matheson 2017 (ApJ 846, 33; arXiv:1611.08746) |
| 56Co half-life, mean life | 77.236 d; tau_Co = 111.3-111.4 d | same |
| eps_Ni (energy per gram of Ni) | 3.9e10 erg/g/s (Nadyozhin 1994); 3.9805e10 (Arnett et al. 2017) | Nadyozhin 1994, ApJS 92, 527 |
| eps_Co (per gram of Co) | 6.78e9 erg/g/s (Nadyozhin 1994). Split: gamma 6.4552e9, positron kinetic energy 2.1458e8 erg/g/s (Arnett et al. 2017) | as cited |

**Total decay power, full trapping**, as used for Arnett's rule (Nadyozhin 1994; written this way in
Stritzinger & Leibundgut 2005, A&A 431, 423):

L_decay(t) = (M_Ni / M_sun) [6.45e43 exp(-t / 8.8 d) + 1.45e43 exp(-t / 111.3 d)] erg/s.

The 1.45e43 equals eps_Co * M_sun * tau_Co/(tau_Co - tau_Ni) [DERIVED]: the Bateman factor is built in.

**Gamma-ray trapping** (Clocchiatti & Wheeler 1997, ApJ 491, 375; Wheeler, Johnson & Clocchiatti
2015, MNRAS 450, 1295, arXiv:1411.5975):

- Deposited fraction of gamma-ray energy: D(t) = 1 - exp(-(T0/t)^2). This goes to (T0/t)^2 at late
  times, the t0^2/t^2 limit stated in the literature.
- T0 = sqrt(C kappa_gamma M_ej^2 / E_K), with C = 0.05 for a power-law density profile [SOURCE].
- kappa_gamma is about 0.025-0.03 cm^2/g (effective gray value; Swartz, Sutherland & Harkness 1995,
  ApJ 446, 766) [APPROX range].
- The tail's log slope is steepest at about 1.07 T0.

Deposited power:

L_dep(t) = M_Ni [ eps_Ni e^(-t/tau_Ni) D(t) + eps_Co,gamma * Co(t) D(t) + eps_Co,e+ * Co(t) ],
with Co(t) = (tau_Co / (tau_Co - tau_Ni)) (e^(-t/tau_Co) - e^(-t/tau_Ni)).

Positrons are taken as fully trapped, which holds for the first year or two.

T0 worked examples [DERIVED; C = 0.05, kappa_gamma = 0.03]:

| Case | M_ej | E | T0 | Note |
|---|---|---|---|---|
| Type Ia | 1.4 M_sun | 1.3e51 erg | about 35 d | Literature value for Ia: about 30-45 d (e.g. Wygoda, Elbaz & Katz 2019, MNRAS 484, 3941) |
| Type Ib/c | 3 M_sun | 1e51 erg | about 85 d | |
| Type II-P | 10 M_sun | 1e51 erg | about 280 d | Near-full trapping through the plateau |

### 4.2 Arnett model (Types Ia, Ib, Ic, Ic-BL; also the rise of IIb)

**Diffusion time** (Arnett 1982; Valenti et al. 2008; Inserra et al. 2013, ApJ 770, 128,
arXiv:1304.3320, Eq. D2):

tau_m = (2 kappa M_ej / (beta c v))^(1/2), with v = sqrt(10 E/(3 M_ej)) and beta = 13.8.
Equivalently tau_m = 1.05 (beta c)^-1/2 kappa^1/2 M_ej^3/4 E^-1/4.

Example [DERIVED]: kappa = 0.1, M_ej = 3 M_sun, E = 1e51 erg gives tau_m of about 23 d.

**Luminosity** (Arnett 1982; Valenti et al. 2008, Eq. A1). With x = t/tau_m:

L(t) = M_Ni e^(-x^2) [ (eps_Ni - eps_Co) Int_0^x 2z e^(z^2 - 2zy) dz + eps_Co Int_0^x 2z e^(z^2 - 2zy + 2zs) dz ],

where y = tau_m/(2 tau_Ni) and s = tau_m (tau_Co - tau_Ni)/(2 tau_Co tau_Ni).
Multiply the late-time part by the trapping factor in 4.1, or switch to L_dep after the peak.
The same convolution kernel works for any heating source P(t'):
L(t) = e^(-x^2) Int_0^x P(t') 2 (t'/tau_m) e^((t'/tau_m)^2) dt'/tau_m (Inserra et al. 2013, Eq. D1).

**Opacity.** kappa is a free parameter. Values of 0.05-0.1 cm^2/g are common for stripped-envelope
supernovae (e.g. Drout et al. 2011, ApJ 741, 97, use 0.07). Results scale as kappa^1/2 [APPROX range].

**Arnett's rule.** L_peak is about L_decay(t_peak). The rise time is about tau_m (Arnett 1982). Check:
observed Type Ia B-band rise is about 17-19 d (Hayden et al. 2010, ApJ 712, 350; Firth et al. 2015,
MNRAS 446, 3895) [APPROX range].

### 4.3 Type II-P plateau, then the Ni tail

**Kasen & Woosley 2009** (ApJ 703, 2205; arXiv:0910.1590, Eq. 11). E51; M10 = M_ej/10 M_sun;
R500 = R/500 R_sun [SOURCE]:

- L50 = 1.26e42 E51^(5/6) M10^(-1/2) R500^(2/3) X_He erg/s.
- t_p,0 = 122 d E51^(-1/4) M10^(1/2) R500^(1/6) X_He^(1/2).
- Typographical corrections (Sukhbold et al. 2016, ApJ 821, 38, arXiv:1510.04643): X_He is normalized
  to 0.33, and t_p,0 is proportional to X_He,0.33^(-1/2). Goldberg et al. 2019 quote the Kasen & Woosley
  normalization as log L50 = 42.10 + log(X_He/0.33).

**Goldberg, Bildsten & Paxton 2019** (ApJ 879, 3; arXiv:1903.09114). Fitted to MESA+STELLA models;
recommended [SOURCE]:

- log10(L50 / erg s^-1) = 42.16 - 0.40 log M10 + 0.74 log E51 + 0.76 log R500.
- Plateau duration including Ni: log10(t_p / d) = 2.184 + 0.134 log(M_Ni/M_sun) + 0.411 log M10 - 0.282 log E51.
  This fit uses 218 models with M_Ni of about 0.03 M_sun or more.
- Ni extension in the Kasen & Woosley form: t_p = t_p,0 (1 + C_f M_Ni E51^-1/2 M10^-1/2 R500^-1)^(1/6),
  with C_f of about 87 in Goldberg et al. units.
- Popov 1993 (ApJ 414, 712) analytic scalings, for reference: L proportional to M^-1/2 E^5/6 R^2/3
  kappa^-1/3 T_ion^4/3; t_p proportional to M^1/2 E^-1/6 R^1/6 kappa^1/6 T_ion^-2/3.

**Assembly for the app** [APPROX procedure]:

1. For t below about 1-3 d, use shock cooling (3.3, Sapir & Waxman with n = 3/2 and M_env = H envelope).
2. Plateau: hold L at L50, or give it a gentle slope. The Kasen & Woosley V-band relation includes a
   +3.1 log(t/50 d) magnitude term at fixed velocity, so the plateau fades slowly.
3. At t_p, drop over a transition several days wide. A Fermi-Dirac-like step (Olivares et al. 2010,
   ApJ 715, 833) gives the shape. The width parameter is a free display choice.
4. After the drop, use L_dep(t) from 4.1. II-P tails are close to full trapping during the first year.
5. If H envelope mass is below about 1 M_sun, treat the event as II-L/IIb-like: shorter or no plateau,
   and an Arnett peak [APPROX].

Standard-candle checks (Kasen & Woosley 2009):
- M_V(50 d) = -17.4 - 6.9 log10(v_ph/5,000 km/s) + 3.1 log10(t/50 d), rms 0.27 mag.
- M_V,50 = -18.4 - 0.03 (t_p,0 - 100 d), rms about 0.4 mag.

### 4.4 Magnetar-powered light curves (fast NS spin, strong B)

**Kasen & Bildsten 2010** (ApJ 717, 245; arXiv:0911.0680). I_NS = 1e45 g cm^2, R_NS = 10 km,
sin^2(alpha) = 1/2 [SOURCE]:

- E_p = 2e50 P10^-2 erg = 2e52 (P/1 ms)^-2 erg.
- t_p = 1.3 B14^-2 P10^2 yr, i.e. 4.7 d B14^-2 P_ms^2.
- L_p(t) = (E_p / t_p) (l - 1) / (1 + t/t_p)^l, with l = 2 for dipole spin-down.
- t_d = [ (3/(4 pi)) M_ej kappa / (v_f c) ]^(1/2), with v_f = [(E_p + E_sn)/(2 M_ej)]^(1/2).
- Peak (l = 2): L_peak is about f (E_p t_p / t_d^2) [ln(1 + t_d/t_p) - t_d/(t_d + t_p)], with f = 3/2.
- t_peak = t_p ([ (l-1) E_p / (L_peak t_p) ]^(1/l) - 1).

**Inserra et al. 2013**, Eqs. D6-D7 (45 deg field inclination) [SOURCE]:
L_mag(t) = 4.9e46 B14^2 P_ms^-4 (1 + t/tau_p)^-2 erg/s, with tau_p = 4.7 d B14^-2 P_ms^2.
Feed this through the Arnett kernel (4.2).
Fitted SLSN-I values: B14 = 3.6-7.4, P = 1.7-7.5 ms, M_ej = 2.3-8.6 M_sun.

Other samples (Nicholl et al. 2017, as cited in arXiv:1812.03749): P of about 1.2-4 ms, B of about
(0.2-1.8)e14 G, M_ej = 2.2-12.9 M_sun.

Worked example [DERIVED]: P = 2 ms, B = 1e14 G gives E_p = 5e51 erg, t_p = 18.8 d and
L0 = 3.1e45 erg/s.

**Caveat.** Full trapping of the spin-down power is assumed. At late times the trapping falls
(gamma-ray and X-ray leakage), so the tail is overestimated.

### 4.5 Bolometric to V (or r) magnitude

**M_bol** = -2.5 log10(L / 3.0128e35 erg/s) (IAU 2015 B2).

**Lyman, Bersier & James 2014** (MNRAS 437, 3848; arXiv:1311.1946) give quadratic bolometric
corrections, BC_x = c0 + c1 (x - y) + c2 (x - y)^2, where BC_x = M_bol - M_x (BC can be positive).
Selected fits [SOURCE, Tables 2-3]:

| Sample | Colour | Valid range | c0 | c1 | c2 | rms |
|---|---|---|---|---|---|---|
| Stripped-envelope | B-V (gives BC_B) | 0.0-1.3 | -0.083 | -0.139 | -0.691 | 0.109 |
| Stripped-envelope | g-r (gives BC_g) | -0.3-1.0 | +0.054 | -0.195 | -0.719 | 0.076 |
| Type II | B-V (gives BC_B) | 0.0-1.6 | -0.138 | -0.013 | -0.649 | 0.094 |
| Type II | g-r (gives BC_g) | -0.2-1.3 | +0.053 | -0.089 | -0.736 | 0.059 |
| Type II, cooling phase | g-r | | -0.146 | +0.479 | -2.257 | |

Then M_V = M_B - (B - V).

**Colour evolution.** Synthesize colours from template spectra (Section 10.3), or use a blackbody at
T_col.
- Shock cooling: T_col from 3.3.
- II-P plateau: photospheric T near hydrogen recombination, about 5,000-7,000 K [APPROX].
- Ia/Ibc near peak: T of about 8,000-12,000 K [APPROX].
- Blackbodies overestimate UV flux, because metal-line blanketing suppresses it. Templates are preferred.
- Type Ia late colour (Lira relation): (B-V) = 0.725 - 0.0118 (t_V - 60 d), for 30-90 d after
  V maximum (Phillips et al. 1999, AJ 118, 1766).

**Filter zero points.** V (Vega): f_lambda,0 = 3.63e-9 erg/s/cm^2/Angstrom, lambda_eff about 5,450 Angstrom
(Bessell, Castelli & Plez 1998, A&A 333, 231). AB: m_AB = -2.5 log10(f_nu) - 48.60 (Oke & Gunn 1983,
ApJ 266, 713).

### 4.6 Peak absolute magnitudes by type

Richardson et al. 2014 (AJ 147, 118; arXiv:1403.5755), B band, corrected for host and foreground
extinction, H0 = 70. Volume-limited sample (distance modulus below 35, about 100 Mpc) [SOURCE]:

| Type | Mean M_B | sigma | N |
|---|---|---|---|
| Ia (normal) | -19.26 | 0.51 | 171 |
| Ib | -17.54 | 0.94 | 18 |
| Ic | -17.67 | 1.04 | 36 |
| IIb | -17.03 | 0.93 | 15 |
| II-L | -17.98 | 0.90 | 17 |
| II-P | -16.80 | 0.97 | 74 |
| IIn | -18.62 | 1.48 | 21 |

Other anchors:
- SN 1998bw (Ic-BL, with GRB 980425): peak V = 13.62, M_V = -19.35 (Galama et al. 1998, Nature 395,
  670; arXiv:astro-ph/9806175).
- SN 1987A peaked near V of about 2.9-3 at about day 85; the low peak reflects its compact blue
  supergiant progenitor (Arnett et al. 1989, ARA&A 27, 629) [APPROX value].

### 4.7 Apparent magnitude, extinction, naked eye

- m_V = M_V + 5 log10(D/10 pc) + A_V.
- A_V = R_V E(B-V), with R_V = 3.1 for the diffuse ISM (Cardelli, Clayton & Mathis 1989, ApJ 345, 245).
  Other bands follow the CCM law: A_B/A_V of about 1.32, A_R/A_V about 0.75, A_I/A_V about 0.48
  [APPROX, read from the CCM curve].
- Galactic dust: Adams et al. 2013 (ApJ 778, 164; arXiv:1306.0559) normalize to A_V = 30 mag toward the
  Galactic Centre. The magnitude of the next Galactic supernova is set mainly by extinction, not
  distance. App option: A_V = (30 mag)(D / 8.5 kpc) in the disk plane [APPROX], with a slider for
  high-latitude lines of sight.
- Naked-eye limit: m_V of about 6 under dark skies (conventional value, roughly 6-6.5) [APPROX].
  Adams et al. 2013 put the chance of a naked-eye Galactic supernova within 50 years at about 20-50%.

---

## 5. Gamma-ray lines

### 5.1 Lines

| Isotope (half-life) | Line energy (keV) and intensity per decay | Source |
|---|---|---|
| 56Ni (6.075 d) | 158.38 (98.8%), 811.85 (86.0%), 749.95 (49.5%), 269.50 (36.5%), 480.44 (36.5%), 1561.80 (14.0%) | Junde et al. 2011, NDS 112, 1513 (NNDC) |
| 56Co (77.24 d) | 846.77 (about 100%), 1238.3 (66%); plus e+ annihilation (positrons in 19% of decays) | Churazov et al. 2014, Nature 512, 406 (arXiv:1405.3332) |
| 44Ti (58.9 +/- 0.3 yr) -> 44Sc -> 44Ca | 67.87 (93.0%), 78.32 (96.8%), 1157.0 (about 100%) | Ahmad et al. 2006, PRC 74, 065803; NNDC |

### 5.2 Line flux model [DERIVED from decay kinematics]

- Number of 56Ni atoms at t = 0: N0 = M_Ni / (56 m_u).
- Line photon flux: F_line(t) = [N(t)/tau] * I_line * f_esc(t) / (4 pi D^2) photons/cm^2/s.
  - N_Ni(t) = N0 e^(-t/tau_Ni).
  - N_Co(t) = N0 (tau_Co/(tau_Co - tau_Ni)) (e^(-t/tau_Co) - e^(-t/tau_Ni)).
- Escape fraction: f_esc(t) of about exp(-(T0/t)^2), with T0 from 4.1. This ignores Compton-scattered
  photons, which leave the line [APPROX].
- Emergence: the 56Co lines peak at about 70-100 d for SNe Ia (Diehl et al. 2014). Using the formula
  above, the peak falls near t = sqrt(2) T0 for t much less than tau_Co.
- Calibration check against SN 2014J [DERIVED]: M_Ni = 0.6 M_sun, D = 3.53 Mpc, t = 75 d, f_esc = 0.6
  gives F_847 of about 3.0e-4, versus the measured 2.34e-4 +/- 0.74e-4. Consistent.

### 5.3 Observed

- **SN 2014J** (Ia, M82, D = 3.53 +/- 0.26 Mpc) [SOURCE]:
  - Churazov et al. 2014, INTEGRAL days 50-100: F_847 = (2.34 +/- 0.74)e-4 and
    F_1238 = (2.78 +/- 0.74)e-4 photons/cm^2/s. Escape fractions about 0.60/0.64.
    M_Ni = 0.61 +/- 0.13 M_sun. Line width about 4,100 km/s rms; blueshift about -3,100 km/s.
  - Diehl et al. 2014, Science 345, 1162 (arXiv:1407.3061), SPI days 16.6-19.2:
    F_158 = (1.10 +/- 0.42)e-4 and F_812 = (1.90 +/- 0.66)e-4 photons/cm^2/s, implying about 0.06 M_sun
    of 56Ni near the surface.
  - Later SPI analysis gives M_Ni = 0.49 +/- 0.09 M_sun (Diehl et al. 2015, A&A 574, A72; arXiv:1409.5477).
- **SN 1987A:**
  - SMM detected the 847 keV line from about day 160-250 on. Average 847 keV flux from 1 Aug to
    31 Oct 1987 was (1.0 +/- 0.25)e-3 photons/cm^2/s; the 1238 keV evidence was weaker
    (Matz et al. 1988, Nature 331, 416; IAU Circ. 4510).
  - The early emergence needed 56Co mixed outward.
  - 44Ti mass about 1.5e-4 M_sun (NuSTAR; Boggs et al. 2015, Science 348, 670) [value quoted from memory
    of the abstract; verify before shipping].
- **Cas A 44Ti** (D of about 3.4 kpc):
  - NuSTAR: initial M(44Ti) = (1.25 +/- 0.3)e-4 M_sun, from the 68 keV line (Grefenstette et al. 2014,
    Nature 506, 339, arXiv:1403.4978).
  - Deeper data: (1.54 +/- 0.21)e-4 M_sun (Grefenstette et al. 2017, ApJ 834, 19).
  - INTEGRAL: (1.37 +/- 0.19)e-4 M_sun (Siegert et al. 2015, A&A 579, A124).
  - Fastest clumps about 5,350 km/s.
  - Flux check [DERIVED]: 1.25e-4 M_sun, age about 333 yr in 2014, tau = 85 yr gives
    F_68 of about 1.7e-5 photons/cm^2/s.

### 5.4 Sensitivities (narrow line, 3 sigma)

| Instrument | Sensitivity | Source |
|---|---|---|
| INTEGRAL/SPI | 3e-5 photons/cm^2/s at 1 MeV for 1e6 s; about 5e-5 at 511 keV; 2.5e-5 at 1,809 keV. Energy resolution about 2 keV at 1 MeV. INTEGRAL ended science operations in Feb 2025 [verify date] | Roques et al. 2003, A&A 411, L91 (arXiv:astro-ph/0310793) |
| COSI (NASA SMEX, launch planned 2027) | 2-yr survey: 1.2e-5 at 511 keV; 3.0e-6 at 1.157 MeV (44Ti) and 1.809 MeV. Resolution 6.0 keV at 511 keV and 9.0 keV at 1.157 MeV. 0.2-5 MeV band. Expected to detect 56Co from an Ia at 20 Mpc | Tomsick et al. 2023, arXiv:2308.12362 |
| NuSTAR (68/78 keV) | Detects Cas A 44Ti in Ms exposures | Grefenstette et al. 2014 |

**Detection test:** compare F_line, averaged over the observing window, with the sensitivity scaled as
(T_obs / T_ref)^-1/2 [APPROX, background-dominated]. For a pointed SPI observation of length T:
S(T) = 3e-5 (1e6 s / T)^1/2.

Worked example [DERIVED]: a Type Ia with M_Ni = 0.6 M_sun at 10 Mpc gives F_847 peak of about 3e-5.
That is marginal for SPI and clear for COSI over the event, but COSI's survey exposure on one source
in about 100 d is far less than 2 yr. Apply the same scaling.

---

## 6. Radio and X-ray from circumstellar interaction (days to years)

### 6.1 Synchrotron self-absorption (Chevalier 1998, ApJ 499, 810)

Formulas as given in Chevalier & Fransson 2006 (ApJ 651, 381; arXiv:astro-ph/0607196), for p = 3.
alpha = eps_e/eps_B; f = emitting-volume filling factor (0.5) [SOURCE]:

- R_p = 4.0e14 alpha^-1/19 (f/0.5)^-1/19 (F_op/mJy)^9/19 (D/Mpc)^18/19 (nu/5 GHz)^-1 cm.
- B_p = 1.1 alpha^-4/19 (f/0.5)^-4/19 (F_op/mJy)^-2/19 (D/Mpc)^-4/19 (nu/5 GHz) G.
- Shock speed: v_s is about 0.88 R_p / t_p.
- u_B = B^2/(8 pi) = 0.052 eps_B,-1 A_* t_10^-2 erg/cm^3, with eps_B,-1 = eps_B/0.1 and t_10 = t/10 d.

**Forward model for the app** [DERIVED by inverting the two equations above]:

1. B(t) = sqrt(8 pi * 0.052 eps_B,-1 A_* t_10^-2) G.
2. R(t) = v_s t.
3. Peak spectral luminosity:
   L_nu,p = 1.197e24 [ R B / (4.4e14 alpha^-5/19 (f/0.5)^-5/19) ]^(19/7) erg/s/Hz,
   with R in cm and B in G. 1.197e24 = 4 pi (1 Mpc)^2 x 1 mJy.
4. Peak frequency:
   nu_p = 5 GHz (B / 1.1 G) alpha^4/19 (f/0.5)^4/19 (L_nu,p / 1.197e24)^(2/19).
5. Spectrum: L_nu proportional to nu^5/2 below nu_p and nu^-(p-1)/2 = nu^-1 above.
   Use a smooth join, e.g. L_nu = L_p [ (nu/nu_p)^(-5/2 s) + (nu/nu_p)^(s) ]^(-1/s) with s of about 1
   [APPROX shape].
   [CONTROLLER NOTE 2026-11-11 (ruling R42): as written this join peaks at 0.55 L_p at 1.30 nu_p (with s = 1).
   The app rescales it so the maximum is L_p at nu_p: L_nu = 1.81907 L_p / ((x0 x)^(-5/2) + x0 x), x = nu/nu_p,
   x0 = 2.5^(2/7) = 1.29935. The asymptotic slopes are unchanged.]

Check [DERIVED]: SN 1994I (t_p = 36 d, 4.86 GHz, 17 mJy, 8.3 Mpc) gives R = 1.17e16 cm and B = 0.51 G.
Reinserting gives L_p = 1.42e27 erg/s/Hz at 4.88 GHz, which matches the observed 1.40e27.

**Shock velocity** [APPROX]: use the observed ranges.
- Ib/c: R_p/t_p of about 32,000-105,000 km/s (CF06 Table 3: SN 1983N, 1990B, 1994I, 2001ig, 2002ap,
  2003L, 2003bg).
- II-P: about 1e4 km/s scale (Chevalier, Fransson & Nymark 2006, ApJ 641, 1029).
- Scale with sqrt(E/M_ej) relative to the canonical E = 1e51 erg and M_ej = 3 M_sun (Ib/c) or 10 M_sun (II).
- For II-P and IIn, free-free absorption by the dense wind can dominate over synchrotron self-absorption
  at early times. The SSA-only model then overestimates early low-frequency flux.

**Population ranges** (Bietenholz et al. 2021, ApJ 908, 75, arXiv:2011.11737; 2-10 GHz, 294 SNe,
D < 100 Mpc) [SOURCE]:

| Type | Mean log10 L_pk (erg/s/Hz) | sigma | Rise time t_pk |
|---|---|---|---|
| II (excluding IIn) | 25.3 | 1.3 | 10^(1.6 +/- 1.0) d |
| Ib/c | 25.4 | 1.7 | 10^(1.1 +/- 0.5) d |
| IIn | 26.5 | 1.1 | |

Only 31% of supernovae were detected.

**SN 2008D example** (Soderberg et al. 2008): R of about 3e15 (t/5 d)^0.9 cm; B of about 2.4 (t/5 d)^-1 G;
v of about 0.25c; Mdot of about 7e-6 M_sun/yr.

### 6.2 X-rays

**Thermal free-free** (reverse-shock dominated; CF06 Eq. 30) [SOURCE]:

L_ff = 3e35 [(n-3)(n-4)^2 / (4(n-2))] beta^1/2 zeta_2^-1 A_*^2 t_10^-1 erg/s.

- For n = 10 this is 2.4e36 beta^1/2 zeta_2^-1 A_*^2 t_10^-1 erg/s.
- beta = T_e/T_eq is 1 or less. Set zeta_2 to about 1 (an order-unity factor in CF06) [APPROX].
- Example [DERIVED]: red supergiant with A_* = 10 gives about 2.4e38 erg/s at 10 d, the typical II-P range.

**Inverse Compton** (CF06 Eq. 31): E dL_IC/dE is about
8.8e36 gamma_min eps_r,-1 A_* v_s4 (L_bol/1e42 erg/s) t_10^-1 erg/s.

**Observed scale** [SOURCE]:
- II-P are the faintest X-ray supernovae, up to about 3e39 erg/s (Dwarkadas 2014, MNRAS 440, 1917).
- IIn are the brightest and thermal; Ib/c emission is non-thermal (Dwarkadas & Gruszko 2012,
  MNRAS 419, 1515).
- SN 2008D at about 10 d: 1e39 erg/s (Soderberg et al. 2008).

**Sensitivity** [APPROX]: Chandra/XMM can reach about 1e-15 erg/cm^2/s in about 100 ks. Swift XRT about
1e-13 to 1e-14 in about 10 ks. Einstein Probe WXT about 2.5e-11 in 1,000 s.

### 6.3 SN 1987A ring

- The forward shock reached the equatorial ring at about 3,000 d (first optical hotspot 1995;
  Sonneborn et al. 1998, ApJ 492, L139; Fransson et al. 2015, ApJL 806, L19).
- Optical ring emission peaked at about 8,000-8,300 d (about 2009) and then faded roughly linearly.
  The blast wave has left the ring and clumps are being destroyed (Fransson et al. 2015; Larsson et al.
  2019, arXiv:1910.09582).
- Chandra 0.5-2 keV flux has been flat at about 8e-12 erg/cm^2/s since about 9,500 d (about 2013).
  The 3-8 keV flux kept rising until at least 10,000 d (Frank et al. 2016, ApJ 829, 40,
  arXiv:1608.02160).
- App implementation [APPROX]: for SN 1987A-like presets, add a ring component with a logistic rise
  from 3,000 to 9,000 d, a plateau, then a slow decline after about 10,000 d.

---

## 7. Supernova remnant evolution (years to about 1,000 years)

### 7.1 Characteristic scales (Truelove & McKee 1999, ApJS 120, 299; erratum ApJS 128, 403)

For a uniform ambient medium, rho_0 = n_0 m_p (as written by Leahy & Williams 2017, AJ 153, 239,
arXiv:1607.06391) [SOURCE]:

- R_ch = 3.4 pc (M_ej/M_sun)^1/3 n_0^-1/3.
- t_ch = 473 yr (E/1e51 erg)^-1/2 (M_ej/M_sun)^5/6 n_0^-1/3.

### 7.2 Blast-wave radius versus time

Uniform ejecta (n = 0) interpolation, smooth from free expansion to Sedov-Taylor (Leahy & Williams 2017,
Table 1 and Table 3; s = 0) [SOURCE]. With R* = R_b/R_ch and t* = t/t_ch:

R* = [ (lambda_b t*)^(-2 alpha) + (xi t*^2)^(-2 alpha/5) ]^(-1/(2 alpha)),
with lambda_b = 2.01, xi = 2.026 and alpha = 1.25. The transition is near t* of about 0.4, R* about 0.6.

For n = 7 power-law ejecta, use their form
R* = [ (zeta_b t*^((n-3)/n))^(-alpha) + (xi t*^2)^(-alpha/5) ]^(-1/alpha), with zeta_b = 1.06 and alpha = 20.3.

**Sedov-Taylor limit** [DERIVED]: R = 1.152 (E t^2 / rho_0)^1/5 (xi^1/5 = 1.152).
That is R of about 0.336 pc E51^0.2 n_0^-0.2 (t/yr)^0.4 for rho_0 = n_0 m_p, or 0.314 pc with mean mass
per H of 1.4 m_p.

**Shock speed and temperature** (Sedov-Taylor) [DERIVED]: v_s = 0.4 R/t; kT_s = (3/16) mu m_p v_s^2
(strong shock, gamma = 5/3, mu of about 0.6).

**Radiative phase** begins at tens of kyr for n_0 of about 1: t_PDS of about 1.33e4 E51^(3/14) n_0^(-4/7) yr
(Cioffi, McKee & Bertschinger 1988, ApJ 334, 252) [coefficient from memory, so marked APPROX; outside
the app's 1,000-yr range unless n_0 is much greater than 1].

**Wind medium** (r^-2; young core-collapse remnants inside the progenitor wind): use Truelove & McKee
s = 2 solutions, or the A_* density. For Cas A, Chevalier & Oishi 2003 (ApJ 593, L23) found the remnant
is still expanding into the red supergiant wind [reference only; values not transcribed].

### 7.3 Reference young remnants

From Green's catalogue (2024 Oct version, mrao.cam.ac.uk/surveys/snrs/) [SOURCE]:

| Remnant | Event / age | Distance | Angular size | S(1 GHz) | Spectral index alpha (S proportional to nu^-alpha) | Type |
|---|---|---|---|---|---|---|
| Cas A (G111.7-2.1) | about 1681 +/- 19 (Fesen et al. 2006, ApJ 645, 283) | 3.4 kpc (Reed et al. 1995, ApJ 440, 706) | 5' | 2,300 Jy | 0.77 | shell, core-collapse (IIb; Krause et al. 2008, Science 320, 1195) |
| Crab (G184.6-5.8) | SN 1054 | about 2 kpc (Trimble 1973, PASP 85, 579) | 7' x 5' | 900 Jy | 0.30 | filled-centre (pulsar wind nebula) |
| Tycho (G120.1+1.4) | SN 1572, Ia | 2.5-4 kpc (debated) | 8' | 50 Jy | 0.58 | shell |
| Kepler (G4.5+6.8) | SN 1604, Ia | 5.1 (+0.8 / -0.7) kpc (Sankrit et al. 2016, ApJ 817, 36) | 3' | 19 Jy | 0.64 | shell |
| SN 1006 (G327.6+14.6) | SN 1006, Ia | about 2.2 kpc | 30' | 19 Jy | 0.6 | shell |
| G1.9+0.3 | about 1900 (youngest known) | about 8.5 kpc | 1.5' | 0.6 Jy | 0.6 | shell |

**X-ray.** Cas A, Tycho and Kepler all exceed 1e36 erg/s and are dominated by thermal (non-equilibrium
ionization) emission (Long et al. 2010, arXiv:1002.1839). Synchrotron 2-10 keV: Cas A about 1.1e36 erg/s,
Tycho about 1.2e35 erg/s (Nakamura et al. 2012, arXiv:1112.0822, Table).

**Radio fading.** Cas A at 1 GHz fades by about (0.97 - 0.30 log10 nu_GHz)% per year
(Baars et al. 1977, A&A 61, 99) [formula from that paper; more recent work finds lower rates].

**X-ray luminosity scaling for the app** [APPROX]: L_X is about Lambda(T) n_e n_H V_shell, with shocked
density 4 n_0 and a shell of about R/12 thickness. Free-free emissivity:
eps_ff = 1.4e-27 T^1/2 n_e n_i Z^2 g_B erg/cm^3/s (Rybicki & Lightman 1979, Eq. 5.15b, g_B about 1.2).
Line emission raises the 0.5-2 keV output by about 10 times at 1e7 K and below [APPROX]. Calibrate the
display against Cas A, Tycho and Kepler.

---

## 8. Pulsars and pulsar wind nebulae

### 8.1 Spin-down (vacuum dipole; Lorimer & Kramer 2005, *Handbook of Pulsar Astronomy*)

- Spin-down luminosity: Edot = 4 pi^2 I Pdot / P^3 (I = 1e45 g cm^2).
- Surface dipole field: B = 3.2e19 sqrt(P Pdot) G.
- Characteristic age: tau_c = P / (2 Pdot).
- Braking index n: P(t) = P0 (1 + t/tau_0)^(1/(n-1)) and Edot(t) = Edot_0 (1 + t/tau_0)^(-(n+1)/(n-1)).
  For n = 3 this is (1 + t/tau_0)^-2, the same as Kasen & Bildsten l = 2.
- From the inputs P0 and B: Pdot_0 = B^2 / (3.2e19)^2 / P0. Then tau_0 = P0 / (2 Pdot_0) for n = 3.
- Normalizations differ between dipole conventions: Kasen & Bildsten use sin^2(alpha) = 1/2. Use one
  convention throughout the app [caveat].

### 8.2 Crab (reference)

- P = 33.4 ms; Pdot = 4.21e-13 s/s (Taylor, Manchester & Lyne 1993 values, via arXiv:1409.2004).
- Edot = 4.5e38 erg/s for I = 1e45.
- n = 2.509 +/- 0.001 (Lyne, Pritchard & Smith 1988; Lyne et al. 1993).
- B is about 3.8e12 G [DERIVED from the formula above].
- Nebula luminosity about 1.3e38 erg/s (Hester 2008, ARA&A 46, 127, as cited by Bühler & Blandford 2014,
  RPPh 77, 066901). That is about 30% of Edot [DERIVED].
- Nebula size about 4.1 x 2.9 pc at 2 kpc [DERIVED from the 7' x 5' catalogue size].
- Radio pulsar flux S_1400 of about 14 mJy (ATNF catalogue; Manchester et al. 2005, AJ 129, 1993)
  [catalogue value; verify at atnf.csiro.au/research/pulsar/psrcat].

### 8.3 Radio pulses and beaming

- **Beaming fraction:** f(P) = 0.09 [log10(P/s) - 1]^2 + 0.03 (Tauris & Manchester 1998, MNRAS 298, 625).
  It was fitted to slow pulsars (P of about 0.1 s or more). For shorter periods, a linear extension is
  used in the literature: f = -0.255 log10(P/s) + 0.135, capped at 0.9 (Titus et al. 2020, as cited in
  arXiv:2402.14030).
  - Example [DERIVED]: Crab P = 0.0334 s gives f of about 0.51 (extension) or 0.58 (original formula).
  - The app should roll the dice once per pulsar: beamed toward Earth with probability f.
- **Radio luminosity function:** log-normal in L_1400 = S D^2, with <log10 L> = -1.1 and sigma = 0.9
  (L in mJy kpc^2) (Faucher-Giguère & Kaspi 2006, ApJ 643, 332).
  - Detected flux: S = L / D^2.
  - Typical survey threshold about 0.1-0.2 mJy at 1.4 GHz (e.g. Parkes Multibeam; Manchester et al.
    2001, MNRAS 328, 17) [APPROX]. Large telescopes reach about 10 microJy.
  - Radiometer equation: S_min = (S/N) T_sys / (G sqrt(n_p t Delta_f)) sqrt(W/(P - W)) (Lorimer & Kramer 2005).
- **Visibility caveat:** for a young supernova the ejecta are opaque to radio (free-free absorption) for
  years to decades. For SN 1987A, no pulsar has been detected; JWST found evidence of a compact source via
  ionized Ar lines (Fransson et al. 2024, Science 383, 898) [APPROX: the opacity time is not modeled here;
  show "pulses visible" after about 10-30 yr].

### 8.4 Neutron star thermal X-ray cooling (Yakovlev & Pethick 2004, ARA&A 42, 169; arXiv:astro-ph/0402143)

**Stages** [SOURCE]:
- Thermal relaxation, about 10-100 yr: crust decoupled from the core.
- Neutrino cooling era, up to about 1e5 yr.
- Photon cooling era after about 1e5 yr.

**Slow (modified Urca) cooling.** The neutrino-era timescale t_slow is about 1 yr / T_i9^6, so
T_i is about 1e9 K (t/yr)^(-1/6) [DERIVED inversion]. T_i is about 1.5e8 K at 1e5 yr (consistent with
the paper). T_s^inf stays above about 1e6 K for t below about 1e5 yr.

**Fast (direct Urca) cooling.** t_fast is about 1 min / T_i9^4, so T_i is proportional to t^(-1/4).
T_s^inf is below 3e5 K by about 1e4 yr.

**Envelope relation** (Gudmundsson, Pethick & Epstein 1983, as given by Yakovlev & Pethick):
T_b = 1.288e8 (T_s6^4 / g_14)^0.455 K, valid for T_s of about 2e5 K or more.
Invert: T_s6 = [ g_14 (T_b/1.288e8)^(1/0.455) ]^(1/4).
g_14 is about 2.4 for 1.4 M_sun and 10 km [APPROX]. Observed (redshifted) T_s^inf = T_s sqrt(1 - r_g/R).

Worked example [DERIVED]: t = 300 yr gives T_b about 3.9e8 K, T_s about 2.3e6 K, T_s^inf about 1.8e6 K,
and L_gamma of about 3e34 erg/s (R = 12 km). The Cas A neutron star (about 340 yr) is measured at about
2.0-2.1e6 K (carbon atmosphere; Heinke & Ho 2010, ApJL 719, L167; Ho & Heinke 2009, Nature 462, 71).
The reported fast cooling of about 4% per 10 yr (Shternin et al. 2011, arXiv:1012.0045) is debated;
later analyses find a weaker decline.

**Display.** Use a blackbody at T_s^inf with R_inf = R / sqrt(1 - r_g/R). Fold in interstellar absorption
N_H of about 2.2e21 A_V cm^-2 (Güver & Özel 2009, MNRAS 400, 2050) [APPROX coefficient; verify].
Before about 100 yr the surface temperature depends on the unrelaxed crust; show a band, not a line.

### 8.5 Pulsar wind nebula, simple model [APPROX]

- L_PWN(t) is about eta Edot(t), with eta of about 0.3 by Crab analogy [DERIVED]. Radio-to-X-ray spectrum
  roughly a broken power law.
- The radius grows proportional to t^(6/5) inside freely expanding ejecta (Chevalier & Fransson 1992,
  ApJ 395, 540) [scaling only; coefficient not transcribed].

---

## 9. Gamma-ray bursts from collapsars

### 9.1 Prompt emission

- **Duration:** long GRBs have T90 above about 2 s (bimodal T90; Kouveliotou et al. 1993, ApJ 413, L101).
- **Isotropic energy:** E_iso from about 1e48 erg (low-luminosity GRB 980425: 8.5e47 erg) up to about
  1e54 erg.
- **Peak energy (Amati relation; Amati 2006, MNRAS 372, 233, arXiv:astro-ph/0601553)** [SOURCE]:
  E_p,i = 95 (+11/-9) keV (E_iso / 1e52 erg)^0.49 (+0.06/-0.05), with extra scatter sigma(log) = 0.15.
  Observed E_p = E_p,i / (1+z). Low-luminosity GRBs such as 980425 are outliers.
- **Spectrum:** Band function (Band et al. 1993, ApJ 413, 281). Typical low-energy index alpha of about -1
  and high-energy index beta of about -2.3 [APPROX typical values].
- **Light curve** [APPROX]: a set of FRED-shaped pulses (fast rise, exponential decay) filling T90; total
  fluence S = E_iso / (4 pi D^2) for an on-axis observer.
- **Detection threshold** [APPROX]: Fermi GBM peak flux about 0.7 photons/cm^2/s (50-300 keV, 1 s)
  (Meegan et al. 2009, ApJ 702, 791 [verify]); Swift BAT about 1e-8 erg/cm^2/s.
  - A GRB 980425-like event (peak 3e-7 erg/cm^2/s at about 39 Mpc) is detectable to about 200 Mpc
    [DERIVED; inverse-square scaling].

### 9.2 Beaming

- Jet half-opening angle from the jet-break time t_j (Sari, Piran & Halpern 1999, ApJ 519, L17, as used
  in Frail et al. 2001, ApJ 562, L55, arXiv:astro-ph/0102282) [SOURCE form]:
  theta_j = 0.057 (t_j/1 d)^(3/8) ((1+z)/2)^(-3/8) (E_iso/1e53 erg)^(-1/8) (eta_gamma/0.2)^(1/8) (n/0.1 cm^-3)^(1/8) rad.
- Beaming-corrected energies cluster near E_gamma of about 5e50 erg (Frail et al. 2001).
- Probability that a random observer sits inside the jet: f_b = 1 - cos(theta_j), about theta_j^2/2
  (two-sided). Example: theta_j = 5 deg gives f_b of about 0.0038 [DERIVED].
- If off-axis: no prompt GRB is seen. A late "orphan" afterglow peaks when the jet decelerates into view
  [qualitative; not modeled].

### 9.3 Afterglow (synchrotron from the forward shock)

**Uniform ISM** (Sari, Piran & Narayan 1998, ApJ 497, L17, arXiv:astro-ph/9712005; adiabatic,
p of about 2.5; t_d in days; E52 = E_iso/1e52 erg; n_1 in cm^-3; D28 = D/1e28 cm) [SOURCE]:

- nu_c = 2.7e12 eps_B^(-3/2) E52^(-1/2) n_1^(-1) t_d^(-1/2) Hz.
- nu_m = 5.7e14 eps_B^(1/2) eps_e^2 E52^(1/2) t_d^(-3/2) Hz.
- F_nu,max = 1.1e5 eps_B^(1/2) E52 n_1^(1/2) D28^(-2) microJy.
- Fast-to-slow transition: t_0 = 210 eps_B^2 eps_e^2 E52 n_1 days.
- Spectrum, slow cooling: F_nu proportional to nu^(1/3) (below nu_m), nu^(-(p-1)/2) (nu_m to nu_c),
  nu^(-p/2) (above nu_c).
- Spectrum, fast cooling: nu^(1/3), then nu^(-1/2), then nu^(-p/2).
- Temporal indices, slow cooling [DERIVED from nu_m proportional to t^-3/2, nu_c proportional to t^-1/2,
  F_max constant]: t^(1/2) (below nu_m), t^(-3(p-1)/4) (nu_m to nu_c), t^(-(3p-2)/4) (above nu_c).
- After the jet break: F proportional to t^(-p) above nu_m (Sari, Piran & Halpern 1999).

**Wind medium** (Chevalier & Li 2000, ApJ 536, 195; normalized at z = 1; x = (1+z)/2) [SOURCE]:

- nu_m is about 5e12 x^(1/2) (eps_e/0.1)^2 (eps_B/0.1)^(1/2) E52^(1/2) t_d^(-3/2) Hz.
- nu_c is about 2e12 x^(-3/2) (eps_B/0.1)^(-3/2) E52^(1/2) A_*^(-2) t_d^(1/2) Hz.
- nu_A is about 1e11 x^(-2/5) (eps_e/0.1)^(-1) (eps_B/0.1)^(1/5) E52^(-2/5) A_*^(6/5) t_d^(-3/5) Hz.
- F_nu,max is about 20 mJy (eps_B/0.1)^(1/2) E52^(1/2) A_* t_d^(-1/2) at z = 1. Converted to a local
  luminosity [DERIVED, H0 = 65, d_L(z=1) = 1.67e28 cm]:
  L_nu,max is about 3.5e32 (eps_B/0.1)^(1/2) E52^(1/2) A_* t_d^(-1/2) erg/s/Hz.
  Then F_nu,max = L_nu,max / (4 pi D^2).
  [CONTROLLER NOTE 2026-11-11 (ruling R39): the 3.5e32 value is the z = 1 rest-frame luminosity per z = 1 observer
  day; read literally at z ~ 0 it is 1.41x too bright. The app uses Chevalier & Li 2000 Eq. 11 directly:
  F_nu,max = 7.0e32 x^(3/2) (eps_B/0.1)^(1/2) E52^(1/2) A_* t_d,obs^(-1/2) / (4 pi d_L^2) (with the paper's
  redshift factors). These wind formulas assume nu_A < nu_m and a relativistic blast wave; outside that range
  (e.g. low-luminosity GRB 980425) the app does not draw a GRB afterglow.]
- Above nu_m (slow cooling): F proportional to t^(-(3p-1)/4).
- For collapsars, use the wind solution with A_* from the inputs (Mdot, v_w). This is the physically
  consistent choice. Typical eps_e of about 0.1 and eps_B of about 0.01-0.1 [APPROX].

### 9.4 GRB 980425 / SN 1998bw (Galama et al. 1998)

- T90 = 23.3 +/- 1.4 s. Peak flux (3.0 +/- 0.3)e-7 erg/cm^2/s. Fluence (4.4 +/- 0.4)e-6 erg/cm^2
  (24-1,820 keV).
- E_gamma = (8.5 +/- 1.0)e47 erg; peak L = 5.5e46 erg/s.
- Host ESO 184-G82, z = 0.0085 (about 36-40 Mpc).
- SN 1998bw (Ic-BL): V_peak = 13.62, M_V = -19.35, photosphere expanding at "several 1e4 km/s".
- An outlier to the Amati relation (Amati 2006).

---

## 10. Spectral type signatures

### 10.1 Classification lines (Filippenko 1997, ARA&A 35, 309; Gal-Yam 2017, *Handbook of Supernovae*, arXiv:1611.09353)

| Type | Defining features near peak | Other features |
|---|---|---|
| II (P/L) | Strong H Balmer P-Cygni lines: H-alpha 6563, H-beta 4861 | Fe II 5169 (velocity tracer); Na I D; Ca II IR triplet later |
| IIb | H early (H-alpha), He I 5876/6678/7065 strengthening within weeks | Becomes Ib-like |
| Ib | He I 5876, 6678, 7065 (no H) | Fe II, Ca II, O I 7774 |
| Ic | No H, no (or weak) He | O I 7774; Ca II H&K and IR triplet 8498/8542/8662; Fe II |
| Ic-BL | Ic with very broad blended lines | Often GRB-associated |
| Ia | Si II 6355 (absorption near 6150 A), S II "W" 5454/5640, no H/He | Ca II H&K and IR; later [Fe II]/[Fe III] nebular |
| IIn | Narrow H emission (CSM interaction) | |
| Nebular (all core-collapse) | [O I] 6300/6364, [Ca II] 7291/7324 | |

### 10.2 Photospheric velocities (absorption minima)

| Type, line | Velocity | Source |
|---|---|---|
| Ia, Si II 6355 at B maximum | 10,600 +/- 400 km/s (normal); high-velocity group at about 12,000 km/s or more | Wang et al. 2009, ApJ 699, L139 |
| II, H-alpha at day 50 | median 7,300 km/s (range about 1,500-9,600) | Gutiérrez et al. 2017, ApJ 850, 89 (arXiv:1709.02487) |
| II-P, Fe II 5169 | v(t) = v_50 (t/50 d)^-0.464 | Nugent et al. 2006, ApJ 645, 841 |
| II-P, Fe II 5169 at day 50 | about 3,500-3,900 km/s mean | [secondhand values; APPROX] |
| IIb at V max | H-alpha 12,500 +/- 800; He I 5876 7,400 +/- 1,600; Fe II 8,400 +/- 1,400 km/s | Liu et al. 2016, ApJ 827, 90 (arXiv:1510.08049) |
| Ib at V max | He I 5876 10,000 +/- 3,000; Fe II 9,100 +/- 1,600 km/s | Liu et al. 2016 |
| Ic at V max | Fe II 11,000 +/- 3,700 (Liu 2016) or 9,700 +/- 1,500 (Modjaz 2016); O I 7774 13,600 +/- 4,300 km/s | Liu et al. 2016; Modjaz et al. 2016, ApJ 832, 108 (arXiv:1509.07124) |
| Ic-BL at V max | Fe II 16,700 +/- 2,700 (no GRB); 23,800 +/- 9,500 (with GRB; 19,200 +/- 2,900 excluding SN 2010bh) km/s | Modjaz et al. 2016 |

**Time dependence for schematic spectra** [APPROX]: velocities fall with time. Use
v(t) = v_peak (t / t_peak)^-0.46 for every type (borrowing the II-P exponent), with a floor near 0.3 v_peak.
Line widths: FWHM of about v/c x lambda.

### 10.3 Templates (to draw schematic spectra or synthesize colours)

- **Nugent templates** (c3.lbl.gov/nugent/nugent_templates.html): Ia normal (v1.2, UBVRIJHK),
  91T, 91bg, super-Chandrasekhar, Ib/c, Ib/c hypernova, IIP, IIL, IIn.
  - Format: day, wavelength (A), f_lambda (arbitrary units). Day 0 is explosion for the spectral series.
  - Cite: Nugent, Kim & Perlmutter 2002, PASP 114, 803; Levan et al. 2005 (Ib/c, hypernova);
    Baron et al. 2004 (IIP); Gilliland et al. 1999; Di Carlo et al. 2002 (IIn).
  - **No license is stated.** The page only warns users to use the templates carefully. Ask the author
    before redistributing them inside the app.
- **SESNspectraLib** (github.com/nyusngroup/SESNspectraLib; Liu et al. 2016; Modjaz et al. 2016):
  MIT license. Code plus Ic velocity templates. Mean IIb/Ib/Ic/Ic-BL spectra are described in the papers.
- Raw observed spectra: WISeREP (wiserep.org), with per-dataset terms.

---

## 11. Recommended signal set for the app

Times are after bounce. "Threshold vs D" gives the largest distance at which the view should show a
detection; scale between anchors as noted.

| View | Real instruments | What it plots | Model (section) | Valid time range | Detection threshold versus distance |
|---|---|---|---|---|---|
| Neutrino burst | Super-K (32 kt), Hyper-K, IceCube, JUNO, DUNE; SNEWS | Event rate (events/s) and cumulative events versus t; per-event energy scatter; flavor luminosity and mean energy | Pagliaroli 2009 two-component fit plus neutronization Gaussian; black hole cutoff at t_BH; IBD counting formula (1.2-1.4) | 1 ms to about 100 s | Counts scale as D^-2. At 10 kpc: Super-K 4,000-7,800; Hyper-K 28,000-53,000; JUNO about 5,000 IBD; DUNE about 3,000 nu_e charged-current. Super-K detects to about 200 kpc (about 8% at M31). IceCube about 6 sigma at 50 kpc. Show "detected" when N is 2 or more within 20 s (Super-K distant trigger) |
| Gravitational waves | LIGO/Virgo/KAGRA, Einstein Telescope, Cosmic Explorer | Strain h(t) and spectrogram (rising g/f-mode track, SASI band, bounce spike) | Energy-normalized stochastic chirp (2.4); bounce ring-down if rotating (2.2) | 0 to about 1-2 s | SNR proportional to 1/D. Non-rotating: aLIGO about 5-10 kpc; ET about 25-140 kpc; CE about 100 kpc. Rotating bounce: about 50-100+ kpc (aLIGO). Bar-mode: to about 1 Mpc (third generation) |
| X-ray/UV flash (shock breakout) | Swift XRT/BAT, Einstein Probe WXT, ULTRASAT | L(t), T(t), band flux | Kistler 2013 delay; Nakar & Sari 2010 / Waxman & Katz 2017 flash; Sapir & Waxman 2017 cooling (3.1-3.3) | Breakout at minutes (Wolf-Rayet) to about 1 d (red supergiant); cooling to about 3-10 d | SN 2008D-like (L_X about 6e43): Einstein Probe at about 2.5e-11 erg/cm^2/s gives about 140 Mpc [DERIVED]; Swift XRT for SN 2008D was about 200 Mpc (Soderberg et al. 2008). ULTRASAT 22.5 AB mag |
| Optical photometry | Ground telescopes, ZTF/Rubin, naked eye | Bolometric L(t); M_V or M_r; apparent m with A_V | Shock cooling, Arnett (I, Ic-BL, IIb), Kasen-Woosley/Goldberg plateau plus Ni tail (II-P), magnetar option; Lyman bolometric corrections (4.x) | about 1 h to about 1-3 yr (Ni tail; late-time positrons and 57Co ignored) | m_V = M_V + mu + A_V. Naked eye at m_V of about 6 or brighter; ZTF about 20.5; Rubin about 24 per visit [APPROX survey depths] |
| Gamma-ray spectrometer | INTEGRAL/SPI (historic), COSI (2027+), NuSTAR (44Ti) | Line fluxes 158/812/847/1238 keV versus t; 44Ti 68/78/1157 keV over centuries | Decay chain times escape fraction exp(-(T0/t)^2) (5.2) | 56Ni: about 10-100 d; 56Co: about 30-500 d; 44Ti: 1-1,000 yr | SPI 3e-5 photons/cm^2/s (1e6 s), so an Ia with 0.6 M_sun Ni is detectable to about 5-10 Mpc. COSI 3e-6 (2 yr survey), so 56Co from an Ia at about 20 Mpc. 44Ti: Cas A-like (1.3e-4 M_sun) is about 1.7e-5 at 3.4 kpc |
| Radio | VLA, MeerKAT, ATCA, SKA | L_nu(t) at chosen frequencies; peak nu_p(t) | Chevalier 1998 SSA, inverted (6.1), with A_* from Mdot and v_w | about 1 d to about 10-30 yr (then remnant phase) | Typical rms about 5-10 microJy/hr (VLA) [APPROX]; 5 sigma at about 30 microJy gives L = 1e25 erg/s/Hz detectable to about 17 Mpc, 1e26 to about 50 Mpc, and 1e27 to about 170 Mpc [DERIVED] |
| X-ray (interaction) | Chandra, XMM, Swift XRT, eROSITA | L_X(t) thermal plus inverse Compton | CF06 free-free (6.2) | about 1 d to about 30 yr | Chandra about 1e-15 erg/cm^2/s, so 1e39 erg/s is detectable to about 90 Mpc and 1e38 to about 30 Mpc [DERIVED] |
| Remnant | Chandra, VLA, any imager | Radius R(t), angular size, shock T, L_X, S_1GHz | Truelove-McKee / Leahy-Williams interpolation (7.2); reference remnants (7.3) | about 10-1,000 yr | Angular size theta = 2R/D; resolvable if above about 0.5" (Chandra). Radio calibrated to Cas A (2,300 Jy at 3.4 kpc) |
| Pulsar timing | FAST, Parkes, MeerKAT | Pulse train, P(t), Pdot, Edot(t) | Dipole spin-down (8.1); beaming dice (8.3) | about 10 yr (ejecta transparency) to 1,000 yr | S = L_1400/D^2; threshold about 0.1 mJy. Crab-like (56 mJy kpc^2) detectable to about 20-25 kpc [DERIVED]; median pulsar about 0.1 mJy kpc^2, i.e. about 1 kpc |
| Neutron star thermal X-ray | Chandra, XMM | T_s^inf(t), L_gamma(t) | Yakovlev & Pethick slow cooling with the Gudmundsson envelope (8.4) | about 30-1,000 yr (shown as a band before about 100 yr) | L about 3e34 erg/s at 300 yr: Chandra-detectable through the Galaxy if N_H allows; not beyond the Local Group |
| GRB monitor | Fermi GBM, Swift BAT, SVOM | Prompt light curve (T90), E_p; afterglow X-ray/optical/radio | Amati relation, FRED pulses (9.1); Sari-Piran-Narayan / Chevalier-Li afterglow (9.3) | Prompt 2-100+ s; afterglow minutes to months | Only if inside the jet (probability f_b). Classical GRB: whole observable universe; llGRB 980425-like to about 200 Mpc |
| Spectrograph | any optical spectrograph | Schematic spectrum: continuum (blackbody at T_col) plus P-Cygni lines at v(t) | Section 10 line list and velocities; Nugent templates if licensed | about 1 d to about 1 yr (photospheric to nebular) | Classification needs about 10 times more light than detection [APPROX]; i.e. m about 2.5 mag brighter than the photometric limit |

---

## 12. Key uncertainties to surface in the user interface

1. **Neutrino light curves** depend on the progenitor and equation of state at the factor-of-2 level
   (Super-K total varies 5 times across Nakazato models; Suwa et al. 2019). Flavor conversion is not
   settled (Janka 2017).
2. **GW amplitude** spans about 3 orders of magnitude in energy across 3D models. 2D waveforms are about
   4 times louder than 3D. Non-rotating supernovae are detectable only within a few kpc with current
   detectors.
3. **Shock breakout** properties depend on the outermost density profile and any dense wind
   (SN 2008D broke out at about 7 times the stellar radius). Red supergiant timing varies by up to a day
   with direction.
4. **Optical:** opacity kappa (0.05-0.1) and Ni mixing set Arnett rise times. Plateau fits carry about
   0.3-0.4 mag scatter. Bolometric corrections are good to about 0.1 mag only within the fitted colour
   ranges.
5. **Galactic extinction** dominates naked-eye visibility (A_V up to about 30 toward the Galactic Centre).
6. **Radio/X-ray** depend on microphysics (eps_e, eps_B) as much as on Mdot. Free-free absorption is not
   in the self-absorption-only model.
7. **Remnant** distances (Tycho, Crab) are uncertain by tens of percent.
8. Values marked [APPROX] or "verify" should be checked before release: the 44Ti mass of SN 1987A,
   the Cioffi coefficient, the N_H/A_V ratio, GBM and survey thresholds, and the INTEGRAL end date.

---

## 13. Reference list (by section)

- Abbasi R. et al. (IceCube) 2011, A&A 535, A109, arXiv:1108.0171
- Abdikamalov E., Pagliaroli G., Radice D. 2020, in *Handbook of GW Astronomy*, arXiv:2010.04356
- Adams S. M. et al. 2013, ApJ 778, 164, arXiv:1306.0559
- Ahmad I. et al. 2006, PRC 74, 065803
- Al Kharusi S. et al. (SNEWS 2.0) 2021, NJP 23, 031201, arXiv:2011.00035
- Alekseev E. N. et al. 1987, JETP Lett. 45, 589
- Amati L. 2006, MNRAS 372, 233, arXiv:astro-ph/0601553
- Arnett W. D. 1982, ApJ 253, 785
- Arnett W. D., Fryer C., Matheson T. 2017, ApJ 846, 33, arXiv:1611.08746
- Baars J. W. M. et al. 1977, A&A 61, 99
- Bessell M. S., Castelli F., Plez B. 1998, A&A 333, 231
- Bietenholz M. F. et al. 2021, ApJ 908, 75, arXiv:2011.11737
- Bionta R. M. et al. 1987, PRL 58, 1494
- Cardelli J. A., Clayton G. C., Mathis J. S. 1989, ApJ 345, 245
- Chevalier R. A. 1998, ApJ 499, 810
- Chevalier R. A., Fransson C. 2006, ApJ 651, 381, arXiv:astro-ph/0607196
- Chevalier R. A., Li Z.-Y. 2000, ApJ 536, 195, arXiv:astro-ph/9908272
- Churazov E. et al. 2014, Nature 512, 406, arXiv:1405.3332
- Clocchiatti A., Wheeler J. C. 1997, ApJ 491, 375
- Diehl R. et al. 2014, Science 345, 1162, arXiv:1407.3061
- Dimmelmeier H. et al. 2008, PRD 78, 064056
- Dwarkadas V. V., Gruszko J. 2012, MNRAS 419, 1515; Dwarkadas V. V. 2014, MNRAS 440, 1917
- Faucher-Giguère C.-A., Kaspi V. M. 2006, ApJ 643, 332
- Filippenko A. V. 1997, ARA&A 35, 309
- Frail D. A. et al. 2001, ApJ 562, L55, arXiv:astro-ph/0102282
- Frank K. A. et al. 2016, ApJ 829, 40, arXiv:1608.02160
- Fransson C. et al. 2015, ApJL 806, L19
- Galama T. J. et al. 1998, Nature 395, 670, arXiv:astro-ph/9806175
- Gal-Yam A. 2017, *Handbook of Supernovae*, arXiv:1611.09353
- Goldberg J. A., Bildsten L., Paxton B. 2019, ApJ 879, 3, arXiv:1903.09114
- Gossan S. E. et al. 2016, PRD 93, 042002, arXiv:1511.02836
- Green D. A. 2024, Catalogue of Galactic SNRs (Oct 2024 version)
- Grefenstette B. W. et al. 2014, Nature 506, 339, arXiv:1403.4978
- Gutiérrez C. P. et al. 2017, ApJ 850, 89, arXiv:1709.02487
- Heinke C. O., Ho W. C. G. 2010, ApJL 719, L167
- Hirata K. et al. 1987, PRL 58, 1490
- Inserra C. et al. 2013, ApJ 770, 128, arXiv:1304.3320
- Janka H.-T. 2017, *Handbook of Supernovae*, arXiv:1702.08713
- Junde H. et al. 2011, Nucl. Data Sheets 112, 1513
- Kachelriess M. et al. 2005, PRD 71, 063003, arXiv:astro-ph/0412082
- Kasen D., Bildsten L. 2010, ApJ 717, 245, arXiv:0911.0680
- Kasen D., Woosley S. E. 2009, ApJ 703, 2205, arXiv:0910.1590
- Keil M. Th., Raffelt G. G., Janka H.-T. 2003, ApJ 590, 971
- Kistler M. D., Haxton W. C., Yüksel H. 2013, ApJ 778, 81, arXiv:1211.6770
- Kouveliotou C. et al. 1993, ApJ 413, L101
- Lattimer J. M., Prakash M. 2001, ApJ 550, 426
- Leahy D. A., Williams J. E. 2017, AJ 153, 239, arXiv:1607.06391
- Liu Y.-Q. et al. 2016, ApJ 827, 90, arXiv:1510.08049
- Lyman J. D., Bersier D., James P. A. 2014, MNRAS 437, 3848, arXiv:1311.1946
- Mamajek E. E. et al. 2015, IAU 2015 Resolutions B2/B3, arXiv:1510.06262, arXiv:1510.07674
- Matz S. M. et al. 1988, Nature 331, 416
- Modjaz M. et al. 2016, ApJ 832, 108, arXiv:1509.07124
- Morozova V. et al. 2018, ApJ 861, 10, arXiv:1801.01914
- Müller B., Janka H.-T., Marek A. 2013, ApJ 766, 43, arXiv:1210.6984
- Nadyozhin D. K. 1994, ApJS 92, 527
- Nakar E., Sari R. 2010, ApJ 725, 904, arXiv:1004.2496
- Nakazato K. et al. 2013, ApJS 205, 2, arXiv:1210.6841
- Nugent P. et al. 2006, ApJ 645, 841
- O'Connor E., Ott C. D. 2011, ApJ 730, 70, arXiv:1010.5550
- Pagliaroli G. et al. 2009, Astropart. Phys. 31, 163, arXiv:0810.0466
- Phillips M. M. et al. 1999, AJ 118, 1766
- Rabinak I., Waxman E. 2011, ApJ 728, 63
- Radice D. et al. 2019, ApJL 876, L9, arXiv:1812.07703
- Richardson D. et al. 2014, AJ 147, 118, arXiv:1403.5755
- Richers S. et al. 2017, PRD 95, 063019
- Roques J. P. et al. 2003, A&A 411, L91, arXiv:astro-ph/0310793
- Sapir N., Waxman E. 2017, ApJ 838, 130, arXiv:1607.03700
- Sari R., Piran T., Narayan R. 1998, ApJ 497, L17, arXiv:astro-ph/9712005
- Sari R., Piran T., Halpern J. P. 1999, ApJ 519, L17
- Shvartzvald Y. et al. 2024, ApJ 964, 74, arXiv:2304.14482
- Soderberg A. M. et al. 2008, Nature 453, 469, arXiv:0802.1712
- Srivastava V. et al. 2019, PRD 100, 043026, arXiv:1906.00084
- Stritzinger M., Leibundgut B. 2005, A&A 431, 423
- Sukhbold T. et al. 2016, ApJ 821, 38, arXiv:1510.04643
- Sutton P. J. 2013, arXiv:1304.0210
- Suwa Y. et al. 2019, ApJ 881, 139, arXiv:1904.09996
- Suwa Y. et al. 2025, Open J. Astrophys. 8, arXiv:2504.19510
- Szczepańczyk M. J. et al. 2021, PRD 104, 102002, arXiv:2104.06462
- Tauris T. M., Manchester R. N. 1998, MNRAS 298, 625
- Tomsick J. A. et al. 2023 (COSI), arXiv:2308.12362
- Truelove J. K., McKee C. F. 1999, ApJS 120, 299; erratum 2000, ApJS 128, 403
- Valenti S. et al. 2008, MNRAS 383, 1485
- Vissani F. 2015, J. Phys. G 42, 013001, arXiv:1409.4710
- Vogel P., Beacom J. F. 1999, PRD 60, 053003
- Wang X. et al. 2009, ApJ 699, L139
- Waxman E., Katz B. 2017, *Handbook of Supernovae*, arXiv:1607.01293
- Wheeler J. C., Johnson V., Clocchiatti A. 2015, MNRAS 450, 1295, arXiv:1411.5975
- Yakovlev D. G., Pethick C. J. 2004, ARA&A 42, 169, arXiv:astro-ph/0402143
