using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Presets;

/// <summary>
/// Something that was actually measured for a real supernova, for a panel to mark (spec §2.3). <see cref="ChannelId"/>
/// takes one of three forms, which a panel must tell apart (review M7; tested in PresetValidationTests):
/// <list type="number">
/// <item>a channel of some <see cref="ISignalModel"/> (e.g. "appV", "line847", "periodS", "ringLx"); the fact's
/// <see cref="Messenger"/> is the panel it belongs on, which can differ from the model's own (the circumstellar model is
/// a Radio model, but its "lx" and "ringLx" go on the X-ray panel);</item>
/// <item>a detector that has no channel, as "cumulative@&lt;detector&gt;": only "cumulative@kamiokande-ii",
/// "cumulative@imb" and "cumulative@baksan" (the SN 1987A neutrino counts);</item>
/// <item>the name of an <see cref="Outcome"/> member, for a property of the outcome rather than a signal (e.g.
/// "Ti44Mass").</item>
/// </list> <see cref="SourceTimeS"/> is the time after
/// core bounce (or ignition) in the source frame, null when the measurement has no single time or its time is not
/// verified. <see cref="Value"/> is in <see cref="Unit"/>. Only values checked against the paper are given
/// (docs/research/presets-verification.md). <see cref="IsUpperLimit"/> marks a non-detection: <see cref="Value"/> is the
/// published upper limit, not a measurement. <see cref="SourceTimeEndS"/>, when given, ends the span of source time that
/// a value covers (an average or a limit over days 45–88), which then starts at <see cref="SourceTimeS"/>.
/// </summary>
public sealed record ObservedFact(Messenger Messenger, string ChannelId, double? SourceTimeS, double Value, string Unit,
    string Description, Source Source, bool IsUpperLimit = false, double? SourceTimeEndS = null);

/// <summary>
/// A real supernova (spec §2.3): the lab inputs that best match it, where and how dimmed it is seen
/// (<see cref="Observer"/>, cited by <see cref="ObserverSource"/>), the published values it pins (one author's consistent
/// set, ruling R44), a plain-language <see cref="Summary"/> and "what was actually seen" (<see cref="WhatWasSeen"/>), and
/// the measurements a panel can mark (<see cref="Facts"/>).
/// </summary>
public sealed record Preset(string Id, string Name, string Summary, string WhatWasSeen, LabInputs Inputs,
    ObserverSetup Observer, PresetPins? Pins, IReadOnlyList<ObservedFact> Facts, Source? ObserverSource = null);
