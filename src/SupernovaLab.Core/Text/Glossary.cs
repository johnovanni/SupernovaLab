using System.Reflection;

namespace SupernovaLab.Core.Text;

/// <summary>
/// Plain-language explanations of every astronomy term in the UI, used for tooltips (global constraint "Glossary").
/// Each term is a public constant, so XAML reads it with <c>{x:Static text:Glossary.InitialMass}</c>; view models hand
/// the UI a key (the constant's name), which <see cref="Get"/> turns into the text. Numbers quoted here come from the
/// research files (docs/research), cited by section.
/// </summary>
public static class Glossary
{
    // ---------------------------------------------------------------- the lab: star (spec §2.1)

    public const string Preset =
        "A real supernova or supernova remnant. Choosing one sets the lab to the inputs that best match it and puts in the " +
        "published values astronomers measured for it (explosion energy, ejected mass, nickel mass, the star's radius, its " +
        "distance and dust). Changing any input afterwards switches back to the lab's own model.";
    public const string PresetInfo =
        "What was actually seen of the chosen supernova, with the measurements and where they were published.";
    public const string StarKind =
        "Which way the star dies. A massive star's iron core collapses (a core-collapse supernova). A white dwarf in a " +
        "binary is a dead star that gains mass from a companion until it explodes by runaway nuclear burning (a Type Ia " +
        "supernova).";
    public const string MassiveStar =
        "A star born with several times the Sun's mass or more. Heavy enough stars end with the collapse of their iron core.";
    public const string WhiteDwarfBinary =
        "A white dwarf (the dense, Earth-sized core left by a Sun-like star) orbiting a companion star. If it gains enough " +
        "mass, or merges with its companion, carbon burning runs away and blows it apart: a Type Ia supernova.";
    public const string InitialMass =
        "The star's mass when it was born, in solar masses. It decides almost everything: below about 9 solar masses the " +
        "star ends quietly as a white dwarf, and above that its core collapses. Stars below about 0.9 solar masses live " +
        "longer than the universe is old, so none has died yet.";
    public const string SolarMass =
        "The Sun's mass, about 2 × 10³⁰ kg, written M☉. Star and remnant masses are given in solar masses.";
    public const string Metallicity =
        "How much of the star is made of elements heavier than helium (astronomers call them all 'metals'), compared with " +
        "the Sun (1). Stars poor in metals blow weaker winds, so they keep more of their mass until they die.";
    public const string Stripping =
        "Most massive stars have a companion. The companion can pull off the star's outer layers: some of the hydrogen " +
        "(Partial), all of it (Full), or the hydrogen and the helium beneath it. What is left decides the supernova type.";
    public const string Rotation =
        "How fast the star spins. Fast and extreme rotation mix the star and can feed a jet-driven explosion or a gamma-ray " +
        "burst when the core collapses to a black hole.";
    public const string CoreField =
        "The magnetic field of the star's core. A strong field can leave a magnetar, a neutron star with an enormous " +
        "magnetic field whose spin-down can make the supernova far brighter.";
    public const string IaChannel =
        "How the white dwarf explodes. Near-Chandrasekhar: it grows to about 1.38 solar masses and ignites in its center. " +
        "Sub-Chandrasekhar: a lighter white dwarf is detonated by a thin helium layer on its surface. Type Iax: a weaker, " +
        "incomplete explosion that may leave part of the white dwarf behind.";
    public const string WhiteDwarfMass =
        "The white dwarf's mass when it explodes, in solar masses. Heavier white dwarfs burn more of themselves into " +
        "radioactive nickel and make brighter supernovae. It is fixed at 1.38 for the near-Chandrasekhar channel.";
    public const string AdvancedPhysics =
        "Choices where the physics is genuinely uncertain. The defaults follow the research files; changing them shows how " +
        "much the result depends on them.";
    public const string NickelMixing =
        "How far the radioactive nickel made in the core is stirred outward into the helium layer. Well mixed nickel lights " +
        "up the helium, so about 0.14 solar masses of helium already shows helium lines (Type Ib); with weak mixing up to " +
        "about 1.6 solar masses of helium can stay hidden (Type Ic) (outcomes §8.2 step 6).";
    public const string NickelYield =
        "How much radioactive nickel the explosion makes. Calibrated to observations (the default) clips the model's " +
        "yield to the amounts measured in real supernovae; Theory shows the model's own value.";
    public const string DenseGas =
        "Dense gas thrown off by the star shortly before it explodes. The ejecta crash into it, which adds light, radio and " +
        "X-rays and shows narrow spectral lines (Type IIn, Ibn or Icn).";
    public const string Opacity =
        "How strongly the ejecta block light (κ, in cm² per gram). Higher opacity traps the light longer, so the supernova " +
        "rises to its peak more slowly. The range 0.05–0.10 is the one used for stripped core-collapse supernovae " +
        "(signals §4.2).";
    public const string OpacityFixedForIa =
        "Type Ia and Iax supernovae use a fixed effective opacity of 0.19 cm²/g, set so that the default Type Ia rises to " +
        "its peak in the observed 17–19 days (Hayden et al. 2010). The 0.05–0.10 range is for core-collapse ejecta, so the " +
        "slider is off for white dwarfs.";

    // ---------------------------------------------------------------- the lab: observer (spec §2.2)

    public const string Distance =
        "How far away the supernova is. Light, neutrinos and gravitational waves all fade with the square of the distance. " +
        "The tick marks are Betelgeuse, the Galactic Center, the Large Magellanic Cloud, Andromeda, M51 and the Virgo " +
        "Cluster.";
    public const string Parsec =
        "A unit of distance: about 3.26 light-years. A kiloparsec (kpc) is a thousand parsecs and a megaparsec (Mpc) a " +
        "million. The Milky Way's center is about 8 kpc away.";
    public const string Dust =
        "Dust between the supernova and us, as magnitudes of visual extinction (A_V). Each 2.5 magnitudes dims visible light " +
        "by a factor of 10, so A_V = 30 (toward the Galactic Center) dims it by 10¹². Dust also dims ultraviolet and " +
        "absorbs soft X-rays, but not gravitational waves, neutrinos, gamma rays or radio.";
    public const string Beam =
        "Whether a jet or a beam points at Earth. A gamma-ray burst's two jets cover a small part of the sky: with jets of " +
        "5° half-opening angle (10° across) only about 1 observer in 260 sees the burst (signals §9.2). A young pulsar's radio beam sweeps more of the " +
        "sky, about half of all directions for a Crab-like pulsar (Tauris & Manchester 1998; signals §8.3).";
    public const string Redshift =
        "Beyond 50 Mpc the expansion of the universe matters: light is stretched to longer wavelengths by a factor 1 + z " +
        "and everything seems to happen 1 + z times more slowly. z is worked out from the distance with a flat universe, " +
        "H₀ = 70 km/s/Mpc and Ωm = 0.3.";
    public const string KCorrection =
        "For a distant supernova the stretched light moves between color bands, so a telescope's filter sees a different " +
        "part of the spectrum than it would nearby. Correcting for this (the K-correction) is not done here, so visible " +
        "and ultraviolet brightnesses beyond 50 Mpc are approximate.";
    public const string ObserverSource =
        "Where the preset's distance and dust come from: the published measurement for that object.";

    // ---------------------------------------------------------------- running the lab (spec §2.4)

    public const string Explode =
        "Starts the timeline at core bounce (or ignition for a white dwarf) and plays it. The physics is the same every " +
        "time for the same inputs.";
    public const string RunNumber =
        "The physics is deterministic. Only the individual detected events, such as each neutrino a detector catches, are " +
        "drawn at random; the run number is the seed. Re-roll draws a new set of events from the same physics.";
    public const string PublishedValue =
        "This number is a published measurement for the chosen real supernova, not the lab's model. The tooltip gives the " +
        "paper.";

    // ---------------------------------------------------------------- the outcome (spec §3)

    public const string Fate = "How the star ends: quietly, in a supernova, or by collapsing to a black hole.";
    public const string SupernovaType =
        "Astronomers classify supernovae by their spectra: Type II show hydrogen; Type Ib show helium but no hydrogen; Type " +
        "Ic show neither; Type Ia are exploding white dwarfs.";
    public const string Remnant = "What is left after the explosion or collapse: a neutron star, a black hole, a white dwarf or nothing.";
    public const string NotDeadYet =
        "Low-mass stars burn their fuel so slowly that they outlive the universe's age of about 13.8 billion years, so none " +
        "has died yet.";
    public const string WhiteDwarf =
        "The dense core left when a star of up to about 9 solar masses sheds its outer layers: about the Sun's mass packed " +
        "into a ball the size of Earth. There is no supernova.";
    public const string Supernova = "The star blows apart, throwing its outer layers into space at thousands of km/s.";
    public const string FailedSupernova =
        "The core collapses but the explosion fails: the star falls into a black hole and simply disappears, with a burst " +
        "of neutrinos but little or no light. N6946-BH1 is a candidate.";
    public const string PairInstability =
        "In a very massive star (helium core of about 64–133 solar masses) the core gets so hot that light turns into " +
        "electron–positron pairs. The pressure drops, the core collapses, and runaway oxygen burning destroys the whole " +
        "star, leaving nothing behind.";
    public const string PulsationalPairInstability =
        "In a helium core of about 35–64 solar masses, pair instability is too weak to destroy the star. Instead it pulses, " +
        "throwing off shells of gas, and then the core collapses to a black hole.";

    public const string TypeIIP =
        "Type II-P: hydrogen lines, and a 'plateau' where the brightness stays nearly flat for about 100 days while the " +
        "thick hydrogen envelope cools. The most common kind of core-collapse supernova.";
    public const string TypeIIL =
        "Type II-L: hydrogen lines, but a thinner hydrogen envelope, so the brightness falls steadily after the peak instead " +
        "of holding a plateau.";
    public const string TypeIIb =
        "Type IIb: the star kept only a thin layer of hydrogen, so hydrogen lines appear early and then fade, leaving a " +
        "helium-rich spectrum. SN 1993J is the classic example.";
    public const string TypeIIn =
        "Type IIn: narrow hydrogen lines from slow, dense gas around the star that the ejecta run into.";
    public const string TypeIb = "Type Ib: no hydrogen, but helium lines. The star lost its hydrogen envelope before it died.";
    public const string TypeIc = "Type Ic: neither hydrogen nor helium lines. The star lost both, or its helium stays hidden.";
    public const string TypeIcBL =
        "Broad-lined Type Ic: a Type Ic whose ejecta move much faster than usual, smearing its lines out. Some come with a " +
        "long gamma-ray burst, like SN 1998bw.";
    public const string TypeIbn = "Type Ibn: narrow helium lines from dense helium-rich gas around a stripped star.";
    public const string TypeIcn = "Type Icn: narrow carbon and oxygen lines from dense gas around a star stripped of hydrogen and helium.";
    public const string SlsnI =
        "Hydrogen-poor superluminous supernova: tens of times brighter than an ordinary supernova, peaking near absolute " +
        "magnitude −21 or brighter. A newborn magnetar's spin-down is the usual explanation.";
    public const string SlsnII = "Hydrogen-rich superluminous supernova: shows hydrogen and peaks near absolute magnitude −21 or brighter.";
    public const string TypeIa =
        "Type Ia: a white dwarf blown apart by runaway carbon burning. No hydrogen; strong silicon lines. They peak near " +
        "absolute B magnitude −19.3, and brighter ones fade more slowly, which makes them good distance markers.";
    public const string TypeIa91T = "A Type Ia like SN 1991T: brighter than normal and slower to fade, with more radioactive nickel.";
    public const string TypeIa91bg = "A Type Ia like SN 1991bg: fainter, redder and faster to fade than normal, with little radioactive nickel.";
    public const string TypeIax =
        "Type Iax: a weaker cousin of Type Ia, with slower ejecta (about 2,000–8,000 km/s) and less light; part of the white " +
        "dwarf may survive.";
    public const string Pisn = "Pair-instability supernova: the whole star is destroyed, often making several solar masses of radioactive nickel.";

    public const string NeutronStar =
        "The collapsed core: more than the Sun's mass squeezed into a ball about 24 km across (radius about 12 km), as dense " +
        "as an atomic nucleus.";
    public const string Magnetar =
        "A neutron star with an extreme magnetic field, about 10¹⁴–10¹⁵ gauss. As it spins down it can pour energy into the " +
        "ejecta and make the supernova far brighter.";
    public const string BlackHole = "A collapsed object whose gravity lets nothing escape, not even light.";
    public const string GammaRayBurst =
        "A jet punches out of a collapsing, fast-spinning star at nearly the speed of light and makes a flash of gamma rays " +
        "lasting tens of seconds. It is seen only from inside the jet.";

    public const string ExplosionEnergy =
        "The energy that drives the ejecta outward, mostly ending as their motion. Ordinary supernovae have about 10⁵¹ erg; " +
        "the neutrinos carry away a few hundred times more.";
    public const string Erg = "A small unit of energy: 10⁻⁷ joule. Supernova energies are about 10⁵¹ erg.";
    public const string EjectaMass = "The mass thrown out by the explosion, in solar masses.";
    public const string Nickel56 =
        "Radioactive nickel-56 made in the explosion. It decays to cobalt-56 (half-life 6.1 days) and then to iron " +
        "(77 days); that decay powers most of the supernova's light after the first weeks.";
    public const string Titanium44 =
        "Radioactive titanium-44, made in small amounts. With a half-life of about 59 years it keeps young remnants such as " +
        "Cassiopeia A and SN 1987A glowing in gamma-ray and hard X-ray lines.";
    public const string ClassificationLuminosity =
        "The engine's estimate of how luminous the supernova is (for a plateau supernova, its plateau luminosity). It is " +
        "used only to choose the label, for example whether it counts as superluminous. The peak brightness shown on the " +
        "summary comes from the drawn light curve instead.";
    public const string PeakBrightness =
        "The brightest point of the light curve drawn in the visible-light panel, as an absolute V magnitude: how bright it " +
        "would look from 10 parsecs. Smaller numbers are brighter.";
    public const string AbsoluteMagnitude =
        "How bright something would look from a standard distance of 10 parsecs, on a backwards scale: smaller is brighter, " +
        "and 5 magnitudes are a factor of 100.";
    public const string ApparentMagnitude =
        "How bright something looks from here, after distance and dust. Smaller is brighter; the naked eye reaches about 6 " +
        "under a dark sky.";
    public const string MagnetarEnergy = "The magnetar's spin energy, which it can give to the ejecta as it slows down.";
    public const string MagnetarTimescale = "How long the magnetar takes to lose most of its spin energy.";
    public const string PhillipsRelation =
        "For Type Ia supernovae, brighter events fade more slowly after the peak (the Phillips relation). This B-band peak is " +
        "the engine's estimate from its peak luminosity (B-band correction about 0); it does not shape the drawn light " +
        "curve. The stated peak brightness and the decline rate Δm15(B) are read off the drawn curve, whose shape does " +
        "not follow the Phillips relation.";
    public const string DeclineRate = "Δm15(B): how many magnitudes a Type Ia fades in the B band during the 15 days after its peak.";
    public const string RemnantMass =
        "The remnant's mass. For a neutron star this is the gravitational mass, which is less than the mass of the matter " +
        "that formed it because the binding energy left as neutrinos.";
    public const string NeutronStarRadius = "The neutron star's radius, about 12 km.";
    public const string SpinPeriod = "How long the newborn neutron star takes to spin once.";
    public const string MagneticField =
        "The strength of the neutron star's surface magnetic field, in gauss. Earth's field is about half a gauss.";
    public const string Kick =
        "Neutron stars are born moving fast, typically a few hundred km/s, because the explosion is slightly lopsided.";
    public const string BlackHoleForms = "When the black hole forms, counted from core bounce.";
    public const string FinalMass = "The star's mass when it dies, after its winds and any companion have taken their share.";
    public const string HeliumCore = "The mass inside the hydrogen-burning shell: the helium core, including the carbon-oxygen core inside it.";
    public const string CarbonOxygenCore = "The innermost layers, made of carbon and oxygen, around the iron core that collapses.";
    public const string HydrogenEnvelope =
        "The outer hydrogen-rich layers. A thick envelope makes a Type II-P; a thin one a II-L or IIb; none at all a Type Ib or Ic.";
    public const string HeliumLayer = "The helium-rich layer between the hydrogen envelope and the carbon-oxygen core.";
    public const string StarRadius = "The star's radius when it dies, in solar radii. Red supergiants are hundreds of times larger than the Sun.";
    public const string SurfaceTemperature = "The temperature of the star's visible surface when it dies, in kelvin.";
    public const string MassLossRate =
        "How much mass the star's wind blows away each year just before it dies. It sets how much gas the shock runs into.";
    public const string WindSpeed = "How fast the star's wind leaves it.";
    public const string GrbIsotropicEnergy =
        "The gamma-ray energy the burst would have if it shone equally in all directions. The real energy is much smaller, " +
        "because the jets are narrow.";
    public const string GrbPeakEnergy = "The photon energy at which the burst gives off most of its power, in the burst's own frame.";
    public const string T90 = "How long the burst lasts: the time over which 90% of its gamma rays arrive.";
    public const string PulsarRadioLuminosity =
        "How bright the pulsar is in radio at 1.4 GHz, as flux density times distance squared (mJy kpc²).";

    public const string Established = "Established: measured, or well-tested physics. Small uncertainty.";
    public const string ModelDependent =
        "Model-dependent: from a published model that matches observations, but it depends on that model's assumptions.";
    public const string Speculative =
        "Speculative: an approximation, an extrapolation, or physics that is still debated. It could be off by a factor of a " +
        "few.";
    public const string Notes = "Plain-language explanations of each decision the lab made, and of any input that had no effect.";
    public const string OutcomeDetails = "Every number of the outcome, with its confidence, range and full source, plus all the notes.";
    public const string OutcomeMap =
        "What happens to stars of every initial mass, with your other inputs held fixed. The colors show the fate; the " +
        "white line is your star. Click the strip to choose a mass. The boundaries come from the same model rules as the " +
        "outcome, so they are model-dependent; hover a band for its mass range.";

    // ---------------------------------------------------------------- time (spec §4)

    public const string ObserverTime =
        "Time since core bounce (or ignition), as clocks on Earth count it from the first signal's arrival. Beyond 50 Mpc " +
        "the expansion of the universe stretches it by 1 + z.";
    public const string LogTimeline =
        "The timeline runs from a thousandth of a second to 1,000 years. Each equal step along it is the same factor in " +
        "time, so the first second gets as much room as the years after it. Click a marker to jump to that event.";
    public const string PlaybackSpeed =
        "How fast time runs, in factors of 10 per second: at 1/3, time grows tenfold every 3 seconds, and the whole " +
        "timeline plays in about 40 seconds.";
    public const string LightTravelTime =
        "Everything we see of a supernova happened long ago: its light, neutrinos and gravitational waves all travel at the " +
        "speed of light. Beyond 50 Mpc this is the lookback time, which allows for the expansion of the universe.";
    public const string NeutrinoBurst =
        "About 99% of the collapse energy leaves as neutrinos within about 10 seconds. Neutrinos have a tiny mass, so they " +
        "arrive slightly after light would; the delay is under a second at 10 kpc and is ignored here. Light from SN 1987A " +
        "arrived hours after its neutrinos, because light escapes only when the shock reaches the surface.";
    public const string GravitationalWaves =
        "Ripples in space from the collapsing core's violent motions in its first second. Current detectors could catch " +
        "them only from a supernova in our own galaxy, and for a slowly spinning core only within a few kpc.";
    public const string ShockBreakout =
        "The moment the shock reaches the star's surface: the first light, a brief flash of X-rays and ultraviolet. It comes " +
        "seconds after collapse for a compact star and up to a day or so for a red supergiant.";
    public const string PlateauEnd =
        "The end of the plateau: the hydrogen envelope has cooled all the way through, and the brightness drops onto the " +
        "slow radioactive tail.";
    public const string GammaRayLines =
        "When the 847 keV gamma-ray line of cobalt-56 is brightest. Early on the ejecta trap the gamma rays; later the " +
        "cobalt has decayed. The line peaks in between.";
    public const string RadioPeak =
        "When the 5 GHz radio emission peaks. The shock running into the star's old wind glows in radio, which at first is " +
        "absorbed by the glowing gas itself.";
    public const string PulsarEmerges =
        "When a young pulsar's radio pulses could get out: for years to decades the ejecta block radio. The lab uses " +
        "10 years, an approximation (signals §8.3).";
    public const string RemnantResolvable =
        "When the expanding remnant looks at least 0.5 arcseconds across from here, Chandra's resolution, so it can be seen " +
        "as an object with a shape rather than a point.";
    public const string Instruments =
        "Opens the six instrument panels: gravitational waves, neutrinos, gamma rays, X-rays and ultraviolet, visible light " +
        "and radio.";
    public const string InstrumentStrip =
        "One small tile per messenger, each showing its main signal and whether it would be detected. Click a tile to open " +
        "its panel.";
    public const string SceneView =
        "A simple 3D view of the star, the expanding ejecta and what is left behind. The radius at top left is the outer " +
        "edge of the debris in the lab's simple model (a uniform ball of gas; the fastest layers are really faster), which " +
        "blends into the remnant model's blast wave between 3 and 10 years; from 10 years it is the drawn forward shock. The grey circles are measuring rings, labelled with their distance from the centre; " +
        "the view steps back to a larger scale when the debris reach the outer ring. " +
        "Colours and the gas around the star (its faint wind haze and any dense shell) are schematic. Drag to turn, wheel to zoom.";

    public const string GuidedTour =
        "A narrated walk through this explosion, from the first millisecond onward: the timeline plays between the stops, " +
        "slows down as it nears each one, and rests there while the caption is read. Sentences that begin \"In the lab\" come " +
        "from the lab's own model, and those that begin \"Expected\" are the model's expectation of a signal that no supernova has ever " +
        "been seen to give; those that begin \"Observed\" are real measurements, marked \"recalled\" when the research " +
        "file quotes them from memory and the paper was not re-opened. Hover over a caption to see where each sentence comes " +
        "from and how firm it is. Moving the camera or the timeline pauses the tour; \"Resume tour\" carries on. Choosing a " +
        "new explosion ends it.";

    public const string DistanceRings =
        "Measuring circles, not part of the supernova: when the debris reach a ring, they are that far from the centre.";

    public const string CloseUp =
        "Opens the Close-up window: what the explosion leaves at its centre (a neutron star or a black hole), seen from close by, " +
        "on the same timeline as this window.";
    public const string CloseUpView =
        "The compact remnant from close by, a place no telescope could reach. Around a black hole, the light of the explosion's " +
        "scene is bent exactly as general relativity says. Drag to turn, wheel to zoom, Home to frame it again.";
    public const string CloseUpGrid =
        "A measuring grid of latitude and longitude lines on a sphere round the black hole, bent like everything behind it.";
    public const string FieldLines = "The neutron star's magnetic field lines, which leave and re-enter its surface.";
    public const string LightCylinder =
        "Where the neutron star's field would have to turn as fast as light to keep up with the spin: the edge of the region it drags round with it.";
    public const string CloseUpCutaway = "Cuts the neutron star open to show its layers: the crust, and the core below it.";
    public const string NeutronDrip =
        "The neutron drip line: deep in the crust, where the density reaches about 4.3 × 10¹¹ g/cm³ (Baym, Pethick & Sutherland 1971; " +
        "modern models 4.3–4.4 × 10¹¹) and the nuclei can hold no more neutrons, so neutrons start to leak out and run free between them. " +
        "Established theory, observed indirectly: pulsar glitches (Link, Epstein & Lattimer 1999), which need superfluid neutrons in the " +
        "inner crust, and the crusts of neutron stars cooling after accretion outbursts (KS 1731−260, MXB 1659−29; Cackett et al. 2006).";
    public const string NuclearPasta =
        "Near the crust's base, where the nuclei are nearly touching, theory says they merge into rods and slabs, named for spaghetti and " +
        "lasagna. Model-dependent: some models have a thick pasta layer (its share of the inner crust is drawn as 30%, a median), some none.";
    public const string InnerCrust =
        "The deeper part of the crust, from the neutron drip line down to the core: a lattice of nuclei bathed in free neutrons, which " +
        "become superfluid as the star cools.";
    public const string OuterCrust = "The upper part of the crust, above the neutron drip line: a lattice of nuclei and electrons.";
    public const string Superfluid =
        "A fluid that flows with no friction. Neutrons in the inner crust are thought to turn superfluid as the star cools; its " +
        "loose, frictionless flow explains the sudden speed-ups (glitches) seen in pulsars such as Vela.";
    public const string OuterCore = "The star's outer core: neutrons, with some protons, electrons and muons.";
    public const string InnerCore =
        "Inner core: unknown. At the centre, more than twice nuclear density, what lies there is not settled: nucleons only, particles " +
        "with strange quarks (hyperons), condensates of pions or kaons, or free quark matter. A low-mass star may have no inner core.";
    public const string NeutronStarAtmosphere = "The star's atmosphere is only centimetres thick: drawn as a labelled line at the surface.";
    public const string ProtoNeutronStar =
        "The newborn neutron star: hot and liquid right through. Its crust crystallises as it cools, about a day after the bounce " +
        "(approximate: from an hour to a month).";
    public const string MagnifiedStrip =
        "The crust is only about a kilometre thick, so a strip of the outermost layers is drawn larger than the rest, which is to scale. " +
        "The label gives how many times larger.";
    public const string CloseUpLabels = "Shows or hides the labels on the view, such as the measuring rings' distances.";
    public const string CloseUpSound =
        "Plays the pulsar's pulses as clicks, one per pulse at the real rate, as radio astronomers hear them. Off until you switch it on. It sounds only while the timeline is playing and pulses would reach Earth; the trace says why when it is silent. Nothing here depends on hearing it.";
    public const string CloseUpPulseTrace =
        "A radio telescope's trace: a mark each time a drawn beam sweeps across Earth, over the last 10 seconds. The picture is slowed so nothing flashes more than once a second; the sound, if on, plays at the real rate.";
    public const string CloseUpFocusView = "Shows the view alone, without the column of numbers beside it.";
    public const string CloseUpLightBendingDetail =
        "How finely the bending of light round the black hole is worked out. Full is sharpest and slowest. Auto picks a coarser level while the view moves, if the graphics card needs it, and full resolution once it stops.";

    public const string Cutaway =
        "Removes one quarter of the picture, like a wedge cut from a cake, so you can see inside. The two flat faces of the cut " +
        "are coloured by element (schematic colours: the key lists them), and the rest of the debris keeps its heat colour. " +
        "Before breakout the faces show the star's layers, drawn to the research's radii.";
    public const string MixingFingers =
        "Rayleigh–Taylor fingers: heavy elements from the core punch out through the lighter layers as the shock decelerates.";
    public const string ForwardShock =
        "The forward shock is the outer edge of the explosion: a shock wave running ahead of the debris into the gas around the " +
        "star, heating it to millions of degrees. The pale shell is that swept-up gas, brightest at its rim where the line of " +
        "sight crosses the most of it. Where the lab models the star's wind (most core-collapse outcomes), its radius in the first " +
        "years is the radio model's shock, joined over 3 to 10 years to Truelove & McKee's solution for the debris' steep outer " +
        "layers; for an outcome without that wind model (a Type Ia, for one) it is drawn from 3 years on Truelove & McKee's solution " +
        "alone. From 10 years it is larger at first than the remnant panel's uniform-debris radius.";
    public const string WindHaze =
        "The wind the star blew before it died, drawn as a faint haze that thins as one over the distance squared and ends " +
        "where the wind's pressure balances the surrounding gas (about 1 to 20 light-years across). The shock lights it up where " +
        "it arrives; the shocked gas inside belongs to the swept-up shell.";
    public const string DenseCircumstellarShell =
        "Dense gas the star threw off in its last years (Type IIn and similar explosions, or SN 2023ixf's confined gas). It flares " +
        "when the shock runs into it. SN 2023ixf's extent is observed (Jacobson-Galán et al. 2023); for the lab's own dense-gas " +
        "outcomes it is a schematic estimate from the research.";
    public const string ReverseShock =
        "The reverse shock: when the swept-up gas slows the debris down, a second shock runs back into the debris themselves " +
        "(outward at first, but more slowly than they move) and heats them. Debris it has passed are drawn hot and bright, packed " +
        "between it and the contact surface. In gas of one atom per cm³ it reaches the centre after about 1,400 years (a Type Ia) to about " +
        "10,000 years (a heavy core-collapse explosion) (Truelove & McKee 1999).";
    public const string ContactSurface =
        "The contact surface separates the star's own debris (inside) from the swept-up surrounding gas (outside); both have " +
        "been shocked. It lies between the reverse shock and the forward shock (Chevalier 1982; Tang & Chevalier 2017).";
    public const string ShockInStar =
        "The shock wave on its way out through the star after the core bounces, drawn as a bright ring with the gas inside it " +
        "brighter. It takes seconds to days to reach the surface, and its position over time is a schematic estimate from the " +
        "research's breakout calculation.";

    public const string PulsarWindNebula =
        "A bubble of charged particles and magnetic field blown by a young pulsar's wind, glowing blue-white (synchrotron light) " +
        "inside the expanding debris. It grows as a power of the age (about the 1.25th) and is bounded by the debris. The Crab " +
        "Nebula is the best-known one; the lab's model comes out smaller than the real Crab, whose mean radius is about 1.7 parsecs.";
    public const string Sn1987aRings =
        "The three glowing rings of gas around SN 1987A: an equatorial ring, the size of 0.665 light-years, and two larger ones " +
        "above and below it, shed by the star about 20,000 years before it exploded. Their size and tilt are measured (Sugerman et " +
        "al. 2005), not predicted by the lab. The south ring is in front of the equatorial ring and the north ring behind it. " +
        "The bright beads are hotspots, where the blast wave strikes dense lumps of the ring. Their rise follows the ring's soft X-ray light curve (from about day 3,000, 1995, to a plateau near day 10,000); the optical ring peaked about 2009. The fade drawn after day 10,000 is a schematic straight line to 2035, a guess.";

    // ---------------------------------------------------------------- instrument panels' views (spec §5.1)

    public const string Strain =
        "Strain h: the fraction by which a passing gravitational wave stretches and squeezes space, drawn as it would " +
        "arrive at Earth.";
    public const string Spectrogram =
        "How strong the signal is at each frequency over time. Brighter colours mean more power, on a logarithmic scale.";
    public const string NeutrinoEventRate = "How many neutrinos the selected detector would record each second.";
    public const string NeutrinoCumulativeEvents =
        "The running total of neutrinos the selected detector would record since core bounce. The dots are one simulated " +
        "run of individual events; Re-roll draws another.";
    public const string NeutrinoLuminosity = "The power carried off by each kind (flavour) of neutrino, in erg per second.";
    public const string NeutrinoMeanEnergy =
        "The average energy of the anti-electron neutrinos, in MeV. Higher-energy neutrinos are easier to detect.";
    public const string NickelCobaltLines =
        "Gamma-ray lines from radioactive nickel-56 and its daughter cobalt-56, the decays that power the supernova's light. " +
        "They escape once the ejecta have thinned out.";
    public const string ShockInWind =
        "The shock running into the gas the star shed before it died. It glows in radio and X-rays for years to decades.";
    public const string GrbAfterglow =
        "The fading glow of a gamma-ray burst's jets as they plough into the surrounding gas, seen in X-rays, light and radio.";
    public const string NeutronStarCooling =
        "The newborn neutron star's hot surface, glowing in soft X-rays as it cools over the centuries.";
    public const string LightCurve =
        "How the supernova's brightness changes with time, in apparent magnitude: smaller numbers are brighter, and about +6 " +
        "is the faintest the naked eye sees under a dark sky.";
    public const string SchematicSpectrum =
        "A sketch of the supernova's spectrum now: a glowing surface (a blackbody) with the dips and bumps of the lines that " +
        "identify its type. It is drawn from a line list, not measured.";
    public const string PulsarPulses =
        "A pulsar is a spinning neutron star whose radio beams sweep past Earth like a lighthouse's, giving one pulse each turn.";
    public const string SupernovaRemnant =
        "The expanding cloud of debris left by the explosion, a supernova remnant, which glows in radio for thousands of years.";

    private static readonly Lazy<IReadOnlyDictionary<string, string>> Terms = new(() =>
        typeof(Glossary).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .ToDictionary(f => f.Name, f => (string)f.GetRawConstantValue()!));

    /// <summary>Every term, by key (the constant's name).</summary>
    public static IReadOnlyDictionary<string, string> All => Terms.Value;

    /// <summary>The text for this key. Throws <see cref="KeyNotFoundException"/> for an unknown key.</summary>
    public static string Get(string key)
        => All.TryGetValue(key, out var text) ? text : throw new KeyNotFoundException($"No glossary term '{key}'.");
}
