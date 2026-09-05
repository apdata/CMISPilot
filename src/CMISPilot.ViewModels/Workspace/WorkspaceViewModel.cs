using APX.Wpf.Shell.ViewModels.Workspace;
using CMISPilot.ViewModels.Connection;

namespace CMISPilot.ViewModels.Workspace;

/// <summary>
/// Werkbank von CMISPilot. Erbt den docking-neutralen Kern (offene Dokument-Tabs,
/// aktives Dokument, Werkzeugfenster und deren Sichtbarkeit) aus
/// <see cref="APX.Wpf.Shell.ViewModels.Workspace.WorkspaceViewModel"/> und ergänzt,
/// was nur CMISPilot betrifft: den Verbindungsstatus. Die Profilverwaltung ist
/// Teil des Verbinden-Dialogs (<see cref="ConnectDialogViewModel"/>), nicht mehr
/// hier – der frühere Backstage-Tab „Profile" ist entfallen.
/// </summary>
public sealed class WorkspaceViewModel : APX.Wpf.Shell.ViewModels.Workspace.WorkspaceViewModel
{
    /// <param name="tools">
    /// Die Werkzeugfenster (aus DI). Reihenfolge = Anzeigereihenfolge je Andockbereich.
    /// </param>
    /// <param name="connection">
    /// Verbindungsstatus (R4 Etappe 2), von der Statusbar gebunden
    /// (<c>{Binding Connection.StatusText}</c> usw.).
    /// </param>
    public WorkspaceViewModel(
        IEnumerable<IToolViewModel> tools,
        ConnectionStatusViewModel connection)
        : base(tools)
    {
        Connection = connection;
    }

    /// <summary>Verbindungsstatus fuer die Statusbar (R4 Etappe 2).</summary>
    public ConnectionStatusViewModel Connection { get; }
}
