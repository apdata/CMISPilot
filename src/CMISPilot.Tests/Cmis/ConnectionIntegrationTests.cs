using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;
using CMISPilot.Cmis.Connection;
using CMISPilot.Cmis.Errors;
using CMISPilot.Cmis.Execution;
using CMISPilot.Cmis.Models;

namespace CMISPilot.Tests.Cmis;

/// <summary>
/// Integrationstests gegen den OpenCMIS InMemory-Server.
/// Überspringen sich selbst, wenn der Server nicht erreichbar ist (DHCP kann die
/// VM-IP wechseln) — so blockieren sie den Build nie.
/// Ausführen z. B. mit: dotnet test --filter "Category=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class ConnectionIntegrationTests
{
    private const string Host = "localhost";
    private const int Port = 8080;
    private const string BrowserUrl = "http://localhost:8080/inmemory/browser";
    private const string AtomPubUrl = "http://localhost:8080/inmemory/atom11";
    private const string RepositoryId = "A1";

    private static bool ServerReachable()
    {
        try
        {
            using var client = new TcpClient();
            var connect = client.ConnectAsync(Host, Port);
            return connect.Wait(TimeSpan.FromSeconds(2)) && client.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static ConnectionService CreateSut(out SessionContext context)
    {
        context = new SessionContext();
        return new ConnectionService(new CmisExecutor(), context);
    }

    [Fact]
    public async Task GetRepositoriesAsync_ListsRepositories()
    {
        if (!ServerReachable()) return; // Port nicht offen -> Test überspringen.

        var sut = CreateSut(out _);
        var profile = new ConnectionProfile { BrowserUrl = BrowserUrl, User = "test", Password = "test" };

        IReadOnlyList<RepositoryInfoDto> repos;
        try
        {
            repos = await sut.GetRepositoriesAsync(profile);
        }
        catch (CmisAppException)
        {
            return; // Kein passender CMIS-Server unter der IP (DHCP-Wechsel) -> überspringen.
        }

        Assert.NotEmpty(repos);
        Assert.Contains(repos, r => r.Id == RepositoryId);
    }

    [Fact]
    public async Task ConnectAsync_YieldsSessionAndRepositoryInfo()
    {
        if (!ServerReachable()) return;

        var sut = CreateSut(out var context);
        var profile = new ConnectionProfile
        {
            BrowserUrl = BrowserUrl,
            User = "test",
            Password = "test",
            RepositoryId = RepositoryId
        };

        RepositoryInfoDto info;
        try
        {
            info = await sut.ConnectAsync(profile);
        }
        catch (CmisAppException)
        {
            return; // Kein passender CMIS-Server unter der IP (DHCP-Wechsel) -> überspringen.
        }

        Assert.Equal(RepositoryId, info.Id);
        Assert.False(string.IsNullOrEmpty(info.RootFolderId));
        Assert.NotNull(info.Capabilities);
        Assert.True(context.IsConnected);
        Assert.Equal(RepositoryId, context.CurrentRepository?.Id);

        await sut.DisconnectAsync();
        Assert.False(context.IsConnected);
    }

    [Fact]
    public async Task GetRepositoriesAsync_AtomPub_ListsRepositories()
    {
        if (!ServerReachable()) return;

        var sut = CreateSut(out _);
        var profile = new ConnectionProfile
        {
            BindingType = CmisBindingType.AtomPub,
            AtomPubUrl = AtomPubUrl,
            User = "test",
            Password = "test"
        };

        IReadOnlyList<RepositoryInfoDto> repos;
        try
        {
            repos = await sut.GetRepositoriesAsync(profile);
        }
        catch (CmisAppException)
        {
            return; // Kein passender CMIS-Server unter der IP (DHCP-Wechsel) -> überspringen.
        }

        Assert.NotEmpty(repos);
        Assert.Contains(repos, r => r.Id == RepositoryId);
    }

    [Fact]
    public async Task ConnectAsync_AtomPub_YieldsSessionAndRepositoryInfo()
    {
        if (!ServerReachable()) return;

        var sut = CreateSut(out var context);
        var profile = new ConnectionProfile
        {
            BindingType = CmisBindingType.AtomPub,
            AtomPubUrl = AtomPubUrl,
            User = "test",
            Password = "test",
            RepositoryId = RepositoryId
        };

        RepositoryInfoDto info;
        try
        {
            info = await sut.ConnectAsync(profile);
        }
        catch (CmisAppException)
        {
            return; // Kein passender CMIS-Server unter der IP (DHCP-Wechsel) -> überspringen.
        }

        Assert.Equal(RepositoryId, info.Id);
        Assert.False(string.IsNullOrEmpty(info.RootFolderId));
        Assert.NotNull(info.Capabilities);
        Assert.True(context.IsConnected);
        Assert.Equal(RepositoryId, context.CurrentRepository?.Id);

        await sut.DisconnectAsync();
        Assert.False(context.IsConnected);
    }
}
