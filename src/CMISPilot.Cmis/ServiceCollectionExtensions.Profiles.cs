using CMISPilot.Cmis.Profiles;
using Microsoft.Extensions.DependencyInjection;

namespace CMISPilot.Cmis;

/// <summary>
/// M10-Registrierung der Profil-Persistenz (T10.1). Eigene Teil-Datei nach
/// dem etablierten Muster. Die konkrete <see cref="ISecretProtector"/>-Instanz
/// (Windows-DPAPI) wird in der App-Schicht registriert; hier wird sie nur
/// per Konstruktor-Injection benötigt (lazy aufgelöst beim ersten Zugriff).
/// </summary>
public static partial class ServiceCollectionExtensions
{
    static partial void AddProfileServices(IServiceCollection services)
    {
        services.AddSingleton<IProfileStore>(sp =>
            new JsonProfileStore(
                JsonProfileStore.GetDefaultFilePath(),
                sp.GetRequiredService<ISecretProtector>()));
    }
}
