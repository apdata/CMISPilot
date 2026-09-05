using APX.Wpf.Shell.ViewModels;
using APX.Wpf.Shell.ViewModels.Logging;
using APX.Wpf.Shell.ViewModels.Workspace;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System;
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

namespace CMISPilot.ViewModels.Types;

/// <summary>
/// Dokument-Tab „Typen" (R5.2): lädt den gesamten Typbaum des aktiven Repositories
/// (Basistypen + abgeleitete Typen, FA-60) und zeigt zu einem selektierten Typ
/// dessen Attribute und alle Property-Definitionen (FA-61/62). Logik aus
/// <c>TypesAreaViewModel</c> (entfernt) (M5) übernommen und nach dem in R4
/// etablierten Muster (<see cref="Explorer.ExplorerDocumentViewModel"/>) auf
/// <see cref="DocumentViewModelBase"/> umgestellt.
///
/// Anders als die Alt-App meldet dieser Tab Erfolg/Fehler über einen injizierten
/// <see cref="ILogger{TCategoryName}"/> statt einer <see cref="NotificationRequestMessage"/>
/// an die (in der neuen Shell noch nicht vorhandene) Shell-InfoBar — die Meldungen
/// landen dadurch in Ausgabe/Fehlerliste (R3).
///
/// Singleton-Registrierung (DI): anders als der Explorer-Tab braucht dieser Tab
/// keinen Laufzeit-Parameter, die feste <see cref="ContentId"/> „types" dedupliziert
/// beim erneuten Öffnen ohnehin (<see cref="WorkspaceViewModel.OpenDocument"/>).
///
/// Bewusst WPF-frei (NFA-03); Serveraufrufe laufen async über
/// <see cref="Shell.ViewModelBase.BeginBusy"/>, nie <c>.Result</c>/<c>.Wait()</c> (NFA-13).
/// </summary>
public sealed partial class TypesDocumentViewModel : DocumentViewModelBase,
    IRecipient<ConnectionStateChangedMessage>
{
    private readonly ITypeService _typeService;
    private readonly ISessionContext _sessionContext;
    private readonly IMessenger _messenger;
    private readonly ILogger<TypesDocumentViewModel> _logger;
    private readonly IDialogService _dialogService;
    private readonly ITypeDefinitionExporter _typeExporter;

    // Analog ServerTreeViewModel: die neue Shell sendet keine
    // ConnectionStateChangedMessage (das war das Muster der Alt-App), sondern
    // verbindet direkt ueber IConnectionService, was ISessionContext.ConnectionChanged
    // aus dem ThreadPool feuert. Deshalb hier direkt am Event haengen und die an die
    // TreeView gebundene RootTypes-Collection ueber den UI-SynchronizationContext
    // aktualisieren (bleibt WPF-frei, NFA-03).
    private readonly SynchronizationContext? _uiContext;

    public TypesDocumentViewModel(
        ITypeService typeService,
        ISessionContext sessionContext,
        IMessenger messenger,
        ILogger<TypesDocumentViewModel> logger,
        IDialogService dialogService,
        ITypeDefinitionExporter typeExporter)
        : base("types")
    {
        _typeService = typeService;
        _sessionContext = sessionContext;
        _messenger = messenger;
        _logger = logger;
        _dialogService = dialogService;
        _typeExporter = typeExporter;
        _uiContext = SynchronizationContext.Current;

        Title = "Typen";

        _messenger.RegisterAll(this);
        _sessionContext.ConnectionChanged += OnSessionConnectionChanged;

        // Beim Oeffnen des Tabs die Typen sofort laden, wenn bereits verbunden.
        ApplyConnectionState();
    }

    /// <inheritdoc />
    public override string? ContextTabKey => "types";

    /// <summary>Basistypen als Wurzeln des Typbaums; abgeleitete Typen hängen über
    /// <see cref="TypeDefinitionDto.Children"/> darunter (FA-60).</summary>
    public ObservableCollection<TypeDefinitionDto> RootTypes { get; } = new();

    public bool HasTypes => RootTypes.Count > 0;

    /// <summary>Aktuell im Baum selektierter Typ; treibt die Detailansicht (FA-61/62).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyCanExecuteChangedFor(nameof(ExportSelectedTypeCommand))]
    private TypeDefinitionDto? _selectedType;

    public bool HasSelection => SelectedType is not null;

    /// <summary>Spiegelt den Verbindungszustand aus dem <see cref="ISessionContext"/>.</summary>
    [ObservableProperty]
    private bool _isConnected;

    /// <summary>
    /// Lädt den gesamten Typbaum ab den Basistypen inkl. aller Property-Definitionen
    /// (FA-60/61/62). Bestehende Auswahl/Anzeige wird ersetzt.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanLoadTypes))]
    private async Task LoadTypesAsync(CancellationToken ct)
    {
        using (BeginBusy())
        {
            try
            {
                var tree = await _typeService
                    .GetTypeTreeAsync(includePropertyDefinitions: true, ct)
                    .ConfigureAwait(true);

                SelectedType = null;
                RootTypes.Clear();
                foreach (var type in tree ?? System.Array.Empty<TypeDefinitionDto>())
                {
                    RootTypes.Add(type);
                }

                OnPropertyChanged(nameof(HasTypes));

                _logger.LogInformation(
                    "Typen geladen: {Count} Basistyp(en) mit abgeleiteten Typen.", RootTypes.Count);
            }
            catch (OperationCanceledException)
            {
                // Abbruch ist kein Fehler (NFA-13) – still verschlucken.
            }
            catch (CmisAppException ex)
            {
                _logger.LogError("Laden fehlgeschlagen: {Message}", Describe(ex));
            }
        }
    }

    private bool CanLoadTypes() => IsConnected && !IsBusy;

    /// <summary>
    /// Exportiert den aktuell gewaehlten Typ als Excel-Datei (F2). Fragt ueber den
    /// Dialog-Dienst einen Zielpfad ab (vorbelegt mit dem Typnamen und Endung .xlsx)
    /// und schreibt Typ-Attribute samt Property-Definitionen. Meldet Erfolg/Fehler
    /// ueber den Logger (Ausgabe/Fehlerliste).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExportSelectedType))]
    private async Task ExportSelectedTypeAsync(CancellationToken ct)
    {
        if (SelectedType is not { } type)
        {
            return;
        }

        var suggestedName = $"{Sanitize(type.DisplayName ?? type.Id)}.xlsx";
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

        using (BeginBusy())
        {
            try
            {
                await _typeExporter.ExportAsync(type, path, ct).ConfigureAwait(true);
                _logger.LogInformation(
                    "Typ \"{Type}\" wurde nach \"{Path}\" exportiert.", type.DisplayName ?? type.Id, path);
            }
            catch (OperationCanceledException)
            {
            }
            catch (System.IO.IOException ex)
            {
                _logger.LogError("Export fehlgeschlagen: Die Datei konnte nicht geschrieben werden ({Message}).", ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError("Export fehlgeschlagen: Kein Zugriff auf die Zieldatei ({Message}).", ex.Message);
            }
        }
    }

    private bool CanExportSelectedType() => HasSelection && !IsBusy;

    /// <summary>Ersetzt in einem Vorschlags-Dateinamen die unter Windows unzulaessigen Zeichen.</summary>
    private static string Sanitize(string name)
    {
        foreach (var c in System.IO.Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return name;
    }

    /// <summary>
    /// Reagiert auf Verbindungswechsel (Alt-App-Pfad ueber den Messenger): siehe
    /// <see cref="ApplyConnectionState"/>.
    /// </summary>
    public void Receive(ConnectionStateChangedMessage message) => ApplyConnectionState();

    /// <summary>
    /// Reagiert auf <see cref="ISessionContext.ConnectionChanged"/> (neuer-Shell-Pfad).
    /// Das Event kann aus einem Hintergrund-Thread kommen, daher ueber den
    /// UI-SynchronizationContext marshallen.
    /// </summary>
    private void OnSessionConnectionChanged(object? sender, System.EventArgs e)
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
    /// Spiegelt den Verbindungszustand und laedt bei bestehender Verbindung die Typen
    /// automatisch nach; bei Trennung wird die Anzeige geleert, damit keine Typen
    /// einer alten Session stehen bleiben.
    /// </summary>
    private void ApplyConnectionState()
    {
        SyncFromContext();
        if (IsConnected)
        {
            _ = LoadTypesAsync(CancellationToken.None);
        }
        else
        {
            SelectedType = null;
            RootTypes.Clear();
            OnPropertyChanged(nameof(HasTypes));
        }
    }

    // IsBusy/IsConnected wirken auf CanExecute von LoadTypes. Da IsBusy in der
    // Basisklasse per [ObservableProperty] liegt, erfolgt die Neubewertung über
    // OnPropertyChanged (Referenzmuster M3/R4).
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(IsBusy) or nameof(IsConnected))
        {
            LoadTypesCommand.NotifyCanExecuteChanged();
        }

        if (e.PropertyName == nameof(IsBusy))
        {
            ExportSelectedTypeCommand.NotifyCanExecuteChanged();
        }
    }

    private void SyncFromContext() => IsConnected = _sessionContext.IsConnected;

    /// <summary>Übersetzt einen CMIS-Fehler in eine kurze, verständliche Meldung (FA-05).</summary>
    private static string Describe(CmisAppException ex) => ex.Kind switch
    {
        CmisErrorKind.Authentication => "Anmeldung fehlgeschlagen. Bitte Verbindung prüfen.",
        CmisErrorKind.Network => "Server nicht erreichbar. Bitte URL und Netzwerk prüfen.",
        CmisErrorKind.NotFound => "Typ oder Ressource nicht gefunden.",
        CmisErrorKind.InvalidArgument => ex.Message,
        CmisErrorKind.PermissionDenied => "Zugriff verweigert. Fehlende Berechtigungen.",
        CmisErrorKind.NotSupported => "Die Operation wird vom Server nicht unterstützt.",
        _ => string.IsNullOrWhiteSpace(ex.Message) ? "Unerwarteter Serverfehler." : ex.Message
    };
}
