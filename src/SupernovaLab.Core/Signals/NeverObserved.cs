using SupernovaLab.Core.Outcomes;

namespace SupernovaLab.Core.Signals;

/// <summary>
/// The "never observed" audit (spec §7.1; docs/research/compact.md §C10): a signal says "Expected" instead of "Detected"
/// when no instance of that physical signal has been observed from any supernova. The audit is by physical signal, not by
/// detector. Takes no time: the pulsar panel keeps "Detected" for other outcomes (§C10's note on time).
/// </summary>
public static class NeverObserved
{
    /// <summary>The badge word for a never-observed signal that the lab's instrument would detect.</summary>
    public const string BadgeWord = "Expected";

    /// <summary>
    /// True when <paramref name="channelId"/> of model <paramref name="modelId"/> has never been observed from any
    /// supernova for this outcome (§C10's App-implementation triples). A null channel stands for the model as a whole and
    /// matches only the rules that cover every channel ("*").
    /// </summary>
    public static bool Applies(string modelId, string? channelId, Outcome o)
    {
        ArgumentNullException.ThrowIfNull(modelId);
        ArgumentNullException.ThrowIfNull(o);
        if (o.Fate is Fate.PairInstabilitySupernova or Fate.PulsationalPairInstability) return true;
        switch (modelId)
        {
            case "gw":
                return true;
            case "neutrino":
                return channelId is "lum_nue" or "lum_nux" or "rate@dune" or "cumulative@dune"
                       || o.Remnant.Kind == RemnantKind.BlackHole;
            case "pulsar":
                return o.HasEquatorialRing; // the sn1987a preset's pass-through pin (R41)
            case "lightcurve":
                return o.Fate == Fate.FailedSupernova;
            default:
                return false;
        }
    }
}
