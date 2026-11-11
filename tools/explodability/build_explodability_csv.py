"""Build src/SupernovaLab.Core/Data/Embedded/explodability-s16.csv from the S16 per-model data.

Usage (see README.md in this folder for the download step):
    python -I build_explodability_csv.py <download_dir> <output_csv>

<download_dir> must hold, unchanged, these files from the MPA Garching CCSN archive (doi:10.17617/1.b):
    figure_N20_Z9.6.txt               explosion results, Z9.6 engine up to 12 Msun, N20 above
    progenitor_properties_presn.txt   presupernova masses
    progenitor_properties_bounce.txt  compactness at core bounce
"""
import math
import os
import sys

# outcomes.md section 1.3 (read from S16 Table 2): (M_ZAMS, log10 R_f [cm]).
# The archive files carry no radius, so RadiusCm is interpolated linearly in log R between these nodes.
RADIUS_GRID = [
    (9.0, 13.46), (9.5, 13.46), (10.0, 13.55), (10.5, 13.58), (11.0, 13.60), (11.5, 13.60),
    (12.0, 13.65), (12.5, 13.68), (13.0, 13.69), (14.0, 13.74), (15.0, 13.77), (16.0, 13.79),
    (17.0, 13.80), (18.0, 13.83), (19.0, 13.85), (20.0, 13.87), (21.0, 13.89), (22.0, 13.91),
    (23.0, 13.94), (24.0, 13.96), (25.0, 13.99), (26.0, 14.00), (27.0, 14.02), (28.0, 14.03),
    (29.0, 14.03), (30.0, 14.03), (31.0, 14.01), (32.0, 13.99), (33.0, 13.96), (35.0, 12.16),
    (40.0, 11.91), (45.0, 10.83), (50.0, 11.58), (55.0, 11.72), (60.0, 10.49), (70.0, 10.63),
    (80.0, 10.62), (100.0, 10.58), (120.0, 11.55),
]

# outcomes.md section 8.2 step 8: a remnant above ~2.2 Msun gravitational cannot be a neutron star.
NS_MAX_GRAVITATIONAL_MASS = 2.2

SOURCE_LINE = (
    "# source: Sukhbold et al. 2016 ApJ 821 38 per-model data (MPA Garching CCSN archive doi:10.17617/1.b"
    " data/SEWBJ_2015): explosions from figure_N20_Z9.6.txt (Z9.6 engine for M<=12; N20 for M>12);"
    " PreSnMass/HeCoreMass/CoCoreMass from progenitor_properties_presn.txt; Compactness25 (xi_2.5 at bounce)"
    " from progenitor_properties_bounce.txt; HydrogenEnvelopeMass = PreSnMass - HeCoreMass (H-rich envelope);"
    " NickelMass = M_Ni + M_Tr (P-HOTB upper value); direct-BH remnant = He-core mass;"
    " RadiusCm interpolated in log R from outcomes.md section 1.3 (S16 Table 2) because the archive lists no radius"
)

HEADER = ("InitialMass,Explodes,Result,RemnantBaryonicMass,ExplosionEnergyErg,NickelMass,Compactness25,"
          "PreSnMass,HeCoreMass,CoCoreMass,HydrogenEnvelopeMass,RadiusCm")


def data_lines(path):
    with open(path, encoding="ascii") as f:
        for line in f:
            if line.strip() and not line.lstrip().startswith("#"):
                yield line.split()


def mass_key(text):
    return round(float(text), 2)


def read_explosions(path):
    rows = {}
    for p in data_lines(path):
        m = mass_key(p[1])
        if p[2] == "failed":
            # "failed SN" lines list M_He_core and M_total_precollapse instead of explosion values.
            rows[m] = {"failed": True, "he_core": float(p[4])}
        else:
            rows[m] = {
                "failed": False,
                "e_bethe": float(p[2]),
                "ni": float(p[4]),
                "tracer": float(p[5]),
                "m_bary": float(p[6]),
                "m_grav": float(p[7]),
            }
    return rows


def read_progenitors(path):
    rows = {}
    for p in data_lines(path):
        rows[mass_key(p[1])] = {
            "m_total": float(p[2]),
            "m_he": float(p[3]),
            "m_co": float(p[4]),
            "xi25": float(p[12]),
        }
    return rows


def log_radius(m):
    for (m0, r0), (m1, r1) in zip(RADIUS_GRID, RADIUS_GRID[1:]):
        if m0 <= m <= m1:
            return r0 + (r1 - r0) * (m - m0) / (m1 - m0)
    raise ValueError(f"mass {m} outside the radius grid")


def g(x):
    return format(x, ".6g")


def main(download_dir, output_csv):
    explosions = read_explosions(os.path.join(download_dir, "figure_N20_Z9.6.txt"))
    presn = read_progenitors(os.path.join(download_dir, "progenitor_properties_presn.txt"))
    bounce = read_progenitors(os.path.join(download_dir, "progenitor_properties_bounce.txt"))
    masses = sorted(explosions)
    if len(masses) != 200 or set(masses) != set(presn) or set(masses) != set(bounce):
        raise SystemExit("model lists differ between the archive files")

    out = [SOURCE_LINE, HEADER]
    for m in masses:
        x, s = explosions[m], presn[m]
        envelope = max(0.0, s["m_total"] - s["m_he"])
        if x["failed"]:
            explodes, result = False, "DirectBlackHole"
            remnant, energy, nickel = x["he_core"], 0.0, 0.0
        else:
            explodes = True
            result = "FallbackBlackHole" if x["m_grav"] > NS_MAX_GRAVITATIONAL_MASS else "NeutronStar"
            remnant, energy, nickel = x["m_bary"], x["e_bethe"] * 1e51, x["ni"] + x["tracer"]
        radius_cm = 10.0 ** log_radius(m)
        out.append(",".join([
            f"{m:.2f}", "true" if explodes else "false", result, g(remnant), g(energy), g(nickel),
            g(bounce[m]["xi25"]), g(s["m_total"]), g(s["m_he"]), g(s["m_co"]), g(envelope),
            format(radius_cm, ".4g"),
        ]))

    with open(output_csv, "w", encoding="ascii", newline="\n") as f:
        f.write("\n".join(out) + "\n")
    print(f"wrote {len(masses)} rows to {output_csv}")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit(__doc__)
    main(sys.argv[1], sys.argv[2])
