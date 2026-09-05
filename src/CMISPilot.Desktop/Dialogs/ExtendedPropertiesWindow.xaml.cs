using System.Windows;
using System.Windows.Input;
using CMISPilot.Desktop.Controls;
using CMISPilot.ViewModels.ObjectDetails;

namespace CMISPilot.Desktop.Dialogs;

/// <summary>
/// Fenster „Erweiterte Eigenschaften" (R6.1): zeigt alle Properties (inkl.
/// Mehrfachwerte/Query-Name/Local-Name), die Typdefinition samt Vererbungskette,
/// Allowable Actions, ACL und Versionsreihe des im Explorer selektierten Objekts.
/// Erreichbar aus dem kontextuellen Explorer-Ribbon-Tab (siehe <c>MainWindow.xaml.cs</c>,
/// <c>OnExtendedPropertiesClick</c>). Bewusst nicht-modal (<see cref="Show"/> statt
/// <see cref="Window.ShowDialog"/>): das Fenster dient der Inspektion, nicht der
/// Eingabe, daher soll die restliche Werkbank währenddessen bedienbar bleiben.
/// </summary>
public partial class ExtendedPropertiesWindow : Window
{
    public ExtendedPropertiesWindow(ExtendedPropertiesViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    /// <summary>Gemeinsame Logik mit <c>MainWindow</c> für „Zeile kopieren"/„Alle Zeilen kopieren", siehe <see cref="GridClipboard"/>.</summary>
    private void OnGridRowPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e) =>
        GridClipboard.SelectRowUnderCursor(e);

    private void OnCopyRowClick(object sender, RoutedEventArgs e) => GridClipboard.CopyRow(sender);

    private void OnCopyAllRowsClick(object sender, RoutedEventArgs e) => GridClipboard.CopyAllRows(sender);
}
