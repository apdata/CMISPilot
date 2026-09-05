using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CMISPilot.Cmis.Connection;
using CMISPilot.Cmis.Contracts;
using CMISPilot.Cmis.Execution;
using CMISPilot.Cmis.Models;
using PortCMIS.Client;

namespace CMISPilot.Cmis.Query;

/// <summary>
/// Führt CMISQL-Abfragen gegen das aktive Repository aus (FA-50/51). Alle
/// blockierenden PortCMIS-Aufrufe laufen über den <see cref="ICmisExecutor"/>
/// (NFA-05/13, Fehler-Mapping zentral). Die aktive PortCMIS-Session kommt
/// library-intern über den <see cref="ICmisSessionAccessor"/>; nach außen wird
/// ausschließlich ein DTO geliefert (NFA-03a).
/// </summary>
internal sealed class QueryService : IQueryService
{
    private readonly ICmisExecutor _executor;
    private readonly ICmisSessionAccessor _sessionAccessor;

    public QueryService(ICmisExecutor executor, ICmisSessionAccessor sessionAccessor)
    {
        _executor = executor;
        _sessionAccessor = sessionAccessor;
    }

    /// <summary>
    /// Führt die übergebene CMISQL-Abfrage aus (FA-50) und liefert das Ergebnis
    /// als Tabelle mit dynamischen Spalten (FA-51). Nutzt <c>session.Query(...)</c>.
    /// </summary>
    public Task<QueryResultDto> QueryAsync(
        string cmisql, bool searchAllVersions = false, CancellationToken ct = default)
    {
        return _executor.RunAsync(() =>
        {
            ISession session = _sessionAccessor.RequireSession();
            IItemEnumerable<IQueryResult> results = session.Query(cmisql, searchAllVersions);
            return CmisModelMapper.ToQueryResult(results.ToList());
        }, ct);
    }
}
