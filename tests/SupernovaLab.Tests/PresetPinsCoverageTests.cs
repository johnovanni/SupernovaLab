using System.Reflection;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Tests;

/// <summary>
/// Task 9's carry-over: a new <see cref="PresetPins"/> property must either count in <see cref="PresetPins.ChangesModelledValue"/> (so the
/// outcome keeps its "Published" label) or be named on the exempt list below, with the reason, so one cannot silently lose it.
/// </summary>
public class PresetPinsCoverageTests
{
    /// <summary>
    /// Not a pinned value: the source citation; the geometry-only pin the lab draws but no model reads (rulings R-P2-27, R-P2-28); and the
    /// note-only flag for a predicted-but-undetected outer shell (ruling R-P2-33).
    /// </summary>
    private static readonly string[] Exempt =
    {
        nameof(PresetPins.ConfinedCsmOuterCm), nameof(PresetPins.UndetectedOuterShell), nameof(PresetPins.SceneDisclosures),
        nameof(PresetPins.Source),
    };

    private static PresetPins WithOnly(ParameterInfo[] parameters, int index)
    {
        var args = new object?[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
            args[i] = parameters[i].ParameterType == typeof(Source) ? new Source("test", "test") : null;
        var under = Nullable.GetUnderlyingType(parameters[index].ParameterType) ?? parameters[index].ParameterType;
        if (under == typeof(double)) args[index] = 1.0;
        else if (under == typeof(bool)) args[index] = true;
        else if (under == typeof(int[])) args[index] = new[] { 46 };
        else if (under != typeof(Source)) throw new NotSupportedException($"{parameters[index].Name}: {under}");
        return (PresetPins)Activator.CreateInstance(typeof(PresetPins), args)!;
    }

    [Fact]
    public void EveryPin_ChangesAModelledValue_OrIsExempt()
    {
        var parameters = typeof(PresetPins).GetConstructors().Single().GetParameters();
        var properties = typeof(PresetPins).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name is not ("EqualityContract" or nameof(PresetPins.ChangesModelledValue))).Select(p => p.Name).ToList();
        // The record is purely positional, so the constructor's parameters are all of its pins.
        Assert.Equal(properties.Order(), parameters.Select(p => p.Name!).Order());
        var unlisted = new List<string>();
        for (int i = 0; i < parameters.Length; i++)
        {
            string name = parameters[i].Name!;
            if (Exempt.Contains(name)) continue;
            if (!WithOnly(parameters, i).ChangesModelledValue) unlisted.Add(name);
        }
        Assert.True(unlisted.Count == 0,
            "These pins do not set ChangesModelledValue (so their outcome would lose its Published label): add them to " +
            $"PresetPins.ChangesModelledValue, or to this test's exempt list with a reason: {string.Join(", ", unlisted)}");
    }

    [Fact]
    public void TheExemptPins_ReallyDoNotChangeAModelledValue()
    {
        var parameters = typeof(PresetPins).GetConstructors().Single().GetParameters();
        foreach (string name in Exempt.Where(n => n != nameof(PresetPins.Source)))
            Assert.False(WithOnly(parameters, Array.FindIndex(parameters, p => p.Name == name)).ChangesModelledValue, name);
    }
}
