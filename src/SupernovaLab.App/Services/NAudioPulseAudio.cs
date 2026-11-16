using NAudio.Wave;

namespace SupernovaLab.App.Services;

/// <summary>
/// The pulsar sound on Windows' audio output (plan ruling P3-10): NAudio's <see cref="WaveOutEvent"/> (winmm) playing a generated
/// 44.1 kHz mono <see cref="ClickTrain"/>. The device is opened only when the sound is on and a period is first asked for, so a
/// window that never sounds never touches the audio system. A missing or failing device is reported in <see cref="Problem"/>, shown
/// on screen, and never throws into the window. NAudio is MIT-licensed and does not use the network.
/// </summary>
public sealed class NAudioPulseAudio : IPulseAudio
{
    private const int LatencyMs = 100;

    private readonly ClickTrain _train = new(44100);
    private WaveOutEvent? _out;
    private bool _enabled, _disposed;
    private double? _period;
    private float _volume = 0.5f;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value || _disposed) return;
            _enabled = value;
            Apply();
        }
    }

    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0, 1);
            _train.Volume = _volume;
        }
    }

    public string? Problem { get; private set; }

    public void SetPulsePeriod(double? receivedPeriodS)
    {
        if (_disposed) return;
        _period = receivedPeriodS;
        Apply();
    }

    private void Apply()
    {
        _train.SetPeriod(_enabled ? _period : null);
        if (!_enabled)
        {
            try { _out?.Pause(); } catch (Exception e) { Problem = Describe(e); }
            return;
        }
        if (_period is null) return;   // silent: the device, if open, plays zeros
        try
        {
            if (_out is null)
            {
                _train.Volume = _volume;
                var device = new WaveOutEvent { DesiredLatency = LatencyMs };
                device.Init(new ClickTrainProvider(_train).ToWaveProvider());
                _out = device;
            }
            if (_out.PlaybackState != PlaybackState.Playing) _out.Play();
            Problem = null;
        }
        catch (Exception e)
        {
            Problem = Describe(e);
            _out?.Dispose();
            _out = null;
        }
    }

    private static string Describe(Exception e) => e is NAudio.MmException ? "no audio output device" : $"audio error ({e.GetType().Name})";

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _train.SetPeriod(null);
        try { _out?.Stop(); } catch { /* closing anyway */ }
        _out?.Dispose();
        _out = null;
    }

    private sealed class ClickTrainProvider : ISampleProvider
    {
        private readonly ClickTrain _train;
        public ClickTrainProvider(ClickTrain train) => (_train, WaveFormat) = (train, WaveFormat.CreateIeeeFloatWaveFormat(train.SampleRate, 1));
        public WaveFormat WaveFormat { get; }
        public int Read(float[] buffer, int offset, int count) => _train.Fill(buffer, offset, count);
    }
}
