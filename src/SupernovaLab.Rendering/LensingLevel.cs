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

    /// <summary>"full", "half" or "quarter".</summary>
    public static string Word(LensingLevel level) => level switch
    {
        LensingLevel.Half => "half",
        LensingLevel.Quarter => "quarter",
        _ => "full",
    };
}

/// <summary>
/// The user's light-bending detail (spec §2.5, ruling R-P3-9): Close-up menu ▸ Light-bending detail, remembered in layout.json.
/// <see cref="Auto"/> (the default) lets the <see cref="LensingGovernor"/> choose the level while the camera moves and refines to
/// full resolution when it stops; the others hold that level.
/// </summary>
public enum LensingDetail { Auto, Full, Half, Quarter }

public static class LensingDetails
{
    /// <summary>The level a fixed detail holds; null for <see cref="LensingDetail.Auto"/>.</summary>
    public static LensingLevel? Fixed(LensingDetail detail) => detail switch
    {
        LensingDetail.Full => LensingLevel.Full,
        LensingDetail.Half => LensingLevel.Half,
        LensingDetail.Quarter => LensingLevel.Quarter,
        _ => null,
    };

    /// <summary>The level traced while the camera moves: the fixed level, or the governor's for Auto.</summary>
    public static LensingLevel MovingLevel(LensingDetail detail, LensingLevel governed) => Fixed(detail) ?? governed;

    /// <summary>
    /// The level the view refines to once the camera stops (ruling R-P3-10): the fixed level, or full resolution for Auto. The cached
    /// ray map makes a still view cheap at any level, and the refinement is spread over frames, so Auto can afford full.
    /// </summary>
    public static LensingLevel StillLevel(LensingDetail detail) => Fixed(detail) ?? LensingLevel.Full;

    /// <summary>The Close-up's note: "Light bending: half resolution (your setting)" or "... (automatic)".</summary>
    public static string Note(LensingLevel shown, bool automatic)
        => $"Light bending: {LensingLevels.Word(shown)} resolution ({(automatic ? "automatic" : "your setting")})";

    /// <summary>A stored name ("Auto", "full", ...), case-insensitive; anything else (a number, an unknown name, null) is Auto.</summary>
    public static LensingDetail Parse(string? text)
        => text is not null && !text.Any(char.IsDigit) && Enum.TryParse(text, ignoreCase: true, out LensingDetail d) && Enum.IsDefined(d)
            ? d
            : LensingDetail.Auto;
}

/// <summary>
/// Steps the lensing resolution down when the Close-up's GPU time stays over <see cref="MaxGpuMs"/> (plan ruling P3-9), on Project 2's
/// <see cref="QualityGovernor"/> pattern: feed it each drawn frame's GPU time (a GL timer query around the Close-up's drawing,
/// <see cref="GpuTimer"/>) and the wall time since the previous frame; over each 3 s of frames it takes the mean GPU time, and above
/// 12 ms, or (ruling proposal R-P3-12) the frames average over 20 ms apart while the lensing takes over 2 ms of them, it steps down one
/// level (1/1 to 1/2 to 1/4). It never steps up by itself; <see cref="Reset"/> is the only way up. With the ray cache (R-P3-10) the
/// Close-up feeds it only the frames that traced a whole map (a moving camera), under the Auto detail only. Ruling R-P3-14: when one
/// level down (a quarter of the rays) would predictably still miss 50 fps or 12 ms, it steps two levels at once (1/1 to 1/4).
/// </summary>
public sealed class LensingGovernor
{
    /// <summary>A mean GPU time per Close-up frame above this (ms) steps the level down (ruling P3-9).</summary>
    public const double MaxGpuMs = 12;

    /// <summary>
    /// Ruling proposal R-P3-12 (Task 4b): fed frames averaging more than this far apart (ms; below 50 fps, spec §2.5) step the level
    /// down too, when the lensing takes more than <see cref="MinGpuMsForFrameRule"/> of them. The two GL views are drawn one after the
    /// other on the UI thread, so the Close-up's GPU time adds to the whole window's frame.
    /// </summary>
    public const double MaxFrameIntervalMs = 20;

    /// <summary>The least mean GPU time (ms) for the frame-rate rule: below it the lensing is not why the frames are slow.</summary>
    public const double MinGpuMsForFrameRule = 2;

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
        double meanInterval = _elapsedS / _frames;
        _elapsedS = _gpuS = 0;
        _frames = 0;
        // P3-9's rule, and (ruling proposal R-P3-12) below 50 fps while the lensing is a real part of the frame.
        bool tooSlow = mean > MaxGpuMs / 1000 || (meanInterval > MaxFrameIntervalMs / 1000 && mean > MinGpuMsForFrameRule / 1000);
        if (!tooSlow || LensingLevels.NextDown(Level) is not { } next) return false;
        // Ruling R-P3-14: one level down quarters the rays, so predict that level's frame: the interval less 3/4 of the lensing's GPU
        // time. If it would still be under 50 fps, or its GPU time still over 12 ms, go straight down two levels.
        double predictedInterval = meanInterval - mean * (1 - 1.0 / 4);
        if ((predictedInterval > MaxFrameIntervalMs / 1000 || mean / 4 > MaxGpuMs / 1000) && LensingLevels.NextDown(next) is { } two)
            next = two;
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
