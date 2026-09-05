using APX.Wpf.Shell.ViewModels;
using APX.Wpf.Shell.ViewModels.Logging;
using APX.Wpf.Shell.ViewModels.Workspace;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CMISPilot.Cmis.Contracts;
using CMISPilot.Cmis.Errors;
using CMISPilot.ViewModels.Messages;
using CMISPilot.ViewModels.Workspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace CMISPilot.ViewModels.Explorer;

/// <summary>
/// Werkzeugfenster „Explorer" (R4 Etappe 2, ersetzt den bisherigen Platzhalter):
/// Server-Baum mit Wurzel Server → Repository → Ordner, Lazy-Load der Unterordner.
/// Baut den Baum neu auf, sobald der <see cref="ISessionContext"/> eine Verbindung
/// meldet, und leert ihn beim Trennen. Auswahl eines Knotens wird als
/// <see cref="NodeSelectedMessage"/> ueber den <see cref="IMessenger"/> gemeldet
/// (Kopplung zum kuenftigen Eigenschaften-Werkzeugfenster, R4 Etappe 3).
///
/// Bewusst WPF-frei (NFA-03); Serveraufrufe laufen async ueber
/// <see cref="ViewModelBase.BeginBusy"/>, nie <c>.Result</c>/<c>.Wait()</c> (NFA-13).
/// </summary>
public sealed partial class ServerTreeViewModel : ToolViewModelBase
{
    private readonly IConnectionService _connectionService;
    private readonly IBrowseService _browseService;
    private readonly ISessionContext _sessionContext;
    private readonly IMessenger _messenger;

    // Der Baum wird beim Konstruieren (UI-Thread) erzeugt; hier fangen wir den
    // UI-SynchronizationContext ein. Der ISessionContext feuert ConnectionChanged
    // aus dem ThreadPool (der Verbindungsaufbau laeuft ueber Task.Run im Executor),
    // die an die TreeView gebundene RootNodes-Collection darf aber nur vom
    // UI-Thread veraendert werden. Deshalb marshallen wir den Baumaufbau hierher
    // zurueck (bleibt WPF-frei, NFA-03: SynchronizationContext ist System.Threading).
    private readonly SynchronizationContext? _uiContext;

    /// <param name="connectionService">Nicht direkt genutzt (Signatur laut Konzept), fuer kuenftige Erweiterungen (z. B. erneutes Verbinden aus dem Baum) vorgehalten.</param>
    /// <param name="browseService">Laedt Wurzelordner und Kinder des aktiven Repositories.</param>
    /// <param name="sessionContext">Haelt den Zustand der aktiven Verbindung (Single Source of Truth).</param>
    /// <param name="messenger">Meldet Knotenauswahl an interessierte Werkzeugfenster.</param>
    public ServerTreeViewModel(
        IConnectionService connectionService,
        IBrowseService browseService,
        ISessionContext sessionContext,
        IMessenger messenger)
        : base("tool:explorer", ToolDock.Left)
    {
        _connectionService = connectionService;
        _browseService = browseService;
        _sessionContext = sessionContext;
        _messenger = messenger;
        _uiContext = SynchronizationContext.Current;

        Title = "Explorer";

        _sessionContext.ConnectionChanged += OnConnectionChanged;

        if (_sessionContext.IsConnected)
        {
            _ = BuildTreeAsync(CancellationToken.None);
        }
    }

    /// <summary>Wurzelknoten des Baums (i. d. R. genau ein Server-Knoten).</summary>
    public ObservableCollection<TreeNodeViewModel> RootNodes { get; } = new();

    /// <summary>Der aktuell im Baum gewaehlte Knoten.</summary>
    [ObservableProperty]
    private TreeNodeViewModel? _selectedNode;

    partial void OnSelectedNodeChanged(TreeNodeViewModel? value) =>
        _messenger.Send(new NodeSelectedMessage(value?.CmisObject));

    /// <summary>
    /// Sucht im aktuell geladenen Baum den Ordnerknoten mit der angegebenen Id, klappt
    /// ihn samt seiner Vorfahren auf und selektiert ihn. Damit folgt der Baum einer
    /// Navigation, die ausserhalb des Baums ausgeloest wurde (z. B. Doppelklick auf ein
    /// Verzeichnis in der Dateiliste des Explorer-Tabs). Das Aufklappen des Zielknotens
    /// loest zugleich den Lazy-Load seiner Unterordner aus (der Baum "erweitert sich").
    /// Nicht geladene Teilbaeume (Platzhalter) werden nicht erzwungen; fuer den
    /// ueblichen Fall (Ziel ist ein Kind des gerade angezeigten Ordners) ist der Knoten
    /// bereits vorhanden.
    /// </summary>
    public void SelectFolder(string folderId)
    {
        foreach (var root in RootNodes)
        {
            if (ExpandAndSelect(root, folderId))
            {
                return;
            }
        }
    }

    private bool ExpandAndSelect(TreeNodeViewModel node, string folderId)
    {
        if (node.ObjectId == folderId && node.Kind == TreeNodeKind.Folder)
        {
            node.IsExpanded = true;
            node.IsSelected = true;
            SelectedNode = node;
            return true;
        }

        foreach (var child in node.Children)
        {
            if (ExpandAndSelect(child, folderId))
            {
                // Vorfahren auf dem Pfad zum Ziel aufklappen, damit es sichtbar ist.
                node.IsExpanded = true;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reagiert auf Verbindungswechsel: Baum aufbauen bzw. leeren. Das Event kann aus
    /// einem Hintergrund-Thread kommen (siehe <see cref="_uiContext"/>), daher die
    /// Collection-Aenderungen an den UI-Thread marshallen.
    /// </summary>
    private void OnConnectionChanged(object? sender, EventArgs e)
    {
        void Handle()
        {
            if (_sessionContext.IsConnected)
            {
                _ = BuildTreeAsync(CancellationToken.None);
            }
            else
            {
                RootNodes.Clear();
                SelectedNode = null;
            }
        }

        if (_uiContext is not null && _uiContext != SynchronizationContext.Current)
        {
            _uiContext.Post(_ => Handle(), null);
        }
        else
        {
            Handle();
        }
    }

    /// <summary>
    /// Baut den Baum neu: Server-Wurzel, darunter der Repository-Knoten des aktiv
    /// verbundenen Repositories, darunter dessen Wurzelordner.
    /// </summary>
    private async Task BuildTreeAsync(CancellationToken ct)
    {
        using (BeginBusy())
        {
            try
            {
                RootNodes.Clear();

                // Server-Knoten zuerst einhaengen, damit der Baum auch dann sichtbar
                // bleibt (Server → Repository), wenn das Laden des Wurzelordners
                // fehlschlaegt (der Browse-Fehler landet sonst im catch, bevor
                // ueberhaupt etwas angezeigt wurde).
                // IsNullOrWhiteSpace statt ??: bei einem AtomPub-Profil ist BrowserUrl
                // leer, nicht null - ein blosses ?? liefert dann eine leere Beschriftung.
                var profile = _sessionContext.CurrentProfile;
                var serverUrl = profile is null ? null
                    : string.IsNullOrWhiteSpace(profile.BrowserUrl) ? profile.AtomPubUrl : profile.BrowserUrl;
                var serverName = string.IsNullOrWhiteSpace(serverUrl) ? "Server" : serverUrl;
                var serverNode = new TreeNodeViewModel(TreeNodeKind.Server, serverName);
                RootNodes.Add(serverNode);
                serverNode.IsExpanded = true;

                var repository = _sessionContext.CurrentRepository;
                if (repository is not null)
                {
                    // Auch hier IsNullOrWhiteSpace: ein leerer repositoryName vom Server
                    // wuerde sonst als Knoten ohne Beschriftung durchgehen.
                    var repoLabel = string.IsNullOrWhiteSpace(repository.Name) ? repository.Id : repository.Name;
                    var repoNode = new TreeNodeViewModel(TreeNodeKind.Repository, repoLabel, repository.Id);
                    serverNode.Children.Add(repoNode);
                    repoNode.IsExpanded = true;

                    var root = await _browseService.GetRootFolderAsync(ct).ConfigureAwait(true);
                    var rootFolderNode = new TreeNodeViewModel(
                        TreeNodeKind.Folder, root.Name ?? root.Id, root.Id, root, LoadChildFoldersAsync);
                    repoNode.Children.Add(rootFolderNode);

                    // Direkt beim Verbinden die Verzeichnisse des Wurzelordners auflisten
                    // (Aufklappen loest den Lazy-Load der Unterordner aus), damit der
                    // Nutzer nach dem Verbinden sofort Server → Repository → Verzeichnisse
                    // sieht, ohne den Wurzelordner erst manuell aufklappen zu muessen.
                    rootFolderNode.IsExpanded = true;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (CmisAppException)
            {
                // Baum bleibt einfach (Server-Wurzel ohne Repository); Details laufen
                // ueber die uebliche Fehlerbehandlung der Verbindungs-Vertikale.
            }
        }
    }

    /// <summary>Lazy-Load der Unterordner eines Ordnerknotens (nur Ordner-Kinder, analog M4).</summary>
    private async Task LoadChildFoldersAsync(TreeNodeViewModel node, CancellationToken ct)
    {
        if (node.ObjectId is null)
        {
            return;
        }

        using (BeginBusy())
        {
            try
            {
                var children = await _browseService.GetChildrenAsync(node.ObjectId, ct).ConfigureAwait(true);

                var folderNodes = children
                    .Where(c => c.IsFolder)
                    .Select(c => new TreeNodeViewModel(
                        TreeNodeKind.Folder, c.Name ?? c.Id, c.Id, c, LoadChildFoldersAsync));

                node.SetChildren(folderNodes);
            }
            catch (OperationCanceledException)
            {
            }
            catch (CmisAppException)
            {
                node.SetChildren(Array.Empty<TreeNodeViewModel>());
            }
        }
    }
}
