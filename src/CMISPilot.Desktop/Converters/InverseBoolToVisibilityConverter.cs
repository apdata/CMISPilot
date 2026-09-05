using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CMISPilot.Desktop.Converters;

/// <summary>
/// Wandelt ein <see cref="bool"/> invertiert in eine <see cref="Visibility"/>:
/// <c>false</c> ⇒ <see cref="Visibility.Visible"/>, <c>true</c> ⇒
/// <see cref="Visibility.Collapsed"/>. Nützlich für Leerzustands-Hinweise
/// (sichtbar, solange keine Daten vorhanden sind).
/// </summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    /// <summary>bool ⇒ invertierte Visibility.</summary>
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Rückrichtung nicht benötigt.</summary>
    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
