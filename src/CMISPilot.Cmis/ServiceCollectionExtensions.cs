using Microsoft.Extensions.DependencyInjection;

namespace CMISPilot.Cmis;

/// <summary>
/// Registriert die Dienste der CMIS-Library im DI-Container.
/// Jede Feature-Vertikale ergaenzt ihre Registrierung in einer eigenen
/// partiellen Datei (siehe Umsetzungsplan, Regeln fuer parallele Agents),
/// damit mehrere Agents nicht dieselbe Datei anfassen.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddCmis(this IServiceCollection services)
    {
        // M0: noch keine Dienste. Feature-Registrierungen folgen ab M1/M3
        // ueber partielle AddCmis*-Methoden. Jede Vertikale bekommt einen eigenen
        // Hook (eine Zeile hier), implementiert in ihrer eigenen Teil-Datei –
        // so kollidieren parallele Agents nicht in derselben Service-Liste.
        AddCmisServices(services);
        AddCmisTypeServices(services);
        AddBrowseServices(services);
        AddQueryServices(services);
        AddObjectServices(services);
        AddDiagnosticsServices(services);
        AddProfileServices(services);
        AddRepositoryInfoServices(services);
        return services;
    }

    /// <summary>
    /// Hook fuer feature-spezifische Registrierungen (partial, je Vertikale
    /// eine eigene Teil-Implementierung).
    /// </summary>
    static partial void AddCmisServices(IServiceCollection services);

    /// <summary>Hook der M5-Vertikale „Typen" (Teil-Datei ServiceCollectionExtensions.Types.cs).</summary>
    static partial void AddCmisTypeServices(IServiceCollection services);

    /// <summary>Hook der Vertikale „Explorer" (M4, Browse-Dienste).</summary>
    static partial void AddBrowseServices(IServiceCollection services);

    /// <summary>Hook der M6-Vertikale „Query" (Teil-Datei ServiceCollectionExtensions.Query.cs).</summary>
    static partial void AddQueryServices(IServiceCollection services);

    /// <summary>Hook der Vertikale „CRUD" (M7, schreibende Object-Dienste).</summary>
    static partial void AddObjectServices(IServiceCollection services);

    /// <summary>Hook der M9-Vertikale „Diagnose" (Teil-Datei ServiceCollectionExtensions.Diagnostics.cs).</summary>
    static partial void AddDiagnosticsServices(IServiceCollection services);

    /// <summary>Hook der M10-Vertikale „Profile" (Teil-Datei ServiceCollectionExtensions.Profiles.cs).</summary>
    static partial void AddProfileServices(IServiceCollection services);

    /// <summary>Hook der Vertikale „Repository-Info" (Teil-Datei ServiceCollectionExtensions.RepositoryInfo.cs).</summary>
    static partial void AddRepositoryInfoServices(IServiceCollection services);
}
