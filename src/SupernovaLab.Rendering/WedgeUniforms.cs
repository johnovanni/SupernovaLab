using OpenTK.Mathematics;
using SupernovaLab.Core.Structure;

namespace SupernovaLab.Rendering;

/// <summary>Sets the wedge frame (<see cref="WedgeOrientation"/>, ruling R-P2-22) on a program that declares u_wedgeX and u_wedgeZ.</summary>
internal static class WedgeUniforms
{
    public static void Set(ShaderProgram program, double phiRad)
    {
        var x = WedgeOrientation.BasisX(phiRad);
        var z = WedgeOrientation.BasisZ(phiRad);
        program.Set("u_wedgeX", new Vector3((float)x.X, (float)x.Y, (float)x.Z));
        program.Set("u_wedgeZ", new Vector3((float)z.X, (float)z.Y, (float)z.Z));
    }
}
