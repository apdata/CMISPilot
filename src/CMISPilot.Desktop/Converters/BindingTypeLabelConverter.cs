using System.Globalization;
using System.Windows.Data;
using CMISPilot.Cmis.Models;

namespace CMISPilot.Desktop.Converters;

/// <summary>
/// Wandelt einen <see cref="CmisBindingType"/> in einen lesbaren deutschen Text fuer
/// die Binding-Auswahl im Verbinden-Dialog (A3). Rueckrichtung nicht benoetigt (die
/// ComboBox bindet SelectedItem direkt an den Enum-Wert).
/// </summary>
public sealed class BindingTypeLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is CmisBindingType type
            ? type switch
            {
                CmisBindingType.Browser => "Browser Binding",
                CmisBindingType.AtomPub => "AtomPub",
                _ => value.ToString() ?? string.Empty
            }
            : string.Empty;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
