namespace SupernovaLab.App.ViewModels;

/// <summary>One option of a dropdown: the value it sets and its plain-language label.</summary>
public sealed record Choice<T>(T Value, string Label)
{
    public override string ToString() => Label;
}
