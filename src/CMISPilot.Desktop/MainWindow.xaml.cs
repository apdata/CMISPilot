using IFileLauncher = APX.Wpf.Shell.ViewModels.Contracts.IFileLauncher;
using ISettingsStore = APX.Wpf.Shell.ViewModels.Contracts.ISettingsStore;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using APX.Wpf.Shell;
using Fluent;
using APX.Wpf.Shell.Docking;
using CMISPilot.Cmis.Contracts;
using CMISPilot.Cmis.Errors;
using CMISPilot.Cmis.Models;
using CMISPilot.Cmis.Profiles;
using CMISPilot.Desktop.Dialogs;
using CMISPilot.ViewModels.Explorer;
using CMISPilot.ViewModels.Export;
using CMISPilot.ViewModels.ObjectDetails;
using CMISPilot.ViewModels.Query;
using CMISPilot.ViewModels.Repository;
using CMISPilot.ViewModels.Shell;
using CMISPilot.ViewModels.Types;
using CMISPilot.ViewModels.Workspace;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CMISPilot.Desktop;

/// <summary>
/// Hauptfenster von CMISPilot. Hostet Ribbon, den Docking-Bereich mit
/// Werkzeugfenstern und Dokument-Tabs sowie die Statusbar.
/// Der <see cref="WorkspaceViewModel"/> wird per DI injiziert und als DataContext
/// gesetzt (R1). Seit R4 Etappe 2 verbindet/trennt das Start-Ribbon über
/// <see cref="IConnectionService"/> und öffnet dafür den <see cref="ConnectDialog"/>
/// (per DI erzeugt).
///
/// <para><b>S6.5, durchgesehen:</b> was <see cref="ShellRibbonWindow"/> und
/// <see cref="DockingHost"/> abdecken (Layout-Zeitpunkt, Bindung der
/// Dokumente/Werkzeugfenster, Theme), steht hier nicht mehr. Was bleibt, ist eine
/// bewusste Brücke für Dinge, die sich nicht binden lassen: Baum-Selektion
/// (<c>TreeView.SelectedItem</c>), die kontextsensitive Ribbon-Tab-Auswahl
/// (<see cref="SyncRibbonToActiveDocument"/> — die Library kennt nur Sichtbarkeit,
/// nicht Auswahl), dynamisch erzeugte Query-Spalten und Fenster mit
/// Laufzeitparametern (<see cref="ExtendedPropertiesWindow"/>, Verbinden-Dialog).
/// Der Index-Editor ist seit P6 kein Teil dieser Klasse mehr — er lebt als
/// eigenstaendiges Plugin (CMISPilot.Plugins.IndexEditor) und trägt seinen
/// kontextbezogenen Ribbon-Tab über PluginContributions bei.</para>
/// </summary>
public partial class MainWindow : ShellRibbonWindow
{
    private readonly WorkspaceViewModel _workspace;
    private readonly IConnectionService _connectionService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ISettingsStore _settingsStore;

    /// <summary>
    /// Von Plugins beigesteuerte kontextbezogene Ribbon-Tabs, nach
    /// <c>ContextTabKey</c>. Ergänzt die fest im XAML stehenden Tabs in
    /// <see cref="SyncRibbonToActiveDocument"/>.
    /// </summary>
    private readonly Dictionary<string, RibbonTabItem> _pluginRibbonTabs = new(StringComparer.Ordinal);

    /// <param name="workspace">Zentrales Werkbank-Modell (aus DI).</param>
    /// <param name="connectionService">Baut die Verbindung auf/ab (Ribbon „Verbinden"/„Trennen").</param>
    /// <param name="serviceProvider">Erzeugt den <see cref="ConnectDialog"/> mit vollem DI-Container.</param>
    /// <param name="settingsStore">Ablage für den gemerkten Fensterzustand (S7.3).</param>
    /// <param name="layoutLogger">
    /// Protokoll der Layout-Persistenz (S7.5). Ohne dieses landen Warnungen zu
    /// einer beschädigten oder unbekannte Kennungen enthaltenden Layout-Datei
    /// nirgendwo – <see cref="ShellRibbonWindow.LayoutLogger"/> ist ohne
    /// Überschreibung <c>null</c>.
    /// </param>
    public MainWindow(
        WorkspaceViewModel workspace,
        IConnectionService connectionService,
        IServiceProvider serviceProvider,
        ISettingsStore settingsStore,
        ILogger<APX.Wpf.Shell.Docking.XmlLayoutPersistence> layoutLogger)
    {
        _workspace = workspace;
        _connectionService = connectionService;
        _serviceProvider = serviceProvider;
        _settingsStore = settingsStore;
        LayoutLogger = layoutLogger;
        DataContext = workspace;
        InitializeComponent();

        // Titel um die Produktversion ergaenzen (Quelle: Assembly-Metadaten, wie
        // im "Ueber CMISPilot"-Fenster - keine doppelte Pflege).
        Title = $"CMISPilot {ProductVersion()}";

        // S7.3: vor dem ersten Show(), damit es nicht sichtbar umspringt.
        RestoreWindowState();

        // Kontextsensitives Ribbon: beim Wechsel des aktiven Dokument-Tabs den
        // passenden Ribbon-Tab automatisch selektieren (nicht nur einblenden).
        _workspace.PropertyChanged += OnWorkspacePropertyChanged;
    }

    /// <summary>
    /// Produktversion aus den Assembly-Metadaten: bevorzugt die informelle Version
    /// (ohne angehaengte Build-Metadaten nach <c>+</c>), sonst die dreiteilige
    /// Assembly-Version.
    /// </summary>
    private static string ProductVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            var plus = informational.IndexOf('+');
            return plus >= 0 ? informational[..plus] : informational;
        }

        var version = assembly.GetName().Version;
        return version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    /// <summary>
    /// Selektiert den zum aktiven Dokument gehoerenden (kontextuellen) Ribbon-Tab,
    /// sobald sich das aktive Dokument aendert. So ist beim Tab-Wechsel immer das
    /// passende kontextsensitive Ribbon aktiv (Explorer/Abfrage/Typen), ohne dass der
    /// Nutzer den Ribbon-Tab von Hand anklicken muss; ohne kontextuelles Dokument faellt
    /// die Auswahl auf den Start-Tab zurueck.
    /// </summary>
    /// <inheritdoc />
    protected override DockingHost DockingHost => DockManager;

    /// <inheritdoc />
    protected override ILogger<APX.Wpf.Shell.Docking.XmlLayoutPersistence> LayoutLogger { get; }

    /// <summary>
    /// Ablageort des Fensterlayouts: neben Log und Einstellungen unter
    /// <c>%APPDATA%\CMISPilot</c>.
    /// </summary>
    protected override string LayoutPath { get; } = System.IO.Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
        "CMISPilot",
        "layout.xml");

    /// <summary>Verwirft das gespeicherte Layout („Ansicht" &gt; „Zurücksetzen").</summary>
    private void OnResetLayoutClick(object sender, RoutedEventArgs e) =>
        LayoutPersistence?.Reset();

    /// <summary>
    /// Stellt Position, Größe und Maximierung aus <see cref="WindowSettings"/>
    /// wieder her (S7.3). Läuft vor dem ersten <c>Show()</c>, damit nichts
    /// sichtbar umspringt.
    ///
    /// <para><b>Mehrmonitorfall:</b> Wurde seit dem letzten Lauf ein Monitor
    /// abgezogen oder die Auflösung geändert, kann die gespeicherte Position
    /// außerhalb jedes sichtbaren Bereichs liegen. Geprüft wird das gegen
    /// <see cref="SystemParameters.VirtualScreenLeft"/> &amp; Co. — die
    /// umschließende Fläche aller angeschlossenen Bildschirme. Liegt das Fenster
    /// (auch nur teilweise) außerhalb, bleibt es bei der XAML-Vorgabe
    /// (<c>WindowStartupLocation="CenterScreen"</c>) statt unsichtbar zu starten.
    /// </para>
    /// </summary>
    private void RestoreWindowState()
    {
        var settings = _settingsStore.Load<WindowSettings>();
        if (!settings.HasValue)
        {
            return;
        }

        var virtualScreen = new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);

        var savedBounds = new Rect(settings.Left, settings.Top, settings.Width, settings.Height);

        if (!virtualScreen.IntersectsWith(savedBounds))
        {
            return;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = settings.Left;
        Top = settings.Top;
        Width = settings.Width;
        Height = settings.Height;

        if (settings.IsMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    /// <summary>Merkt sich Position, Größe und Maximierung beim Schließen.</summary>
    /// <inheritdoc />
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!e.Cancel)
        {
            // Bei maximiertem oder minimiertem Fenster stehen in Left/Top/Width/
            // Height nicht die eigentlichen Restore-Werte, sondern die des
            // aktuellen Zustands (bei Minimiert sogar Werte außerhalb des
            // Bildschirms). RestoreBounds liefert die Größe/Position im
            // normalen Zustand, unabhängig davon, wie das Fenster gerade steht.
            var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;

            _settingsStore.Save(new WindowSettings
            {
                HasValue = true,
                Left = bounds.Left,
                Top = bounds.Top,
                Width = bounds.Width,
                Height = bounds.Height,
                IsMaximized = WindowState == WindowState.Maximized
            });
        }

        base.OnClosing(e);
    }

    private void OnWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WorkspaceViewModel.ActiveDocument)
            or nameof(WorkspaceViewModel.ActiveContextTabKey))
        {
            SyncRibbonToActiveDocument();
        }
    }

    private void SyncRibbonToActiveDocument()
    {
        var key = _workspace.ActiveContextTabKey;

        var tab = key switch
        {
            "explorer" => ExplorerRibbonTab,
            "query" => QueryRibbonTab,
            "types" => TypesRibbonTab,
            "repository-info" => RepositoryInfoRibbonTab,
            // Von Plugins beigesteuerte Tabs stehen nicht im XAML, sondern kommen
            // aus PluginContributions (siehe ApplyPluginContributions).
            _ when key is not null && _pluginRibbonTabs.TryGetValue(key, out var pluginTab) => pluginTab,
            _ => StartRibbonTab
        };

        // Verzoegert, damit die an ActiveContextTabKey gebundene Visibility des
        // kontextuellen Tabs bereits aktualisiert ist – ein noch collapsed Tab laesst
        // sich nicht selektieren.
        Dispatcher.BeginInvoke(
            new System.Action(() =>
            {
                if (tab.Visibility == Visibility.Visible)
                {
                    MainRibbon.SelectedTabItem = tab;
                }
            }),
            System.Windows.Threading.DispatcherPriority.Background);
    }

    /// <summary>
    /// Öffnet den (singleton) Abfrage-Dokument-Tab (R5.1). Anders als der
    /// Explorer-Tab braucht <see cref="QueryDocumentViewModel"/> keinen
    /// Laufzeit-Parameter, daher wird er hier direkt aus dem DI-Container
    /// aufgelöst; die feste ContentId „query" dedupliziert beim erneuten Öffnen
    /// (<see cref="WorkspaceViewModel.OpenDocument"/>).
    /// </summary>
    private void OnOpenQueryClick(object sender, RoutedEventArgs e) =>
        _workspace.OpenDocument(_serviceProvider.GetRequiredService<QueryDocumentViewModel>());

    /// <summary>Öffnet den (singleton) Typen-Dokument-Tab (R5.2), analog <see cref="OnOpenQueryClick"/>.</summary>
    private void OnOpenTypesClick(object sender, RoutedEventArgs e) =>
        _workspace.OpenDocument(_serviceProvider.GetRequiredService<TypesDocumentViewModel>());

    /// <summary>Oeffnet den (singleton) Repository-Info-Tab (FA-10/FA-11), analog OnOpenTypesClick.</summary>
    private void OnOpenRepositoryInfoClick(object sender, RoutedEventArgs e) =>
        _workspace.OpenDocument(_serviceProvider.GetRequiredService<RepositoryInfoDocumentViewModel>());

    /// <summary>Öffnet den Verbinden-Dialog (R4 Etappe 2); baut bei Erfolg die Verbindung im Dialog auf.</summary>
    private void OnConnectClick(object sender, RoutedEventArgs e)
    {
        var dialog = _serviceProvider.GetRequiredService<ConnectDialog>();
        dialog.Owner = this;
        dialog.ShowDialog();
    }

    /// <summary>Trennt die aktive Verbindung (R4 Etappe 2). Fehler landen im Log (Fehlerliste).</summary>
    private async void OnDisconnectClick(object sender, RoutedEventArgs e)
    {
        using (_workspace.Connection.BeginBusy())
        {
            await _connectionService.DisconnectAsync();
        }
    }

    /// <summary>
    /// Doppelklick auf einen Ordnerknoten im Server-Baum (R4 Etappe 3): öffnet den
    /// Ordner als <see cref="ExplorerDocumentViewModel"/>-Tab. Der ViewModel wird
    /// bewusst nicht aus dem DI-Container aufgelöst (der Ordner ist ein
    /// Laufzeit-Parameter), sondern hier mit den aus dem Container geholten
    /// Diensten direkt konstruiert (siehe Klassen-Doku von
    /// <see cref="ExplorerDocumentViewModel"/>). Die Deduplizierung nach Ordner-ID
    /// übernimmt <see cref="WorkspaceViewModel.OpenDocument(IDocumentViewModel)"/>.
    /// </summary>
    private void OnServerTreeMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TreeView { DataContext: ServerTreeViewModel vm })
        {
            return;
        }

        if (vm.SelectedNode is { Kind: TreeNodeKind.Folder, CmisObject: { } folder })
        {
            OpenFolderDocument(folder);
        }
    }

    /// <summary>
    /// Klick auf einen Ordnerknoten im Server-Baum: ist gerade kein Explorer-Tab offen,
    /// wird einer geoeffnet – auch dann, wenn sich die Baum-Selektion dabei nicht aendert
    /// (dann laeuft <see cref="OnServerTreeSelectedItemChanged"/> nicht). So laesst sich der
    /// Explorer nach dem Schliessen des Tabs jederzeit ueber den Baum wieder oeffnen.
    /// Bei bereits offenem Explorer-Tab bleibt die Navigation dem
    /// <see cref="OnServerTreeSelectedItemChanged"/>-Pfad ueberlassen.
    /// </summary>
    private void OnServerTreeItemClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is TreeViewItem
            {
                IsSelected: true,
                DataContext: TreeNodeViewModel { Kind: TreeNodeKind.Folder, CmisObject: { } folder }
            }
            && !_workspace.Documents.OfType<ExplorerDocumentViewModel>().Any())
        {
            OpenFolderDocument(folder);
        }
    }

    /// <summary>
    /// <see cref="TreeView.SelectedItem"/> ist nicht bindbar (read-only), daher wird
    /// die Baum-Selektion hier ins <see cref="ServerTreeViewModel"/> gereicht
    /// (analog PasswordBox-Muster).
    ///
    /// Ein Klick auf einen Ordnerknoten klappt ihn zugleich auf (löst den Lazy-Load der
    /// Unterordner aus → der Baum wird um die Verzeichnisse erweitert) und öffnet/aktiviert
    /// seinen Inhalt als Explorer-Dokument-Tab. So genügt ein einfacher Klick, um wie
    /// gewünscht Inhalt zu laden und den Baum weiter aufzublättern; die Deduplizierung
    /// nach Ordner-Id übernimmt <see cref="WorkspaceViewModel.OpenDocument(IDocumentViewModel)"/>.
    /// </summary>
    private void OnServerTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (sender is not TreeView { DataContext: ServerTreeViewModel vm } ||
            e.NewValue is not TreeNodeViewModel node || node.IsPlaceholder)
        {
            return;
        }

        vm.SelectedNode = node;

        if (node is { Kind: TreeNodeKind.Folder, CmisObject: { } folder })
        {
            node.IsExpanded = true;
            OpenFolderDocument(folder);
        }
    }

    /// <summary>
    /// Öffnet (oder aktiviert bei bereits offenem Tab) den Ordnerinhalt als
    /// <see cref="ExplorerDocumentViewModel"/>. Der ViewModel wird bewusst nicht aus dem
    /// DI-Container aufgelöst (der Ordner ist ein Laufzeit-Parameter), sondern mit den aus
    /// dem Container geholten Diensten direkt konstruiert (siehe Klassen-Doku von
    /// <see cref="ExplorerDocumentViewModel"/>).
    /// </summary>
    private void OpenFolderDocument(CmisObjectDto folder)
    {
        // Windows-Explorer-Verhalten: existiert bereits ein Explorer-Tab, wird er
        // in-place zum geklickten Ordner navigiert und aktiviert (auch wenn gerade ein
        // anderer Tab – etwa Abfrage/Typen – vorn ist). Nur wenn kein Explorer-Tab
        // (mehr) offen ist, wird ein neuer erzeugt.
        var existing = _workspace.Documents.OfType<ExplorerDocumentViewModel>().FirstOrDefault();
        if (existing is not null)
        {
            existing.NavigateTo(folder);
            _workspace.ActiveDocument = existing;
            return;
        }

        var browseService = _serviceProvider.GetRequiredService<IBrowseService>();
        var objectService = _serviceProvider.GetRequiredService<IObjectService>();
        var typeService = _serviceProvider.GetRequiredService<ITypeService>();
        var dialogService = _serviceProvider.GetRequiredService<IDialogService>();
        var fileLauncher = _serviceProvider.GetRequiredService<IFileLauncher>();
        var messenger = _serviceProvider.GetRequiredService<IMessenger>();
        var logger = _serviceProvider.GetRequiredService<ILogger<ExplorerDocumentViewModel>>();
        var listExporter = _serviceProvider.GetRequiredService<IListExporter>();
        _workspace.OpenDocument(new ExplorerDocumentViewModel(
            folder, browseService, objectService, typeService, dialogService, fileLauncher, messenger, logger,
            listExporter));
    }

    /// <summary>
    /// Doppelklick auf ein Objekt in der Dateiliste des Explorer-Tabs: bei einem
    /// Verzeichnis wird in den Ordner navigiert (der Explorer-Tab zeigt dessen Inhalt)
    /// und der Server-Baum entsprechend aufgeklappt/selektiert. Bei einem Dokument wird
    /// <see cref="ExplorerDocumentViewModel.OpenCommand"/> ausgeloest (dieselbe Aktion
    /// wie der Ribbon-Befehl „Öffnen": herunterladen und mit dem Standardprogramm des
    /// Betriebssystems öffnen, FA-41).
    /// </summary>
    private void OnExplorerObjectDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid { DataContext: ExplorerDocumentViewModel vm, SelectedItem: CmisObjectDto obj })
        {
            return;
        }

        if (obj.IsFolder)
        {
            OpenFolderDocument(obj);
            _serviceProvider.GetRequiredService<ServerTreeViewModel>().SelectFolder(obj.Id);
            return;
        }

        if (vm.OpenCommand.CanExecute(null))
        {
            vm.OpenCommand.Execute(null);
        }
    }

    /// <summary>
    /// F2 auf der Explorer-Liste löst denselben <see cref="ExplorerDocumentViewModel.RenameCommand"/>
    /// aus wie das Kontextmenü „Umbenennen" (Dialog statt Inline-Bearbeitung, siehe
    /// CLAUDE.md, Fallstricke).
    /// </summary>
    private void OnExplorerGridPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F2 && sender is DataGrid { DataContext: ExplorerDocumentViewModel vm } &&
            vm.RenameCommand.CanExecute(null))
        {
            vm.RenameCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>Zeigt beim Ziehen über die Explorer-Liste an, ob genau eine Datei gedroppt werden kann.</summary>
    private void OnExplorerGridPreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects =
            sender is DataGrid { DataContext: ExplorerDocumentViewModel vm } &&
            GetSingleDroppedFile(e) is not null &&
            vm.NewDocumentFromFileCommand.CanExecute(null)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>
    /// Öffnet für die gedroppte Datei den „Neues Dokument"-Dialog (vorbelegt mit der
    /// Datei als Inhalt); legt das Dokument beim Speichern im aktuell geöffneten Ordner
    /// an, unabhängig davon, über welcher Zeile losgelassen wurde.
    /// </summary>
    private async void OnExplorerGridDrop(object sender, DragEventArgs e)
    {
        if (sender is DataGrid { DataContext: ExplorerDocumentViewModel vm } &&
            GetSingleDroppedFile(e) is { } filePath)
        {
            await vm.NewDocumentFromFileCommand.ExecuteAsync(filePath).ConfigureAwait(true);
        }

        e.Handled = true;
    }

    /// <summary>Nur genau eine existierende Datei wird akzeptiert (Mehrfach-Drop wird abgelehnt).</summary>
    private static string? GetSingleDroppedFile(DragEventArgs e) =>
        e.Data.GetDataPresent(DataFormats.FileDrop) &&
        e.Data.GetData(DataFormats.FileDrop) is string[] { Length: 1 } files &&
        File.Exists(files[0])
            ? files[0]
            : null;

    /// <summary>
    /// Selektiert die DataGridRow unter dem Cursor bei einem reinen Rechtsklick, damit
    /// „Zeile kopieren" im Kontextmenü immer die tatsächlich angeklickte Zeile trifft
    /// (WPF selektiert bei einem reinen Rechtsklick sonst nicht automatisch).
    /// Gemeinsamer Handler für alle Grids mit „Zeile kopieren"/„Alle Zeilen kopieren"
    /// (Logik in <see cref="Controls.GridClipboard"/>, wiederverwendet von
    /// <c>ExtendedPropertiesWindow</c>).
    /// </summary>
    private void OnGridRowPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e) =>
        Controls.GridClipboard.SelectRowUnderCursor(e);

    private void OnCopyRowClick(object sender, RoutedEventArgs e) => Controls.GridClipboard.CopyRow(sender);

    private void OnCopyAllRowsClick(object sender, RoutedEventArgs e) => Controls.GridClipboard.CopyAllRows(sender);

    /// <summary>
    /// Baut die dynamischen Spalten der Abfrage-Ergebnistabelle auf (Query Browser,
    /// FA-51), sobald das Grid geladen ist, und danach jedes Mal, wenn sich
    /// <see cref="QueryDocumentViewModel.ColumnNames"/> ändert. Das ViewModel bleibt
    /// WPF-frei (NFA-03); der Spaltenaufbau anhand von Spaltennamen gehört daher ins
    /// Code-Behind (analog Alt-App <c>QueryView.xaml.cs</c>, M6).
    ///
    /// <para><c>Loaded</c> statt <c>DataContextChanged</c>: der DataContext dieses
    /// Grids kommt aus einem impliziten <c>DataTemplate</c> (Dokument-Tab) und ist
    /// beim Erzeugen des Elements bereits gesetzt, statt sich später zu ändern -
    /// <c>DataContextChanged</c> feuert für einen von Anfang an korrekten Wert nie
    /// (kein beobachtbarer Wertwechsel), wodurch die Spalten nie aufgebaut wurden und
    /// die Tabelle trotz vorhandener Zeilen leer blieb.</para>
    /// </summary>
    private void OnQueryResultGridLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not DataGrid { DataContext: QueryDocumentViewModel vm } grid)
        {
            return;
        }

        RebuildQueryResultColumns(grid, vm);

        void OnColumnNamesChanged(object? s, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(QueryDocumentViewModel.ColumnNames))
            {
                RebuildQueryResultColumns(grid, vm);
            }
        }

        vm.PropertyChanged += OnColumnNamesChanged;
        grid.Unloaded += (_, _) => vm.PropertyChanged -= OnColumnNamesChanged;
    }

    private static void RebuildQueryResultColumns(DataGrid grid, QueryDocumentViewModel vm)
    {
        grid.Columns.Clear();

        // Ein neues Abfrageergebnis kann eine ganz andere Spaltenmenge haben - ein
        // CustomSort aus der vorherigen Spaltenmenge (siehe OnQueryResultGridSorting)
        // wuerde sonst ins Leere greifen oder auf eine nicht mehr existente Spalte zeigen.
        if (CollectionViewSource.GetDefaultView(grid.ItemsSource) is ListCollectionView view)
        {
            view.CustomSort = null;
        }

        foreach (var column in vm.ColumnNames)
        {
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = column,
                // Auto statt des DataGrid-Standards SizeToHeader: die Spalte richtet
                // sich nach dem Inhalt statt nur nach der Kopfzeile. MinWidth erlaubt
                // es, sie mit der Maus wieder schmal zu ziehen; nach oben ist sie
                // nicht begrenzt, den Ueberhang faengt der horizontale Scrollbalken ab.
                Width = DataGridLength.Auto,
                MinWidth = 40,
                // SortMemberPath dient hier nur als Schluessel fuer
                // OnQueryResultGridSorting - WPFs eingebauter Sort ueber einen
                // Indexer-Bindungspfad (ValuesByColumn[Spalte]) sortiert nicht
                // zuverlaessig typgerecht, siehe dortiger Kommentar.
                SortMemberPath = column,
                Binding = new Binding($"ValuesByColumn[{column}]") { Mode = BindingMode.OneWay }
            });
        }
    }

    /// <summary>
    /// F6: Header-Klick-Sortierung der Abfrage-Ergebnistabelle. Die Spalten sind
    /// dynamisch (<see cref="RebuildQueryResultColumns"/>) und binden per Indexer
    /// gegen <see cref="QueryRowDto.ValuesByColumn"/> (object?-Werte: String, Zahl,
    /// Datum, bool je nach CMIS-Property). WPFs eingebauter Sort ueber
    /// <c>SortDescription</c>/<c>PropertyComparer</c> loest einen solchen
    /// Indexer-Pfad nicht zuverlaessig typgerecht auf (Gefahr: alphabetischer statt
    /// numerischer/chronologischer Vergleich) - daher wird hier selbst sortiert.
    /// </summary>
    private void OnQueryResultGridSorting(object sender, DataGridSortingEventArgs e)
    {
        if (sender is not DataGrid grid || e.Column.SortMemberPath is not { Length: > 0 } column)
        {
            return;
        }

        e.Handled = true;
        var direction = e.Column.SortDirection != ListSortDirection.Ascending
            ? ListSortDirection.Ascending
            : ListSortDirection.Descending;

        if (CollectionViewSource.GetDefaultView(grid.ItemsSource) is ListCollectionView view)
        {
            view.CustomSort = new Controls.QueryRowComparer(column, direction);
        }

        foreach (var col in grid.Columns)
        {
            col.SortDirection = col == e.Column ? direction : null;
        }
    }

    /// <summary>
    /// <see cref="TreeView.SelectedItem"/> ist nicht bindbar (read-only), daher wird
    /// die Baum-Selektion des Typen-Tabs hier ins <see cref="TypesDocumentViewModel"/>
    /// gereicht (R5.2, analog Server-Baum <see cref="OnServerTreeSelectedItemChanged"/>).
    /// </summary>
    private void OnTypesTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (sender is TreeView { DataContext: TypesDocumentViewModel vm } &&
            e.NewValue is CMISPilot.Cmis.Models.TypeDefinitionDto type)
        {
            vm.SelectedType = type;
        }
    }

    /// <summary>Beendet die Anwendung aus dem Backstage.</summary>
    private void OnExitClick(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    /// <summary>Öffnet das „Über CMISPilot"-Fenster (R6.4, Backstage-Eintrag).</summary>
    private void OnAboutClick(object sender, RoutedEventArgs e)
    {
        var window = new AboutWindow { Owner = this };
        window.ShowDialog();
    }

    /// <summary>
    /// Öffnet <see cref="ExtendedPropertiesWindow"/> für das im aktiven Explorer-Tab
    /// selektierte Objekt (R6.1, kontextuelles Explorer-Ribbon). Der ViewModel wird
    /// bewusst nicht aus dem DI-Container aufgelöst (das Objekt ist ein
    /// Laufzeit-Parameter), analog <see cref="OnServerTreeMouseDoubleClick"/>.
    /// </summary>
    private void OnExtendedPropertiesClick(object sender, RoutedEventArgs e)
    {
        if (_workspace.ActiveDocument is not ExplorerDocumentViewModel { SelectedObject: { } target })
        {
            return;
        }

        var objectService = _serviceProvider.GetRequiredService<IObjectService>();
        var typeService = _serviceProvider.GetRequiredService<ITypeService>();
        var logger = _serviceProvider.GetRequiredService<ILogger<ExtendedPropertiesViewModel>>();
        var vm = new ExtendedPropertiesViewModel(target, objectService, typeService, logger);
        var window = new ExtendedPropertiesWindow(vm) { Owner = this };
        window.Show();
    }

    /// <summary>
    /// Befüllt das Aufklapp-Menü des Verbinden-SplitButtons (R6.3) mit den zuletzt
    /// gespeicherten Profilen. <see cref="JsonProfileStore"/> hängt neu gespeicherte
    /// Profile ans Ende der Liste an, daher entspricht die umgekehrte Ladereihenfolge
    /// näherungsweise „zuletzt verwendet". Ein Klick verbindet nur dann direkt, wenn
    /// das Profil eine Repository-ID und ein gespeichertes Passwort hat; sonst öffnet
    /// sich der normale Verbinden-Dialog vorbelegt mit URL/Benutzer.
    /// </summary>
    private async void OnConnectDropDownOpened(object? sender, EventArgs e)
    {
        if (sender is not SplitButton splitButton)
        {
            return;
        }

        splitButton.Items.Clear();

        var profileStore = _serviceProvider.GetRequiredService<IProfileStore>();
        var profiles = await profileStore.LoadAllAsync().ConfigureAwait(true);

        var recent = profiles.Reverse().Take(5).ToList();
        if (recent.Count == 0)
        {
            splitButton.Items.Add(new Fluent.MenuItem { Header = "Keine gespeicherten Profile", IsEnabled = false });
            return;
        }

        foreach (var profile in recent)
        {
            var item = new Fluent.MenuItem { Header = profile.Name };
            item.Click += async (_, _) => await ConnectWithRecentProfileAsync(profile).ConfigureAwait(true);
            splitButton.Items.Add(item);
        }
    }

    /// <summary>
    /// Verbindet direkt mit einem über das Schnellauswahl-Menü gewählten Profil
    /// (R6.3), wenn Repository-ID und (je nach Authentifizierungsart) das nötige
    /// Geheimnis vorhanden sind; sonst öffnet sich der Verbinden-Dialog vorbelegt mit
    /// dem Profil, damit Repository und/oder Passwort ergänzt werden können.
    /// </summary>
    private async Task ConnectWithRecentProfileAsync(ConnectionProfile profile)
    {
        if (!string.IsNullOrEmpty(profile.RepositoryId) && HasRequiredSecret(profile))
        {
            try
            {
                using (_workspace.Connection.BeginBusy())
                {
                    await _connectionService.ConnectAsync(profile).ConfigureAwait(true);
                }

                return;
            }
            catch (CmisAppException)
            {
                // Fällt unten auf den Verbinden-Dialog zurück, in dem der Fehler
                // sichtbar gemacht werden kann (ErrorMessage-Bindung).
            }
        }

        var dialog = _serviceProvider.GetRequiredService<ConnectDialog>();
        dialog.Owner = this;
        await dialog.SelectSavedProfileAsync(profile.Name!).ConfigureAwait(true);
        dialog.ShowDialog();
    }

    /// <summary>
    /// Ob das Profil das fuer seine <see cref="ConnectionProfile.Authentication"/>
    /// noetige Geheimnis bereits gespeichert hat - bei <see cref="CmisAuthenticationType.None"/>
    /// ist gar keins noetig, bei <see cref="CmisAuthenticationType.OAuthBearer"/> zaehlt
    /// der Bearer-Token statt des Passworts. Ohne diese Fallunterscheidung würde die
    /// Schnellauswahl bei diesen beiden Auth-Arten faelschlich immer auf den
    /// Verbinden-Dialog zurückfallen, auch wenn direkt verbunden werden könnte.
    /// </summary>
    private static bool HasRequiredSecret(ConnectionProfile profile) => profile.Authentication switch
    {
        CmisAuthenticationType.None => true,
        CmisAuthenticationType.OAuthBearer => !string.IsNullOrEmpty(profile.BearerToken),
        _ => !string.IsNullOrEmpty(profile.Password)
    };

    /// <summary>
    /// Hängt die Oberflächen-Beiträge der Plugins ein: kontextbezogene Ribbon-Tabs und
    /// Schaltflächen auf dem Start-Tab. Wird von <see cref="App"/> vor
    /// <see cref="Window.Show"/> aufgerufen.
    ///
    /// <para>Fehler eines einzelnen Beitrags werden protokolliert und übersprungen —
    /// ein defektes Plugin darf das Hauptfenster nicht verhindern.</para>
    /// </summary>
    public void ApplyPluginContributions(
        IEnumerable<CMISPilot.Plugins.PluginContributions> contributions, ILogger logger)
    {
        foreach (var contribution in contributions)
        {
            foreach (var tab in contribution.RibbonTabs)
            {
                TryAddPluginRibbonTab(tab, logger);
            }

            foreach (var command in contribution.DocumentCommands)
            {
                TryAddPluginStartButton(command, logger);
            }
        }
    }

    /// <summary>
    /// Baut den kontextbezogenen Ribbon-Tab eines Plugins auf. Die Gruppe und die an
    /// <see cref="WorkspaceViewModel.ActiveContextTabKey"/> gebundene Sichtbarkeit
    /// setzt der Host: eine <c>ElementName</c>-Bindung ins Hauptfenster trägt aus
    /// einem separat geladenen Wörterbuch heraus nicht.
    /// </summary>
    private void TryAddPluginRibbonTab(CMISPilot.Plugins.RibbonTabContribution contribution, ILogger logger)
    {
        try
        {
            var dictionary = new ResourceDictionary { Source = contribution.ResourceDictionary };

            if (dictionary[contribution.ResourceKey] is not RibbonTabItem tab)
            {
                logger.LogError(
                    "Ribbon-Tab \"{Key}\" fehlt in {Uri} oder ist kein RibbonTabItem",
                    contribution.ResourceKey, contribution.ResourceDictionary);
                return;
            }

            var brush = new System.Windows.Media.SolidColorBrush(contribution.AccentColor);
            brush.Freeze();

            var group = new RibbonContextualTabGroup
            {
                Header = contribution.GroupHeader,
                Background = brush,
                BorderBrush = brush,
                Visibility = Visibility.Collapsed
            };

            // Dieselbe Bindung wie bei den fest verdrahteten Tabs: sichtbar, solange
            // ActiveContextTabKey zum Schlüssel des Plugins passt.
            var visibility = new Binding(nameof(WorkspaceViewModel.ActiveContextTabKey))
            {
                Source = _workspace,
                Converter = (IValueConverter)Resources["StringEqualsToVis"],
                ConverterParameter = contribution.ContextTabKey
            };

            group.SetBinding(UIElement.VisibilityProperty, visibility);
            tab.SetBinding(UIElement.VisibilityProperty, visibility);

            MainRibbon.ContextualGroups.Add(group);
            tab.Group = group;
            MainRibbon.Tabs.Add(tab);

            _pluginRibbonTabs[contribution.ContextTabKey] = tab;

            logger.LogDebug(
                "Plugin-Ribbon-Tab \"{Header}\" für Kontext \"{Key}\" eingehängt",
                contribution.GroupHeader, contribution.ContextTabKey);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ribbon-Tab eines Plugins konnte nicht eingehängt werden");
        }
    }

    /// <summary>Hängt eine Plugin-Schaltfläche in die Gruppe „Erweiterungen" des Start-Tabs.</summary>
    private void TryAddPluginStartButton(CMISPilot.Plugins.DocumentCommandContribution contribution, ILogger logger)
    {
        try
        {
            var button = new Fluent.Button { Header = contribution.Header };

            if (contribution.ToolTip is { Length: > 0 } tip)
            {
                button.ToolTip = tip;
            }

            // IconResourceKey ist ein zur Bauzeit ungeprueftes Zeichenkette-auf-
            // Schluessel-Nachschlagen (P1.4) - ein Tippfehler im Plugin faellt sonst
            // erst optisch auf (Schaltflaeche ohne Icon), deshalb hier protokollieren.
            if (contribution.IconResourceKey is { Length: > 0 } iconKey)
            {
                if (Application.Current.TryFindResource(iconKey) is { } icon)
                {
                    button.Icon = icon;
                }
                else
                {
                    logger.LogWarning(
                        "Plugin-Schaltfläche \"{Header}\": Icon-Ressource \"{IconKey}\" nicht gefunden",
                        contribution.Header, iconKey);
                }
            }

            button.Click += (_, _) =>
            {
                try
                {
                    // Liefert die Fabrik null, bricht das Öffnen ab (z. B. abgebrochener
                    // Datei-Dialog) — dann entsteht bewusst kein leerer Tab.
                    if (contribution.CreateDocument() is { } document)
                    {
                        _workspace.OpenDocument(document);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Plugin-Dokument \"{Header}\" konnte nicht geöffnet werden", contribution.Header);
                }
            };

            PluginToolsGroup.Items.Add(button);
            PluginToolsGroup.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Plugin-Schaltfläche \"{Header}\" konnte nicht eingehängt werden", contribution.Header);
        }
    }
}
