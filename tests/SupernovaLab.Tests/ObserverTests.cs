using SupernovaLab.Core.Observer;

namespace SupernovaLab.Tests;

public class ObserverTests
{
    [Fact]
    public void Default_DistanceModulus_Is15At10Kpc()
        // 5 log10(10000 / 10) = 5 * 3 = 15 exactly.
        => Assert.Equal(15.0, ObserverSetup.Default.DistanceModulus, 6);

    [Fact]
    public void Cosmology_Z0p1_Is418p5Mpc()
    {
        // Same table value as Universe3D's CosmologyTests: z = 0.1 -> 418.5 Mpc, within 0.5%.
        double d = CosmologyModel.Default.ComovingDistanceMpc(0.1);
        Assert.InRange(d, 418.5 * 0.995, 418.5 * 1.005);
    }

    [Fact]
    public void At1Gpc_RedshiftIsAbout0p25()
    {
        var o = new ObserverSetup(1e9, 0, true);
        // 0.248: independent Python integration of dD/dz = (c/H0)/sqrt(0.3(1+z)^3 + 0.7), H0 = 70, bisected for D = 1000 Mpc.
        Assert.InRange(o.Redshift, 0.238, 0.258);
        Assert.Equal(1 + o.Redshift, o.TimeDilation, 12);
        // Luminosity distance = D (1+z), so it exceeds D.
        Assert.True(o.LuminosityDistanceCm > 1e9 * 3.0857e18);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(3.0)]
    [InlineData(10.0)]
    public void Redshift_RoundTripsComovingDistance(double z)
    {
        var c = CosmologyModel.Default;
        Assert.Equal(z, c.RedshiftAtComovingDistance(c.ComovingDistanceMpc(z)), 6);
    }

    [Fact]
    public void ComovingDistance_BeyondTable_MatchesIndependentIntegral()
    {
        // z = 10: 9440.25 Mpc, from an independent Python Simpson integration (H0 = 70, Om = 0.3).
        double d = CosmologyModel.Default.ComovingDistanceMpc(10);
        Assert.InRange(d, 9440.25 * 0.999, 9440.25 * 1.001);
        Assert.InRange(CosmologyModel.Default.RedshiftAtComovingDistance(9440.25), 9.99, 10.01);
    }

    [Fact]
    public void BelowFiftyMpc_NoCosmology()
    {
        var o = new ObserverSetup(49e6, 0, true);
        Assert.Equal(0.0, o.Redshift);
        Assert.Equal(1.0, o.TimeDilation);
        Assert.Equal(49e6 * 3.0857e18, o.LuminosityDistanceCm, 1e12);
    }

    [Fact]
    public void LightTravelYears_10Kpc_IsAbout32600()
        // 10,000 pc x 3.0857e18 cm / (2.99792458e10 cm/s x 3.156e7 s/yr) = 32,613 yr (by hand).
        => Assert.InRange(ObserverSetup.Default.LightTravelYears, 32600, 32630);

    [Fact]
    public void Extinction_V_IsAv() => Assert.Equal(1.0, Extinction.A(Band.V, 1));

    [Fact]
    public void Extinction_Bands_UseSignalsRatios()
    {
        Assert.Equal(1.32, Extinction.A(Band.B, 1), 12);
        Assert.Equal(0.75, Extinction.A(Band.R, 1), 12);
        Assert.Equal(0.48, Extinction.A(Band.I, 1), 12);
        Assert.Equal(3.0, Extinction.A(Band.B, 3.0 / 1.32), 12);
    }

    [Fact]
    public void NhFromAv_Is2p2e21() => Assert.Equal(2.2e22, Extinction.NhFromAv(10), 1e10);

    [Fact]
    public void XrayTransmission_IsPhotoelectricAbsorptionByTheGasColumn()
    {
        // e^(−N_H σ(E)), N_H = 2.2e21 A_V, σ = 2.4e-22 cm² (E / 1 keV)^(−8/3). By hand: A_V = 1 at 1 keV, τ = 0.528,
        // e^−0.528 = 0.589783; at 0.5 keV σ = 2.4e-22 × 0.5^(−8/3) = 1.52390e-21, τ = 3.35259, e^−τ = 0.0349936.
        Assert.Equal(1.0, Extinction.XrayTransmission(1, 0));
        Assert.Equal(0.589783, Extinction.XrayTransmission(1, 1), 6);
        Assert.Equal(0.0349936, Extinction.XrayTransmission(0.5, 1), 7);
    }
}
