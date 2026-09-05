using System;
using System.IO;
using System.Threading.Tasks;
using ClosedXML.Excel;
using CMISPilot.Cmis.Models;
using CMISPilot.ViewModels.Export;

namespace CMISPilot.Tests.ViewModels;

/// <summary>
/// Unit-Tests des Excel-Exports von Typdefinitionen (F2). Erzeugt eine echte
/// .xlsx-Datei in einem temporaeren Verzeichnis und liest sie mit ClosedXML zurueck,
/// um Kopfbereich und Property-Tabelle zu pruefen.
/// </summary>
public sealed class ClosedXmlTypeDefinitionExporterTests : IDisposable
{
    private readonly string _dir;

    public ClosedXmlTypeDefinitionExporterTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "CMISPilotExport_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static TypeDefinitionDto SampleType() => new()
    {
        Id = "cmis:document",
        DisplayName = "Dokument",
        QueryName = "cmis:document",
        BaseType = CmisBaseType.Document,
        IsCreatable = true,
        PropertyDefinitions = new[]
        {
            new PropertyDefinitionDto
            {
                Id = "cmis:name", DisplayName = "Name", PropertyType = CmisPropertyType.String,
                Cardinality = CmisCardinality.Single, IsRequired = true, IsQueryable = true,
                Updatability = CmisUpdatability.ReadWrite, MaxLength = 255
            },
            new PropertyDefinitionDto
            {
                Id = "my:betrag", DisplayName = "Betrag", PropertyType = CmisPropertyType.Decimal,
                Cardinality = CmisCardinality.Single, IsRequired = false,
                MinValue = "0", MaxValue = "1000000", Precision = "64"
            }
        }
    };

    [Fact]
    public async Task Export_schreibt_Kopfbereich_und_eine_Zeile_je_Property()
    {
        var path = Path.Combine(_dir, "typ.xlsx");
        var sut = new ClosedXmlTypeDefinitionExporter();

        await sut.ExportAsync(SampleType(), path);

        Assert.True(File.Exists(path));

        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheet(1);

        // Kopfbereich: irgendwo steht die Typ-ID als Wert.
        var idCell = ws.CellsUsed(c => c.GetString() == "cmis:document");
        Assert.NotEmpty(idCell);

        // Tabellenkopf existiert.
        var header = ws.CellsUsed(c => c.GetString() == "MaxLaenge");
        Assert.NotEmpty(header);

        // Beide Properties tauchen als Zeilen auf.
        Assert.NotEmpty(ws.CellsUsed(c => c.GetString() == "cmis:name"));
        Assert.NotEmpty(ws.CellsUsed(c => c.GetString() == "my:betrag"));

        // Die Laenge der Name-Property ist exportiert.
        Assert.NotEmpty(ws.CellsUsed(c => c.GetString() == "255"));
    }

    [Fact]
    public async Task Export_eines_Typs_ohne_Properties_erzeugt_trotzdem_eine_gueltige_Datei()
    {
        var path = Path.Combine(_dir, "leer.xlsx");
        var sut = new ClosedXmlTypeDefinitionExporter();

        await sut.ExportAsync(
            new TypeDefinitionDto { Id = "my:leer", DisplayName = "Leer", BaseType = CmisBaseType.Document },
            path);

        Assert.True(File.Exists(path));
        using var wb = new XLWorkbook(path);
        Assert.Equal(1, wb.Worksheets.Count);
    }
}
