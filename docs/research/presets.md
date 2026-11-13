# Supernova Lab: research for real-supernova presets

Compiled 2026-11-11. Purpose: best-estimate parameters (with uncertainties and citations) for app presets.

## READ THIS FIRST: confidence tags

Web access in this session returned only abstracts, summaries and some full-text extractions. Every value carries a tag:

- **[V]** = checked this session against the paper's arXiv abstract/full text (or an extraction of it). Safe to use, subject to normal rounding.
- **[R]** = recalled from the literature by the researcher; the cited paper exists, but the number was NOT re-opened this session. Verify against the paper before hard-coding anything the app's model is tested against.
- **[D]** = derived here by simple scaling or arithmetic (formula given). Not a published value.
- "unknown" = not known, or not found. Nothing has been invented.

**Task 13 check (2026-11-11):** the values the presets use were re-checked against the papers in `presets-verification.md`; where the two differ (e.g. SN 1987A peak V 2.975, Crab P 33.39 ms, Kepler 5.1 kpc, SN 2008D author sets), that file wins.

Where papers disagree, the spread is given rather than one number. Real ranges for explosion energy, Ni mass and ejecta mass are model dependent (spherical vs aspherical, different light-curve codes); a preset should pick one author's self-consistent set, not mix sources.

Units: E in foe (1 foe = 1e51 erg). Positions J2000.

---

## Quick summary table

| # | Object | Type | Distance | Date | M_Ni (Msun) | E (foe) | Remnant |
|---|---|---|---|---|---|---|---|
| 1 | SN 1987A | II-pec (BSG) | 51.4 +/- 1.2 kpc (ring); 49.6 +/- 0.6 kpc (LMC) | 1987 Feb 23 | ~0.07 | ~1-1.5 [R] | NS (indirect JWST evidence, 2024) |
| 2 | SN 1054 / Crab | II (low energy; EC-SN?) | 2.0 +/- 0.5 kpc | 1054 Jul 4 (Chinese records) | unknown | ~0.01-0.1 [R] | Pulsar, P = 33 ms |
| 3 | Cas A | IIb | 3.4 (+0.3/-0.1) kpc | ~1671-1681 | ~0.06 [R] (Fe); 44Ti ~1.25e-4 [V-ish] | ~2 [R] | CCO (NS, C atmosphere) |
| 4 | SN 2011fe | Ia | 6.4 Mpc | 2011 Aug 23.7 | ~0.4-0.55 [R] | n/a (Ia) | none (total disruption) |
| 5 | SN 2014J | Ia | ~3.5 Mpc | 2014 Jan 14.75 | 0.62 +/- 0.13 [V] / ~0.6 | n/a | none |
| 6 | SN 1993J | IIb | 3.63 +/- 0.34 Mpc | 1993 Mar 27.5 | ~0.07-0.1 [R] | ~1 [R] | unknown |
| 7 | SN 1998bw | Ic-BL + GRB | z = 0.0085 (~38 Mpc) | 1998 Apr 25.9 | 0.4-0.7 | 2-5 x10 (spherical-equivalent) | unknown (BH/magnetar) |
| 8 | SN 2008D | Ib | ~27 Mpc | 2008 Jan 9.5 | ~0.07 [R] | ~6 [R] | unknown |
| 9 | SN 2023ixf | II (II-L/P) | 6.85 +/- 0.15 Mpc [R] | 2023 May ~18.7 | unknown | unknown | unknown (none seen yet) |
| 10 | SN 2015bn | SLSN-I | z = 0.1136 [V] | peak MJD 57102 [V] | n/a (magnetar) | ~1 [V] | magnetar (model) |
| 11 | Tycho 1572, Kepler 1604 | Ia | 2.3-4; ~5 kpc | 1572 Nov 6; 1604 Oct 9 | - | - | none |
| 12 | N6946-BH1 | failed SN? (disputed) | 7.7 Mpc [R] | outburst 2009 | - | - | BH? or merger product |
| 13 | Betelgeuse | future II-P/L | 168 (+27/-15) pc ... 222 (+48/-34) pc | not yet | - | - | - |

---

## 1. SN 1987A (LMC)

**Host / distance / position.** Large Magellanic Cloud. Distance 51.4 +/- 1.2 kpc from the ring geometry (Panagia 2005, ASP Conf. Ser. 342; Panagia et al. 1991, ApJL 380, L23) [R]. Independent LMC geometric distance 49.59 +/- 0.09 (stat) +/- 0.54 (sys) kpc from eclipsing binaries (Pietrzynski et al. 2019, Nature 567, 200) [R]. Use 51.4 +/- 1.2 kpc (SN-specific) or 49.6 kpc; they differ by 3.5%. RA 05h35m28.0s, Dec -69d16m11s [R].

**Dates.** Neutrino burst 1987 Feb 23, ~07:35 UT. Earliest optical photographs about 3 h later (Feb 23.44 UT, ~6th mag); announced discovery Feb 24 (Shelton; Duhalde; Jones) [R].

**Type.** II-pec (peculiar; slow rise, no plateau) [R].

**Progenitor.** Sk -69 202, a B3 I blue supergiant (BSG), L ~1e5 Lsun, Teff ~16,000 K, R ~40-50 Rsun (~3e12 cm) (Woosley 1988 ApJ 330, 218; Arnett et al. 1989 ARA&A 27, 629) [R]. Initial mass ~18-20 Msun (same refs; Smartt 2009 ARA&A 47, 63 for context) [R]. Binary: the BSG state and the triple-ring nebula are best explained by a merger of a ~15 + ~5 Msun binary ~20,000 yr before explosion (Podsiadlowski 1992 PASP 104, 717; Morris & Podsiadlowski 2007 Science 315, 1103) [R]. Merger is the leading but not unique explanation.

**Explosion.** E_kin ~1.1-1.5 foe, ejecta ~14-15 Msun (hydro-model dependent, e.g. Utrobin & Chugai; Blinnikov et al. 2000 ApJ 532, 1132) [R: verify, spread is real]. 56Ni ~0.07 Msun (Arnett et al. 1989; Fransson & Kozma 2002 New Astron. Rev. 46, 487) [R].

**Light curve.** Peak at ~85-90 d after explosion (~1987 May 20), V ~2.9 mag apparent; M_bol peak ~ -15.5 [R]. Rise time ~85 d. No plateau (slow, powered by Ni/Co decay and H recombination after a short cooling peak).

**Remnant.** Neutron star, not directly detected as a pulsar for 35+ years. 2024: JWST NIRSpec/MIRI found narrow, spatially unresolved, blueshifted [Ar II], [Ar VI], [S III]/[S IV] lines near the centre of the ejecta, best explained by ionisation from a cooling neutron star or a pulsar wind nebula; blueshift may indicate a natal kick (Fransson et al. 2024, Science 383, 898; arXiv:2403.04386) [V]. Earlier hints: ALMA "blob" (Cigan et al. 2019 ApJ 886, 51) and X-ray/ALMA limits (Alp et al. 2018 ApJ 864, 174; Page et al. 2020 ApJ 898, 125) [R]. NS mass and spin: unknown. Pulsar pulsations: not detected.

**Observed by instrument.**
- Neutrinos (first extragalactic and only core-collapse neutrinos ever detected): Kamiokande-II 11 events over ~13 s, E ~6-36 MeV (Hirata et al. 1987, PRL 58, 1490); IMB 8 events over ~6 s, E ~20-40 MeV (Bionta et al. 1987, PRL 58, 1494); Baksan 5 events (Alekseev et al. 1987, JETP Lett. 45, 589). Total 24. (Mont Blanc LSD reported 5 events ~5 h earlier; usually considered unrelated/disputed.) All [R]; the "~20 events within 13 s" total for the two water Cherenkov detectors is consistent with the Mirizzi et al. 2016 review (arXiv:1508.00785) [V]. Inferred binding energy ~3e53 erg (Loredo & Lamb 2002, PRD 65, 063002) [R].
- Gravitational waves: none (no detectors with the needed sensitivity existed). Claimed coincident bar signals are not accepted.
- Shock breakout: not observed (the neutrino burst preceded the earliest photos; breakout was effectively missed).
- Optical: complete light curve and spectra from day 0; BSG progenitor identified in archival plates.
- Gamma-ray lines: 56Co 847 and 1238 keV detected (SMM: Matz et al. 1988, Nature 331, 416; also balloon experiments); 44Ti 67.9/78 keV later (NuSTAR: Boggs et al. 2015, Science 348, 670) [R].
- Radio: fading emission 1987, re-brightened from ~1990 as ejecta hit the ring. X-ray: hard X-rays from ~1987 Aug (Ginga, Mir-Kvant), soft X-rays brightening from 1991 (ROSAT, Chandra) [R].
- Ring: equatorial ring, radius ~0.2 pc, about 20,000 yr old, expanding at ~10 km/s (Panagia 1991; Burrows et al. 1995) [R]. Ejecta/reverse shock began hitting it ~1995, "hot spots" lit up around the ring from ~2000-2004 (Pun et al. 2002; Lawrence 2000) [R]. The ring interaction peaked ~2010-2012 and has been fading since ~2020 as the shock leaves the ring (Larsson et al. 2019, ApJ 886, 147) [R].

**Tooltip facts.** "Neutrinos from SN 1987A reached Earth about 3 hours before the first light, and only 24 of them were caught by three detectors; they are still the only supernova neutrinos ever seen." "In 2024 JWST found chemical fingerprints of a hidden neutron star, 37 years after the blast."

---

## 2. SN 1054 / Crab Nebula (M1, NGC 1952)

**Position / distance.** Taurus. Crab pulsar RA 05h34m31.97s, Dec +22d00m52.1s [R]. Distance 2.0 +/- 0.5 kpc (Trimble 1968, ApJ 73, 535; Kaplan et al. 2008, ApJ 677, 1201) [R].

**Dates / records.** First recorded as a "guest star" in Chinese records on 1054 Jul 4 (Chinese astronomers of the Song dynasty); visible in daylight for 23 days and at night until 1056 Apr 6, **642 days** after 1054 Jul 4 (Julian calendar, 1056 a leap year) [D] (Clark & Stephenson 1977, The Historical Supernovae; Stephenson & Green 2002) [R]. *Correction (Task 12 fix round 1): this file first said "653 days", which does not match its own two dates; the authors' 653 days may count in the Chinese calendar or from a different start, so the Tour does not use either number. See scientific-inconsistencies.md #50.* Possible Japanese and Arabic records; no secure European record.

**Peak brightness.** Reinterpretation of the records gives peak apparent magnitude roughly -4 to -7 (brighter than Venus), uncertain (Collins, Claspy & Martin 1999, PASP 111, 871) [R]. Treat the number as +/-1.5 mag.

**Type.** Under-luminous, low-energy Type II; no secure spectrum. Interpreted as a low-energy electron-capture/ONeMg-core collapse (Nomoto 1982, Nature 299, 803; Nomoto et al. 1982) with CSM interaction (Smith 2013, MNRAS 434, 102) or a faint Type II-P (Tominaga, Blinnikov & Nomoto 2013, ApJL 771, L12) [R].

**Progenitor.** Initial mass debate: ~8-10 Msun (super-AGB / electron-capture; Nomoto 1982; Tominaga 2013; Smith 2013 ~8-10) vs. up to ~9-13 Msun from nebular abundances (MacAlpine & Satterfield 2008, AJ 136, 2152) [R]. Type: RSG/super-AGB. Binary: no evidence. Radius: unknown. Pre-SN mass-loss envelope: nebula ejecta mass 4.6 +/- 1.8 Msun in filaments (Fesen, Shull & Hurford 1997, AJ 113, 354) [R]. Explosion energy low: ~1e49-1e50 erg [R] (Yang & Chevalier 2015, ApJ 798, 6) -- unknown within a factor of ~10. 56Ni: unknown.

**Remnant.** Crab pulsar PSR B0531+21: P = 33.5 ms (~33.6), Pdot ~4.2e-13 s/s, surface B ~3.8e12 G (ATNF Pulsar Catalogue, Manchester et al. 2005, AJ 129, 1993), spin-down luminosity ~4.5e38 erg/s, characteristic age ~1,260 yr vs. true age ~970 yr [R]. Discovered 1968 (Staelin & Reifenstein 1968, Science 162, 1481) [R]. Neutron-star mass: unknown. Nebula: pulsar wind nebula, ~3 pc radius, expanding at ~1,500 km/s (Trimble 1968; Hester 2008, ARA&A 46, 127) [R].

**Observed.** Optical (1054, historical only), radio-to-TeV nebula today. Gamma-ray flares from the nebula (Tavani et al. 2011, Science 331, 736; Abdo et al. 2011, Science 331, 739) [R]. Photons to ~1 PeV (LHAASO 2021, Nature 594, 33) [R]. No neutrino/GW/breakout data (pre-modern).

**Tooltip facts.** "The Crab pulsar spins 30 times a second and is still releasing ~100,000 times the Sun's power." "The explosion was bright enough to be seen in daylight for 23 days."

---

## 3. Cassiopeia A

**Position / distance.** RA 23h23m24s, Dec +58d48.9m (remnant centre) [R]. Distance 3.4 (+0.3/-0.1) kpc (Reed et al. 1995, ApJ 440, 706) [R]. Remnant diameter ~5 arcmin (~5 pc).

**Date.** Expansion age ~1671 +/- ? (Fesen et al. 2006, ApJ 645, 283); proper-motion age 1681 +/- 19 (Thorstensen et al. 2001, AJ 122, 297) [R]. Possible naked-eye sighting by John Flamsteed on 1680 Aug 16 ("3 Cas", mag ~6; Ashworth 1980, J. Hist. Astron. 11, 1) is uncertain; no other sighting recorded, likely due to dust extinction (A_V ~5 mag) [R]. Say "around 1680 (1671-1681), perhaps seen once".

**Type.** IIb, from spectra of its light echoes (Krause et al. 2008, Science 320, 1195; Rest et al. 2008, ApJ 681, L81) [R: widely cited; "optical light echo spectra indicate Type IIb" supported by secondary summary seen this session].

**Progenitor.** Initial mass ~15-25 Msun (Young et al. 2006, ApJ 640, 891), possibly a binary-stripped star (Krause 2008; Young 2006; Hirschi et al. 2009, A&A 502, 925) [R]. Pre-SN envelope mostly lost; type RSG -> stripped (YSG?) ; radius unknown. Ejecta mass ~2-4 Msun [R: Willingale 2003]. E_kin ~2 foe (Laming & Hwang 2003, ApJ 597, 347) [R]. 56Ni ~0.06 Msun [R; Eriksen 2009]. Spread large.

**44Ti.** NuSTAR mapping of the 67.9 keV and 78.4 keV lines (and 1.157 MeV earlier): clumpy, centrally located, not co-located with Fe; total 44Ti mass (1.25 +/- 0.3) x 10^-4 Msun (Grefenstette et al. 2014, Nature 506, 339); asymmetry confirmed with 2.4 Ms of data (Grefenstette et al. 2017, ApJ 834, 19) [R/search-confirmed qualitatively]. First 44Ti line: COMPTEL (Iyudin et al. 1994, A&A 284, L1) [R]. The NuSTAR result supports a large-scale, non-spherical ("sloshing", SASI-like) explosion.

**Remnant.** Central compact object found by Chandra in first-light imaging 1999 (Tananbaum 1999) [R]. It has no detected pulsations; X-ray spectrum fits a carbon atmosphere on a neutron star with ~10-15 km radius (Ho & Heinke 2009, Nature 462, 71) [R; search summary this session: carbon atmosphere on a NS-sized object]. Cooling: surface temperature fell ~4% in 10 yr, interpreted as neutron superfluidity (Heinke & Ho 2010, ApJL 719, L167; Page et al. 2011, PRL 106, 081101; Shternin et al. 2011) [R]. Mass: unknown.

**Observed.** Optical echoes; X-ray (Chandra, 44Ti NuSTAR), IR (JWST 2023 imaging, "Green Monster"), radio (brightest radio source in sky outside Sun, historically) [R]. No neutrinos/GW (not detectable).

**Tooltip facts.** "Cas A's light echoes let astronomers take a spectrum of the explosion itself, 330 years later." "NuSTAR saw radioactive titanium lumps that show the blast was lopsided."

---

## 4. SN 2011fe (M101, Type Ia)

**Position / distance.** RA 14h03m05.81s, Dec +54d16m25.4s [R]. M101 at 6.4 Mpc (Nugent et al. 2011, Nature 480, 344; arXiv:1110.6201) [V]; closest SN Ia in 25 years [V]. Uncertainty ~ +/-0.5 Mpc (Cepheid/other, e.g. Shappee & Stanek 2011) [R].

**Date.** Discovered by PTF 2011 Aug 24.17 UT, caught ~11 h after first light; explosion estimated Aug 23.7 UT [R]. Rise to B-max ~17.6 d [R]; B-max ~Sep 10.

**Type.** Ia normal (spectroscopically "Branch core-normal") with very low host extinction [R].

**Peak.** B ~9.9-10.0 mag apparent; absolute M_B ~ -19.0 to -19.2 (mu ~29.0) [R] (Richmond & Smith 2012; Pereira et al. 2013, A&A 554, A27).

**Progenitor.** Carbon-oxygen white dwarf; companion most likely a main-sequence star (no early shock), luminous red giants and most He-star donors ruled out [V: Nugent 2011]. Pre-explosion HST/Spitzer: progenitor 10-100x fainter than previous SN Ia limits; any evolved red companion had initial mass <3.5 Msun; favours double-degenerate or subgiant/MS donor (Li et al. 2011, Nature 480, 348; arXiv:1109.1593) [V]. Radius of exploding star R < 0.02 Rsun (Bloom et al. 2012, ApJL 744, L17); companion R < 0.1 Rsun if shock-heating applies [R: from Nugent/Bloom]. Radio/X-ray: no CSM, Mdot < ~1e-9 Msun/yr (Chomiuk et al. 2012, ApJ 750, 164; Horesh et al. 2012, ApJ 746, 21) [R].

**Explosion parameters.** 56Ni ~0.53 +/- 0.11 Msun (Pereira et al. 2013) [R]; other estimates 0.4-0.55 (Mazzali et al. 2014, MNRAS 439, 1959: ejecta ~1.1-1.4 Msun) [R, verify]. E_kin ~1.1-1.3 foe [R]. Total disruption, no remnant.

**Observed.** Very early optical/UV with t^2 rise (Nugent 2011); X-ray/radio non-detections; INTEGRAL 56Co line non-detections (Isern et al. 2013, A&A 552, A97) [R]; no neutrinos/GW expected.

**Tooltip.** "Caught within a day of exploding: the best-observed Type Ia ever." "Pre-explosion images showed that the star that blew up was a white dwarf too faint to see, not a giant."

---

## 5. SN 2014J (M82, Type Ia)

**Position / distance.** RA 09h55m42.14s, Dec +69d40m26.0s [R]. M82 ~3.5 Mpc (TRGB: Dalcanton et al. 2009, ApJS 183, 67: 3.53 Mpc; Foley et al. 2014 used 3.3) [R]; uncertainty ~0.3 Mpc [R].

**Date.** Discovered 2014 Jan 21.8 UT by S. Fossey (UCL) and students (CBET 3792) [R]; explosion ~Jan 14.75 UT (Zheng et al. 2014, ApJL 792, L11) [R]; B-max ~2014 Feb 1.7 [R].

**Type.** Ia, slightly peculiar (high-velocity features; decline rate dm15(B) ~1.1) [R].

**Extinction.** Heavy: E(B-V) ~1.2-1.3, total A_V ~1.7-2, with unusually low R_V ~1.4 +/- 0.1 (Goobar et al. 2014, ApJL 784, L12; Amanullah et al. 2014, ApJL 788, L21; Foley et al. 2014, ApJ 792, 29) [R]. Consequently B ~12 mag and V ~10.6 mag at max (apparent, heavily reddened) [R]; corrected M_B ~ -19.2 [R].

**Gamma-ray lines.** INTEGRAL/SPI detected 56Co lines at 847 and 1238 keV and a 200-400 keV continuum from ~50-100 d after explosion; mass-weighted expansion velocity ~10,000 +/- 3,000 km/s; 56Ni mass 0.6 +/- 0.1 Msun (Churazov et al. 2014, Nature 512, 406; arXiv:1405.3332) [V]; a separate INTEGRAL detection of early 56Ni lines at 158 and 812 keV (Diehl et al. 2014, Science 345, 1162) implied a small amount of 56Ni near the surface (~0.03 Msun, [R] not verified) -- which was disputed. First Type Ia gamma-ray lines ever measured.

**Progenitor.** No progenitor in HST pre-explosion images; excludes luminous RG and most SG donors (Kelly et al. 2014, ApJ 790, 3) [R]; X-ray and radio non-detections Mdot < ~1e-9 Msun/yr (Margutti et al. 2014, ApJ 790, 52; Perez-Torres et al. 2014, ApJ 792, 38) [R].

**Tooltip.** "Closest Type Ia in 42 years; its gamma rays directly showed the radioactive nickel-cobalt decay powering the glow." "Dust in M82 dimmed it by ~2 magnitudes and made the extinction law unusual."

---

## 6. SN 1993J (M81, Type IIb)

**Position / distance.** RA 09h55m24.77s, Dec +69d01m13.7s [R]. 3.63 +/- 0.34 Mpc (HST Cepheids, Freedman et al. 1994, Nature 371, 757) [R]; ~3.6 Mpc in Fox et al. 2014 [V via search summary].

**Date.** Discovered 1993 Mar 28.9 UT (F. Garcia Diaz); explosion ~Mar 27.5 [R].

**Type.** IIb: early H-alpha, becoming Ib-like within weeks (Filippenko et al. 1993) [R]. Light curve double-peaked: early cooling peak (V~12 within ~3 d), then Ni-powered peak V ~10.8 at ~20 d [R]; M_V ~ -17 at second peak [R, A_V ~0.5].

**Progenitor and companion.** Pre-explosion K0 I supergiant ~13-22 Msun (Aldering et al. 1994, AJ 107, 662; Podsiadlowski et al. 1993, Nature 364, 509; Nomoto et al. 1993, Nature 364, 507) [R]. Binary: Maund et al. 2004 (Nature 427, 129; arXiv:astro-ph/0401090) detected spectral features of a hot, massive companion about 10 yr after explosion (primary ~15 Msun, mass range quoted 13-20); a B-star companion also reported later (Fox et al. 2014, ApJ 790?, arXiv:1405.4863) with HST COS/WFC3 in 2012 -- but continuum detection unconfirmed and line-of-sight coincidence not excluded [V via ar5iv summary]. Models: donor lost ~8 Msun of envelope, exploding at ~5.4 Msun; companion up to ~22 Msun [V via search summary]. Radius of progenitor ~ 600 Rsun (RSG) [R].

**Energies.** E_kin ~1 foe; 56Ni ~0.07-0.1 Msun [R: Woosley et al. 1994, ApJ 429, 300]. Unknown precisely.

**Remnant.** Unknown (no compact object detected). Observed in radio VLBI (Marcaide et al. 1995, Science 270, 1475; Bartel et al. 2002, ApJ 581, 404): the first direct image of a supernova shell expanding, free-expansion becoming decelerating [R]; X-rays (ROSAT: Zimmermann 1994, Nature 367, 621) [R].

**Tooltip.** "One of the first supernovae to show that stars can be stripped by a companion; the companion was seen a decade later." "VLLBI radio movie shows the shell expanding."

---

## 7. SN 1998bw / GRB 980425

**Host / distance / position.** ESO 184-G82 (barred spiral), z = 0.0085, ~38 Mpc (Galama et al. 1998, Nature 395, 670) [R]. RA 19h35m03.2s, Dec -52d50m46s [R].

**Date.** GRB 980425 on 1998 Apr 25.909 UT (BeppoSAX GRBM/WFC, BATSE); SN found 1998 Apr 26 within the WFC error box [R].

**Type.** Ic-BL (broad-line; Patat et al. 2001, ApJ 555, 900) [R]; the first SN-GRB association.

**GRB.** Low-luminosity GRB: duration ~35 s (BeppoSAX); isotropic equivalent gamma-ray energy ~7.2-8e47 erg (Galama 1998 ~8e47 [R]; Iwamoto et al. 1998 used 7.2e47 from BATSE fluence 4.4e-6 erg/cm^2 [V via search summary]); ~1e4 times fainter than cosmological GRBs. Radio afterglow very bright and indicates mildly relativistic ejecta (Kulkarni et al. 1998, Nature 395, 663: E ~1e50 erg in Gamma beta ~1-2 ejecta) [R]. X-ray counterpart (Pian et al. 2000, ApJ 536, 778) [R]. Soderberg et al. 2004 (Nature 430, 648) place it in a population of low-luminosity GRBs [R].

**Explosion.** E_kin(spherical equivalent) 3e52 erg with 12.4 Msun CO star and 0.7 Msun 56Ni (Iwamoto et al. 1998, Nature 395, 672; arXiv:astro-ph/9806382) [V via search summary]. Other models: 5e52 erg and 0.4 Msun Ni (Nakamura et al. 2001, ApJ 550, 991); aspherical: 2e51 erg, 0.2-0.5 Msun Ni (Hoflich et al. 1999, ApJ 521, 179; Maeda et al. 2006) [V via search summary for Nakamura/Hoflich]. Ejecta mass ~10 Msun, progenitor CO core ~13 Msun, ZAMS ~40 Msun [R: Iwamoto/Nakamura/Mazzali]. Range of 56Ni: 0.2-0.7 Msun, depends on asphericity.

**Peak.** Peak at ~16 d after GRB (~mid-May 1998) V ~13.7 mag apparent, M_V ~ -19 [R: Galama 1998; Patat 2001; Woosley 1999] (large-ish uncertainty). Rise ~15-17 d.

**Remnant.** Unknown (BH or magnetar). Spherical energy exceeds normal SN by ~30x.

**Tooltip.** "First proof that gamma-ray bursts come from supernovae, though this GRB was a thousand times weaker than usual." "The explosion was roughly 10 times more energetic than a normal one."

---

## 8. SN 2008D (Type Ib, NGC 2770)

**Host / distance / position.** NGC 2770, Lynx, ~27 Mpc (z = 0.0065) (Soderberg et al. 2008, Nature 453, 469; arXiv:0802.1712) [R]; "90 million light-years" [V via press summary]. RA 09h09m30.6s, Dec +33d08m20s [R].

**Date.** Swift XRT was observing SN 2007uy in the same galaxy and caught an X-ray transient (XRT 080109) on 2008 Jan 9.5 UT, lasting ~ 400-530 s [V partially: press confirms 530 s of X-ray]. Optical SN appeared days later (peak ~20 d later).

**X-ray shock breakout.** L_X peak ~6e43 erg/s; total E_X ~2e46 erg; interpreted as shock breakout from a compact (Wolf-Rayet-like) star, R ~1e11 cm? (Soderberg 2008) [R: not verified numerically]. Alternative: breakout through a dense wind (Chevalier & Fransson 2008, ApJL 683, L135) [R]. Not a GRB; low-luminosity X-ray flash.

**Type.** Initially Ic-like with broad lines, evolving to Ib (He lines) within ~2 weeks (Modjaz et al. 2009, ApJ 702, 226; Mazzali et al. 2008, Science 321, 1185) [R].

**Explosion.** E_kin ~6 foe; Mej ~3-5 Msun; 56Ni ~0.07 Msun; progenitor ZAMS ~20-30 Msun, He star ~6-8 Msun pre-SN (Mazzali 2008; Tanaka et al. 2009) [R]. "More than 30 times the mass of the Sun" quoted in press release for the progenitor -- do not use; Mazzali is the paper source. Unknown whether binary.

**Peak.** M_V ~ -17 [R: unverified]. Remnant unknown. Neutrinos/GW none.

**Tooltip.** "The first time a supernova was caught in the act of exploding, by accident."

---

## 9. SN 2023ixf (M101)

**Position / distance.** RA 14h03m38.56s, Dec +54d18m41.9s [R]. M101 distance 6.85 +/- 0.15 Mpc (Riess et al. 2022, ApJ 934, L7, Cepheids) [R]; most SN papers adopt 6.85-6.9 Mpc [R].

**Date.** Discovered 2023 May 19.73 UT by Koichi Itagaki [R]; first light estimated May ~18.7 UT +/- ~0.1 d (Hosseinzadeh et al. 2023, ApJL 953, L16; Bostroem et al. 2023, ApJ 956, L5) [R: verify exact epoch]. Closest SN II since SN 2004dj (not closer than 2014J overall) [R].

**Type.** II: early "flash-ionisation" narrow lines (He II, H, C, N) indicating dense CSM, fading in ~1 week; light curve intermediate II-L/II-P with plateau ~ 80-90 d [R: low confidence on plateau length]. Peak apparent magnitude: V ~ 10.5-11 [R, low confidence]; absolute M_V ~ -18 [R, low confidence]. Rise time: ~ weeks; verify with Hosseinzadeh/Bostroem.

**Progenitor (pre-explosion data).** Luminous dusty RSG. Estimates differ:
- 17 +/- 4 Msun, log L/Lsun = 5.1 +/- 0.2, Teff ~3500 K (+800/-1400), pulsating with P ~1000 d, amplitude ~0.6 mag; mass-loss rate ~3e-5 to 3e-4 Msun/yr over the last 3-19 yr; no IR-bright outburst (Jencson et al. 2023, ApJL 952, L30; arXiv:2306.08678) [V].
- 16.2-17.4 Msun (SED), 17-19 Msun from local star-formation history (Niu et al. 2023, ApJL 955, L15; arXiv:2308.04677) [V via search summary].
- ~12 Msun (Pledger & Shara 2023, ApJL 953, L14, HST isochrones) [V via search summary].
- 14-20 Msun (Ransome et al.) [V via search summary]; ~18.2 Msun with episodic pre-SN mass loss (Keck AO, not attributed this session) [V via search summary]; 9-14 (Kilpatrick et al. 2023, ApJ 952, L23) [R]; ~20 +/- 4 Msun (Soraisam et al. 2023, ApJ 957, 64) [R]; 13.5-15 Msun from nebular/light-curve modelling [V via search summary]. Real spread 9-20 Msun.
- Binary: unknown. Radius: RSG, ~ order of 1000 Rsun [R: not verified].

**Early CSM interaction.** Dense confined CSM within ~1e15 cm, Mdot ~1e-3 to 1e-2 Msun/yr over final ~1 yr (Jacobson-Galan et al. 2023, ApJL 954, L42; Bostroem 2023; Hosseinzadeh 2023) [R]. NuSTAR X-ray: absorbed, NH ~1e24 cm^-2, Mdot ~1e-4 Msun/yr at larger radii (Grefenstette et al. 2023, ApJ 952, L3) [R]. Polarisation: asymmetric CSM (Vasylyev et al. 2023, ApJL 959, L26) [R].

**Other.** 56Ni mass, E_kin, M_ej: unknown to this research (published values exist but not retrieved). Remnant: unknown. No neutrino/GW signal (too far, no detector triggered).

**Tooltip.** "The progenitor was an enormous dusty red supergiant that was pulsating and puffing out gas shortly before it blew." "Caught within a day, so we could watch its star's own wind lit up by the blast."

---

## 10. Superluminous SN: SN 2015bn (chosen)

**Why 2015bn over 2006gy.** SN 2015bn (PS15ae) has a ~300-day multi-wavelength light curve (-50 to +250 d), spectroscopic and nebular-phase coverage, spectropolarimetry, and published magnetar fits (Nicholl et al. 2016, ApJ 826, 39). SN 2006gy is the older, poorly sampled, and its power source (CSM vs. pair-instability) is still disputed (Ofek 2007; Smith 2007) [R]. Choose 2015bn.

**Position / redshift.** RA 11h33m41.55s, Dec +00d43m33.4s [V]; z = 0.1136 [V] (d_L ~ 0.53 Gpc [D]); faint host (M_B ~ -16.0) [V].

**Dates.** r-band max at MJD 57102 (~2015 Apr 19) [V]. First CSS detection 88 d before peak (79 d rest frame); explosion at most ~92 d before max; magnetar-fit rise times 69 d (first point = precursor bump) or 85 d (smooth) [V].

**Type.** Type I SLSN (slowly declining, SLSN-I) [V].

**Peak.** M_U ~ -23.07 +/- 0.09 (Vega); M_g = -22.0 +/- 0.08 (AB); bolometric peak L = 2.3 +/- 0.4 x 10^44 erg/s [V]. Apparent: r ~16.7 at max (from a forum summary; treat as unverified) -- say "r ~ 16.7 [unverified]".

**Magnetar fit (kappa = 0.2 cm^2/g)** [V]:

| Case | M_ej (Msun) | P (ms) | B (1e14 G) | chi2_red |
|---|---|---|---|---|
| Rise 69 d (first detection = bump) | 8.4 | 2.1 | 0.9 | 0.31 |
| Rise 85 d (smooth) | 15.1 | 1.7 | 1.0 | 0.48 |

Spin-down timescale ~20 d; preferred progenitor stripped, >=10 Msun, E_kin ~1 foe [V, abstract]. Alternative: ~20 Msun of CSM interaction [V]. Nebular-phase fits (Nicholl et al. 2016, ApJL 828, L18; arXiv:1608.02995) list e.g. 8.3 Msun, 2.1 ms, 0.9e14 G and 7.4 Msun, 1.5 ms, 0.2e14 G [V via search summary: verify which analysis]. Model-dependent: a 2023 semi-analytic model paper (arXiv:2308.12997) reports strong discrepancies for most inferred parameters [V via search summary].

**Remnant.** Magnetar, in the model (not observed). 56Ni: not the power source; no value. Radio: deep limit rules out off-axis GRB [V]. Neutrinos/GW: none.

**Tooltip.** "A hundred times brighter than a normal supernova; the glow is thought to come from a newborn magnetar spinning ~500 times a second." "Its light curve shows 30-50 day wiggles that no one fully explains."

---

## 11. Tycho (SN 1572) and Kepler (SN 1604)

**Tycho's SN 1572 (B Cas, 3C 10).** Type Ia, confirmed from a light-echo spectrum (Krause et al. 2008, Nature 456, 617) [R]. RA 00h25m21s, Dec +64d09m [R]. Distance ~2.3-4 kpc (2.5-3 kpc favoured; Chevalier, Kirshner & Raymond 1980; Hayato et al. 2010) [R, range]. First seen 1572 Nov 6 (Schedar; Tycho Brahe noticed it Nov 11 and wrote *De nova stella*). Peaked ~ -4 mag (Venus-like), naked-eye visible ~16 months, visible in daylight near maximum [R]. Remnant: none (total disruption); candidate donor star Tycho G (Ruiz-Lapuente et al. 2004, Nature 431, 1069) disputed [R].

**Kepler's SN 1604 (SN Ophiuchi, G4.5+6.8).** Type Ia, likely with CSM (Reynolds et al. 2007, ApJ 668, L135; Katsuda et al. 2015, ApJ 808, 49) [R]. RA 17h30m41s, Dec -21d29m [R]. Distance ~5 kpc (4.8 +1.4/-1.1; Sankrit et al. 2005) [R]. First seen 1604 Oct 9; Kepler observed from Oct 17; peak ~ -2.5 to -3 mag (brighter than Jupiter after Venus), visible for ~12 months [R]. Last Galactic supernova seen with the naked eye (or last securely recorded) [R]. Remnant: none.

---

## 12. N6946-BH1 (failed-SN candidate in NGC 6946)

**Host / distance.** NGC 6946, ~7.7 Mpc [R].

**Original claim.** Gerke, Kochanek & Stanek 2015 (MNRAS 450, 3289) from the Large Binocular Telescope survey for failed supernovae: a ~25 Msun red supergiant had a weak optical outburst in 2009 and then vanished; HST showed no optical source afterward (Adams et al. 2017, MNRAS 468, 4968; arXiv:1609.01283: "The search for failed supernovae with the Large Binocular Telescope: confirmation of a disappearing star") [V: title/volume confirmed; ~25 Msun RSG and 2009 outburst confirmed]. Interpretation: collapse to a black hole with an ejected envelope producing a ~1e6 Lsun weak transient (Nadezhin 1980; Lovegrove & Woosley 2013) [R]. Follow-up: Basinger et al. 2021 (ApJ 909, 79; arXiv:2007.15658) "N6946-BH1, still no star" consistent with failed SN [V via search].

**Revisits.**
- Beasor, Hosseinzadeh, Smith, Davies, Jencson, Pearson & Sand, "JWST reveals a luminous infrared source at the position of the failed supernova candidate N6946-BH1", ApJ 2024 (arXiv:2309.16121, first posted Sep 2023, v2 Jan 2024): JWST NIRCam/MIRI 14 yr after; earlier pre/post imaging is a blend of at least 3 sources; total luminosity 13-25% of progenitor; IR resembles PAH-like emission; conclusion: consistent with a stellar merger, failed SN not excluded [V]. (Beasor 2023/24 not 2024 only.)
- Kochanek, Neustadt & Stanek (JWST/MIRI, ApJ 2024; arXiv:2310.01514): very red, dusty source with only ~10-15% of progenitor luminosity; silicate dust, large grains as predicted for a failed SN [V via search summary]; also LBT MIR counterpart paper.
- Later LBT/HST monitoring: SEDs inconsistent with surviving star behind wind/dust [V via search summary, source not named].
- Bottom line: unresolved; failed-SN (BH) vs. merger/obscured survivor. The preset should be labelled "candidate".

**Parameters.** Progenitor ~25 Msun RSG [V]; L ~ 1e5.? Lsun [unknown]; remnant: BH of ~ few Msun? unknown; explosion energy unknown (no SN seen); neutrinos/GW: nothing observed. X-ray limit consistent with no accretion [R].

**Tooltip.** "A giant star that seemed to vanish instead of exploding: either a black hole formed quietly or two stars merged and hid in dust. JWST can't yet tell."

---

## 13. Betelgeuse (future preset)

**Position / properties.** alpha Ori, RA 05h55m10.3s, Dec +07d24m25s [R]. Red supergiant, semi-regular variable (V ~0.0-1.6; 2019-20 Great Dimming reached ~1.6) [R].

**Distance and the debate.**
- 168 (+27/-15) pc, parallax 5.95 (+0.58/-0.85) mas (Joyce et al. 2020, ApJ 902, 63; arXiv:2006.09837) [V].
- 222 (+48/-34) pc (Harper et al. 2017, AJ 154, 11; radio + Hipparcos) [V via search summary: 222 +48/-34].
- Neuhauser et al. 2022 (MNRAS) discuss ~165 pc versus larger distances; the disagreement is unresolved [V via review summary].
- A conservative preset range: 168-222 pc; default ~200 pc.

**Mass / radius.**
- Present-day mass 16.5-19 Msun (Joyce 2020) [V]; ~20 (+5/-3) Msun (Dolan 2016, ApJ 819, 7) [V via search summary]; ZAMS 17-18 (Neuhauser 2022) [V via review summary]; 14-19 range commonly quoted [V via search summary, low quality].
- Radius 764 (+116/-62) Rsun (Joyce 2020) [V]; 640 Rsun from 2022 Great Dimming study [V via search summary, unattributed]; ~887 +/-203 Rsun (Harper 2017) [R]; 702-880 [secondary].
- Pulsation: fundamental ~400 d, first overtone 185 +/- 13.5 d [V]. Evolutionary state: core-helium-burning per Joyce 2020 [V]; Neuhauser prefer near base of RGB [V via review summary]. Time to explosion: ~1e5 yr order [R, not stated in the abstract fetched].
- Companion: a ~6-yr velocity period possibly from a low-mass companion (~1.5 Msun) proposed (MacLeod et al. 2024, arXiv:2409.11332) [V title/arXiv listing only; details unverified].

**Expected SN type.** Type II (II-P or II-L); H-rich, plateau ~100 d if II-P [R]. Predicted M_V at peak ~ -17 [R: typical].

**Neutrino predictions.**
- Core-collapse burst: scale the 10 kpc counts by (10 kpc/d)^2. At 200 pc: (50)^2 = 2,500x; at 168 pc 3,540x; at 222 pc 2,030x [D]. For ~4,000-8,000 Super-K events at 10 kpc [see table below] gives roughly 1e7 events in Super-K (order of 8e6 to 3e7 across the distance range) [D]. Detector saturates; deadtime/pile-up would affect real counting.
- Pre-supernova neutrinos (Mukhopadhyay et al. 2020, arXiv:2004.02045): for a ~15 Msun star at 0.2 kpc, JUNO would see ~93 events (4 h), ~193 (1 h), ~314 (2 min before collapse) -- with 15, 23, 25 of those background; ">~100 events" about 4 h before and ~200 realistic; at distance 0.222 +/- 0.040 kpc in Table 6 [V]. No Super-K or KamLAND count given in that paper [V]. Super-K (Gd-loaded) and KamLAND operate a joint pre-supernova alert (arXiv:2404.09920) [V via search summary]. An older estimate (arXiv:astro-ph/0405006) gave ~1000/day (Si burning) in Super-K with neutron tagging [V via search summary; pre-Gd, treat as rough].

**Predicted brightness if it exploded today [D].** Distance modulus mu = 5 log10(d/10 pc): 168 pc -> 6.1; 200 pc -> 6.5; 222 pc -> 6.7. With M_V ~ -17 (typical II-P peak) and A_V ~0.2-0.5, peak m_V ~ -10.5 to -10.0, i.e. comparable to a quarter-to-half Moon (full Moon is -12.7), easily visible in daytime. Plateau ~ -9 to -10 for ~3 months. Low-energy/faint II-P (M_V ~ -15) would be ~ -8.5 (still brighter than Venus). Uncertainty +/- ~1.5 mag in M_V.

**Tooltip.** "If Betelgeuse exploded tonight we'd see it in daylight, and the neutrino detectors would be flooded with millions of events. It is probably still ~100,000 years from exploding."

---

## Table: "If it happened in the Milky Way at 10 kpc today"

| Messenger | Expected | Basis and citation |
|---|---|---|
| Neutrinos, Super-Kamiokande (22.5 kt fiducial; 32 kt in some counts) | ~4,000 to ~8,000 events in ~10-15 s (model dependent: progenitor, EOS, mixing). Cross-check: scale from Kamiokande-II seeing 11 events from 51.4 kpc with 2.14 kt: 11 x (22.5/2.14) x (51.4/10)^2 = ~3,000 [D]; real count is larger because modern thresholds are lower and the total is for 3e53 erg | Scholberg 2012, Ann. Rev. Nucl. Part. Sci. 62, 81 (arXiv:1205.6003) and Abe et al. 2016, Astropart. Phys. 81, 39 (arXiv:1601.04778) (counts [R]; pages fetched did not show the table); Mirizzi et al. 2016, Riv. Nuovo Cim. 39, 1 (arXiv:1508.00785) [V: review exists; counts not seen] |
| Neutrinos, other | IceCube: millions of photons in the glacial ice give a rate increase of ~1e6 over noise [R]; JUNO (20 kt LS) ~5,000 IBD [R]; DUNE (40 kt LAr) ~3,000 nu_e events [R] | Mirizzi 2016; Scholberg 2012 (all [R]) |
| Gravitational waves | Marginal to detectable with LIGO-Virgo-KAGRA depending on model: non-rotating neutrino-driven explosions emit ~1e-9 to 1e-8 Msun c^2 and have a horizon of order a few to ~10 kpc; rapidly rotating or strongly asymmetric models are detectable to tens of kpc. A neutrino-triggered search would be decisive [R]. Be explicit in the app: not guaranteed | Abbott et al. 2020, PRD 101, 084002 and Szczepanczyk et al. 2021, PRD 104, 102002 for search sensitivity [R]; Abdikamalov, Pagliaroli & Radice 2020 review (arXiv:2010.04356) [R] |
| Naked-eye optical | Depends on line of sight. An unobscured II-P would be m_V ~ +13 at 10 kpc [D] (M_V ~ -17 + mu 15); Galactic-plane dust (A_V ~ 2 to > 30 mag) usually makes it fainter. Adams et al. 2013 (ApJ 778, 164; arXiv:1306.0559) conclude most Galactic SNe would not be naked-eye in V; however many would be bright in the near-IR [R: conclusion recalled, not verified]. Tell the app: "usually NOT naked-eye at 10 kpc" |
| Shock breakout, X-ray/gamma | Detectable: Swift/Fermi/Einstein Probe would see a breakout flash lasting seconds to minutes; neutrinos give a ~hours-early warning (SNEWS) [R] | Soderberg 2008 (SN 2008D) [R]; SNEWS 2.0 (Al Kharusi et al. 2021, New J. Phys. 23, 031201) [R] |

---

## Cross-cutting caveats for the preset author

1. Nearly all parameters beyond the neutrino counts, the SN 2014J/2015bn/2023ixf progenitor numbers, and the Betelgeuse Joyce numbers are tagged [R]; the first-pass verification budget did not cover every paper. Re-verify [R] items that tests depend on.
2. Explosion energy, ejecta mass, and Ni mass are model-dependent; multiple self-consistent literature sets exist (see SN 1998bw, SN 1987A, SN 2023ixf). Preset one set, document the source, show the range.
3. Distances: use the stated ranges (Betelgeuse 168-222 pc; SN 1987A 49.6-51.4 kpc; M82 3.3-3.5 Mpc). Brightness and neutrino predictions scale with d^-2.
4. Items that are disputed or candidates: N6946-BH1 (failed SN vs merger), SN 1054 progenitor (EC-SN), Cas A date (1671-1681), SN 1987A NS (indirect evidence), SN 2014J early 56Ni lines.
