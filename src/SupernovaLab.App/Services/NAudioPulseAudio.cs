using NAudio.Wave;

namespace SupernovaLab.App.Services;

/// <summary>
/// The pulsar sound on Windows' audio output (plan ruling P3-10): NAudio's <see cref="WaveOutEvent"/> (winmm) playing a generated
/// 44.1 kHz mono <see cref="ClickTrain"/>. The device is opened only when the sound is on and a period is first asked for, so a
/// window that never sounds never touches the audio system. A missing or failing device (at opening, or lost mid-play, such as an
/// unplugged headset) is reported in <see cref="Problem"/>, shown on screen, never thrown into the window, and not retried at the
/// timeline's tick rate: the failure is latched until the sound is switched off and on again (Task 8 fix round 1). Switching off stops
/// and releases the device, so no queued sound is left to play on re-enabling. NAudio is MIT-licensed and does not use the network.
/// </summary>
public sealed class NAudioPulseAudio : IPulseAudio
{
    private const int LatencyMs = 100;

    private readonly ClickTrain _train = new(44100);
    private readonly Func<IWavePlayer> _deviceFactory;
    private IWavePlayer? _out;
    private bool _enabled, _disposed;
    private volatile bool _failed;          // set on any thread (PlaybackStopped); cleared when Enabled is toggled
    private volatile string? _problem;
    private double? _period;
    private float _volume = 0.5f;

    /// <param name="deviceFactory">Makes the output device (null: a <see cref="WaveOutEvent"/>); tests pass a fake.</param>
    public NAudioPulseAudio(Func<IWavePlayer>? deviceFactory = null)
        => _deviceFactory = deviceFactory ?? (() => new WaveOutEvent { DesiredLatency = LatencyMs });

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value || _disposed) return;
            _enabled = value;
            _failed = false;            // a toggle is the retry
            _problem = null;
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

    public string? Problem => _problem;

    public void SetPulsePeriod(double? receivedPeriodS)
    {
        if (_disposed) return;
        _period = receivedPeriodS;
        Apply();
    }

    private void Apply()
    {
        _train.SetPeriod(_enabled && !_failed ? _period : null);
        if (!_enabled || _failed)
        {
            ReleaseDevice();             // stop and release: nothing queued is left to sound later
            return;
        }
        if (_period is null || _out is not null) return;   // silent: an open device plays zeros
        IWavePlayer? device = null;
        try
        {
            _train.Volume = _volume;
            device = _deviceFactory();
            device.PlaybackStopped += OnPlaybackStopped;
            device.Init(new ClickTrainProvider(_train).ToWaveProvider());
            device.Play();
            _out = device;
            _problem = null;
        }
        catch (Exception e)
        {
            _failed = true;
            _problem = Describe(e);
            if (device is not null)
            {
                device.PlaybackStopped -= OnPlaybackStopped;
                try { device.Dispose(); } catch { /* already failing */ }
            }
        }
    }

    /// <summary>The device stopped (on its own thread): with an error, such as an unplugged device, the problem is shown and the device is released on the next call.</summary>
    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (_disposed || e.Exception is null) return;
        _problem = Describe(e.Exception);
        _failed = true;
    }

    private void ReleaseDevice()
    {
        var device = _out;
        _out = null;
        if (device is null) return;
        device.PlaybackStopped -= OnPlaybackStopped;
        try { device.Stop(); } catch { /* releasing anyway */ }
        try { device.Dispose(); } catch { /* releasing anyway */ }
    }

    private static string Describe(Exception e) => e is NAudio.MmException ? "no audio output device" : $"audio error ({e.GetType().Name})";

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _train.SetPeriod(null);
        ReleaseDevice();
    }

    private sealed class ClickTrainProvider : ISampleProvider
    {
        private readonly ClickTrain _train;
        public ClickTrainProvider(ClickTrain train) => (_train, WaveFormat) = (train, WaveFormat.CreateIeeeFloatWaveFormat(train.SampleRate, 1));
        public WaveFormat WaveFormat { get; }
        public int Read(float[] buffer, int offset, int count) => _train.Fill(buffer, offset, count);
    }
}
