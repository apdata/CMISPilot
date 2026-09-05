using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CMISPilot.Cmis.Connection;
using CMISPilot.Cmis.Contracts;
using CMISPilot.Cmis.Errors;
using CMISPilot.Cmis.Execution;
using CMISPilot.Cmis.Models;
using PortCMIS.Client;
using PortCMIS.Data;
using PortCMIS.Enums;

namespace CMISPilot.Cmis.Objects;

/// <summary>
/// Schreibende Operationen (FA-70/71/72/74) und Dokumentinhalte (FA-40/42, M8) auf
/// Basis der aktiven PortCMIS-Session. Folgt dem M4-Referenzmuster
/// (<see cref="CMISPilot.Cmis.Browse.BrowseService"/>): bezieht die Session über
/// <see cref="ICmisSessionAccessor"/>, führt blockierende PortCMIS-Aufrufe über den
/// <see cref="ICmisExecutor"/> aus (NFA-05/13) und liefert nach außen nur DTOs
/// (NFA-03a), Mapping zentral über <see cref="CmisModelMapper"/>.
/// </summary>
internal sealed class ObjectService : IObjectService
{
    private readonly ICmisExecutor _executor;
    private readonly ICmisSessionAccessor _sessionAccessor;

    public ObjectService(ICmisExecutor executor, ICmisSessionAccessor sessionAccessor)
    {
        _executor = executor;
        _sessionAccessor = sessionAccessor;
    }

    public Task<CmisObjectDto> CreateFolderAsync(
        string parentId, IDictionary<string, object?> properties, CancellationToken ct = default)
    {
        RequireId(parentId, "Es wurde keine Ordner-ID (Zielordner) angegeben.");
        RequireProperties(properties);

        return _executor.RunAsync(() =>
        {
            var session = _sessionAccessor.RequireSession();
            var parent = session.CreateObjectId(parentId);
            var newId = session.CreateFolder(ToPortCmis(properties), parent);
            return CmisModelMapper.ToDto(session.GetObject(newId));
        }, ct);
    }

    public Task<CmisObjectDto> CreateDocumentAsync(
        string parentId,
        IDictionary<string, object?> properties,
        Stream? content = null,
        string? fileName = null,
        string? mimeType = null,
        CancellationToken ct = default)
    {
        RequireId(parentId, "Es wurde keine Ordner-ID (Zielordner) angegeben.");
        RequireProperties(properties);

        return _executor.RunAsync(() =>
        {
            var session = _sessionAccessor.RequireSession();
            var parent = session.CreateObjectId(parentId);

            IContentStream? contentStream = content is null
                ? null
                : new ContentStream
                {
                    Stream = content,
                    FileName = fileName,
                    MimeType = string.IsNullOrWhiteSpace(mimeType) ? "application/octet-stream" : mimeType
                };

            // Der VersioningState muss zur Typdefinition passen: versionierbare Typen
            // verlangen MAJOR/MINOR, nicht-versionierbare (z. B. das cmis:document des
            // InMemory-Servers) NONE. Ein fixer Wert wirft sonst eine
            // CmisConstraintException ("versioning state flag is incompatible").
            var typeId = properties.TryGetValue("cmis:objectTypeId", out var t)
                         && t is string ts && !string.IsNullOrWhiteSpace(ts)
                ? ts
                : "cmis:document";
            var versionable = (session.GetTypeDefinition(typeId) as IDocumentTypeDefinition)?.IsVersionable ?? false;
            var versioningState = versionable ? VersioningState.Major : VersioningState.None;

            var newId = session.CreateDocument(
                ToPortCmis(properties), parent, contentStream, versioningState);
            return CmisModelMapper.ToDto(session.GetObject(newId));
        }, ct);
    }

    public Task<CmisObjectDto> UpdatePropertiesAsync(
        string objectId, IDictionary<string, object?> properties, CancellationToken ct = default)
    {
        RequireId(objectId, "Es wurde keine Objekt-ID angegeben.");
        RequireProperties(properties);

        return _executor.RunAsync(() =>
        {
            var session = _sessionAccessor.RequireSession();
            var obj = session.GetObject(session.CreateObjectId(objectId));
            var updated = obj.UpdateProperties(ToPortCmis(properties));
            return CmisModelMapper.ToDto(updated);
        }, ct);
    }

    public Task DeleteAsync(string objectId, bool allVersions = true, CancellationToken ct = default)
    {
        RequireId(objectId, "Es wurde keine Objekt-ID angegeben.");

        return _executor.RunAsync(() =>
        {
            var session = _sessionAccessor.RequireSession();
            session.Delete(session.CreateObjectId(objectId), allVersions);
        }, ct);
    }

    public Task DeleteTreeAsync(string folderId, bool allVersions = true, CancellationToken ct = default)
    {
        RequireId(folderId, "Es wurde keine Ordner-ID angegeben.");

        return _executor.RunAsync(() =>
        {
            var session = _sessionAccessor.RequireSession();
            if (session.GetObject(session.CreateObjectId(folderId)) is not IFolder folder)
            {
                throw new CmisInvalidArgumentException(
                    "Das Objekt ist kein Ordner; deleteTree ist nur für Ordner möglich.");
            }

            // UnfileObject.Delete: Kinder werden mitgelöscht statt nur ausgehängt (FA-74).
            folder.DeleteTree(allVersions, UnfileObject.Delete, true);
        }, ct);
    }

    public Task<CmisContentDto> GetContentStreamAsync(string objectId, CancellationToken ct = default)
    {
        RequireId(objectId, "Es wurde keine Objekt-ID angegeben.");

        return _executor.RunAsync(() =>
        {
            var session = _sessionAccessor.RequireSession();
            if (session.GetObject(session.CreateObjectId(objectId)) is not IDocument doc)
            {
                throw new CmisInvalidArgumentException(
                    "Das Objekt ist kein Dokument; ein Inhalt kann nur für Dokumente geladen werden.");
            }

            var stream = doc.GetContentStream()
                ?? throw new CmisNotFoundException("Das Dokument hat keinen Inhalt (kein Content-Stream).");

            return new CmisContentDto
            {
                Stream = stream.Stream,
                FileName = stream.FileName,
                MimeType = stream.MimeType,
                Length = (long?)stream.Length
            };
        }, ct);
    }

    public Task SetContentStreamAsync(
        string objectId,
        Stream content,
        string fileName,
        string mimeType,
        bool overwrite = true,
        CancellationToken ct = default)
    {
        RequireId(objectId, "Es wurde keine Objekt-ID angegeben.");
        if (content is null)
        {
            throw new CmisInvalidArgumentException("Es wurde kein Inhalt angegeben.");
        }

        return _executor.RunAsync(() =>
        {
            var session = _sessionAccessor.RequireSession();
            if (session.GetObject(session.CreateObjectId(objectId)) is not IDocument doc)
            {
                throw new CmisInvalidArgumentException(
                    "Das Objekt ist kein Dokument; ein Inhalt kann nur für Dokumente gesetzt werden.");
            }

            var contentStream = new ContentStream
            {
                Stream = content,
                FileName = fileName,
                MimeType = string.IsNullOrWhiteSpace(mimeType) ? "application/octet-stream" : mimeType
            };
            doc.SetContentStream(contentStream, overwrite);
        }, ct);
    }

    public Task<IReadOnlyList<AclEntryDto>> GetAclAsync(string objectId, CancellationToken ct = default)
    {
        RequireId(objectId, "Es wurde keine Objekt-ID angegeben.");

        return _executor.RunAsync<IReadOnlyList<AclEntryDto>>(() =>
        {
            var session = _sessionAccessor.RequireSession();
            var acl = session.GetAcl(session.CreateObjectId(objectId), false);
            return acl?.Aces?.Select(CmisModelMapper.ToDto).ToList() ?? new List<AclEntryDto>();
        }, ct);
    }

    public Task<IReadOnlyList<ObjectVersionDto>> GetAllVersionsAsync(string objectId, CancellationToken ct = default)
    {
        RequireId(objectId, "Es wurde keine Objekt-ID angegeben.");

        return _executor.RunAsync<IReadOnlyList<ObjectVersionDto>>(() =>
        {
            var session = _sessionAccessor.RequireSession();
            if (session.GetObject(session.CreateObjectId(objectId)) is not IDocument doc)
            {
                // Nicht-Dokumente (z. B. Ordner) haben keine Versionsreihe.
                return new List<ObjectVersionDto>();
            }

            return doc.GetAllVersions().Select(CmisModelMapper.ToVersionDto).ToList();
        }, ct);
    }

    private static void RequireId(string id, string message)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new CmisInvalidArgumentException(message);
        }
    }

    private static void RequireProperties(IDictionary<string, object?>? properties)
    {
        if (properties is null || properties.Count == 0)
        {
            throw new CmisInvalidArgumentException("Es wurden keine Properties angegeben.");
        }
    }

    /// <summary>
    /// Übernimmt die Properties 1:1 (inkl. <c>null</c>-Werten) in ein PortCMIS-Dictionary.
    /// <c>null</c> ist bei <c>UpdateProperties</c> bewusst zulässig: es löscht die Property
    /// serverseitig (z. B. wenn im Bearbeiten-Dialog ein Feld geleert wurde, FA-72).
    /// </summary>
    private static IDictionary<string, object> ToPortCmis(IDictionary<string, object?> properties) =>
        properties.ToDictionary(kv => kv.Key, kv => kv.Value!, System.StringComparer.Ordinal);
}
