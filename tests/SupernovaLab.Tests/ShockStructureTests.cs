using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Structure;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Tests;

/// <summary>
/// Task 8: the forward shock, contact surface and reverse shock (structure §S4; ruling R-P2-24, R-P2-4 for the start).
/// Expected values are hand-computed from the published formulas (printed in the comments; computed outside the code under
/// test, in Python), never with the code under test.
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

    // ---------------------------------------------------------------- the published trajectories (§S4.2)

    [Theory]
    // TM99 Table 5 (n = 0): R_b = 2.01 t (1 + 1.72 t^1.5)^(-2/3) / (1.42 t - 0.254)^0.4 past 0.495; R_r = 1.83 t (1 + 3.26 t^1.5)^(-2/3)
    // / t (0.779 - 0.106 t - 0.533 ln t). Table 7 (n = 7): 1.06 t^(4/7) / (1.42 t - 0.312)^0.4 past 0.732; 0.841 t^(4/7) /
    // t (0.815 - 0.116 t - 0.511 ln t) past 0.363. §S4.2 rebuilds (n = 10, 12): 1.147 t^0.7 / (1.42 t - 0.2935)^0.4 past 0.481,
    // 0.980 t^0.7 / t (0.9506 - 0.192 t - 0.4208 ln t) past 0.220; 1.210 t^0.75 / (1.42 t - 0.2809)^0.4 past 0.424,
    // 1.052 t^0.75 / t (1.0468 - 0.251 t - 0.3573 ln t) past 0.182. Hand values:
    [InlineData(0, 0.1, 0.194027, 0.171413)]
    [InlineData(0, 0.495, 0.727612, 0.546276)]
    [InlineData(0, 1.0, 1.063358, 0.673000)]
    [InlineData(0, 2.0, 1.462350, 0.395105)]
    [InlineData(7, 0.1, 0.284366, 0.225615)]
    [InlineData(7, 0.5, 0.713327, 0.555599)]
    [InlineData(7, 1.0, 1.041876, 0.699000)]
    [InlineData(7, 2.0, 1.449142, 0.457604)]
    [InlineData(10, 0.1, 0.228857, 0.195536)]
    [InlineData(10, 1.0, 1.048800, 0.758600)]
    [InlineData(10, 2.0, 1.453374, 0.549847)]
    [InlineData(12, 0.1, 0.215172, 0.187075)]
    [InlineData(12, 1.0, 1.053476, 0.795800)]
    [InlineData(12, 2.0, 1.456247, 0.594277)]
    public void TrueloveMcKee_MatchesTheTables(int n, double tStar, double forward, double reverse)
    {
        Assert.Equal(forward, ShockStructureModel.TmForward(n, tStar), 2e-6);
        Assert.Equal(reverse, ShockStructureModel.TmReverse(n, tStar), 2e-6);
    }

    [Theory]
    // TC17 eq. 32 / Table 5: R_c = [(zeta t^((n-3)/n))^-alpha + (c t^b)^-alpha]^(-1/alpha), (alpha, b, c, zeta) = (0.89, -0.37,
    // 1.66, 1.826 with t^1) for n = 0, (4.28, -0.11, 1.09, 0.89) n = 7, (2.92, -0.12, 1.07, 1.01) n = 10, (2.35, -0.16, 1.11,
    // 1.08) n = 12. Hand values:
    [InlineData(0, 0.1, 0.170001)]
    [InlineData(0, 1.0, 0.798233)]
    [InlineData(0, 2.0, 0.883970)]
    [InlineData(7, 0.1, 0.238732)]
    [InlineData(7, 1.0, 0.819996)]
    [InlineData(10, 0.1, 0.201287)]
    [InlineData(10, 2.0, 0.918466)]
    [InlineData(12, 0.1, 0.191500)]
    [InlineData(12, 1.0, 0.815042)]
    public void TangChevalier_Contact(int n, double tStar, double contact)
        => Assert.Equal(contact, ShockStructureModel.TcContact(n, tStar), 2e-6);

    [Theory]
    // Roots of TM99's core formulas (bisection, by hand): 2.5811 (n = 0), 2.6812 (7), 2.7411 (10), 2.7372 (12); §S4.2 quotes
    // 2.58, 2.69, 2.74, 2.74 (the App block's test: 2.58 and 2.69 to 0.01).
    [InlineData(0, 2.5811, 2.58)]
    [InlineData(7, 2.6812, 2.69)]
    [InlineData(10, 2.7411, 2.74)]
    [InlineData(12, 2.7372, 2.74)]
    public void ReverseShock_ReachesTheCentre_WhereStructureS4_2Says(int n, double root, double quoted)
    {
        Assert.True(ShockStructureModel.TmReverse(n, root - 0.001) > 0);
        Assert.Equal(0, ShockStructureModel.TmReverse(n, root + 0.001));
        Assert.Equal(0, ShockStructureModel.TmReverse(n, 5));
        Assert.InRange(root, quoted - 0.01, quoted + 0.01);
    }

    [Fact]
    public void CharacteristicScales_UseRho0EqualsN0TimesTheProtonMass()
    {
        // TM99 eq. 1-3 with rho0 = n0 m_p (the lab's convention, signals §7.1; Tang & Chevalier's): 1 M☉, 1e51 erg, n0 = 1:
        // R_ch = (1.989e33 / 1.6726e-24)^(1/3) = 1.05943e19 cm = 3.43342 pc; t_ch = 1e51^-0.5 1.989e33^(5/6) 1.6726e-24^(-1/3)
        // = 473.436 yr (§S4.2: 3.43 pc, 473 yr).
        var (r, t) = ShockStructureModel.CharacteristicScales(1e51, 1, 1);
        Assert.Equal(3.43342, r / PhysicalConstants.Pc, 4);
        Assert.Equal(473.436, t / Year, 2);
        // Scalings: R_ch ∝ M^(1/3) n0^(-1/3); t_ch ∝ E^(-1/2) M^(5/6) n0^(-1/3).
        var (r2, t2) = ShockStructureModel.CharacteristicScales(4e51, 8, 8);
        Assert.Equal(r, r2, r * 1e-12);
        Assert.Equal(t * 0.5 * Math.Pow(8, 5.0 / 6) / 2, t2, t * 1e-12);
    }

    [Fact]
    public void RemnantIndex_IsStructureS4_2sPerClassN()
    {
        Assert.Equal(7, ShockStructureModel.RemnantIndex(EjectaClass.TypeIa));
        Assert.Equal(12, ShockStructureModel.RemnantIndex(EjectaClass.RedSupergiant));
        Assert.Equal(10, ShockStructureModel.RemnantIndex(EjectaClass.BlueSupergiant));
        Assert.Equal(10, ShockStructureModel.RemnantIndex(EjectaClass.StrippedEnvelope));
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

    // ---------------------------------------------------------------- the drawn radii (ruling R-P2-24)

    [Fact]
    public void Shocks_InTheWindEra_TheForwardShockIsTheRadioModelsShock()
    {
        // Wind era: R_f = v_s t, the radio panel's shock (signals §6.1: hydrogen-rich 10,000 km/s at 1e51 erg and 10 M☉, ∝ √(E/M_ej)),
        // with Chevalier's s = 2 ratios for a red supergiant (n = 12).
        var o = Default();
        Assert.Equal(EjectaClass.RedSupergiant, EjectaStructureModel.ClassOf(o));
        double vs = 1e9 * Math.Sqrt(o.ExplosionEnergyErg!.Value / o.EjectaMass!.Value / (1e51 / 10));
        foreach (double t in new[] { 10 * Day, 100 * Day, Year, 3 * Year })
        {
            var s = ShockStructureModel.At(o, t)!;
            Assert.Equal(1, s.ForwardCm / CsmInteractionModel.ShockRadiusCm(o, t), 12);
            Assert.Equal(1, s.ForwardCm / (vs * t), 9);
            Assert.Equal(1 / 1.2259, s.ContactCm / s.ForwardCm, 9);
            Assert.Equal(0.9874 / 1.2259, s.ReverseCm / s.ForwardCm, 9);
        }
    }

    [Fact]
    public void Shocks_InTheRemnantEra_AreTheClassNTrajectories()
    {
        // From 10 yr: TM99 forward and reverse, TC17 contact, for n = 12 (default red supergiant), 10 (Cas A), 7 (Type Ia), in
        // characteristic units with the outcome's E and M_ej and n0 = 1.
        foreach (var (o, n) in new[] { (Default(), 12), (Preset("casa"), 10), (TestOutcomes.TypeIa(), 7) })
        {
            var (rch, tch) = ShockStructureModel.CharacteristicScales(o.ExplosionEnergyErg!.Value, o.EjectaMass!.Value, 1);
            foreach (double yr in new[] { 10.0, 30, 100, 300, 1000 })
            {
                double t = yr * Year, ts = t / tch;
                var s = ShockStructureModel.At(o, t)!;
                Assert.Equal(1, s.ForwardCm / (ShockStructureModel.TmForward(n, ts) * rch), 12);
                Assert.Equal(1, s.ContactCm / (Math.Min(ShockStructureModel.TcContact(n, ts), ShockStructureModel.TmForward(n, ts)) * rch), 12);
                double reverse = ShockStructureModel.TmReverse(n, ts) * rch;
                Assert.Equal(Math.Min(reverse, s.ContactCm), s.ReverseCm, s.ContactCm * 1e-12);
            }
        }
    }

    [Fact]
    public void Shocks_AtTenYears_RunAheadOfProject1sUniformEjectaRadius()
    {
        // Inconsistency #33: for n = 12 at t* ≈ 0.003, TM99's 1.210 t*^0.75 is about 2.6 times the n = 0 form's 2.01 t*.
        var o = Default();
        double t = RemnantEvolutionModel.StartS;
        double ratio = ShockStructureModel.At(o, t)!.ForwardCm / EjectaKinematics.RadiusCm(o, t);
        Assert.InRange(ratio, 2.3, 2.9);
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

        // A Type Ia (no circumstellar gas): from EjectaKinematics.BlendStartS = 3 yr, with the n = 7 remnant solution from the start.
        var ia = TestOutcomes.TypeIa();
        Assert.Equal(EjectaKinematics.BlendStartS, ShockStructureModel.StartTimeS(ia));
        Assert.Null(ShockStructureModel.At(ia, 2.9 * Year));
        var s = ShockStructureModel.At(ia, 3 * Year)!;
        var (rch, tch) = ShockStructureModel.CharacteristicScales(ia.ExplosionEnergyErg!.Value, ia.EjectaMass!.Value, 1);
        Assert.Equal(1, s.ForwardCm / (ShockStructureModel.TmForward(7, 3 * Year / tch) * rch), 12);
        Assert.Equal(0, s.ConfinementWeight, 12);
        Assert.Equal(1, ShockStructureModel.At(ia, 30 * Year)!.ConfinementWeight, 12);

        // No explosion, no shocks.
        Assert.Null(ShockStructureModel.At(TestOutcomes.Star(0.9), 10 * Year));
        Assert.True(double.IsNaN(ShockStructureModel.StartTimeS(TestOutcomes.Star(0.9))));
    }

    [Fact]
    public void Radii_AreContinuous_AtThreeAndTenYears_AndBlendedGeometrically()
    {
        foreach (var o in new[] { Default(), Preset("casa"), Preset("sn1987a"), TestOutcomes.TypeIa() })
        {
            foreach (double edge in new[] { EjectaKinematics.BlendStartS, RemnantEvolutionModel.StartS })
            {
                if (ShockStructureModel.At(o, edge * (1 - 1e-9)) is null) continue;   // a Type Ia starts at 3 yr
                var a = ShockStructureModel.At(o, edge * (1 - 1e-9))!;
                var b = ShockStructureModel.At(o, edge * (1 + 1e-9))!;
                Assert.Equal(1, a.ForwardCm / b.ForwardCm, 6);
                Assert.Equal(1, a.ContactCm / b.ContactCm, 6);
                Assert.Equal(1, a.ReverseCm / b.ReverseCm, 6);
            }
        }
        // Midway in log t between 3 and 10 yr the smoothstep weight is 1/2: R_f = √(v_s t × R_f,TM99(t)).
        var d = Default();
        double tm = Math.Sqrt(3 * 10) * Year;
        Assert.Equal(0.5, ShockStructureModel.BlendWeight(tm), 12);
        var (rch, tch) = ShockStructureModel.CharacteristicScales(d.ExplosionEnergyErg!.Value, d.EjectaMass!.Value, 1);
        double expected = Math.Sqrt(CsmInteractionModel.ShockRadiusCm(d, tm) * ShockStructureModel.TmForward(12, tm / tch) * rch);
        Assert.Equal(1, ShockStructureModel.At(d, tm)!.ForwardCm / expected, 9);
    }

    [Fact]
    public void ReverseLagrangianSpeed_NeverRises_SoAShockedParticleStaysShocked()
    {
        // Ruling R-P2-24: v_m(t) = running minimum of R_r(t') / (t' - t_bo). Across the 3-10 yr join the instantaneous value
        // rises for these outcomes; the running minimum must not, and the shocked speeds must be nested.
        foreach (var o in new[] { Default(), Preset("casa") })
        {
            double tbo = EjectaKinematics.BreakoutTimeS(o), start = ShockStructureModel.StartTimeS(o);
            double previous = double.PositiveInfinity;
            bool rose = false;
            double lastInstant = 0;
            for (int i = 0; i <= 3000; i++)
            {
                double t = start * Math.Pow(100 * Year / start, i / 3000.0);
                var s = ShockStructureModel.At(o, t)!;
                double vm = s.ReverseLagrangianCmS;
                Assert.True(vm <= previous * (1 + 1e-12), $"{o.Type} at {t / Year:0.###} yr: {vm} after {previous}");
                double instant = s.ReverseCm / (t - tbo);
                // Where it matters (w = 1, from 10x the start) it never lies above the present value by more than the grid's
                // interpolation error.
                if (t >= 10 * start)
                    Assert.True(vm <= instant * (1 + 1e-3), $"{o.Type} at {t / Year:0.###} yr: running minimum above the present value");
                if (t > 3 * Year && t < 10 * Year && instant > lastInstant && lastInstant > 0) rose = true;
                lastInstant = instant;
                previous = vm;
            }
            Assert.True(rose, "the test should cover a stretch where the instantaneous value rises");

            // Nested: a particle shocked at t1 is shocked at every later t (w = 1 from 10× the start).
            double vmax = EjectaStructureModel.For(o)!.MaxCmS, r0 = o.Star!.RadiusCm.Value;
            double[] times = { 2 * Year, 4 * Year, 6 * Year, 10 * Year, 30 * Year, 100 * Year };
            for (int k = 0; k + 1 < times.Length; k++)
            for (int j = 1; j <= 100; j++)
            {
                double v = vmax * j / 100;
                bool before = ParticleKinematics.ShockedWeight(v, times[k] - tbo, r0, vmax, ShockStructureModel.At(o, times[k])) > 0;
                bool after = ParticleKinematics.ShockedWeight(v, times[k + 1] - tbo, r0, vmax, ShockStructureModel.At(o, times[k + 1])) > 0;
                Assert.True(!before || after, $"{o.Type}: v = {v / 1e5:0} km/s un-shocked between {times[k] / Year} and {times[k + 1] / Year} yr");
            }
        }
    }

    [Fact]
    public void ReverseShock_ReachesCentre_AtStructureS4_2Time()
    {
        // E = 1e51 erg, M_ej = 1 M☉, n0 = 1, a compact hydrogen-rich star (blue-supergiant class, n = 10): t_ch = 473.436 yr. TM99's
        // n = 10 reverse shock reaches the centre at t* = 2.7411, so at 1,297.7 yr; just before it is still out, just after at 0.
        var o = TestOutcomes.Custom(SupernovaType.IIP, 1e51, 1, 0.05, 1e13);
        Assert.Equal(EjectaClass.BlueSupergiant, EjectaStructureModel.ClassOf(o));
        double tch = 473.436 * Year;
        var before = ShockStructureModel.At(o, 2.735 * tch)!;
        var after = ShockStructureModel.At(o, 2.75 * tch)!;
        Assert.True(before.ReverseCm > 0);
        Assert.True(before.ReverseCm < 0.01 * before.ForwardCm);
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
        for (int i = 0; i <= 30; i++) times.Add(3600 * Math.Pow(1000 * Year / 3600, i / 30.0));

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
            Assert.False(double.IsNaN(s.ReverseLagrangianCmS), what);
            checkedCount++;
        }
        Assert.True(checkedCount > 2500, $"{checkedCount}");
    }
}
