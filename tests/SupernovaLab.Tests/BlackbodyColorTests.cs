using SupernovaLab.Rendering;

namespace SupernovaLab.Tests;

/// <summary>Task 16: the blackbody colour of the star and the ejecta (normalized sRGB).</summary>
public class BlackbodyColorTests
{
    [Fact]
    public void SunLike_IsNearWhite()
    {
        var (r, g, b) = BlackbodyColor.FromTemperature(5800);
        Assert.True(r > 0.85 && g > 0.85 && b > 0.85, $"({r}, {g}, {b})");
    }

    [Fact]
    public void Cool_IsRedDominant()
    {
        var (r, g, b) = BlackbodyColor.FromTemperature(3000);
        Assert.True(r > g && g > b, $"({r}, {g}, {b})");
        Assert.Equal(1f, r, 3);
    }

    [Fact]
    public void Hot_IsBlueDominant()
    {
        var (r, g, b) = BlackbodyColor.FromTemperature(30000);
        Assert.True(b > g && g > r, $"({r}, {g}, {b})");
        Assert.Equal(1f, b, 3);
    }

    [Fact]
    public void Channels_AreNormalizedToTheBrightest()
    {
        foreach (double k in new[] { 1000.0, 2000, 4000, 6500, 10000, 50000, 1e6 })
        {
            var (r, g, b) = BlackbodyColor.FromTemperature(k);
            Assert.Equal(1f, Math.Max(r, Math.Max(g, b)), 3);
            Assert.True(r >= 0 && g >= 0 && b >= 0);
        }
    }

    [Fact]
    public void NotAPositiveTemperature_IsWhite()
    {
        Assert.Equal((1f, 1f, 1f), BlackbodyColor.FromTemperature(double.NaN));
        Assert.Equal((1f, 1f, 1f), BlackbodyColor.FromTemperature(0));
    }
}
