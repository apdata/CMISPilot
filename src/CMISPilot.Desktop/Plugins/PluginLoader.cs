using System.IO;
using System.Reflection;
using CMISPilot.Plugins;

namespace CMISPilot.Desktop.Plugins;

/// <summary>
/// Findet und laedt Plugins aus dem Verzeichnis <c>Plugins/</c> neben der Anwendung.
///
/// <para><b>Ladeverfahren</b> (Plan §3.1, Variante B): Die Assemblies wandern in den
/// Standard-<c>AssemblyLoadContext</c>, nicht in einen isolierten. Nur so loesen
/// WPF-<c>pack://</c>-URIs, XAML-Typverweise und Theming-Ressourcen aus einem Plugin
/// heraus zuverlaessig auf.</para>
///
/// <para><b>Fehlertoleranz:</b> Ein kaputtes Plugin darf den Start nie verhindern. Alles
/// wird protokolliert und uebersprungen; die Anwendung laeuft dann ohne dieses Plugin
/// weiter. Das ist zugleich die Grundlage dafuer, dass CMISPilot voellig ohne Plugins
/// auslieferbar bleibt (Plan §1.1).</para>
///
/// <para>Laeuft <b>vor</b> dem Aufbau des DI-Containers und kann deshalb noch keinen
/// <c>ILogger</c> benutzen. Meldungen werden gesammelt und vom Aufrufer nach dem
/// Hochfahren des Logs ausgegeben (<see cref="Messages"/>).</para>
/// </summary>
public sealed class PluginLoader
{
    private readonly List<string> _messages = new();

    /// <summary>
    /// Herkunfts-Assembly je geladenem Plugin — Grundlage von
    /// <see cref="TryIdentifySource"/> (Plan P2.6a).
    /// </summary>
    private readonly Dictionary<IPlugin, Assembly> _pluginAssemblies = new();

    /// <summary>Die geladenen, vertragskonformen Plugins.</summary>
    public IReadOnlyList<IPlugin> Plugins { get; private set; } = Array.Empty<IPlugin>();

    /// <summary>
    /// Was beim Laden passiert ist — nach dem Hochfahren des Logs auszugeben.
    /// Enthaelt auch die Gruende fuer uebersprungene Plugins.
    /// </summary>
    public IReadOnlyList<string> Messages => _messages;

    /// <summary>Standardverzeichnis: <c>Plugins/</c> neben der Anwendung.</summary>
    public static string GetDefaultDirectory() =>
        Path.Combine(AppContext.BaseDirectory, "Plugins");

    /// <summary>
    /// Nimmt eine Meldung auf, die erst spaeter ins Log kann. Fuer Fehler, die nach
    /// dem Laden, aber vor dem Hochfahren des Logs auftreten (z. B. in
    /// <see cref="IPlugin.ConfigureServices"/>).
    /// </summary>
    public void AddMessage(string message) => _messages.Add(message);

    /// <summary>
    /// Versucht, eine Ausnahme einem geladenen Plugin zuzuordnen (Plan P2.6a).
    /// Die eigentliche Logik steht als reine, testbare Funktion in
    /// <see cref="PluginSourceIdentifier"/>.
    /// </summary>
    public IPlugin? TryIdentifySource(Exception exception) =>
        PluginSourceIdentifier.TryIdentify(exception, _pluginAssemblies);

    /// <summary>
    /// Ordnet ein Objekt (P2.6b: das aktive Dokument beim Absturz) best effort einem
    /// geladenen Plugin zu, anhand der Assembly seines Laufzeittyps.
    /// </summary>
    public IPlugin? TryIdentifyOwner(object instance) =>
        PluginSourceIdentifier.TryIdentifyOwner(instance, _pluginAssemblies);

    /// <summary>
    /// Durchsucht das Verzeichnis nach Plugins. Fehlt es oder ist es leer, ist das der
    /// Normalfall einer Auslieferung ohne Plugins — kein Fehler.
    /// </summary>
    public void Load(string directory)
    {
        if (!Directory.Exists(directory))
        {
            _messages.Add($"Kein Plugin-Verzeichnis unter \"{directory}\" — starte ohne Plugins.");
            return;
        }

        var found = new List<IPlugin>();

        foreach (var file in Directory.EnumerateFiles(directory, "*.dll", SearchOption.AllDirectories))
        {
            foreach (var plugin in LoadFromFile(file))
            {
                if (found.Any(p => string.Equals(p.Id, plugin.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    _messages.Add($"Plugin \"{plugin.Id}\" liegt mehrfach vor — \"{file}\" uebersprungen.");
                    continue;
                }

                found.Add(plugin);
                _messages.Add($"Plugin geladen: {plugin.Name} {plugin.Version} (Id {plugin.Id}).");
            }
        }

        Plugins = found;
    }

    private IEnumerable<IPlugin> LoadFromFile(string file)
    {
        Assembly assembly;
        try
        {
            assembly = Assembly.LoadFrom(file);
        }
        catch (BadImageFormatException)
        {
            // Keine .NET-Assembly (z. B. native Begleit-DLL) — still uebergehen.
            yield break;
        }
        catch (Exception ex)
        {
            _messages.Add($"\"{file}\" konnte nicht geladen werden: {ex.Message}");
            yield break;
        }

        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // Haeufigster Fall einer veralteten Plugin-DLL: eine Abhaengigkeit passt
            // nicht mehr. Mit Klartext melden statt roh durchschlagen zu lassen.
            _messages.Add(
                $"Typen aus \"{Path.GetFileName(file)}\" nicht ladbar (vermutlich gegen eine " +
                $"aeltere Fassung gebaut): {ex.LoaderExceptions.FirstOrDefault()?.Message ?? ex.Message}");
            yield break;
        }

        foreach (var type in types)
        {
            if (!typeof(IPlugin).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface)
            {
                continue;
            }

            IPlugin? plugin = null;
            try
            {
                plugin = (IPlugin?)Activator.CreateInstance(type);
            }
            catch (Exception ex)
            {
                _messages.Add($"Plugin \"{type.FullName}\" liess sich nicht erzeugen: {ex.Message}");
            }

            if (plugin is null)
            {
                continue;
            }

            // Einzige Kompatibilitaetspruefung: die Hauptversion des Vertrags. Faengt
            // die im Plugins-Verzeichnis vergessene alte DLL mit klarer Meldung ab.
            if (plugin.ContractVersion.Major != PluginContract.Version.Major)
            {
                _messages.Add(
                    $"Plugin \"{plugin.Id}\" wurde gegen Vertrag {plugin.ContractVersion} gebaut, " +
                    $"diese Fassung von CMISPilot erwartet {PluginContract.Version.Major}.x — " +
                    "uebersprungen. Plugin neu bauen.");
                continue;
            }

            _pluginAssemblies[plugin] = assembly;
            yield return plugin;
        }
    }
}
