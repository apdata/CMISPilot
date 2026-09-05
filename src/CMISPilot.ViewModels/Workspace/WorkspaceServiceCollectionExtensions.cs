using APX.Wpf.Shell.ViewModels;
using APX.Wpf.Shell.ViewModels.Logging;
using APX.Wpf.Shell.ViewModels.Workspace;
using CMISPilot.ViewModels.Connection;
using CMISPilot.ViewModels.Diagnostics;
using CMISPilot.ViewModels.Explorer;
using CMISPilot.ViewModels.Query;
using CMISPilot.ViewModels.Types;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace CMISPilot.ViewModels.Workspace;

/// <summary>
/// DI-Registrierung des neuen Werkbank-Modells (R1). Bewusst getrennt vom alten
/// <c>AddViewModels</c> (Sidebar-Shell), damit die Alt-App unberührt bleibt, bis die
/// neue Shell Feature-Parität erreicht (R7).
///
/// Seit R3 ist „Ausgabe" ein echtes, log-gespeistes Werkzeugfenster
/// (<see cref="OutputToolViewModel"/>). Seit R4 Etappe 2 ist „Explorer" der echte
/// Server-Baum (<see cref="ServerTreeViewModel"/>); seit R4 Etappe 3 ist
/// „Eigenschaften" das echte, selektionsgesteuerte Werkzeugfenster
/// (<see cref="PropertiesViewModel"/>). Seit R5 sind „Abfrage" (<see cref="QueryDocumentViewModel"/>)
/// und „Typen" (<see cref="TypesDocumentViewModel"/>) echte Dokument-Tabs, und der
/// vormalige „Konsole"-Platzhalter ist das echte Diagnose-Werkzeugfenster
/// (<see cref="DiagnosticsToolViewModel"/>). Die Registrierungsreihenfolge je
/// Andockbereich bleibt bestehen.
/// </summary>
public static class WorkspaceServiceCollectionExtensions
{
    /// <summary>Registriert <see cref="WorkspaceViewModel"/>, den Log-Feed und die Werkzeugfenster.</summary>
    public static IServiceCollection AddWorkspace(this IServiceCollection services)
    {
        // S5: Log-Strom und die Werkzeugfenster Ausgabe und Fehlerliste kommen aus
        // APX.Wpf.Shell und werden dort ueber AddApxShell registriert (App.xaml.cs).

        // F2: Excel-Export von Typdefinitionen (ClosedXML). WPF-frei, daher hier in der
        // Werkbank-Registrierung und nicht in der Shell.
        services.AddSingleton<Export.ITypeDefinitionExporter, Export.ClosedXmlTypeDefinitionExporter>();

        // F3: Excel-Export von Abfrage- und Ordnerlisten (teilt sich den Aufbau mit F2).
        services.AddSingleton<Export.IListExporter, Export.ClosedXmlListExporter>();

        // R4 Etappe 2: Messenger fuer die Entkopplung Server-Baum ↔ Eigenschaften
        // (NodeSelectedMessage). Die neue Shell registriert (anders als die alte
        // Sidebar-App, siehe ServiceCollectionExtensions.Shell.cs) noch keinen
        // IMessenger; hier zentral fuer die gesamte Werkbank.
        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);

        // R4 Etappe 2: Verbindungsstatus (Statusbar) + Verbinden-Dialog-VM.
        services.AddSingleton<ConnectionStatusViewModel>();
        services.AddTransient<ConnectDialogViewModel>();

        // Werkzeugfenster. Reihenfolge = Anzeigereihenfolge.
        services.AddSingleton<IToolViewModel>(sp => sp.GetRequiredService<ServerTreeViewModel>());
        services.AddSingleton<ServerTreeViewModel>();

        // R4 Etappe 3: echtes Eigenschaften-Werkzeugfenster (folgt der Auswahl per
        // NodeSelectedMessage, zeigt Displayname/PropertyID/Datentyp/Pflichtfeld).
        services.AddSingleton<IToolViewModel>(sp => sp.GetRequiredService<PropertiesViewModel>());
        services.AddSingleton<PropertiesViewModel>();

        // Ausgabe und Fehlerliste stehen hier nicht mehr: sie kommen aus der Shell.
        // Ihre Position in der Anzeigereihenfolge ergibt sich daraus, dass
        // AddApxShell vor AddWorkspace aufgerufen wird.

        // R5.3: Diagnose-Werkzeugfenster (ersetzt den R1-Platzhalter „Konsole").
        services.AddSingleton<IToolViewModel>(sp => sp.GetRequiredService<DiagnosticsToolViewModel>());
        services.AddSingleton<DiagnosticsToolViewModel>();

        // R5.1/R5.2: „Abfrage" und „Typen" als Dokument-Tabs. Anders als der
        // Explorer-Tab (Laufzeit-Parameter Ordner) brauchen sie keine manuelle
        // Konstruktion; die feste ContentId dedupliziert beim erneuten Öffnen.
        services.AddSingleton<QueryDocumentViewModel>();
        services.AddSingleton<TypesDocumentViewModel>();

        // Repository-Info-Tab (FA-10/FA-11), gleiche Bauart: feste ContentId, keine
        // Laufzeit-Parameter, deshalb als Singleton.
        services.AddSingleton<Repository.RepositoryInfoDocumentViewModel>();

        // Der PowerBuilder-Index-Mapping-Editor (frueher hier registriert) ist kein
        // Teil von CMISPilot mehr - er lebt als eigenstaendiges Plugin in
        // CMISPilot.Plugins.IndexEditor.

        services.AddSingleton<WorkspaceViewModel>();

        // Plan-Plugin-Schnittstelle P1.5: Plugins duerfen eigene Dokument-Tabs
        // oeffnen (z. B. "Neu"/"Laden" aus einem kontextbezogenen Ribbon-Tab, der
        // nicht auf dem Start-Tab sitzt). Sie kennen CMISPilot.ViewModels nicht,
        // sondern nur den Vertrag (APX.Wpf.Shell.ViewModels.Workspace) und
        // injizieren sich dessen WorkspaceViewModel-Basis. Dieselbe Singleton-
        // Instanz wie oben, nur unter der Basis-Typidentitaet zusaetzlich sichtbar.
        services.AddSingleton<global::APX.Wpf.Shell.ViewModels.Workspace.WorkspaceViewModel>(
            sp => sp.GetRequiredService<WorkspaceViewModel>());

        return services;
    }
}
