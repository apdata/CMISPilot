using CMISPilot.Cmis.Contracts;
using CMISPilot.Cmis.Types;
using Microsoft.Extensions.DependencyInjection;

namespace CMISPilot.Cmis;

/// <summary>
/// M5-Registrierungen der CMIS-Library (Vertikale „Typen-Browser"). Eigene
/// partielle Teil-Datei, damit parallele Feature-Agents nicht dieselbe
/// Registrierungsdatei anfassen (Umsetzungsplan §3).
/// </summary>
public static partial class ServiceCollectionExtensions
{
    static partial void AddCmisTypeServices(IServiceCollection services)
    {
        services.AddSingleton<ITypeService, TypeService>();
    }
}
