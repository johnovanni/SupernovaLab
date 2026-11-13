using System.Globalization;
using SupernovaLab.App.ViewModels;
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
        return InTheLab(m.GlossaryKey switch
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
        });
    }

    /// <summary>
    /// The mixing stop's caption (ruling R-P2-33, final review I7), model-labelled: what the cut-away slice shows. Nickel is carried
    /// outward and, where the class has hydrogen to mix, hydrogen inward; the reach is SN 1987A's observed one for that preset (the
    /// outcome with its ring), otherwise from 3D simulations (structure §S3); the shapes are schematic.
    /// </summary>
    public static string Mixing(Outcome o, Core.Structure.MixingReach m)
    {
        string what = double.IsFinite(m.HydrogenMinCmS)
            ? "nickel carried outward and hydrogen pulled inward"
            : "nickel carried outward";
        string reach = o.HasEquatorialRing ? "their reach is the one observed in SN 1987A" : "their reach is from 3D simulations";
        return InTheLab($"A slice through the debris shows the mixing fingers: {what} by Rayleigh–Taylor instabilities as the shock " +
                        $"crossed the star; {reach}, their shapes schematic (structure §S3).");
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
