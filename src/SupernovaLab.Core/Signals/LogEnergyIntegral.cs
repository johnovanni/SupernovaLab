namespace SupernovaLab.Core.Signals;

/// <summary>
/// ∫ f(E) d(ln E) over an energy band by Simpson's rule in ln E, the band integral the X-ray views share (the interaction
/// X-rays' gas transmission, the cooling neutron star's blackbody share).
/// </summary>
internal static class LogEnergyIntegral
{
    /// <summary>∫ f(E) d(ln E) from <paramref name="low"/> to <paramref name="high"/> (same units as E), with an even
    /// number of Simpson intervals.</summary>
    public static double Simpson(Func<double, double> f, double low, double high, int intervals)
    {
        if (intervals <= 0 || intervals % 2 != 0) throw new ArgumentOutOfRangeException(nameof(intervals));
        double a = Math.Log(low), h = (Math.Log(high) - a) / intervals, sum = 0;
        for (int i = 0; i <= intervals; i++)
        {
            double w = i == 0 || i == intervals ? 1 : i % 2 == 1 ? 4 : 2;
            sum += w * f(Math.Exp(a + i * h));
        }
        return sum * h / 3;
    }
}
