namespace CMISPilot.Plugins;

/// <summary>
/// Version des Plugin-Vertrags. Ein Plugin gibt sie als
/// <see cref="IPlugin.ContractVersion"/> zurueck; der Loader vergleicht die
/// Hauptversion.
/// </summary>
public static class PluginContract
{
    /// <summary>
    /// Aktuelle Vertragsversion. <b>Hauptversion erhoehen</b>, sobald sich an
    /// <see cref="IPlugin"/> oder den Beitragstypen etwas aendert, das bestehende
    /// Plugins brechen wuerde — dann weist der Loader alte Plugin-DLLs mit klarer
    /// Meldung ab, statt sie halb zu laden.
    ///
    /// <para>Bewusst unabhaengig von der Version der Anwendung CMISPilot: die
    /// aendert sich laufend aus Gruenden, die kein Plugin beruehren. Vertrag 1.0 in
    /// CMISPilot 2.4 ist der Normalfall.</para>
    /// </summary>
    public static Version Version { get; } = new(1, 0);
}
