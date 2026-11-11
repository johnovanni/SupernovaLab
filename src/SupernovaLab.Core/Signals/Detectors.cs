using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Signals;

/// <summary>
/// Every instrument in spec §5's panel table, with its sensitivity from signals §11 and the section it details
/// (§1.4, §2.3, §3, §4.7, §5.4, §6, §8, §9). Later tasks use these Ids and add none.
/// Values marked [APPROX] are the research file's own placeholders, or a choice explained in the comment.
/// </summary>
public static class Detectors
{
    private const string Events20s = "events in 20 s";

    public static IReadOnlyList<Detector> All { get; } = new Detector[]
    {
        // ---- neutrinos (signals §1.4, §11). Event-counting detectors use Super-K's distant-supernova trigger:
        // 2 events within 20 s (Abdikamalov et al. 2020). [APPROX] The same rule is applied to Hyper-K, JUNO and DUNE.
        new("superk", "Super-Kamiokande", Messenger.Neutrinos, 2, Events20s,
            new Source("Abdikamalov et al. 2020, arXiv:2010.04356 (distant-SN trigger: 2 events within 20 s); " +
                       "Al Kharusi et al. 2021, NJP 23, 031201 (SNEWS 2.0; 32 kt water)", "signals §1.4, §11")),
        new("hyperk", "Hyper-Kamiokande", Messenger.Neutrinos, 2, Events20s,
            new Source("Al Kharusi et al. 2021, NJP 23, 031201 (217 kt fiducial); Super-K's 2-event rule [APPROX]",
                "signals §1.4, §11")),
        // [APPROX] IceCube sees no single events, only a collective rise in photon hits; about 6σ at 50 kpc (signals §11).
        // The research gives no detection threshold, so the conventional 5σ is used.
        new("icecube", "IceCube", Messenger.Neutrinos, 5, "σ (excess photon hits in 20 s)",
            new Source("Abbasi et al. 2011, A&A 535, A109; Al Kharusi et al. 2021 (about 6σ at 50 kpc); 5σ threshold [APPROX]",
                "signals §1.4, §11")),
        new("juno", "JUNO", Messenger.Neutrinos, 2, Events20s,
            new Source("Al Kharusi et al. 2021, NJP 23, 031201 (20 kt liquid scintillator); Super-K's 2-event rule [APPROX]",
                "signals §1.4, §11")),
        new("dune", "DUNE", Messenger.Neutrinos, 2, Events20s,
            new Source("Al Kharusi et al. 2021, NJP 23, 031201 (40 kt liquid argon, ν_e charged current); Super-K's " +
                       "2-event rule [APPROX]", "signals §1.4, §11")),

        // ---- gravitational waves (signals §2.3, §11): signal-to-noise ratio 8, as signals §2.3 uses for the ET range.
        new("aligo", "Advanced LIGO", Messenger.GravitationalWaves, 8, "SNR (optimal orientation)",
            new Source("Gossan et al. 2016, PRD 93, 042002; SNR 8 as for the ET range [DERIVED]", "signals §2.3, §11")),
        new("et", "Einstein Telescope", Messenger.GravitationalWaves, 8, "SNR (optimal orientation)",
            new Source("Radice et al. 2019; Abdikamalov et al. 2020 (range for SNR 8)", "signals §2.3, §11")),
        new("ce", "Cosmic Explorer", Messenger.GravitationalWaves, 8, "SNR (optimal orientation)",
            new Source("Srivastava et al. 2019, PRD 100, 043026; SNR 8 as for the ET range [DERIVED]", "signals §2.3, §11")),

        // ---- gamma rays (signals §5.4, §9.1, §11)
        new("gbm", "Fermi GBM", Messenger.GammaRays, 0.7, "photons cm⁻² s⁻¹ (50–300 keV peak, 1 s)",
            new Source("Meegan et al. 2009, ApJ 702, 791 [APPROX, verify]", "signals §9.1, §11")),
        new("bat", "Swift BAT", Messenger.GammaRays, 1e-8, "erg cm⁻² s⁻¹ (peak)",
            new Source("Swift BAT trigger threshold as given in the research file [APPROX; no paper cited]", "signals §9.1, §11")),
        new("spi", "INTEGRAL/SPI", Messenger.GammaRays, 3e-5, "photons cm⁻² s⁻¹ (narrow line, 3σ, 10⁶ s)",
            new Source("Roques et al. 2003, A&A 411, L91", "signals §5.4, §11")),
        new("cosi", "COSI", Messenger.GammaRays, 3.0e-6, "photons cm⁻² s⁻¹ (narrow line, 2-yr survey)",
            new Source("Tomsick et al. 2023, arXiv:2308.12362", "signals §5.4, §11")),
        // [APPROX] The research gives no NuSTAR line sensitivity, only that it detects Cas A's ⁴⁴Ti in Ms exposures. The
        // threshold is set at that flux, about 1.7e-5 photons cm⁻² s⁻¹ (signals §5.3, §11), so it is an upper bound.
        new("nustar", "NuSTAR", Messenger.GammaRays, 1.7e-5, "photons cm⁻² s⁻¹ (68/78 keV, Ms exposure)",
            new Source("Grefenstette et al. 2014, Nature 506, 339 (Cas A ⁴⁴Ti detected in Ms exposures) [APPROX]",
                "signals §5.3, §5.4, §11")),

        // ---- X-rays and UV (signals §3, §6.2, §11)
        // [APPROX] signals §6.2 gives 1e-13 to 1e-14 erg cm⁻² s⁻¹ in about 10 ks; the conservative end is used.
        new("swift-xrt", "Swift XRT", Messenger.XRaysUv, 1e-13, "erg cm⁻² s⁻¹ (0.3–10 keV, ~10 ks)",
            new Source("Swift XRT sensitivity as given in the research file [APPROX]; SN 2008D seen to ~200 Mpc " +
                       "(Soderberg et al. 2008, Nature 453, 469)", "signals §3.2, §6.2, §11")),
        new("einstein-probe", "Einstein Probe WXT", Messenger.XRaysUv, 2.5e-11, "erg cm⁻² s⁻¹ (1,000 s)",
            new Source("Einstein Probe WXT sensitivity as given in the research file [APPROX]", "signals §6.2, §11")),
        new("ultrasat", "ULTRASAT", Messenger.XRaysUv, 22.5, "AB mag (limiting, near-UV)",
            new Source("ULTRASAT limiting magnitude as given in the research file", "signals §3, §11")),
        new("chandra", "Chandra", Messenger.XRaysUv, 1e-15, "erg cm⁻² s⁻¹ (~100 ks)",
            new Source("Chandra sensitivity as given in the research file [APPROX]", "signals §6.2, §11")),
        new("xmm", "XMM-Newton", Messenger.XRaysUv, 1e-15, "erg cm⁻² s⁻¹ (~100 ks)",
            new Source("XMM-Newton sensitivity as given in the research file [APPROX]", "signals §6.2, §11")),

        // ---- visible light (signals §4.7, §11)
        new("eye", "Naked eye", Messenger.VisibleLight, 6, "V mag (limiting, dark sky)",
            new Source("Conventional naked-eye limit, roughly 6–6.5 [APPROX]; Adams et al. 2013, ApJ 778, 164",
                "signals §4.7, §11")),
        new("ztf", "ZTF", Messenger.VisibleLight, 20.5, "mag (limiting, per visit)",
            new Source("ZTF survey depth as given in the research file [APPROX]", "signals §4.7, §11")),
        new("rubin", "Rubin Observatory (LSST)", Messenger.VisibleLight, 24, "mag (limiting, per visit)",
            new Source("Rubin survey depth as given in the research file [APPROX]", "signals §4.7, §11")),
        // [APPROX] Classification needs about 10 times more light than detection, i.e. about 2.5 mag brighter than the
        // photometric limit (signals §11); taken against ZTF's 20.5: 20.5 − 2.5 = 18.
        new("spectrograph", "Typical spectrograph", Messenger.VisibleLight, 18, "V mag (classification limit)",
            new Source("Classification needs ~10× the light of detection: ZTF's 20.5 − 2.5 mag [APPROX]", "signals §10, §11")),

        // ---- radio (signals §6.1, §8.3, §11)
        new("vla", "VLA", Messenger.Radio, 0.03, "mJy (5σ)",
            new Source("5σ for a typical rms of 5–10 µJy per hour [DERIVED from an APPROX rms]", "signals §6.1, §11")),
        // [APPROX] The research quotes no MeerKAT continuum limit; the radio row's typical interferometer limit (quoted for
        // the VLA) is used.
        new("meerkat", "MeerKAT", Messenger.Radio, 0.03, "mJy (5σ)",
            new Source("The VLA's typical 5σ limit applied to MeerKAT [APPROX]", "signals §6.1, §11")),
        new("fast", "FAST", Messenger.Radio, 0.01, "mJy (1.4 GHz pulsar search)",
            new Source("\"Large telescopes reach about 10 µJy\" (Lorimer & Kramer 2005 radiometer scale) [APPROX]",
                "signals §8.3, §11")),
        new("ska", "SKA", Messenger.Radio, 0.01, "mJy",
            new Source("\"Large telescopes reach about 10 µJy\" applied to the SKA [APPROX]", "signals §8.3, §11")),
    };

    private static readonly Dictionary<string, Detector> ById = All.ToDictionary(d => d.Id, StringComparer.Ordinal);

    /// <summary>The detector with this Id. Throws <see cref="KeyNotFoundException"/> for an unknown Id.</summary>
    public static Detector Get(string id) => ById.TryGetValue(id, out var d)
        ? d
        : throw new KeyNotFoundException($"No detector with Id '{id}'.");
}
