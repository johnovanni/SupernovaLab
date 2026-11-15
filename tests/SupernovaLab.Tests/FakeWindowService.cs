using SupernovaLab.App.Services;
using SupernovaLab.Core.Signals;

namespace SupernovaLab.Tests;

/// <summary>Records the window requests a view model makes (spec §7: view models never create windows).</summary>
public sealed class FakeWindowService : IWindowService
{
    public List<Messenger?> InstrumentsCalls { get; } = new();
    public int OutcomeDetailsCalls { get; private set; }
    public int PresetInfoCalls { get; private set; }
    public int CloseUpCalls { get; private set; }

    public void ShowInstruments(Messenger? focus = null) => InstrumentsCalls.Add(focus);
    public void ShowOutcomeDetails() => OutcomeDetailsCalls++;
    public void ShowPresetInfo() => PresetInfoCalls++;
    public void ShowCloseUp() => CloseUpCalls++;
}
