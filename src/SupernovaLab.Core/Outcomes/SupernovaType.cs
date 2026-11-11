namespace SupernovaLab.Core.Outcomes;

/// <summary>
/// The observed class of the explosion (spec §3; outcomes §3.2, §4.1, §8.2 step 6). <see cref="None"/> when there is no
/// supernova.
/// </summary>
public enum SupernovaType
{
    None,
    /// <summary>Type II-P: hydrogen lines and a long plateau (H-rich envelope ≳ 2 M☉).</summary>
    IIP,
    /// <summary>Type II-L: hydrogen lines, a thin envelope and no long plateau.</summary>
    IIL,
    /// <summary>Type IIb: a little hydrogen (≳ 0.001 M☉) that fades, leaving a helium-rich spectrum.</summary>
    IIb,
    /// <summary>Type IIn: narrow hydrogen lines from dense circumstellar gas.</summary>
    IIn,
    /// <summary>Type Ib: no hydrogen, helium lines.</summary>
    Ib,
    /// <summary>Type Ic: no hydrogen and no visible helium.</summary>
    Ic,
    /// <summary>Broad-lined Ic (hypernova).</summary>
    IcBL,
    /// <summary>Type Ibn: narrow helium lines from dense circumstellar gas.</summary>
    Ibn,
    /// <summary>Type Icn: narrow carbon and oxygen lines from dense circumstellar gas.</summary>
    Icn,
    /// <summary>Hydrogen-poor superluminous supernova.</summary>
    SlsnI,
    /// <summary>Hydrogen-rich superluminous supernova.</summary>
    SlsnII,
    Ia,
    Ia91T,
    Ia91bg,
    Iax,
    /// <summary>Pair-instability supernova.</summary>
    Pisn,
}
