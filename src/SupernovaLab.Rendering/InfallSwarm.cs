namespace SupernovaLab.Rendering;

/// <summary>
/// Task 20 (the owner: the first second's dots "sometimes move inward and sometimes outward"): the infalling matter's dots, each with its
/// own radius in km. Before, the shader placed every dot between the shock and <see cref="CoreAnimation.InfallOuterFactor"/> shock radii
/// each frame, so a growing shock pushed them all outward with it, which matter ahead of a shock never does. Now a dot only falls, at the
/// free-fall profile |v| = v_sh √(R_sh / r) of the research's speed (compact §C1) slowed down as <see cref="CoreAnimation.Slowdown"/>
/// says; when the shock reaches it, it fades out there over <see cref="FadeS"/> (swept up by the shock) and comes back, fading in, in the
/// outer half of the band (fresh matter falling from farther out). Its brightness never changes faster than one fade in
/// <see cref="FadeS"/> (flash safety, spec §1). The dots' directions are fixed (uniform on the sphere, the same every run).
/// </summary>
public sealed class InfallSwarm
{
    /// <summary>[Presentation] A dot's fade in or out, wall seconds.</summary>
    public const float FadeS = 0.5f;

    private readonly float[] _dir;      // 3 per dot
    private readonly double[] _r;
    private readonly float[] _in, _out; // fade-in progress 0..1; fade-out remaining 1..0, or −1 while falling
    private readonly bool[] _respawned;
    private readonly uint[] _hash;

    public InfallSwarm(int count)
    {
        Count = count;
        _dir = new float[3 * count];
        _r = new double[count];
        _in = new float[count];
        _out = new float[count];
        _respawned = new bool[count];
        _hash = new uint[count];
        var rng = new Random(1987);
        for (int i = 0; i < count; i++)
        {
            double z = 2 * rng.NextDouble() - 1, phi = 2 * Math.PI * rng.NextDouble(), s = Math.Sqrt(1 - z * z);
            (_dir[3 * i], _dir[3 * i + 1], _dir[3 * i + 2]) = ((float)(s * Math.Cos(phi)), (float)(s * Math.Sin(phi)), (float)z);
            _hash[i] = (uint)rng.Next() | 1u;
        }
    }

    public int Count { get; }

    public double RadiusKm(int i) => _r[i];

    /// <summary>The dot's opacity factor, 0 to 1 (its fade in times its fade out).</summary>
    public float Alpha(int i) => Math.Min(_in[i], 1f) * (_out[i] < 0 ? 1f : _out[i]);

    public bool IsFadingOut(int i) => _out[i] >= 0;

    /// <summary>Whether the dot came back farther out during the last <see cref="Step"/> (the one outward move allowed).</summary>
    public bool Respawned(int i) => _respawned[i];

    /// <summary>Spreads the dots over the band from the shock out, as a steady fall would (r^1.5 uniform), all fully shown.</summary>
    public void Reset(double shockKm)
    {
        double inner = Math.Pow(shockKm, 1.5), outer = Math.Pow(CoreAnimation.InfallOuterFactor * shockKm, 1.5);
        for (int i = 0; i < Count; i++)
        {
            double tau = Next(i);
            _r[i] = Math.Pow(outer - tau * (outer - inner), 2.0 / 3.0);
            (_in[i], _out[i], _respawned[i]) = (1f, -1f, false);
        }
    }

    /// <summary>Moves every dot on by <paramref name="wallDtS"/> of wall time (real time = wall ÷ <paramref name="slowdown"/>).</summary>
    public void Step(double wallDtS, double shockKm, double infallKmS, double slowdown)
    {
        if (!(shockKm > 0) || !(wallDtS >= 0)) return;
        double realDt = slowdown > 0 && double.IsFinite(infallKmS) ? wallDtS / slowdown : 0;
        float fade = (float)(wallDtS / FadeS);
        for (int i = 0; i < Count; i++)
        {
            _respawned[i] = false;
            if (_out[i] >= 0)
            {
                _out[i] -= fade;
                if (_out[i] > 0) continue;
                // Swept up and faded: fresh matter, in the band's outer half, fading in.
                double inner = Math.Pow(shockKm, 1.5), outer = Math.Pow(CoreAnimation.InfallOuterFactor * shockKm, 1.5);
                _r[i] = Math.Pow(outer - 0.5 * Next(i) * (outer - inner), 2.0 / 3.0);
                (_in[i], _out[i], _respawned[i]) = (0f, -1f, true);
                continue;
            }
            _in[i] = Math.Min(1f, _in[i] + fade);
            if (realDt > 0) _r[i] = Math.Max(0, _r[i] - infallKmS * Math.Sqrt(shockKm / _r[i]) * realDt);
            if (_r[i] <= shockKm) _out[i] = Math.Max(0f, 1f - fade);   // reached by the shock: it starts fading from its brightness now
        }
    }

    /// <summary>The dots for the vertex buffer: x, y, z (km) and opacity, 4 floats each.</summary>
    public float[] Vertices(float[]? into = null)
    {
        var v = into is { Length: var n } && n >= 4 * Count ? into : new float[4 * Count];
        for (int i = 0; i < Count; i++)
        {
            float r = (float)_r[i];
            v[4 * i] = _dir[3 * i] * r;
            v[4 * i + 1] = _dir[3 * i + 1] * r;
            v[4 * i + 2] = _dir[3 * i + 2] * r;
            v[4 * i + 3] = Alpha(i);
        }
        return v;
    }

    /// <summary>The dot's next number in [0, 1) (xorshift32 on its own state: the same every run).</summary>
    private double Next(int i)
    {
        uint x = _hash[i];
        x ^= x << 13; x ^= x >> 17; x ^= x << 5;
        _hash[i] = x;
        return x / 4294967296.0;
    }
}
