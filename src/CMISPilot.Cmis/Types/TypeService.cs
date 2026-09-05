using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CMISPilot.Cmis.Connection;
using CMISPilot.Cmis.Contracts;
using CMISPilot.Cmis.Execution;
using CMISPilot.Cmis.Models;
using PortCMIS.Client;

namespace CMISPilot.Cmis.Types;

/// <summary>
/// Lädt CMIS-Typdefinitionen des aktiven Repositories (FA-60/61/62). Alle
/// blockierenden PortCMIS-Aufrufe laufen über den <see cref="ICmisExecutor"/>
/// (NFA-05/13, Fehler-Mapping zentral). Die aktive PortCMIS-Session kommt
/// library-intern über den <see cref="ICmisSessionAccessor"/>; nach außen werden
/// ausschließlich DTOs geliefert (NFA-03a).
/// </summary>
internal sealed class TypeService : ITypeService
{
    private readonly ICmisExecutor _executor;
    private readonly ICmisSessionAccessor _sessionAccessor;

    public TypeService(ICmisExecutor executor, ICmisSessionAccessor sessionAccessor)
    {
        _executor = executor;
        _sessionAccessor = sessionAccessor;
    }

    /// <summary>
    /// Lädt den gesamten Typbaum ab den Basistypen (<c>cmis:document</c>,
    /// <c>cmis:folder</c>, <c>cmis:relationship</c>, <c>cmis:policy</c>,
    /// <c>cmis:item</c>) inkl. aller abgeleiteten Typen als verschachtelte DTOs
    /// (FA-60). Nutzt <c>GetTypeDescendants(null, -1, ...)</c>.
    /// </summary>
    public Task<IReadOnlyList<TypeDefinitionDto>> GetTypeTreeAsync(
        bool includePropertyDefinitions = true, CancellationToken ct = default)
    {
        return _executor.RunAsync<IReadOnlyList<TypeDefinitionDto>>(() =>
        {
            ISession session = _sessionAccessor.RequireSession();
            IList<ITree<IObjectType>> trees =
                session.GetTypeDescendants(null, -1, includePropertyDefinitions);
            return trees.Select(CmisModelMapper.ToDtoTree).ToList();
        }, ct);
    }

    /// <summary>
    /// Lädt die direkten Untertypen eines Typs, oder – bei <paramref name="typeId"/>
    /// null – die Basistypen.
    /// </summary>
    public Task<IReadOnlyList<TypeDefinitionDto>> GetTypeChildrenAsync(
        string? typeId, bool includePropertyDefinitions = true, CancellationToken ct = default)
    {
        return _executor.RunAsync<IReadOnlyList<TypeDefinitionDto>>(() =>
        {
            ISession session = _sessionAccessor.RequireSession();
            IItemEnumerable<IObjectType> children =
                session.GetTypeChildren(typeId, includePropertyDefinitions);
            return children.Select(t => CmisModelMapper.ToDto(t)).ToList();
        }, ct);
    }

    /// <summary>Lädt eine einzelne Typdefinition inkl. Property-Definitionen (FA-61/62).</summary>
    public Task<TypeDefinitionDto> GetTypeDefinitionAsync(string typeId, CancellationToken ct = default)
    {
        return _executor.RunAsync(() =>
        {
            ISession session = _sessionAccessor.RequireSession();
            IObjectType type = session.GetTypeDefinition(typeId);
            return CmisModelMapper.ToDto(type);
        }, ct);
    }
}
