using System.Globalization;
using System.Windows.Data;
using CMISPilot.Cmis.Models;

namespace CMISPilot.Desktop.Converters;

/// <summary>
/// Wandelt einen <see cref="CmisAuthenticationType"/> in einen lesbaren deutschen
/// Text fuer die Anzeige in der Auth-Auswahl (Verbinden-Dialog/Profil, R6). Rueck-
/// richtung nicht benoetigt (die ComboBox bindet SelectedItem direkt an den Enum-Wert).
/// </summary>
public sealed class AuthenticationTypeLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is CmisAuthenticationType type
            ? type switch
            {
                CmisAuthenticationType.None => "Keine",
                CmisAuthenticationType.Standard => "Standard (Benutzer/Passwort)",
                CmisAuthenticationType.OAuthBearer => "OAuth 2.0 (Bearer Token)",
                _ => value.ToString() ?? string.Empty
            }
            : string.Empty;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
