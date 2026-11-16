namespace SupernovaLab.Core.Text;

/// <summary>Plain-language comparisons for the panels (spec §5.1: "as bright as Venus", "visible in daylight").</summary>
public static class Comparisons
{
    /// <summary>
    /// A comparison for an apparent V magnitude (smaller is brighter). The ranges overlap, so they are checked in this
    /// order: brighter than −12.7 (the full Moon); −4.9 to −3.5 (Venus at its brightest to its faintest); brighter than −4
    /// (daylight); −1.5 to 1.5 (the brightest stars); 6 or brighter (dark-sky naked-eye limit, signals §4.7); 9 or brighter
    /// (binoculars); fainter (a telescope). Between −3.5 and −1.5 it is "visible to the naked eye". NaN (no light) gives
    /// "no visible light".
    /// </summary>
    public static string ForApparentMagnitude(double m)
    {
        if (double.IsNaN(m)) return "no visible light";
        if (m < -12.7) return "brighter than the full Moon";
        if (m >= -4.9 && m <= -3.5) return "as bright as Venus";
        if (m < -4) return "visible in daylight";
        if (m >= -1.5 && m <= 1.5) return "as bright as the brightest stars";
        if (m <= 6) return "visible to the naked eye";
        if (m <= 9) return "needs binoculars";
        return "needs a telescope";
    }

    /// <summary>A refrigerator magnet's field at its face, about 100 G (10 mT; typical fridge magnets are a few to a few tens of mT) [APPROX].</summary>
    public const double FridgeMagnetG = 100;

    /// <summary>
    /// A comparison for a neutron star's surface field (spec §5): the field in fridge magnets, said as a word. The ratio B / 100 G is put in
    /// the largest named unit (thousand, million, billion, trillion, quadrillion) at or below it and rounded to one significant figure:
    /// 10¹⁴ G is "about a trillion times", 5 × 10¹⁴ G "about 5 trillion times", 10¹⁵ G "about 10 trillion times". A field at or below
    /// a fridge magnet's, or NaN, says so plainly.
    /// </summary>
    public static string ForMagneticField(double fieldG)
    {
        if (!(fieldG > 0) || double.IsInfinity(fieldG)) return "no field to compare";
        double ratio = fieldG / FridgeMagnetG;
        if (ratio < 1e3) return $"about {ratio.ToString(ratio < 10 ? "0.#" : "0", System.Globalization.CultureInfo.InvariantCulture)} times a fridge magnet's field (about {FridgeMagnetG:0} G)";
        (double Unit, string Name)[] units = [(1e15, "quadrillion"), (1e12, "trillion"), (1e9, "billion"), (1e6, "million"), (1e3, "thousand")];
        var (unit, name) = units.First(u => ratio >= u.Unit * 0.999999);
        double n = ratio / unit, scale = Math.Pow(10, Math.Floor(Math.Log10(n)));
        double rounded = Math.Round(n / scale, MidpointRounding.AwayFromZero) * scale;
        string count = rounded == 1 ? "a" : rounded.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
        return $"about {count} {name} times a fridge magnet's field (about {FridgeMagnetG:0} G)";
    }
}
