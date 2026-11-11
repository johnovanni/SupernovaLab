using System.Globalization;

namespace SupernovaLab.Core.Units;

/// <summary>Plain-language formatting of cgs values for the UI boundary.</summary>
public static class UnitFormat
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private const string Superscripts = "⁰¹²³⁴⁵⁶⁷⁸⁹";

    /// <summary>Three significant figures, trailing zeros trimmed.</summary>
    private static string Sig(double v) => v.ToString("G3", Inv);

    public static string Mass(double msun) => $"{Sig(msun)} M☉";

    public static string Energy(double erg)
    {
        if (erg == 0) return "0 erg";
        int exp = (int)Math.Floor(Math.Log10(Math.Abs(erg)));
        double mant = erg / Math.Pow(10, exp);
        // Rounding to 3 figures can push the mantissa to 10.
        if (Math.Abs(double.Parse(Sig(mant), Inv)) >= 10) { mant /= 10; exp++; }
        return $"{Sig(mant)} × 10{Superscript(exp)} erg";
    }

    public static string Time(double seconds)
    {
        double a = Math.Abs(seconds);
        if (a == 0) return "0 s";
        if (a < 1e-3) return $"{Sig(seconds * 1e6)} µs";
        if (a < 1) return $"{Sig(seconds * 1e3)} ms";
        if (a < 60) return $"{Sig(seconds)} s";
        if (a < 3600) return $"{Sig(seconds / 60)} min";
        if (a < PhysicalConstants.Day) return $"{Sig(seconds / 3600)} h";
        if (a < PhysicalConstants.Year) return $"{Sig(seconds / PhysicalConstants.Day)} days";
        return $"{Sig(seconds / PhysicalConstants.Year)} years";
    }

    public static string Distance(double cm)
    {
        double ly = cm / (PhysicalConstants.C * PhysicalConstants.Year);
        double lyRounded = double.Parse(ly.ToString("G3", Inv), Inv);
        string lyText = lyRounded.ToString("N0", Inv);
        double pc = cm / PhysicalConstants.Pc;
        string main = pc >= 1e6 ? $"{Sig(pc / 1e6)} Mpc"
                    : pc >= 1e3 ? $"{Sig(pc / 1e3)} kpc"
                    : $"{Sig(pc)} pc";
        return $"{main} ({lyText} light-years)";
    }

    private static string Superscript(int n)
    {
        var s = new System.Text.StringBuilder();
        if (n < 0) s.Append('⁻');
        foreach (char ch in Math.Abs(n).ToString(Inv)) s.Append(Superscripts[ch - '0']);
        return s.ToString();
    }
}
