using System.Globalization;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Rendering;

/// <summary>A scale bar: its physical length (cm), its length on screen (px) and its label, e.g. "2 AU".</summary>
public sealed record ScaleBarChoice(double LengthCm, double LengthPx, string Label);

/// <summary>
/// The 3D view's scale bar (spec §6): km, AU, light-years or parsecs. The unit is the largest of these of which at least
/// one fits in the space allowed, and the length is the largest 1, 2 or 5 × 10^k of it that fits, so the bar is between
/// a fifth of the space and all of it. Kilometres from 1 million up are written "20 million km"; AU and kilometres keep
/// thousands separators ("20,000 AU"), since no unit in the spec's list falls between them and the next one.
/// </summary>
public static class ScaleBar
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static readonly (double Cm, string Name)[] Units =
    {
        (PhysicalConstants.Pc, "pc"),
        (PhysicalConstants.LightYear, "light-year"),
        (PhysicalConstants.Au, "AU"),
        (1e5, "km"),
    };

    /// <summary>The bar for a view at this scale (cm per pixel) with at most this many pixels for it; null for a bad scale.</summary>
    public static ScaleBarChoice? Choose(double cmPerPx, double maxPx)
    {
        if (!(cmPerPx > 0) || double.IsInfinity(cmPerPx) || !(maxPx > 0)) return null;
        double maxCm = cmPerPx * maxPx;
        foreach (var (unitCm, name) in Units)
        {
            double inUnit = maxCm / unitCm;
            if (inUnit < 1 && name != "km") continue;
            double nice = Nice(inUnit);
            double cm = nice * unitCm;
            return new ScaleBarChoice(cm, cm / cmPerPx, Label(nice, name));
        }
        return null;
    }

    /// <summary>The largest 1, 2 or 5 × 10^k not above x (x &gt; 0).</summary>
    private static double Nice(double x)
    {
        double decade = Math.Pow(10, Math.Floor(Math.Log10(x) + 1e-12));
        double m = x / decade;
        return (m >= 5 - 1e-9 ? 5 : m >= 2 - 1e-9 ? 2 : 1) * decade;
    }

    private static string Label(double value, string unit) => unit switch
    {
        "km" when value >= 1e6 => $"{Number(value / 1e6)} million km",
        "light-year" => $"{Number(value)} {(value == 1 ? "light-year" : "light-years")}",
        _ => $"{Number(value)} {unit}",
    };

    private static string Number(double v) => v >= 1 ? v.ToString("#,##0", Inv) : v.ToString("0.###", Inv);
}
