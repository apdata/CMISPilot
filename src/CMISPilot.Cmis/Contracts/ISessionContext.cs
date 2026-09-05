using System;
using CMISPilot.Cmis.Models;

namespace CMISPilot.Cmis.Contracts;

/// <summary>
/// Hält den Zustand der aktiven CMIS-Verbindung (Singleton). Bietet nach außen
/// ausschließlich DTOs; die konkrete PortCMIS-Session bleibt library-intern
/// (NFA-03a). Andere Dienste (Browse/Query/Type/Object) beziehen die aktive
/// Session library-intern über diesen Kontext.
/// </summary>
public interface ISessionContext
{
    /// <summary>True, wenn eine aktive Session besteht.</summary>
    bool IsConnected { get; }

    /// <summary>Info des aktuell verbundenen Repositories (null, wenn getrennt).</summary>
    RepositoryInfoDto? CurrentRepository { get; }

    /// <summary>Aktuell genutztes Verbindungsprofil (ohne Passwort-Weitergabe nach außen empfohlen).</summary>
    ConnectionProfile? CurrentProfile { get; }

    /// <summary>Wird ausgelöst, wenn sich der Verbindungszustand ändert (Connect/Disconnect).</summary>
    event EventHandler? ConnectionChanged;
}
