using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Timeline;

namespace SupernovaLab.Tests;

/// <summary>
/// Review Focus 3: every preset × the extreme observers {10 pc, 1 Gpc, A_V = 30} × every ISignalModel (found by
/// reflection) × 50 log-spaced times across the timeline. Every sample is NaN or finite, never negative where the channel
/// cannot be; magnitudes are finite and inside −30…+60 before the dust in their band (<see cref="DustAllowance"/>); every
/// Detect returns a status with a reason. The preset outcomes are computed once and shared.
/// </summary>
public class ModelObserverSweepTests
{
    private static readonly IReadOnlyList<ISignalModel> Models = typeof(ISignalModel).Assembly.GetTypes()
        .Where(t => typeof(ISignalModel).IsAssignableFrom(t) && !t.IsInterface)
        .OrderBy(t => t.FullName, StringComparer.Ordinal)
        .Select(t => (ISignalModel)Activator.CreateInstance(t)!)
        .ToList();

    private static readonly Dictionary<string, Outcome> Outcomes =
        PresetCatalog.LoadEmbedded().ToDictionary(p => p.Id, p => TestOutcomes.Engine.Compute(p.Inputs, p.Pins));

    private static readonly (string Name, ObserverSetup Observer)[] Observers =
    {
        ("10 pc", new ObserverSetup(10, 0, true)),
        ("1 Gpc", new ObserverSetup(1e9, 0, true)),
        ("A_V 30", new ObserverSetup(10_000, 30, true)),
    };

    /// <summary>50 observer times, log-spaced over the timeline (1 ms to 1,000 yr), ends included.</summary>
    private static readonly double[] ObserverTimes = Enumerable.Range(0, 50)
        .Select(i => TimeAxis.FromFraction(i / 49.0)).ToArray();

    public static TheoryData<string> PresetIds()
    {
        var data = new TheoryData<string>();
        foreach (var p in PresetCatalog.LoadEmbedded()) data.Add(p.Id);
        return data;
    }

    /// <summary>
    /// The dust in the channel's band: magnitudes stay inside −30…+60 before the dust, which can add more. Behind A_V = 30
    /// the near-UV (ULTRASAT, A_UV = 2.6 A_V) loses 78 mag, so a breakout seen there through the Galactic Centre's dust is
    /// near magnitude 80 — finite and correct, just very faint.
    /// </summary>
    private static double DustAllowance(SignalChannel c, ObserverSetup obs) => Extinction.A(BandOf(c), obs.Av);

    /// <summary>
    /// The extinction band of each magnitude channel (review M10). A new magnitude channel fails here until its band is
    /// added, so it never gets the wrong allowance.
    /// </summary>
    private static Band BandOf(SignalChannel c) => c.Id switch
    {
        "absV" or "appV" => Band.V,
        "afterglowOpt" => Band.V, // AB magnitude at 550 nm
        "mag@ultrasat" => Band.Uv, // 230–290 nm
        _ => throw new InvalidOperationException($"Magnitude channel '{c.Id}' has no extinction band in this test; add it."),
    };

    /// <summary>A magnitude channel (not a margin against a limit): its unit is a magnitude and it is not "detect@…".</summary>
    private static bool IsMagnitude(SignalChannel c)
        => c.Unit.EndsWith("mag", StringComparison.Ordinal) && ChannelId.Parse(c.Id).Base != "detect";

    [Theory]
    [MemberData(nameof(PresetIds))]
    public void EveryModel_AtTheExtremeObservers_GivesSaneSamplesAndADetectionVerdict(string presetId)
    {
        var o = Outcomes[presetId];
        foreach (var (name, obs) in Observers)
        foreach (var m in Models)
        foreach (double tObs in ObserverTimes)
        {
            double t = TimeAxis.ToSource(tObs, obs);
            foreach (var c in m.Channels)
            {
                double v = m.Sample(o, obs, t, c.Id);
                string where = $"{presetId}, {name}: {m.Id}/{c.Id} at t = {t:g4} s";
                Assert.True(double.IsNaN(v) || double.IsFinite(v), $"{where} is infinite");
                if (double.IsFinite(v) && !c.MayBeNegative) Assert.True(v >= 0, $"{where} is negative: {v}");
                if (double.IsFinite(v) && IsMagnitude(c))
                    Assert.True(v >= -30 && v <= 60 + DustAllowance(c, obs), $"{where}: magnitude {v}");
            }
            foreach (var d in Detectors.All)
            {
                var det = m.Detect(o, obs, t, d);
                Assert.NotNull(det);
                Assert.True(Enum.IsDefined(det.Status), $"{presetId}, {name}: {m.Id}/{d.Id} status");
                Assert.False(string.IsNullOrWhiteSpace(det.Reason), $"{presetId}, {name}: {m.Id}/{d.Id} at {t:g4} s gave no reason");
            }
        }
    }
}
