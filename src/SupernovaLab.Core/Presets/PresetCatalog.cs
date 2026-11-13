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
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private static IReadOnlyList<Preset> Load()
    {
        var assembly = Assembly.GetExecutingAssembly();
        string name = assembly.GetManifestResourceNames().Single(n => n.EndsWith(ResourceSuffix, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name)!;
        return Parse(stream);
    }

    /// <summary>
    /// The presets in a presets.json stream (the seam the tests use). Ruling R47f: an unknown key (a misspelt pin would
    /// otherwise vanish silently), a value of the wrong kind or malformed JSON throws <see cref="InvalidDataException"/>,
    /// naming the preset where it can.
    /// </summary>
    internal static IReadOnlyList<Preset> Parse(Stream stream)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(stream,
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"presets.json is not valid JSON: {e.Message}", e);
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("presets.json must be an object with a \"presets\" list.");
            JsonElement list = default;
            foreach (var property in root.EnumerateObject())
            {
                if (!property.NameEquals("presets"))
                    throw new InvalidDataException($"presets.json has an unknown key '{property.Name}'.");
                list = property.Value;
            }
            if (list.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("presets.json has no \"presets\" list.");

            var presets = new List<Preset>();
            int index = 0;
            foreach (var element in list.EnumerateArray())
            {
                index++;
                string label = element.ValueKind == JsonValueKind.Object && element.TryGetProperty("id", out var id) &&
                               id.ValueKind == JsonValueKind.String
                    ? id.GetString()!
                    : $"#{index}";
                PresetDto dto;
                try
                {
                    dto = element.Deserialize<PresetDto>(Options)
                          ?? throw new InvalidDataException($"Preset '{label}' in presets.json is empty.");
                }
                catch (JsonException e)
                {
                    throw new InvalidDataException($"Preset '{label}' in presets.json: {e.Message}", e);
                }
                presets.Add(ToPreset(dto));
            }
            return Validated(presets);
        }
    }

    private static IReadOnlyList<Preset> Validated(List<Preset> presets)
    {
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
                p.EquatorialRing, p.NsGravitationalMass, p.RemnantMass, p.ConfinedCsmOuterCm, p.UndetectedOuterShell,
                p.SceneDisclosures);

        var facts = (d.Facts ?? new List<FactDto>()).Select(f => new ObservedFact(
            f.Messenger ?? throw new InvalidDataException($"A fact of preset '{id}' has no messenger."),
            Required(f.ChannelId, "fact channelId", id), f.SourceTimeS,
            f.Value ?? throw new InvalidDataException($"A fact of preset '{id}' has no value."),
            Required(f.Unit, "fact unit", id), Required(f.Description, "fact description", id),
            ToSource(f.Source, id, "fact"), f.IsUpperLimit ?? false, SpanEnd(f, id))).ToList();

        return new Preset(id, Required(d.Name, "name", id), Required(d.Summary, "summary", id),
            Required(d.WhatWasSeen, "whatWasSeen", id), inputs, observer, pins, facts, ToSource(o.Source, id, "observer"))
        {
            TourLines = (d.TourLines ?? new List<TourLineDto>()).Select(l => new TourLine(
                l.SourceTimeS is double t && double.IsFinite(t) && t > 0
                    ? t
                    : throw new InvalidDataException($"A tour line of preset '{id}' needs a positive sourceTimeS."),
                Required(l.Text, "tour line text", id), ToSource(l.Source, id, "tour line"), l.Highlight,
                l.Recalled ?? false, l.FromBreakout ?? false)).ToList(),
        };
    }

    private static string Required(string? value, string field, string id)
        => string.IsNullOrWhiteSpace(value) ? throw new InvalidDataException($"Preset '{id}' has no {field}.") : value;

    private static Source ToSource(SourceDto? s, string id, string what)
        => new(Required(s?.Citation, $"{what} citation", id), Required(s?.ResearchRef, $"{what} research reference", id));

    // ---------------------------------------------------------------- the file's shape

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
        public List<TourLineDto>? TourLines { get; set; }
    }

    private sealed class TourLineDto
    {
        public double? SourceTimeS { get; set; }
        public string? Text { get; set; }
        public SourceDto? Source { get; set; }
        public Messenger? Highlight { get; set; }
        public bool? Recalled { get; set; }
        public bool? FromBreakout { get; set; }
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
        public double? NsGravitationalMass { get; set; }
        public double? RemnantMass { get; set; }
        public double? ConfinedCsmOuterCm { get; set; }
        public bool? UndetectedOuterShell { get; set; }
        public int[]? SceneDisclosures { get; set; }
        public SourceDto? Source { get; set; }
    }

    /// <summary>A fact's span end: after its start, and only with a start.</summary>
    private static double? SpanEnd(FactDto f, string id)
    {
        if (f.SourceTimeEndS is not double end) return null;
        if (f.SourceTimeS is not double start || !(end > start))
            throw new InvalidDataException($"A fact of preset '{id}' has a sourceTimeEndS without an earlier sourceTimeS.");
        return end;
    }

    private sealed class FactDto
    {
        public Messenger? Messenger { get; set; }
        public string? ChannelId { get; set; }
        public double? SourceTimeS { get; set; }
        public double? SourceTimeEndS { get; set; }
        public bool? IsUpperLimit { get; set; }
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
