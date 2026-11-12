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
}
