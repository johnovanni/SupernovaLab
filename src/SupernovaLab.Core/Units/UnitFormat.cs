using System.Globalization;

namespace SupernovaLab.Core.Units;

/// <summary>Plain-language formatting of cgs values for the UI boundary.</summary>
public static class UnitFormat
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private const string Superscripts = "⁰¹²³⁴⁵⁶⁷⁸⁹";

    /// <summary>Rounds to three significant figures.</summary>
    private static double Round3(double v)
    {
        if (v == 0 || double.IsNaN(v) || double.IsInfinity(v)) return v;
        int exp = (int)Math.Floor(Math.Log10(Math.Abs(v)));
        double scale = Math.Pow(10, 2 - exp);
        return Math.Round(v * scale, MidpointRounding.AwayFromZero) / scale;
    }

    /// <summary>Three significant figures, never scientific notation; 100 or more gets thousands separators.</summary>
    private static string Sig(double v)
    {
        double r = Round3(v);
        return Math.Abs(r) >= 100 ? r.ToString("N0", Inv) : r.ToString("0.##########", Inv);
    }

    private readonly record struct Unit(double Div, double Upper, string Name);

    /// <summary>
    /// Chooses the unit after rounding: the first unit whose 3-figure value is below its upper bound.
    /// A value that rounds up to the bound is shown as 1 of the next unit (e.g. 59.96 s is "1 min").
    /// </summary>
    private static string Scaled(double v, Unit[] units)
    {
        for (int i = 0; i < units.Length; i++)
        {
            double r = Round3(v / units[i].Div);
            if (i == units.Length - 1 || r < units[i].Upper) return $"{Sig(r)} {units[i].Name}";
            if (Round3(v / units[i + 1].Div) < 1 && r >= units[i].Upper)
                return $"1 {units[i + 1].Name}";
        }
        throw new InvalidOperationException();
    }

    public static string Mass(double msun) => $"{Sig(msun)} M☉";

    public static string Energy(double erg)
    {
        if (erg == 0) return "0 erg";
        int exp = (int)Math.Floor(Math.Log10(Math.Abs(erg)));
        double mant = Round3(erg / Math.Pow(10, exp));
        // Rounding to 3 figures can push the mantissa to 10.
        if (Math.Abs(mant) >= 10) { mant /= 10; exp++; }
        return $"{Sig(mant)} × 10{Superscript(exp)} erg";
    }

    private static readonly Unit[] TimeUnits =
    {
        new(1e-6, 1000, "µs"),
        new(1e-3, 1000, "ms"),
        new(1, 60, "s"),
        new(60, 60, "min"),
        new(3600, 24, "h"),
        new(PhysicalConstants.Day, PhysicalConstants.Year / PhysicalConstants.Day, "days"),
        new(PhysicalConstants.Year, double.PositiveInfinity, "years"),
    };

    public static string Time(double seconds)
    {
        if (seconds == 0) return "0 s";
        string text = Scaled(Math.Abs(seconds), TimeUnits);
        return seconds < 0 ? "-" + text : text;
    }

    private static readonly Unit[] DistanceUnits =
    {
        new(1, 1000, "pc"),
        new(1e3, 1000, "kpc"),
        new(1e6, double.PositiveInfinity, "Mpc"),
    };

    public static string Distance(double cm)
    {
        double ly = Round3(cm / (PhysicalConstants.C * PhysicalConstants.Year));
        return $"{Scaled(cm / PhysicalConstants.Pc, DistanceUnits)} ({ly.ToString("N0", Inv)} light-years)";
    }

    private static string Superscript(int n)
    {
        var s = new System.Text.StringBuilder();
        if (n < 0) s.Append('⁻');
        foreach (char ch in Math.Abs(n).ToString(Inv)) s.Append(Superscripts[ch - '0']);
        return s.ToString();
    }
}
