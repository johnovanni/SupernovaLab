using SupernovaLab.Core.Compact;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Text;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Rendering;

/// <summary>
/// The cutaway's schematic layer colours (spec §4.2: schematic, with a legend). The same hex strings colour the legend's swatches, the
/// magnified strip and (tone-mapped, <see cref="NeutronStarCutawayRenderer"/>) the cut faces.
/// </summary>
public static class CutawayPalette
{
    public const string InnerCore = "#8E6BD6", OuterCore = "#3F78C4", Pasta = "#E0873A", InnerCrust = "#D8C45A", OuterCrust = "#9C9A90",
        Atmosphere = "#F2F0E6", DripLine = "#5CF2FF", Hot = "#FF9A4D";

    /// <summary>The colour of a layer by name (<see cref="StarLayer.Name"/>); the proto-neutron star's hot colour for none.</summary>
    public static string For(string layerName) => layerName switch
    {
        "Inner core" => InnerCore,
        "Outer core" => OuterCore,
        "Nuclear pasta" => Pasta,
        "Inner crust" => InnerCrust,
        "Outer crust" => OuterCrust,
        "Atmosphere" => Atmosphere,
        _ => Hot,
    };

    /// <summary>"#RRGGBB" as normalized sRGB.</summary>
    public static (float R, float G, float B) Rgb(string hex)
        => (Convert.ToInt32(hex.Substring(1, 2), 16) / 255f, Convert.ToInt32(hex.Substring(3, 2), 16) / 255f, Convert.ToInt32(hex.Substring(5, 2), 16) / 255f);
}

/// <summary>One line of the cutaway's key: a layer (or the drip line) with its schematic colour, label, confidence and tooltip.</summary>
public sealed record CutawayLegendEntry(string Name, string Label, string ColorHex, Confidence Confidence, string ToolTip, string? ConfidenceText = null);

/// <summary>
/// One piece of the magnified strip: from <see cref="StartFraction"/> to <see cref="EndFraction"/> of its length (0 deep, 1 the
/// surface), in <see cref="ColorHex"/>. The atmosphere is a line, not a piece.
/// </summary>
public sealed record CutawayStripSegment(string Name, double StartFraction, double EndFraction, string ColorHex, string ToolTip);

/// <summary>
/// What the Close-up's cutaway shows at one moment (spec §4.2): before the crust forms a hot, uniform proto-neutron star; after, the
/// layers (to scale), the neutron drip line, and the magnified strip of the outermost layers (the crust is only about a kilometre
/// thick). Built by <see cref="Build"/> from the star's mass, radius and whether the crust has formed; plain data, so the view model
/// draws the key and the strip from it and the renderer the faces.
/// </summary>
/// <param name="HasCrust">The crust has formed (compact §C5.3).</param>
/// <param name="RadiusKm">The star's radius.</param>
/// <param name="Layers">Centre outward (empty before the crust forms).</param>
/// <param name="DripRadiusFraction">The drip line's radius over the star's (NaN before the crust forms).</param>
/// <param name="Legend">The key, centre outward, then the drip line.</param>
/// <param name="Strip">The magnified strip's pieces, deep to the surface (empty before the crust forms).</param>
/// <param name="DripStripFraction">The drip line's place along the strip, 0 deep to 1 the surface (NaN before the crust forms).</param>
/// <param name="Magnification">The strip's ×N (0 before the crust forms).</param>
/// <param name="MagnifiedLabel">"Magnified ×5", or null before the crust forms.</param>
/// <param name="DripLabel">"Neutron drip: 4.3 × 10¹¹ g/cm³", or null before the crust forms.</param>
/// <param name="StateLabel">"Proto-neutron star: hot and liquid, no crust yet" before the crust forms; null after.</param>
/// <param name="Readouts">The numbers the column shows while the cutaway is on, each with its confidence and source.</param>
/// <param name="Notes">The notes that describe the cutaway.</param>
public sealed record CutawayInfo(bool HasCrust, double RadiusKm, IReadOnlyList<StarLayer> Layers, double DripRadiusFraction,
    IReadOnlyList<CutawayLegendEntry> Legend, IReadOnlyList<CutawayStripSegment> Strip, double DripStripFraction, int Magnification,
    string? MagnifiedLabel, string? DripLabel, string? StateLabel, IReadOnlyList<Readout> Readouts, IReadOnlyList<string> Notes)
{
    /// <summary>Spec §4.2: the layers are to scale; the colours and the strip are not (R-P3-19: nothing here is bent either).</summary>
    public const string ScaleNote =
        "Cutaway: the layers' radii are to scale (from the research's crust model at this star's mass and radius), except the magnified strip, " +
        "which is labelled. The layer colours are schematic: the key lists them";

    /// <summary>The cutaway is an analytic drawing, not light traced through the star's spacetime (ruling R-P3-19, as the overlays).</summary>
    public const string UnbentNote =
        "The cutaway is drawn without the bending of light, as an analytic model of the star's inside, over the star's bent image: the " +
        "bent limb shows round it, and the notch is black";

    /// <summary>Pasta and the inner core are not settled (spec §4.2).</summary>
    public const string UncertainLayersNote =
        "Nuclear pasta is model-dependent (some models have none; it is drawn as 30% of the inner crust, a median). The inner core's make-up, " +
        "and whether a low-mass star has one, is unknown (it is drawn as half the core's radius, an approximation)";

    /// <summary>The crust time, in words.</summary>
    public const string CrustTimeNote =
        "The crust forms as the star cools, about a day after the bounce (approximate: an hour to a month; compact §C5.3); until then the " +
        "cutaway shows a hot, uniform proto-neutron star";

    private static readonly Source DripSource = new(
        "Neutron drip density: Baym, Pethick & Sutherland 1971 (4.3 × 10¹¹ g/cm³); modern models 4.3–4.4 × 10¹¹ (Chamel et al. 2015; Chamel & Haensel 2008). " +
        "Indirect evidence: pulsar glitches (Link, Epstein & Lattimer 1999) and crust cooling after outbursts (Cackett et al. 2006)", "compact §C5.1, §C5.4");

    private static readonly Source DepthSource = new(
        "Depth of the drip and crust–core boundary from the star's mass and radius: Zdunik, Fortin & Haensel 2017, Eq. 8 (accurate to 1% of the crust) [derived]",
        "compact §C5.2");

    private static readonly Source TimeSource = new(
        "The crust freezes below about 5 × 10⁹ K, about 3 × 10⁻³ yr (Gnedin et al. 2001; Chamel & Haensel 2008): about 1 day [derived, approximate]; " +
        "the shallow outer crust freezes over weeks to months", "compact §C5.3");

    /// <summary>The cutaway for a star of this mass and radius (cheap; callers keep the two results per star).</summary>
    public static CutawayInfo Build(Outcome outcome, double massMsun, double radiusKm, bool hasCrust)
    {
        var timeQ = new Quantity(NeutronStarLayers.CrustFormationTimeS(outcome), "s", Confidence.ModelDependent, TimeSource, 3600, 30 * 86400.0);
        if (!hasCrust)
        {
            return new CutawayInfo(false, radiusKm, Array.Empty<StarLayer>(), double.NaN,
                new[] { new CutawayLegendEntry("Proto-neutron star", NeutronStarLayers.ProtoNeutronStarLabel, CutawayPalette.Hot, Confidence.Established,
                    Glossary.ProtoNeutronStar) },
                Array.Empty<CutawayStripSegment>(), double.NaN, 0, null, null, NeutronStarLayers.ProtoNeutronStarLabel,
                new[] { new Readout("Crust forms after", timeQ, Glossary.ProtoNeutronStar) },
                new[] { ScaleNote, CrustTimeNote });
        }

        var layers = NeutronStarLayers.For(massMsun, radiusKm);
        double dripKm = NeutronStarLayers.DripDepthKm(massMsun, radiusKm);
        double crustKm = NeutronStarLayers.CrustThicknessKm(massMsun, radiusKm);
        int n = NeutronStarLayers.Magnification(crustKm, radiusKm);
        double depthShown = radiusKm / n;                       // the strip spans the outermost R/N of depth, drawn R long
        double rDrip = radiusKm - dripKm;
        string magnified = $"Magnified ×{n}";
        string dripLabel = $"Neutron drip: {UnitFormat.Scientific(NeutronStarLayers.DripDensityGcm3, "g/cm³")}";

        var legend = new List<CutawayLegendEntry>();
        foreach (var l in layers)
        {
            string tip = l.Name switch
            {
                "Inner core" => Glossary.InnerCore,
                "Outer core" => Glossary.OuterCore,
                "Nuclear pasta" => Glossary.NuclearPasta,
                "Inner crust" => Glossary.InnerCrust + " " + Glossary.Superfluid,
                "Outer crust" => Glossary.OuterCrust,
                _ => Glossary.NeutronStarAtmosphere,
            };
            legend.Add(new CutawayLegendEntry(l.Name, l.Label, CutawayPalette.For(l.Name), l.Confidence, tip));
        }
        legend.Add(new CutawayLegendEntry("Neutron drip line", dripLabel, CutawayPalette.DripLine, Confidence.Established, Glossary.NeutronDrip,
            NeutronStarLayers.DripConfidenceText));

        double lo = radiusKm - depthShown;
        var strip = new List<CutawayStripSegment>();
        foreach (var l in layers)
        {
            if (l.Name == "Atmosphere" || l.OuterRadiusKm <= lo) continue;
            double a = Math.Max(l.InnerRadiusKm, lo), b = l.OuterRadiusKm;
            string tip = legend.First(e => e.Name == l.Name).ToolTip;
            strip.Add(new CutawayStripSegment(l.Name, (a - lo) / depthShown, (b - lo) / depthShown, CutawayPalette.For(l.Name), tip));
        }

        var readouts = new List<Readout>
        {
            new("Neutron drip density",
                new Quantity(NeutronStarLayers.DripDensityGcm3, "g/cm³", Confidence.Established, DripSource, 4.0e11, 4.4e11),
                Glossary.NeutronDrip),
            new("Drip depth", new Quantity(dripKm, "km", Confidence.ModelDependent, DepthSource),
                "How far below the surface the neutron drip line lies (the research's range: 0.19 km at 2.2 solar masses to 0.53 km at 1.2)."),
            new("Crust thickness", new Quantity(crustKm, "km", Confidence.ModelDependent, DepthSource),
                $"From the surface to the crust-core boundary; {UnitFormat.Number(100 * crustKm / radiusKm)}% of the radius (the research's range: 3.9% at 2.2 solar masses to 10.2% at 1.2)."),
            new("Crust formed after", timeQ, Glossary.ProtoNeutronStar),
        };
        return new CutawayInfo(true, radiusKm, layers, rDrip / radiusKm, legend, strip, (rDrip - lo) / depthShown, n, magnified, dripLabel, null,
            readouts, new[] { ScaleNote, UnbentNote, UncertainLayersNote, CrustTimeNote });
    }
}
