using APX.Wpf.Shell.ViewModels;
using APX.Wpf.Shell.ViewModels.Workspace;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CMISPilot.Cmis.Contracts;
using CMISPilot.Cmis.Errors;
using CMISPilot.Cmis.Models;
using CMISPilot.ViewModels.Export;
using CMISPilot.ViewModels.Shell;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace CMISPilot.ViewModels.Repository;

/// <summary>
/// Dokument-Tab „Repository-Info" (FA-10/FA-11): zeigt Eckdaten, Capabilities und
/// ACL-Capabilities des verbundenen Repositories und kann beides nach Excel sowie das
/// CMIS-JSON als Datei ausgeben.
///
/// <para>Folgt der Verbindung: baut sich bei <see cref="ISessionContext.ConnectionChanged"/>
/// neu auf und leert sich beim Trennen (Muster aus dem Server-Baum). Bewusst WPF-frei
/// (NFA-03), Serveraufrufe async über <see cref="ViewModelBase.BeginBusy"/> (NFA-13).</para>
/// </summary>
public sealed partial class RepositoryInfoDocumentViewModel : DocumentViewModelBase, IDisposable
{
    private readonly IRepositoryInfoService _repositoryInfoService;
    private readonly ISessionContext _sessionContext;
    private readonly IDialogService _dialogService;
    private readonly IListExporter _listExporter;
    private readonly ILogger<RepositoryInfoDocumentViewModel> _logger;
    private readonly SynchronizationContext? _uiContext;

    /// <summary>Feste ContentId: der Tab wird beim erneuten Öffnen dedupliziert.</summary>
    public const string ContentIdConst = "repository-info";

    public RepositoryInfoDocumentViewModel(
        IRepositoryInfoService repositoryInfoService,
        ISessionContext sessionContext,
        IDialogService dialogService,
        IListExporter listExporter,
        ILogger<RepositoryInfoDocumentViewModel> logger)
        : base(ContentIdConst)
    {
        _repositoryInfoService = repositoryInfoService;
        _sessionContext = sessionContext;
        _dialogService = dialogService;
        _listExporter = listExporter;
        _logger = logger;
        _uiContext = SynchronizationContext.Current;

        Title = "Repository-Info";

        _sessionContext.ConnectionChanged += OnConnectionChanged;
        IsConnected = _sessionContext.IsConnected;
        if (IsConnected)
        {
            _ = LoadAsync(CancellationToken.None);
        }
    }

    /// <inheritdoc />
    public override string? ContextTabKey => ContentIdConst;

    /// <summary>Eckdaten des Repositories (FA-10).</summary>
    public ObservableCollection<RepositoryInfoRowViewModel> GeneralRows { get; } = new();

    /// <summary>Repository-Capabilities (FA-11).</summary>
    public ObservableCollection<RepositoryInfoRowViewModel> CapabilityRows { get; } = new();

    /// <summary>ACL-Capabilities, Berechtigungen und deren Zuordnung (FA-11).</summary>
    public ObservableCollection<RepositoryInfoRowViewModel> AclRows { get; } = new();

    [ObservableProperty]
    private bool _isConnected;

    /// <summary>True, sobald Daten geladen sind — steuert Anzeige und Export-Kommandos.</summary>
    [ObservableProperty]
    private bool _hasData;

    /// <summary>Überschrift der Ansicht, sobald geladen.</summary>
    [ObservableProperty]
    private string _repositoryTitle = string.Empty;

    private RepositoryInfoDto? _current;

    /// <summary>Lädt die Repository-Information der aktiven Verbindung nach.</summary>
    [RelayCommand(CanExecute = nameof(CanLoad))]
    private async Task LoadAsync(CancellationToken ct)
    {
        if (!_sessionContext.IsConnected)
        {
            Clear();
            return;
        }

        using (BeginBusy())
        {
            try
            {
                var info = await _repositoryInfoService.GetRepositoryInfoAsync(ct).ConfigureAwait(true);
                Apply(info);
            }
            catch (OperationCanceledException)
            {
            }
            catch (CmisAppException ex)
            {
                Clear();
                _logger.LogWarning("Repository-Information konnte nicht gelesen werden: {Message}", ex.Message);
            }
        }
    }

    private bool CanLoad() => !IsBusy && IsConnected;

    /// <summary>Schreibt die angezeigten Angaben als Excel-Datei.</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportToExcelAsync(CancellationToken ct)
    {
        if (_current is not { } info)
        {
            return;
        }

        var path = await _dialogService
            .PickSaveFileAsync($"{Sanitize(info.Name ?? info.Id)} - Repository-Info.xlsx")
            .ConfigureAwait(true);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        if (!path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            path += ".xlsx";
        }

        var rows = BuildExportRows();

        using (BeginBusy())
        {
            try
            {
                await _listExporter
                    .ExportRepositoryInfoAsync(info, rows, BuildPermissionMappingRows(), path, ct)
                    .ConfigureAwait(true);
                _logger.LogInformation("Repository-Information wurde nach \"{Path}\" exportiert.", path);
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

    /// <summary>Speichert die Repository-Information als CMIS-JSON.</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task SaveJsonAsync(CancellationToken ct)
    {
        if (_current is not { } info)
        {
            return;
        }

        var path = await _dialogService
            .PickSaveFileAsync($"{Sanitize(info.Name ?? info.Id)} - RepositoryInfo.json")
            .ConfigureAwait(true);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            path += ".json";
        }

        using (BeginBusy())
        {
            try
            {
                var json = await _repositoryInfoService.GetRepositoryInfoJsonAsync(ct).ConfigureAwait(true);
                await File.WriteAllTextAsync(path, json, System.Text.Encoding.UTF8, ct).ConfigureAwait(true);
                _logger.LogInformation("Repository-Information wurde als JSON nach \"{Path}\" gespeichert.", path);
            }
            catch (OperationCanceledException)
            {
            }
            catch (CmisAppException ex)
            {
                _logger.LogError("Speichern fehlgeschlagen: {Message}", ex.Message);
            }
            catch (IOException ex)
            {
                _logger.LogError("Speichern fehlgeschlagen: Die Datei konnte nicht geschrieben werden ({Message}).", ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError("Speichern fehlgeschlagen: Kein Zugriff auf die Zieldatei ({Message}).", ex.Message);
            }
        }
    }

    private bool CanExport() => !IsBusy && HasData;

    /// <summary>Die angezeigten Zeilen als Exportzeilen, Abschnitt für Abschnitt.</summary>
    public IReadOnlyList<RepositoryInfoRowExportDto> BuildExportRows() =>
    [
        .. GeneralRows.Select(r => new RepositoryInfoRowExportDto("Eckdaten", r.Name, r.Value)),
        .. CapabilityRows.Select(r => new RepositoryInfoRowExportDto("Capabilities", r.Name, r.Value)),
        .. AclRows.Select(r => new RepositoryInfoRowExportDto("ACL-Capabilities", r.Name, r.Value))
    ];

    private void Apply(RepositoryInfoDto info)
    {
        _current = info;
        RepositoryTitle = string.IsNullOrWhiteSpace(info.Name) ? info.Id : info.Name!;

        GeneralRows.Clear();
        Add(GeneralRows, "Name", info.Name);
        Add(GeneralRows, "ID", info.Id);
        Add(GeneralRows, "Beschreibung", info.Description);
        Add(GeneralRows, "Hersteller", info.VendorName);
        Add(GeneralRows, "Produkt", info.ProductName);
        Add(GeneralRows, "Produktversion", info.ProductVersion);
        Add(GeneralRows, "CMIS-Version", info.CmisVersion);
        Add(GeneralRows, "Wurzelordner-ID", info.RootFolderId);
        Add(GeneralRows, "Thin-Client-URI", info.ThinClientUri);
        Add(GeneralRows, "Letzter Change-Log-Token", info.LatestChangeLogToken);
        Add(GeneralRows, "Änderungsprotokoll unvollständig", Format(info.ChangesIncomplete));
        Add(GeneralRows, "Änderungen für Typen", string.Join(", ", info.ChangesOnType));
        Add(GeneralRows, "Principal-ID anonym", info.PrincipalIdAnonymous);
        Add(GeneralRows, "Principal-ID jeder", info.PrincipalIdAnyone);
        foreach (var feature in info.ExtensionFeatures)
        {
            var label = feature.CommonName ?? feature.Id;
            Add(GeneralRows, $"Erweiterung: {label}",
                string.Join(" ", new[] { feature.VersionLabel, feature.Description, feature.Url }
                    .Where(x => !string.IsNullOrWhiteSpace(x))));
            foreach (var entry in feature.FeatureData)
            {
                Add(GeneralRows, $"Erweiterung {label}: {entry.Key}", entry.Value);
            }
        }

        // Herstellerspezifische Erweiterungsdaten sind ein Baum. Fuer die Name/Wert-
        // Tabelle wird er als Pfad geplattet (a > b > c), damit die Herkunft eines
        // Wertes ablesbar bleibt; die vollstaendige Struktur steht im JSON-Export.
        AddExtensions(GeneralRows, info.Extensions, prefix: "Erweiterungsdaten");

        CapabilityRows.Clear();
        if (info.Capabilities is { } cap)
        {
            Add(CapabilityRows, "Inhaltsstrom-Aktualisierung", cap.ContentStreamUpdates);
            Add(CapabilityRows, "Änderungsprotokoll", cap.Changes);
            Add(CapabilityRows, "Renditions", cap.Renditions);
            Add(CapabilityRows, "Sortierung", cap.OrderBy);
            Add(CapabilityRows, "Abfragen", cap.Query);
            Add(CapabilityRows, "Joins", cap.Join);
            Add(CapabilityRows, "ACL", cap.Acl);
            Add(CapabilityRows, "GetDescendants", Format(cap.GetDescendantsSupported));
            Add(CapabilityRows, "GetFolderTree", Format(cap.GetFolderTreeSupported));
            Add(CapabilityRows, "Mehrfachablage", Format(cap.MultifilingSupported));
            Add(CapabilityRows, "Ablage lösbar", Format(cap.UnfilingSupported));
            Add(CapabilityRows, "Versionsspezifische Ablage", Format(cap.VersionSpecificFilingSupported));
            Add(CapabilityRows, "PWC durchsuchbar", Format(cap.PwcSearchableSupported));
            Add(CapabilityRows, "PWC änderbar", Format(cap.PwcUpdatableSupported));
            Add(CapabilityRows, "Alle Versionen durchsuchbar", Format(cap.AllVersionsSearchableSupported));
            Add(CapabilityRows, "Anlegbare Property-Typen", string.Join(", ", cap.CreatablePropertyTypes));

            if (cap.NewTypeSettableAttributes is { } settable)
            {
                Add(CapabilityRows, "Neuer Typ: ID setzbar", Format(settable.Id));
                Add(CapabilityRows, "Neuer Typ: Local Name setzbar", Format(settable.LocalName));
                Add(CapabilityRows, "Neuer Typ: Local Namespace setzbar", Format(settable.LocalNamespace));
                Add(CapabilityRows, "Neuer Typ: Displayname setzbar", Format(settable.DisplayName));
                Add(CapabilityRows, "Neuer Typ: Query Name setzbar", Format(settable.QueryName));
                Add(CapabilityRows, "Neuer Typ: Beschreibung setzbar", Format(settable.Description));
                Add(CapabilityRows, "Neuer Typ: Creatable setzbar", Format(settable.Creatable));
                Add(CapabilityRows, "Neuer Typ: Fileable setzbar", Format(settable.Fileable));
                Add(CapabilityRows, "Neuer Typ: Queryable setzbar", Format(settable.Queryable));
                Add(CapabilityRows, "Neuer Typ: Volltextindiziert setzbar", Format(settable.FulltextIndexed));
                Add(CapabilityRows, "Neuer Typ: In Supertyp-Abfrage setzbar", Format(settable.IncludedInSupertypeQuery));
                Add(CapabilityRows, "Neuer Typ: Policy steuerbar setzbar", Format(settable.ControllablePolicy));
                Add(CapabilityRows, "Neuer Typ: ACL steuerbar setzbar", Format(settable.ControllableAcl));
            }
        }

        AclRows.Clear();
        if (info.AclCapabilities is { } acl)
        {
            Add(AclRows, "Unterstützte Berechtigungen", acl.SupportedPermissions);
            Add(AclRows, "Weitergabe", acl.AclPropagation);
            foreach (var permission in acl.Permissions)
            {
                Add(AclRows, $"Berechtigung: {permission.Id}", permission.Description);
            }

            foreach (var mapping in acl.PermissionMapping)
            {
                Add(AclRows, mapping.Key, string.Join(", ", mapping.Permissions));
            }
        }

        HasData = GeneralRows.Count > 0;
    }

    /// <summary>Nimmt einen Erweiterungsbaum als Pfad-Zeilen auf.</summary>
    private static void AddExtensions(
        ObservableCollection<RepositoryInfoRowViewModel> target,
        IReadOnlyList<CmisExtensionElementDto> elements,
        string prefix)
    {
        foreach (var element in elements)
        {
            var path = $"{prefix} > {element.Name}";
            Add(target, path, element.Value);
            foreach (var attribute in element.Attributes)
            {
                Add(target, $"{path} [{attribute.Key}]", attribute.Value);
            }

            AddExtensions(target, element.Children, path);
        }
    }

    /// <summary>
    /// Die Berechtigungszuordnung als eigene Tabelle: eine Zeile je Paar aus Schlüssel
    /// und Berechtigung. In der Name/Wert-Ansicht stehen die Berechtigungen mit Komma
    /// verbunden in einer Zelle — zum Lesen genügt das, zum Auswerten nicht.
    /// </summary>
    public IReadOnlyList<PermissionMappingExportDto> BuildPermissionMappingRows() =>
        _current?.AclCapabilities is { } acl
            ? [.. acl.PermissionMapping.SelectMany(
                m => m.Permissions.Select(p => new PermissionMappingExportDto(m.Key, p)))]
            : [];

    private void Clear()
    {
        _current = null;
        GeneralRows.Clear();
        CapabilityRows.Clear();
        AclRows.Clear();
        RepositoryTitle = string.Empty;
        HasData = false;
    }

    /// <summary>
    /// Nimmt eine Zeile auf, sofern ein Wert vorliegt. Leere Angaben werden weggelassen,
    /// statt eine Tabelle mit halb leeren Zeilen zu zeigen — die Server liefern je nach
    /// Umfang sehr unterschiedlich viel.
    /// </summary>
    private static void Add(ObservableCollection<RepositoryInfoRowViewModel> target, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target.Add(new RepositoryInfoRowViewModel(name, value!));
        }
    }

    private static string? Format(bool? value) =>
        value is bool b ? (b ? "Ja" : "Nein") : null;

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return name;
    }

    private void OnConnectionChanged(object? sender, EventArgs e)
    {
        void Handle()
        {
            IsConnected = _sessionContext.IsConnected;
            if (IsConnected)
            {
                _ = LoadAsync(CancellationToken.None);
            }
            else
            {
                Clear();
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

    // IsBusy/IsConnected/HasData wirken auf CanExecute; der generierte OnIsBusyChanged-Hook
    // liegt in der Basisklasse und ist hier nicht implementierbar (Referenzmuster der Shell).
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(IsBusy) or nameof(IsConnected))
        {
            LoadCommand.NotifyCanExecuteChanged();
        }

        if (e.PropertyName is nameof(IsBusy) or nameof(HasData))
        {
            ExportToExcelCommand.NotifyCanExecuteChanged();
            SaveJsonCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>Meldet sich vom Verbindungs-Event ab.</summary>
    public void Dispose() => _sessionContext.ConnectionChanged -= OnConnectionChanged;
}

/// <summary>Eine Zeile der Repository-Info-Ansicht.</summary>
/// <param name="Name">Bezeichnung des Wertes.</param>
/// <param name="Value">Der Wert als Text.</param>
public sealed record RepositoryInfoRowViewModel(string Name, string Value);
