using CMISPilot.Cmis.Contracts;
using CMISPilot.Cmis.Objects;
using Microsoft.Extensions.DependencyInjection;

namespace CMISPilot.Cmis;

/// <summary>
/// M7-Registrierungen der CMIS-Library (Vertikale „CRUD"). Eigene partielle
/// Teil-Datei, damit parallele Feature-Vertikalen (M4–M8) nicht dieselbe
/// Registrierungsdatei anfassen (Umsetzungsplan §3).
/// </summary>
public static partial class ServiceCollectionExtensions
{
    static partial void AddObjectServices(IServiceCollection services)
    {
        // Factory-Registrierung, weil ObjectService den library-internen
        // ICmisSessionAccessor bezieht (bleibt internal, NFA-03a).
        services.AddSingleton<IObjectService>(sp => new ObjectService(
            sp.GetRequiredService<Execution.ICmisExecutor>(),
            sp.GetRequiredService<Connection.ICmisSessionAccessor>()));
    }
}
