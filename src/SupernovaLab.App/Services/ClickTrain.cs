namespace SupernovaLab.App.Services;

/// <summary>
/// The pulsar's click train as samples (spec §4.4, ruling P3-10), synthesised in code: no recordings, no licence. One click per
/// pulse: a 6 ms raised-cosine envelope on a 500 Hz carrier (three cycles), schematic. Task 15 (the owner's first look): the carrier was
/// 1 kHz; it is lower now, and the click's peak higher (0.97 of full scale), because age-related hearing loss is usually worst at high
/// pitches. Task 16 (the owner's second look, still nothing heard): the click was 2 ms (one cycle); the ear sums a short sound's energy
/// over about a fifth of a second, so a click three times as long sounds about 5 dB louder at the same peak. Whole cycles under the
/// symmetric envelope sum to zero, so a click pushes the speaker out and back with no net thump. Task 16 also adds a test tone
/// (<see cref="StartTestTone"/>): 0.3 s at 400 Hz, played when the sound is switched on or its volume moved, so the speaker is heard
/// even when no pulses reach Earth.
/// The generator runs continuously, so a period that changes (the pulsar spins down) changes the spacing smoothly, with no buffer to
/// rebuild. A period under 1 ms is a continuous tone at
/// 1/P, which is what a click train at that rate is. No period, or a bad one, is silence (a click in flight finishes). Thread-safe:
/// the audio thread calls <see cref="Fill"/> while the UI thread sets the period and volume.
/// </summary>
public sealed class ClickTrain
{
    /// <summary>The click's length, s (Task 16: it was 2 ms).</summary>
    public const double ClickS = 0.006;

    /// <summary>Under this period the click train is a tone at 1/P, s.</summary>
    public const double ToneBelowS = 0.001;

    /// <summary>The click's carrier, Hz: three cycles in the 6 ms click (Task 15; it was 1 kHz).</summary>
    public const double CarrierHz = 500;

    /// <summary>Task 16: the test tone's pitch, Hz, and length, s; it fades in and out over <see cref="TestToneFadeS"/>.</summary>
    public const double TestToneHz = 400, TestToneS = 0.3, TestToneFadeS = 0.02;

    /// <summary>The default loudness (Task 15: it was 0.5).</summary>
    public const float DefaultVolume = 0.8f;

    // Three cycles under the raised cosine peak at 0.933 of the product's scale; 1.04 puts the click's peak at 0.97 of full scale.
    private const float ClickPeak = 1.04f, TonePeak = 0.4f, TestTonePeak = 0.8f;

    private readonly object _lock = new();
    private readonly double _rate;
    private double? _periodS;
    private float _volume = DefaultVolume;
    private long _clicks, _samples;
    private double _pos = double.MaxValue;   // samples since the current click started (huge: none in flight)
    private double _tonePhase;
    private long _testPos = long.MaxValue;   // samples since the test tone started (huge: none playing)

    public ClickTrain(int sampleRate = 44100) => _rate = sampleRate > 0 ? sampleRate : throw new ArgumentOutOfRangeException(nameof(sampleRate));

    public int SampleRate => (int)_rate;

    /// <summary>How many clicks <see cref="Fill"/> has written (a diagnostic: the launch log's "clicks queued").</summary>
    public long ClicksWritten { get { lock (_lock) return _clicks; } }

    /// <summary>How many samples <see cref="Fill"/> has written, silent ones included.</summary>
    public long SamplesWritten { get { lock (_lock) return _samples; } }

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

    /// <summary>Whether the test tone is playing.</summary>
    public bool TestToneActive { get { lock (_lock) return _testPos < TestToneSamples; } }

    private long TestToneSamples => (long)Math.Round(TestToneS * _rate);

    /// <summary>Task 16: starts the test tone, mixed over any clicks; asked again while it plays (a slider drag), it runs on unrestarted.</summary>
    public void StartTestTone()
    {
        lock (_lock)
        {
            if (_testPos >= TestToneSamples) _testPos = 0;
        }
    }

    /// <summary>Stops the test tone at once.</summary>
    public void StopTestTone()
    {
        lock (_lock) _testPos = long.MaxValue;
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
                        if (_pos < 1) _clicks++;   // a click's first sample
                        double env = 0.5 * (1 - Math.Cos(2 * Math.PI * _pos / clickSamples));
                        v = ClickPeak * (float)(env * Math.Sin(2 * Math.PI * CarrierHz * _pos / _rate));
                        _pos++;
                    }
                    else if (period > 0) _pos++;
                    else _pos = double.MaxValue;
                }
                if (_testPos < TestToneSamples) v = Math.Clamp(v + TestToneSample(), -1f, 1f);
                buffer[offset + i] = v * _volume;
            }
            _samples += count;
        }
        return count;
    }

    /// <summary>The test tone's next sample (under the lock): a sine with raised-cosine fades at both ends.</summary>
    private float TestToneSample()
    {
        long n = TestToneSamples, fade = (long)Math.Round(TestToneFadeS * _rate), k = _testPos++;
        double env = k < fade ? 0.5 * (1 - Math.Cos(Math.PI * k / fade))
                   : k > n - fade ? 0.5 * (1 - Math.Cos(Math.PI * (n - k) / fade))
                   : 1;
        return TestTonePeak * (float)(env * Math.Sin(2 * Math.PI * TestToneHz * k / _rate));
    }
}
