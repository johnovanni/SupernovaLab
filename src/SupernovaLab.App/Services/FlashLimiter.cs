namespace SupernovaLab.App.Services;

/// <summary>
/// Flash safety for a giant flare's drawn brightness (spec §1 criterion 6, Review Focus 5; WCAG 2.3.1's 3 flashes a second): the
/// brightness the views really draw, from the brightness the model asks for, on the wall clock.
/// <list type="bullet">
/// <item>At most one rise per <see cref="MinRiseSpacingS"/> (0.5 s) of wall time, however fast the timeline is scrubbed back and
/// forth across a flare. A rise the limit refuses is held down: the output may fall, never rise, until the spacing has passed.</item>
/// <item>A rise takes at least <see cref="MinRiseS"/> (0.15 s) of wall time, even when the request jumps (a scrub straight to the
/// flare's peak): the output climbs at most 1 per 0.15 s.</item>
/// <item>The output stays in 0..1.</item>
/// </list>
/// One instance per view; call <see cref="Apply"/> once per drawn frame.
/// </summary>
public sealed class FlashLimiter
{
    /// <summary>The fewest wall-clock seconds between the starts of two rises.</summary>
    public const double MinRiseSpacingS = 0.5;

    /// <summary>The fewest wall-clock seconds a rise from nothing to full brightness takes.</summary>
    public const double MinRiseS = 0.15;

    private const double RisingEpsilon = 1e-4, MaxFrameS = 1.0 / 30;

    private double _output, _lastWallS = double.NaN, _lastRiseStartS = double.NegativeInfinity;
    private bool _rising;

    /// <summary>The limited brightness for this requested brightness at this wall-clock time (s, increasing).</summary>
    public double Apply(double intensity, double wallTimeS)
    {
        double want = double.IsFinite(intensity) ? Math.Clamp(intensity, 0, 1) : 0;
        double dt = double.IsNaN(_lastWallS) ? 0 : Math.Max(0, wallTimeS - _lastWallS);
        _lastWallS = wallTimeS;

        double result;
        if (want > _output + RisingEpsilon)
        {
            // The request rises. A new rise (after a fall, or from nothing) needs the spacing; one in progress carries on.
            if (!_rising && wallTimeS - _lastRiseStartS < MinRiseSpacingS)
            {
                result = _output;
            }
            else
            {
                if (!_rising) _lastRiseStartS = wallTimeS;
                _rising = true;
                // A frame gap longer than this (nothing was drawn meanwhile, or the first frame) counts as one frame, so the first drawn
                // step of a rise is a frame's worth, not a jump.
                double step = (dt > 0 ? Math.Min(dt, MaxFrameS) : MaxFrameS) / MinRiseS;
                result = Math.Min(want, _output + step);
            }
        }
        else
        {
            if (want < _output - RisingEpsilon) _rising = false;
            // A fall is slewed too (at least MinRiseS from full to nothing), so a flare the limiter has clipped never ends as a hard cut.
            double fall = (dt > 0 ? Math.Min(dt, MaxFrameS) : MaxFrameS) / MinRiseS;
            result = Math.Max(want, _output - fall);
            result = Math.Min(result, _output);
            if (result <= 0) _rising = false;
        }
        _output = result;
        return result;
    }

    private object? _context;
    private int _run;

    /// <summary>
    /// Notes the outcome and run the brightness is for (a new outcome or Re-roll gives a new flare list). It never zeroes the output and never
    /// lifts the rise spacing: the last rise's wall time and the output are kept, so dragging a slider (a new outcome every frame) cannot
    /// cut the flare to nothing and re-rise it without the 0.5 s between rises. A request that drops to 0 falls on the slewed fall.
    /// Returns whether the context changed. Call once per drawn frame.
    /// </summary>
    public bool ResetIfChanged(object? context, int run)
    {
        if (ReferenceEquals(context, _context) && run == _run) return false;
        (_context, _run) = (context, run);
        return true;
    }

}
