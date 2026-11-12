using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Presets;

/// <summary>
/// Something that was actually measured for a real supernova, for a panel to mark (spec §2.3). <see cref="ChannelId"/> is
/// the model channel the measurement compares with (e.g. "appV", "line847", "periodS"), with a detector suffix where the
/// detector has no channel of its own (e.g. "cumulative@kamiokande-ii"); for a property of the outcome rather than a
/// signal it is the <see cref="Outcome"/> member's name (e.g. "Ti44Mass"). <see cref="SourceTimeS"/> is the time after
/// core bounce (or ignition) in the source frame, null when the measurement has no single time or its time is not
/// verified. <see cref="Value"/> is in <see cref="Unit"/>. Only values checked against the paper are given
/// (docs/research/presets-verification.md).
/// </summary>
public sealed record ObservedFact(Messenger Messenger, string ChannelId, double? SourceTimeS, double Value, string Unit,
    string Description, Source Source);

/// <summary>
/// A real supernova (spec §2.3): the lab inputs that best match it, where and how dimmed it is seen
/// (<see cref="Observer"/>, cited by <see cref="ObserverSource"/>), the published values it pins (one author's consistent
/// set, ruling R44), a plain-language <see cref="Summary"/> and "what was actually seen" (<see cref="WhatWasSeen"/>), and
/// the measurements a panel can mark (<see cref="Facts"/>).
/// </summary>
public sealed record Preset(string Id, string Name, string Summary, string WhatWasSeen, LabInputs Inputs,
    ObserverSetup Observer, PresetPins? Pins, IReadOnlyList<ObservedFact> Facts, Source? ObserverSource = null);
