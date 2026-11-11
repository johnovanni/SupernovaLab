namespace SupernovaLab.Core.Progenitors;

/// <summary>Which path the lab follows: a massive star (core collapse) or a white dwarf in a binary (Type Ia).</summary>
public enum StarKind { MassiveStar, WhiteDwarf }

/// <summary>How much of the star a companion has removed (outcomes §8.1, §8.2 step 2).</summary>
public enum Stripping { None, Partial, Full, HydrogenAndHelium }

public enum Rotation { Slow, Fast, Extreme }

/// <summary>Core magnetic field; Strong is magnetar-forming.</summary>
public enum CoreField { Normal, Strong }

public enum TypeIaChannel { NearChandrasekhar, SubChandrasekhar, Iax }

/// <summary>Decides the Ib vs Ic helium threshold (outcomes §8.2 step 6).</summary>
public enum NickelMixing { WellMixed, Weak }

/// <summary>Observed-calibrated or theory ⁵⁶Ni yields (outcomes §8.2 step 5).</summary>
public enum NickelYield { Calibrated, Theory }

/// <summary>
/// The lab's inputs (spec §2.1). Masses in M☉, metallicity as Z/Z☉, opacity in cm² g⁻¹.
/// </summary>
public sealed record LabInputs(StarKind Kind, double InitialMass, double Metallicity, Stripping Stripping,
    Rotation Rotation, CoreField Field, TypeIaChannel IaChannel, double WhiteDwarfMass,
    NickelMixing Mixing, NickelYield Yield, bool DenseCsm, double Opacity)
{
    public const double MinInitialMass = 0.8, MaxInitialMass = 300;
    public const double MinMetallicity = 0.001, MaxMetallicity = 2;
    public const double MinWhiteDwarfMass = 0.85, MaxWhiteDwarfMass = 1.38;
    public const double MinOpacity = 0.05, MaxOpacity = 0.10;

    /// <summary>
    /// A 16 M☉ star at solar metallicity with everything else at its default. 16, not 15, M☉ (ruling R8): in the
    /// S16 N20 data 15.0 M☉ fails to explode, and the default star should explode.
    /// </summary>
    public static LabInputs Default { get; } = new(StarKind.MassiveStar, 16, 1.0, Stripping.None, Rotation.Slow,
        CoreField.Normal, TypeIaChannel.NearChandrasekhar, 1.38, NickelMixing.WellMixed, NickelYield.Calibrated,
        false, 0.07);

    /// <summary>
    /// These inputs with every number held inside its slider range. The white-dwarf mass is fixed at 1.38 M☉ for the
    /// near-Chandrasekhar channel (spec §2.1).
    /// </summary>
    public LabInputs Clamped() => this with
    {
        InitialMass = Math.Clamp(InitialMass, MinInitialMass, MaxInitialMass),
        Metallicity = Math.Clamp(Metallicity, MinMetallicity, MaxMetallicity),
        WhiteDwarfMass = IaChannel == TypeIaChannel.NearChandrasekhar
            ? MaxWhiteDwarfMass
            : Math.Clamp(WhiteDwarfMass, MinWhiteDwarfMass, MaxWhiteDwarfMass),
        Opacity = Math.Clamp(Opacity, MinOpacity, MaxOpacity),
    };
}
