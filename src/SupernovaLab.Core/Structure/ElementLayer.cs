namespace SupernovaLab.Core.Structure;

/// <summary>
/// The composition layers the 3D view tags its particles with (structure §S2). Core collapse and pair instability, centre
/// outward: <see cref="NickelIron"/>, <see cref="CarbonOxygenCore"/> (the "metal core": O, Ne, Mg, Si, S),
/// <see cref="Helium"/>, <see cref="Hydrogen"/> (the hydrogen-rich envelope, X_H ≈ 0.45–0.68, not pure hydrogen). Type Ia,
/// centre outward: <see cref="NickelIron"/> (⁵⁶Ni plus stable iron group), <see cref="IntermediateMass"/> (Si-dominated),
/// <see cref="UnburnedCarbonOxygen"/>. The numeric values are uploaded to the GPU as the particle's layer tag.
/// </summary>
public enum ElementLayer
{
    NickelIron,
    CarbonOxygenCore,
    Helium,
    Hydrogen,
    IntermediateMass,
    UnburnedCarbonOxygen,
}
