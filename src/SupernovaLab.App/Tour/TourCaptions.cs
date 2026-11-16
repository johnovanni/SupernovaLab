using System.Globalization;
using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Compact;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Text;
using SupernovaLab.Core.Units;

namespace SupernovaLab.App.Tour;

/// <summary>
/// The tour's caption templates (Task 12). Every number in a caption is a model value formatted by
/// <see cref="UnitFormat"/>, and every time is an observer time (source time × (1 + z)), the timeline's clock. A template
/// says only what the model says; a number the model does not give is left out.
/// </summary>
public static class TourCaptions
{
    private static readonly LightCurveModel LightCurve = new();

    /// <summary>True for the four thermonuclear types, whose clock starts at ignition rather than at core bounce.</summary>
    public static bool IsThermonuclear(Outcome o)
        => o.Type is SupernovaType.Ia or SupernovaType.Ia91T or SupernovaType.Ia91bg or SupernovaType.Iax;

    /// <summary>"In the lab, the shock ...": a sentence from the lab's model, marked as such so it is not read as observed.</summary>
    public static string InTheLab(string sentence)
    {
        // Keep an acronym's capitals ("GRB"): lower-case the first letter only when the second is not upper case.
        bool acronym = sentence.Length > 1 && char.IsUpper(sentence[1]);
        return "In the lab, " + (acronym ? sentence[0] : char.ToLowerInvariant(sentence[0])) + sentence[1..];
    }

    /// <summary>
    /// "Expected: the shock ...": a lab-model sentence about a signal that has never been observed from any supernova (spec §7.1;
    /// <see cref="NeverObserved"/>), so it is what the model expects, not something seen.
    /// </summary>
    public static string Expected(string text) => "Expected: " + text;

    /// <summary>The glossary key's model Id for the never-observed audit; null for a marker whose signal has no audited model.</summary>
    private static string? AuditedModelId(string glossaryKey) => glossaryKey switch
    {
        nameof(Glossary.NeutrinoBurst) => "neutrino",
        nameof(Glossary.GravitationalWaves) => "gw",
        nameof(Glossary.PeakBrightness) or nameof(Glossary.PlateauEnd) => "lightcurve",
        nameof(Glossary.PulsarEmerges) => "pulsar",
        _ => null,
    };

    /// <summary>"Observed: ... (recalled)." for a history line; a recalled ([R]) fact says so before its full stop.</summary>
    public static string Observed(string text, bool recalled)
        => "Observed: " + (recalled ? text.TrimEnd().TrimEnd('.') + " (recalled)." : text);

    private static string Clock(Outcome o) => IsThermonuclear(o) ? "ignition" : "core bounce";

    /// <summary>The first stop's caption: how this outcome begins ("Core collapse", "Ignition", or the star's own fate).</summary>
    public static string Begin(Outcome o) => o.Fate switch
    {
        Fate.NotDeadYet => "This star is still on the main sequence: it has not reached the end of its life, so nothing explodes",
        Fate.WhiteDwarf => "This star ends its life as a white dwarf: there is no explosion",
        Fate.PairInstabilitySupernova => "Pair instability: the core contracts, its oxygen ignites, and the whole star is blown apart",
        Fate.PulsationalPairInstability => "Pair instability: the core pulses, throwing off shells of gas before it collapses",
        _ => IsThermonuclear(o) ? "Ignition" : "Core collapse",
    };

    /// <summary>The observer time of the shock breakout, <c>EjectaKinematics.BreakoutTimeS(o) × (1 + z)</c>; NaN with no ejecta.</summary>
    public static double BreakoutObserverTimeS(Outcome o, ObserverSetup obs)
        => EjectaKinematics.BreakoutTimeS(o) * obs.TimeDilation;

    /// <summary>"The shock breaks out of the star 1.5 hours after core bounce, a flash of X-rays and ultraviolet light."</summary>
    public static string Breakout(Outcome o, ObserverSetup obs, double markerObserverTimeS)
    {
        double t = BreakoutObserverTimeS(o, obs);
        if (!double.IsFinite(t) || t <= 0) t = markerObserverTimeS;
        return $"The shock breaks out of the star {UnitFormat.Time(t)} after {Clock(o)}, a flash of X-rays and ultraviolet light.";
    }

    /// <summary>The caption for an event marker, by its glossary key; the marker's own caption for an unknown key.</summary>
    public static string ForMarker(EventMarker m, Outcome o, ObserverSetup obs)
    {
        string t = UnitFormat.Time(m.ObserverTimeS);
        string clock = Clock(o);
        string text = m.GlossaryKey switch
        {
            nameof(Glossary.NeutrinoBurst)
                => $"A burst of neutrinos streams out of the collapsed core {t} after {clock}, carrying away most of the energy the collapse released.",
            nameof(Glossary.GravitationalWaves) => $"Gravitational waves ripple out from the churning core {t} after {clock}.",
            nameof(Glossary.BlackHoleForms) => $"The core collapses into a black hole {t} after {clock}.",
            nameof(Glossary.ShockBreakout) => Breakout(o, obs, m.ObserverTimeS),
            nameof(Glossary.GammaRayBurst) => obs.BeamTowardEarth
                ? $"A gamma-ray burst peaks {t} after {clock}; its jet points toward us."
                : $"A gamma-ray burst peaks {t} after {clock}, but its jet points away from us, so we see little of it.",
            nameof(Glossary.PeakBrightness) => $"The supernova reaches its peak brightness {t} after {clock}{PeakMagnitude(o, obs, m.ObserverTimeS)}.",
            nameof(Glossary.PlateauEnd)
                => $"The bright plateau ends {t} after {clock}, when the hydrogen has all recombined and the light falls to the radioactive tail.",
            nameof(Glossary.GammaRayLines) => $"The cobalt-56 gamma-ray line at {GammaLineModel.Co56MainLineKeV:0} keV is at its brightest {t} after {clock}.",
            nameof(Glossary.RadioPeak) => $"Radio emission from the shock running into the star's old wind peaks {t} after {clock}.",
            nameof(Glossary.PulsarEmerges) => $"A young pulsar's radio pulses can now get out of the thinning ejecta, {t} after {clock}.",
            nameof(Glossary.RemnantResolvable)
                => $"The expanding remnant is now at least {RemnantEvolutionModel.ResolvableArcsec.ToString(CultureInfo.InvariantCulture)} arcseconds across from here, so Chandra could resolve it, {t} after {clock}.",
            _ => m.Caption,
        };
        return NeverObserved.Applies(AuditedModelId(m.GlossaryKey) ?? "", null, o) ? Expected(text) : InTheLab(text);
    }

    /// <summary>
    /// The mixing stop's caption (ruling R-P2-33, final review I7), model-labelled: what the cut-away slice shows. Nickel is carried
    /// outward and, where the class has hydrogen to mix, hydrogen inward; the reach is SN 1987A's observed one for that preset (the
    /// outcome with its ring), otherwise from 3D simulations (structure §S3); the shapes are schematic.
    /// </summary>
    public static string Mixing(Outcome o, Core.Structure.MixingReach m)
    {
        // Kept short (final fix wave): this stop is usually merged with the peak-light stop, and a long caption covers the slice.
        string what = double.IsFinite(m.HydrogenMinCmS) ? "nickel carried outward and hydrogen inward" : "nickel carried outward";
        string reach = o.HasEquatorialRing ? "reach observed in SN 1987A" : "reach from 3D simulations";
        return InTheLab($"A slice through the debris shows the mixing fingers: {what}; {reach}, shapes schematic (structure §S3).");
    }

    /// <summary>", at about V = 12.3 as seen from here" from the light-curve model, or "" when the model gives no finite value.</summary>
    private static string PeakMagnitude(Outcome o, ObserverSetup obs, double observerTimeS)
    {
        try
        {
            double v = LightCurve.Sample(o, obs, observerTimeS / obs.TimeDilation, "appV");
            return double.IsFinite(v) ? ", at about V = " + v.ToString("0.0", CultureInfo.InvariantCulture) + " as seen from here" : "";
        }
        catch (Exception e)
        {
            System.Diagnostics.Trace.TraceError($"Tour peak magnitude: {e}");
            return "";
        }
    }


    // ---- Close-up stops (Project 3, Task 13; spec §2.4) ----
    // Every number below is the Close-up model's own value (CoreCollapseHistory, BlackHoleModel, PulseSchedule, MagnetarFlares), formatted
    // by UnitFormat, and every time is an observer time. The first second is "From simulations" (nothing there is observed directly).

    /// <summary>"In the lab, the core ... (From simulations).": a first-second sentence from the Close-up's simulation-based tracks.</summary>
    private static string Simulated(string sentence)
        => $"In the lab, {char.ToLowerInvariant(sentence[0])}{sentence[1..].TrimEnd('.')} ({CoreCollapseHistory.FromSimulationsLabel}).";

    private static string Km(double km) => UnitFormat.Distance(km * 1e5);

    /// <summary>The core's state at an observer time (converted to source time).</summary>
    private static CoreState CoreAt(Outcome o, ObserverSetup obs, double observerTimeS)
        => CoreCollapseHistory.At(o, observerTimeS / obs.TimeDilation);

    private static string Radii(CoreState s)
    {
        var parts = new List<string>();
        if (double.IsFinite(s.ShockRadiusKm)) parts.Add($"the shock is {Km(s.ShockRadiusKm)} from the centre");
        double pns = CoreCollapseHistory.DrawnProtoNsRadiusKm(s);
        if (double.IsFinite(pns)) parts.Add($"the proto-neutron star is {Km(pns)} in radius");
        return parts.Count == 0 ? "" : "; " + string.Join(" and ", parts);
    }

    /// <summary>The bounce stop: the core stops collapsing and the shock is born.</summary>
    public static string CloseUpBounce(Outcome o, ObserverSetup obs, double observerTimeS)
        => Simulated($"{UnitFormat.Time(observerTimeS)} after core bounce the core has rebounded and its shock wave is moving out{Radii(CoreAt(o, obs, observerTimeS))}.");

    /// <summary>The stall stop: the shock runs out of energy while gas keeps falling in.</summary>
    public static string CloseUpStall(Outcome o, ObserverSetup obs, double observerTimeS)
    {
        var s = CoreAt(o, obs, observerTimeS);
        string infall = double.IsFinite(s.InfallSpeedKmS) ? $", with gas falling in ahead of it at {UnitFormat.Number(s.InfallSpeedKmS)} km/s" : "";
        return Simulated($"{UnitFormat.Time(observerTimeS)} after core bounce the shock stalls{infall}{Radii(s)}.");
    }

    /// <summary>The revival stop: neutrino heating pushes the stalled shock outward (a magnetar-driven explosion revives it differently).</summary>
    public static string CloseUpRevival(Outcome o, ObserverSetup obs, double observerTimeS)
    {
        string text = Simulated($"{UnitFormat.Time(observerTimeS)} after core bounce neutrino heating revives the shock and it moves outward{Radii(CoreAt(o, obs, observerTimeS))}.");
        return CoreCollapseHistory.TrackFor(o) == CoreTrack.Magnetorotational ? text + " " + CoreCollapseHistory.MagnetorotationalNote + "." : text;
    }

    /// <summary>The black-hole stop: the proto-neutron star collapses inside its own horizon.</summary>
    public static string CloseUpBlackHole(Outcome o, ObserverSetup obs, double observerTimeS)
        => Simulated($"{UnitFormat.Time(observerTimeS)} after core bounce the proto-neutron star collapses into a black hole; its horizon is {Km(CoreCollapseHistory.HorizonAtFormationKm(o))} in radius.");

    /// <summary>The crust stop: the outer layers of the neutron star freeze into a solid crust.</summary>
    public static string CloseUpCrust(double observerTimeS)
        => InTheLab($"About {UnitFormat.Time(observerTimeS)} after core bounce the neutron star's outer layers crystallise into a solid crust (approximate: the time is uncertain).");

    /// <summary>The settled stop: the black hole has stopped changing and spins.</summary>
    public static string CloseUpSettled(Outcome o, double observerTimeS)
        => InTheLab($"{UnitFormat.Time(observerTimeS)} after core bounce the black hole has settled, with a spin of {UnitFormat.Number(BlackHoleModel.SettledSpin(o))}; the Close-up reads \"{BlackHoleModel.SettledCaption}\".");

    /// <summary>The pulse stop: the young pulsar's beam can be seen (or misses us).</summary>
    public static string CloseUpPulses(Outcome o, ObserverSetup obs, double observerTimeS)
    {
        double src = observerTimeS / obs.TimeDilation;
        var vis = PulseSchedule.Visibility(o, obs, src);
        string tail = vis == PulseVisibility.Received
            ? $"its pulses reach us {PulseSchedule.RateText(PulseSchedule.ReceivedPeriodS(o, obs, src))}"
            : "its beam misses Earth, so we would not see the pulses";
        return InTheLab($"{UnitFormat.Time(observerTimeS)} after core bounce the debris have thinned enough for the pulsar's beam to get out; {tail}.");
    }

    /// <summary>The giant-flare stop, compared with the 2004 flare.</summary>
    public static string CloseUpFlare(GiantFlare f, double observerTimeS)
        => InTheLab($"{UnitFormat.Time(observerTimeS)} after core bounce the magnetar gives a giant flare, a spike of {UnitFormat.Energy(f.EnergyErg)} (isotropic) lasting {f.SpikeS.ToString("0.00", CultureInfo.InvariantCulture)} s (speculative). {MagnetarFlares.Comparison2004}: {MagnetarFlares.Comparison2004Numbers}.");
    /// <summary>The instrument to brighten at a marker (null for none).</summary>
    public static Messenger? HighlightFor(string glossaryKey) => glossaryKey switch
    {
        nameof(Glossary.NeutrinoBurst) => Messenger.Neutrinos,
        nameof(Glossary.GravitationalWaves) => Messenger.GravitationalWaves,
        nameof(Glossary.ShockBreakout) => Messenger.XRaysUv,
        nameof(Glossary.GammaRayBurst) or nameof(Glossary.GammaRayLines) => Messenger.GammaRays,
        nameof(Glossary.PeakBrightness) => Messenger.VisibleLight,
        nameof(Glossary.RadioPeak) or nameof(Glossary.PulsarEmerges) => Messenger.Radio,
        _ => null,
    };
}
