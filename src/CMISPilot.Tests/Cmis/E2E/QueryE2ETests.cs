using System.Linq;
using System.Threading.Tasks;
using CMISPilot.Cmis.Errors;
using CMISPilot.Cmis.Query;

namespace CMISPilot.Tests.Cmis.E2E;

/// <summary>
/// T11.3 – Query gegen A1 (<see cref="QueryService"/>): eine einfache CMISQL-Abfrage
/// ausführen und Spalten/Zeilen prüfen.
/// </summary>
[Trait("Category", "Integration")]
public class QueryE2ETests
{
    [Fact]
    public async Task Query_SelectFolders_ReturnsColumnsAndRows()
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
            var query = new QueryService(s.Executor, s.Context);

            var result = await query.QueryAsync("SELECT * FROM cmis:folder");

            Assert.NotNull(result);
            Assert.NotEmpty(result.ColumnNames);
            // A1 enthält mindestens den Root- und Beispielordner -> Zeilen erwartet.
            Assert.NotEmpty(result.Rows);
            Assert.All(result.Rows, r => Assert.NotNull(r.ValuesByColumn));
        }
    }
}
