using SupernovaLab.Rendering;

namespace SupernovaLab.Tests;

/// <summary>Project 2 Task 4: the HDR pipeline's pure-C# parts (no GL context needed).</summary>
public class HdrPipelineTests
{
    [Fact]
    public void HdrPipeline_IsSupported_FalseWhenTheFormatFails()
    {
        Assert.False(HdrPipeline.IsSupported(() => false));
        Assert.False(HdrPipeline.IsSupported(() => throw new InvalidOperationException("no RGBA16F")));
        Assert.True(HdrPipeline.IsSupported(() => true));
    }

    [Fact]
    public void Tonemap_IsMonotonic_AndMapsZeroToZero()
    {
        Assert.Equal(0f, HdrPipeline.Tonemap(0f), 6);
        Assert.Equal(0f, HdrPipeline.Tonemap(-1f));
        float previous = 0f;
        for (float x = 0.01f; x <= 50f; x *= 1.05f)
        {
            float y = HdrPipeline.Tonemap(x);
            Assert.InRange(y, previous, 1f);
            previous = y;
        }
        Assert.Equal(1f, HdrPipeline.Tonemap(1000f));
        // Narkowicz's fit at 1: (2.51 + 0.03) / (2.43 + 0.59 + 0.14) = 2.54 / 3.16.
        Assert.Equal(2.54f / 3.16f, HdrPipeline.Tonemap(1f), 5);
    }

    [Fact]
    public void BloomLevels_ForViewport()
    {
        var levels = HdrPipeline.BloomLevels(1366, 768);
        Assert.Equal(5, levels.Length);
        Assert.All(levels, l => Assert.True(l.Width >= 8 && l.Height >= 8));
        Assert.Equal((683, 384), (levels[0].Width, levels[0].Height));
        Assert.Equal((42, 24), (levels[4].Width, levels[4].Height));
    }

    [Fact]
    public void BloomLevels_StopBeforeAnyLevelGetsSmallerThan8px_AndAZeroViewportHasNone()
    {
        var small = HdrPipeline.BloomLevels(100, 60);
        Assert.InRange(small.Length, 1, 4);
        Assert.All(small, l => Assert.True(l.Width >= 8 && l.Height >= 8));
        Assert.Empty(HdrPipeline.BloomLevels(0, 0));
        Assert.Empty(HdrPipeline.BloomLevels(10, 10));
    }

    [Fact]
    public void QualityLevel_ParticleCounts()
    {
        Assert.Equal(200_000, (int)QualityLevel.High);
        Assert.Equal(100_000, (int)QualityLevel.Medium);
        Assert.Equal(50_000, (int)QualityLevel.Low);
    }
}
