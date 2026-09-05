using System.Threading.Tasks;
using CMISPilot.Cmis.Connection;
using CMISPilot.Cmis.Contracts;
using CMISPilot.Cmis.Errors;
using CMISPilot.Cmis.Execution;
using CMISPilot.Cmis.Models;

namespace CMISPilot.Tests.Cmis;

public class ConnectionServiceTests
{
    private static ConnectionService CreateSut(out SessionContext context)
    {
        context = new SessionContext();
        return new ConnectionService(new CmisExecutor(), context);
    }

    [Fact]
    public async Task ConnectAsync_WithoutUrl_ThrowsInvalidArgument()
    {
        var sut = CreateSut(out _);
        var profile = new ConnectionProfile { RepositoryId = "A1" };
        await Assert.ThrowsAsync<CmisInvalidArgumentException>(() => sut.ConnectAsync(profile));
    }

    [Fact]
    public async Task ConnectAsync_WithoutRepositoryId_ThrowsInvalidArgument()
    {
        var sut = CreateSut(out _);
        var profile = new ConnectionProfile { BrowserUrl = "http://localhost/browser" };
        await Assert.ThrowsAsync<CmisInvalidArgumentException>(() => sut.ConnectAsync(profile));
    }

    [Fact]
    public async Task GetRepositoriesAsync_WithoutUrl_ThrowsInvalidArgument()
    {
        var sut = CreateSut(out _);
        await Assert.ThrowsAsync<CmisInvalidArgumentException>(
            () => sut.GetRepositoriesAsync(new ConnectionProfile()));
    }

    [Fact]
    public async Task GetRepositoriesAsync_AtomPubOhneAtomPubUrl_ThrowsInvalidArgument()
    {
        var sut = CreateSut(out _);
        var profile = new ConnectionProfile
        {
            BindingType = CmisBindingType.AtomPub,
            // Browser-URL bewusst gesetzt: bestaetigt, dass bei AtomPub tatsaechlich
            // AtomPubUrl geprueft wird, nicht BrowserUrl.
            BrowserUrl = "http://localhost/browser"
        };
        await Assert.ThrowsAsync<CmisInvalidArgumentException>(
            () => sut.GetRepositoriesAsync(profile));
    }

    [Fact]
    public async Task ConnectAsync_AtomPubOhneAtomPubUrl_ThrowsInvalidArgument()
    {
        var sut = CreateSut(out _);
        var profile = new ConnectionProfile
        {
            BindingType = CmisBindingType.AtomPub,
            RepositoryId = "A1"
        };
        await Assert.ThrowsAsync<CmisInvalidArgumentException>(() => sut.ConnectAsync(profile));
    }

    [Fact]
    public async Task DisconnectAsync_ClearsContext()
    {
        var sut = CreateSut(out var context);
        await sut.DisconnectAsync();
        Assert.False(context.IsConnected);
        Assert.Null(context.CurrentRepository);
    }

    [Fact]
    public void SessionContext_InitialState_IsDisconnected()
    {
        var context = new SessionContext();
        Assert.False(context.IsConnected);
        Assert.Null(context.CurrentRepository);
        Assert.Null(context.CurrentProfile);
    }

    [Fact]
    public void SessionContext_RequireSession_WhenDisconnected_Throws()
    {
        ICmisSessionAccessor accessor = new SessionContext();
        Assert.Throws<CmisAppException>(() => accessor.RequireSession());
    }
}
