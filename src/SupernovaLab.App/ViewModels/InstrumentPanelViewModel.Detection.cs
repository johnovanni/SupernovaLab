using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Units;

namespace SupernovaLab.App.ViewModels;

/// <summary>
/// The detection badges (spec §5.1, ruling R50): both show the state at the current time. The panel's badge judges only
/// the selected sub-tab's models for the selected instrument, so it always speaks of the curve beside it; the strip tile's
/// badge is the best current-time verdict of every model of the panel that judges the instrument ("does this instrument
/// see this messenger now").
/// </summary>
public abstract partial class InstrumentPanelViewModel
{
    private Detection _currentDetection;
    private Detection _stripDetection;
    private (string Full, string Short) _badge = ("Not modeled here", "Not modeled");
    private (string Full, string Short) _stripBadge = ("Not modeled here", "Not modeled");

    /// <summary>The panel's badge: the selected instrument's verdict on the selected sub-tab's signal, now.</summary>
    public Detection CurrentDetection
    {
        get => _currentDetection;
        private set
        {
            if (SetProperty(ref _currentDetection, value)) OnPropertyChanged(nameof(DetectionToolTip));
        }
    }

    /// <summary>The tile's badge: the selected instrument's best verdict on any of this panel's signals, now.</summary>
    public Detection StripDetection
    {
        get => _stripDetection;
        private set
        {
            if (SetProperty(ref _stripDetection, value)) OnPropertyChanged(nameof(StripToolTip));
        }
    }

    /// <summary>The panel badge's words: Detected / Too faint at this distance / Blocked by dust / … (spec §5.1).</summary>
    public string DetectionText => ErrorText is not null ? ModelErrorText : _badge.Full;

    /// <summary>The panel badge's shorter words.</summary>
    public string DetectionShortText => ErrorText is not null ? ModelErrorText : _badge.Short;

    /// <summary>The tile badge's words (short).</summary>
    public string StripBadgeText => ErrorText is not null ? ModelErrorText : _stripBadge.Short;

    /// <summary>The panel badge's tooltip: the instrument, the verdict and its reason.</summary>
    public string DetectionToolTip => $"{SelectedInstrument.Name}: {DetectionText}.{Environment.NewLine}{Environment.NewLine}" +
                                      (ErrorText ?? CurrentDetection.Reason);

    /// <summary>The tile's tooltip: the panel, the instrument, the verdict and its reason.</summary>
    public string StripToolTip =>
        $"{Title}: {(ErrorText is not null ? ModelErrorText : _stripBadge.Full)} ({SelectedInstrument.Name}).{Environment.NewLine}" +
        $"{Environment.NewLine}{ErrorText ?? StripDetection.Reason}{Environment.NewLine}{Environment.NewLine}Click to open the panel.";

    private void SetBadges((string Full, string Short) panel, (string Full, string Short) strip)
    {
        bool changed = panel != _badge || strip != _stripBadge;
        _badge = panel;
        _stripBadge = strip;
        if (!changed) return;
        OnPropertyChanged(nameof(DetectionText));
        OnPropertyChanged(nameof(DetectionShortText));
        OnPropertyChanged(nameof(StripBadgeText));
        OnPropertyChanged(nameof(DetectionToolTip));
        OnPropertyChanged(nameof(StripToolTip));
    }

    /// <summary>Both badges show a model error.</summary>
    private void SetError(string message)
    {
        CurrentDetection = StripDetection = new Detection(DetectionStatus.NotModeled, message);
        SetBadges((ModelErrorText, ModelErrorText), (ModelErrorText, ModelErrorText));
    }

    /// <summary>Both verdicts for the selected instrument at the current time (ruling R50).</summary>
    private void JudgeNow()
    {
        var instrument = SelectedInstrument;
        var all = DetectorModels(instrument);
        var tab = TabModels(SelectedSubTab);
        double t = SourceTimeS;
        // Task 15: the list keeps the catalogue's order at every time (it used to be sorted by verdict, so what was listed moved as the
        // verdicts changed); the badges pick the best verdict from it, the first of equals in catalogue order.
        var verdicts = all.Select(m => (Model: m, Verdict: VerdictNow(m, instrument, t), InTab: tab.Contains(m))).ToList();
        JudgedModelIds = verdicts.Select(v => v.Model.Id).ToList();

        // The tile: the best verdict over every model that judges this instrument.
        if (verdicts.Count == 0)
        {
            StripDetection = new Detection(DetectionStatus.NotModeled, $"{instrument.Name} is not modeled in this panel.");
            _stripBadgeModel = null;
        }
        else
        {
            var best = Best(verdicts);
            StripDetection = best.Verdict;
            _stripBadgeModel = best.Model;
        }

        // The panel: only the selected sub-tab's models.
        var own = Best(verdicts.Where(x => x.InTab).ToList());
        (string, string) panelBadge;
        if (own.Model is null)
        {
            var users = Instruments.Where(d => DetectorModels(d).Any(tab.Contains)).Select(d => d.Name).ToList();
            CurrentDetection = new Detection(DetectionStatus.NotModeled,
                $"{instrument.Name} does not observe this view's signal ({SelectedSubTab})" +
                (users.Count > 0 ? $"; {string.Join(", ", users)} {(users.Count == 1 ? "does" : "do")}." : "."));
            panelBadge = ("Not observed by this instrument", "Not observed");
        }
        else
        {
            CurrentDetection = own.Verdict;
            panelBadge = Badge(own.Model, own.Verdict);
        }
        SetBadges(panelBadge, _stripBadgeModel is { } m ? Badge(m, StripDetection) : ("Not modeled here", "Not modeled"));
    }

    private ISignalModel? _stripBadgeModel;

    /// <summary>
    /// The models that judge the selected instrument, in the order the panel keeps them: the catalogue's order, the same at every time
    /// (Task 15), however their verdicts change.
    /// </summary>
    public IReadOnlyList<string> JudgedModelIds { get; private set; } = Array.Empty<string>();

    /// <summary>The best verdict (by <see cref="Rank"/>); of equals, one in the selected sub-tab, then the earliest in the list. Default for an empty list.</summary>
    private static (ISignalModel? Model, Detection Verdict, bool InTab) Best(IReadOnlyList<(ISignalModel Model, Detection Verdict, bool InTab)> list)
    {
        if (list.Count == 0) return default;
        var best = list[0];
        foreach (var x in list)
        {
            int r = Rank(x.Verdict.Status), b = Rank(best.Verdict.Status);
            if (r < b || r == b && x.InTab && !best.InTab) best = x;
        }
        return best;
    }

    /// <summary>
    /// A model's verdict now. A panel may restate a verdict that the model gives for a past moment (the gamma panel: a
    /// burst trigger after the burst is over). Through the per-panel cache.
    /// </summary>
    protected virtual Detection VerdictNow(ISignalModel model, Detector instrument, double sourceTimeS)
        => DetectCached(model, instrument, sourceTimeS);

    /// <summary>
    /// The badge words for a model's verdict, in full and short; a panel may say more (the X-ray panel's filling exposure,
    /// a brief signal that is over). Computed once per time step, so a panel may ask its models here.
    /// </summary>
    protected virtual (string Full, string Short) Badge(ISignalModel model, Detection d) => d.Status switch
    {
        DetectionStatus.Detected => IsNeverObserved(model)
            ? (NeverObserved.BadgeWord, NeverObserved.BadgeWord)
            : ("Detected", "Detected"),
        DetectionStatus.TooFaint => ("Too faint at this distance", "Too faint"),
        DetectionStatus.BlockedByDust => ("Blocked by dust", "Blocked by dust"),
        DetectionStatus.NotPointedAtUs => ("Not pointed at us", "Not pointed at us"),
        DetectionStatus.NotProduced => ("Not produced by this supernova", "Not produced"),
        DetectionStatus.NotYet => ("Not yet", "Not yet"),
        _ => ("Not modeled here", "Not modeled"),
    };

    /// <summary>The badge words for a brief signal that is over, by whether the instrument caught it.</summary>
    protected static (string Full, string Short) OverWords(bool caught, bool neverObserved = false)
        => caught
            ? neverObserved ? ("Over: expected to be detected", "Over: expected") : ("Over: it was detected", "Over: detected")
            : ("Over: too faint at this distance", "Over: too faint");

    /// <summary>
    /// Whether no instance of this model's signal has been observed from any supernova for this outcome (§C10), so a
    /// detection is only "Expected". The badge judges the selected instrument, so the neutrino model's channel is that
    /// instrument's event rate.
    /// </summary>
    protected bool IsNeverObserved(ISignalModel model)
        => NeverObserved.Applies(model.Id, model.Id == "neutrino" ? ChannelId.Compose("rate", SelectedInstrument.Id) : null, Outcome);

    /// <summary>
    /// For a brief signal (the neutrino burst, the gravitational waves) whose model has ended: "Over", with whether the
    /// instrument caught it, from the model's verdict at its last moment. Null while the signal lasts.
    /// </summary>
    protected (string Full, string Short)? OverBadge(ISignalModel model, Detection d)
    {
        if (d.Status != DetectionStatus.NotModeled || !model.Applies(Outcome)) return null;
        double end = model.ValidRange(Outcome).EndS;
        if (!(SourceTimeS > end)) return null;
        return OverWords(DetectCached(model, SelectedInstrument, end).Status == DetectionStatus.Detected, IsNeverObserved(model));
    }

    private static int Rank(DetectionStatus s) => s switch
    {
        DetectionStatus.Detected => 0,
        DetectionStatus.BlockedByDust => 1,
        DetectionStatus.TooFaint => 2,
        DetectionStatus.NotPointedAtUs => 3,
        DetectionStatus.NotYet => 4,
        DetectionStatus.NotModeled => 5,
        _ => 6,
    };

    /// <summary>
    /// Why a model draws nothing now (spec §9, "not modeled here"): its own verdict's reason, asked of the first of this
    /// panel's instruments that it judges; for a model no instrument here judges, its range.
    /// </summary>
    protected virtual string NotDrawnReason(ISignalModel model)
    {
        var detector = Instruments.FirstOrDefault(d => DetectorModels(d).Contains(model));
        if (detector is null)
        {
            if (!model.Applies(Outcome)) return "Not produced by this supernova.";
            var (start, end) = model.ValidRange(Outcome);
            double z1 = Observer.TimeDilation;
            return SourceTimeS < start || SourceTimeS > end
                ? $"Not modeled here: this view covers {UnitFormat.Time(start * z1)} to {UnitFormat.Time(end * z1)} after the explosion."
                : "Not modeled at this time.";
        }
        var d = DetectCached(model, detector, SourceTimeS);
        return d.Status is DetectionStatus.NotModeled or DetectionStatus.NotProduced or DetectionStatus.NotYet
            or DetectionStatus.NotPointedAtUs
            ? d.Reason
            : CurveSpan();
    }

    /// <summary>Where the drawn curves begin and end, for a time when they draw nothing.</summary>
    private string CurveSpan()
    {
        var times = Series.Where(s => !s.IsDots).SelectMany(s => s.ObserverTimesS).ToList();
        if (times.Count == 0) return "Nothing is drawn for this outcome.";
        double first = times.Min(), last = times.Max();
        return TimeS > last ? $"Not modeled here: the curve ends {UnitFormat.Time(last)} after the explosion."
            : TimeS < first ? $"Not modeled here yet: the curve starts {UnitFormat.Time(first)} after the explosion."
            : "Not modeled at this moment (a gap in the curve).";
    }
}
