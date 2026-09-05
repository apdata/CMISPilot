using System.Globalization;
using System.Windows;
using System.Windows.Data;
using CMISPilot.ViewModels.Explorer;

namespace CMISPilot.Desktop.Converters;

/// <summary>
/// Bildet <see cref="TreeNodeKind"/> auf das passende Baum-Icon ab (R4 Etappe 2,
/// Konzept §3.3). Die Icons liegen als <c>DrawingImage</c>-Ressourcen in
/// <c>Resources/Icons/Icons.xaml</c> (in <see cref="Application.Resources"/> eingehängt).
/// </summary>
public sealed class TreeNodeKindToIconConverter : IValueConverter
{
    /// <summary>TreeNodeKind ⇒ DrawingImage-Ressource.</summary>
    public object? Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            TreeNodeKind.Server => "Icon.Server",
            TreeNodeKind.Repository => "Icon.Database",
            TreeNodeKind.Folder => "Icon.FolderClosed",
            _ => "Icon.Document"
        };

        return Application.Current.TryFindResource(key);
    }

    /// <summary>Rückrichtung nicht benötigt.</summary>
    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
