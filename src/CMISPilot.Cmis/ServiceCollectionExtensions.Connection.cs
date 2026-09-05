using CMISPilot.Cmis.Connection;
using CMISPilot.Cmis.Contracts;
using CMISPilot.Cmis.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace CMISPilot.Cmis;

/// <summary>
/// M1-Registrierungen der CMIS-Library (Connection-Fundament). Eigene partielle
/// Teil-Datei, damit spätere Vertikalen (M4–M8) ihre Dienste in eigenen Dateien
/// ergänzen, ohne diese hier anzufassen.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    static partial void AddCmisServices(IServiceCollection services)
    {
        // Async-Grundlage (T1.1)
        services.AddSingleton<ICmisExecutor, CmisExecutor>();

        // Verbindungszustand als Singleton; beide Sichten (öffentlich/DTO + intern/Session)
        // teilen sich dieselbe Instanz.
        services.AddSingleton<SessionContext>();
        services.AddSingleton<ISessionContext>(sp => sp.GetRequiredService<SessionContext>());
        services.AddSingleton<ICmisSessionAccessor>(sp => sp.GetRequiredService<SessionContext>());

        // Verbindungsdienst (T1.4)
        services.AddSingleton<IConnectionService, ConnectionService>();
    }
}
