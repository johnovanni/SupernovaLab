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
            ("FINGER_ASPECT", CutFaceModel.FingerAspect), ("FINGER_HEAD", CutFaceModel.FingerHead), ("FINGER_HEAD_HEIGHT", CutFaceModel.FingerHeadHeight),
            ("FINGER_NARROW", CutFaceModel.FingerNarrow), ("FINGER_LEAN", CutFaceModel.FingerLean), ("FINGER_GAP", CutFaceModel.FingerGap),
            ("FINGER_JITTER", CutFaceModel.FingerJitter), ("BAND", CutFaceModel.Band),
            ("STALK_NARROW", CutFaceModel.StalkNarrow), ("STALK_ROOT", CutFaceModel.StalkRoot), ("BASE_LENGTH", CutFaceModel.BaseLength),
            ("CAP_WIDTH", CutFaceModel.CapWidth), ("CAP_HEIGHT", CutFaceModel.CapHeight), ("CAP_DROOP0", CutFaceModel.CapDroop0),
            ("CAP_DROOP", CutFaceModel.CapDroop), ("MEANDER_AMP", CutFaceModel.MeanderAmp), ("MEANDER_FREQ", CutFaceModel.MeanderFreq),
            ("RIM_OXYGEN", CutFaceModel.RimOxygen), ("RIM_HELIUM", CutFaceModel.RimHelium), ("TONGUE_MEANDER", CutFaceModel.TongueMeander),
            ("EDGE_LOW_FREQ", CutFaceModel.EdgeLowFreq), ("EDGE_LOW_AMP", CutFaceModel.EdgeLowAmp),
            ("EDGE_HIGH_FREQ", CutFaceModel.EdgeHighFreq), ("EDGE_HIGH_AMP", CutFaceModel.EdgeHighAmp),
            ("BOUNDARY_FREQ", CutFaceModel.BoundaryFreq), ("BOUNDARY_GAIN", CutFaceModel.BoundaryGain), ("BOUNDARY_OUT", CutFaceModel.BoundaryOut),
            ("BOUNDARY_IN", CutFaceModel.BoundaryIn), ("BOUNDARY_FALLOFF", CutFaceModel.BoundaryFalloff),
            ("CLUMP_FREQ", CutFaceModel.ClumpFreq), ("CLUMP_LEVEL", CutFaceModel.ClumpLevel), ("CLUMP_FROM", CutFaceModel.ClumpFrom),
            ("CLUMP_TO", CutFaceModel.ClumpTo), ("CLUMP_SOLID_FROM", CutFaceModel.ClumpSolidFrom), ("CLUMP_SOLID_TO", CutFaceModel.ClumpSolidTo),
        };
        foreach (var (name, value) in pairs)
        {
            var m = System.Text.RegularExpressions.Regex.Match(text, $@"const float {name} = (-?[0-9.]+);");
            Assert.True(m.Success, $"cutface.frag declares {name}");
            Assert.Equal(value, float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
        }
        Assert.Contains($"const uint EDGE_SEED = {CutFaceModel.EdgeSeed}u;", text);
        Assert.Contains($"const uint BOUNDARY_SEED = {CutFaceModel.BoundarySeed}u;", text);
        Assert.Contains($"const uint CLUMP_SEED = {CutFaceModel.ClumpSeed}u;", text);
        Assert.Contains($"const uint BOUNDARY_FINGER_SEED = 0x{CutFaceModel.BoundaryFingerSeed:x8}u;", text);
        // The per-pixel rules, line for line with CutFaceModel.At / InsideBoundary / InBoundaryFinger / InPlume / InHead / InTongue.
        foreach (string needle in new[] {
            "float nn = clamp(BOUNDARY_GAIN * fbm1(BOUNDARY_SEED + uint(k), psi * BOUNDARY_FREQ, 3), -1.0, 1.0);",
            "float rb = b.x * (1.0 + (nn >= 0.0 ? hi : lo) * nn);", "float u = psi / b.w + b2.z;",
            "uint s = hash(BOUNDARY_FINGER_SEED + uint(k) * 7919u + uint(int(cell)));",
            "float x = (u - cell - 0.5 - FINGER_JITTER * (2.0 * unit(s + 1u) - 1.0)) * b.w * b.x;",
            "uint s2 = hash(BOUNDARY_FINGER_SEED + 0x8000u + uint(k) * 7919u + uint(int(j)));",
            "return !inBoundaryFinger(x2, rb - r, b.z, s2, rag);",
            "if (!(len > 0.0) || unit(s + 4u) < FINGER_GAP) return false;", "float l = len * (0.5 + 0.5 * unit(s));",
            "float h = l / (2.0 * FINGER_ASPECT) * (0.8 + 0.25 * unit(s + 2u));",
            "x = abs(x - FINGER_LEAN * (2.0 * unit(s + 3u) - 1.0) * y);",
            "if (y <= capC && x <= h * (1.0 - FINGER_NARROW * y / l) * rag) return true;",
            "float centre = f.x + MEANDER_AMP * w * s * value1(uint(f.w), s * MEANDER_FREQ);",
            "float arc = abs(psi - centre) * r;", "float stalk = f.z * (1.0 - STALK_NARROW * s) * rag;",
            "float hw = stalk * (STALK_ROOT + (1.0 - STALK_ROOT) * sstep(0.0, BASE_LENGTH, s));",
            "float capR = CAP_HEIGHT * CAP_WIDTH * f.z;", "bool dense = clump > -2.0 + (CLUMP_LEVEL + 2.0) * clumpy;",
            "if (r <= capC && arc <= hw + ex * r && (level > 1 || dense)) return level;",
            "if (inHead(arc, r, capW + ex * r, capR + ex * f.y, capC)) return level;",
            "return capC - y <= R * (CAP_DROOP0 + CAP_DROOP * x * x) * sqrt(1.0 - x * x);",
            "float hw = t.z * (0.15 + 0.85 * s) * rag;", "return arc <= hw + rimHe * r ? 2 : 0;",
            "if (l == 1 && off > band) l = rimO > 0.0 ? 2 : 3;", "r <= u_tongueCut",
            "int first = int(cl.z) + c, count = int(cl.w) + 1;",
            "float rag = 1.0 + EDGE_LOW_AMP * fbm2(EDGE_SEED + uint(u_plane), v_face.x * EDGE_LOW_FREQ, v_face.y * EDGE_LOW_FREQ, 2)",
            "+ EDGE_HIGH_AMP * value2(EDGE_SEED + 8u + uint(u_plane), v_face.x * EDGE_HIGH_FREQ, v_face.y * EDGE_HIGH_FREQ);",
            "float clump = fbm2(CLUMP_SEED + uint(u_plane), v_face.x * CLUMP_FREQ, v_face.y * CLUMP_FREQ, 2);",
            "float psi = atan(v_face.x, v_face.y);", "uniform vec4 u_cluster[10];", "uniform vec4 u_finger[20];", "uniform vec4 u_tongue[30];",
            "uniform vec4 u_boundary[6];", "uniform vec4 u_boundary2[6];" })
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
            Assert.Equal(80, p.Fingers.Length);    // u_finger[20]
            Assert.Equal(120, p.Tongues.Length);   // u_tongue[30]
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
                Assert.Equal(cl.Fingers.Count + 1, cl.Tongues.Count);
                for (int j = 0; j < cl.Tongues.Count; j++)
                    Assert.Equal(cl.Tongues[j].CentreRad, p.Tongues[4 * (first + c + j)]);
                first += cl.Fingers.Count;
            }
        }
    }
}
