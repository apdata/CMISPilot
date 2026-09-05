using System.Globalization;
using System.Windows;
using System.Windows.Data;
using APX.Wpf.Shell.ViewModels.Logging;

namespace CMISPilot.Desktop.Converters;

/// <summary>
/// Bildet <see cref="LogLevelKind"/> auf ein Statussymbol ab (Ausgabe- und
/// Fehlerliste-Grid, Spalte "Ebene", Visual-Studio-Stil). Die Icons liegen als
/// <c>DrawingImage</c>-Ressourcen in <c>Resources/Icons/Icons.xaml</c>.
/// </summary>
public sealed class LogLevelToIconConverter : IValueConverter
{
    /// <summary>LogLevelKind ⇒ DrawingImage-Ressource.</summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value is LogLevelKind kind ? MapKey(kind) : "Icon.LevelInformation";
        return Application.Current?.TryFindResource(key);
    }

    /// <summary>Rückrichtung nicht benötigt.</summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;

    /// <summary>Reine Zuordnung, ohne WPF-Abhängigkeit testbar.</summary>
    internal static string MapKey(LogLevelKind level) => level switch
    {
        LogLevelKind.Error => "Icon.LevelError",
        LogLevelKind.Warning => "Icon.LevelWarning",
        LogLevelKind.Debug => "Icon.LevelDebug",
        _ => "Icon.LevelInformation"
    };
}
