using APX.Wpf.Shell.ViewModels;
using APX.Wpf.Shell.ViewModels.Logging;
using APX.Wpf.Shell.ViewModels.Workspace;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CMISPilot.Cmis.Contracts;
using CMISPilot.Cmis.Errors;
using CMISPilot.Cmis.Models;
using CMISPilot.ViewModels.Shell;
using Microsoft.Extensions.Logging;

namespace CMISPilot.ViewModels.ObjectDetails;

/// <summary>
/// ViewModel des Fensters „Erweiterte Eigenschaften" (R6.1). Erweitert das schlanke
/// Eigenschaften-Werkzeugfenster (<see cref="Explorer.PropertiesViewModel"/>) um:
/// alle Properties inkl. Mehrfachwerte/Query-Name/Local-Name, die Typdefinition samt
/// Vererbungskette (über <see cref="TypeDefinitionDto.ParentTypeId"/>), die Allowable
/// Actions des Objekts sowie ACL und Versionsreihe (neu über
/// <see cref="IObjectService.GetAclAsync"/>/<see cref="IObjectService.GetAllVersionsAsync"/>,
/// R6.1-Ergänzung an <c>IObjectService</c>).
///
/// Wird für ein einzelnes, bereits geladenes <see cref="CmisObjectDto"/> erzeugt (das
/// aktuell im Explorer selektierte Objekt); anders als die Werkzeugfenster folgt dieses
/// ViewModel keiner laufenden Selektion, sondern wird pro Fensteraufruf neu instanziiert
/// (siehe <c>MainWindow.xaml.cs</c>, analog <see cref="Explorer.ExplorerDocumentViewModel"/>).
///
/// Bewusst WPF-frei (NFA-03); Serveraufrufe laufen async über
/// <see cref="ViewModelBase.BeginBusy"/>, nie <c>.Result</c>/<c>.Wait()</c> (NFA-13).
/// </summary>
public sealed class ExtendedPropertiesViewModel : ViewModelBase
{
    private readonly CmisObjectDto _target;
    private readonly IObjectService _objectService;
    private readonly ITypeService _typeService;
    private readonly ILogger<ExtendedPropertiesViewModel> _logger;

    /// <param name="target">Das zu inspizierende Objekt (Explorer-Selektion).</param>
    /// <param name="objectService">Lädt ACL und Versionsreihe (R6.1).</param>
    /// <param name="typeService">Lädt Typdefinition und Vererbungskette.</param>
    /// <param name="logger">Meldet Ladefehler (landen in Ausgabe/Fehlerliste, R3).</param>
    public ExtendedPropertiesViewModel(
        CmisObjectDto target,
        IObjectService objectService,
        ITypeService typeService,
        ILogger<ExtendedPropertiesViewModel> logger)
    {
        _target = target;
        _objectService = objectService;
        _typeService = typeService;
        _logger = logger;

        Title = $"Erweiterte Eigenschaften – {target.Name ?? target.Id}";

        _ = LoadAsync(CancellationToken.None);
    }

    public string Title { get; }
    public string ObjectId => _target.Id;
    public string? Name => _target.Name;
    public bool IsDocument => _target.IsDocument;

    /// <summary>Alle Properties des Objekts (inkl. Mehrfachwerte/Query-Name/Local-Name).</summary>
    public ObservableCollection<ExtendedPropertyRowViewModel> Properties { get; } = new();

    /// <summary>
    /// Typdefinition des Objekts und ihre Vererbungskette bis zum Basistyp
    /// (index 0 = konkreter Objekttyp, letzter Eintrag = Basistyp).
    /// </summary>
    public ObservableCollection<TypeDefinitionDto> TypeHierarchy { get; } = new();

    /// <summary>Erlaubte Aktionen (Allowable Actions) des Objekts, sofern geladen.</summary>
    public ObservableCollection<string> AllowableActions { get; } = new();

    /// <summary>True, wenn Allowable Actions für dieses Objekt vorliegen.</summary>
    public bool HasAllowableActions => AllowableActions.Count > 0;

    /// <summary>ACL des Objekts (R6.1).</summary>
    public ObservableCollection<AclEntryRowViewModel> AclEntries { get; } = new();

    /// <summary>True, wenn ACL-Einträge geladen werden konnten.</summary>
    public bool HasAclEntries => AclEntries.Count > 0;

    /// <summary>Versionsreihe des Objekts (nur bei Dokumenten befüllt, R6.1).</summary>
    public ObservableCollection<ObjectVersionDto> Versions { get; } = new();

    /// <summary>True, wenn eine Versionsreihe geladen werden konnte.</summary>
    public bool HasVersions => Versions.Count > 0;

    /// <summary>Lädt Typdefinition, Property-Zeilen, ACL und Versionsreihe nach.</summary>
    private async Task LoadAsync(CancellationToken ct)
    {
        using (BeginBusy())
        {
            var definitions = await LoadTypeHierarchyAsync(ct).ConfigureAwait(true);
            BuildPropertyRows(definitions);
            BuildAllowableActions();
            await LoadAclAsync(ct).ConfigureAwait(true);

            if (_target.IsDocument)
            {
                await LoadVersionsAsync(ct).ConfigureAwait(true);
            }
        }
    }

    /// <summary>
    /// Lädt die Typdefinition des Objekts und folgt <see cref="TypeDefinitionDto.ParentTypeId"/>
    /// bis zum Basistyp; füllt dabei <see cref="TypeHierarchy"/>. Liefert die
    /// Property-Definitionen des konkreten (untersten) Typs für <see cref="BuildPropertyRows"/>.
    /// </summary>
    private async Task<IReadOnlyList<PropertyDefinitionDto>> LoadTypeHierarchyAsync(CancellationToken ct)
    {
        TypeHierarchy.Clear();

        var typeId = _target.TypeId;
        var visited = new HashSet<string>(StringComparer.Ordinal);

        while (!string.IsNullOrEmpty(typeId) && visited.Add(typeId))
        {
            try
            {
                var definition = await _typeService.GetTypeDefinitionAsync(typeId, ct).ConfigureAwait(true);
                TypeHierarchy.Add(definition);
                typeId = definition.ParentTypeId;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (CmisAppException ex)
            {
                _logger.LogWarning("Typdefinition \"{TypeId}\" konnte nicht geladen werden: {Message}", typeId, ex.Message);
                break;
            }
        }

        return TypeHierarchy.FirstOrDefault()?.PropertyDefinitions ?? Array.Empty<PropertyDefinitionDto>();
    }

    /// <summary>Baut die Property-Zeilen aus den Objekt-Properties und den Property-Definitionen des Typs.</summary>
    private void BuildPropertyRows(IReadOnlyList<PropertyDefinitionDto> definitions)
    {
        Properties.Clear();

        foreach (var property in _target.Properties)
        {
            var definition = definitions.FirstOrDefault(d => string.Equals(d.Id, property.Id, StringComparison.Ordinal));

            Properties.Add(new ExtendedPropertyRowViewModel
            {
                DisplayName = definition?.DisplayName ?? property.DisplayName ?? property.Id,
                PropertyId = property.Id,
                LocalName = definition?.LocalName ?? string.Empty,
                QueryName = property.QueryName ?? definition?.QueryName ?? string.Empty,
                DataType = (definition?.PropertyType ?? property.PropertyType)?.ToString() ?? string.Empty,
                IsMultiValued = property.IsMultiValued,
                IsRequired = definition?.IsRequired ?? false,
                Value = property.IsMultiValued
                    ? string.Join("; ", property.Values.Select(v => v?.ToString() ?? string.Empty))
                    : property.ValueAsString ?? property.Value?.ToString() ?? string.Empty
            });
        }
    }

    private void BuildAllowableActions()
    {
        AllowableActions.Clear();
        if (_target.AllowableActions is null)
        {
            return;
        }

        foreach (var action in _target.AllowableActions.OrderBy(a => a, StringComparer.OrdinalIgnoreCase))
        {
            AllowableActions.Add(action);
        }

        OnPropertyChanged(nameof(HasAllowableActions));
    }

    private async Task LoadAclAsync(CancellationToken ct)
    {
        AclEntries.Clear();
        try
        {
            var entries = await _objectService.GetAclAsync(_target.Id, ct).ConfigureAwait(true);
            foreach (var entry in entries)
            {
                AclEntries.Add(new AclEntryRowViewModel
                {
                    PrincipalId = entry.PrincipalId,
                    Permissions = string.Join(", ", entry.Permissions),
                    IsDirect = entry.IsDirect
                });
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (CmisAppException ex)
        {
            // Manche Repositories (z. B. der InMemory-Server ohne ACL-Unterstützung)
            // lehnen die ACL-Abfrage ab; die restliche Ansicht bleibt trotzdem nutzbar.
            _logger.LogWarning("ACL konnte nicht geladen werden: {Message}", ex.Message);
        }

        OnPropertyChanged(nameof(HasAclEntries));
    }

    private async Task LoadVersionsAsync(CancellationToken ct)
    {
        Versions.Clear();
        try
        {
            var versions = await _objectService.GetAllVersionsAsync(_target.Id, ct).ConfigureAwait(true);
            foreach (var version in versions)
            {
                Versions.Add(version);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (CmisAppException ex)
        {
            _logger.LogWarning("Versionsreihe konnte nicht geladen werden: {Message}", ex.Message);
        }

        OnPropertyChanged(nameof(HasVersions));
    }
}
