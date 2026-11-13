namespace SupernovaLab.Rendering;

/// <summary>
/// The 3D view's "director" camera (plan ruling P5). The view's half-height is always V / 0.9 for a ring value V on the
/// 1-2-5 ladder, so the outer ring sits on the 90% line. It holds still while the debris grow, steps two rungs out when the
/// framing radius reaches V, and two rungs in when the radius falls below 0.8 of the value two rungs down. A jump takes the
/// smallest V with radius ≤ 0.6 V. Steps ease log(half-height) with a smoothstep over real time.
/// </summary>
public sealed class CameraDirector
{
    /// <summary>Step out when framing radius ÷ outer ring reaches this.</summary>
    public const double StepOutAt = 1.0;
    /// <summary>Step in when framing radius falls below this × the ring value two rungs down.</summary>
    public const double StepInBelow = 0.8;
    /// <summary>A jump takes the smallest ring V with framing radius ≤ this × V.</summary>
    public const double JumpFraction = 0.6;
    /// <summary>The outer ring sits at this fraction of the half-height.</summary>
    public const double RingLine = 0.9;

    private readonly double _ease;
    private bool _started;
    private double _ringCm;
    private int _index;
    private double _fromLog, _toLog, _elapsed;

    public CameraDirector(double easeSeconds = 0.3) => _ease = easeSeconds > 0 ? easeSeconds : 0;

    /// <summary>The current physical half-height of the view (cm); 0 before the first framing radius.</summary>
    public double HalfHeightCm { get; private set; }

    /// <summary>True while a step is easing.</summary>
    public bool IsEasing { get; private set; }

    /// <summary>The outer ring value V (cm) the camera holds, or is easing to.</summary>
    public double OuterRingCm => _ringCm;

    /// <summary>Snaps to the smallest ring with framing radius ≤ 0.6 V (new outcome, timeline click, Home).</summary>
    public void Jump(double frameRadiusCm)
    {
        if (!Valid(frameRadiusCm)) return;
        _index = DistanceRings.IndexAtLeast(frameRadiusCm / JumpFraction);
        _ringCm = DistanceRings.TableValue(_index);
        HalfHeightCm = _ringCm / RingLine;
        _fromLog = _toLog = Math.Log(HalfHeightCm);
        _elapsed = 0;
        IsEasing = false;
        _started = true;
    }

    /// <summary>Advances by real (wall-clock) time and steps the ring if the framing radius calls for it. A bad radius keeps the scale.</summary>
    public void Update(double frameRadiusCm, double wallDeltaS)
    {
        if (!_started)
        {
            Jump(frameRadiusCm);
            return;
        }
        if (Valid(frameRadiusCm)) Retarget(frameRadiusCm);
        if (IsEasing) Advance(wallDeltaS > 0 ? wallDeltaS : 0);
    }

    private void Retarget(double r)
    {
        // Two rungs on the table of rings (see DistanceRings): a unit boundary is crossed consistently with Rungs.
        int last = DistanceRings.TableCount - 1;
        int idx = _index;
        while (idx < last && r >= StepOutAt * DistanceRings.TableValue(idx))
            idx = Math.Min(idx + 2, last);
        if (idx == _index)
            while (idx - 2 >= 0 && r < StepInBelow * DistanceRings.TableValue(idx - 2))
                idx -= 2;
        if (idx == _index) return;
        _index = idx;
        double ring = DistanceRings.TableValue(idx);
        _ringCm = ring;
        _fromLog = Math.Log(HalfHeightCm);
        _toLog = Math.Log(ring / RingLine);
        _elapsed = 0;
        IsEasing = true;
    }

    private void Advance(double dt)
    {
        _elapsed += dt;
        double u = _ease > 0 ? _elapsed / _ease : 1;
        if (u >= 1)
        {
            HalfHeightCm = _ringCm / RingLine;
            IsEasing = false;
            return;
        }
        double s = u * u * (3 - 2 * u);
        HalfHeightCm = Math.Exp(_fromLog + (_toLog - _fromLog) * s);
    }

    private static bool Valid(double r) => r > 0 && !double.IsInfinity(r);
}
