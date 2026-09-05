using System.Windows.Media;
using APX.Wpf.Shell.ViewModels.Workspace;

namespace CMISPilot.Plugins;

/// <summary>
/// Was ein Plugin zur Oberflaeche beitraegt. Bewusst knapp gehalten — es kommt nur
/// hinein, was der erste echte Anwendungsfall (Index-Editor) braucht.
/// </summary>
public sealed class PluginContributions
{
    /// <summary>
    /// Ressourcen-Woerterbuch, das der Host beim Start in
    /// <c>Application.Resources</c> mischt — vor allem die <c>DataTemplate</c>s der
    /// beigesteuerten Dokument-Tabs. Als <c>pack://application:,,,/Assembly;component/Pfad.xaml</c>
    /// anzugeben, oder <c>null</c> fuer ein Plugin ohne eigene Ressourcen.
    ///
    /// <para>Bewusst ein einzelner Eintrag statt einer Liste (P1.4): bisher braucht
    /// jedes Plugin genau eines. Ein zweites (z. B. für separat gepflegte Icons)
    /// laesst sich als eigenes <c>ResourceDictionary</c> mit
    /// <c>ResourceDictionary.MergedDictionaries</c> im ersten buendeln — dafuer
    /// braucht der Vertrag keine eigene Liste.</para>
    /// </summary>
    public Uri? ResourceDictionary { get; init; }

    /// <summary>Kontextbezogene Ribbon-Tabs (erscheinen nur beim passenden Dokument-Tab).</summary>
    public IReadOnlyList<RibbonTabContribution> RibbonTabs { get; init; } = Array.Empty<RibbonTabContribution>();

    /// <summary>Schaltflaechen auf dem Start-Tab, die ein Dokument des Plugins oeffnen.</summary>
    public IReadOnlyList<DocumentCommandContribution> DocumentCommands { get; init; }
        = Array.Empty<DocumentCommandContribution>();
}

/// <summary>
/// Ein kontextbezogener Ribbon-Tab. Der fertige <c>Fluent:RibbonTabItem</c> kommt als
/// Ressource aus dem Plugin — so bleibt die volle XAML-Ausdruckskraft erhalten
/// (Bindungen an <c>ActiveDocument</c>, Aufklapp-Schaltflaechen, Icons), statt dass
/// der Host Oberflaeche aus Deskriptoren zusammenbaut.
///
/// <para>Die Anbindung an die kontextuelle Tab-Gruppe uebernimmt der Host: eine
/// <c>ElementName</c>-Bindung ins Hauptfenster funktioniert aus einem separat
/// geladenen Woerterbuch heraus nicht.</para>
/// </summary>
public sealed class RibbonTabContribution
{
    /// <summary>
    /// Schluessel, unter dem der Tab sichtbar wird. Muss mit
    /// <see cref="DocumentViewModelBase.ContextTabKey"/> des zugehoerigen
    /// Dokument-Tabs uebereinstimmen.
    /// </summary>
    public required string ContextTabKey { get; init; }

    /// <summary>Beschriftung der kontextuellen Tab-Gruppe (der farbige Streifen darueber).</summary>
    public required string GroupHeader { get; init; }

    /// <summary>Akzentfarbe der Tab-Gruppe (Office-typischer farbiger Streifen).</summary>
    public required Color AccentColor { get; init; }

    /// <summary>Woerterbuch, das den Ribbon-Tab enthaelt.</summary>
    public required Uri ResourceDictionary { get; init; }

    /// <summary>Schluessel des <c>Fluent:RibbonTabItem</c> in diesem Woerterbuch.</summary>
    public required string ResourceKey { get; init; }
}

/// <summary>
/// Eine Schaltflaeche auf dem Start-Tab, die einen Dokument-Tab des Plugins oeffnet.
/// </summary>
public sealed class DocumentCommandContribution
{
    /// <summary>Beschriftung der Schaltflaeche.</summary>
    public required string Header { get; init; }

    /// <summary>
    /// Schluessel eines Icons aus den Anwendungsressourcen (z. B. <c>Icon.Database</c>),
    /// oder <c>null</c> fuer keine Grafik.
    /// </summary>
    public string? IconResourceKey { get; init; }

    /// <summary>Kurzhinweis (Tooltip).</summary>
    public string? ToolTip { get; init; }

    /// <summary>
    /// Erzeugt den zu oeffnenden Dokument-Tab. Liefert die Fabrik <c>null</c>, wird
    /// nichts geoeffnet — so laesst sich ein Abbruch im Datei-Dialog abbilden, ohne
    /// dass ein leerer Tab entsteht.
    /// </summary>
    public required Func<DocumentViewModelBase?> CreateDocument { get; init; }
}
