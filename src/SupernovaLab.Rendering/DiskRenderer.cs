using OpenTK.Mathematics;

namespace SupernovaLab.Rendering;

/// <summary>
/// Plan Task 11 (spec §6.2): the collapsar's accretion disk as the Close-up's lensing pass shades it, and the C# mirror of
/// <c>disk.frag</c> (linked into <c>lensing.frag</c> and <c>lensing_shade.frag</c>, as <c>nsurface.frag</c> is). The disk itself is
/// traced in <c>lensing.frag</c> (<see cref="Core.Compact.BlackHoleOptics.TraceRay"/>'s disk item): a thin, opaque annulus in the
/// equatorial plane from the ISCO of the drawn spin (exact) to MW99's 200 km (from simulations); a ray that meets it ends there and
/// carries the light's exact redshift factor g = 1/(u^t (1 − Ω ℓ)) (<see cref="Core.Compact.BlackHoleOptics.DiskRedshift"/>) and the
/// radius r into the ray map (alpha 5).
/// <para><b>What is schematic.</b> The brightness: g⁴ (exact: the bolometric intensity I/ν⁴ is the same all along a ray) times
/// <see cref="Profile"/>, a thin disk's radial shape normalised to 1 at its peak; and the colour: the blackbody colour
/// (<see cref="BlackbodyColor"/>) of g × <see cref="EmittedTemperatureK"/>, a schematic temperature ∝ profile^¼ peaking at
/// <see cref="PeakTemperatureK"/> (a real collapsar torus is at about 10¹⁰ K and shines in neutrinos; its visible colour would be the
/// same blue-white everywhere). A blackbody seen through a shift g is a blackbody at g × T, so the side turning towards the camera is
/// both brighter and bluer, and the gas near the hole redder (the gravitational redshift).</para>
/// <para><b>The ray map (Task 11's caching choice).</b> The disk's geometry, its inner and outer edge (<see cref="LensingUniforms.DiskInner"/>,
/// <see cref="LensingUniforms.DiskOuter"/>), is part of the rays: a change retraces the map (<see cref="RayMapPlanner.SameRays"/>). It
/// changes only with the drawn spin and mass, which already retrace it (24 steps over the spin-up), and when the disk forms and ends. Its
/// brightness (<see cref="LensingUniforms.DiskLevel"/>: the fade in and out, and the exposure) is applied when the map is sampled, so the
/// fade never retraces. Nothing in the shading depends on the time (no orbiting texture), so nothing flickers.</para>
/// </summary>
public static class DiskRenderer
{
    /// <summary>The profile peaks at this many ISCO radii: u²(1 − √u) with u = r_ISCO/r peaks at √u = 0.8, r = 1.5625 r_ISCO.</summary>
    public const double ProfilePeakIscos = 1.5625;

    /// <summary>[Schematic] The emitted temperature at the profile's peak (K): a white at g = 1.</summary>
    public const double PeakTemperatureK = 6500;

    /// <summary>The colour ramp: <see cref="RampSize"/> blackbody colours from <see cref="RampFirstK"/>, each 2^<see cref="RampStep"/> hotter.</summary>
    public const int RampSize = 12;
    public const double RampFirstK = 1000, RampStep = 0.5;

    /// <summary>
    /// [Presentation] The disk's brightness at the profile's peak at g = 1, in the HDR target's units times the exposure (as the controller's
    /// other levels). Task 11's launch: at 1.0 the approaching side (g⁴ up to about 3) saturated to white over a wide area, hiding its blue,
    /// so 0.6 (not yet seen in a launch); the receding side stays a visible orange.
    /// </summary>
    public const float Level = 0.6f;

    private static readonly double PeakValue = Shape(1 / ProfilePeakIscos);

    private static double Shape(double u) => u * u * (1 - Math.Sqrt(u));

    /// <summary>
    /// [Schematic] The radial brightness profile, 1 at <see cref="ProfilePeakIscos"/> × r_ISCO: (r_ISCO/r)² (1 − √(r_ISCO/r)), the shape of
    /// a thin disk's emission with no torque at its inner edge (0 there; Shakura &amp; Sunyaev 1973's factor), but falling as r⁻² rather than
    /// a thin disk's r⁻³ so that the disk's size (from the simulations) shows. 0 inside the ISCO.
    /// </summary>
    public static double Profile(double r, double isco)
    {
        if (!(isco > 0) || !(r >= isco)) return 0;
        return Shape(isco / r) / PeakValue;
    }

    /// <summary>The bolometric brightness: g⁴ × <see cref="Profile"/> (the exact g⁴ times the schematic profile).</summary>
    public static double Intensity(double g, double r, double isco) => g * g * g * g * Profile(r, isco);

    /// <summary>[Schematic] The emitted temperature: <see cref="PeakTemperatureK"/> × profile^¼ (a thin disk's T ∝ F^¼).</summary>
    public static double EmittedTemperatureK(double r, double isco) => PeakTemperatureK * Math.Pow(Profile(r, isco), 0.25);

    /// <summary>The temperature of the blackbody seen through the shift g: g × <see cref="EmittedTemperatureK"/>.</summary>
    public static double ObservedTemperatureK(double g, double r, double isco) => g * EmittedTemperatureK(r, isco);

    /// <summary>The k-th ramp temperature (K): <see cref="RampFirstK"/> × 2^(k <see cref="RampStep"/>).</summary>
    public static double RampKelvin(int k) => RampFirstK * Math.Pow(2, k * RampStep);

    /// <summary>The sRGB transfer function undone (IEC 61966-2-1): the ramp is in the HDR target's linear units.</summary>
    public static float Linear(float c) => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);

    /// <summary>The ramp's colours: the linearised, normalised blackbody colours of <see cref="RampKelvin"/>.</summary>
    public static IReadOnlyList<Vector3> Ramp { get; } = Enumerable.Range(0, RampSize).Select(k =>
    {
        var (r, g, b) = BlackbodyColor.FromTemperature(RampKelvin(k));
        return new Vector3(Linear(r), Linear(g), Linear(b));
    }).ToArray();

    /// <summary>The ramp's colour at this temperature, linear between entries, clamped at both ends (disk.frag's rampColor).</summary>
    public static Vector3 RampColor(double kelvin)
    {
        double k = Math.Log2(Math.Max(kelvin, RampFirstK) / RampFirstK) / RampStep;
        k = Math.Clamp(k, 0, RampSize - 1);
        int i = (int)Math.Floor(k), j = Math.Min(i + 1, RampSize - 1);
        return Vector3.Lerp(Ramp[i], Ramp[j], (float)(k - i));
    }

    /// <summary>The C# mirror of disk.frag's diskShade at level 1: the ramp colour of the shifted temperature × g⁴ × the profile.</summary>
    public static Vector3 Color(double g, double r, double isco)
        => RampColor(ObservedTemperatureK(g, r, isco)) * (float)Intensity(g, r, isco);

    /// <summary>Sets disk.frag's uniforms (and lensing.frag's annulus) on a program: the edges, the level and the ramp.</summary>
    internal static void Apply(ShaderProgram program, in LensingUniforms u)
    {
        program.Set("u_diskInner", u.DiskInner);
        program.Set("u_diskOuter", u.DiskOuter);
        program.Set("u_diskLevel", u.DiskLevel);
        for (int k = 0; k < RampSize; k++) program.Set(RampNames[k], Ramp[k]);
    }

    private static readonly string[] RampNames = Enumerable.Range(0, RampSize).Select(k => $"u_diskRamp[{k}]").ToArray();
}
