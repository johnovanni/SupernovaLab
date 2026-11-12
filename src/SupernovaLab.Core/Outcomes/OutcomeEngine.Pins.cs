using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Outcomes;

/// <summary>
/// Preset pins (spec §2.3; rulings R35, R36, R41, R44, R46): published values put in place of the model's own, the notes
/// when a pin does not fit the outcome, and ruling R22's class judged again on the pinned values.
/// </summary>
public sealed partial class OutcomeEngine
{
    /// <summary>
    /// What ruling R22 needs to re-judge a hydrogen-rich, superluminous-energy magnetar after pins: its base class and that
    /// class's note, the spin-down sentence that starts the class note, and the class note now in the notes.
    /// </summary>
    private sealed record HydrogenRichMagnetar(SupernovaType BaseType, string BaseTypeNote, string Pours, string ClassNote);

    /// <summary>
    /// The outcome's values after a preset's pins (rulings R35, R36, R41, R44, R46): the star (with its pinned radius), the
    /// class (R22 re-judged), the explosion values, ⁴⁴Ti when pinned, the GRB, pulsar and ring pins, and whether any pin
    /// was applied.
    /// </summary>
    private sealed record Pinned(PresupernovaStar Star, SupernovaType Type, Quantity? Energy, Quantity? Ejecta,
        Quantity? Nickel, Quantity? Ti44, Quantity? GrbEnergy, Quantity? GrbPeak, Quantity? GrbT90, Quantity? PulsarRadio,
        bool Ring, bool Published);

    private static Pinned ApplyPins(LabInputs x, Draft d, PresupernovaStar star, List<string> notes, PresetPins? pins)
    {
        var (energy, ejecta, nickel) = (d.Energy, d.Ejecta, d.Nickel);
        var type = d.Type;
        bool published = false;
        // A preset's values are one author's published fit to the real event (presets.md), so ModelDependent; their
        // Source is the preset's citation (ruling R44).
        Quantity Pin(double v, string unit)
        {
            published = true;
            return new Quantity(v, unit, Confidence.ModelDependent, pins!.Source, Published: true);
        }

        // Ruling R44: explosion pins apply only to an explosion; the radius describes the star, so it always applies.
        bool explosion = d.Fate is Fate.Supernova or Fate.PairInstabilitySupernova ||
                         (d.Fate == Fate.PulsationalPairInstability && d.Type != SupernovaType.None);
        bool neutronStar = d.Remnant.Kind is RemnantKind.NeutronStar or RemnantKind.Magnetar;
        bool coreCollapse = x.Kind == StarKind.MassiveStar && d.Fate == Fate.Supernova;
        Quantity? ti44 = null, grbEnergy = null, grbPeak = null, grbT90 = null, pulsarRadio = null;
        bool ring = false;
        if (pins is not null)
        {
            if (pins.RadiusCm is double r) star = star with { RadiusCm = Pin(r, "cm") };
            if (!explosion)
            {
                if (HasExplosionPins(pins))
                    notes.Add("The published explosion values of this event are not used: in the lab this star does not " +
                              "explode, so there is no explosion to give them to.");
            }
            else
            {
                if (pins.ExplosionEnergyErg is double e) energy = Pin(e, "erg");
                if (pins.EjectaMass is double ej) ejecta = Pin(ej, MSun);
                if (pins.NickelMass is double ni) nickel = Pin(ni, MSun);
                if (pins.Ti44Mass is double t)
                {
                    if (coreCollapse) ti44 = Pin(t, MSun);
                    else notes.Add("The published ⁴⁴Ti mass is not used: the lab follows ⁴⁴Ti for core-collapse supernovae only.");
                }
                // Ruling R36: the gamma-ray burst pins, for an outcome that has a GRB.
                if (pins.GrbEisoErg is not null || pins.GrbEpKeV is not null || pins.GrbT90S is not null)
                {
                    if (d.HasGrb)
                    {
                        if (pins.GrbEisoErg is double eiso) grbEnergy = Pin(eiso, "erg");
                        if (pins.GrbEpKeV is double ep) grbPeak = Pin(ep, "keV");
                        if (pins.GrbT90S is double t90) grbT90 = Pin(t90, "s");
                    }
                    else notes.Add("The published gamma-ray burst values are not used: in the lab this explosion makes no burst.");
                }
                // Rulings R41 and R46: the neutron-star pins. Mass, spin and field went onto the remnant when it formed
                // (Exploded).
                if (pins.NsSpinPeriodS is not null || pins.NsFieldG is not null || pins.PulsarRadioLumMjyKpc2 is not null ||
                    pins.NsGravitationalMass is not null)
                {
                    if (neutronStar)
                    {
                        published |= pins.NsSpinPeriodS is not null || pins.NsFieldG is not null ||
                                     pins.NsGravitationalMass is not null;
                        if (pins.PulsarRadioLumMjyKpc2 is double l) pulsarRadio = Pin(l, "mJy kpc²");
                    }
                    else notes.Add("The published neutron-star values are not used: in the lab this explosion leaves no " +
                                   "neutron star.");
                }
                if (pins.EquatorialRing == true)
                {
                    ring = true;
                    published = true;
                }
                NoteNickelCapBroken(x, pins, ejecta, nickel, notes);
            }
        }

        // Ruling R44: a hydrogen-rich magnetar's SLSN-II / luminous-II class is judged again on the pinned E and M_ej.
        if (d.R22 is { } r22 && explosion && (pins?.ExplosionEnergyErg is not null || pins?.EjectaMass is not null) &&
            ejecta is { Value: > 0 } && energy is { Value: > 0 } && d.MagnetarEnergy is { } mep && d.MagnetarTimescale is { } mtp)
            type = RejudgeHydrogenRichMagnetar(x, r22, type, star, mep.Value, mtp.Value, energy.Value, ejecta.Value, notes);

        return new Pinned(star, type, energy, ejecta, nickel, ti44, grbEnergy, grbPeak, grbT90, pulsarRadio, ring, published);
    }

    /// <summary>Whether the pins hold any value that needs an explosion (ruling R44): every pin but the radius.</summary>
    private static bool HasExplosionPins(PresetPins p)
        => p.ExplosionEnergyErg is not null || p.EjectaMass is not null || p.NickelMass is not null || p.Ti44Mass is not null ||
           p.GrbEisoErg is not null || p.GrbEpKeV is not null || p.GrbT90S is not null || p.NsSpinPeriodS is not null ||
           p.NsFieldG is not null || p.PulsarRadioLumMjyKpc2 is not null || p.EquatorialRing == true ||
           p.NsGravitationalMass is not null;

    /// <summary>
    /// Ruling R44: pinned values are trusted, so the model's cap (⁵⁶Ni ≤ 0.5 M_ej for core-collapse and pair-instability
    /// outcomes, ruling R15; ⁵⁶Ni &lt; M_ej for thermonuclear ones, ruling R17) is not applied to them, but a pin set that
    /// breaks it is pointed out.
    /// </summary>
    private static void NoteNickelCapBroken(LabInputs x, PresetPins pins, Quantity? ejecta, Quantity? nickel, List<string> notes)
    {
        if (pins.NickelMass is null && pins.EjectaMass is null) return;
        if (nickel is not { } ni || ejecta is not { Value: > 0 } ej) return;
        bool thermonuclear = x.Kind == StarKind.WhiteDwarf;
        bool broken = thermonuclear ? ni.Value >= ej.Value : ni.Value > NickelMaxEjectaFraction * ej.Value;
        if (!broken) return;
        notes.Add($"The published ⁵⁶Ni mass ({UnitFormat.Mass(ni.Value)}) is more than {(thermonuclear ? "the" : "half the")} " +
                  $"ejecta mass ({UnitFormat.Mass(ej.Value)}), which the lab's own model never makes; the published values " +
                  "are kept as they are.");
    }

    /// <summary>
    /// Ruling R44 with R22 and R32: the class of a hydrogen-rich magnetar supernova whose energy or ejecta mass a preset
    /// pinned, judged on the one magnetar peak with those values. When the class changes, its note changes with it, and
    /// the base-class sentence is removed (SLSN-II) or put back (base class).
    /// </summary>
    private static SupernovaType RejudgeHydrogenRichMagnetar(LabInputs x, HydrogenRichMagnetar r22, SupernovaType type,
        PresupernovaStar star, double spinDownEnergy, double spinDownTime, double kineticEnergy, double ejecta, List<string> notes)
    {
        double peak = MagnetarModel.PeakLuminosityErgS(spinDownEnergy, spinDownTime, ejecta, kineticEnergy,
            MagnetarModel.EjectaOpacity(x.InitialMass, star.HydrogenEnvelopeMass.Value, ejecta));
        var (slsn, note) = HydrogenRichMagnetarClass(r22.Pours, peak);
        var judged = slsn ?? r22.BaseType;
        if (x.DenseCsm) judged = SpectralTyper.WithDenseCsm(judged);
        if (judged == type) return type;

        int at = notes.IndexOf(r22.ClassNote);
        if (at >= 0) notes[at] = note + " (Judged with the published explosion values.)";
        else notes.Add(note);
        if (slsn is null) notes.Insert(Math.Max(at, 0), r22.BaseTypeNote);
        else notes.Remove(r22.BaseTypeNote);
        return judged;
    }

    /// <summary>
    /// Rulings R41 and R46: this neutron star (or magnetar) with a preset's published birth spin period, dipole field and
    /// gravitational mass, where the pins give them. The mass sets the neutrino binding energy (NeutrinoModel).
    /// </summary>
    private static Remnant WithSpinPins(Remnant neutronStar, PresetPins? pins)
    {
        if (pins is null) return neutronStar;
        Quantity Pin(double v, string unit) => new(v, unit, Confidence.ModelDependent, pins.Source, Published: true);
        return neutronStar with
        {
            SpinPeriodS = pins.NsSpinPeriodS is double p ? Pin(p, "s") : neutronStar.SpinPeriodS,
            MagneticFieldG = pins.NsFieldG is double b ? Pin(b, "G") : neutronStar.MagneticFieldG,
            Mass = pins.NsGravitationalMass is double m ? Pin(m, MSun) : neutronStar.Mass,
        };
    }
}
