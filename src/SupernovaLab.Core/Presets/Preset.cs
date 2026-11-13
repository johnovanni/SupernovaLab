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
    ObserverSetup Observer, PresetPins? Pins, IReadOnlyList<ObservedFact> Facts, Source? ObserverSource = null)
{
    /// <summary>The guided tour's history lines for this supernova (Project 2, plan ruling P10), in source time; empty when none.</summary>
    public IReadOnlyList<TourLine> TourLines { get; init; } = Array.Empty<TourLine>();
}

/// <summary>
/// One line of a real supernova's history for the guided tour: <see cref="Text"/> happened at <see cref="SourceTimeS"/>
/// seconds after core bounce (or ignition) in the source frame (the tour multiplies by 1 + z). <see cref="Source"/> says
/// where the fact comes from; a fact docs/research/presets.md tags [R] (recalled, not re-opened) keeps that tag in its
/// source text and sets <see cref="Recalled"/>, which the tour shows as "(recalled)". <see cref="Highlight"/> is the
/// instrument panel to brighten while the line is shown, or null. When <see cref="FromBreakout"/> is set, the time is a delay
/// after first light, which the tour places at the lab's own shock breakout (ruling R-P2-31); otherwise it is the time after
/// core bounce.
/// </summary>
public sealed record TourLine(double SourceTimeS, string Text, Source Source, Messenger? Highlight, bool Recalled = false,
    bool FromBreakout = false);
