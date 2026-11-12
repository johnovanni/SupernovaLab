using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Signals;

namespace SupernovaLab.App.ViewModels;

/// <summary>
/// One drawn curve, in observer time (s). A model's NaNs (outside its valid range) are never drawn: a curve with gaps is
/// split into several segments with the same <see cref="Label"/>, so <see cref="Values"/> are always finite, and positive
/// when <see cref="LogY"/>.
/// </summary>
public sealed record PlotSeries(string Label, double[] ObserverTimesS, double[] Values, string Unit, bool LogY)
{
    /// <summary>The model channel drawn (e.g. "appV"), for matching preset facts; null for derived curves.</summary>
    public string? ChannelId { get; init; }

    /// <summary>Drawn as separate dots (the simulated neutrino events), not as a line.</summary>
    public bool IsDots { get; init; }
}

/// <summary>A shaded range between two curves (the neutron star's surface temperature before ~100 yr, signals §8.4).</summary>
public sealed record PlotBand(string Label, double[] ObserverTimesS, double[] Low, double[] High, string Unit);

/// <summary>A labelled straight line: a level (an instrument's limit, an untimed observation) or, when <see cref="IsTime"/>, a time.</summary>
public sealed record ReferenceLine(string Label, double Value, bool IsTime);

/// <summary>A preset's measurement drawn as a marker on the curve of its channel, at its observer time.</summary>
public sealed record FactMarker(ObservedFact Fact, double? ObserverTimeS, string Label);

/// <summary>A text row under a panel's plot: a preset's measurement (marked or not) with its source.</summary>
public sealed record FactNote(string Text, string SourceText);

/// <summary>An instrument in a panel's picker, with its published limit and that limit's confidence (rulings R24, R27).</summary>
public sealed record InstrumentChoice(Detector Detector, string Text, string ToolTip)
{
    public override string ToString() => Text;
}

/// <summary>What a panel's plot shows.</summary>
public enum PlotKind
{
    /// <summary>Curves against log observer time, with the shared "now" cursor.</summary>
    TimeSeries,
    /// <summary>The gravitational-wave spectrogram: frequency against linear time, log colour scale.</summary>
    Spectrogram,
    /// <summary>The schematic spectrum at the current time, against wavelength.</summary>
    Spectrum,
    /// <summary>One second of pulsar pulses at the current time.</summary>
    PulseTrain,
}

/// <summary>
/// The gravitational-wave spectrogram as log10 of the power normalized to 1, held at or above −<see cref="FloorDecades"/>
/// (the bounce bin is about 10⁵ times the track, so a linear scale would show only the bounce). [timeBin, freqBin].
/// </summary>
public sealed record SpectrogramView(double[,] Log10Power, double MaxObserverTimeS, double MaxFrequencyHz)
{
    public const double FloorDecades = 6;
}

/// <summary>The schematic spectrum at the current time (signals §10) and a caption that says what it is.</summary>
public sealed record SpectrumView(double[] WavelengthsAngstrom, double[] RelativeFlux, IReadOnlyList<SpectralLine> Lines,
    double VelocityKmS, string Caption);

/// <summary>One second of pulses (observer time from the first pulse, s), with a caption giving the period and flux.</summary>
public sealed record PulseTrainView(IReadOnlyList<double> PulseOffsetsS, double PeriodS, double WindowS, string Caption);
