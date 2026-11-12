using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Presets;

/// <summary>
/// The real-supernova presets (spec §2.3), read from the embedded <c>Data/Embedded/presets.json</c>, whose values come
/// from docs/research/presets.md as checked in docs/research/presets-verification.md. Inputs the file leaves out keep
/// <see cref="LabInputs.Default"/>; a missing beam flag means the beam points at Earth. The list is parsed once.
/// </summary>
public static class PresetCatalog
{
    private const string ResourceSuffix = "Data.Embedded.presets.json";

    private static readonly Lazy<IReadOnlyList<Preset>> All = new(Load);

    /// <summary>Every preset, in the file's order. Throws <see cref="InvalidDataException"/> for a malformed file.</summary>
    public static IReadOnlyList<Preset> LoadEmbedded() => All.Value;

    /// <summary>The preset with this Id. Throws <see cref="KeyNotFoundException"/> for an unknown Id.</summary>
    public static Preset Get(string id)
        => LoadEmbedded().FirstOrDefault(p => p.Id == id) ?? throw new KeyNotFoundException($"No preset with Id '{id}'.");

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static IReadOnlyList<Preset> Load()
    {
        var assembly = Assembly.GetExecutingAssembly();
        string name = assembly.GetManifestResourceNames().Single(n => n.EndsWith(ResourceSuffix, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name)!;
        var file = JsonSerializer.Deserialize<FileDto>(stream, Options)
                   ?? throw new InvalidDataException("presets.json is empty.");
        var presets = (file.Presets ?? throw new InvalidDataException("presets.json has no \"presets\" list."))
            .Select(ToPreset).ToList();
        var duplicate = presets.GroupBy(p => p.Id).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null) throw new InvalidDataException($"presets.json lists '{duplicate.Key}' twice.");
        return presets;
    }

    private static Preset ToPreset(PresetDto d)
    {
        string id = Required(d.Id, "id", "?");
        var i = d.Inputs ?? new InputsDto();
        var def = LabInputs.Default;
        var inputs = new LabInputs(i.Kind ?? def.Kind, i.InitialMass ?? def.InitialMass, i.Metallicity ?? def.Metallicity,
            i.Stripping ?? def.Stripping, i.Rotation ?? def.Rotation, i.Field ?? def.Field, i.IaChannel ?? def.IaChannel,
            i.WhiteDwarfMass ?? def.WhiteDwarfMass, i.Mixing ?? def.Mixing, i.Yield ?? def.Yield, i.DenseCsm ?? def.DenseCsm,
            i.Opacity ?? def.Opacity);
        var o = d.Observer ?? throw new InvalidDataException($"Preset '{id}' has no observer.");
        if (o.DistancePc is not (double distance and > 0))
            throw new InvalidDataException($"Preset '{id}' needs a positive observer distance.");
        var observer = new ObserverSetup(distance, o.Av ?? 0, o.BeamTowardEarth ?? true);

        PresetPins? pins = null;
        if (d.Pins is { } p)
            pins = new PresetPins(p.ExplosionEnergyErg, p.EjectaMass, p.NickelMass, p.RadiusCm, ToSource(p.Source, id, "pins"),
                p.Ti44Mass, p.GrbEisoErg, p.GrbEpKeV, p.GrbT90S, p.NsSpinPeriodS, p.NsFieldG, p.PulsarRadioLumMjyKpc2,
                p.EquatorialRing);

        var facts = (d.Facts ?? new List<FactDto>()).Select(f => new ObservedFact(
            f.Messenger ?? throw new InvalidDataException($"A fact of preset '{id}' has no messenger."),
            Required(f.ChannelId, "fact channelId", id), f.SourceTimeS,
            f.Value ?? throw new InvalidDataException($"A fact of preset '{id}' has no value."),
            Required(f.Unit, "fact unit", id), Required(f.Description, "fact description", id),
            ToSource(f.Source, id, "fact"))).ToList();

        return new Preset(id, Required(d.Name, "name", id), Required(d.Summary, "summary", id),
            Required(d.WhatWasSeen, "whatWasSeen", id), inputs, observer, pins, facts, ToSource(o.Source, id, "observer"));
    }

    private static string Required(string? value, string field, string id)
        => string.IsNullOrWhiteSpace(value) ? throw new InvalidDataException($"Preset '{id}' has no {field}.") : value;

    private static Source ToSource(SourceDto? s, string id, string what)
        => new(Required(s?.Citation, $"{what} citation", id), Required(s?.ResearchRef, $"{what} research reference", id));

    // ---------------------------------------------------------------- the file's shape

    private sealed class FileDto
    {
        public List<PresetDto>? Presets { get; set; }
    }

    private sealed class PresetDto
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Summary { get; set; }
        public string? WhatWasSeen { get; set; }
        public InputsDto? Inputs { get; set; }
        public ObserverDto? Observer { get; set; }
        public PinsDto? Pins { get; set; }
        public List<FactDto>? Facts { get; set; }
    }

    private sealed class InputsDto
    {
        public StarKind? Kind { get; set; }
        public double? InitialMass { get; set; }
        public double? Metallicity { get; set; }
        public Stripping? Stripping { get; set; }
        public Rotation? Rotation { get; set; }
        public CoreField? Field { get; set; }
        public TypeIaChannel? IaChannel { get; set; }
        public double? WhiteDwarfMass { get; set; }
        public NickelMixing? Mixing { get; set; }
        public NickelYield? Yield { get; set; }
        public bool? DenseCsm { get; set; }
        public double? Opacity { get; set; }
    }

    private sealed class ObserverDto
    {
        public double? DistancePc { get; set; }
        public double? Av { get; set; }
        public bool? BeamTowardEarth { get; set; }
        public SourceDto? Source { get; set; }
    }

    private sealed class PinsDto
    {
        public double? ExplosionEnergyErg { get; set; }
        public double? EjectaMass { get; set; }
        public double? NickelMass { get; set; }
        public double? RadiusCm { get; set; }
        public double? Ti44Mass { get; set; }
        public double? GrbEisoErg { get; set; }
        public double? GrbEpKeV { get; set; }
        public double? GrbT90S { get; set; }
        public double? NsSpinPeriodS { get; set; }
        public double? NsFieldG { get; set; }
        public double? PulsarRadioLumMjyKpc2 { get; set; }
        public bool? EquatorialRing { get; set; }
        public SourceDto? Source { get; set; }
    }

    private sealed class FactDto
    {
        public Messenger? Messenger { get; set; }
        public string? ChannelId { get; set; }
        public double? SourceTimeS { get; set; }
        public double? Value { get; set; }
        public string? Unit { get; set; }
        public string? Description { get; set; }
        public SourceDto? Source { get; set; }
    }

    private sealed class SourceDto
    {
        public string? Citation { get; set; }
        public string? ResearchRef { get; set; }
    }
}
