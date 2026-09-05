using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CMISPilot.Cmis.Models;

namespace CMISPilot.Cmis.Contracts;

/// <summary>
/// Schreibende Operationen und Dokumentinhalte (FA-70/71/72/73/74, FA-40/42).
/// Setzt eine aktive Session voraus. Properties werden als einfache
/// ID-zu-Wert-Map übergeben (z. B. { "cmis:name": "Neu", "cmis:objectTypeId": "cmis:folder" }),
/// damit keine PortCMIS-Typen nach außen lecken.
/// </summary>
public interface IObjectService
{
    /// <summary>Legt einen Ordner unter <paramref name="parentId"/> an (FA-70).</summary>
    Task<CmisObjectDto> CreateFolderAsync(
        string parentId, IDictionary<string, object?> properties, CancellationToken ct = default);

    /// <summary>
    /// Legt ein Dokument unter <paramref name="parentId"/> an, optional mit Inhalt (FA-71/73).
    /// </summary>
    Task<CmisObjectDto> CreateDocumentAsync(
        string parentId,
        IDictionary<string, object?> properties,
        Stream? content = null,
        string? fileName = null,
        string? mimeType = null,
        CancellationToken ct = default);

    /// <summary>Aktualisiert Properties eines Objekts (FA-72). Liefert das aktualisierte Objekt.</summary>
    Task<CmisObjectDto> UpdatePropertiesAsync(
        string objectId, IDictionary<string, object?> properties, CancellationToken ct = default);

    /// <summary>Löscht ein einzelnes Objekt (FA-74).</summary>
    Task DeleteAsync(string objectId, bool allVersions = true, CancellationToken ct = default);

    /// <summary>Löscht einen Ordner samt Inhalt (deleteTree, FA-74).</summary>
    Task DeleteTreeAsync(string folderId, bool allVersions = true, CancellationToken ct = default);

    /// <summary>Lädt den Content-Stream eines Dokuments (FA-40). Aufrufer schließt den Stream.</summary>
    Task<CmisContentDto> GetContentStreamAsync(string objectId, CancellationToken ct = default);

    /// <summary>Setzt oder ersetzt den Inhalt eines Dokuments (FA-42/73).</summary>
    Task SetContentStreamAsync(
        string objectId,
        Stream content,
        string fileName,
        string mimeType,
        bool overwrite = true,
        CancellationToken ct = default);

    /// <summary>
    /// Lädt die ACL (Access Control List) eines Objekts (R6.1). Liefert eine leere
    /// Liste, wenn der Server keine ACLs unterstützt oder keine Einträge vorhanden sind.
    /// </summary>
    Task<IReadOnlyList<AclEntryDto>> GetAclAsync(string objectId, CancellationToken ct = default);

    /// <summary>
    /// Lädt alle Versionen der Versionsreihe eines Dokuments (R6.1). Für nicht
    /// versionierbare Dokumente bzw. Nicht-Dokumente wird eine einelementige Liste
    /// mit dem Objekt selbst geliefert.
    /// </summary>
    Task<IReadOnlyList<ObjectVersionDto>> GetAllVersionsAsync(string objectId, CancellationToken ct = default);
}
