using System.Threading;
using System.Threading.Tasks;
using CMISPilot.Cmis.Contracts;
using CMISPilot.Cmis.Execution;
using CMISPilot.Cmis.Models;

namespace CMISPilot.Cmis.Connection;

/// <summary>
/// Liefert die Repository-Information der aktiven Sitzung (FA-10/FA-11).
///
/// <para>Bezieht die Sitzung ueber den library-internen
/// <see cref="ICmisSessionAccessor"/>, damit PortCMIS-Typen die Library nicht
/// verlassen (NFA-03a). Beide Zugriffe laufen ueber den <see cref="ICmisExecutor"/>:
/// PortCMIS liest <c>RepositoryInfo</c> zwar aus der bestehenden Sitzung, aber die
/// Zuordnung ist blockierender Fremdcode und gehoert damit nicht auf den UI-Thread
/// (NFA-13).</para>
/// </summary>
internal sealed class RepositoryInfoService : IRepositoryInfoService
{
    private readonly ICmisExecutor _executor;
    private readonly ICmisSessionAccessor _sessionAccessor;

    public RepositoryInfoService(ICmisExecutor executor, ICmisSessionAccessor sessionAccessor)
    {
        _executor = executor;
        _sessionAccessor = sessionAccessor;
    }

    public Task<RepositoryInfoDto> GetRepositoryInfoAsync(CancellationToken ct = default) =>
        _executor.RunAsync(() => CmisModelMapper.ToDto(_sessionAccessor.RequireSession().RepositoryInfo), ct);

    public Task<string> GetRepositoryInfoJsonAsync(CancellationToken ct = default) =>
        _executor.RunAsync(() => RepositoryInfoJsonWriter.Write(_sessionAccessor.RequireSession().RepositoryInfo), ct);
}
