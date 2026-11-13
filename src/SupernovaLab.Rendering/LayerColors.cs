using SupernovaLab.Core.Structure;

namespace SupernovaLab.Rendering;

/// <summary>
/// The schematic element colours (Global Constraints, spec §3.5) the cutaway paints on cut faces and in the legend:
/// hydrogen #D85A30, helium #378ADD, oxygen/silicon (the carbon-oxygen core) #639922, nickel/iron #EF9F27, Type Ia
/// intermediate-mass elements #7F77DD, Type Ia unburned carbon/oxygen #888780.
/// </summary>
public static class LayerColors
{
    private static readonly (string Hex, string Name)[] Table =
    {
        ("#EF9F27", "Nickel/iron"),                  // NickelIron
        ("#639922", "Oxygen/silicon"),               // CarbonOxygenCore
        ("#378ADD", "Helium"),                       // Helium
        ("#D85A30", "Hydrogen"),                     // Hydrogen
        ("#7F77DD", "Intermediate-mass elements"),   // IntermediateMass
        ("#888780", "Unburned carbon/oxygen"),       // UnburnedCarbonOxygen
    };

    /// <summary>The number of layers (the size of the shader's colour array).</summary>
    public const int Count = 6;

    /// <summary>The layer's colour as "#RRGGBB".</summary>
    public static string HexOf(ElementLayer layer) => Table[(int)layer].Hex;

    /// <summary>The layer's plain-language name, for the legend.</summary>
    public static string NameOf(ElementLayer layer) => Table[(int)layer].Name;

    /// <summary>The layer's colour as normalized sRGB (display values, 0 to 1).</summary>
    public static (float R, float G, float B) For(ElementLayer layer)
    {
        string hex = HexOf(layer);
        return (Convert.ToInt32(hex.Substring(1, 2), 16) / 255f, Convert.ToInt32(hex.Substring(3, 2), 16) / 255f,
            Convert.ToInt32(hex.Substring(5, 2), 16) / 255f);
    }

    /// <summary>
    /// The HDR value a cut face is drawn with so that, after the exposure, the tone map and gamma 2.2, it shows the element's
    /// hex colour: the display value raised to 2.2, then the tone map's inverse, then divided by the exposure. Faces are drawn
    /// after the bloom is built (see <see cref="HdrPipeline.End"/>), so the glow of the debris does not wash them out.
    /// </summary>
    public static (float R, float G, float B) FaceHdr(ElementLayer layer, float exposure)
    {
        var (r, g, b) = For(layer);
        return (Hdr(r, exposure), Hdr(g, exposure), Hdr(b, exposure));
    }

    private static float Hdr(float display, float exposure)
        => HdrPipeline.InverseTonemap(MathF.Pow(display, 2.2f)) / exposure;
}
