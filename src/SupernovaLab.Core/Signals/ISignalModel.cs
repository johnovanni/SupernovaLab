using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Signals;

/// <summary>The six messengers or bands, one instrument panel each (spec §5.1).</summary>
public enum Messenger { GravitationalWaves, Neutrinos, GammaRays, XRaysUv, VisibleLight, Radio }

/// <summary>
/// One plotted quantity of a model. A detector-dependent channel's Id carries the detector after '@'
/// (<see cref="ChannelId"/>), e.g. "rate@superk".
/// </summary>
public sealed record SignalChannel(string Id, string Label, string Unit, bool LogScale);

/// <summary>
/// A signal model (spec §5, §7). Models are pure functions of their inputs, with no state, and take source time:
/// seconds after core bounce (or ignition for a Type Ia). Every implementation is a public sealed class with a public
/// parameterless constructor, so the contract tests find and construct them by reflection.
/// </summary>
public interface ISignalModel
{
    /// <summary>A short stable identifier, e.g. "neutrino".</summary>
    string Id { get; }
    Messenger Messenger { get; }
    IReadOnlyList<SignalChannel> Channels { get; }
    Confidence Confidence { get; }
    IReadOnlyList<Source> Sources { get; }

    /// <summary>Whether this outcome produces the signal at all.</summary>
    bool Applies(Outcome outcome);

    /// <summary>The source-time range (s) over which the model is valid.</summary>
    (double StartS, double EndS) ValidRange(Outcome outcome);

    /// <summary>
    /// The channel's value at this source time. NaN outside <see cref="ValidRange"/> or when the model does not apply.
    /// Throws <see cref="ArgumentException"/> for a channel the model does not have.
    /// </summary>
    double Sample(Outcome outcome, ObserverSetup observer, double sourceTimeS, string channelId);

    /// <summary>Whether this detector sees the signal at this source time, with a plain-language reason.</summary>
    Detection Detect(Outcome outcome, ObserverSetup observer, double sourceTimeS, Detector detector);
}
