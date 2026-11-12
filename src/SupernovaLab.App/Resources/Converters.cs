using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Text;
using SupernovaLab.Core.Units;

namespace SupernovaLab.App.Resources;

/// <summary>Confidence → badge brush: Established green, ModelDependent amber, Speculative red (theme keys).</summary>
public sealed class ConfidenceBrushConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        Confidence.Established => Application.Current?.TryFindResource("EstablishedBrush"),
        Confidence.ModelDependent => Application.Current?.TryFindResource("ModelDependentBrush"),
        Confidence.Speculative => Application.Current?.TryFindResource("SpeculativeBrush"),
        _ => Brushes.Gray,
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Confidence → the badge's word.</summary>
public sealed class ConfidenceTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        Confidence.Established => "Established",
        Confidence.ModelDependent => "Model-dependent",
        Confidence.Speculative => "Speculative",
        _ => "",
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>A glossary key (from a view model) → its text.</summary>
public sealed class GlossaryConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string key && Glossary.All.TryGetValue(key, out var text) ? text : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Fate → its color on the outcome map (theme keys Fate&lt;name&gt;Brush).</summary>
public sealed class FateBrushConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Fate fate ? Application.Current?.TryFindResource($"Fate{fate}Brush") ?? Brushes.Gray : Brushes.Gray;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Visible for a non-null, non-empty value (string or list); Collapsed otherwise. "Invert" reverses it.</summary>
public sealed class VisibleWhenConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool present = value switch
        {
            null => false,
            bool b => b,
            string s => s.Length > 0,
            System.Collections.ICollection c => c.Count > 0,
            _ => true,
        };
        if (parameter as string == "Invert") present = !present;
        return present ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>
/// [fraction 0..1, width] → a Canvas offset: fraction × width, minus the optional parameter (half the item's width) so
/// the item is centered on the point.
/// </summary>
public sealed class FractionToOffsetConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not double f || values[1] is not double width) return 0.0;
        double centre = parameter is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double c) ? c : 0;
        return f * width - centre;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>
/// [fraction 0..1, width] → the Canvas offset of a label of the parameter's width centered on that point, held inside
/// 0..width so labels near the ends stay visible.
/// </summary>
public sealed class CenteredLabelOffsetConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not double f || values[1] is not double width) return 0.0;
        double label = parameter is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double w) ? w : 0;
        return Math.Max(0, Math.Min(f * width - label / 2, width - label));
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
