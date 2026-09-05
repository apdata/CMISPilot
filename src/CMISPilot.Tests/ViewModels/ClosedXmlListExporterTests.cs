using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ClosedXML.Excel;
using CMISPilot.Cmis.Models;
using CMISPilot.ViewModels.Export;

namespace CMISPilot.Tests.ViewModels;

/// <summary>
/// Unit-Tests des Excel-Exports von Abfrage- und Ordnerlisten (F3). Erzeugt echte
/// .xlsx-Dateien in einem temporaeren Verzeichnis und liest sie mit ClosedXML zurueck.
/// </summary>
public sealed class ClosedXmlListExporterTests : IDisposable
{
    private readonly string _dir;

    public ClosedXmlListExporterTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "CMISPilotListExport_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static (IReadOnlyList<string> Columns, IReadOnlyList<QueryRowDto> Rows) SampleResult() =>
    (
        new[] { "cmis:name", "cmis:objectId", "my:betrag" },
        new[]
        {
            new QueryRowDto
            {
                ObjectId = "1",
                ValuesByColumn = new Dictionary<string, object?>
                {
                    ["cmis:name"] = "Dokument A",
                    ["cmis:objectId"] = "1",
                    ["my:betrag"] = 42.5m
                }
            },
            new QueryRowDto
            {
                ObjectId = "2",
                ValuesByColumn = new Dictionary<string, object?>
                {
                    ["cmis:name"] = "Dokument B",
                    ["cmis:objectId"] = "2"
                    // my:betrag fehlt bewusst -> leere Zelle
                }
            }
        }
    );

    [Fact]
    public async Task Abfrageexport_schreibt_Kopfbereich_Spalten_und_eine_Zeile_je_Treffer()
    {
        var path = Path.Combine(_dir, "abfrage.xlsx");
        var (columns, rows) = SampleResult();
        var sut = new ClosedXmlListExporter();

        await sut.ExportQueryResultAsync(columns, rows, "SELECT * FROM cmis:document", path);

        Assert.True(File.Exists(path));

        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheet(1);

        // Kopfbereich: die ausgefuehrte Abfrage steht in einer Zelle.
        Assert.NotEmpty(ws.CellsUsed(c => c.GetString() == "SELECT * FROM cmis:document"));

        // Tabellenkopf: eine Spalte je Query-Name.
        Assert.NotEmpty(ws.CellsUsed(c => c.GetString() == "cmis:name"));
        Assert.NotEmpty(ws.CellsUsed(c => c.GetString() == "my:betrag"));

        // Beide Treffer als Zeilen.
        Assert.NotEmpty(ws.CellsUsed(c => c.GetString() == "Dokument A"));
        Assert.NotEmpty(ws.CellsUsed(c => c.GetString() == "Dokument B"));

        // Zahlen bleiben Zahlen (rechenbar/sortierbar in Excel).
        Assert.NotEmpty(ws.CellsUsed(c => c.DataType == XLDataType.Number && c.GetDouble() == 42.5));
    }

    [Fact]
    public async Task Abfrageexport_ohne_Treffer_erzeugt_trotzdem_eine_gueltige_Datei()
    {
        var path = Path.Combine(_dir, "leer.xlsx");
        var sut = new ClosedXmlListExporter();

        await sut.ExportQueryResultAsync(Array.Empty<string>(), Array.Empty<QueryRowDto>(), null, path);

        Assert.True(File.Exists(path));
        using var wb = new XLWorkbook(path);
        Assert.Equal(1, wb.Worksheets.Count);
    }

    [Fact]
    public async Task Ordnerexport_schreibt_eine_Zeile_je_Objekt_mit_Art_und_Groesse()
    {
        var path = Path.Combine(_dir, "ordner.xlsx");
        var folder = new CmisObjectDto { Id = "f1", Name = "Rechnungen", BaseType = CmisBaseType.Folder };
        var objects = new[]
        {
            new CmisObjectDto
            {
                Id = "d1", Name = "Rechnung.pdf", BaseType = CmisBaseType.Document,
                TypeId = "cmis:document", ContentStreamLength = 2048,
                ContentStreamMimeType = "application/pdf",
                CreatedBy = "alex",
                CreationDate = new DateTimeOffset(2026, 8, 5, 10, 0, 0, TimeSpan.Zero)
            },
            new CmisObjectDto
            {
                Id = "f2", Name = "Unterordner", BaseType = CmisBaseType.Folder, TypeId = "cmis:folder"
            }
        };
        var sut = new ClosedXmlListExporter();

        await sut.ExportObjectListAsync(folder, objects, path);

        Assert.True(File.Exists(path));

        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheet(1);

        // Kopfbereich mit Ordnername und -ID.
        Assert.NotEmpty(ws.CellsUsed(c => c.GetString() == "Ordner: Rechnungen"));
        Assert.NotEmpty(ws.CellsUsed(c => c.GetString() == "f1"));

        // Beide Kindobjekte, jeweils mit ihrer Art.
        Assert.NotEmpty(ws.CellsUsed(c => c.GetString() == "Rechnung.pdf"));
        Assert.NotEmpty(ws.CellsUsed(c => c.GetString() == "Unterordner"));
        Assert.NotEmpty(ws.CellsUsed(c => c.GetString() == "Dokument"));
        Assert.NotEmpty(ws.CellsUsed(c => c.GetString() == "Ordner"));

        // Groesse als Zahl.
        Assert.NotEmpty(ws.CellsUsed(c => c.DataType == XLDataType.Number && c.GetDouble() == 2048));
    }

    [Fact]
    public async Task ExportRepositoryInfo_SchreibtKopfbereichUndTabelle()
    {
        var path = Path.Combine(_dir, "repo.xlsx");
        var info = new RepositoryInfoDto
        {
            Id = "A1",
            Name = "Testablage",
            ProductName = "OpenCMIS InMemory",
            ProductVersion = "1.1",
            CmisVersion = "1.1"
        };

        IReadOnlyList<RepositoryInfoRowExportDto> rows =
        [
            new("Eckdaten", "Name", "Testablage"),
            new("Capabilities", "Abfragen", "bothcombined")
        ];

        await new ClosedXmlListExporter().ExportRepositoryInfoAsync(info, rows, [], path);

        Assert.True(File.Exists(path));

        using var workbook = new XLWorkbook(path);
        var ws = workbook.Worksheet("Repository-Info");

        Assert.Equal("Repository: Testablage", ws.Cell(1, 1).GetString());
        Assert.Equal("Repository-ID", ws.Cell(2, 1).GetString());
        Assert.Equal("A1", ws.Cell(2, 2).GetString());

        // Kopfbereich sind 4 Paare (Zeilen 2-5), danach eine Leerzeile -> Tabellenkopf in Zeile 7.
        Assert.Equal("Abschnitt", ws.Cell(7, 1).GetString());
        Assert.Equal("Bezeichnung", ws.Cell(7, 2).GetString());
        Assert.Equal("Wert", ws.Cell(7, 3).GetString());
        Assert.Equal("Eckdaten", ws.Cell(8, 1).GetString());
        Assert.Equal("bothcombined", ws.Cell(9, 3).GetString());
    }

    [Fact]
    public async Task ExportRepositoryInfo_LegtDieBerechtigungszuordnungAufEinEigenesBlatt()
    {
        var path = Path.Combine(_dir, "repo-acl.xlsx");
        var info = new RepositoryInfoDto { Id = "A1", Name = "Testablage" };

        IReadOnlyList<RepositoryInfoRowExportDto> rows = [new("Eckdaten", "Name", "Testablage")];
        IReadOnlyList<PermissionMappingExportDto> mapping =
        [
            new("canGetProperties.Object", "cmis:read"),
            new("canGetProperties.Object", "cmis:write")
        ];

        await new ClosedXmlListExporter().ExportRepositoryInfoAsync(info, rows, mapping, path);

        using var workbook = new XLWorkbook(path);
        var ws = workbook.Worksheet("Berechtigungszuordnung");

        Assert.Equal("Berechtigungszuordnung", ws.Cell(1, 1).GetString());
        // Kopfbereich sind 2 Paare (Zeilen 2-3), danach eine Leerzeile -> Tabellenkopf in Zeile 5.
        Assert.Equal("Schlüssel", ws.Cell(5, 1).GetString());
        Assert.Equal("Berechtigung", ws.Cell(5, 2).GetString());
        // Je Paar eine eigene Zeile, nicht mit Komma in einer Zelle.
        Assert.Equal("cmis:read", ws.Cell(6, 2).GetString());
        Assert.Equal("cmis:write", ws.Cell(7, 2).GetString());
    }

    [Fact]
    public async Task ExportRepositoryInfo_OhneZuordnung_LegtKeinZweitesBlattAn()
    {
        var path = Path.Combine(_dir, "repo-ohne-acl.xlsx");
        var info = new RepositoryInfoDto { Id = "A1", Name = "Testablage" };

        await new ClosedXmlListExporter().ExportRepositoryInfoAsync(
            info, [new RepositoryInfoRowExportDto("Eckdaten", "Name", "Testablage")], [], path);

        using var workbook = new XLWorkbook(path);
        Assert.Single(workbook.Worksheets);
    }
}
