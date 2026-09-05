using System.Diagnostics;
using System.Reflection;
using CMISPilot.Plugins;

namespace CMISPilot.Desktop.Plugins;

/// <summary>
/// Ordnet eine Ausnahme best effort einem geladenen Plugin zu (Plan P2.6a). Als
/// reine, statische Funktion aus <see cref="PluginLoader"/> herausgezogen, damit
/// sie ohne echtes Laden von Assemblies von der Festplatte testbar ist.
///
/// <para>Kein Beweis: eine Ausnahme kann auch entstehen, weil der Host ein Plugin
/// falsch aufruft. Zwei Wege, weil einer allein nachweislich nicht reicht:</para>
///
/// <para><b>1. Stapelrahmen.</b> Trifft, wenn tatsächlich Code des Plugins auf dem
/// Stapel steht (z. B. eine Ausnahme aus einem Command-Handler des
/// Plugin-ViewModels).</para>
///
/// <para><b>2. Typname im Ausnahmetext.</b> Nötig für den Fall, der P2.6a
/// überhaupt ausgelöst hat: ein <see cref="System.Windows.Markup.XamlParseException"/>
/// aus einer fehlerhaften Bindung im <c>DataTemplate</c> eines Plugins wirft
/// <b>ausschließlich innerhalb von WPF-Interna</b> (Binding-Engine, Ribbon) — kein
/// einziger Stapelrahmen gehört dem Plugin. Der Typname der Ziel-Eigenschaft steht
/// aber im Ausnahmetext
/// (<c>"…schreibgeschützten Eigenschaft \"ClickCount\" vom Typ \"SpikePlugin.SpikeDocumentViewModel\"."</c>),
/// deshalb wird der Text jedes geladenen Plugin-Typnamens dagegen geprüft. Ohne
/// diesen zweiten Weg identifiziert P2.6a genau den Fehler nicht, der es
/// veranlasst hat — real durchprobiert.</para>
/// </summary>
public static class PluginSourceIdentifier
{
    /// <summary>
    /// Ordnet ein Objekt (z. B. das aktive Dokument beim Absturz) best effort einem
    /// Plugin zu, anhand der Assembly seines Laufzeittyps. Grundlage für P2.6b: das
    /// betroffene Dokument-Tab identifizieren, um es zu schließen.
    /// </summary>
    public static IPlugin? TryIdentifyOwner(
        object instance, IReadOnlyDictionary<IPlugin, Assembly> pluginAssemblies) =>
        TryFindByAssembly(instance.GetType().Assembly, pluginAssemblies);

    public static IPlugin? TryIdentify(
        Exception exception, IReadOnlyDictionary<IPlugin, Assembly> pluginAssemblies)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            foreach (var frame in new StackTrace(current, fNeedFileInfo: false).GetFrames())
            {
                var assembly = frame.GetMethod()?.DeclaringType?.Assembly;
                if (assembly is null)
                {
                    continue;
                }

                if (TryFindByAssembly(assembly, pluginAssemblies) is { } plugin)
                {
                    return plugin;
                }
            }
        }

        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (string.IsNullOrEmpty(current.Message))
            {
                continue;
            }

            if (TryFindByMessage(current.Message, pluginAssemblies) is { } plugin)
            {
                return plugin;
            }
        }

        return null;
    }

    private static IPlugin? TryFindByAssembly(
        Assembly assembly, IReadOnlyDictionary<IPlugin, Assembly> pluginAssemblies)
    {
        foreach (var (plugin, pluginAssembly) in pluginAssemblies)
        {
            if (pluginAssembly == assembly)
            {
                return plugin;
            }
        }

        return null;
    }

    private static IPlugin? TryFindByMessage(
        string message, IReadOnlyDictionary<IPlugin, Assembly> pluginAssemblies)
    {
        foreach (var (plugin, assembly) in pluginAssemblies)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t is not null).ToArray()!;
            }

            foreach (var type in types)
            {
                if (type.FullName is { Length: > 0 } fullName &&
                    message.Contains(fullName, StringComparison.Ordinal))
                {
                    return plugin;
                }
            }
        }

        return null;
    }
}
