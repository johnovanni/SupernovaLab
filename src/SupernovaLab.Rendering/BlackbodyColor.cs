using SupernovaLab.Core.Units;

namespace SupernovaLab.Rendering;

/// <summary>
/// The colour of a blackbody of a given temperature as the eye would see it, in normalized sRGB (the brightest channel
/// is 1; brightness is set separately). Planck's law (CODATA constants, <see cref="PhysicalConstants"/>) is weighted by
/// the CIE 1931 2° colour-matching functions in Wyman, Sloan &amp; Shirley's multi-lobe Gaussian fit (2013, JCGT 2(2), 1,
/// Eq. 2) from 380 to 780 nm, converted from XYZ to linear sRGB with the IEC 61966-2-1 matrix (D65 white), negative
/// (out-of-gamut) channels clipped to 0, normalized and gamma-encoded with the sRGB transfer function. The Sun's
/// 5,800 K comes out a slightly warm white, 3,000 K orange-red and 30,000 K pale blue.
/// </summary>
public static class BlackbodyColor
{
    private const double LambdaMinNm = 380, LambdaMaxNm = 780, StepNm = 5;

    /// <summary>Below this the visible spectrum is nearly all deep red and Planck's exponent overflows: the colour of 1,000 K.</summary>
    private const double MinKelvin = 1000;

    /// <summary>Second radiation constant c2 = hc/k, nm K.</summary>
    private static readonly double C2NmK = PhysicalConstants.Planck * PhysicalConstants.C / PhysicalConstants.Boltzmann * 1e7;

    /// <summary>Normalized sRGB (0–1, gamma-encoded) of a blackbody at this temperature; white when it is not a positive number.</summary>
    public static (float R, float G, float B) FromTemperature(double kelvin)
    {
        if (!(kelvin > 0)) return (1f, 1f, 1f);
        double t = Math.Max(kelvin, MinKelvin);

        double x = 0, y = 0, z = 0;
        for (double nm = LambdaMinNm; nm <= LambdaMaxNm; nm += StepNm)
        {
            double planck = Math.Pow(nm, -5) / (Math.Exp(C2NmK / (nm * t)) - 1);
            x += planck * XBar(nm);
            y += planck * YBar(nm);
            z += planck * ZBar(nm);
        }

        double r = Math.Max(0, 3.2406 * x - 1.5372 * y - 0.4986 * z);
        double g = Math.Max(0, -0.9689 * x + 1.8758 * y + 0.0415 * z);
        double b = Math.Max(0, 0.0557 * x - 0.2040 * y + 1.0570 * z);
        double max = Math.Max(r, Math.Max(g, b));
        if (!(max > 0)) return (1f, 0f, 0f);
        return ((float)Encode(r / max), (float)Encode(g / max), (float)Encode(b / max));
    }

    /// <summary>The sRGB transfer function (IEC 61966-2-1).</summary>
    private static double Encode(double linear)
        => linear <= 0.0031308 ? 12.92 * linear : 1.055 * Math.Pow(linear, 1 / 2.4) - 0.055;

    /// <summary>A Gaussian with different widths below and above its peak (Wyman et al. 2013, Eq. 2).</summary>
    private static double Lobe(double nm, double mu, double sigmaBelow, double sigmaAbove)
    {
        double s = (nm - mu) / (nm < mu ? sigmaBelow : sigmaAbove);
        return Math.Exp(-0.5 * s * s);
    }

    private static double XBar(double nm)
        => 1.056 * Lobe(nm, 599.8, 37.9, 31.0) + 0.362 * Lobe(nm, 442.0, 16.0, 26.7) - 0.065 * Lobe(nm, 501.1, 20.4, 26.2);

    private static double YBar(double nm) => 0.821 * Lobe(nm, 568.8, 46.9, 40.5) + 0.286 * Lobe(nm, 530.9, 16.3, 31.1);

    private static double ZBar(double nm) => 1.217 * Lobe(nm, 437.0, 11.8, 36.0) + 0.681 * Lobe(nm, 459.0, 26.0, 13.8);
}
