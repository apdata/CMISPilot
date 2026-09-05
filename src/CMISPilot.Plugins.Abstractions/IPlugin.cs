using Microsoft.Extensions.DependencyInjection;

namespace CMISPilot.Plugins;

/// <summary>
/// Einstiegspunkt eines CMISPilot-Plugins. Der Loader sucht in den Assemblies unter
/// <c>Plugins/</c> nach Implementierungen dieser Schnittstelle, prueft
/// <see cref="ContractVersion"/> und bindet den Rest ueber die Beitragspunkte ein.
///
/// <para><b>Zweck der Schnittstelle</b> (Plan §1.1): bestimmte Funktionen sollen
/// nicht im Standardprogramm liegen. Es geht ausdruecklich <i>nicht</i> um ein
/// Plugin-Oekosystem fuer Dritte — alle Plugins werden zusammen mit dem Host
/// gebaut. Deshalb bleibt dieser Vertrag bewusst klein.</para>
/// </summary>
public interface IPlugin
{
    /// <summary>
    /// Stabile, technische Kennung (z. B. <c>apdata.indexeditor</c>). Dient dem Log
    /// und dem Erkennen doppelt abgelegter Plugins.
    /// </summary>
    string Id { get; }

    /// <summary>Anzeigename fuer Log und Diagnose.</summary>
    string Name { get; }

    /// <summary>
    /// Eigene Version des Plugins. Nur fuer Anzeige und Log — sie entscheidet
    /// <b>nicht</b> ueber Kompatibilitaet.
    /// </summary>
    Version Version { get; }

    /// <summary>
    /// Version von <c>CMISPilot.Plugins.Abstractions</c>, gegen die dieses Plugin
    /// gebaut wurde. Das Einzige, was der Loader prueft: stimmt die Hauptversion
    /// nicht mit <see cref="PluginContract.Version"/> ueberein, wird das Plugin mit
    /// klarer Meldung abgelehnt.
    ///
    /// <para>Der Sinn ist allein, eine im <c>Plugins/</c>-Verzeichnis vergessene
    /// alte DLL verstaendlich abzuweisen, statt sie mit einer
    /// <c>MissingMethodException</c> auflaufen zu lassen. Ueblicherweise setzt eine
    /// Implementierung schlicht <c>PluginContract.Version</c>.</para>
    /// </summary>
    Version ContractVersion { get; }

    /// <summary>
    /// Registriert die Dienste des Plugins im DI-Container des Hosts (u. a. die
    /// ViewModels der beigesteuerten Dokument-Tabs). Wird beim Aufbau des Hosts
    /// aufgerufen.
    /// </summary>
    void ConfigureServices(IServiceCollection services);

    /// <summary>
    /// Was das Plugin zur Oberflaeche beitraegt. Wird nach dem Aufbau des Containers
    /// abgefragt; <paramref name="services"/> ist der fertige Anbieter, damit die
    /// Beitraege ihre ViewModels aufloesen koennen.
    /// </summary>
    PluginContributions GetContributions(IServiceProvider services);
}
