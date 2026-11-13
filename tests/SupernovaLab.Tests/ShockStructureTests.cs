using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Structure;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Tests;

/// <summary>
/// Task 8: the forward shock, contact surface and reverse shock (structure §S4 and its App block; rulings R-P2-4, R-P2-10,
/// R-P2-11). Expected values are hand-computed from the published formulas (printed in the comments; computed outside the
/// code under test), never with the code under test.
/// </summary>
public class ShockStructureTests
{
    private const double Year = PhysicalConstants.Year, Day = PhysicalConstants.Day;

    private static Outcome Preset(string id)
    {
        var p = PresetCatalog.Get(id);
        return TestOutcomes.Engine.Compute(p.Inputs, p.Pins);
    }

    private static Outcome Default() => TestOutcomes.Engine.Compute(LabInputs.Default);

    // ---------------------------------------------------------------- the published n = 0 trajectories (§S4.2)

    [Theory]
    // TM99 Table 5 (n = 0), t* <= 0.495: R_b = 2.01 t* (1 + 1.72 t*^1.5)^(-2/3), R_r = 1.83 t* (1 + 3.26 t*^1.5)^(-2/3);
    // t* > 0.495: R_b = (1.42 t* - 0.254)^0.4, R_r = t* (0.779 - 0.106 t* - 0.533 ln t*). Hand values (Python):
    [InlineData(0.1, 0.194027, 0.171413)]
    [InlineData(0.495, 0.727612, 0.546276)]
    [InlineData(1.0, 1.063358, 0.673000)]
    [InlineData(2.0, 1.462350, 0.395105)]
    public void TrueloveMcKee_N0_MatchesTable5(double tStar, double forward, double reverse)
    {
        Assert.Equal(forward, ShockStructureModel.TmForwardN0(tStar), 2e-6);
        Assert.Equal(reverse, ShockStructureModel.TmReverseN0(tStar), 2e-6);
    }

    [Theory]
    // TC17 eq. 32 / Table 5 (n = 0): R_c = [(1.826 t*)^-0.89 + (1.66 t*^-0.37)^-0.89]^(-1/0.89); Tables 1, 3:
    // R_b = [(2.01 t*)^-2.5 + (2.026 t*^2)^-0.5]^-0.4. Hand values:
    [InlineData(0.1, 0.170001, 0.191596)]
    [InlineData(1.0, 0.798233, 1.053834)]
    [InlineData(2.0, 0.883970, 1.469301)]
    public void TangChevalier_N0_ContactAndForward(double tStar, double contact, double forward)
    {
        Assert.Equal(contact, ShockStructureModel.TcContactN0(tStar), 2e-6);
        Assert.Equal(forward, ShockStructureModel.TcForwardN0(tStar), 2e-6);
    }

    [Theory]
    // §S4 App block values: q_c = R_c,TC / R_b,TC and q_r = R_r,TM / R_b,TM.
    [InlineData(0.01, 0.905882, 0.909516)]
    [InlineData(0.1, 0.887288, 0.883450)]
    [InlineData(0.3, 0.865535, 0.807462)]
    [InlineData(1.0, 0.757456, 0.632901)]
    [InlineData(2.0, 0.601626, 0.270185)]
    public void RemnantRatios_MatchTheAppBlock(double tStar, double qc, double qr)
    {
        Assert.Equal(qc, ShockStructureModel.RemnantContactRatio(tStar), 2e-6);
        Assert.Equal(qr, ShockStructureModel.RemnantReverseRatio(tStar), 2e-6);
    }

    [Fact]
    public void RemnantContactRatio_IsHeldPastTc17sLimit()
    {
        // TC17's fit is valid to t* = 4; the App block holds q_c at its t* = 4 value, 0.4208 (hand: 0.833600 / 1.980787).
        Assert.Equal(0.420843, ShockStructureModel.RemnantContactRatio(4), 2e-6);
        Assert.Equal(0.420843, ShockStructureModel.RemnantContactRatio(10), 2e-6);
    }

    [Fact]
    public void RemnantReverseRatio_ReachesZeroAt2_58()
    {
        // TM99's n = 0 core formula t* (0.779 - 0.106 t* - 0.533 ln t*) is zero at t* = 2.5811 (bisection, by hand); §S4.2: 2.58.
        Assert.True(ShockStructureModel.RemnantReverseRatio(2.57) > 0);
        Assert.Equal(0, ShockStructureModel.RemnantReverseRatio(2.59));
        Assert.Equal(0, ShockStructureModel.RemnantReverseRatio(5));
    }

    [Fact]
    public void WindRatios_MatchStructureS4_1()
    {
        // §S4.1 table (Chevalier 1982, s = 2): n = 12: R1/Rc 1.2259, R2/Rc 0.9874 -> q_c 1/1.2259 = 0.8157, q_r 0.9874/1.2259 = 0.8054;
        // n = 10.18: 1.2374, 0.9840 -> 0.8081, 0.7952.
        var rsg = ShockStructureModel.WindRatios(EjectaClass.RedSupergiant);
        Assert.Equal(0.8157, rsg.Contact, 3);
        Assert.Equal(0.8054, rsg.Reverse, 3);
        foreach (var c in new[] { EjectaClass.BlueSupergiant, EjectaClass.StrippedEnvelope })
        {
            var r = ShockStructureModel.WindRatios(c);
            Assert.Equal(0.8081, r.Contact, 3);
            Assert.Equal(0.7952, r.Reverse, 3);
        }
    }

    // ---------------------------------------------------------------- the drawn radii (App block)

    [Fact]
    public void Shocks_AtTenYears_ForwardEqualsProject1BlastWave()
    {
        foreach (var o in new[] { Default(), TestOutcomes.TypeIa(), Preset("casa") })
        {
            double t = RemnantEvolutionModel.StartS;
            var s = ShockStructureModel.At(o, t)!;
            double project1 = o.Star!.RadiusCm.Value + RemnantEvolutionModel.RadiusCm(o, t);
            Assert.Equal(1, s.ForwardCm / project1, 9);
            Assert.Equal(1, s.ForwardCm / EjectaKinematics.RadiusCm(o, t), 9);
            // From 10 yr the ratios are the remnant era's: R_c = q_c(t*) R_f.
            double tStar = t / RemnantEvolutionModel.CharacteristicTimeS(o.ExplosionEnergyErg!.Value, o.EjectaMass!.Value, 1);
            Assert.Equal(ShockStructureModel.RemnantContactRatio(tStar), s.ContactCm / s.ForwardCm, 9);
        }
    }

    [Fact]
    public void Shocks_InCsmEra_ForwardEqualsProject1Radius_WithTheWindRatios()
    {
        // Ruling R-P2-11 overrides the brief's "forward = the CSM model's v_s t": the drawn forward shock is Project 1's radius
        // (EjectaKinematics.RadiusCm) in every era, and before 3 yr the contact and reverse shock are Chevalier's s = 2 ratios.
        var o = Default();
        Assert.Equal(EjectaClass.RedSupergiant, EjectaStructureModel.ClassOf(o));
        foreach (double t in new[] { 10 * Day, 100 * Day, Year, 3 * Year })
        {
            var s = ShockStructureModel.At(o, t)!;
            Assert.Equal(EjectaKinematics.RadiusCm(o, t), s.ForwardCm, 6);
            Assert.Equal(1 / 1.2259, s.ContactCm / s.ForwardCm, 9);
            Assert.Equal(0.9874 / 1.2259, s.ReverseCm / s.ForwardCm, 9);
        }
    }

    [Fact]
    public void Shocks_StartAtTheCsmStart_OrAtThreeYearsWithoutAWind()
    {
        // Ruling R-P2-4. The default star has a wind: shocks from the CSM model's start (max(1 d, breakout)).
        var o = Default();
        double start = new CsmInteractionModel().StartTimeS(o);
        Assert.Equal(start, ShockStructureModel.StartTimeS(o));
        Assert.Null(ShockStructureModel.At(o, 0.99 * start));
        Assert.NotNull(ShockStructureModel.At(o, start));
        Assert.Equal(0, ShockStructureModel.At(o, start)!.ConfinementWeight, 12);
        Assert.Equal(0.5, ShockStructureModel.At(o, Math.Sqrt(10) * start)!.ConfinementWeight, 9);
        Assert.Equal(1, ShockStructureModel.At(o, 10 * start)!.ConfinementWeight, 12);
        Assert.Equal(1, ShockStructureModel.At(o, 100 * start)!.ConfinementWeight, 12);

        // A Type Ia (no circumstellar gas): from EjectaKinematics.BlendStartS = 3 yr, with the remnant ratios from the start.
        var ia = TestOutcomes.TypeIa();
        Assert.Equal(EjectaKinematics.BlendStartS, ShockStructureModel.StartTimeS(ia));
        Assert.Null(ShockStructureModel.At(ia, 2.9 * Year));
        var s = ShockStructureModel.At(ia, 3 * Year)!;
        double tStar = 3 * Year / RemnantEvolutionModel.CharacteristicTimeS(ia.ExplosionEnergyErg!.Value, ia.EjectaMass!.Value, 1);
        Assert.Equal(ShockStructureModel.RemnantContactRatio(tStar), s.ContactCm / s.ForwardCm, 9);
        Assert.Equal(0, s.ConfinementWeight, 12);
        Assert.Equal(1, ShockStructureModel.At(ia, 30 * Year)!.ConfinementWeight, 12);

        // No explosion, no shocks.
        Assert.Null(ShockStructureModel.At(TestOutcomes.Star(0.9), 10 * Year));
        Assert.True(double.IsNaN(ShockStructureModel.StartTimeS(TestOutcomes.Star(0.9))));
    }

    [Fact]
    public void Ratios_AreContinuous_AtThreeAndTenYears()
    {
        foreach (var o in new[] { Default(), Preset("casa"), Preset("sn1987a"), TestOutcomes.TypeIa() })
        {
            foreach (double edge in new[] { EjectaKinematics.BlendStartS, RemnantEvolutionModel.StartS })
            {
                var a = ShockStructureModel.Ratios(o, edge * (1 - 1e-9));
                var b = ShockStructureModel.Ratios(o, edge * (1 + 1e-9));
                Assert.Equal(a.Contact, b.Contact, 6);
                Assert.Equal(a.Reverse, b.Reverse, 6);
            }
        }
    }

    [Fact]
    public void ReverseShock_ReachesCentre_AtStructureS4_2Time()
    {
        // E = 1e51 erg, M_ej = 1 M☉, n0 = 1: t_ch = 473 yr × 1^(-1/2) × 1^(5/6) = 473 yr exactly (§S4.2, the lab's units). The
        // n = 0 reverse shock reaches the centre at t* = 2.5811 (root of 0.779 - 0.106 t* - 0.533 ln t*), so at
        // 2.5811 × 473 yr = 1,220.9 yr. Just before that it is still out; just after, at the centre.
        var o = TestOutcomes.Custom(SupernovaType.IIP, 1e51, 1, 0.05, 1e13);
        double tch = 473 * Year;
        Assert.Equal(tch, RemnantEvolutionModel.CharacteristicTimeS(1e51, 1, 1), 6);
        var before = ShockStructureModel.At(o, 2.57 * tch)!;
        var after = ShockStructureModel.At(o, 2.60 * tch)!;
        Assert.True(before.ReverseCm > 0);
        Assert.True(before.ReverseCm < 0.02 * before.ForwardCm);
        Assert.Equal(0, after.ReverseCm);
        Assert.True(after.ContactCm > 0);
    }

    [Fact]
    public void Shocks_AreOrdered_ReverseContactForward()
    {
        // Review Focus 2: the outcome-map strip at two metallicities and the extremes, at log-spaced times from 1 hr to 1,000 yr
        // (and the hand-off edges).
        var outcomes = new List<Outcome>();
        foreach (double z in new[] { 1.0, 0.1 })
        {
            var template = LabInputs.Default with { Metallicity = z };
            foreach (var cell in OutcomeMap.Strip(TestOutcomes.Engine, template))
                outcomes.Add(TestOutcomes.Engine.Compute(template with { InitialMass = cell.InitialMass }));
        }
        outcomes.Add(TestOutcomes.Custom(SupernovaType.Ic, 1e52, 0.1, 0.01, 1e11));     // the fastest, lightest explosion
        outcomes.Add(TestOutcomes.Custom(SupernovaType.IIP, 1e50, 20, 0.01, 7e13));     // the slowest, heaviest
        outcomes.Add(TestOutcomes.TypeIa());
        outcomes.Add(TestOutcomes.Collapsar());
        foreach (string id in new[] { "sn1987a", "casa", "tycho", "sn1998bw", "sn2023ixf", "sn2015bn" }) outcomes.Add(Preset(id));

        var times = new List<double> { EjectaKinematics.BlendStartS, RemnantEvolutionModel.StartS };
        for (int i = 0; i <= 60; i++) times.Add(3600 * Math.Pow(1000 * Year / 3600, i / 60.0));

        int checkedCount = 0;
        foreach (var o in outcomes)
        foreach (double t in times)
        {
            var s = ShockStructureModel.At(o, t);
            if (s is null) continue;
            string what = $"{o.Inputs.InitialMass:0.##} M☉ Z {o.Inputs.Metallicity} {o.Type} at {t / Year:0.####} yr";
            Assert.True(double.IsFinite(s.ForwardCm) && s.ForwardCm > 0, what);
            Assert.True(double.IsFinite(s.ContactCm) && s.ContactCm > 0 && s.ContactCm <= s.ForwardCm, what);
            Assert.True(double.IsFinite(s.ReverseCm) && s.ReverseCm >= 0 && s.ReverseCm <= s.ContactCm, what);
            Assert.InRange(s.ConfinementWeight, 0, 1);
            checkedCount++;
        }
        Assert.True(checkedCount > 5000, $"{checkedCount}");
    }
}
