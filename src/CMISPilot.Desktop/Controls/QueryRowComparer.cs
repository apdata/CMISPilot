using System;
using System.Collections;
using System.ComponentModel;
using System.Globalization;
using CMISPilot.Cmis.Models;

namespace CMISPilot.Desktop.Controls;

/// <summary>
/// Typ-bewusster Vergleich für die dynamischen Spalten der Abfrage-Ergebnistabelle
/// (F6). <see cref="QueryRowDto.ValuesByColumn"/> enthält beliebige CMIS-Werttypen
/// (string, Zahl, bool, DateTime/DateTimeOffset) als <c>object?</c>. WPFs
/// eingebauter Sort über <c>SortDescription</c>/<c>PropertyComparer</c> löst einen
/// Indexer-Bindungspfad wie <c>ValuesByColumn[Spalte]</c> nicht zuverlässig
/// typgerecht auf, deshalb übernimmt diese Klasse den Vergleich selbst
/// (verwendet von <c>MainWindow.OnQueryResultGridSorting</c> als
/// <see cref="System.Windows.Data.ListCollectionView.CustomSort"/>).
/// </summary>
internal sealed class QueryRowComparer(string column, ListSortDirection direction) : IComparer
{
    public int Compare(object? x, object? y)
    {
        var valueX = (x as QueryRowDto)?.ValuesByColumn.GetValueOrDefault(column);
        var valueY = (y as QueryRowDto)?.ValuesByColumn.GetValueOrDefault(column);
        var result = CompareValues(valueX, valueY);
        return direction == ListSortDirection.Descending ? -result : result;
    }

    private static int CompareValues(object? x, object? y)
    {
        // null gilt als kleinster Wert, damit fehlende Werte bei Auf- wie
        // Absteigend konsistent ans Ende sortieren (Vorzeichenwechsel oben in
        // Compare() dreht bei Descending sonst auch die Position von null um).
        if (x is null && y is null)
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        // Gleicher, vergleichbarer Typ (string, DateTime/DateTimeOffset, bool, ...):
        // direkt per IComparable statt ueber ToString(), damit z. B. Daten
        // chronologisch statt alphabetisch sortieren.
        if (x is IComparable comparableX && x.GetType() == y.GetType())
        {
            return comparableX.CompareTo(y);
        }

        // Unterschiedliche Zahlentypen (z. B. long vs. decimal, je nach CMIS-
        // Property-Datentyp): ueber double vergleichen statt ueber ToString(),
        // sonst wuerde "10" vor "9" einsortiert.
        if (IsNumeric(x) && IsNumeric(y))
        {
            return Convert.ToDouble(x, CultureInfo.InvariantCulture)
                .CompareTo(Convert.ToDouble(y, CultureInfo.InvariantCulture));
        }

        // Rueckfall fuer gemischte/unbekannte Typen: kulturinvarianter String-Vergleich,
        // wirft nie und deckt den ueblichen Fall (beides string) mit ab.
        return string.Compare(
            Convert.ToString(x, CultureInfo.InvariantCulture),
            Convert.ToString(y, CultureInfo.InvariantCulture),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNumeric(object value) =>
        value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;
}
