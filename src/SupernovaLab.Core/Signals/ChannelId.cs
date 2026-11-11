namespace SupernovaLab.Core.Signals;

/// <summary>
/// A channel Id split into its base and an optional detector suffix: "rate@superk" is ("rate", "superk"), "lum_nue" is
/// ("lum_nue", null). Every model with detector-dependent channels parses its Ids with this one helper.
/// </summary>
public readonly record struct ChannelId(string Base, string? DetectorId)
{
    public const char Separator = '@';

    /// <summary>Splits at the first '@'. Throws <see cref="ArgumentException"/> when either side of the '@' is empty.</summary>
    public static ChannelId Parse(string channelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);
        int at = channelId.IndexOf(Separator);
        if (at < 0) return new ChannelId(channelId, null);
        if (at == 0 || at == channelId.Length - 1)
            throw new ArgumentException($"Malformed channel Id '{channelId}': expected 'base{Separator}detector'.", nameof(channelId));
        return new ChannelId(channelId[..at], channelId[(at + 1)..]);
    }

    /// <summary>"base@detector".</summary>
    public static string Compose(string baseId, string detectorId) => $"{baseId}{Separator}{detectorId}";

    public override string ToString() => DetectorId is null ? Base : Compose(Base, DetectorId);
}
