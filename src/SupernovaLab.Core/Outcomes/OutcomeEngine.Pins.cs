using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Outcomes;

/// <summary>
/// Preset pins (spec §2.3; rulings R35, R36, R41, R44, R46, R53): published values put in place of the model's own, the
/// notes when a pin does not fit the outcome, the notes that state a pinned value rewritten with it, the mass budget, and
/// the superluminous class judged again on the pinned values.
/// </summary>
public sealed partial class OutcomeEngine
{
    /// <summary>
    /// The outcome's values after a preset's pins (rulings R35, R36, R41, R44, R46, R53): the star (with its pinned radius),
    /// the class (R22/R53 re-judged; a Type Ia's subtype from a pinned ⁵⁶Ni), the explosion values, the remnant (with a
    /// pinned black-hole mass), ⁴⁴Ti when pinned, the GRB, pulsar and ring pins, and whether any pin was applied.
    /// </summary>
    private sealed record Pinned(PresupernovaStar Star, SupernovaType Type, Quantity? Energy, Quantity? Ejecta,
        Quantity? Nickel, Remnant Remnant, Quantity? Ti44, Quantity? GrbEnergy, Quantity? GrbPeak, Quantity? GrbT90,
        Quantity? PulsarRadio, bool Ring, bool Published, double? ConfinedCsmOuterCm = null, bool UndetectedOuterShell = false,
        IReadOnlyList<int>? SceneDisclosures = null);

    /// <summary>Ruling R53 (I2): a pinned ejecta and remnant may exceed the star's final mass by this much (M☉) unremarked.</summary>
    private const double MassBudgetTolerance = 0.01;

    private static Pinned ApplyPins(LabInputs x, Draft d, PresupernovaStar star, List<string> notes, PresetPins? pins)
    {
        var (energy, ejecta, nickel) = (d.Energy, d.Ejecta, d.Nickel);
        var type = d.Type;
        var remnant = d.Remnant;
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
        double? confinedCsm = null;
        // Ruling R-P2-33 (I12): a note-only flag for an exploding outcome; not a published value, so not `published`.
        bool undetectedShell = pins?.UndetectedOuterShell == true && explosion;
        if (pins is not null)
        {
            // Ruling R-P2-27: observed geometry for the 3D view only (a Type Ia has no surroundings); no model reads it.
            if (pins.ConfinedCsmOuterCm is > 0 and double confined && d.Type is not (SupernovaType.Ia or SupernovaType.Ia91T or
                    SupernovaType.Ia91bg or SupernovaType.Iax))
            {
                confinedCsm = confined;   // geometry only (ruling R-P2-28): not a published modelled value, so not `published`
            }
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
                if (pins.NickelMass is double ni)
                {
                    nickel = Pin(ni, MSun);
                    type = WithPinnedIaNickel(x, d, type, ni, notes);
                }
                // Ruling R53 (I2): a published black-hole mass (the same author set as the ejecta).
                if (pins.RemnantMass is double rm)
                {
                    if (remnant.Kind == RemnantKind.BlackHole)
                    {
                        // The momentum-scaled kick follows the mass; a direct collapse keeps its zero kick.
                        var kick = remnant.KickKmS is { Value: > 0 } ? RemnantModel.BlackHoleKick(rm, directCollapse: false)
                            : remnant.KickKmS;
                        remnant = remnant with { Mass = Pin(rm, MSun), KickKmS = kick };
                    }
                    else notes.Add("The published remnant mass is not used: in the lab this explosion leaves no black hole.");
                }
                if (d.RemnantNote is { } rn && d.RemnantNoteFor is { } noteFor && notes.IndexOf(rn) is var at and >= 0 &&
                    (pins.EjectaMass is not null || pins.RemnantMass is not null) &&
                    ejecta is not null && remnant.Mass is not null)
                    notes[at] = noteFor(ejecta.Value, remnant.Mass.Value);
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
                        // Review M2: observed on the remnant, not assumed from the pins.
                        published |= d.Remnant.SpinPeriodS?.Published == true || d.Remnant.MagneticFieldG?.Published == true ||
                                     d.Remnant.Mass?.Published == true;
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
                // Review M3: R15 still caps the MODEL's ⁵⁶Ni (core collapse and pair instability, R17) under a pinned M_ej.
                if (pins.EjectaMass is not null && pins.NickelMass is null && x.Kind == StarKind.MassiveStar &&
                    nickel is not null && ejecta is { Value: > 0 })
                    nickel = CapNickel(nickel, ejecta.Value);
                NoteNickelCapBroken(x, pins, ejecta, nickel, notes);
                if (pins.EjectaMass is not null || pins.RemnantMass is not null)
                    NoteMassBudget(pins, star, ejecta, remnant, notes);
            }
        }

        // Rulings R44 and R53: a superluminous-energy magnetar's class is judged again on the pinned E and M_ej.
        if (d.Slsn is { } candidate && explosion && (pins?.ExplosionEnergyErg is not null || pins?.EjectaMass is not null) &&
            ejecta is { Value: > 0 } && energy is { Value: > 0 } && d.MagnetarEnergy is { } mep && d.MagnetarTimescale is { } mtp)
            type = RejudgeMagnetar(x, candidate, type, star, mep.Value, mtp.Value, energy.Value, ejecta.Value, notes);

        return new Pinned(star, type, energy, ejecta, nickel, remnant, ti44, grbEnergy, grbPeak, grbT90, pulsarRadio, ring,
            published, confinedCsm, undetectedShell,
            pins?.SceneDisclosures is { Length: > 0 } disclosures ? disclosures.ToArray() : null);   // ruling R-P2-32: pass-through
    }

    /// <summary>
    /// Ruling R53 (I1): a pinned ⁵⁶Ni on a Type Ia (not Iax) sets its subtype by the model's own rule (step 9) and rewrites
    /// the ⁵⁶Ni sentence with the published value. Returns the class.
    /// </summary>
    private static SupernovaType WithPinnedIaNickel(LabInputs x, Draft d, SupernovaType type, double nickel, List<string> notes)
    {
        if (x.Kind != StarKind.WhiteDwarf || type == SupernovaType.Iax || d.IaNickelNote is not { } old) return type;
        var (subtype, note) = TypeIaModel.Subtype(nickel, published: true);
        int at = notes.IndexOf(old);
        if (at >= 0) notes[at] = note;
        else notes.Add(note);
        return subtype;
    }

    /// <summary>
    /// Ruling R53 (I2): when the pinned ejecta and the remnant together weigh more than the lab's star at death, the
    /// published values are kept and a note says the real star was heavier than the lab's model of it. The remnant's
    /// shown mass is used (gravitational for a neutron star, lighter than its baryonic mass), so only a real excess counts.
    /// </summary>
    private static void NoteMassBudget(PresetPins pins, PresupernovaStar star, Quantity? ejecta, Remnant remnant,
        List<string> notes)
    {
        if (ejecta is null || remnant.Mass is not { } rem) return;
        double mf = star.FinalMass.Value, total = ejecta.Value + rem.Value;
        if (total <= mf + MassBudgetTolerance) return;
        string ejectaWho = pins.EjectaMass is not null ? "published" : "model's";
        string remnantWho = rem.Published ? "published" : "model's";
        string kind = remnant.Kind switch
        {
            RemnantKind.BlackHole => "black hole",
            RemnantKind.Magnetar => "magnetar",
            RemnantKind.NeutronStar => "neutron star",
            _ => "remnant",
        };
        notes.Add($"The {ejectaWho} ejecta ({UnitFormat.Mass(ejecta.Value)}) and the {remnantWho} {kind} " +
                  $"({UnitFormat.Mass(rem.Value)}) add up to {UnitFormat.Mass(total)}, more than the lab's star holds when it " +
                  $"dies ({UnitFormat.Mass(mf)}): the real star was heavier than the lab's model of it. The published values " +
                  "are kept.");
    }

    /// <summary>Whether the pins hold any value that needs an explosion (ruling R44): every pin but the radius.</summary>
    private static bool HasExplosionPins(PresetPins p)
        => p.ExplosionEnergyErg is not null || p.EjectaMass is not null || p.NickelMass is not null || p.Ti44Mass is not null ||
           p.GrbEisoErg is not null || p.GrbEpKeV is not null || p.GrbT90S is not null || p.NsSpinPeriodS is not null ||
           p.NsFieldG is not null || p.PulsarRadioLumMjyKpc2 is not null || p.EquatorialRing == true ||
           p.NsGravitationalMass is not null || p.RemnantMass is not null;

    /// <summary>
    /// Ruling R44: pinned values are trusted, so the model's cap (⁵⁶Ni ≤ 0.5 M_ej for core-collapse and pair-instability
    /// outcomes, ruling R15; ⁵⁶Ni &lt; M_ej for thermonuclear ones, ruling R17) is not applied to them, but a pin set that
    /// breaks it is pointed out, naming which of the two values is published and which is the model's (review M3). A
    /// model ⁵⁶Ni under a pinned core-collapse M_ej has already been capped, so only a thermonuclear one can break it.
    /// </summary>
    private static void NoteNickelCapBroken(LabInputs x, PresetPins pins, Quantity? ejecta, Quantity? nickel, List<string> notes)
    {
        if (pins.NickelMass is null && pins.EjectaMass is null) return;
        if (nickel is not { } ni || ejecta is not { Value: > 0 } ej) return;
        bool thermonuclear = x.Kind == StarKind.WhiteDwarf;
        bool broken = thermonuclear ? ni.Value >= ej.Value : ni.Value > NickelMaxEjectaFraction * ej.Value;
        if (!broken) return;
        string nickelWho = pins.NickelMass is not null ? "published" : "model's";
        string ejectaWho = pins.EjectaMass is not null ? "published" : "model's";
        notes.Add($"The {nickelWho} ⁵⁶Ni mass ({UnitFormat.Mass(ni.Value)}) is more than {(thermonuclear ? "the" : "half the")} " +
                  $"{ejectaWho} ejecta mass ({UnitFormat.Mass(ej.Value)}), which the lab's own model never makes; the values " +
                  "are kept as they are.");
    }

    /// <summary>
    /// Rulings R44, R22, R32 and R53: the class of a superluminous-energy magnetar supernova whose energy or ejecta mass a
    /// preset pinned, judged on outcomes §5.3's conditions with the pinned ejecta and, with hydrogen, on the one magnetar
    /// peak with those values. When the judgement changes, its note changes with it, and the base-class sentence is
    /// removed (superluminous) or put back (base class).
    /// </summary>
    private static SupernovaType RejudgeMagnetar(LabInputs x, SuperluminousCandidate c, SupernovaType type,
        PresupernovaStar star, double spinDownEnergy, double spinDownTime, double kineticEnergy, double ejecta, List<string> notes)
    {
        double peak = c.Hydrogen
            ? MagnetarModel.PeakLuminosityErgS(spinDownEnergy, spinDownTime, ejecta, kineticEnergy,
                MagnetarModel.EjectaOpacity(x.InitialMass, star.HydrogenEnvelopeMass.Value, ejecta))
            : double.NaN;
        var (slsn, note) = MagnetarClass(c.Pours, c.Hydrogen, x.InitialMass, ejecta, peak);
        if (note == c.ClassNote) return type;

        var judged = slsn ?? c.BaseType;
        if (x.DenseCsm) judged = SpectralTyper.WithDenseCsm(judged);
        bool wasSuperluminous = type is SupernovaType.SlsnI or SupernovaType.SlsnII;
        int at = notes.IndexOf(c.ClassNote);
        if (at >= 0) notes[at] = note + " (Judged with the published explosion values.)";
        else notes.Add(note);
        if (wasSuperluminous && slsn is null) notes.Insert(Math.Max(at, 0), c.BaseTypeNote);
        else if (!wasSuperluminous && slsn is not null) notes.Remove(c.BaseTypeNote);
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
