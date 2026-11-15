namespace SupernovaLab.Rendering;

/// <summary>
/// The lensing pass's resolution (plan ruling P3-9): it traces one ray per pixel of the view divided by the enum value in each
/// direction (1/1, 1/2 or 1/4), then upsamples bilinearly.
/// </summary>
public enum LensingLevel
{
    Full = 1,
    Half = 2,
    Quarter = 4,
}

public static class LensingLevels
{
    /// <summary>"1/1", "1/2" or "1/4".</summary>
    public static string Text(LensingLevel level) => $"1/{(int)level}";

    /// <summary>The traced image's size for a view of this many pixels: the view divided by the level, rounded up, at least 1 × 1.</summary>
    public static (int Width, int Height) TargetSize(int viewWidth, int viewHeight, LensingLevel level)
    {
        int k = (int)level;
        return (Math.Max(1, (viewWidth + k - 1) / k), Math.Max(1, (viewHeight + k - 1) / k));
    }

    /// <summary>The level after this one, or null at <see cref="LensingLevel.Quarter"/>.</summary>
    public static LensingLevel? NextDown(LensingLevel level) => level switch
    {
        LensingLevel.Full => LensingLevel.Half,
        LensingLevel.Half => LensingLevel.Quarter,
        _ => null,
    };
}

/// <summary>
/// Steps the lensing resolution down when the Close-up's GPU time stays over <see cref="MaxGpuMs"/> (plan ruling P3-9), on Project 2's
/// <see cref="QualityGovernor"/> pattern: feed it each drawn frame's GPU time (a GL timer query around the Close-up's drawing,
/// <see cref="GpuTimer"/>) and the wall time since the previous frame; over each 3 s of frames it takes the mean GPU time, and above
/// 12 ms it steps down one level (1/1 to 1/2 to 1/4). It never steps up by itself; <see cref="Reset"/> is the only way up.
/// </summary>
public sealed class LensingGovernor
{
    /// <summary>A mean GPU time per Close-up frame above this (ms) steps the level down (ruling P3-9).</summary>
    public const double MaxGpuMs = 12;

    /// <summary>The length of the window the mean is taken over (wall time), s; as <see cref="QualityGovernor.WindowS"/>.</summary>
    public const double WindowS = QualityGovernor.WindowS;

    private double _elapsedS, _gpuS;
    private int _frames;

    public LensingGovernor(LensingLevel start) => Level = start;

    public LensingLevel Level { get; private set; }

    /// <summary>Adds one drawn frame (its GPU time and the wall time since the last frame, s); true when the level stepped down.</summary>
    public bool Update(double gpuTimeS, double frameIntervalS)
    {
        if (!(gpuTimeS >= 0) || !(frameIntervalS > 0) || !double.IsFinite(gpuTimeS) || !double.IsFinite(frameIntervalS)) return false;
        _elapsedS += frameIntervalS;
        _gpuS += gpuTimeS;
        _frames++;
        if (_elapsedS < WindowS - 1e-9) return false;
        double mean = _gpuS / _frames;
        _elapsedS = _gpuS = 0;
        _frames = 0;
        if (!(mean > MaxGpuMs / 1000) || LensingLevels.NextDown(Level) is not { } next) return false;
        Level = next;
        return true;
    }

    /// <summary>Takes this level and starts a new window.</summary>
    public void Reset(LensingLevel level)
    {
        Level = level;
        _elapsedS = _gpuS = 0;
        _frames = 0;
    }
}
