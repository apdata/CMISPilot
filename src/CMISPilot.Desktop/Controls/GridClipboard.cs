using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CMISPilot.Desktop.Controls;

/// <summary>
/// Gemeinsame Logik fuer "Zeile kopieren"/"Alle Zeilen kopieren" im Kontextmenue
/// beliebiger <see cref="DataGrid"/>s (Eigenschaften-Fenster, Abfrageergebnis,
/// Ausgabe/Fehlerliste, Diagnose usw.). Nutzt die eingebaute DataGrid-
/// Zwischenablagenserialisierung (<see cref="ApplicationCommands.Copy"/>) statt
/// eigenen Text-Zusammenbaus, damit Spaltenformatierung/Escaping konsistent mit
/// Strg+C bleibt. Von mehreren Code-Behind-Klassen (MainWindow, ExtendedPropertiesWindow)
/// per duenner Wrapper-Methode verwendet, da XAML-Eventattribute nur Methoden der
/// eigenen Klasse referenzieren koennen.
/// </summary>
internal static class GridClipboard
{
    /// <summary>
    /// Selektiert die DataGridRow unter dem Cursor bei einem reinen Rechtsklick, damit
    /// „Zeile kopieren" immer die tatsächlich angeklickte Zeile trifft (WPF selektiert
    /// dabei anders als bei einem Linksklick nicht automatisch).
    /// </summary>
    public static void SelectRowUnderCursor(MouseButtonEventArgs e)
    {
        if (FindAncestor<DataGridRow>((DependencyObject)e.OriginalSource) is { } row)
        {
            row.IsSelected = true;
        }
    }

    /// <summary>Kopiert die aktuell selektierte(n) Zeile(n) des Grids in die Zwischenablage.</summary>
    public static void CopyRow(object contextMenuSender)
    {
        if (TryGetContextMenuGrid(contextMenuSender, out var grid) && ApplicationCommands.Copy.CanExecute(null, grid))
        {
            ApplicationCommands.Copy.Execute(null, grid);
        }
    }

    /// <summary>Kopiert alle Zeilen des Grids in die Zwischenablage, unabhängig von der aktuellen Selektion.</summary>
    public static void CopyAllRows(object contextMenuSender)
    {
        if (!TryGetContextMenuGrid(contextMenuSender, out var grid))
        {
            return;
        }

        grid.SelectAllCells();
        if (ApplicationCommands.Copy.CanExecute(null, grid))
        {
            ApplicationCommands.Copy.Execute(null, grid);
        }
    }

    /// <summary>Löst das Grid auf, an dessen Kontextmenü der Klick auftrat (über <see cref="ContextMenu.PlacementTarget"/>).</summary>
    private static bool TryGetContextMenuGrid(object sender, out DataGrid grid)
    {
        if (sender is MenuItem { Parent: ContextMenu { PlacementTarget: DataGrid dataGrid } })
        {
            grid = dataGrid;
            return true;
        }

        grid = null!;
        return false;
    }

    private static T? FindAncestor<T>(DependencyObject? element) where T : DependencyObject
    {
        while (element is not null && element is not T)
        {
            element = VisualTreeHelper.GetParent(element);
        }

        return element as T;
    }
}
