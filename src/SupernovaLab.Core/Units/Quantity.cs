namespace SupernovaLab.Core.Units;

/// <summary>A displayed number with its unit, confidence, source and optional uncertainty range.</summary>
public sealed record Quantity(double Value, string Unit, Confidence Confidence, Source Source,
                              double? Low = null, double? High = null, bool Published = false);
