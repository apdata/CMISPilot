using System.Globalization;
using System.Windows.Data;

namespace CMISPilot.Desktop.Converters;

/// <summary>Invertiert einen <see cref="bool"/>-Wert (für gegenläufige IsEnabled-Bindungen).</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    /// <summary>bool ⇒ invertierter bool.</summary>
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;

    /// <summary>Symmetrisch invertierbar.</summary>
    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;
}
