using CMISPilot.Cmis.Connection;
using CMISPilot.Cmis.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace CMISPilot.Cmis;

/// <summary>
/// Registrierungen der Vertikale „Repository-Info" (FA-10/FA-11). Eigene partielle
/// Teil-Datei nach dem Muster der uebrigen Vertikalen, damit die Aggregator-Datei
/// nur eine Hook-Zeile bekommt.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    static partial void AddRepositoryInfoServices(IServiceCollection services)
    {
        services.AddSingleton<IRepositoryInfoService, RepositoryInfoService>();
    }
}
