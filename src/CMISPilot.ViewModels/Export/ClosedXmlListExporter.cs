using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using CMISPilot.Cmis.Models;

namespace CMISPilot.ViewModels.Export;

/// <summary>
/// Excel-Export von Abfrage- und Ordnerlisten (F3) mit ClosedXML. Teilt sich mit
/// <see cref="ClosedXmlTypeDefinitionExporter"/> (F2) den Aufbau: Kopfbereich mit
/// Kontextangaben, darunter eine Excel-Tabelle (Autofilter, gebaenderte Zeilen) mit
/// einer Zeile je Datensatz.
///
/// Bewusst WPF-frei (NFA-03): reine Datei-/Format-Logik ohne UI-Bezug.
/// </summary>
public sealed class ClosedXmlListExporter : IListExporter
{
    public Task ExportQueryResultAsync(
        IReadOnlyList<string> columnNames,
        IReadOnlyList<QueryRowDto> rows,
        string? cmisql,
        string filePath,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        using var workbook = new XLWorkbook();
        var ws = workbook.AddWorksheet("Abfrageergebnis");

        var pairs = new List<(string Label, string Value)>
        {
            ("Zeilen", rows.Count.ToString(CultureInfo.InvariantCulture)),
            ("Exportiert am", DateTime.Now.ToString("g", CultureInfo.CurrentCulture))
        };

        if (!string.IsNullOrWhiteSpace(cmisql))
        {
            // Mehrzeilige Abfragen in einer Zelle zusammenfassen, damit der Kopfbereich
            // eine feste Hoehe behaelt.
            pairs.Insert(0, ("Abfrage", SingleLine(cmisql)));
        }

        var startRow = WriteHeader(ws, "Abfrageergebnis", pairs);

        // Spalten: die Query-Namen in Reihenfolge der SELECT-Liste. Liefert der Server
        // keine Spaltennamen (z. B. bei leerem Ergebnis), bleibt nur der Kopfbereich.
        WriteTable(
            ws, startRow, columnNames,
            rows.Select(row => columnNames
                .Select(c => ToCellValue(row.ValuesByColumn.TryGetValue(c, out var v) ? v : null))
                .ToArray()));

        ws.Columns().AdjustToContents();
        workbook.SaveAs(filePath);
        return Task.CompletedTask;
    }

    public Task ExportObjectListAsync(
        CmisObjectDto folder,
        IReadOnlyList<CmisObjectDto> objects,
        string filePath,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        using var workbook = new XLWorkbook();
        var ws = workbook.AddWorksheet("Ordnerinhalt");

        var startRow = WriteHeader(ws, $"Ordner: {folder.Name ?? folder.Id}", new[]
        {
            ("Ordner-ID", folder.Id),
            ("Objekte", objects.Count.ToString(CultureInfo.InvariantCulture)),
            ("Exportiert am", DateTime.Now.ToString("g", CultureInfo.CurrentCulture))
        });

        string[] headers =
        {
            "Name", "Art", "Typ", "ID", "Groesse (Bytes)", "MIME-Typ", "Dateiname",
            "Erstellt von", "Erstellt am", "Geaendert von", "Geaendert am"
        };

        WriteTable(ws, startRow, headers, objects.Select(o => new[]
        {
            ToCellValue(o.Name),
            ToCellValue(o.IsFolder ? "Ordner" : o.IsDocument ? "Dokument" : o.BaseType.ToString()),
            ToCellValue(o.TypeId),
            ToCellValue(o.Id),
            ToCellValue(o.ContentStreamLength),
            ToCellValue(o.ContentStreamMimeType),
            ToCellValue(o.ContentStreamFileName),
            ToCellValue(o.CreatedBy),
            ToCellValue(o.CreationDate),
            ToCellValue(o.LastModifiedBy),
            ToCellValue(o.LastModificationDate)
        }));

        ws.Columns().AdjustToContents();
        workbook.SaveAs(filePath);
        return Task.CompletedTask;
    }

    public Task ExportRepositoryInfoAsync(
        RepositoryInfoDto info,
        IReadOnlyList<RepositoryInfoRowExportDto> rows,
        IReadOnlyList<PermissionMappingExportDto> permissionMapping,
        string filePath,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        using var workbook = new XLWorkbook();
        var ws = workbook.AddWorksheet("Repository-Info");

        var startRow = WriteHeader(ws, $"Repository: {info.Name ?? info.Id}", new[]
        {
            ("Repository-ID", info.Id),
            ("Produkt", $"{info.ProductName} {info.ProductVersion}".Trim()),
            ("CMIS-Version", info.CmisVersion ?? string.Empty),
            ("Exportiert am", DateTime.Now.ToString("g", CultureInfo.CurrentCulture))
        });

        string[] headers = { "Abschnitt", "Bezeichnung", "Wert" };

        WriteTable(ws, startRow, headers, rows.Select(r => new[]
        {
            ToCellValue(r.Section),
            ToCellValue(r.Name),
            ToCellValue(r.Value)
        }));

        ws.Columns().AdjustToContents();

        // Zweites Blatt nur fuer die Berechtigungszuordnung: als einzige Angabe hat sie
        // eine andere Spaltenform (Schluessel -> n Berechtigungen). Auf dem
        // Uebersichtsblatt stuenden die Berechtigungen mit Komma verbunden in einer
        // Zelle und liessen sich nicht filtern; als eigene Tabelle schon.
        if (permissionMapping.Count > 0)
        {
            var mappingSheet = workbook.AddWorksheet("Berechtigungszuordnung");
            var mappingStart = WriteHeader(mappingSheet, "Berechtigungszuordnung", new[]
            {
                ("Repository", info.Name ?? info.Id),
                ("Zuordnungen", permissionMapping.Count.ToString(CultureInfo.InvariantCulture))
            });

            WriteTable(mappingSheet, mappingStart, new[] { "Schlüssel", "Berechtigung" },
                permissionMapping.Select(m => new[] { ToCellValue(m.Key), ToCellValue(m.Permission) }));

            mappingSheet.Columns().AdjustToContents();
        }

        workbook.SaveAs(filePath);
        return Task.CompletedTask;
    }

    public Task ExportObjectPropertiesAsync(
        CmisObjectDto target,
        IReadOnlyList<PropertyRowExportDto> rows,
        string filePath,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        using var workbook = new XLWorkbook();
        var ws = workbook.AddWorksheet("Eigenschaften");

        var startRow = WriteHeader(ws, $"Eigenschaften: {target.Name ?? target.Id}", new[]
        {
            ("Objekt-ID", target.Id),
            ("Typ", target.TypeId ?? string.Empty),
            ("Properties", rows.Count.ToString(CultureInfo.InvariantCulture)),
            ("Exportiert am", DateTime.Now.ToString("g", CultureInfo.CurrentCulture))
        });

        string[] headers = { "Displayname", "PropertyID", "Typ", "Datentyp", "Pflichtfeld", "Wert" };

        WriteTable(ws, startRow, headers, rows.Select(r => new[]
        {
            ToCellValue(r.DisplayName),
            ToCellValue(r.PropertyId),
            ToCellValue(r.OwningTypeId),
            ToCellValue(r.DataType),
            ToCellValue(r.IsRequired),
            ToCellValue(r.Value)
        }));

        ws.Columns().AdjustToContents();
        workbook.SaveAs(filePath);
        return Task.CompletedTask;
    }

    /// <summary>Schreibt Titel und Schluessel/Wert-Paare; liefert die Startzeile der Tabelle.</summary>
    private static int WriteHeader(IXLWorksheet ws, string title, IEnumerable<(string Label, string Value)> pairs)
    {
        var titleCell = ws.Cell(1, 1);
        titleCell.Value = title;
        titleCell.Style.Font.Bold = true;
        titleCell.Style.Font.FontSize = 14;

        var row = 2;
        foreach (var (label, value) in pairs)
        {
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 2).Value = value;
            row++;
        }

        return row + 1; // Leerzeile zwischen Kopfbereich und Tabelle
    }

    /// <summary>
    /// Schreibt Tabellenkopf und Datenzeilen ab <paramref name="startRow"/> und
    /// formatiert den Bereich als Excel-Tabelle (Autofilter, gebaenderte Zeilen),
    /// sofern es Spalten und mindestens eine Datenzeile gibt.
    /// </summary>
    private static void WriteTable(
        IXLWorksheet ws, int startRow, IReadOnlyList<string> headers, IEnumerable<XLCellValue[]> rows)
    {
        if (headers.Count == 0)
        {
            return;
        }

        for (var c = 0; c < headers.Count; c++)
        {
            var cell = ws.Cell(startRow, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        var row = startRow + 1;
        foreach (var values in rows)
        {
            for (var c = 0; c < headers.Count && c < values.Length; c++)
            {
                ws.Cell(row, c + 1).Value = values[c];
            }

            row++;
        }

        if (row > startRow + 1)
        {
            ws.Range(startRow, 1, row - 1, headers.Count).CreateTable();
        }
    }

    /// <summary>
    /// Uebersetzt einen Property-Wert in einen typgerechten Zellwert: Zahlen, Wahrheits-
    /// werte und Datumsangaben bleiben Zahlen bzw. Datumswerte (damit in Excel gerechnet
    /// und sortiert werden kann), alles andere wird Text. Mehrwertige Properties werden
    /// mit Semikolon verbunden.
    /// </summary>
    private static XLCellValue ToCellValue(object? value) => value switch
    {
        null => string.Empty,
        string s => s,
        bool b => b,
        DateTimeOffset dto => dto.LocalDateTime,
        DateTime dt => dt,
        sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal
            => Convert.ToDouble(value, CultureInfo.InvariantCulture),
        IEnumerable list => string.Join(
            "; ", list.Cast<object?>().Select(v => Convert.ToString(v, CultureInfo.CurrentCulture) ?? string.Empty)),
        _ => Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty
    };

    /// <summary>Faltet einen mehrzeiligen Text (CMISQL-Editor) in eine Zeile.</summary>
    private static string SingleLine(string text) =>
        string.Join(" ", text.Split('\r', '\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
