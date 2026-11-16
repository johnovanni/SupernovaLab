namespace SupernovaLab.Rendering;

/// <summary>What the lensing pass traces this frame (ruling R-P3-10).</summary>
public enum RayMapWork
{
    /// <summary>Nothing: the cached map is still right; the frame only samples the surroundings through it.</summary>
    None,

    /// <summary>The whole map, now, at <see cref="RayMapStep.Level"/>; it is shown this frame.</summary>
    Trace,

    /// <summary>A band of rows of a finer map being built behind the shown one; shown once its last band is traced.</summary>
    RefineStrip,
}

/// <summary>
/// One frame's tracing: the work, the level and the map's size in pixels, the band of rows to trace (all of them for
/// <see cref="RayMapWork.Trace"/>) and whether this band completes the map (it is then the one shown).
/// </summary>
public readonly record struct RayMapStep(RayMapWork Work, LensingLevel Level, int Width, int Height, int FirstRow, int Rows, bool Completes)
{
    /// <summary>The rays this frame traces.</summary>
    public int Pixels => Work == RayMapWork.None ? 0 : Width * Rows;
}

/// <summary>
/// Ruling R-P3-10, the cached ray map, as a pure state machine (the GL side is <see cref="LensingPass"/>). What a pixel's ray does
/// (captured, escaping along a direction, or hitting a neutron star's surface at a point) depends only on the camera's pose and
/// field of view, the spin, the surface radius, the grid switch, the viewport and the level: everything in
/// <see cref="LensingUniforms"/> except the surface colour, and never the time. So the map is traced only when one of those
/// changes, and every other frame only samples the (time-varying) environment through it.
/// <para>While the camera moves (the inputs differ from the shown map's) the whole map is traced at once at the moving level (the
/// governor's for Auto, or the user's). Once the inputs hold still, a finer still level (full resolution for Auto) is built behind
/// the shown map in bands of at most <see cref="PixelsPerFrame"/> rays, one band per frame, so no frame carries more than its
/// share; the finer map replaces the coarse one when its last band is traced. A coarser still level is traced at once (it is cheap).
/// <see cref="PixelsPerFrame"/> follows the measured GPU cost per ray (<see cref="ReportGpuTime"/>) so a band takes about
/// <see cref="RefineBudgetS"/>.</para>
/// </summary>
public sealed class RayMapPlanner
{
    /// <summary>The GPU time one refinement band aims at (s): a quarter of a 60 Hz frame.</summary>
    public const double RefineBudgetS = 0.004;

    /// <summary>The band size before any measurement: about 4 ms of tracing on Intel Iris Xe at spin 0.9 (Task 4's speed test).</summary>
    public const int DefaultPixelsPerFrame = 65_536;

    /// <summary>The smallest band, so a very slow GPU still finishes the refinement.</summary>
    public const int MinPixelsPerFrame = 4_096;

    /// <summary>The weight of each new measurement in the running means.</summary>
    private const double Smoothing = 0.25;

    private LensingUniforms _shownRays;
    private int _viewW, _viewH;
    private LensingLevel _refineLevel;
    private int _refineRow;
    private double _secondsPerRay = double.NaN, _otherWorkS;

    /// <summary>Whether a map has been traced (false at first and after <see cref="Invalidate"/>).</summary>
    public bool HasMap { get; private set; }

    /// <summary>The level of the map shown now.</summary>
    public LensingLevel ShownLevel { get; private set; } = LensingLevel.Full;

    /// <summary>Whether a finer map is being built behind the shown one.</summary>
    public bool IsRefining { get; private set; }

    /// <summary>The most rays one refinement band traces.</summary>
    public int PixelsPerFrame { get; private set; } = DefaultPixelsPerFrame;

    /// <summary>True when two sets of uniforms give the same rays: all equal except the surface colour (applied when sampling).</summary>
    public static bool SameRays(in LensingUniforms a, in LensingUniforms b)
        => (a with { SurfaceColor = default }) == (b with { SurfaceColor = default });

    /// <summary>The band size that takes <see cref="RefineBudgetS"/> at this cost per ray (s); the default when it is unknown.</summary>
    public static int PixelsFor(double secondsPerRay)
    {
        if (!(secondsPerRay > 0) || !double.IsFinite(secondsPerRay)) return DefaultPixelsPerFrame;
        double n = RefineBudgetS / secondsPerRay;
        return n >= int.MaxValue ? int.MaxValue : Math.Max(MinPixelsPerFrame, (int)Math.Round(n));
    }

    /// <summary>The next frame's tracing for these inputs: the moving level while they change, then the still level.</summary>
    public RayMapStep Next(in LensingUniforms rays, int viewWidth, int viewHeight, LensingLevel movingLevel, LensingLevel stillLevel)
    {
        bool changed = !HasMap || viewWidth != _viewW || viewHeight != _viewH || !SameRays(rays, _shownRays);
        if (changed || (int)stillLevel > (int)ShownLevel)
        {
            // Moving (or a coarser still level): the whole map now. A coarser level is cheap, and a moving camera needs it this frame.
            var level = changed ? movingLevel : stillLevel;
            (HasMap, _shownRays, _viewW, _viewH, ShownLevel, IsRefining) = (true, rays, viewWidth, viewHeight, level, false);
            var (w, h) = LensingLevels.TargetSize(viewWidth, viewHeight, level);
            return new RayMapStep(RayMapWork.Trace, level, w, h, 0, h, true);
        }
        if (stillLevel == ShownLevel)
        {
            IsRefining = false;
            return default;
        }
        // Still, and the still level is finer than the shown map: the next band of the finer map.
        if (!IsRefining || _refineLevel != stillLevel)
        {
            (IsRefining, _refineLevel, _refineRow) = (true, stillLevel, 0);
        }
        var (rw, rh) = LensingLevels.TargetSize(viewWidth, viewHeight, stillLevel);
        int rows = Math.Clamp(PixelsPerFrame / rw, 1, rh - _refineRow);
        int first = _refineRow;
        _refineRow += rows;
        bool completes = _refineRow >= rh;
        if (completes) (IsRefining, ShownLevel) = (false, stillLevel);
        return new RayMapStep(RayMapWork.RefineStrip, stillLevel, rw, rh, first, rows, completes);
    }

    /// <summary>Forgets the map (a new context, the lensing switched off): the next frame traces it again.</summary>
    public void Invalidate()
    {
        HasMap = false;
        IsRefining = false;
    }

    /// <summary>
    /// One drawn frame's GPU time (s) and the rays it traced (0 for a frame that only sampled): the cost per ray is the frame's time
    /// above the sampling frames' running mean, per ray. Sets <see cref="PixelsPerFrame"/>.
    /// </summary>
    public void ReportGpuTime(int raysTraced, double gpuTimeS)
    {
        if (!(gpuTimeS >= 0) || !double.IsFinite(gpuTimeS) || raysTraced < 0) return;
        if (raysTraced == 0)
        {
            _otherWorkS += Smoothing * (gpuTimeS - _otherWorkS);
            return;
        }
        double perRay = Math.Max(gpuTimeS - _otherWorkS, 0) / raysTraced;
        if (!(perRay > 0)) return;
        _secondsPerRay = double.IsNaN(_secondsPerRay) ? perRay : _secondsPerRay + Smoothing * (perRay - _secondsPerRay);
        PixelsPerFrame = PixelsFor(_secondsPerRay);
    }
}
