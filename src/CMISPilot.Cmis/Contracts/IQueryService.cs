using System.Threading;
using System.Threading.Tasks;
using CMISPilot.Cmis.Models;

namespace CMISPilot.Cmis.Contracts;

/// <summary>
/// Führt CMISQL-Abfragen aus (FA-50/51). Setzt eine aktive Session voraus.
/// </summary>
public interface IQueryService
{
    /// <summary>
    /// Führt eine CMISQL-Abfrage aus und liefert das Ergebnis als Tabelle.
    /// </summary>
    /// <param name="cmisql">Die CMISQL-Abfrage (SELECT ...).</param>
    /// <param name="searchAllVersions">Wenn true, werden alle Versionen durchsucht.</param>
    Task<QueryResultDto> QueryAsync(
        string cmisql, bool searchAllVersions = false, CancellationToken ct = default);
}
