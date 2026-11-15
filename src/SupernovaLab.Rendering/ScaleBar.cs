using System.Globalization;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Rendering;

/// <summary>A scale bar: its physical length (cm), its length on screen (px) and its label, e.g. "2 AU".</summary>
public sealed record ScaleBarChoice(double LengthCm, double LengthPx, string Label);

/// <summary>
/// The 3D view's scale bar (spec §6): km, AU, light-years or parsecs, switching where the overlay's radius
/// (<see cref="UnitFormat.Distance"/>) does: parsecs from 1 pc; light-years from 10,000 AU, fractions allowed ("0.5
/// light-years", as the overlay gives pc with light-years there); AU from 1 AU; km from 1 km; metres below (Project 3: the
/// Close-up's crust strip, spec §2.2). The length is the largest 1, 2
/// or 5 × 10^k of the unit that fits, so the bar is between a fifth of the space and all of it. Kilometres from 1 million
/// up are written "20 million km"; AU and km keep thousands separators ("5,000 AU").
/// </summary>
public static class ScaleBar
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Each unit (cm), and the space (cm) from which it is used.</summary>
    private static readonly (double Cm, double FromCm, string Name)[] Units =
    {
        (PhysicalConstants.Pc, PhysicalConstants.Pc, "pc"),
        (PhysicalConstants.LightYear, 1e4 * PhysicalConstants.Au, "light-year"),
        (PhysicalConstants.Au, PhysicalConstants.Au, "AU"),
        (1e5, 1e5, "km"),
        (100, 0, "m"),
    };

    /// <summary>The bar for a view at this scale (cm per pixel) with at most this many pixels for it; null for a bad scale.</summary>
    public static ScaleBarChoice? Choose(double cmPerPx, double maxPx)
    {
        if (!(cmPerPx > 0) || double.IsInfinity(cmPerPx) || !(maxPx > 0)) return null;
        double maxCm = cmPerPx * maxPx;
        foreach (var (unitCm, fromCm, name) in Units)
        {
            if (maxCm < fromCm) continue;
            double nice = Nice(maxCm / unitCm);
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

    /// <summary>The unit (its size in cm and name) used for a view spanning this many cm: the thresholds <see cref="Choose"/> uses.</summary>
    public static (double UnitCm, string Name) UnitFor(double spanCm)
    {
        foreach (var (unitCm, fromCm, name) in Units)
            if (spanCm >= fromCm) return (unitCm, name);
        return (Units[^1].Cm, Units[^1].Name);
    }

    /// <summary>The size in cm of a unit given its name ("pc", "light-year", "AU", "km" or "m").</summary>
    internal static double UnitCmOf(string name)
    {
        foreach (var (unitCm, _, n) in Units)
            if (n == name) return unitCm;
        throw new ArgumentException($"Unknown unit '{name}'.", nameof(name));
    }

    /// <summary>A value in a unit as text, e.g. "5,000 AU", "20 million km", "0.5 light-years".</summary>
    public static string Label(double value, string unit) => unit switch
    {
        "km" when value >= 1e6 => $"{Number(value / 1e6)} million km",
        "light-year" => $"{Number(value)} {(value == 1 ? "light-year" : "light-years")}",
        _ => $"{Number(value)} {unit}",
    };

    private static string Number(double v) => v >= 1 ? v.ToString("#,##0", Inv) : v.ToString("0.###", Inv);
}
