namespace SupernovaLab.Core.Signals;

/// <summary>The detection badge's states (spec §5.1).</summary>
public enum DetectionStatus { Detected, TooFaint, BlockedByDust, NotPointedAtUs, NotProduced, NotYet, NotModeled }

/// <summary>A detection verdict with a plain-language reason, e.g. "Super-Kamiokande expects about 8,000 events".</summary>
public sealed record Detection(DetectionStatus Status, string Reason);
