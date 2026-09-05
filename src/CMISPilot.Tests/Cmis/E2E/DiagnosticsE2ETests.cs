using System.Linq;
using System.Threading.Tasks;
using CMISPilot.Cmis.Browse;
using CMISPilot.Cmis.Diagnostics;
using CMISPilot.Cmis.Errors;

namespace CMISPilot.Tests.Cmis.E2E;

/// <summary>
/// T11.6 – Diagnose gegen A1: Bei verdrahtetem <see cref="IDiagnosticsLog"/> im
/// <c>CmisExecutor</c> müssen nach echten Serveroperationen Log-Einträge vorliegen
/// (Operations-Level, FA-80).
/// </summary>
[Trait("Category", "Integration")]
public class DiagnosticsE2ETests
{
    [Fact]
    public async Task Diagnostics_LogsServerOperations()
    {
        if (!E2EServer.Reachable()) return;

        var log = new InMemoryDiagnosticsLog();

        E2ESession s;
        try
        {
            s = await E2EServer.ConnectAsync(log);
        }
        catch (CmisAppException)
        {
            return;
        }

        await using (s)
        {
            // Ein paar echte Operationen ausführen (nutzt denselben, mit Log verdrahteten Executor).
            var browse = new BrowseService(s.Executor, s.Context);
            var root = await browse.GetRootFolderAsync();
            await browse.GetChildrenAsync(root.Id);

            var entries = log.GetEntries();
            Assert.NotEmpty(entries);
            // Jeder Eintrag trägt einen Operationsnamen (via [CallerMemberName]).
            Assert.All(entries, e => Assert.False(string.IsNullOrWhiteSpace(e.Operation)));
        }
    }
}
