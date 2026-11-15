namespace SupernovaLab.Core.Compact;

/// <summary>
/// A 3-vector of doubles, Core's one vector type (plan ruling P3-2). Core references no OpenTK; Rendering converts this
/// to its own <c>Vector3d</c> at the boundary.
/// </summary>
public readonly record struct Vec3(double X, double Y, double Z)
{
    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3 operator -(Vec3 a) => new(-a.X, -a.Y, -a.Z);
    public static Vec3 operator *(Vec3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);
    public static Vec3 operator *(double s, Vec3 a) => new(a.X * s, a.Y * s, a.Z * s);

    public double Dot(Vec3 b) => X * b.X + Y * b.Y + Z * b.Z;

    public Vec3 Cross(Vec3 b) => new(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X);

    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

    /// <summary>This vector scaled to unit length; the zero vector stays zero (never NaN).</summary>
    public Vec3 Normalized()
    {
        double len = Length;
        return len > 0 ? new Vec3(X / len, Y / len, Z / len) : default;
    }
}
