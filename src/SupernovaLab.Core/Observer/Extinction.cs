namespace SupernovaLab.Core.Observer;

public enum Band { U, B, V, R, I, Uv, SoftXray }

/// <summary>Interstellar extinction A_band = (A_band / A_V) x A_V, with R_V = 3.1.</summary>
public static class Extinction
{
    /// <summary>Extinction in magnitudes in the given band for a visual extinction <paramref name="av"/>.</summary>
    public static double A(Band band, double av) => av * Ratio(band);

    /// <summary>Hydrogen column density, cm^-2: N_H = 2.2e21 A_V (Guver and Ozel 2009; signals section 6, [APPROX coefficient]).</summary>
    public static double NhFromAv(double av) => 2.2e21 * av;

    private static double Ratio(Band band) => band switch
    {
        // signals.md section 4.7 (Cardelli, Clayton and Mathis 1989, ApJ 345, 245; R_V = 3.1) [APPROX, read from the CCM curve].
        Band.V => 1.0,
        Band.B => 1.32,
        Band.R => 0.75,
        Band.I => 0.48,
        // Not given in signals section 4.7. [APPROX] choices read from the CCM 1989 curve, R_V = 3.1:
        // U (3650 A, 1/lambda = 2.74 per um): A_U/A_V about 1.56.
        Band.U => 1.56,
        // UV (about 2000-2500 A, 1/lambda about 4-5 per um, near the 2175 A bump): A_UV/A_V about 2.6.
        Band.Uv => 2.6,
        // Soft X-rays are absorbed by gas, not dimmed by dust in magnitudes; attenuation uses
        // NhFromAv with a photoelectric cross-section, so there is no magnitude ratio. [APPROX]
        Band.SoftXray => 0.0,
        _ => throw new ArgumentOutOfRangeException(nameof(band)),
    };
}
