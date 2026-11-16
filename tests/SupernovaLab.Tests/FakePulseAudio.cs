using SupernovaLab.App.Services;

namespace SupernovaLab.Tests;

/// <summary>Records what the Close-up asks of the pulse sound; plays nothing (plan Task 8, ruling P3-10).</summary>
public sealed class FakePulseAudio : IPulseAudio
{
    public bool Enabled { get; set; }
    public float Volume { get; set; } = 1;
    public string? Problem { get; set; }

    /// <summary>The last period asked for (null: silence).</summary>
    public double? LastPeriod { get; private set; }

    /// <summary>Every <see cref="SetPulsePeriod"/> call, in order.</summary>
    public List<double?> History { get; } = new();

    public bool Disposed { get; private set; }

    public void SetPulsePeriod(double? receivedPeriodS)
    {
        LastPeriod = receivedPeriodS;
        History.Add(receivedPeriodS);
    }

    /// <summary>How many test tones were asked for (Task 16).</summary>
    public int TestTones { get; private set; }

    public void PlayTestTone()
    {
        if (Enabled) TestTones++;
    }

    public void Dispose() => Disposed = true;
}
