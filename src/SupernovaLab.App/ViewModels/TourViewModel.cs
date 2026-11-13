using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SupernovaLab.App.Controllers;
using SupernovaLab.App.Tour;

namespace SupernovaLab.App.ViewModels;

/// <summary>
/// The guided tour's face (Project 2, Task 13): the Tour button, and the caption panel over the 3D view with its pause,
/// previous, next, exit and "Resume tour" controls. <see cref="SourceText"/> is the caption's tooltip, so every sentence has its
/// source and confidence on hover (spec criterion 4).
/// </summary>
public sealed class TourViewModel : ObservableObject
{
    public TourViewModel(TourDirector director)
    {
        Director = director ?? throw new ArgumentNullException(nameof(director));
        TourCommand = new RelayCommand(Director.Start, () => !IsActive);
        PauseCommand = new RelayCommand(Director.Pause, () => IsActive && !ShowResume);
        ResumeCommand = new RelayCommand(Director.Resume, () => ShowResume);
        NextCommand = new RelayCommand(Director.Next, () => IsActive);
        PreviousCommand = new RelayCommand(Director.Previous, () => IsActive);
        ExitCommand = new RelayCommand(Director.Exit, () => IsActive);
        Director.Changed += (_, _) => Refresh();
    }

    public TourDirector Director { get; }

    public IRelayCommand TourCommand { get; }
    public IRelayCommand PauseCommand { get; }
    public IRelayCommand ResumeCommand { get; }
    public IRelayCommand NextCommand { get; }
    public IRelayCommand PreviousCommand { get; }
    public IRelayCommand ExitCommand { get; }

    /// <summary>Attaches the 3D view's controller once the window has loaded (null when it closes).</summary>
    public void AttachScene(SceneController? scene) => Director.Scene = scene;

    public bool IsActive => Director.State != TourState.Off;

    /// <summary>True while the user's input has paused the tour (or Pause was pressed): "Resume tour" is shown.</summary>
    public bool ShowResume => Director.State == TourState.Paused;

    /// <summary>The current stop's caption; empty when the tour is off.</summary>
    public string Caption => Director.Current?.Caption ?? "";

    /// <summary>The caption's tooltip: the source and confidence of each of its sentences.</summary>
    public string SourceText => Director.Current?.SourceText ?? "";

    public bool HasSources => SourceText.Length > 0;

    /// <summary>"Stop 3 of 9"; empty when the tour is off.</summary>
    public string CounterText => Director.Current is null ? "" : $"Stop {Director.StopIndex + 1} of {Director.Stops.Count}";

    /// <summary>"Tour ended: new explosion" after a new outcome ended the tour, else null.</summary>
    public string? Note => Director.Note;

    private void Refresh()
    {
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(ShowResume));
        OnPropertyChanged(nameof(Caption));
        OnPropertyChanged(nameof(SourceText));
        OnPropertyChanged(nameof(HasSources));
        OnPropertyChanged(nameof(CounterText));
        OnPropertyChanged(nameof(Note));
        foreach (var c in new[] { TourCommand, PauseCommand, ResumeCommand, NextCommand, PreviousCommand, ExitCommand })
            c.NotifyCanExecuteChanged();
    }
}
