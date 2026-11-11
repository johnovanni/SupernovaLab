namespace SupernovaLab.Core.Units;

/// <summary>cgs constants from docs/research/signals.md section 0.2.</summary>
public static class PhysicalConstants
{
    /// <summary>Speed of light, cm/s (exact).</summary>
    public const double C = 2.99792458e10;
    /// <summary>Gravitational constant, cm^3 g^-1 s^-2 (CODATA).</summary>
    public const double G = 6.674e-8;
    /// <summary>Solar mass, g.</summary>
    public const double MSun = 1.989e33;
    /// <summary>Solar radius, cm (IAU 2015 B3 nominal).</summary>
    public const double RSun = 6.957e10;
    /// <summary>Parsec, cm.</summary>
    public const double Pc = 3.0857e18;
    public const double Kpc = 3.0857e21;
    public const double Mpc = 3.0857e24;
    /// <summary>Day, s.</summary>
    public const double Day = 86400;
    /// <summary>Year, s.</summary>
    public const double Year = 3.156e7;
    /// <summary>MeV in erg.</summary>
    public const double MeV = 1.602177e-6;
    /// <summary>Solar luminosity, erg/s (IAU 2015 B3).</summary>
    public const double LSun = 3.828e33;
    /// <summary>M_bol zero-point luminosity, erg/s (IAU 2015 B2); M_bol = -2.5 log10(L / this).</summary>
    public const double MbolZeroPointLum = 3.0128e35;
}
