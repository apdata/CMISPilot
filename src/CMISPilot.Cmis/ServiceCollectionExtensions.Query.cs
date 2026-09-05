using CMISPilot.Cmis.Contracts;
using CMISPilot.Cmis.Query;
using Microsoft.Extensions.DependencyInjection;

namespace CMISPilot.Cmis;

/// <summary>
/// M6-Registrierungen der CMIS-Library (Vertikale „Query-Konsole"). Eigene
/// partielle Teil-Datei, damit parallele Feature-Agents nicht dieselbe
/// Registrierungsdatei anfassen (Umsetzungsplan §3).
/// </summary>
public static partial class ServiceCollectionExtensions
{
    static partial void AddQueryServices(IServiceCollection services)
    {
        services.AddSingleton<IQueryService, QueryService>();
    }
}
