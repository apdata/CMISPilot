using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CMISPilot.Desktop.Converters;

/// <summary>
/// Wandelt einen <see cref="string"/> in eine <see cref="Visibility"/>: nicht-leer
/// (und nicht <c>null</c>) ⇒ <see cref="Visibility.Visible"/>, sonst
/// <see cref="Visibility.Collapsed"/>. Für optionale Fehlertexte (z. B. im
/// Verbinden-Dialog, R4 Etappe 2).
/// </summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    /// <summary>string ⇒ Visibility.</summary>
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Rückrichtung nicht benötigt.</summary>
    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
