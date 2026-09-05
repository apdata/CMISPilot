using System.Threading.Tasks;
using CMISPilot.Cmis.Errors;

namespace CMISPilot.Tests.Cmis.E2E;

/// <summary>
/// T11.7 – Verbindung-Regression gegen A1: <c>ConnectAsync</c> liefert Session +
/// RepositoryInfo + Capabilities, Kontext wird aktiv gesetzt und wieder geleert.
/// </summary>
[Trait("Category", "Integration")]
public class ConnectionE2ETests
{
    [Fact]
    public async Task Connect_YieldsSessionAndRepositoryInfoAndCapabilities()
    {
        if (!E2EServer.Reachable()) return;

        E2ESession s;
        try
        {
            s = await E2EServer.ConnectAsync();
        }
        catch (CmisAppException)
        {
            return; // Kein passender CMIS-Server unter der IP -> überspringen.
        }

        await using (s)
        {
            Assert.Equal(E2EServer.RepositoryId, s.Info.Id);
            Assert.False(string.IsNullOrEmpty(s.Info.RootFolderId));
            Assert.NotNull(s.Info.Capabilities);
            Assert.True(s.Context.IsConnected);
            Assert.Equal(E2EServer.RepositoryId, s.Context.CurrentRepository?.Id);
        }

        Assert.False(s.Context.IsConnected);
    }
}
