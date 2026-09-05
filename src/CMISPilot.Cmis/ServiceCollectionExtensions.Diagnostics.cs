using CMISPilot.Cmis.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CMISPilot.Cmis;

/// <summary>
/// M9-Registrierungen der CMIS-Library (Vertikale „Diagnose & Robustheit").
/// Eigene partielle Teil-Datei, damit parallele Feature-Agents nicht dieselbe
/// Registrierungsdatei anfassen (Umsetzungsplan §3).
/// </summary>
public static partial class ServiceCollectionExtensions
{
    static partial void AddDiagnosticsServices(IServiceCollection services)
    {
        services.AddSingleton<IDiagnosticsLog>(_ =>
        {
            var log = new InMemoryDiagnosticsLog();
            // T9.1: macht den Log fuer LoggingHttpInvoker erreichbar, den PortCMIS
            // selbst per Activator.CreateInstance() instanziiert (kein DI-Hook
            // moeglich, siehe LoggingHttpInvoker-Doku).
            DiagnosticsLogAmbient.SetCurrent(log);
            return log;
        });
    }
}
