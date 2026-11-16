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

    /// <summary>
    /// Task 16b: the silence written first each time the device opens, s. A Bluetooth speaker takes a fraction of a second to connect and
    /// drops what comes first (the owner's experience with other apps); the device then stays open, playing silence, while the sound is on.
    /// </summary>
    public const double LeadInS = 0.25;

    private readonly ClickTrain _train = new(44100);
    private readonly Func<IWavePlayer> _deviceFactory;
    private IWavePlayer? _out;
    private bool _enabled, _disposed;
    private volatile bool _failed;          // set on any thread (PlaybackStopped); cleared when Enabled is toggled
    private volatile string? _problem;
    private double? _period;
    private float _volume = ClickTrain.DefaultVolume;

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

    /// <summary>Task 15's diagnostic: how many times an output device was opened and started.</summary>
    public int DevicesOpened { get; private set; }

    private string _deviceName = "";

    /// <summary>Whether a device is open now.</summary>
    public bool IsDeviceOpen => _out is not null;

    /// <summary>How many clicks the device has pulled from the click train (written into its buffers, so queued for the speaker).</summary>
    public long ClicksQueued => _train.ClicksWritten;

    /// <summary>How many samples the device has pulled (44,100 a second while it plays, silent ones included).</summary>
    public long SamplesQueued => _train.SamplesWritten;

    /// <summary>
    /// Task 15: the sound's state in one line for the --snapshot log, so a launch confirms the sound by log, never by ear: whether the device
    /// opened, and how many clicks and samples it has queued.
    /// </summary>
    public string DiagnosticText
    {
        get
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            string device = DevicesOpened == 0 ? "audio device: never opened"
                : $"audio device opened {DevicesOpened}× ({_deviceName}, {_train.SampleRate} Hz mono IEEE float, {LatencyMs} ms latency), now {(IsDeviceOpen ? "open" : "released")}";
            return $"{device}; {ClicksQueued} clicks queued in {SamplesQueued} samples ({(SamplesQueued / (double)_train.SampleRate).ToString("0.00", inv)} s); " +
                   $"test tones {TestTonesPlayed}; volume {(int)Math.Round(_volume * 100)}%; problem: {_problem ?? "none"}";
        }
    }

    /// <summary>How many test tones have been started (Task 16; the launch log's "test tones").</summary>
    public int TestTonesPlayed { get; private set; }

    public void PlayTestTone()
    {
        if (_disposed || !_enabled || _failed) return;
        _train.StartTestTone();
        TestTonesPlayed++;
        Apply(openForTone: true);
    }

    public void SetPulsePeriod(double? receivedPeriodS)
    {
        if (_disposed) return;
        _period = receivedPeriodS;
        Apply();
    }

    private void Apply(bool openForTone = false)
    {
        _train.SetPeriod(_enabled && !_failed ? _period : null);
        if (!_enabled || _failed)
        {
            _train.StopTestTone();       // Task 16: a tone cut off by switching off does not finish on switching on again
            ReleaseDevice();             // stop and release: nothing queued is left to sound later
            return;
        }
        if ((_period is null && !openForTone) || _out is not null) return;   // silent: an open device plays zeros
        IWavePlayer? device = null;
        try
        {
            _train.Volume = _volume;
            device = _deviceFactory();
            device.PlaybackStopped += OnPlaybackStopped;
            _train.HoldSilence(LeadInS);
            device.Init(new ClickTrainProvider(_train).ToWaveProvider());
            device.Play();
            _out = device;
            DevicesOpened++;
            _deviceName = device.GetType().Name;
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
