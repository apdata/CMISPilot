using CMISPilot.Cmis.Browse;
using CMISPilot.Cmis.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace CMISPilot.Cmis;

/// <summary>
/// M4-Registrierungen der CMIS-Library (Vertikale „Explorer"). Eigene partielle
/// Teil-Datei, damit parallele Feature-Vertikalen (M5–M8) nicht dieselbe
/// Registrierungsdatei anfassen (Umsetzungsplan §3).
/// </summary>
public static partial class ServiceCollectionExtensions
{
    static partial void AddBrowseServices(IServiceCollection services)
    {
        // Factory-Registrierung, weil BrowseService den library-internen
        // ICmisSessionAccessor bezieht (bleibt internal, NFA-03a).
        services.AddSingleton<IBrowseService>(sp => new BrowseService(
            sp.GetRequiredService<Execution.ICmisExecutor>(),
            sp.GetRequiredService<Connection.ICmisSessionAccessor>()));
    }
}
