using CommunityToolkit.Mvvm.ComponentModel;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Text;
using SupernovaLab.Core.Units;

namespace SupernovaLab.App.ViewModels;

/// <summary>
/// What changed in the lab: a preset was applied, a star input was edited (which drops a preset and its pins), or an
/// observer input was edited (which keeps the explosion pins but leaves the preset's observed distance and dust, R49).
/// </summary>
public sealed class LabChangedEventArgs(Preset? appliedPreset, string? droppedPresetName, string? observerLeftPresetName = null)
    : EventArgs
{
    /// <summary>The preset just applied, or null for an input edit.</summary>
    public Preset? AppliedPreset { get; } = appliedPreset;

    /// <summary>The name of the preset whose published values this edit dropped, or null.</summary>
    public string? DroppedPresetName { get; } = droppedPresetName;

    /// <summary>
    /// The name of the preset whose published distance and dust this observer edit left behind (its explosion values
    /// still apply), or null.
    /// </summary>
    public string? ObserverLeftPresetName { get; } = observerLeftPresetName;
}

/// <summary>A labelled value for the preset-info window: a published pin or an observed fact.</summary>
public sealed record InfoRow(string Label, string Value, string Citation, string ResearchRef)
{
    public string SourceText => $"{Citation} ({ResearchRef})";
}

/// <summary>
/// The lab's inputs (spec §2): the star (§2.1), the observer (§2.2) and the presets (§2.3). Any edit raises
/// <see cref="Changed"/> once. Rulings R44/R47e/R49: a preset applies its inputs, observer and pins. Editing a star or
/// explosion input afterwards drops the preset and all its pins. Editing only the observer (distance, dust, beam) keeps
/// the preset and its explosion pins, but the observer is no longer the real one. Each change says which happened.
/// </summary>
public sealed partial class LabViewModel : ObservableObject
{
    /// <summary>Distances on the observer slider: 10 pc to 1 Gpc (spec §2.2).</summary>
    public const double MinDistancePc = 10, MaxDistancePc = 1e9;
    public const double MaxAv = 30;

    private bool _applying;
    private bool _observerIsPublished;

    public LabViewModel() : this(PresetCatalog.LoadEmbedded()) { }

    public LabViewModel(IReadOnlyList<Preset> presets)
    {
        Presets = presets;
        var d = LabInputs.Default;
        _kind = d.Kind;
        _initialMass = d.InitialMass;
        _metallicity = d.Metallicity;
        _stripping = d.Stripping;
        _rotation = d.Rotation;
        _field = d.Field;
        _iaChannel = d.IaChannel;
        _whiteDwarfMass = d.WhiteDwarfMass;
        _mixing = d.Mixing;
        _yield = d.Yield;
        _denseCsm = d.DenseCsm;
        _opacity = d.Opacity;
        var o = ObserverSetup.Default;
        _distancePc = o.DistancePc;
        _av = o.Av;
        _beamTowardEarth = o.BeamTowardEarth;
    }

    /// <summary>Raised once per edit or preset selection.</summary>
    public event EventHandler<LabChangedEventArgs>? Changed;

    // ---------------------------------------------------------------- presets (spec §2.3)

    public IReadOnlyList<Preset> Presets { get; }

    private Preset? _selectedPreset;

    /// <summary>
    /// The chosen real supernova. Setting it applies its inputs, observer and pins at once (one <see cref="Changed"/>).
    /// It returns to null when a star input is edited afterwards (ruling R47e); an observer edit keeps it (R49), and
    /// choosing the same preset again then restores its published distance, dust and beam (Task 14 review N2).
    /// </summary>
    public Preset? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (ReferenceEquals(value, _selectedPreset) && (value is null || _observerIsPublished)) return;
            if (value is null)
            {
                string? dropped = _selectedPreset?.Name;
                SetPreset(null);
                Changed?.Invoke(this, new LabChangedEventArgs(null, dropped));
                return;
            }
            Apply(value);
        }
    }

    /// <summary>The selected preset's published explosion values, or null (no preset, or a star input was edited).</summary>
    public PresetPins? Pins => _selectedPreset?.Pins;

    public bool HasPreset => _selectedPreset is not null;

    private void SetPreset(Preset? preset)
    {
        _selectedPreset = preset;
        _observerIsPublished = preset is not null;
        OnPropertyChanged(nameof(SelectedPreset));
        OnPropertyChanged(nameof(HasPreset));
        NotifyObserverSource();
        OnPropertyChanged(nameof(PresetPinRows));
        OnPropertyChanged(nameof(PresetPinSourceText));
        OnPropertyChanged(nameof(PresetFactRows));
        OnPropertyChanged(nameof(PresetObserverText));
        OnPropertyChanged(nameof(PresetObserverSourceText));
    }

    private void Apply(Preset p)
    {
        _applying = true;
        try
        {
            var x = p.Inputs;
            Kind = x.Kind;
            InitialMass = x.InitialMass;
            Metallicity = x.Metallicity;
            Stripping = x.Stripping;
            Rotation = x.Rotation;
            Field = x.Field;
            IaChannel = x.IaChannel;
            WhiteDwarfMass = x.WhiteDwarfMass;
            Mixing = x.Mixing;
            Yield = x.Yield;
            DenseCsm = x.DenseCsm;
            Opacity = x.Opacity;
            DistancePc = p.Observer.DistancePc;
            Av = p.Observer.Av;
            BeamTowardEarth = p.Observer.BeamTowardEarth;
        }
        finally
        {
            _applying = false;
        }
        SetPreset(p);
        Changed?.Invoke(this, new LabChangedEventArgs(p, null));
    }

    /// <summary>A star or explosion input was edited: drop the preset and all its pins, and report the change once.</summary>
    private void Edited()
    {
        if (_applying) return;
        string? dropped = _selectedPreset?.Name;
        if (dropped is not null) SetPreset(null);
        Changed?.Invoke(this, new LabChangedEventArgs(null, dropped));
    }

    /// <summary>
    /// Ruling R49: an observer input was edited. The preset and its explosion pins stay; its published distance and dust
    /// no longer describe the lab's observer. Reported once.
    /// </summary>
    private void ObserverEdited()
    {
        if (_applying) return;
        string? left = null;
        if (_selectedPreset is not null && _observerIsPublished)
        {
            _observerIsPublished = false;
            left = _selectedPreset.Name;
            NotifyObserverSource();
        }
        Changed?.Invoke(this, new LabChangedEventArgs(null, null, left));
    }

    private void NotifyObserverSource()
    {
        OnPropertyChanged(nameof(IsObserverPublished));
        OnPropertyChanged(nameof(ObserverSourceText));
        OnPropertyChanged(nameof(ObserverChangedNote));
    }

    /// <summary>True while the observer is the selected preset's published one.</summary>
    public bool IsObserverPublished => _selectedPreset is not null && _observerIsPublished;

    /// <summary>The source of the preset's distance and dust while they are in use (the badge's tooltip); null otherwise.</summary>
    public string? ObserverSourceText => IsObserverPublished && _selectedPreset!.ObserverSource is { } s
        ? $"Published distance and dust of {_selectedPreset.Name}.\n\nSource: {s.Citation} ({s.ResearchRef})"
        : null;

    /// <summary>For the Preset info window: the preset's real distance and dust.</summary>
    public string? PresetObserverText => _selectedPreset is { } p
        ? $"{UnitFormat.Distance(p.Observer.DistancePc * PhysicalConstants.Pc)}, " +
          $"A_V = {p.Observer.Av.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture)} mag"
        : null;

    /// <summary>The source of the preset's real distance and dust, or null.</summary>
    public string? PresetObserverSourceText => _selectedPreset?.ObserverSource is { } s
        ? $"Source: {s.Citation} ({s.ResearchRef})"
        : null;

    /// <summary>Ruling R49: shown when a preset is selected but its observer was changed; null otherwise.</summary>
    public string? ObserverChangedNote => _selectedPreset is not null && !_observerIsPublished
        ? "The lab's observer has been moved: the explosion still uses the published values, but it is now seen from a " +
          "different distance, through different dust or along a different line of sight than it really was."
        : null;

    /// <summary>The published values the selected preset pins, for the Preset info window.</summary>
    public IReadOnlyList<InfoRow> PresetPinRows => _selectedPreset?.Pins is { } pins ? PinRows(pins) : Array.Empty<InfoRow>();

    /// <summary>The citation of the selected preset's published values, or null.</summary>
    public string? PresetPinSourceText => _selectedPreset?.Pins is { } pins
        ? $"Source: {pins.Source.Citation} ({pins.Source.ResearchRef})"
        : null;

    /// <summary>What was measured for the selected preset, with citations, for the Preset info window.</summary>
    public IReadOnlyList<InfoRow> PresetFactRows => _selectedPreset is { } p
        ? p.Facts.Select(f => new InfoRow(f.Description, (f.IsUpperLimit ? "< " : "") + FactValue(f.Value, f.Unit) + FactTime(f),
            f.Source.Citation, f.Source.ResearchRef)).ToList()
        : Array.Empty<InfoRow>();

    /// <summary>
    /// When a fact was measured: a count accumulates "in the first 12.4 s"; a value with a span covers "over days 45–88
    /// after the explosion"; a value averaged over a window has no single time (its description gives the window);
    /// otherwise "at 84.5 days after the explosion".
    /// </summary>
    internal static string FactTime(ObservedFact f)
    {
        if (f.SourceTimeS is not double t) return "";
        if (f.SourceTimeEndS is double end)
        {
            const double day = PhysicalConstants.Day;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            return t >= day && end < 1000 * day
                ? $" over days {(t / day).ToString("0.#", inv)}–{(end / day).ToString("0.#", inv)} after the explosion"
                : $" from {UnitFormat.Time(t)} to {UnitFormat.Time(end)} after the explosion";
        }
        if (f.ChannelId.StartsWith("cumulative", StringComparison.Ordinal)) return $" in the first {UnitFormat.Time(t)}";
        if (f.Description.Contains("averag", StringComparison.OrdinalIgnoreCase)) return "";
        return $" at {UnitFormat.Time(t)} after the explosion";
    }

    /// <summary>
    /// A measured value with its unit, as published: masses and energies in the outcome's formats (three figures),
    /// otherwise up to three decimals, or scientific notation for very small or large values.
    /// </summary>
    private static string FactValue(double v, string unit) => unit switch
    {
        "M☉" or "erg" or "erg/s" => OutcomeTexts.Format(v, unit),
        _ when v != 0 && (Math.Abs(v) < 0.01 || Math.Abs(v) >= 1e4) => UnitFormat.Scientific(v, unit),
        _ => $"{UnitFormat.Figure(v)} {unit}",
    };

    private static IReadOnlyList<InfoRow> PinRows(PresetPins p)
    {
        var rows = new List<InfoRow>();
        void Add(string label, double? v, string unit)
        {
            if (v is double x) rows.Add(new InfoRow(label, OutcomeTexts.Format(x, unit), p.Source.Citation, p.Source.ResearchRef));
        }
        Add("Explosion energy", p.ExplosionEnergyErg, "erg");
        Add("Ejecta mass", p.EjectaMass, "M☉");
        Add("⁵⁶Ni mass", p.NickelMass, "M☉");
        Add("Progenitor radius", p.RadiusCm, "cm");
        Add("⁴⁴Ti mass", p.Ti44Mass, "M☉");
        Add("Gamma-ray burst isotropic energy", p.GrbEisoErg, "erg");
        Add("Gamma-ray burst peak energy (rest frame)", p.GrbEpKeV, "keV");
        Add("Gamma-ray burst duration T90", p.GrbT90S, "s");
        Add("Neutron star spin period at birth", p.NsSpinPeriodS, "s");
        Add("Neutron star magnetic field", p.NsFieldG, "G");
        Add("Pulsar radio luminosity (1.4 GHz)", p.PulsarRadioLumMjyKpc2, "mJy kpc²");
        Add("Neutron star mass (gravitational)", p.NsGravitationalMass, "M☉");
        if (p.EquatorialRing == true)
            rows.Add(new InfoRow("Equatorial ring", "included", p.Source.Citation, p.Source.ResearchRef));
        Add("Confined circumstellar gas, outer radius (3D view only)", p.ConfinedCsmOuterCm, "cm");
        return rows;
    }

    // ---------------------------------------------------------------- the star (spec §2.1)

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMassiveStar), nameof(IsWhiteDwarf), nameof(IsOpacityEnabled), nameof(OpacityToolTip),
        nameof(OpacityText))]
    private StarKind _kind;

    /// <summary>For the Kind radio buttons.</summary>
    public bool IsMassiveStar
    {
        get => Kind == StarKind.MassiveStar;
        set { if (value) Kind = StarKind.MassiveStar; }
    }

    public bool IsWhiteDwarf
    {
        get => Kind == StarKind.WhiteDwarf;
        set { if (value) Kind = StarKind.WhiteDwarf; }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InitialMassLog), nameof(InitialMassNumber))]
    private double _initialMass;

    /// <summary>The log slider's position: log10 of the initial mass, rounded to three significant figures when set.</summary>
    public double InitialMassLog
    {
        get => Math.Log10(InitialMass);
        set => InitialMass = Math.Clamp(RoundSig(Math.Pow(10, value)), LabInputs.MinInitialMass, LabInputs.MaxInitialMass);
    }

    /// <summary>The mass without its unit; the view draws "M☉" at normal weight beside it.</summary>
    public string InitialMassNumber => UnitFormat.Number(InitialMass);
    public double MinInitialMassLog => Math.Log10(LabInputs.MinInitialMass);
    public double MaxInitialMassLog => Math.Log10(LabInputs.MaxInitialMass);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MetallicityLog), nameof(MetallicityText))]
    private double _metallicity;

    public double MetallicityLog
    {
        get => Math.Log10(Metallicity);
        set => Metallicity = Math.Clamp(RoundSig(Math.Pow(10, value)), LabInputs.MinMetallicity, LabInputs.MaxMetallicity);
    }

    public string MetallicityText => $"{UnitFormat.Number(Metallicity)} × Sun";
    public double MinMetallicityLog => Math.Log10(LabInputs.MinMetallicity);
    public double MaxMetallicityLog => Math.Log10(LabInputs.MaxMetallicity);

    [ObservableProperty] private Stripping _stripping;
    [ObservableProperty] private Rotation _rotation;
    [ObservableProperty] private CoreField _field;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWhiteDwarfMassEnabled), nameof(WhiteDwarfMassNumber), nameof(WhiteDwarfMassSlider))]
    private TypeIaChannel _iaChannel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WhiteDwarfMassNumber), nameof(WhiteDwarfMassSlider))]
    private double _whiteDwarfMass;

    /// <summary>The white-dwarf mass is fixed at 1.38 M☉ for the near-Chandrasekhar channel (spec §2.1).</summary>
    public bool IsWhiteDwarfMassEnabled => IaChannel != TypeIaChannel.NearChandrasekhar;

    /// <summary>The mass in use (1.38 when fixed), without its unit.</summary>
    public string WhiteDwarfMassNumber => UnitFormat.Number(WhiteDwarfMassSlider);

    /// <summary>The slider's value: the mass in use, so the disabled near-Chandrasekhar slider shows 1.38.</summary>
    public double WhiteDwarfMassSlider
    {
        get => IaChannel == TypeIaChannel.NearChandrasekhar ? LabInputs.MaxWhiteDwarfMass : WhiteDwarfMass;
        set
        {
            if (IaChannel != TypeIaChannel.NearChandrasekhar) WhiteDwarfMass = value;
        }
    }

    // ---------------------------------------------------------------- advanced (uncertain physics)

    [ObservableProperty] private NickelMixing _mixing;
    [ObservableProperty] private NickelYield _yield;
    [ObservableProperty] private bool _denseCsm;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OpacityText))]
    private double _opacity;

    public string OpacityText => IsOpacityEnabled
        ? $"{Opacity.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)} cm²/g"
        : "0.19 cm²/g (fixed)";

    /// <summary>Ruling R31: Type Ia/Iax use a fixed effective κ = 0.19 cm²/g, so the slider is off for white dwarfs.</summary>
    public bool IsOpacityEnabled => Kind == StarKind.MassiveStar;

    /// <summary>The slider's tooltip: what κ is, or (for white dwarfs) why it is fixed.</summary>
    public string OpacityToolTip => IsOpacityEnabled ? Glossary.Opacity : Glossary.OpacityFixedForIa;

    // ---------------------------------------------------------------- the observer (spec §2.2)

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DistanceLog), nameof(DistanceText), nameof(DistanceShortText), nameof(KCorrectionNote),
        nameof(RedshiftText))]
    private double _distancePc;

    public double DistanceLog
    {
        get => Math.Log10(DistancePc);
        set => DistancePc = Math.Clamp(RoundSig(Math.Pow(10, value)), MinDistancePc, MaxDistancePc);
    }

    public double MinDistanceLog => Math.Log10(MinDistancePc);
    public double MaxDistanceLog => Math.Log10(MaxDistancePc);
    public string DistanceText => UnitFormat.Distance(DistancePc * PhysicalConstants.Pc);

    /// <summary>"51.4 kpc · 168,000 ly", short enough for the label row; the tooltip has <see cref="DistanceText"/>.</summary>
    public string DistanceShortText
    {
        get
        {
            string pc = DistanceText;
            int paren = pc.IndexOf(" (", StringComparison.Ordinal);
            if (paren > 0) pc = pc[..paren];
            double ly = DistancePc * PhysicalConstants.Pc / (PhysicalConstants.C * PhysicalConstants.Year);
            string lyText = ly >= 1e9 ? $"{UnitFormat.Number(ly / 1e9)} billion"
                : ly >= 1e6 ? $"{UnitFormat.Number(ly / 1e6)} million"
                : UnitFormat.Number(ly);
            return $"{pc} · {lyText} ly";
        }
    }

    /// <summary>The landmark distances of spec §2.2, for the "jump to" dropdown and the slider's ticks.</summary>
    public IReadOnlyList<Choice<double>> DistanceLandmarks { get; } = new Choice<double>[]
    {
        new(200, "Betelgeuse (0.2 kpc)"),
        new(8_200, "Galactic Center (8.2 kpc)"),
        new(50_000, "Large Magellanic Cloud (50 kpc)"),
        new(780_000, "Andromeda (0.78 Mpc)"),
        new(8.6e6, "M51 (8.6 Mpc)"),
        new(16.5e6, "Virgo Cluster (16.5 Mpc)"),
    };

    /// <summary>Spec §2.2: above 50 Mpc the app says the K-correction is not modeled; null below.</summary>
    public string? KCorrectionNote => Observer.Redshift > 0
        ? "Beyond 50 Mpc the shift of light between bands (K-correction) is not modeled."
        : null;

    /// <summary>The redshift and the time stretch it brings, above 50 Mpc; null below.</summary>
    public string? RedshiftText
    {
        get
        {
            double z = Observer.Redshift;
            return z > 0
                ? $"Redshift z = {z.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)}: times are stretched " +
                  $"by 1 + z = {(1 + z).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)}."
                : null;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AvText))]
    private double _av;

    public string AvText => $"A_V = {Av.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} mag";

    /// <summary>The dust presets of spec §2.2.</summary>
    public IReadOnlyList<Choice<double>> DustPresets { get; } = new Choice<double>[]
    {
        new(0, "None (0)"),
        new(0.1, "Looking out of the Galaxy (0.1)"),
        new(2, "Across the Galactic disk (2)"),
        new(30, "Toward the Galactic Center (30)"),
    };

    [ObservableProperty] private bool _beamTowardEarth;

    // ---------------------------------------------------------------- dropdown choices

    public IReadOnlyList<Choice<Stripping>> StrippingChoices { get; } = new Choice<Stripping>[]
    {
        new(Stripping.None, "None"),
        new(Stripping.Partial, "Partial (a little hydrogen left)"),
        new(Stripping.Full, "Full (hydrogen gone)"),
        new(Stripping.HydrogenAndHelium, "Hydrogen and helium gone"),
    };

    public IReadOnlyList<Choice<Rotation>> RotationChoices { get; } = new Choice<Rotation>[]
    {
        new(Rotation.Slow, "Slow"), new(Rotation.Fast, "Fast"), new(Rotation.Extreme, "Extreme"),
    };

    public IReadOnlyList<Choice<CoreField>> FieldChoices { get; } = new Choice<CoreField>[]
    {
        new(CoreField.Normal, "Normal"), new(CoreField.Strong, "Strong (magnetar-forming)"),
    };

    public IReadOnlyList<Choice<TypeIaChannel>> IaChannelChoices { get; } = new Choice<TypeIaChannel>[]
    {
        new(TypeIaChannel.NearChandrasekhar, "Near-Chandrasekhar"),
        new(TypeIaChannel.SubChandrasekhar, "Sub-Chandrasekhar"),
        new(TypeIaChannel.Iax, "Type Iax"),
    };

    public IReadOnlyList<Choice<NickelMixing>> MixingChoices { get; } = new Choice<NickelMixing>[]
    {
        new(NickelMixing.WellMixed, "Well mixed"), new(NickelMixing.Weak, "Weak"),
    };

    public IReadOnlyList<Choice<NickelYield>> YieldChoices { get; } = new Choice<NickelYield>[]
    {
        new(NickelYield.Calibrated, "Calibrated to observations"), new(NickelYield.Theory, "Theory"),
    };

    // ---------------------------------------------------------------- the result

    /// <summary>The star inputs as the engine takes them.</summary>
    public LabInputs Inputs => new(Kind, InitialMass, Metallicity, Stripping, Rotation, Field, IaChannel, WhiteDwarfMass,
        Mixing, Yield, DenseCsm, Opacity);

    public ObserverSetup Observer => new(DistancePc, Av, BeamTowardEarth);

    // Every star or explosion input edit goes through Edited (ruling R47e).
    partial void OnKindChanged(StarKind value) => Edited();
    partial void OnInitialMassChanged(double value) => Edited();
    partial void OnMetallicityChanged(double value) => Edited();
    partial void OnStrippingChanged(Stripping value) => Edited();
    partial void OnRotationChanged(Rotation value) => Edited();
    partial void OnFieldChanged(CoreField value) => Edited();
    partial void OnIaChannelChanged(TypeIaChannel value) => Edited();
    partial void OnWhiteDwarfMassChanged(double value) => Edited();
    partial void OnMixingChanged(NickelMixing value) => Edited();
    partial void OnYieldChanged(NickelYield value) => Edited();
    partial void OnDenseCsmChanged(bool value) => Edited();
    partial void OnOpacityChanged(double value) => Edited();
    // Ruling R49: observer edits keep the explosion pins.
    partial void OnDistancePcChanged(double value) => ObserverEdited();
    partial void OnAvChanged(double value) => ObserverEdited();
    partial void OnBeamTowardEarthChanged(bool value) => ObserverEdited();

    /// <summary>Rounds to three significant figures, so slider values read as plain numbers (16.2, not 16.2183…).</summary>
    internal static double RoundSig(double v)
    {
        if (v == 0 || !double.IsFinite(v)) return v;
        double scale = Math.Pow(10, 2 - (int)Math.Floor(Math.Log10(Math.Abs(v))));
        return Math.Round(v * scale, MidpointRounding.AwayFromZero) / scale;
    }
}
