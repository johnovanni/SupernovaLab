namespace SupernovaLab.Core.Units;

/// <summary>cgs constants from docs/research/signals.md section 0.2, plus a few CODATA 2018 radiation constants.</summary>
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
    /// <summary>keV in erg.</summary>
    public const double KeV = 1.602177e-9;
    /// <summary>Atomic mass unit m_u, g.</summary>
    public const double AtomicMassUnit = 1.6605e-24;
    /// <summary>Solar luminosity, erg/s (IAU 2015 B3).</summary>
    public const double LSun = 3.828e33;
    /// <summary>M_bol zero-point luminosity, erg/s (IAU 2015 B2); M_bol = -2.5 log10(L / this).</summary>
    public const double MbolZeroPointLum = 3.0128e35;

    // Not in signals section 0.2: CODATA 2018 values (k_B and h c exact in SI; sigma_SB derived from them).
    /// <summary>Stefan-Boltzmann constant, erg cm^-2 s^-1 K^-4 (CODATA 2018).</summary>
    public const double StefanBoltzmann = 5.670374419e-5;
    /// <summary>Electron rest energy m_e c², keV (CODATA 2018).</summary>
    public const double ElectronRestEnergyKeV = 510.99895;
    /// <summary>Classical electron radius r_e, cm (CODATA 2018).</summary>
    public const double ClassicalElectronRadius = 2.8179403262e-13;
    /// <summary>Boltzmann constant, eV/K (CODATA 2018, exact).</summary>
    public const double BoltzmannEv = 8.617333262e-5;
    /// <summary>h c, eV nm (CODATA 2018, exact to the digits given).</summary>
    public const double HcEvNm = 1239.84198;
    /// <summary>Planck constant h, erg s (CODATA 2018, exact: 6.62607015e-34 J s).</summary>
    public const double Planck = 6.62607015e-27;
    /// <summary>Boltzmann constant k_B, erg/K (CODATA 2018, exact: 1.380649e-23 J/K).</summary>
    public const double Boltzmann = 1.380649e-16;
}
