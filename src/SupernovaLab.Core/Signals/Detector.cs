using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Signals;

/// <summary>
/// A real instrument and its published sensitivity (signals §11). <see cref="Threshold"/> is in
/// <see cref="ThresholdUnit"/>: a count, a significance, a flux or a limiting magnitude. Models decide how the
/// threshold is compared (for a magnitude, brighter means smaller).
/// </summary>
public sealed record Detector(string Id, string Name, Messenger Messenger, double Threshold, string ThresholdUnit, Source Source);
