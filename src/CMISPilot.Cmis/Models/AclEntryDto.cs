using System;
using System.Collections.Generic;

namespace CMISPilot.Cmis.Models;

/// <summary>
/// UI-freundliche Repräsentation eines ACL-Eintrags (Access Control Entry, R6.1).
/// Eigene Abbildung von PortCMIS <c>IAce</c>.
/// </summary>
public sealed class AclEntryDto
{
    /// <summary>ID des Principals (Benutzer/Gruppe), dem die Berechtigungen gewährt werden.</summary>
    public string PrincipalId { get; init; } = string.Empty;

    /// <summary>Gewährte Berechtigungen, z. B. "cmis:read", "cmis:write", "cmis:all".</summary>
    public IReadOnlyList<string> Permissions { get; init; } = Array.Empty<string>();

    /// <summary>True, wenn der Eintrag direkt am Objekt gesetzt ist (nicht nur geerbt).</summary>
    public bool IsDirect { get; init; }
}
