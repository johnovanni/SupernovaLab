namespace SupernovaLab.Rendering;

/// <summary>
/// Steps the 3D view's particle budget down when the GPU cannot render it in time (Project 2, Task 11; ruling R-P2-30). Feed it each rendered
/// frame's GPU render time (a GL timer query around the scene's drawing, see <see cref="GpuTimer"/>) and the wall time since the previous
/// frame. Over each 3 s of frames it takes the mean render time; if that is above 1 / <see cref="MinFps"/> s (20 ms) it steps down one
/// <see cref="QualityLevel"/> (High to Medium to Low). Render time, not the frame interval, is measured: the host may run at 30 Hz, or an
/// on-demand frame rate may be bounded by an event rate, with a fast GPU. It never steps up by itself and never goes below
/// <see cref="QualityLevel.Low"/>; a manual choice (<see cref="Reset"/>) is the only way up. The window starts again after every change and
/// every <see cref="Reset"/>.
/// </summary>
public sealed class QualityGovernor
{
    /// <summary>The frame rate the GPU must be able to sustain: a mean render time above 1 / this steps down.</summary>
    public const double MinFps = 50;

    /// <summary>The length of the window the mean is taken over (wall time), s.</summary>
    public const double WindowS = 3;

    private double _elapsedS, _renderS;
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

    /// <summary>Adds one rendered frame (its GPU time and the wall time since the last frame, s); true when the level changed (a step down). Negative and non-finite times are ignored.</summary>
    public bool Update(double renderTimeS, double frameIntervalS)
    {
        if (!(renderTimeS >= 0) || !(frameIntervalS > 0) || !double.IsFinite(renderTimeS) || !double.IsFinite(frameIntervalS)) return false;
        _elapsedS += frameIntervalS;
        _renderS += renderTimeS;
        _frames++;
        if (_elapsedS < WindowS - 1e-9) return false;
        double mean = _renderS / _frames;
        _elapsedS = _renderS = 0;
        _frames = 0;
        if (!(mean > 1 / MinFps) || NextDown(Level) is not { } next) return false;
        Level = next;
        return true;
    }

    /// <summary>Takes this level (a manual choice, or the controller's) and starts a new window.</summary>
    public void Reset(QualityLevel level)
    {
        Level = level;
        _elapsedS = _renderS = 0;
        _frames = 0;
    }
}
