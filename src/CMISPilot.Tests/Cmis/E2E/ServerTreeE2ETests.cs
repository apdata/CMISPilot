using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CMISPilot.Cmis.Browse;
using CMISPilot.Cmis.Errors;
using CMISPilot.ViewModels.Explorer;
using CommunityToolkit.Mvvm.Messaging;

namespace CMISPilot.Tests.Cmis.E2E;

/// <summary>
/// R4 Etappe 2 – E2E: treibt den <see cref="ServerTreeViewModel"/> gegen den echten
/// A1-Testserver. Verifiziert, dass sich nach dem Verbinden der Baum aufbaut
/// (Server → Repository → Wurzelordner) und dass der Lazy-Load der Unterordner
/// über einen echten <c>GetChildren</c>-Aufruf funktioniert. Soft-Skip, wenn der
/// Server nicht erreichbar ist (DHCP/Betrieb, Muster wie die übrigen E2E-Tests).
/// </summary>
[Trait("Category", "Integration")]
public class ServerTreeE2ETests
{
    [Fact]
    public async Task ServerTree_NachVerbindung_BautServerRepositoryOrdnerAuf()
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
            var browse = new BrowseService(s.Executor, s.Context);
            var messenger = new WeakReferenceMessenger();

            // Verbunden ⇒ der Konstruktor stößt den Baumaufbau an (fire-and-forget).
            var vm = new ServerTreeViewModel(s.Connection, browse, s.Context, messenger);

            // Server-Wurzel.
            await WaitAsync(() => vm.RootNodes.Count > 0);
            Assert.Single(vm.RootNodes);
            var server = vm.RootNodes[0];
            Assert.Equal(TreeNodeKind.Server, server.Kind);

            // Repository-Knoten unter dem Server.
            await WaitAsync(() => server.Children.Any(c => c.Kind == TreeNodeKind.Repository));
            var repo = server.Children.First(c => c.Kind == TreeNodeKind.Repository);
            Assert.Equal(E2EServer.RepositoryId, repo.ObjectId);

            // Wurzelordner unter dem Repository.
            await WaitAsync(() => repo.Children.Any(c => c.Kind == TreeNodeKind.Folder && !c.IsPlaceholder));
            var rootFolder = repo.Children.First(c => c.Kind == TreeNodeKind.Folder && !c.IsPlaceholder);
            Assert.False(string.IsNullOrEmpty(rootFolder.ObjectId));

            // Lazy-Load: Aufklappen lädt die echten Unterordner nach (A1-Root hat welche).
            rootFolder.IsExpanded = true;
            await WaitAsync(() => rootFolder.AreChildrenLoaded);
            Assert.All(rootFolder.Children, c => Assert.Equal(TreeNodeKind.Folder, c.Kind));
        }
    }

    /// <summary>Pollt bis zur Bedingung oder Timeout (der Baumaufbau läuft asynchron).</summary>
    private static async Task WaitAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var sw = Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < timeoutMs)
        {
            await Task.Delay(50);
        }
    }
}
