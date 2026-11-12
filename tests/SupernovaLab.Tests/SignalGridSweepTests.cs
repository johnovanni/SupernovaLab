using System.Collections.Concurrent;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Timeline;

namespace SupernovaLab.Tests;

/// <summary>
/// Review M2: every ISignalModel over a trimmed lab grid, so regressions in the extreme outcomes (superluminous and
/// magnetar-boosted events, PPISN Ibn/Icn, ultra-stripped, magnetorotational, pair-instability, every Type Ia channel) are
/// caught, not only the presets'. 6 masses × 3 metallicities × every stripping, rotation, field and dense-CSM option × two
/// ⁵⁶Ni/opacity extremes, plus the Type Ia channels; × 2 extreme observers × 12 log-spaced times. The final review's
/// probe ran a larger grid (4,276 inputs × 4 observers × 30 times) single-threaded in about 4.5 minutes; this one runs the
/// outcomes in parallel (the models are pure) and takes well under a minute.
/// </summary>
public class SignalGridSweepTests
{
    private static readonly IReadOnlyList<ISignalModel> Models = typeof(ISignalModel).Assembly.GetTypes()
        .Where(t => typeof(ISignalModel).IsAssignableFrom(t) && !t.IsInterface)
        .OrderBy(t => t.FullName, StringComparer.Ordinal)
        .Select(t => (ISignalModel)Activator.CreateInstance(t)!)
        .ToList();

    private static readonly (string Name, ObserverSetup Observer)[] Observers =
    {
        ("10 pc", new ObserverSetup(10, 0, true)),
        ("1 Gpc, A_V 30, beam away", new ObserverSetup(1e9, 30, false)),
    };

    private static readonly double[] ObserverTimes = Enumerable.Range(0, 12)
        .Select(i => TimeAxis.FromFraction(i / 11.0)).ToArray();

    private static IEnumerable<LabInputs> Inputs()
    {
        var d = LabInputs.Default;
        foreach (double m in new[] { 9.1, 12, 25, 40, 90, 200 })
        foreach (double z in new[] { 0.01, 0.3, 1.0 })
        foreach (var s in Enum.GetValues<Stripping>())
        foreach (var r in Enum.GetValues<Rotation>())
        foreach (var f in Enum.GetValues<CoreField>())
        foreach (bool csm in new[] { false, true })
        foreach (var (mix, yield, kappa) in new[] { (NickelMixing.WellMixed, NickelYield.Calibrated, 0.05), (NickelMixing.Weak, NickelYield.Theory, 0.10) })
            yield return d with
            {
                InitialMass = m, Metallicity = z, Stripping = s, Rotation = r, Field = f, DenseCsm = csm, Mixing = mix,
                Yield = yield, Opacity = kappa,
            };
        foreach (var ch in Enum.GetValues<TypeIaChannel>())
        foreach (double wd in new[] { 0.85, 1.0, 1.15, 1.38 })
            yield return d with { Kind = StarKind.WhiteDwarf, IaChannel = ch, WhiteDwarfMass = wd };
    }

    private static bool IsMagnitude(SignalChannel c)
        => c.Unit.EndsWith("mag", StringComparison.Ordinal) && ChannelId.Parse(c.Id).Base != "detect";

    private static Band BandOf(SignalChannel c) => c.Id == "mag@ultrasat" ? Band.Uv : Band.V;

    /// <summary>
    /// The brightest sane magnitude: −30 for supernova light (the review's 1e46 erg/s magnetar is M_V −26.4); −40 for a
    /// GRB's optical afterglow, whose early flash can reach M ≈ −38 (GRB 080319B) and which reads about −31.6 at 10 pc
    /// for the lab's classical collapsar burst.
    /// </summary>
    private static double Brightest(SignalChannel c) => c.Id == "afterglowOpt" ? -40 : -30;

    [Fact]
    public void EveryModel_OverTheTrimmedLabGrid_GivesSaneSamplesAndReasons()
    {
        var problems = new ConcurrentBag<string>();
        var inputs = Inputs().ToList();
        Parallel.ForEach(inputs, x =>
        {
            Outcome o;
            try
            {
                o = TestOutcomes.Engine.Compute(x);
            }
            catch (Exception e)
            {
                problems.Add($"{x}: engine threw {e.GetType().Name}: {e.Message}");
                return;
            }
            foreach (var (name, obs) in Observers)
            foreach (var m in Models)
            foreach (double tObs in ObserverTimes)
            {
                double t = TimeAxis.ToSource(tObs, obs);
                string where = $"{x.Kind} {x.InitialMass} M☉ Z {x.Metallicity} {x.Stripping} {x.Rotation} {x.Field} " +
                               $"CSM {x.DenseCsm} {x.Mixing} {x.Yield} κ {x.Opacity} Ia {x.IaChannel} {x.WhiteDwarfMass} ({o.Type}), " +
                               $"{name}: {m.Id} at t = {t:g4} s";
                try
                {
                    foreach (var c in m.Channels)
                    {
                        double v = m.Sample(o, obs, t, c.Id);
                        if (!(double.IsNaN(v) || double.IsFinite(v))) problems.Add($"{where}/{c.Id} is infinite");
                        else if (double.IsFinite(v) && !c.MayBeNegative && v < 0) problems.Add($"{where}/{c.Id} is negative: {v}");
                        else if (double.IsFinite(v) && IsMagnitude(c) &&
                                 !(v >= Brightest(c) && v <= 60 + Extinction.A(BandOf(c), obs.Av)))
                            problems.Add($"{where}/{c.Id}: magnitude {v}");
                    }
                    foreach (var det in Detectors.All)
                    {
                        var d = m.Detect(o, obs, t, det);
                        if (d is null || string.IsNullOrWhiteSpace(d.Reason)) problems.Add($"{where}/{det.Id}: no reason");
                        else if (d.Reason.Contains("NaN", StringComparison.Ordinal) || d.Reason.Contains('∞') ||
                                 d.Reason.Contains("Infinity", StringComparison.Ordinal) ||
                                 d.Reason.Contains("E+", StringComparison.Ordinal))
                            problems.Add($"{where}/{det.Id}: bad number in \"{d.Reason}\"");
                    }
                }
                catch (Exception e)
                {
                    problems.Add($"{where}: threw {e.GetType().Name}: {e.Message}");
                }
            }
        });
        Assert.True(problems.IsEmpty, $"{problems.Count} problems, e.g.:{Environment.NewLine}" +
                                      string.Join(Environment.NewLine, problems.Take(20)));
    }
}
