using System.Reflection;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Timeline;

namespace SupernovaLab.Tests;

/// <summary>
/// The ISignalModel contract (spec §7; Review Focus 2 and 3), run over every model in the Core assembly found by
/// reflection, so later tasks' models are covered automatically.
/// </summary>
public class SignalContractTests
{
    private static IEnumerable<Type> ModelTypes() => typeof(ISignalModel).Assembly.GetTypes()
        .Where(t => typeof(ISignalModel).IsAssignableFrom(t) && !t.IsInterface)
        .OrderBy(t => t.FullName, StringComparer.Ordinal);

    private static ISignalModel Create(string typeName)
        => (ISignalModel)Activator.CreateInstance(ModelTypes().Single(t => t.FullName == typeName))!;

    private static readonly Dictionary<string, Outcome> Outcomes = new()
    {
        ["12 M☉"] = TestOutcomes.Star(12),
        ["23 M☉ failed"] = TestOutcomes.Star(23),
        ["Type Ia"] = TestOutcomes.TypeIa(),
        ["3 M☉ white dwarf"] = TestOutcomes.Star(3),
    };

    private static readonly Dictionary<string, ObserverSetup> Observers = new()
    {
        ["default"] = ObserverSetup.Default,
        ["10 pc"] = new ObserverSetup(10, 0, true),
        ["1 Gpc, A_V 30"] = new ObserverSetup(1e9, 30, true),
    };

    public static TheoryData<string, string, string> Cases()
    {
        var data = new TheoryData<string, string, string>();
        foreach (var t in ModelTypes())
        foreach (string o in Outcomes.Keys)
        foreach (string obs in Observers.Keys)
            data.Add(t.FullName!, o, obs);
        return data;
    }

    /// <summary>t = −1 s, 0 and twice the timeline's end, plus every 50th point of the panels' grid.</summary>
    private static IEnumerable<double> Times()
    {
        yield return -1;
        yield return 0;
        yield return TimeAxis.MaxS * 2;
        double[] grid = TimeAxis.LogGrid();
        for (int i = 0; i < grid.Length; i += 50) yield return grid[i];
    }

    /// <summary>
    /// Channels that may legitimately be negative: magnitudes ("mag") and dimensionless signed signals such as a GW strain
    /// (unit ""). Every other channel is a luminosity, count, flux, energy, temperature or similar and must be ≥ 0.
    /// </summary>
    private static bool MayBeNegative(SignalChannel c) => c.Unit is "mag" or "";

    [Fact]
    public void EveryModel_IsPublicSealedWithAParameterlessConstructor()
    {
        var types = ModelTypes().ToList();
        Assert.Contains(typeof(NeutrinoModel), types);
        foreach (var t in types)
        {
            Assert.True(t.IsPublic && t.IsSealed && !t.IsAbstract, $"{t.FullName} must be public sealed");
            Assert.NotNull(t.GetConstructor(BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes));
        }
    }

    [Fact]
    public void ModelIds_AreUnique_AndEveryModelDescribesItself()
    {
        var models = ModelTypes().Select(t => (ISignalModel)Activator.CreateInstance(t)!).ToList();
        Assert.Equal(models.Count, models.Select(m => m.Id).Distinct().Count());
        foreach (var m in models)
        {
            Assert.False(string.IsNullOrWhiteSpace(m.Id));
            Assert.NotEmpty(m.Channels);
            Assert.Equal(m.Channels.Count, m.Channels.Select(c => c.Id).Distinct().Count());
            Assert.NotEmpty(m.Sources);
            foreach (var s in m.Sources)
            {
                Assert.False(string.IsNullOrWhiteSpace(s.Citation), $"{m.Id} source citation");
                Assert.False(string.IsNullOrWhiteSpace(s.ResearchRef), $"{m.Id} source research ref");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Sample_IsNaNOrFinite_NeverNegative_AndNaNOutsideItsRange(string model, string outcome, string observer)
    {
        var m = Create(model);
        var o = Outcomes[outcome];
        var obs = Observers[observer];
        bool applies = m.Applies(o);
        var (start, end) = m.ValidRange(o);
        Assert.False(double.IsNaN(start) || double.IsNaN(end), "ValidRange must not be NaN");
        Assert.True(start <= end, $"ValidRange start {start} > end {end}");

        foreach (var c in m.Channels)
        foreach (double t in Times())
        {
            double v = m.Sample(o, obs, t, c.Id);
            string where = $"{m.Id}/{c.Id} at t = {t} s ({outcome}, {observer})";
            Assert.True(double.IsNaN(v) || double.IsFinite(v), $"{where} is infinite");
            if (!applies || t < start || t > end) Assert.True(double.IsNaN(v), $"{where} should be NaN, was {v}");
            if (double.IsFinite(v) && !MayBeNegative(c)) Assert.True(v >= 0, $"{where} is negative: {v}");
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Detect_NeverThrows_AndNotProducedWhenTheModelDoesNotApply(string model, string outcome, string observer)
    {
        var m = Create(model);
        var o = Outcomes[outcome];
        var obs = Observers[observer];
        bool applies = m.Applies(o);

        foreach (var d in Detectors.All)
        foreach (double t in Times())
        {
            var det = m.Detect(o, obs, t, d);
            Assert.NotNull(det);
            Assert.False(string.IsNullOrWhiteSpace(det.Reason), $"{m.Id}/{d.Id} at {t} s gave no reason");
            if (!applies) Assert.Equal(DetectionStatus.NotProduced, det.Status);
        }
    }
}
