namespace SupernovaLab.App.Services;

/// <summary>
/// The pulsar's click train as samples (spec §4.4, ruling P3-10), synthesised in code: no recordings, no licence. One click per
/// pulse: a 2 ms raised-cosine envelope on a 1 kHz carrier (two cycles), schematic. The generator runs continuously, so a period that
/// changes (the pulsar spins down) changes the spacing smoothly, with no buffer to rebuild. A period under 1 ms is a continuous tone at
/// 1/P, which is what a click train at that rate is. No period, or a bad one, is silence (a click in flight finishes). Thread-safe:
/// the audio thread calls <see cref="Fill"/> while the UI thread sets the period and volume.
/// </summary>
public sealed class ClickTrain
{
    /// <summary>The click's length, s.</summary>
    public const double ClickS = 0.002;

    /// <summary>Under this period the click train is a tone at 1/P, s.</summary>
    public const double ToneBelowS = 0.001;

    private const double CarrierHz = 1000;
    private const float ClickPeak = 0.8f, TonePeak = 0.4f;

    private readonly object _lock = new();
    private readonly double _rate;
    private double? _periodS;
    private float _volume = 0.5f;
    private double _pos = double.MaxValue;   // samples since the current click started (huge: none in flight)
    private double _tonePhase;

    public ClickTrain(int sampleRate = 44100) => _rate = sampleRate > 0 ? sampleRate : throw new ArgumentOutOfRangeException(nameof(sampleRate));

    public int SampleRate => (int)_rate;

    /// <summary>0 to 1.</summary>
    public float Volume
    {
        get { lock (_lock) return _volume; }
        set { lock (_lock) _volume = float.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0; }
    }

    /// <summary>The pulse period, s; null, or a non-positive or non-finite one, is silence. A first period starts a click at once.</summary>
    public void SetPeriod(double? periodS)
    {
        lock (_lock)
        {
            double? p = periodS is { } v && v > 0 && double.IsFinite(v) ? v : null;
            if (p is not null && _periodS is null && _pos >= ClickS * _rate) _pos = 0;   // a click in flight is not cut
            _periodS = p;
        }
    }

    /// <summary>Writes <paramref name="count"/> mono samples.</summary>
    public int Fill(float[] buffer, int offset, int count)
    {
        lock (_lock)
        {
            double period = _periodS ?? 0, periodSamples = period * _rate;
            bool tone = period > 0 && period < ToneBelowS;
            double clickSamples = Math.Min(ClickS, period > 0 ? period : ClickS) * _rate;
            for (int i = 0; i < count; i++)
            {
                float v = 0;
                if (tone)
                {
                    v = TonePeak * (float)Math.Sin(_tonePhase);
                    _tonePhase += 2 * Math.PI / periodSamples;
                    if (_tonePhase > 2 * Math.PI) _tonePhase -= 2 * Math.PI;
                }
                else
                {
                    if (period > 0 && _pos >= periodSamples) _pos -= periodSamples;
                    if (_pos < clickSamples)
                    {
                        double env = 0.5 * (1 - Math.Cos(2 * Math.PI * _pos / clickSamples));
                        v = ClickPeak * (float)(env * Math.Sin(2 * Math.PI * CarrierHz * _pos / _rate));
                        _pos++;
                    }
                    else if (period > 0) _pos++;
                    else _pos = double.MaxValue;
                }
                buffer[offset + i] = v * _volume;
            }
        }
        return count;
    }
}
