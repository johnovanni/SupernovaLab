using SupernovaLab.Core.Signals;

namespace SupernovaLab.App.Services;

/// <summary>
/// Opens the non-modal windows (spec §5.0). Each opens at most once; asking again brings it to the front. View models call
/// this and never create windows (spec §7); tests substitute a fake that records the calls.
/// </summary>
public interface IWindowService
{
    /// <summary>The Instruments window; <paramref name="focus"/> names the panel to maximize (Task 15 handles focus).</summary>
    void ShowInstruments(Messenger? focus = null);

    void ShowOutcomeDetails();

    void ShowPresetInfo();

    /// <summary>The Close-up window (spec §2.2): opened at most once; asking again brings it to the front.</summary>
    void ShowCloseUp();
}
