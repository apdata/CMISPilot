using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using CMISPilot.Cmis.Errors;
using CMISPilot.Cmis.Types;
using CMISPilot.ViewModels.Export;

namespace CMISPilot.Tests.Cmis.E2E;

/// <summary>
/// F2 gegen A1: eine echte CMIS-Typdefinition (cmis:document) laden und mit dem
/// ClosedXML-Exporter nach Excel schreiben. Prueft die vollstaendige Kette
/// (Live-Typdefinition inkl. der neuen Laengen-/Wertebereichsfelder → .xlsx) gegen
/// den Server. Soft-Skip, wenn A1 nicht erreichbar ist.
/// </summary>
[Trait("Category", "Integration")]
public class TypeExportE2ETests
{
    [Fact]
    public async Task ExportiereLiveTyp_cmisDocument_nachExcel()
    {
        if (!E2EServer.Reachable()) return;

        E2ESession s;
        try
        {
            s = await E2EServer.ConnectAsync();
        }
        catch (CmisAppException)
        {
            return;
        }

        await using (s)
        {
            var types = new TypeService(s.Executor, s.Context);
            var type = await types.GetTypeDefinitionAsync("cmis:document");
            Assert.NotEmpty(type.PropertyDefinitions);

            var dir = Path.Combine(Path.GetTempPath(), "CMISPilotE2EExport_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "cmis_document.xlsx");

            try
            {
                var exporter = new ClosedXmlTypeDefinitionExporter();
                await exporter.ExportAsync(type, path);

                Assert.True(File.Exists(path));

                using var wb = new XLWorkbook(path);
                var ws = wb.Worksheet(1);

                // Typ-ID im Kopfbereich und cmis:name als Property-Zeile.
                Assert.NotEmpty(ws.CellsUsed(c => c.GetString() == "cmis:document"));
                Assert.NotEmpty(ws.CellsUsed(c => c.GetString() == "cmis:name"));
            }
            finally
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
        }
    }
}
