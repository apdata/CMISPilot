using IFileLauncher = APX.Wpf.Shell.ViewModels.Contracts.IFileLauncher;
using APX.Wpf.Shell.ViewModels;
using APX.Wpf.Shell.ViewModels.Logging;
using APX.Wpf.Shell.ViewModels.Workspace;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CMISPilot.Cmis.Contracts;
using CMISPilot.Cmis.Errors;
using CMISPilot.Cmis.Models;
using CMISPilot.ViewModels.Dialogs;
using CMISPilot.ViewModels.Export;
using CMISPilot.ViewModels.Messages;
using CMISPilot.ViewModels.Shell;
using CMISPilot.ViewModels.Workspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

namespace CMISPilot.ViewModels.Explorer;

/// <summary>
/// Dokument-Tab „Explorer" (R4 Etappe 3/4): zeigt die Objektliste (Kinder) eines im
/// Server-Baum geöffneten Ordners samt Breadcrumb. Es gibt bewusst genau einen
/// Explorer-Tab (feste <see cref="ContentIdConst"/>): ein Klick auf einen Ordner im
/// Server-Baum navigiert diesen Tab in-place zum gewählten Ordner
/// (<see cref="NavigateTo"/>), wie der Inhaltsbereich des Windows-Explorers. Ist der
/// Tab geschlossen, öffnet der Klick ihn neu; ist ein anderer Tab aktiv, wird auf den
/// Explorer-Tab gewechselt (Steuerung in <c>MainWindow.xaml.cs</c>).
///
/// Die Auswahl in der Objektliste (<see cref="SelectedObject"/>) wird wie die
/// Baum-Selektion als <see cref="NodeSelectedMessage"/> gemeldet, damit das
/// Eigenschaften-Werkzeugfenster auch der Listenauswahl folgt (Konzept §4.3).
///
/// Seit R4 Etappe 4 trägt dieser Tab zusätzlich die CRUD-Kommandos (Logik aus
/// <c>ExplorerAreaViewModel</c> (entfernt) übernommen, FA-70/71/72/73/74/75, FA-40/41/42):
/// „inspiziertes Objekt" ist <see cref="SelectedObject"/> (die Listen-Auswahl), der
/// Ordner des Tabs (<see cref="Folder"/>) ist der Zielordner für Neu-Anlegen. Anders
/// als die alte Sidebar-App (die per <see cref="NotificationRequestMessage"/> eine
/// Shell-InfoBar füttert, die es in der neuen Shell noch nicht gibt) meldet dieser
/// Tab Erfolg/Fehler über einen injizierten <see cref="ILogger{TCategoryName}"/> —
/// die Meldungen landen dadurch automatisch in Ausgabe/Fehlerliste (R3).
///
/// Erzeugung erfolgt bewusst nicht über den DI-Container (der Ordner ist ein
/// Laufzeit-Parameter), sondern direkt in der Shell (<c>MainWindow.xaml.cs</c>),
/// die die benötigten Dienste aus dem <c>IServiceProvider</c> holt und den
/// ViewModel-Konstruktor selbst aufruft (einfachster Weg ohne zusätzliche
/// Factory-Abstraktion).
///
/// Bewusst WPF-frei (NFA-03); Serveraufrufe laufen async über
/// <see cref="ViewModelBase.BeginBusy"/>, nie <c>.Result</c>/<c>.Wait()</c> (NFA-13).
/// </summary>
public sealed partial class ExplorerDocumentViewModel : DocumentViewModelBase
{
    private readonly IBrowseService _browseService;
    private readonly IObjectService _objectService;
    private readonly ITypeService _typeService;
    private readonly IDialogService _dialogService;
    private readonly IFileLauncher _fileLauncher;
    private readonly IMessenger _messenger;
    private readonly ILogger<ExplorerDocumentViewModel> _logger;
    private readonly IListExporter _listExporter;

    /// <param name="folder">Der anzuzeigende Ordner (zugleich Zielordner für Neu-Anlegen).</param>
    /// <param name="browseService">Lädt Kinder und Elternordner des Ordners.</param>
    /// <param name="objectService">Schreibende CMIS-Operationen (Anlegen/Bearbeiten/Löschen/Inhalte).</param>
    /// <param name="typeService">Lädt die erzeugbaren Objekttypen für den Anlegen-Dialog.</param>
    /// <param name="dialogService">Zeigt den Bearbeiten-/Anlegen-Dialog und Rückfragen.</param>
    /// <param name="fileLauncher">Öffnet heruntergeladene Dateien mit dem Standardprogramm.</param>
    /// <param name="messenger">Meldet die Listen-Selektion an das Eigenschaften-Werkzeugfenster.</param>
    /// <param name="logger">Meldet Erfolg/Fehler der Schreiboperationen (landet in Ausgabe/Fehlerliste).</param>
    /// <param name="listExporter">Schreibt die Objektliste als Excel-Datei (F3).</param>
    public ExplorerDocumentViewModel(
        CmisObjectDto folder,
        IBrowseService browseService,
        IObjectService objectService,
        ITypeService typeService,
        IDialogService dialogService,
        IFileLauncher fileLauncher,
        IMessenger messenger,
        ILogger<ExplorerDocumentViewModel> logger,
        IListExporter listExporter)
        : base(ContentIdConst)
    {
        _browseService = browseService;
        _objectService = objectService;
        _typeService = typeService;
        _dialogService = dialogService;
        _fileLauncher = fileLauncher;
        _messenger = messenger;
        _logger = logger;
        _listExporter = listExporter;

        Folder = folder;
        Title = folder.Name ?? folder.Id;

        // F3: der Export haengt daran, dass die Objektliste gefuellt ist.
        Objects.CollectionChanged += (_, _) => ExportListCommand.NotifyCanExecuteChanged();

        _ = LoadAsync(CancellationToken.None);
    }

    /// <summary>
    /// Feste <see cref="DocumentViewModelBase.ContentId"/> aller Explorer-Tabs. Anders
    /// als frueher (eine ContentId je Ordner) gibt es bewusst genau <b>einen</b>
    /// Explorer-Tab, der wie der Inhaltsbereich des Windows-Explorers beim Klick im
    /// Baum in-place zum gewaehlten Ordner navigiert (siehe <see cref="NavigateTo"/>).
    /// </summary>
    public const string ContentIdConst = "explorer";

    /// <summary>Der Ordner, dessen Inhalt dieser Tab zeigt (Zielordner für Neu-Anlegen).</summary>
    public CmisObjectDto Folder { get; private set; }

    // Laufende Nummer der Navigation. Da der Tab in-place navigiert (NavigateTo) und
    // LoadAsync zwischen den Serveraufrufen await-Punkte hat, koennen bei schnellem
    // Wechsel durch mehrere Ordner mehrere Ladevorgaenge nebenlaeufig sein. Der Guard
    // sorgt dafuer, dass nur der jeweils juengste Vorgang Objektliste/Breadcrumb
    // schreibt und ein aelterer, spaeter zurueckkehrender Aufruf sein Ergebnis verwirft.
    private int _navigationVersion;

    /// <summary>
    /// Navigiert den Tab zu einem anderen Ordner (Windows-Explorer-artig, in-place):
    /// setzt Ordner und Titel neu und laedt Objektliste samt Breadcrumb. Die
    /// Listen-Auswahl wird zurueckgesetzt, damit das Eigenschaften-Fenster nicht auf
    /// ein Objekt aus dem vorigen Ordner zeigt.
    /// </summary>
    public void NavigateTo(CmisObjectDto folder)
    {
        Folder = folder;
        Title = folder.Name ?? folder.Id;
        SelectedObject = null;
        OnPropertyChanged(nameof(Folder));
        _ = LoadAsync(CancellationToken.None);
    }

    /// <inheritdoc />
    public override string? ContextTabKey => "explorer";

    /// <summary>Direkte Kinder (Ordner + Dokumente) des Ordners.</summary>
    public ObservableCollection<CmisObjectDto> Objects { get; } = new();

    /// <summary>Breadcrumb vom Elternordner (falls vorhanden) bis zum aktuellen Ordner.</summary>
    public ObservableCollection<CmisObjectDto> PathSegments { get; } = new();

    /// <summary>Das aktuell in der Objektliste gewählte Objekt ("inspiziertes Objekt" für CRUD).</summary>
    [ObservableProperty]
    private CmisObjectDto? _selectedObject;

    partial void OnSelectedObjectChanged(CmisObjectDto? value)
    {
        _messenger.Send(new NodeSelectedMessage(value));
        NotifyCommandsCanExecuteChanged();
    }

    /// <summary>Lädt Objektliste und Breadcrumb des Ordners.</summary>
    private async Task LoadAsync(CancellationToken ct)
    {
        // Diese Navigation markieren und den Zielordner lokal festhalten, damit sich der
        // Ladevorgang nicht auf die (nach await ggf. schon weiter navigierte) Folder-
        // Eigenschaft stuetzt. Nach jedem await pruefen, ob inzwischen eine neuere
        // Navigation laeuft; dann Ergebnis verwerfen, statt einen anderen Ordner anzuzeigen.
        var version = ++_navigationVersion;
        var folder = Folder;

        using (BeginBusy())
        {
            try
            {
                var children = await _browseService.GetChildrenAsync(folder.Id, ct).ConfigureAwait(true);
                if (version != _navigationVersion)
                {
                    return;
                }

                Objects.Clear();
                foreach (var child in children)
                {
                    Objects.Add(child);
                }

                var parents = await _browseService.GetParentsAsync(folder.Id, ct).ConfigureAwait(true);
                if (version != _navigationVersion)
                {
                    return;
                }

                PathSegments.Clear();
                foreach (var parent in parents)
                {
                    PathSegments.Add(parent);
                }

                PathSegments.Add(folder);
            }
            catch (OperationCanceledException)
            {
            }
            catch (CmisAppException)
            {
                // Objektliste/Breadcrumb bleiben leer; der Fehler wird nicht separat
                // gemeldet, da der Tab bereits geöffnet ist (kein Blocker fürs Ansehen
                // des sonstigen Werkbank-Zustands).
            }
        }
    }

    // --- CRUD-Kommandos (R4 Etappe 4, aus ExplorerAreaViewModel übernommen) ---

    /// <summary>Legt einen Ordner im Ordner dieses Tabs an (FA-70).</summary>
    [RelayCommand(CanExecute = nameof(CanNewFolder))]
    private async Task NewFolderAsync(CancellationToken ct)
    {
        var types = await LoadCreatableTypesAsync(CmisBaseType.Folder, "cmis:folder", ct).ConfigureAwait(true);
        var dialogVm = EditPropertiesViewModel.ForCreate("Neuer Ordner", types, "cmis:folder");
        if (!await _dialogService.ShowEditPropertiesAsync(dialogVm).ConfigureAwait(true))
        {
            return;
        }

        var name = dialogVm.Name;
        var success = false;
        using (BeginBusy())
        {
            try
            {
                await _objectService.CreateFolderAsync(Folder.Id, dialogVm.BuildProperties(), ct)
                    .ConfigureAwait(true);
                success = true;
            }
            catch (OperationCanceledException)
            {
            }
            catch (CmisAppException ex)
            {
                _logger.LogError("Anlegen fehlgeschlagen: {Message}", Describe(ex));
            }
        }

        if (success)
        {
            _logger.LogInformation("Der Ordner \"{Name}\" wurde angelegt.", name);
            await ReloadObjectsAsync(ct).ConfigureAwait(true);
        }
    }

    private bool CanNewFolder() => !IsBusy && Allows(Folder, "CanCreateFolder");

    /// <summary>Legt ein Dokument (ohne Inhalt) im Ordner dieses Tabs an (FA-71).</summary>
    [RelayCommand(CanExecute = nameof(CanNewDocument))]
    private async Task NewDocumentAsync(CancellationToken ct)
    {
        var types = await LoadCreatableTypesAsync(CmisBaseType.Document, "cmis:document", ct).ConfigureAwait(true);
        var dialogVm = EditPropertiesViewModel.ForCreate("Neues Dokument", types, "cmis:document");
        if (!await _dialogService.ShowEditPropertiesAsync(dialogVm).ConfigureAwait(true))
        {
            return;
        }

        await CreateDocumentFromDialogAsync(dialogVm, ct).ConfigureAwait(true);
    }

    private bool CanNewDocument() => !IsBusy && Allows(Folder, "CanCreateDocument");

    /// <summary>
    /// Legt ein Dokument aus einer per Drag&amp;Drop uebergebenen Datei an: oeffnet
    /// denselben "Neues Dokument"-Dialog wie <see cref="NewDocumentAsync"/>, aber mit
    /// bereits vorbelegter <see cref="EditPropertiesViewModel.ContentFilePath"/> (F1),
    /// wodurch der Dialog den Dateinamen automatisch als cmis:name vorschlaegt.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanNewDocument))]
    private async Task NewDocumentFromFileAsync(string filePath, CancellationToken ct)
    {
        var types = await LoadCreatableTypesAsync(CmisBaseType.Document, "cmis:document", ct).ConfigureAwait(true);
        var dialogVm = EditPropertiesViewModel.ForCreate("Neues Dokument", types, "cmis:document");
        dialogVm.ContentFilePath = filePath;
        if (!await _dialogService.ShowEditPropertiesAsync(dialogVm).ConfigureAwait(true))
        {
            return;
        }

        await CreateDocumentFromDialogAsync(dialogVm, ct).ConfigureAwait(true);
    }

    /// <summary>Gemeinsamer Anlegen-Ablauf fuer <see cref="NewDocumentAsync"/> und <see cref="NewDocumentFromFileAsync"/>.</summary>
    private async Task CreateDocumentFromDialogAsync(EditPropertiesViewModel dialogVm, CancellationToken ct)
    {
        var name = dialogVm.Name;
        var properties = dialogVm.BuildProperties();
        var contentPath = dialogVm.ContentFilePath;
        var success = false;
        using (BeginBusy())
        {
            try
            {
                // F1: Wurde im Dialog eine Datei gewaehlt, wird sie gleich als
                // Content-Stream mitgespeichert, statt das Dokument leer anzulegen und
                // den Inhalt anschliessend ueber "Inhalt setzen" nachzureichen.
                if (!string.IsNullOrEmpty(contentPath) && File.Exists(contentPath))
                {
                    await using var fileStream = File.OpenRead(contentPath);
                    await _objectService.CreateDocumentAsync(
                        Folder.Id, properties, fileStream,
                        Path.GetFileName(contentPath), GuessMimeType(contentPath), ct)
                        .ConfigureAwait(true);
                }
                else
                {
                    await _objectService.CreateDocumentAsync(Folder.Id, properties, ct: ct)
                        .ConfigureAwait(true);
                }

                success = true;
            }
            catch (OperationCanceledException)
            {
            }
            catch (IOException ex)
            {
                // Die lokale Datei liess sich nicht lesen (gesperrt, geloescht, keine Rechte).
                _logger.LogError("Anlegen fehlgeschlagen: Die Datei konnte nicht gelesen werden ({Message}).", ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError("Anlegen fehlgeschlagen: Kein Zugriff auf die Datei ({Message}).", ex.Message);
            }
            catch (CmisAppException ex)
            {
                _logger.LogError("Anlegen fehlgeschlagen: {Message}", Describe(ex));
            }
        }

        if (success)
        {
            _logger.LogInformation("Das Dokument \"{Name}\" wurde angelegt.", name);
            await ReloadObjectsAsync(ct).ConfigureAwait(true);
        }
    }

    /// <summary>Öffnet den separaten Bearbeiten-Dialog für das inspizierte Objekt (FA-72).</summary>
    [RelayCommand(CanExecute = nameof(CanEdit))]
    private async Task EditAsync(CancellationToken ct)
    {
        var target = SelectedObject;
        if (target is null)
        {
            return;
        }

        var dialogVm = EditPropertiesViewModel.ForEdit(target);
        if (!await _dialogService.ShowEditPropertiesAsync(dialogVm).ConfigureAwait(true))
        {
            return;
        }

        var name = dialogVm.Name;
        var success = false;
        using (BeginBusy())
        {
            try
            {
                await _objectService.UpdatePropertiesAsync(target.Id, dialogVm.BuildProperties(), ct)
                    .ConfigureAwait(true);
                success = true;
            }
            catch (OperationCanceledException)
            {
            }
            catch (CmisAppException ex)
            {
                _logger.LogError("Speichern fehlgeschlagen: {Message}", Describe(ex));
            }
        }

        if (success)
        {
            _logger.LogInformation("Die Eigenschaften von \"{Name}\" wurden aktualisiert.", name);
            await ReloadObjectsAsync(ct).ConfigureAwait(true);
        }
    }

    private bool CanEdit() => !IsBusy && SelectedObject is { } obj && Allows(obj, "CanUpdateProperties");

    /// <summary>
    /// Benennt das inspizierte Objekt um: CMIS kennt kein eigenes "Rename", das ist
    /// schlicht ein <c>updateProperties</c>-Aufruf auf <c>cmis:name</c>. Zeigt dafuer
    /// einen schlanken Dialog (F2/Kontextmenue "Umbenennen" in MainWindow.xaml.cs) -
    /// ein zunaechst versuchtes Inline-Umbenennen der Name-Spalte liess sich nicht
    /// zuverlaessig wieder verlassen (siehe CLAUDE.md, Fallstricke). Dieselbe
    /// Berechtigung wie <see cref="EditAsync"/>.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanEdit))]
    private async Task RenameAsync(CancellationToken ct)
    {
        var target = SelectedObject;
        if (target is null)
        {
            return;
        }

        var newName = await _dialogService.ShowRenameDialogAsync(target.Name ?? string.Empty).ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(newName) || newName == target.Name)
        {
            return;
        }

        var success = false;
        using (BeginBusy())
        {
            try
            {
                await _objectService.UpdatePropertiesAsync(
                    target.Id, new Dictionary<string, object?> { ["cmis:name"] = newName }, ct)
                    .ConfigureAwait(true);
                success = true;
            }
            catch (OperationCanceledException)
            {
            }
            catch (CmisAppException ex)
            {
                _logger.LogError("Umbenennen fehlgeschlagen: {Message}", Describe(ex));
            }
        }

        if (success)
        {
            _logger.LogInformation("\"{OldName}\" wurde in \"{NewName}\" umbenannt.", target.Name, newName);
            await ReloadObjectsAsync(ct).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Löscht das inspizierte Objekt nach Rückfrage (FA-74); für nicht-leere Ordner über
    /// <c>deleteTree</c>.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteAsync(CancellationToken ct)
    {
        var target = SelectedObject;
        if (target is null)
        {
            return;
        }

        var confirmed = await _dialogService.ConfirmAsync(
            "Löschen bestätigen",
            target.IsFolder
                ? $"Soll der Ordner \"{target.Name}\" inklusive Inhalt endgültig gelöscht werden?"
                : $"Soll \"{target.Name}\" endgültig gelöscht werden?").ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        var name = target.Name;
        var success = false;
        using (BeginBusy())
        {
            try
            {
                if (target.IsFolder)
                {
                    await _objectService.DeleteTreeAsync(target.Id, ct: ct).ConfigureAwait(true);
                }
                else
                {
                    await _objectService.DeleteAsync(target.Id, ct: ct).ConfigureAwait(true);
                }

                success = true;
            }
            catch (OperationCanceledException)
            {
            }
            catch (CmisAppException ex)
            {
                _logger.LogError("Löschen fehlgeschlagen: {Message}", Describe(ex));
            }
        }

        if (success)
        {
            _logger.LogInformation("\"{Name}\" wurde gelöscht.", name);
            SelectedObject = null;
            await ReloadObjectsAsync(ct).ConfigureAwait(true);
        }
    }

    private bool CanDelete() =>
        !IsBusy && SelectedObject is { } obj &&
        Allows(obj, obj.IsFolder ? new[] { "CanDeleteTree", "CanDeleteObject" } : new[] { "CanDeleteObject" });

    // --- Dokumentinhalte-Kommandos (FA-40/41/42/73) ---

    /// <summary>
    /// Lädt den Content-Stream des inspizierten Dokuments und speichert ihn lokal
    /// unter einem über <see cref="IDialogService.PickSaveFileAsync"/> gewählten
    /// Zielpfad (FA-40).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDownload))]
    private async Task DownloadAsync(CancellationToken ct)
    {
        var target = SelectedObject;
        if (target is null)
        {
            return;
        }

        var suggestedName = target.ContentStreamFileName ?? target.Name ?? target.Id;
        var path = await _dialogService.PickSaveFileAsync(suggestedName).ConfigureAwait(true);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        var success = false;
        using (BeginBusy())
        {
            try
            {
                await DownloadToAsync(target.Id, path, ct).ConfigureAwait(true);
                success = true;
            }
            catch (OperationCanceledException)
            {
            }
            catch (CmisAppException ex)
            {
                _logger.LogError("Herunterladen fehlgeschlagen: {Message}", Describe(ex));
            }
        }

        if (success)
        {
            _logger.LogInformation("\"{Name}\" wurde gespeichert.", target.Name);
        }
    }

    private bool CanDownload() =>
        !IsBusy && SelectedObject is { } obj && obj.IsDocument && Allows(obj, "CanGetContentStream");

    /// <summary>
    /// Lädt den Content-Stream des inspizierten Dokuments in eine temporäre Datei
    /// und öffnet ihn anschließend mit dem Standardprogramm des Betriebssystems
    /// (FA-41). Der Download-Fehlerpfad läuft identisch zu <see cref="DownloadAsync"/>.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanOpen))]
    private async Task OpenAsync(CancellationToken ct)
    {
        var target = SelectedObject;
        if (target is null)
        {
            return;
        }

        var fileName = target.ContentStreamFileName ?? target.Name ?? target.Id;
        var tempDir = Path.Combine(Path.GetTempPath(), "CMISPilot", target.Id);
        var path = Path.Combine(tempDir, fileName);

        var success = false;
        using (BeginBusy())
        {
            try
            {
                Directory.CreateDirectory(tempDir);
                await DownloadToAsync(target.Id, path, ct).ConfigureAwait(true);
                success = true;
            }
            catch (OperationCanceledException)
            {
            }
            catch (CmisAppException ex)
            {
                _logger.LogError("Öffnen fehlgeschlagen: {Message}", Describe(ex));
            }
        }

        if (success)
        {
            _fileLauncher.Launch(path);
        }
    }

    private bool CanOpen() =>
        !IsBusy && SelectedObject is { } obj && obj.IsDocument && Allows(obj, "CanGetContentStream");

    /// <summary>
    /// Setzt bzw. ersetzt den Inhalt des inspizierten Dokuments mit einer über
    /// <see cref="IDialogService.PickOpenFileAsync"/> gewählten lokalen Datei
    /// (FA-42/73) und aktualisiert danach die Objektliste.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSetContent))]
    private async Task SetContentAsync(CancellationToken ct)
    {
        var target = SelectedObject;
        if (target is null)
        {
            return;
        }

        var path = await _dialogService.PickOpenFileAsync().ConfigureAwait(true);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        var success = false;
        using (BeginBusy())
        {
            try
            {
                await using var fileStream = File.OpenRead(path);
                await _objectService.SetContentStreamAsync(
                    target.Id, fileStream, Path.GetFileName(path), GuessMimeType(path), ct: ct)
                    .ConfigureAwait(true);
                success = true;
            }
            catch (OperationCanceledException)
            {
            }
            catch (CmisAppException ex)
            {
                _logger.LogError("Inhalt setzen fehlgeschlagen: {Message}", Describe(ex));
            }
        }

        if (success)
        {
            _logger.LogInformation("Der Inhalt von \"{Name}\" wurde ersetzt.", target.Name);
            await ReloadObjectsAsync(ct).ConfigureAwait(true);
        }
    }

    private bool CanSetContent() =>
        !IsBusy && SelectedObject is { } obj && obj.IsDocument && Allows(obj, "CanSetContentStream");

    // --- Export (F3) ---

    /// <summary>
    /// Exportiert die aktuelle Objektliste des Ordners als Excel-Datei (F3): eine Zeile
    /// je Kindobjekt mit Name, Art, Typ, Groesse und den Zeitstempeln. Fragt ueber den
    /// Dialog-Dienst einen Zielpfad ab und meldet Erfolg/Fehler ueber den Logger.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExportList))]
    private async Task ExportListAsync(CancellationToken ct)
    {
        if (Objects.Count == 0)
        {
            return;
        }

        var suggestedName = $"{Sanitize(Folder.Name ?? Folder.Id)}.xlsx";
        var path = await _dialogService.PickSaveFileAsync(suggestedName).ConfigureAwait(true);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        // Endung sicherstellen (der generische Speichern-Dialog erzwingt sie nicht).
        if (!path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            path += ".xlsx";
        }

        // Ordner und Liste festhalten: der Export laeuft async, in der Zwischenzeit
        // koennte der Tab bereits zu einem anderen Ordner navigiert sein.
        var folder = Folder;
        var objects = Objects.ToList();

        using (BeginBusy())
        {
            try
            {
                await _listExporter.ExportObjectListAsync(folder, objects, path, ct).ConfigureAwait(true);
                _logger.LogInformation(
                    "Inhalt von \"{Name}\" ({Count} Objekt(e)) wurde nach \"{Path}\" exportiert.",
                    folder.Name ?? folder.Id, objects.Count, path);
            }
            catch (OperationCanceledException)
            {
            }
            catch (IOException ex)
            {
                _logger.LogError("Export fehlgeschlagen: Die Datei konnte nicht geschrieben werden ({Message}).", ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError("Export fehlgeschlagen: Kein Zugriff auf die Zieldatei ({Message}).", ex.Message);
            }
        }
    }

    private bool CanExportList() => !IsBusy && Objects.Count > 0;

    /// <summary>Ersetzt in einem Vorschlags-Dateinamen die unter Windows unzulaessigen Zeichen.</summary>
    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return name;
    }

    /// <summary>
    /// Lädt die erzeugbaren Objekttypen eines Basistyps (z. B. alle von <c>cmis:folder</c>
    /// abgeleiteten, erzeugbaren Typen) inkl. ihrer Property-Definitionen — Basis für die
    /// Typ-Auswahl und die Pflichtfeld-Abfrage im Anlegen-Dialog (FA-71). Schlägt das Laden
    /// fehl (oder liefert nichts), wird ein minimaler Fallback mit nur dem Basistyp
    /// zurückgegeben, damit „Neuer Ordner/Neues Dokument" weiter funktioniert.
    /// </summary>
    private async Task<IReadOnlyList<TypeDefinitionDto>> LoadCreatableTypesAsync(
        CmisBaseType baseType, string baseTypeId, CancellationToken ct)
    {
        using (BeginBusy())
        {
            try
            {
                var tree = await _typeService.GetTypeTreeAsync(includePropertyDefinitions: true, ct)
                    .ConfigureAwait(true);

                var creatable = Flatten(tree)
                    .Where(t => t.BaseType == baseType && t.IsCreatable != false)
                    .OrderBy(t => t.DisplayName ?? t.Id, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

                if (creatable.Count > 0)
                {
                    return creatable;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (CmisAppException ex)
            {
                _logger.LogWarning("Typen konnten nicht geladen werden: {Message}", Describe(ex));
            }
        }

        // Fallback: nur der Basistyp (ohne zusätzliche Pflichtfelder).
        return new[]
        {
            new TypeDefinitionDto { Id = baseTypeId, DisplayName = baseTypeId, BaseType = baseType }
        };
    }

    /// <summary>Plättet den (verschachtelten) Typbaum in eine flache Liste.</summary>
    private static IEnumerable<TypeDefinitionDto> Flatten(IEnumerable<TypeDefinitionDto> types)
    {
        foreach (var t in types)
        {
            yield return t;
            foreach (var child in Flatten(t.Children))
            {
                yield return child;
            }
        }
    }

    /// <summary>Lädt den Content-Stream via <see cref="IObjectService"/> und schreibt ihn nach <paramref name="path"/>.</summary>
    private async Task DownloadToAsync(string objectId, string path, CancellationToken ct)
    {
        using var content = await _objectService.GetContentStreamAsync(objectId, ct).ConfigureAwait(true);
        await using var target = File.Create(path);
        await content.Stream.CopyToAsync(target, ct).ConfigureAwait(true);
    }

    /// <summary>
    /// Sehr einfache Dateiendung-zu-MIME-Zuordnung für <c>SetContentStreamAsync</c>
    /// (FA-42/73). Unbekannte Endungen fallen auf <c>application/octet-stream</c>
    /// zurück (der Server bzw. <c>ObjectService</c> tut dies ohnehin).
    /// </summary>
    private static string GuessMimeType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".txt" => "text/plain",
        ".csv" => "text/csv",
        ".xml" => "application/xml",
        ".json" => "application/json",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ".zip" => "application/zip",
        _ => "application/octet-stream"
    };

    /// <summary>
    /// Prüft, ob mindestens eine der übergebenen Allowable Actions erlaubt ist (FA-75).
    /// Sind die Allowable Actions nicht geladen (<c>null</c>), wird nicht eingeschränkt
    /// (fail-open) — der Server weist eine tatsächlich unzulässige Operation ohnehin
    /// über <see cref="CmisAppException"/> zurück.
    /// </summary>
    private static bool Allows(CmisObjectDto obj, params string[] anyOf)
    {
        if (obj.AllowableActions is null)
        {
            return true;
        }

        return anyOf.Any(a => obj.AllowableActions.Contains(a, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Lädt die Objektliste (ohne Breadcrumb, der bleibt unverändert) neu, z. B. nach einer Schreiboperation.</summary>
    private async Task ReloadObjectsAsync(CancellationToken ct)
    {
        using (BeginBusy())
        {
            try
            {
                var children = await _browseService.GetChildrenAsync(Folder.Id, ct).ConfigureAwait(true);
                Objects.Clear();
                foreach (var child in children)
                {
                    Objects.Add(child);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (CmisAppException ex)
            {
                _logger.LogError("Inhalt konnte nicht neu geladen werden: {Message}", Describe(ex));
            }
        }
    }

    /// <summary>Benachrichtigt alle CRUD-Kommandos über eine mögliche Änderung ihres CanExecute.</summary>
    private void NotifyCommandsCanExecuteChanged()
    {
        NewFolderCommand.NotifyCanExecuteChanged();
        NewDocumentCommand.NotifyCanExecuteChanged();
        NewDocumentFromFileCommand.NotifyCanExecuteChanged();
        EditCommand.NotifyCanExecuteChanged();
        RenameCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        DownloadCommand.NotifyCanExecuteChanged();
        OpenCommand.NotifyCanExecuteChanged();
        SetContentCommand.NotifyCanExecuteChanged();
        ExportListCommand.NotifyCanExecuteChanged();
    }

    // IsBusy liegt in der Basisklasse per [ObservableProperty]; der generierte
    // OnIsBusyChanged-Hook lässt sich hier nicht implementieren – daher über
    // OnPropertyChanged (M3-Referenzmuster, siehe ExplorerAreaViewModel).
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(IsBusy))
        {
            NotifyCommandsCanExecuteChanged();
        }
    }

    private static string Describe(CmisAppException ex) => ex.Kind switch
    {
        CmisErrorKind.Authentication => "Anmeldung fehlgeschlagen. Bitte erneut verbinden.",
        CmisErrorKind.Network => "Server nicht erreichbar. Bitte Verbindung prüfen.",
        CmisErrorKind.NotFound => "Ordner oder Objekt nicht gefunden.",
        CmisErrorKind.InvalidArgument => ex.Message,
        CmisErrorKind.PermissionDenied => "Zugriff verweigert. Fehlende Berechtigungen.",
        CmisErrorKind.NotSupported => "Die Operation wird vom Server nicht unterstützt.",
        _ => string.IsNullOrWhiteSpace(ex.Message) ? "Unerwarteter Serverfehler." : ex.Message
    };
}
