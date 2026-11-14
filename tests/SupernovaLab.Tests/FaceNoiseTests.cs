using SupernovaLab.Core.Structure;
using SupernovaLab.Rendering;

namespace SupernovaLab.Tests;

/// <summary>Task 16: the hash and value noise the cut faces' fingers, tongues and ragged boundaries are drawn with (cutface.frag mirrors them).</summary>
public class FaceNoiseTests
{
    [Fact]
    public void Hash_IsTheLowbias32Mix()
    {
        // lowbias32 (C. Wellons): x ^= x >> 16; x *= 0x7feb352d; x ^= x >> 15; x *= 0x846ca68b; x ^= x >> 16.
        static uint Reference(uint x)
        {
            unchecked
            {
                x ^= x >> 16; x *= 0x7feb352du; x ^= x >> 15; x *= 0x846ca68bu; x ^= x >> 16;
            }
            return x;
        }
        foreach (uint x in new uint[] { 0, 1, 2, 12345, 0x9E3779B1u, uint.MaxValue })
            Assert.Equal(Reference(x), FaceNoise.Hash(x));
        Assert.NotEqual(FaceNoise.Hash(1), FaceNoise.Hash(2));
    }

    [Fact]
    public void Unit_IsInZeroToOne_AndExactInFloat()
    {
        for (uint i = 0; i < 2000; i++)
        {
            float u = FaceNoise.Unit(i);
            Assert.InRange(u, 0f, 1f);
            Assert.True(u < 1f);
            // 24 bits: exactly representable, so the shader's float(h >> 8) * (1 / 16777216) is the same number.
            Assert.Equal((double)(FaceNoise.Hash(i) >> 8) / 16777216.0, u);
        }
    }

    [Fact]
    public void ValueNoise1_HitsTheLatticeValues_IsContinuous_AndStaysInRange()
    {
        for (int i = -5; i < 5; i++)
            Assert.Equal(2 * FaceNoise.Unit(FaceNoise.Lattice(7, i)) - 1, FaceNoise.Value1(7, i), 6);
        float previous = FaceNoise.Value1(7, -5f);
        for (int k = 1; k <= 10_000; k++)
        {
            float x = -5f + k * 0.001f, v = FaceNoise.Value1(7, x);
            Assert.InRange(v, -1f, 1f);
            Assert.True(Math.Abs(v - previous) < 0.01f, $"jump at {x}");
            previous = v;
        }
        Assert.NotEqual(FaceNoise.Value1(7, 0.5f), FaceNoise.Value1(8, 0.5f));
    }

    [Fact]
    public void ValueNoise2_HitsTheLatticeValues_IsContinuous_AndStaysInRange()
    {
        Assert.Equal(2 * FaceNoise.Unit(FaceNoise.Lattice2(3, 2, -1)) - 1, FaceNoise.Value2(3, 2f, -1f), 6);
        var rng = new Random(4);
        for (int k = 0; k < 5000; k++)
        {
            float x = (float)(rng.NextDouble() * 40 - 20), y = (float)(rng.NextDouble() * 40 - 20);
            float v = FaceNoise.Value2(3, x, y);
            Assert.InRange(v, -1f, 1f);
            Assert.True(Math.Abs(FaceNoise.Value2(3, x + 1e-3f, y) - v) < 0.01f);
            Assert.True(Math.Abs(FaceNoise.Value2(3, x, y + 1e-3f) - v) < 0.01f);
        }
    }

    [Fact]
    public void Fbm_StaysInRange_IsDeterministic_AndIsNotFlat()
    {
        float min = 1, max = -1;
        for (int k = 0; k < 4000; k++)
        {
            float x = k * 0.0137f;
            float a = FaceNoise.Fbm1(11, x, 3), b = FaceNoise.Fbm2(11, x, 0.3f * x, 2);
            Assert.InRange(a, -1f, 1f);
            Assert.InRange(b, -1f, 1f);
            Assert.Equal(a, FaceNoise.Fbm1(11, x, 3));
            min = Math.Min(min, a);
            max = Math.Max(max, a);
        }
        Assert.True(max - min > 0.8f, "the noise varies");
    }

    [Fact]
    public void TheShader_UsesTheSameHashAndLattice()
    {
        var asm = typeof(ParticleRenderer).Assembly;
        string resource = asm.GetManifestResourceNames().Single(n => n.EndsWith(".cutface.frag", StringComparison.Ordinal));
        using var stream = asm.GetManifestResourceStream(resource)!;
        string text = new System.IO.StreamReader(stream).ReadToEnd();
        foreach (string needle in new[] { "x ^= x >> 16u;", "x *= 0x7feb352du;", "x ^= x >> 15u;", "x *= 0x846ca68bu;",
            "uint(i) + seed * 0x9e3779b1u", "uint(i) + hash(uint(j) + seed * 0x9e3779b1u)",
            "float(hash(n) >> 8u) * (1.0 / 16777216.0)", "a + (b - a) * t", "f * f * (3.0 - 2.0 * f)" })
            Assert.Contains(needle, text);
    }

    private static string Frag()
    {
        var asm = typeof(ParticleRenderer).Assembly;
        string resource = asm.GetManifestResourceNames().Single(n => n.EndsWith(".cutface.frag", StringComparison.Ordinal));
        using var stream = asm.GetManifestResourceStream(resource)!;
        return new System.IO.StreamReader(stream).ReadToEnd();
    }

    [Fact]
    public void TheShader_DeclaresCutFaceModelsConstants_WithTheSameValues()
    {
        string text = Frag();
        var pairs = new (string Glsl, float Value)[]
        {
            ("SPREAD", CutFaceModel.Spread), ("STALK_ROOT", CutFaceModel.StalkRoot), ("NECK_START", CutFaceModel.NeckStart), ("STALK_NARROW", CutFaceModel.StalkNarrow), ("STALK_MAX_ANGLE", CutFaceModel.StalkMaxAngle),
            ("CAP_LENGTH", CutFaceModel.CapLength), ("CAP_UNDER", CutFaceModel.CapUnder), ("CAP_WIDTH", CutFaceModel.CapWidth),
            ("WIGGLE_AMP", CutFaceModel.WiggleAmp), ("WIGGLE_FREQ", CutFaceModel.WiggleFreq), ("TONGUE_WIGGLE", CutFaceModel.TongueWiggle),
            ("TONGUE_MAX_ANGLE", CutFaceModel.TongueMaxAngle), ("EDGE_FREQ", CutFaceModel.EdgeFreq), ("RAGGED", CutFaceModel.Ragged),
            ("BOUNDARY_FREQ", CutFaceModel.BoundaryFreq), ("BOUNDARY_OUT", CutFaceModel.BoundaryOut), ("BOUNDARY_IN", CutFaceModel.BoundaryIn),
            ("BOUNDARY_FALLOFF", CutFaceModel.BoundaryFalloff),
        };
        foreach (var (name, value) in pairs)
        {
            var m = System.Text.RegularExpressions.Regex.Match(text, $@"const float {name} = ([0-9.]+);");
            Assert.True(m.Success, $"cutface.frag declares {name}");
            Assert.Equal(value, float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
        }
        Assert.Contains($"const uint EDGE_SEED = {CutFaceModel.EdgeSeed}u;", text);
        Assert.Contains($"const uint BOUNDARY_SEED = {CutFaceModel.BoundarySeed}u;", text);
        // The per-pixel rules, line for line with CutFaceModel.At / InFinger / InTongue.
        foreach (string needle in new[] {
            "float grow = STALK_ROOT + (1.0 - STALK_ROOT) * min(1.0, s / NECK_START);",
            "float neck = 1.0 - STALK_NARROW * max(0.0, s - NECK_START) / (1.0 - NECK_START);",
            "float hw = f.z * grow * neck * rag;", "return d <= min(hw, STALK_MAX_ANGLE * slot);", "float x = d / (CAP_WIDTH * f.z * rag);",
            "float y = (r - capC) / (r >= capC ? capR : CAP_UNDER * capR);", "float hw = t.z * (0.2 + 0.8 * s) * rag;", "return d <= min(hw, TONGUE_MAX_ANGLE * slot);",
            "fbm1(BOUNDARY_SEED + uint(k), psi * BOUNDARY_FREQ, 3)", "fbm2(EDGE_SEED + uint(u_plane), v_face.x * EDGE_FREQ, v_face.y * EDGE_FREQ, 2)",
            "float psi = atan(v_face.x, v_face.y);", "uniform vec4 u_cluster[10];", "uniform vec4 u_finger[50];", "uniform vec4 u_tongue[40];" })
            Assert.Contains(needle, text);
    }

    [Fact]
    public void ThePlaneArrays_PackTheClustersAsTheShaderReadsThem()
    {
        var faces = CutFaceModelTests.SceneFaces(null, 30 * 86400.0);
        for (int plane = 0; plane < 2; plane++)
        {
            var clusters = faces.ClustersOn(plane);
            var p = CutFacePlaneArrays.Pack(faces, plane);
            Assert.Equal(clusters.Count, p.ClusterCount);
            Assert.Equal(4 * CutFaceRenderer.MaxCones, p.Clusters.Length);
            Assert.Equal(200, p.Fingers.Length);   // u_finger[50]
            Assert.Equal(160, p.Tongues.Length);   // u_tongue[40]
            int first = 0;
            for (int c = 0; c < clusters.Count; c++)
            {
                var cl = clusters[c];
                Assert.Equal(cl.CentreRad, p.Clusters[4 * c]);
                Assert.Equal(cl.HalfWidthRad, p.Clusters[4 * c + 1]);
                Assert.Equal(first, (int)p.Clusters[4 * c + 2]);
                Assert.Equal(cl.Fingers.Count, (int)p.Clusters[4 * c + 3]);
                for (int j = 0; j < cl.Fingers.Count; j++)
                {
                    Assert.Equal(cl.Fingers[j].TipRadius, p.Fingers[4 * (first + j) + 1]);
                    Assert.Equal(cl.Fingers[j].Seed, (uint)p.Fingers[4 * (first + j) + 3]);   // 24-bit seeds are exact as floats
                }
                for (int j = 0; j < cl.Tongues.Count; j++)
                    Assert.Equal(cl.Tongues[j].CentreRad, p.Tongues[4 * (first - c + j)]);
                first += cl.Fingers.Count;
            }
        }
    }
}
