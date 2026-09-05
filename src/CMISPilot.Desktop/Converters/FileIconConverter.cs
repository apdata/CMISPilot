using System.Globalization;
using System.Windows;
using System.Windows.Data;
using CMISPilot.Cmis.Models;
using CMISPilot.Desktop.Services;

namespace CMISPilot.Desktop.Converters;

/// <summary>
/// Symbol für eine Zeile der Explorer-Objektliste: Ordner bekommen weiterhin das feste
/// <c>Icon.FolderClosed</c>; für Dokumente wird zuerst das von Windows der Dateiendung
/// zugeordnete Symbol versucht (<see cref="ShellIconCache"/>, praktisch wie der
/// Windows-Explorer selbst – <c>.pdf</c> zeigt dann z. B. das Adobe-Logo, <c>.xlsm</c>
/// das von Excel). Liefert Windows dafür nichts (keine Endung, keine registrierte
/// Anwendung), bleibt es beim bisherigen, festen <c>Icon.Document</c>.
/// </summary>
public sealed class FileIconConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not CmisObjectDto obj)
        {
            return Application.Current.TryFindResource("Icon.Document");
        }

        if (obj.IsFolder)
        {
            return Application.Current.TryFindResource("Icon.FolderClosed");
        }

        var fileName = obj.ContentStreamFileName ?? obj.Name;
        return ShellIconCache.GetIconForFileName(fileName)
            ?? Application.Current.TryFindResource("Icon.Document");
    }

    /// <summary>Rückrichtung nicht benötigt.</summary>
    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
