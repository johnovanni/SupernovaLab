using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Tests;

/// <summary>Review Focus 1: every input combination gives a well-formed outcome.</summary>
public class OutcomeSweepTests
{
    private static readonly OutcomeEngine Engine = new(ExplodabilityTable.LoadEmbedded());

    private static readonly double[] Masses = { 0.8, 1, 5, 8.5, 9.1, 12, 18, 23, 26, 40, 80, 150, 300 };
    private static readonly double[] Metallicities = { 0.001, 0.1, 1, 2 };

    private static IEnumerable<LabInputs> Inputs()
    {
        foreach (double m in Masses)
        foreach (double z in Metallicities)
        foreach (Stripping s in Enum.GetValues<Stripping>())
        foreach (Rotation r in Enum.GetValues<Rotation>())
        foreach (CoreField f in Enum.GetValues<CoreField>())
        foreach (bool csm in new[] { false, true })
            yield return LabInputs.Default with
            {
                InitialMass = m, Metallicity = z, Stripping = s, Rotation = r, Field = f, DenseCsm = csm,
            };

        foreach (TypeIaChannel channel in Enum.GetValues<TypeIaChannel>())
        foreach (double wd in new[] { 0.85, 1.0, 1.38 })
            yield return LabInputs.Default with { Kind = StarKind.WhiteDwarf, IaChannel = channel, WhiteDwarfMass = wd };
    }

    private static IEnumerable<(string Name, Quantity? Q)> Quantities(Outcome o)
    {
        yield return ("E", o.ExplosionEnergyErg);
        yield return ("Mej", o.EjectaMass);
        yield return ("MNi", o.NickelMass);
        yield return ("Lpeak", o.PeakLuminosityErgS);
        yield return ("Emag", o.MagnetarEnergyErg);
        yield return ("tmag", o.MagnetarTimescaleS);
        var r = o.Remnant;
        yield return ("rem.M", r.Mass);
        yield return ("rem.R", r.RadiusKm);
        yield return ("rem.P", r.SpinPeriodS);
        yield return ("rem.B", r.MagneticFieldG);
        yield return ("rem.kick", r.KickKmS);
        yield return ("rem.tBH", r.FormationTimeS);
        if (o.Star is { } s)
        {
            yield return ("Mf", s.FinalMass);
            yield return ("MHe", s.HeCoreMass);
            yield return ("MCO", s.CoCoreMass);
            yield return ("Menv", s.HydrogenEnvelopeMass);
            yield return ("MHeEnv", s.HeliumEnvelopeMass);
            yield return ("R", s.RadiusCm);
            yield return ("Teff", s.SurfaceTemperatureK);
            yield return ("Mdot", s.MassLossRateMsunPerYr);
            yield return ("vwind", s.WindSpeedKmS);
        }
    }

    private static string Describe(LabInputs x) => x.Kind == StarKind.WhiteDwarf
        ? $"Ia {x.IaChannel} M_WD={x.WhiteDwarfMass}"
        : $"M={x.InitialMass} Z={x.Metallicity} {x.Stripping} {x.Rotation} {x.Field} CSM={x.DenseCsm}";

    [Fact]
    public void EveryCombination_GivesAWellFormedOutcome()
    {
        int count = 0;
        foreach (var x in Inputs())
        {
            count++;
            string where = Describe(x);
            var o = Engine.Compute(x);

            foreach (var (name, q) in Quantities(o))
            {
                if (q is null) continue;
                Assert.True(q.Source is not null, $"{name} has no source at {where}");
                Assert.False(string.IsNullOrWhiteSpace(q.Source.Citation), $"{name} citation at {where}");
                Assert.False(string.IsNullOrWhiteSpace(q.Source.ResearchRef), $"{name} research ref at {where}");
                Assert.True(double.IsFinite(q.Value) && q.Value >= 0, $"{name} = {q.Value} at {where}");
                if (q.Low is double lo) Assert.True(double.IsFinite(lo) && lo >= 0, $"{name}.Low = {lo} at {where}");
                if (q.High is double hi) Assert.True(double.IsFinite(hi) && hi >= 0, $"{name}.High = {hi} at {where}");
            }

            // Every note is real text; failed supernovae always explain themselves.
            Assert.All(o.Notes, n => Assert.False(string.IsNullOrWhiteSpace(n), $"empty note at {where}"));
            if (o.Fate == Fate.FailedSupernova) Assert.NotEmpty(o.Notes);

            // The rotation and field inputs are either used or a note says they had no effect.
            if (x.Rotation != Rotation.Slow)
                Assert.True(o.Notes.Any(n => n.Contains("rotation", StringComparison.OrdinalIgnoreCase)),
                    $"no rotation note at {where}");
            if (x.Field == CoreField.Strong)
            {
                bool used = o.Remnant.Kind == RemnantKind.Magnetar ||
                            o.Notes.Any(n => n.Contains("magnetorotational", StringComparison.OrdinalIgnoreCase));
                Assert.True(used || o.Notes.Any(n => n.Contains("magnetic field") && n.Contains("no effect")),
                    $"no field note at {where}");
            }

            // Rulings R15 / R17: ⁵⁶Ni never exceeds half the ejecta in core-collapse and pair-instability explosions;
            // thermonuclear ones (Ia, Iax) keep the published yields, bounded only by Ni < M_ej.
            if (o.NickelMass is { } ni && o.EjectaMass is { } ej)
            {
                if (x.Kind == StarKind.WhiteDwarf)
                    Assert.True(ni.Value < ej.Value, $"Ni {ni.Value} ≥ M_ej {ej.Value} at {where}");
                else
                    Assert.True(ni.Value <= 0.5 * ej.Value + 1e-12, $"Ni {ni.Value} > 0.5 × M_ej {ej.Value} at {where}");
            }
            if (o.Fate is Fate.Supernova or Fate.PairInstabilitySupernova)
            {
                Assert.NotNull(o.NickelMass);
                Assert.NotNull(o.EjectaMass);
                Assert.NotNull(o.ExplosionEnergyErg);
            }

            if (o.HasGrb) Assert.Equal(SupernovaType.IcBL, o.Type);
            if (o.Remnant.Kind == RemnantKind.Magnetar)
                Assert.Equal(CoreField.Strong, x.Field);
        }
        Assert.Equal(13 * 4 * 4 * 3 * 2 * 2 + 3 * 3, count);
    }
}
