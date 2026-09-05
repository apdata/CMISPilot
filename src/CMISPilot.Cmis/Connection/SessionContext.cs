using System;
using CMISPilot.Cmis.Contracts;
using CMISPilot.Cmis.Errors;
using CMISPilot.Cmis.Models;
using PortCMIS.Client;

namespace CMISPilot.Cmis.Connection;

/// <summary>
/// Singleton-Zustand der aktiven CMIS-Verbindung. Implementiert die öffentliche
/// <see cref="ISessionContext"/> (nur DTOs) und den library-internen
/// <see cref="ICmisSessionAccessor"/> (PortCMIS-Session). So sehen die
/// UI-Schichten nie einen PortCMIS-Typ (NFA-03a), während die Sibling-Dienste
/// die aktive Session intern nutzen können.
/// </summary>
public sealed class SessionContext : ISessionContext, ICmisSessionAccessor
{
    private readonly object _gate = new();
    private ISession? _session;
    private RepositoryInfoDto? _repository;
    private ConnectionProfile? _profile;

    public bool IsConnected
    {
        get { lock (_gate) return _session is not null; }
    }

    public RepositoryInfoDto? CurrentRepository
    {
        get { lock (_gate) return _repository; }
    }

    public ConnectionProfile? CurrentProfile
    {
        get { lock (_gate) return _profile; }
    }

    public event EventHandler? ConnectionChanged;

    ISession? ICmisSessionAccessor.Session
    {
        get { lock (_gate) return _session; }
    }

    ISession ICmisSessionAccessor.RequireSession()
    {
        lock (_gate)
        {
            return _session
                ?? throw new CmisAppException(
                    CmisErrorKind.InvalidArgument,
                    "Es besteht keine aktive Verbindung. Bitte zuerst verbinden.");
        }
    }

    /// <summary>Setzt die aktive Session (library-intern, von der ConnectionService aufgerufen).</summary>
    internal void Set(ISession session, RepositoryInfoDto repository, ConnectionProfile profile)
    {
        lock (_gate)
        {
            _session = session;
            _repository = repository;
            _profile = profile;
        }
        ConnectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Leert den Kontext (library-intern, beim Trennen).</summary>
    internal void Clear()
    {
        lock (_gate)
        {
            _session = null;
            _repository = null;
            _profile = null;
        }
        ConnectionChanged?.Invoke(this, EventArgs.Empty);
    }
}
