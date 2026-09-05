using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CMISPilot.Cmis.Models;

namespace CMISPilot.Cmis.Contracts;

/// <summary>
/// Navigation im Repository: Ordner/Objekte laden (FA-20/21/22). Setzt eine aktive
/// Session im <see cref="ISessionContext"/> voraus.
/// </summary>
public interface IBrowseService
{
    /// <summary>Lädt den Wurzelordner des aktuellen Repositories.</summary>
    Task<CmisObjectDto> GetRootFolderAsync(CancellationToken ct = default);

    /// <summary>Lädt ein einzelnes Objekt (Ordner oder Dokument) per ID.</summary>
    Task<CmisObjectDto> GetObjectAsync(string objectId, CancellationToken ct = default);

    /// <summary>Lädt ein Objekt über seinen Pfad, z. B. "/Sites/marketing".</summary>
    Task<CmisObjectDto> GetObjectByPathAsync(string path, CancellationToken ct = default);

    /// <summary>Lädt die direkten Kinder eines Ordners (FA-21).</summary>
    Task<IReadOnlyList<CmisObjectDto>> GetChildrenAsync(string folderId, CancellationToken ct = default);

    /// <summary>
    /// Lädt die Elternordner eines Objekts (für Breadcrumb/Pfad, FA-92).
    /// Beim Wurzelordner leer.
    /// </summary>
    Task<IReadOnlyList<CmisObjectDto>> GetParentsAsync(string objectId, CancellationToken ct = default);
}
