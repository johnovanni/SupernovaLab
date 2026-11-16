using System.Globalization;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Signals;

namespace SupernovaLab.Core.Compact;

/// <summary>Whether a radio telescope on Earth would receive the pulsar's pulses now (spec §4.4).</summary>
public enum PulseVisibility { Received, HiddenByDebris, BeamMissesEarth, NotAPulsar }

/// <summary>
/// The Close-up's pulse trace and pulsar sound (spec §4.4; plan Task 8): when pulses arrive, at what period, and the words that say
/// so. One rule for the trace, the sound and the radio panel: the beam must sweep Earth (as <see cref="Magnetosphere.BeamCanCrossEarth"/>
/// and the radio panel's NotPointedAtUs decide; this is checked first, as the panel does), and the pulses must have escaped the debris
/// (<see cref="PulsarModel.EmergenceTimeS"/>). Source time throughout (s after bounce).
/// </summary>
public static class PulseSchedule
{
    public const string HiddenText = "To Earth: hidden by debris";
    public const string MissesText = "Beam misses Earth";
    public const string NotAPulsarText = "No pulses: this remnant is not a pulsar";

    private static readonly PulsarModel Pulsar = new();

    /// <summary>The first beam's centre crosses Earth's azimuth (−y, ruling P3-3) when the spin phase is 3π/2; the opposite beam's, π/2.</summary>
    private const double MainBeamPhaseRad = 1.5 * Math.PI, OppositeBeamPhaseRad = 0.5 * Math.PI;

    /// <summary>What a telescope on Earth would receive at this source time.</summary>
    public static PulseVisibility Visibility(Outcome o, ObserverSetup obs, double sourceTimeS)
    {
        double emergence = Pulsar.EmergenceTimeS(o);
        if (double.IsNaN(emergence) || !(sourceTimeS >= 0)) return PulseVisibility.NotAPulsar;
        if (!Magnetosphere.BeamCanCrossEarth(o, obs)) return PulseVisibility.BeamMissesEarth;
        return sourceTimeS >= emergence ? PulseVisibility.Received : PulseVisibility.HiddenByDebris;
    }

    /// <summary>The period a telescope on Earth measures: P(t) (1 + z). NaN for no pulsar.</summary>
    public static double ReceivedPeriodS(Outcome o, ObserverSetup obs, double sourceTimeS)
        => double.IsNaN(Pulsar.EmergenceTimeS(o)) ? double.NaN : PulsarModel.Spin.Of(o).PeriodS(Math.Max(sourceTimeS, 0)) * obs.TimeDilation;

    /// <summary>
    /// The spin phases (radians in [0, 2π), the convention of <c>RemnantRenderer.MagneticAxis</c>) at which a drawn beam's centre
    /// crosses the Earth arrow: the main beam's, and the opposite beam's too when it also reaches Earth (an interpulse). Empty when no
    /// beam can cross.
    /// </summary>
    public static IReadOnlyList<double> CrossingPhasesRad(Outcome o, ObserverSetup obs)
    {
        if (double.IsNaN(Pulsar.EmergenceTimeS(o)) || !Magnetosphere.BeamCanCrossEarth(o, obs)) return Array.Empty<double>();
        var list = new List<double> { MainBeamPhaseRad };
        double zeta = Magnetosphere.EarthAngleRad(o, obs), alpha = Magnetosphere.TiltRad(o);
        double rho = Magnetosphere.BeamHalfAngleRad(PulsarModel.Spin.Of(o).P0);
        if (Math.Abs(zeta - (Math.PI - alpha)) <= rho) list.Add(OppositeBeamPhaseRad);
        return list;
    }

    /// <summary>
    /// The trace's words: "Sound: real rate, 30 a second · Picture: shown 60× slower" while pulses are received, else the reason
    /// ("To Earth: hidden by debris", "Beam misses Earth"). The sound's rate is the received one; the picture's is the drawn one.
    /// </summary>
    public static string TraceText(Outcome o, ObserverSetup obs, double sourceTimeS)
    {
        switch (Visibility(o, obs, sourceTimeS))
        {
            case PulseVisibility.NotAPulsar: return NotAPulsarText;
            case PulseVisibility.HiddenByDebris: return HiddenText;
            case PulseVisibility.BeamMissesEarth: return MissesText;
        }
        double received = ReceivedPeriodS(o, obs, sourceTimeS);
        string picture = Magnetosphere.SpeedLabel(PulsarModel.Spin.Of(o).PeriodS(sourceTimeS));
        return $"Sound: real rate, {RateText(received)} · Picture: {char.ToLowerInvariant(picture[0])}{picture[1..]}";
    }

    /// <summary>"30 a second" (two significant figures) from one a second up; "one every 3.4 s" below it.</summary>
    public static string RateText(double periodS)
    {
        var inv = CultureInfo.InvariantCulture;
        if (!(periodS > 0)) return "no pulses";
        double rate = 1 / periodS;
        if (rate < 1 - 1e-9) return $"one every {periodS.ToString("0.##", inv)} s";
        double scale = Math.Pow(10, Math.Floor(Math.Log10(rate)) - 1);
        double rounded = Math.Round(rate / scale, MidpointRounding.AwayFromZero) * scale;
        return $"{rounded.ToString("N0", inv)} a second";
    }
}
