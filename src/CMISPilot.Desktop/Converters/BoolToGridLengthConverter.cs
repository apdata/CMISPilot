using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CMISPilot.Desktop.Converters;

/// <summary>
/// Wandelt ein <see cref="bool"/> (Pane sichtbar) in eine <see cref="GridLength"/>:
/// <c>true</c> ⇒ die im <c>ConverterParameter</c> angegebene Pixelbreite/-höhe,
/// <c>false</c> ⇒ <c>0</c>. So kollabiert ein Grid-Bereich vollständig, wenn das
/// zugehörige Werkzeugfenster ausgeblendet wird (R1, Grid+Splitter-Layout).
/// </summary>
public sealed class BoolToGridLengthConverter : IValueConverter
{
    /// <summary>Fallback-Größe, falls kein gültiger Parameter übergeben wird.</summary>
    private const double DefaultSize = 240;

    /// <summary>bool + Zielgröße (Parameter) ⇒ GridLength.</summary>
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        var visible = value is true;
        if (!visible)
        {
            return new GridLength(0);
        }

        var size = DefaultSize;
        if (parameter is string s &&
            double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            size = parsed;
        }

        return new GridLength(size, GridUnitType.Pixel);
    }

    /// <summary>Rückrichtung nicht benötigt (Einwege-Bindung).</summary>
    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
