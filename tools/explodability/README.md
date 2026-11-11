# Explodability table: provenance

`src/SupernovaLab.Core/Data/Embedded/explodability-s16.csv` holds the per-mass outcomes of single stars at solar
metallicity used by `ExplodabilityTable` (outcomes.md §1.2–§1.4, §8.2 steps 4–5).

## Data path taken

**The published S16 per-model data** (task 2 brief, step 1, path 1). The band-rule fallback (path 2) was not needed.

Source: Sukhbold, Ertl, Woosley, Brown & Janka 2016, ApJ 821, 38 (arXiv:1510.04643), supplementary data in the
MPA Garching Core-Collapse Supernova Archive, doi:10.17617/1.b. Landing page:
https://wwwmpa.mpa-garching.mpg.de/ccsnarchive/data/SEWBJ_2015/index.html

Files used (downloaded 2026-11-11):

| File | URL | SHA-256 |
|---|---|---|
| `figure_N20_Z9.6.txt` | https://wwwmpa.mpa-garching.mpg.de/ccsnarchive/data/SEWBJ_2015/data/figure_7_8/figure_N20_Z9.6.txt | `8d497b8d44d47648fb30fa74ce9044b879848542a7b69d6c9be5a05af868417e` |
| `progenitor_properties_presn.txt` | https://wwwmpa.mpa-garching.mpg.de/ccsnarchive/data/SEWBJ_2015/data/progenitor_properties_presn.txt | `3e98729389aa0187ae6111b4e95c909bb2a51d21f4ab518664d34fc34f880529` |
| `progenitor_properties_bounce.txt` | https://wwwmpa.mpa-garching.mpg.de/ccsnarchive/data/SEWBJ_2015/data/progenitor_properties_bounce.txt | `a77b5b3ab9f7f0786b107f4159f8d175f860e93f2cb13a138b8ec841ab09f79a` |

`figure_N20_Z9.6.txt` is the archive's text version of S16 Fig. 7/8 for the "Z9.6 until and including 12 M☉, N20 for
higher masses" calibration, which is exactly the engine choice of outcomes §8.2 step 5. The P-HOTB per-engine files
(`data/explosion_results_PHOTB/results_N20`, `results_Z9.6`) were also downloaded as a cross-check; their "With
fallback" values match the figure file (e.g. 12.25 M☉: mass cut 1.558, M_Ni 0.053176, M_Tr 0.035361).

The table has the archive's 200 models: 9–13 M☉ in 0.25 M☉ steps, 13.1–30 M☉ in 0.1 M☉ steps, then 31, 32, 33, 35, 40,
45, 50, 55, 60, 70, 80, 100, 120 M☉. `Lookup` returns the nearest model; explodability is never interpolated (§1.3).

## Column mapping

| CSV column | From |
|---|---|
| `InitialMass` | `M_ZAMS` |
| `Explodes` | `false` on "failed SN" lines, else `true` |
| `Result` | failed → `DirectBlackHole`; exploded with `M_g,remnant` > 2.2 M☉ (NS maximum, outcomes §8.2 step 8) → `FallbackBlackHole`; else `NeutronStar`. The five fallback models (27.4, 29.0, 29.1, 29.2, 29.6 M☉) have M_fallback 2.4–5.5 M☉ and gravitational remnants 3.9–7.1 M☉; every NS has M_g ≤ 1.75 M☉. |
| `RemnantBaryonicMass` | exploded: `M_b,remnant` (includes fallback). Failed: the line's `M_He_core`, the S16 default BH mass (outcomes §1.5, §8.2 step 5). |
| `ExplosionEnergyErg` | `E_exp[B]` × 10⁵¹; 0 for failed models |
| `NickelMass` | `M_Ni + M_Tr`: the upper P-HOTB value, which is how outcomes §1.4 quotes S16 Table 7 (e.g. 10.0 M☉: 0.0206 + 0.0103 = 0.031). 0 for failed models; ~10⁻¹³ for fallback models. |
| `Compactness25` | `Compactnes_2.5` from `progenitor_properties_bounce.txt` (OO11 define ξ₂.₅ at bounce) |
| `PreSnMass`, `HeCoreMass`, `CoCoreMass` | `M_total_precollapse`, `M_He_core`, `M_CO_core` from `progenitor_properties_presn.txt` |
| `HydrogenEnvelopeMass` | `PreSnMass − HeCoreMass`: the H-rich envelope mass M_env of outcomes §1.3 (not the hydrogen mass) |
| `RadiusCm` | **Not in the archive files.** Interpolated linearly in log R between the outcomes §1.3 grid nodes (S16 Table 2, log R_f to 0.01 dex), then 10^logR. Above 30 M☉ every model sits on a grid node. |

## Steps to rebuild

Download into a new, empty folder, then run the script from this folder with that folder as an argument:

```sh
mkdir s16 && cd s16
B=https://wwwmpa.mpa-garching.mpg.de/ccsnarchive/data/SEWBJ_2015/data
curl -sSLO $B/figure_7_8/figure_N20_Z9.6.txt
curl -sSLO $B/progenitor_properties_presn.txt
curl -sSLO $B/progenitor_properties_bounce.txt
sha256sum *.txt   # compare with the table above
cd ..
python -I tools/explodability/build_explodability_csv.py s16 src/SupernovaLab.Core/Data/Embedded/explodability-s16.csv
```

The script checks that all three files list the same 200 models and writes the `# source:` line.

## Notes and differences from the outcomes.md band summary

- The per-model data is the authority; outcomes §1.4 gives only band ranges for N20. Some details differ from the
  prose bands: N20 fails over 21.8–25.1 M☉ (not exactly 22–25), the fallback BHs sit at 27.4 and 29.0–29.6 M☉, 15.0 and
  15.1 M☉ fail, and 60, 80, 100 and 120 M☉ explode while 70 M☉ fails.
- The 100 M☉ model leaves a 2.033 M☉ baryonic NS (1.752 M☉ gravitational), just above the 1.2–2.0 bound of the task
  brief's test. The value is kept as published; the test pins it.
- Per ruling R1 the app consults this table only for H-rich single stars inside 9–30 M☉ (outcomes §8.2 step 4); the
  rows above 30 M☉ are kept for completeness.
- The archive's presupernova masses differ slightly from outcomes §1.3 in places (e.g. 120 M☉: 5.997 vs 6.160 M☉).
