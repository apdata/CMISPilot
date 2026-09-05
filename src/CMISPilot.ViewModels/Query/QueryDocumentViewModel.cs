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
using CMISPilot.ViewModels.Export;
using CMISPilot.ViewModels.Messages;
using CMISPilot.ViewModels.Shell;
using CMISPilot.ViewModels.Workspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

namespace CMISPilot.ViewModels.Query;

/// <summary>
/// Dokument-Tab „Abfrage" (R5.1): führt eine vom Nutzer eingegebene CMISQL-Abfrage
/// aus (FA-50) und liefert das Ergebnis als Tabelle mit dynamischen Spalten (FA-51).
/// Logik aus <c>QueryAreaViewModel</c> (entfernt) (M6) übernommen und nach dem in
/// R4 etablierten Muster (<see cref="Explorer.ExplorerDocumentViewModel"/>) auf
/// <see cref="DocumentViewModelBase"/> umgestellt.
///
/// Anders als die Alt-App meldet dieser Tab Erfolg/Fehler über einen injizierten
/// <see cref="ILogger{TCategoryName}"/> statt einer <see cref="NotificationRequestMessage"/>
/// an die (in der neuen Shell noch nicht vorhandene) Shell-InfoBar — die Meldungen
/// landen dadurch in Ausgabe/Fehlerliste (R3).
///
/// Singleton-Registrierung (DI): anders als der Explorer-Tab braucht dieser Tab
/// keinen Laufzeit-Parameter, die feste <see cref="ContentId"/> „query" dedupliziert
/// beim erneuten Öffnen ohnehin (<see cref="WorkspaceViewModel.OpenDocument"/>).
///
/// Bewusst WPF-frei (NFA-03); Serveraufrufe laufen async über
/// <see cref="Shell.ViewModelBase.BeginBusy"/>, nie <c>.Result</c>/<c>.Wait()</c> (NFA-13).
/// </summary>
public sealed partial class QueryDocumentViewModel : DocumentViewModelBase,
    IRecipient<ConnectionStateChangedMessage>
{
    /// <summary>
    /// Dateifilter fuer den Laden-/Speichern-Dialog: zeigt zuerst nur *.cmisql.
    /// </summary>
    private const string CmisqlFileFilter = "CMISQL-Abfragen (*.cmisql)|*.cmisql|Alle Dateien (*.*)|*.*";

    private readonly IQueryService _queryService;
    private readonly ISessionContext _sessionContext;
    private readonly IMessenger _messenger;
    private readonly ILogger<QueryDocumentViewModel> _logger;
    private readonly IDialogService _dialogService;
    private readonly IListExporter _listExporter;

    // Analog ServerTree/Types: die neue Shell sendet keine ConnectionStateChangedMessage,
    // sondern verbindet direkt ueber IConnectionService (ConnectionChanged aus dem
    // ThreadPool). Ohne direktes Abonnement bliebe IsConnected auf dem Wert beim
    // Oeffnen stehen (Ausfuehren dauerhaft deaktiviert, wenn der Tab vor dem Verbinden
    // geoeffnet wurde). Deshalb hier direkt am Event haengen (UI-marshalliert).
    private readonly SynchronizationContext? _uiContext;

    public QueryDocumentViewModel(
        IQueryService queryService,
        ISessionContext sessionContext,
        IMessenger messenger,
        ILogger<QueryDocumentViewModel> logger,
        IDialogService dialogService,
        IListExporter listExporter)
        : base("query")
    {
        _queryService = queryService;
        _sessionContext = sessionContext;
        _messenger = messenger;
        _logger = logger;
        _dialogService = dialogService;
        _listExporter = listExporter;
        _uiContext = SynchronizationContext.Current;

        Title = "Abfrage";

        _messenger.RegisterAll(this);
        _sessionContext.ConnectionChanged += OnSessionConnectionChanged;
        SyncFromContext();
    }

    /// <inheritdoc />
    public override string? ContextTabKey => "query";

    /// <summary>Vom Nutzer eingegebene CMISQL-Abfrage (mehrzeiliger Editor).</summary>
    [ObservableProperty]
    private string _cmisqlText = string.Empty;

    /// <summary>Spaltennamen (Query-Namen) des letzten Ergebnisses; treibt die
    /// dynamische Spaltenerzeugung im DataGrid der View (FA-51).</summary>
    [ObservableProperty]
    private IReadOnlyList<string> _columnNames = Array.Empty<string>();

    /// <summary>Ergebniszeilen der zuletzt ausgeführten Abfrage.</summary>
    public ObservableCollection<QueryRowDto> Rows { get; } = new();

    public bool HasResult => Rows.Count > 0;

    /// <summary>Meldungen dieses Abfrage-Tabs (SSMS-Muster: eigener „Meldungen"-Tab
    /// neben dem Ergebnis, unabhängig vom globalen Ausgabe-Fenster).</summary>
    public ObservableCollection<string> Messages { get; } = new();

    /// <summary>0 = Ergebnis-Tab, 1 = Meldungen-Tab. Springt bei Fehlern automatisch
    /// auf „Meldungen", bei Erfolg zurück auf „Ergebnis" (SSMS-Verhalten).</summary>
    [ObservableProperty]
    private int _selectedResultTabIndex;

    /// <summary>Spiegelt den Verbindungszustand aus dem <see cref="ISessionContext"/>.</summary>
    [ObservableProperty]
    private bool _isConnected;

    /// <summary>
    /// Führt die aktuelle CMISQL-Abfrage aus (FA-50) und ersetzt das bisherige
    /// Ergebnis durch die neuen Spalten/Zeilen (FA-51).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecuteQuery))]
    private async Task ExecuteQueryAsync(CancellationToken ct)
    {
        using (BeginBusy())
        {
            try
            {
                QueryResultDto result = await _queryService
                    .QueryAsync(CmisqlText, searchAllVersions: false, ct)
                    .ConfigureAwait(true);

                Rows.Clear();
                foreach (var row in result.Rows)
                {
                    Rows.Add(row);
                }
                ColumnNames = result.ColumnNames;

                OnPropertyChanged(nameof(HasResult));

                _logger.LogInformation(
                    "Abfrage ausgeführt: {RowCount} Zeile(n), {ColumnCount} Spalte(n).",
                    Rows.Count, ColumnNames.Count);
                AppendMessage($"Abfrage ausgeführt: {Rows.Count} Zeile(n), {ColumnNames.Count} Spalte(n).");
                SelectedResultTabIndex = 0;
            }
            catch (OperationCanceledException)
            {
                // Abbruch ist kein Fehler (NFA-13) – still verschlucken.
            }
            catch (CmisAppException ex)
            {
                var description = Describe(ex);
                _logger.LogError("Abfrage fehlgeschlagen: {Message}", description);
                AppendMessage($"Abfrage fehlgeschlagen: {description}");
                SelectedResultTabIndex = 1;
            }
        }
    }

    private bool CanExecuteQuery() =>
        IsConnected && !IsBusy && !string.IsNullOrWhiteSpace(CmisqlText);

    /// <summary>
    /// Speichert die aktuelle CMISQL-Abfrage in eine Datei (Query Browser: „Speichern").
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSaveQuery))]
    private async Task SaveQueryAsync(CancellationToken ct)
    {
        var path = await _dialogService.PickSaveFileAsync("Abfrage.cmisql", CmisqlFileFilter).ConfigureAwait(true);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        if (!path.EndsWith(".cmisql", StringComparison.OrdinalIgnoreCase))
        {
            path += ".cmisql";
        }

        using (BeginBusy())
        {
            try
            {
                await File.WriteAllTextAsync(path, CmisqlText, ct).ConfigureAwait(true);
                AppendMessage($"Abfrage gespeichert nach \"{path}\".");
            }
            catch (OperationCanceledException)
            {
            }
            catch (IOException ex)
            {
                AppendMessage($"Speichern fehlgeschlagen: Die Datei konnte nicht geschrieben werden ({ex.Message}).");
                SelectedResultTabIndex = 1;
            }
            catch (UnauthorizedAccessException ex)
            {
                AppendMessage($"Speichern fehlgeschlagen: Kein Zugriff auf die Zieldatei ({ex.Message}).");
                SelectedResultTabIndex = 1;
            }
        }
    }

    private bool CanSaveQuery() => !IsBusy && !string.IsNullOrWhiteSpace(CmisqlText);

    /// <summary>
    /// Lädt eine gespeicherte CMISQL-Abfrage aus einer Datei (Query Browser: „Laden").
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanLoadQuery))]
    private async Task LoadQueryAsync(CancellationToken ct)
    {
        var path = await _dialogService.PickOpenFileAsync(CmisqlFileFilter).ConfigureAwait(true);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        using (BeginBusy())
        {
            try
            {
                CmisqlText = await File.ReadAllTextAsync(path, ct).ConfigureAwait(true);
                AppendMessage($"Abfrage geladen aus \"{path}\".");
            }
            catch (OperationCanceledException)
            {
            }
            catch (IOException ex)
            {
                AppendMessage($"Laden fehlgeschlagen: Die Datei konnte nicht gelesen werden ({ex.Message}).");
                SelectedResultTabIndex = 1;
            }
            catch (UnauthorizedAccessException ex)
            {
                AppendMessage($"Laden fehlgeschlagen: Kein Zugriff auf die Datei ({ex.Message}).");
                SelectedResultTabIndex = 1;
            }
        }
    }

    private bool CanLoadQuery() => !IsBusy;

    /// <summary>
    /// Exportiert das aktuelle Abfrageergebnis als Excel-Datei (F3): eine Zeile je
    /// Treffer, eine Spalte je Query-Name, darueber ein Kopfbereich mit der Abfrage.
    /// Fragt ueber den Dialog-Dienst einen Zielpfad ab und meldet Erfolg/Fehler ueber
    /// den Logger (Ausgabe/Fehlerliste).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExportResult))]
    private async Task ExportResultAsync(CancellationToken ct)
    {
        if (Rows.Count == 0)
        {
            return;
        }

        var path = await _dialogService.PickSaveFileAsync("Abfrageergebnis.xlsx").ConfigureAwait(true);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        // Endung sicherstellen (der generische Speichern-Dialog erzwingt sie nicht).
        if (!path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            path += ".xlsx";
        }

        // Zeilen kopieren: der Export laeuft async, waehrenddessen koennte eine neue
        // Abfrage die Collection ersetzen.
        var rows = Rows.ToList();
        var columns = ColumnNames;

        using (BeginBusy())
        {
            try
            {
                await _listExporter
                    .ExportQueryResultAsync(columns, rows, CmisqlText, path, ct)
                    .ConfigureAwait(true);

                _logger.LogInformation(
                    "Abfrageergebnis ({RowCount} Zeile(n)) wurde nach \"{Path}\" exportiert.", rows.Count, path);
                AppendMessage($"Abfrageergebnis ({rows.Count} Zeile(n)) wurde nach \"{path}\" exportiert.");
            }
            catch (OperationCanceledException)
            {
            }
            catch (IOException ex)
            {
                _logger.LogError("Export fehlgeschlagen: Die Datei konnte nicht geschrieben werden ({Message}).", ex.Message);
                AppendMessage($"Export fehlgeschlagen: Die Datei konnte nicht geschrieben werden ({ex.Message}).");
                SelectedResultTabIndex = 1;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError("Export fehlgeschlagen: Kein Zugriff auf die Zieldatei ({Message}).", ex.Message);
                AppendMessage($"Export fehlgeschlagen: Kein Zugriff auf die Zieldatei ({ex.Message}).");
                SelectedResultTabIndex = 1;
            }
        }
    }

    private bool CanExportResult() => HasResult && !IsBusy;

    /// <summary>
    /// Reagiert auf Verbindungswechsel (Alt-App-Pfad ueber den Messenger): siehe
    /// <see cref="ApplyConnectionState"/>.
    /// </summary>
    public void Receive(ConnectionStateChangedMessage message) => ApplyConnectionState();

    /// <summary>
    /// Reagiert auf <see cref="ISessionContext.ConnectionChanged"/> (neuer-Shell-Pfad).
    /// Das Event kann aus einem Hintergrund-Thread kommen, daher UI-marshalliert.
    /// </summary>
    private void OnSessionConnectionChanged(object? sender, EventArgs e)
    {
        if (_uiContext is not null && _uiContext != SynchronizationContext.Current)
        {
            _uiContext.Post(_ => ApplyConnectionState(), null);
        }
        else
        {
            ApplyConnectionState();
        }
    }

    /// <summary>
    /// Spiegelt den Verbindungszustand; bei Trennung wird das Ergebnis geleert, damit
    /// keine Zeilen einer alten Session stehen bleiben.
    /// </summary>
    private void ApplyConnectionState()
    {
        SyncFromContext();
        if (!IsConnected)
        {
            Rows.Clear();
            ColumnNames = Array.Empty<string>();
            OnPropertyChanged(nameof(HasResult));
        }
    }

    // IsBusy/IsConnected/CmisqlText wirken auf CanExecute von ExecuteQuery. Da
    // IsBusy in der Basisklasse per [ObservableProperty] liegt, erfolgt die
    // Neubewertung über OnPropertyChanged (Referenzmuster M3/R4).
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(IsBusy) or nameof(IsConnected) or nameof(CmisqlText))
        {
            ExecuteQueryCommand.NotifyCanExecuteChanged();
        }

        // Speichern haengt zusaetzlich am vorhandenen Abfragetext, Laden nur am Busy-Zustand.
        if (e.PropertyName is nameof(IsBusy) or nameof(CmisqlText))
        {
            SaveQueryCommand.NotifyCanExecuteChanged();
        }

        if (e.PropertyName is nameof(IsBusy))
        {
            LoadQueryCommand.NotifyCanExecuteChanged();
        }

        // F3: der Export haengt am Vorhandensein eines Ergebnisses und am Busy-Zustand.
        if (e.PropertyName is nameof(IsBusy) or nameof(HasResult))
        {
            ExportResultCommand.NotifyCanExecuteChanged();
        }
    }

    private void SyncFromContext() => IsConnected = _sessionContext.IsConnected;

    /// <summary>Fuegt eine zeitgestempelte Zeile im „Meldungen"-Tab dieses Abfrage-Tabs hinzu.</summary>
    private void AppendMessage(string text) => Messages.Add($"{DateTime.Now:HH:mm:ss} {text}");

    /// <summary>Übersetzt einen CMIS-Fehler in eine kurze, verständliche Meldung (FA-05).</summary>
    private static string Describe(CmisAppException ex) => ex.Kind switch
    {
        CmisErrorKind.Authentication => "Anmeldung fehlgeschlagen. Bitte Verbindung prüfen.",
        CmisErrorKind.Network => "Server nicht erreichbar. Bitte URL und Netzwerk prüfen.",
        CmisErrorKind.NotFound => "Objekt oder Ressource nicht gefunden.",
        CmisErrorKind.InvalidArgument => ex.Message,
        CmisErrorKind.PermissionDenied => "Zugriff verweigert. Fehlende Berechtigungen.",
        CmisErrorKind.NotSupported => "Die Operation wird vom Server nicht unterstützt.",
        _ => string.IsNullOrWhiteSpace(ex.Message) ? "Unerwarteter Serverfehler." : ex.Message
    };
}
