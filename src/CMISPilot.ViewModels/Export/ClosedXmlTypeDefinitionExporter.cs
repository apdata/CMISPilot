using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using CMISPilot.Cmis.Models;

namespace CMISPilot.ViewModels.Export;

/// <summary>
/// Excel-Export einer Typdefinition (F2) mit ClosedXML. Erzeugt ein Arbeitsblatt mit
/// einem Kopfbereich (Typ-Attribute) und darunter einer Tabelle mit einer Zeile je
/// Property-Definition: ID, Anzeigename, Datentyp, Kardinalitaet, Pflichtfeld,
/// queryable, orderable, Aenderbarkeit, Laenge/Wertebereich, vererbt.
///
/// Bewusst WPF-frei (NFA-03): reine Datei-/Format-Logik ohne UI-Bezug.
/// </summary>
public sealed class ClosedXmlTypeDefinitionExporter : ITypeDefinitionExporter
{
    public Task ExportAsync(TypeDefinitionDto type, string filePath, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        using var workbook = new XLWorkbook();
        var ws = workbook.AddWorksheet(SheetName(type));

        var row = WriteHeader(ws, type);
        row += 1; // Leerzeile zwischen Kopfbereich und Tabelle
        WritePropertyTable(ws, type, startRow: row);

        ws.Columns().AdjustToContents();
        workbook.SaveAs(filePath);
        return Task.CompletedTask;
    }

    /// <summary>Schreibt die Typ-Attribute als Schluessel/Wert-Paare; liefert die naechste freie Zeile.</summary>
    private static int WriteHeader(IXLWorksheet ws, TypeDefinitionDto type)
    {
        var title = ws.Cell(1, 1);
        title.Value = $"Typ: {type.DisplayName ?? type.Id}";
        title.Style.Font.Bold = true;
        title.Style.Font.FontSize = 14;

        var pairs = new (string Label, string? Value)[]
        {
            ("ID", type.Id),
            ("Anzeigename", type.DisplayName),
            ("Query-Name", type.QueryName),
            ("Beschreibung", type.Description),
            ("Basistyp", type.BaseType.ToString()),
            ("Uebergeordneter Typ", type.ParentTypeId),
            ("creatable", Bool(type.IsCreatable)),
            ("fileable", Bool(type.IsFileable)),
            ("queryable", Bool(type.IsQueryable)),
            ("fulltextIndexed", Bool(type.IsFulltextIndexed))
        };

        var row = 2;
        foreach (var (label, value) in pairs)
        {
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 2).Value = value ?? string.Empty;
            row++;
        }

        return row;
    }

    /// <summary>Schreibt die Property-Definitionen als Tabelle ab <paramref name="startRow"/>.</summary>
    private static void WritePropertyTable(IXLWorksheet ws, TypeDefinitionDto type, int startRow)
    {
        string[] headers =
        {
            "ID", "Anzeigename", "Datentyp", "Kardinalitaet", "Pflichtfeld",
            "queryable", "orderable", "Aenderbarkeit", "MaxLaenge", "MinWert", "MaxWert",
            "Praezision", "vererbt", "Beschreibung"
        };

        for (var c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(startRow, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        var row = startRow + 1;
        foreach (var p in type.PropertyDefinitions)
        {
            ws.Cell(row, 1).Value = p.Id;
            ws.Cell(row, 2).Value = p.DisplayName ?? string.Empty;
            ws.Cell(row, 3).Value = p.PropertyType.ToString();
            ws.Cell(row, 4).Value = p.Cardinality?.ToString() ?? string.Empty;
            ws.Cell(row, 5).Value = Bool(p.IsRequired);
            ws.Cell(row, 6).Value = Bool(p.IsQueryable);
            ws.Cell(row, 7).Value = Bool(p.IsOrderable);
            ws.Cell(row, 8).Value = p.Updatability?.ToString() ?? string.Empty;
            ws.Cell(row, 9).Value = p.MaxLength.HasValue
                ? p.MaxLength.Value.ToString(CultureInfo.InvariantCulture)
                : string.Empty;
            ws.Cell(row, 10).Value = p.MinValue ?? string.Empty;
            ws.Cell(row, 11).Value = p.MaxValue ?? string.Empty;
            ws.Cell(row, 12).Value = p.Precision ?? string.Empty;
            ws.Cell(row, 13).Value = Bool(p.IsInherited);
            ws.Cell(row, 14).Value = p.Description ?? string.Empty;
            row++;
        }

        // Als Excel-Tabelle formatieren (Autofilter, gebaenderte Zeilen), sofern es
        // mindestens eine Datenzeile gibt.
        if (type.PropertyDefinitions.Count > 0)
        {
            ws.Range(startRow, 1, row - 1, headers.Length).CreateTable();
        }
    }

    private static string SheetName(TypeDefinitionDto type)
    {
        // Excel-Blattnamen: max. 31 Zeichen, keine der Zeichen : \ / ? * [ ].
        var raw = type.DisplayName ?? type.Id;
        var cleaned = raw
            .Replace(':', '_').Replace('\\', '_').Replace('/', '_')
            .Replace('?', '_').Replace('*', '_').Replace('[', '_').Replace(']', '_');
        return cleaned.Length <= 31 ? cleaned : cleaned[..31];
    }

    private static string Bool(bool? value) => value switch
    {
        true => "ja",
        false => "nein",
        null => string.Empty
    };
}
