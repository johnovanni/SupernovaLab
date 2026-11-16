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

    /// <summary>
    /// Why the signal has never been observed (compact.md §C10's reason column), for the "Expected" badge's tooltip (Task 14, Task 2's
    /// carried minor). Null when <see cref="Applies"/> is false. The most specific reason wins: the outcome's fate, then the channel, then
    /// the model.
    /// </summary>
    public static string? Reason(string modelId, string? channelId, Outcome o)
    {
        if (!Applies(modelId, channelId, o)) return null;
        if (o.Fate == Fate.PairInstabilitySupernova)
            return "No pair-instability supernova has been confirmed (SN 2007bi and SN 2018ibb are candidates).";
        if (o.Fate == Fate.PulsationalPairInstability)
            return "No pulsational pair-instability event has been confirmed.";
        return modelId switch
        {
            "gw" => "No gravitational waves have been detected from any supernova; the 1987 bar-detector claim for SN 1987A is disputed.",
            "neutrino" when channelId is "lum_nue" or "rate@dune" or "cumulative@dune" =>
                "No electron neutrinos (νe) from a supernova have ever been identified, not even the neutronization burst; " +
                "SN 1987A's 24 neutrinos were electron antineutrinos.",
            "neutrino" when channelId is "lum_nux" => "Muon and tau neutrinos from a supernova have never been separately identified.",
            "neutrino" => "The only supernova neutrinos ever detected (SN 1987A) came from a collapse that left a neutron star; no " +
                          "collapse that made a black hole has been seen in neutrinos.",
            "pulsar" => "No pulsar has been seen in SN 1987A; the evidence for its neutron star is indirect (JWST, Fransson et al. 2024).",
            "lightcurve" => "No failed supernova has been confirmed; N6946-BH1's vanished star is a candidate.",
            _ => "No instance of this signal has been observed from any supernova.",
        };
    }

    /// <summary>The tooltip paragraph for an "Expected" badge: the rule and <see cref="Reason"/>, with its source.</summary>
    public static string? ToolTip(string modelId, string? channelId, Outcome o)
        => Reason(modelId, channelId, o) is { } reason
            ? $"Expected, not detected: this signal has never been observed from any supernova. {reason} (compact.md §C10)"
            : null;
}
