namespace SupernovaLab.Core.Units;

/// <summary>Where a number comes from: a literature citation and the section of our research files.</summary>
/// <example>("Sukhbold et al. 2016, ApJ 821, 38", "outcomes §1.2")</example>
public sealed record Source(string Citation, string ResearchRef);
