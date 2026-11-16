using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Compact;

/// <summary>
/// One layer of the neutron star (spec §4.2), between two radii (km from the centre). The atmosphere has no thickness here: it is a
/// labelled line at the surface (<see cref="InnerRadiusKm"/> = <see cref="OuterRadiusKm"/>).
/// </summary>
public sealed record StarLayer(string Name, double InnerRadiusKm, double OuterRadiusKm, Confidence Confidence, Source Source, string Label);

/// <summary>
/// The neutron star's layers and the neutron drip line (spec §4.2; compact §C5 and its App block). The boundaries come from
/// Zdunik, Fortin &amp; Haensel 2017's Eq. 8 for a crust layer at baryon chemical potential μ: r_b = r_g / (1 − (1 − r_g/R) (μ₀/μ_b)²),
/// r_g = 2GM/c² [D]. Layer depths are accurate to under 1% of the crust thickness for M &gt; 1 M☉.
/// </summary>
public static class NeutronStarLayers
{
    /// <summary>§C5.1 [V via Zdunik]: the neutron drip density, 4.3 × 10¹¹ g/cm³ (BPS 1971; modern models 4.3–4.4 × 10¹¹).</summary>
    public static double DripDensityGcm3 => 4.3e11;

    /// <summary>The spec's label for the unknown inner core.</summary>
    public const string UnknownCoreLabel = "Inner core: unknown";

    /// <summary>Spec §4.2: the drip line's confidence, in words (it is <see cref="Confidence.Established"/> with a note).</summary>
    public const string DripConfidenceText = "Established theory, observed indirectly";

    /// <summary>The proto-neutron star's label before the crust forms (spec §4.2).</summary>
    public const string ProtoNeutronStarLabel = "Proto-neutron star: hot and liquid, no crust yet";

    /// <summary>§C5.2: ⁵⁶Fe at the surface, MeV (Zdunik et al. 2017).</summary>
    public const double SurfaceChemicalPotentialMeV = 930.4;

    /// <summary>§C5.2: drip is where the neutron's chemical potential reaches its rest mass, MeV [R].</summary>
    public const double DripChemicalPotentialMeV = 939.565;

    /// <summary>§C5.2: the crust–core boundary, MeV (DH functional; Zdunik et al. 2017, Table 1).</summary>
    public const double CoreChemicalPotentialMeV = 953.3;

    /// <summary>§C5 App [APPROX]: the pasta's share of the inner crust's thickness (Balliet et al. 2021's 30% median); the range is 0–43%.</summary>
    public const double PastaFractionOfInnerCrust = 0.30;

    /// <summary>§C5 App [APPROX]: the inner core ends at this fraction of the core's radius ("may reach several km").</summary>
    public const double InnerCoreFractionOfCoreRadius = 0.5;

    /// <summary>
    /// §C5.3 / App [D/APPROX]: the crust crystallises about 1 day (86,400 s) after bounce, the same for every neutron-star outcome (range
    /// 1 h to 1 month). Moved here from <see cref="NeutronStarModel"/> (ledger ruling S1).
    /// </summary>
    public static double CrustFormationTimeS(Outcome o) => 86_400;

    /// <summary>The crust is crystallising by this source time.</summary>
    public static bool HasCrust(Outcome o, double sourceTimeS) => sourceTimeS >= CrustFormationTimeS(o);

    private static readonly Source LayersSource = new(
        "Layer radii from Zdunik, Fortin & Haensel 2017 (Eq. 8, crust to 1% of its thickness) with the lab's mass and radius [derived]; " +
        "layer contents from Yakovlev & Pethick 2004 and Chamel & Haensel 2008", "compact §C5");

    private static readonly Source PastaSource = new(
        "Pasta share of the inner crust: Balliet et al. 2021's median 30% (range 0–43%, absent in some models); Chamel & Haensel 2008 [APPROX]",
        "compact §C5.1");

    private static readonly Source CoreSource = new(
        "Yakovlev & Pethick 2004: outer core n, p, e, μ up to about 2ρ₀; the inner core's extent (here half the core's radius) is an approximation",
        "compact §C5.1");

    /// <summary>The radius (km) of the layer at baryon chemical potential <paramref name="muMeV"/> (§C5.2, Eq. 8).</summary>
    private static double BoundaryKm(double massMsun, double radiusKm, double muMeV)
    {
        double rg = 2 * BlackHoleOptics.GravitationalRadiusCm(massMsun) / 1e5;
        double k = (1 - rg / radiusKm) * Math.Pow(SurfaceChemicalPotentialMeV / muMeV, 2);
        return rg / (1 - k);
    }

    /// <summary>The layers, centre outward: inner core, outer core, nuclear pasta, inner crust, outer crust, atmosphere (§C5 App table).</summary>
    public static IReadOnlyList<StarLayer> For(double massMsun, double radiusKm)
    {
        double rDrip = BoundaryKm(massMsun, radiusKm, DripChemicalPotentialMeV);
        double rCore = BoundaryKm(massMsun, radiusKm, CoreChemicalPotentialMeV);
        double rPasta = rCore + PastaFractionOfInnerCrust * (rDrip - rCore);
        double rInnerCore = InnerCoreFractionOfCoreRadius * rCore;
        return new StarLayer[]
        {
            new("Inner core", 0, rInnerCore, Confidence.Speculative, CoreSource, UnknownCoreLabel),
            new("Outer core", rInnerCore, rCore, Confidence.Established, CoreSource, "Outer core: neutrons, protons, electrons, muons"),
            new("Nuclear pasta", rCore, rPasta, Confidence.ModelDependent, PastaSource, "Nuclear pasta"),
            new("Inner crust", rPasta, rDrip, Confidence.Established, LayersSource, "Inner crust: nuclei in a sea of free (superfluid) neutrons"),
            new("Outer crust", rDrip, radiusKm, Confidence.Established, LayersSource, "Outer crust: a lattice of nuclei and electrons"),
            new("Atmosphere", radiusKm, radiusKm, Confidence.Established, LayersSource, "Atmosphere: centimetres thick"),
        };
    }

    /// <summary>How deep the neutron drip lies below the surface (km): 0.427 at 1.4 M☉ and 12 km; 0.19–0.53 over 2.2–1.2 M☉ (§C5.2).</summary>
    public static double DripDepthKm(double massMsun, double radiusKm)
        => radiusKm - BoundaryKm(massMsun, radiusKm, DripChemicalPotentialMeV);

    /// <summary>The crust's thickness (km), surface to the crust–core boundary: 0.99 km, 8.3% of the radius at 1.4 M☉ and 12 km (§C5.2).</summary>
    public static double CrustThicknessKm(double massMsun, double radiusKm)
        => radiusKm - BoundaryKm(massMsun, radiusKm, CoreChemicalPotentialMeV);

    /// <summary>
    /// The magnified strip's ×N: the strip spans the outermost R/N of the star's depth and is drawn as long as the star's radius, so the
    /// crust fills crust × N / R of it; the smallest whole N that makes that at least 40% (5 at 1.4 M☉, 12 km).
    /// </summary>
    public static int Magnification(double crustKm, double radiusKm)
        => crustKm > 0 && radiusKm > 0 ? Math.Max(2, (int)Math.Ceiling(MinCrustShareOfStrip * radiusKm / crustKm - 1e-9)) : 2;

    /// <summary>The least share of the magnified strip the crust fills (plan Task 7).</summary>
    public const double MinCrustShareOfStrip = 0.4;
}
