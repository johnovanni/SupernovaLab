namespace SupernovaLab.Rendering;

/// <summary>
/// Steps the 3D view's particle budget down when it cannot keep up (Project 2, Task 11). Feed it each frame's time; over each
/// 3 s of frames it takes the mean frame rate (frames counted / seconds elapsed, so one long frame among good ones does not
/// count for much), and if that is below <see cref="MinFps"/> it steps down one <see cref="QualityLevel"/>
/// (High to Medium to Low). It never steps up by itself and never goes below <see cref="QualityLevel.Low"/>: a manual choice
/// (<see cref="Reset"/>) is the only way up. The window starts again after every change and every <see cref="Reset"/>.
/// </summary>
public sealed class QualityGovernor
{
    /// <summary>The mean frame rate below which the governor steps down.</summary>
    public const double MinFps = 50;

    /// <summary>The length of the window the mean is taken over, s.</summary>
    public const double WindowS = 3;

    private double _elapsedS;
    private int _frames;

    public QualityGovernor(QualityLevel start) => Level = start;

    public QualityLevel Level { get; private set; }

    /// <summary>The level after this one, or null at <see cref="QualityLevel.Low"/>.</summary>
    public static QualityLevel? NextDown(QualityLevel level) => level switch
    {
        QualityLevel.High => QualityLevel.Medium,
        QualityLevel.Medium => QualityLevel.Low,
        _ => null,
    };

    /// <summary>Adds one frame of this duration (s); true when the level changed (a step down). Non-positive and non-finite times are ignored.</summary>
    public bool Update(double frameTimeS)
    {
        if (!(frameTimeS > 0) || !double.IsFinite(frameTimeS)) return false;
        _elapsedS += frameTimeS;
        _frames++;
        if (_elapsedS < WindowS - 1e-9) return false;
        double fps = _frames / _elapsedS;
        _elapsedS = 0;
        _frames = 0;
        if (fps >= MinFps || NextDown(Level) is not { } next) return false;
        Level = next;
        return true;
    }

    /// <summary>Takes this level (a manual choice, or the controller's) and starts a new window.</summary>
    public void Reset(QualityLevel level)
    {
        Level = level;
        _elapsedS = 0;
        _frames = 0;
    }
}
