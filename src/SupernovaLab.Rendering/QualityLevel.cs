namespace SupernovaLab.Rendering;

/// <summary>The 3D view's particle budget (Project 2): the enum value is the number of particles drawn.</summary>
public enum QualityLevel
{
    High = 200_000,
    Medium = 100_000,
    Low = 50_000,
}

public static class QualityLevels
{
    /// <summary>The level the app starts at (set by the Project 2 performance test).</summary>
    public const QualityLevel Default = QualityLevel.High;

    /// <summary>Parses "high", "medium" or "low" (any case); null for anything else.</summary>
    public static QualityLevel? Parse(string? text) => text?.Trim().ToLowerInvariant() switch
    {
        "high" => QualityLevel.High,
        "medium" => QualityLevel.Medium,
        "low" => QualityLevel.Low,
        _ => null,
    };
}
