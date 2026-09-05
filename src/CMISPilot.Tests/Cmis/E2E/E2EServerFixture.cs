using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using CMISPilot.Cmis.Connection;
using CMISPilot.Cmis.Diagnostics;
using CMISPilot.Cmis.Execution;
using CMISPilot.Cmis.Models;

namespace CMISPilot.Tests.Cmis.E2E;

/// <summary>
/// Gemeinsame Basis für die M11-E2E-Integrationstests (T11.1–T11.7). Kapselt die
/// Testserver-Konstanten, die Port-Probe (Soft-Skip bei nicht erreichbarem Server,
/// DHCP kann die VM-IP wechseln) und den echten Verbindungsaufbau über
/// <see cref="ConnectionService"/>. Übernimmt exakt das Muster aus
/// <see cref="ConnectionIntegrationTests"/>.
/// </summary>
internal static class E2EServer
{
    public const string Host = "localhost";
    public const int Port = 8080;
    public const string BrowserUrl = "http://localhost:8080/inmemory/browser";
    public const string RepositoryId = "A1";
    public const string User = "test";
    public const string Password = "test";

    /// <summary>Port-Probe: true, wenn der Testserver TCP-erreichbar ist.</summary>
    public static bool Reachable()
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

    public static ConnectionProfile Profile() => new()
    {
        BrowserUrl = BrowserUrl,
        User = User,
        Password = Password,
        RepositoryId = RepositoryId
    };

    /// <summary>
    /// Baut eine echte Session gegen A1 auf. Liefert den <see cref="SessionContext"/>
    /// (zugleich <c>ICmisSessionAccessor</c> für die Sibling-Services), den geteilten
    /// <see cref="CmisExecutor"/> und die <see cref="ConnectionService"/> zurück.
    /// Der Executor kann optional mit einem Diagnose-Log verdrahtet werden (T11.6).
    /// </summary>
    public static async Task<E2ESession> ConnectAsync(IDiagnosticsLog? log = null)
    {
        var context = new SessionContext();
        var executor = log is null ? new CmisExecutor() : new CmisExecutor(log);
        var connection = new ConnectionService(executor, context);
        var info = await connection.ConnectAsync(Profile());
        return new E2ESession(context, executor, connection, info);
    }
}

/// <summary>Ergebnis eines E2E-Verbindungsaufbaus, inkl. sauberem Teardown.</summary>
internal sealed class E2ESession : IAsyncDisposable
{
    public E2ESession(SessionContext context, CmisExecutor executor,
        ConnectionService connection, RepositoryInfoDto info)
    {
        Context = context;
        Executor = executor;
        Connection = connection;
        Info = info;
    }

    public SessionContext Context { get; }
    public CmisExecutor Executor { get; }
    public ConnectionService Connection { get; }
    public RepositoryInfoDto Info { get; }

    public async ValueTask DisposeAsync()
    {
        try { await Connection.DisconnectAsync(); } catch { /* best effort */ }
    }
}
