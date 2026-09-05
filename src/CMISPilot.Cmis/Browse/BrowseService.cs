using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CMISPilot.Cmis.Connection;
using CMISPilot.Cmis.Contracts;
using CMISPilot.Cmis.Errors;
using CMISPilot.Cmis.Execution;
using CMISPilot.Cmis.Models;
using PortCMIS.Client;

namespace CMISPilot.Cmis.Browse;

/// <summary>
/// Navigation im Repository (FA-20/21/22): Wurzelordner, Objekte, Kinder und
/// Elternordner laden. Bezieht die aktive PortCMIS-Session library-intern über den
/// <see cref="ICmisSessionAccessor"/> und führt alle blockierenden PortCMIS-Aufrufe
/// über den <see cref="ICmisExecutor"/> aus (NFA-05/13). Nach außen nur DTOs
/// (NFA-03a); Mapping zentral über <see cref="CmisModelMapper"/>.
/// </summary>
internal sealed class BrowseService : IBrowseService
{
    private readonly ICmisExecutor _executor;
    private readonly ICmisSessionAccessor _sessionAccessor;

    public BrowseService(ICmisExecutor executor, ICmisSessionAccessor sessionAccessor)
    {
        _executor = executor;
        _sessionAccessor = sessionAccessor;
    }

    public Task<CmisObjectDto> GetRootFolderAsync(CancellationToken ct = default) =>
        _executor.RunAsync(() =>
        {
            var session = _sessionAccessor.RequireSession();
            return CmisModelMapper.ToDto(session.GetRootFolder());
        }, ct);

    public Task<CmisObjectDto> GetObjectAsync(string objectId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            throw new CmisInvalidArgumentException("Es wurde keine Objekt-ID angegeben.");
        }

        return _executor.RunAsync(() =>
        {
            var session = _sessionAccessor.RequireSession();
            return CmisModelMapper.ToDto(session.GetObject(session.CreateObjectId(objectId)));
        }, ct);
    }

    public Task<CmisObjectDto> GetObjectByPathAsync(string path, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new CmisInvalidArgumentException("Es wurde kein Pfad angegeben.");
        }

        return _executor.RunAsync(() =>
        {
            var session = _sessionAccessor.RequireSession();
            return CmisModelMapper.ToDto(session.GetObjectByPath(path));
        }, ct);
    }

    public Task<IReadOnlyList<CmisObjectDto>> GetChildrenAsync(string folderId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(folderId))
        {
            throw new CmisInvalidArgumentException("Es wurde keine Ordner-ID angegeben.");
        }

        return _executor.RunAsync<IReadOnlyList<CmisObjectDto>>(() =>
        {
            var session = _sessionAccessor.RequireSession();
            if (session.GetObject(session.CreateObjectId(folderId)) is not IFolder folder)
            {
                throw new CmisInvalidArgumentException(
                    "Das Objekt ist kein Ordner; Kinder können nur für Ordner geladen werden.");
            }

            return folder.GetChildren()
                .Select(child => CmisModelMapper.ToDto(child))
                .ToList();
        }, ct);
    }

    public Task<IReadOnlyList<CmisObjectDto>> GetParentsAsync(string objectId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            throw new CmisInvalidArgumentException("Es wurde keine Objekt-ID angegeben.");
        }

        return _executor.RunAsync<IReadOnlyList<CmisObjectDto>>(() =>
        {
            var session = _sessionAccessor.RequireSession();
            var obj = session.GetObject(session.CreateObjectId(objectId));

            // Nur ablegbare Objekte (Ordner/Dokumente) haben Elternordner; der
            // Wurzelordner liefert eine leere Liste (siehe PortCMIS Parents-Vertrag).
            if (obj is not IFileableCmisObject fileable)
            {
                return new List<CmisObjectDto>();
            }

            return fileable.Parents
                .Select(parent => CmisModelMapper.ToDto(parent))
                .ToList();
        }, ct);
    }
}
