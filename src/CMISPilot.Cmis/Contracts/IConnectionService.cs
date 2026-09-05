using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CMISPilot.Cmis.Models;

namespace CMISPilot.Cmis.Contracts;

/// <summary>
/// Baut CMIS-Verbindungen über das Browser Binding auf und ab (FA-01/02/05).
/// Alle Methoden sind asynchron und abbrechbar (NFA-05/13).
/// </summary>
public interface IConnectionService
{
    /// <summary>
    /// Listet die auf dem Server verfügbaren Repositories, ohne eine dauerhafte
    /// Session zu etablieren (FA-02). <see cref="ConnectionProfile.RepositoryId"/>
    /// wird dabei nicht benötigt.
    /// </summary>
    Task<IReadOnlyList<RepositoryInfoDto>> GetRepositoriesAsync(
        ConnectionProfile profile, CancellationToken ct = default);

    /// <summary>
    /// Baut eine Session zum in <see cref="ConnectionProfile.RepositoryId"/>
    /// angegebenen Repository auf, macht sie im <see cref="ISessionContext"/>
    /// aktiv und liefert dessen Repository-Info inkl. Capabilities (FA-01/10/11).
    /// </summary>
    Task<RepositoryInfoDto> ConnectAsync(ConnectionProfile profile, CancellationToken ct = default);

    /// <summary>Trennt die aktive Session und leert den <see cref="ISessionContext"/>.</summary>
    Task DisconnectAsync(CancellationToken ct = default);
}
