using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Outcomes;

/// <summary>
/// Outcomes §8.2 step 6 (thresholds from §3.2): the spectral class of an explosion from the star's envelope at death.
/// </summary>
public static class SpectralTyper
{
    /// <summary>II-P needs an H-rich envelope of at least 2 M☉ (H03). Compared with the envelope mass (ruling R10).</summary>
    public const double PlateauMinEnvelope = 2.0;
    /// <summary>[APPROX] II-L lower edge: an H-rich envelope above 0.5 M☉ (outcomes §8.2 step 6 calls it an approximation).</summary>
    public const double LinearMinEnvelope = 0.5;
    /// <summary>IIb needs at least ~0.001 M☉ of hydrogen itself (Dessart et al. 2011); hydrogen = X_H × envelope (R10).</summary>
    public const double IIbMinHydrogen = 0.001;
    /// <summary>He mass that shows He I lines when ⁵⁶Ni is well mixed (Hachinger et al. 2012): Ib at or above it.</summary>
    public const double HeliumVisibleWellMixed = 0.14;
    /// <summary>With weak ⁵⁶Ni mixing, Ic is allowed up to ~1.6 M☉ of He (Dessart et al. 2012): Ib only above it.</summary>
    public const double HeliumVisibleWeakMixing = 1.6;

    /// <summary>
    /// The class before any dense-CSM override, and a plain sentence explaining it. "He-stripped" (H and He removed)
    /// forces Ic. The visible helium is the He-rich shell above the CO core (<see cref="PresupernovaStar.HeliumEnvelopeMass"/>).
    /// </summary>
    public static (SupernovaType Type, string Note) Classify(PresupernovaStar star, double initialMass, Stripping stripping,
        NickelMixing mixing)
    {
        double envelope = star.HydrogenEnvelopeMass.Value;
        double hydrogen = ProgenitorModel.HydrogenMassFraction(initialMass) * envelope;
        double helium = star.HeliumEnvelopeMass.Value;
        string env = UnitFormat.Mass(envelope);

        if (envelope >= PlateauMinEnvelope)
            return (SupernovaType.IIP, $"It keeps a thick hydrogen envelope ({env}), so it is a Type II-P supernova, " +
                                       "whose light stays on a long plateau.");
        if (envelope > LinearMinEnvelope)
            return (SupernovaType.IIL, $"Its hydrogen envelope is thin ({env}, under 2 M☉), so the light fades without " +
                                       "a long plateau: a Type II-L supernova.");
        if (hydrogen >= IIbMinHydrogen)
            return (SupernovaType.IIb, $"Only about {UnitFormat.Mass(hydrogen)} of hydrogen is left, enough to show " +
                                       "hydrogen lines at first before helium takes over: a Type IIb supernova.");

        if (stripping == Stripping.HydrogenAndHelium)
            return (SupernovaType.Ic, "Its companion removed the helium too, so the spectrum shows neither hydrogen nor " +
                                      "helium: a Type Ic supernova.");

        bool weak = mixing == NickelMixing.Weak;
        bool heliumVisible = weak ? helium > HeliumVisibleWeakMixing : helium >= HeliumVisibleWellMixed;
        string he = UnitFormat.Mass(helium);
        if (heliumVisible)
            return (SupernovaType.Ib, $"It has no hydrogen left but keeps {he} of helium, enough to show helium lines: " +
                                      "a Type Ib supernova.");
        return (SupernovaType.Ic, weak
            ? $"It has no hydrogen, and with weakly mixed nickel its {he} of helium stays hidden (up to about 1.6 M☉ can): " +
              "a Type Ic supernova."
            : $"It has no hydrogen and too little helium ({he}) to show helium lines: a Type Ic supernova.");
    }

    /// <summary>
    /// The "pre-supernova eruption / dense CSM" override (step 6, Speculative as a progenitor mapping): narrow lines from
    /// the surrounding gas. Hydrogen present gives IIn; without hydrogen, Ibn (helium visible) or Icn.
    /// </summary>
    public static SupernovaType WithDenseCsm(SupernovaType type) => type switch
    {
        SupernovaType.IIP or SupernovaType.IIL or SupernovaType.IIb => SupernovaType.IIn,
        SupernovaType.Ib => SupernovaType.Ibn,
        SupernovaType.Ic => SupernovaType.Icn,
        _ => type,
    };

    /// <summary>True for the hydrogen-rich classes whose light comes from a plateau (II-P, II-L).</summary>
    public static bool HasPlateau(SupernovaType type) => type is SupernovaType.IIP or SupernovaType.IIL;
}
