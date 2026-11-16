namespace SupernovaLab.App.Services;

/// <summary>
/// The Close-up's optional pulsar sound (spec §4.4, plan ruling P3-10): one short click per pulse at the received period. The view
/// model asks for a period while pulses are received (Task 15: paused or playing; it was only while the timeline played), and for null
/// (silence) otherwise. Nothing on screen depends on it: the rhythm and every reason for silence are shown in words. Tests use a fake.
/// </summary>
public interface IPulseAudio : IDisposable
{
    /// <summary>Whether the sound is switched on (Close-up menu ▸ Sound). Off by default, so nothing sounds unasked.</summary>
    bool Enabled { get; set; }

    /// <summary>The loudness, 0 to 1.</summary>
    float Volume { get; set; }

    /// <summary>Why the sound cannot play (for example no audio device); null when nothing is wrong. Shown on screen.</summary>
    string? Problem { get; }

    /// <summary>The received pulse period, s (P(t)(1 + z)); null for silence.</summary>
    void SetPulsePeriod(double? receivedPeriodS);

    /// <summary>
    /// Task 16: plays a short test tone (while switched on), so switching the sound on or moving its volume is heard even when no pulses
    /// reach Earth. Asked again while it plays, it runs on.
    /// </summary>
    void PlayTestTone();
}
