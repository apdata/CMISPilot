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

namespace CMISPilot.ViewModels.Explorer;

/// <summary>
/// Werkzeugfenster „Eigenschaften" (R4 Etappe 3, ersetzt den bisherigen Platzhalter
/// <c>tool:properties</c>): folgt der Auswahl im Server-Baum und in der Objektliste
/// des Explorer-Dokument-Tabs über die <see cref="NodeSelectedMessage"/> und zeigt je
/// Property Displayname, PropertyID, Datentyp, Pflichtfeld und den liefernden Typ an.
///
/// Die Metadaten (Datentyp/Pflichtfeld/Displayname/Herkunftstyp) stammen aus den
/// Typdefinitionen des selektierten Objekts: dem primären Typ
/// (<see cref="ITypeService.GetTypeDefinitionAsync"/> über <see cref="CmisObjectDto.TypeId"/>)
/// sowie allen zugewiesenen Secondary Types/Aspekten
/// (<see cref="CmisObjectDto.SecondaryTypeIds"/>). Eine Property-ID, die in mehreren
/// Typen vorkommt, gewinnt aus dem zuletzt geladenen Typ (Reihenfolge: primär, dann
/// Secondary Types in Server-Reihenfolge) — in der Praxis kollidieren Secondary-Type-
/// Properties nicht mit dem Primärtyp. Die Rohwerte kommen aus
/// <see cref="CmisObjectDto.Properties"/>. Wird keine passende Property-Definition
/// gefunden, bleiben Datentyp/Pflichtfeld/Herkunftstyp leer/false und der rohe Wert
/// wird trotzdem angezeigt.
///
/// Bewusst WPF-frei (NFA-03); Serveraufrufe laufen async über
/// <see cref="ViewModelBase.BeginBusy"/>, nie <c>.Result</c>/<c>.Wait()</c> (NFA-13).
/// </summary>
public sealed partial class PropertiesViewModel : ToolViewModelBase, IRecipient<NodeSelectedMessage>
{
    private readonly ITypeService _typeService;
    private readonly IDialogService _dialogService;
    private readonly IListExporter _listExporter;
    private readonly ILogger<PropertiesViewModel> _logger;

    /// <summary>Das aktuell inspizierte Objekt (Namensvorschlag/Kopfbereich beim Export).</summary>
    private CmisObjectDto? _current;

    /// <summary>Eine Property-Definition zusammen mit der ID des Typs, der sie liefert.</summary>
    private readonly record struct PropertyDefinitionWithOwner(PropertyDefinitionDto Definition, string OwningTypeId);

    /// <param name="typeService">Lädt die Typdefinition (inkl. Property-Definitionen) des selektierten Objekts.</param>
    /// <param name="messenger">Meldet die Registrierung als Empfänger von <see cref="NodeSelectedMessage"/> an.</param>
    /// <param name="dialogService">Fragt den Zielpfad für den Excel-Export ab.</param>
    /// <param name="listExporter">Schreibt die Property-Zeilen als Excel-Datei.</param>
    /// <param name="logger">Meldet Erfolg/Fehler des Exports (landet in Ausgabe/Fehlerliste).</param>
    public PropertiesViewModel(
        ITypeService typeService,
        IMessenger messenger,
        IDialogService dialogService,
        IListExporter listExporter,
        ILogger<PropertiesViewModel> logger)
        : base("tool:properties", ToolDock.Right)
    {
        _typeService = typeService;
        _dialogService = dialogService;
        _listExporter = listExporter;
        _logger = logger;

        Title = "Eigenschaften";

        Properties.CollectionChanged += (_, _) => ExportCommand.NotifyCanExecuteChanged();

        messenger.RegisterAll(this);
    }

    /// <summary>Die Eigenschaften des aktuell inspizierten Objekts, je eine Zeile pro Property.</summary>
    public ObservableCollection<PropertyRowViewModel> Properties { get; } = new();

    /// <summary>
    /// Die Zeilen, die zum aktuellen Filter passen — daran haengt die Tabelle.
    ///
    /// <para>Bewusst eine zweite Sammlung statt eines <c>ICollectionView</c>-Filters:
    /// der waere ein WPF-Typ und muesste im Code-Behind an die Lebensdauer des Grids
    /// gehaengt werden. Genau daran ist die erste Fassung gescheitert — AvalonDock
    /// haengt Werkzeugfenster beim Wiederherstellen des Layouts um, wodurch das
    /// Abonnement auf Filteraenderungen verlorenging und der Filter zwar gesetzt, aber
    /// nie neu ausgewertet wurde. Hier haengt nichts an der Ansicht.</para>
    ///
    /// <para><see cref="Properties"/> bleibt die vollstaendige Liste; der Excel-Export
    /// arbeitet weiter darauf und liefert deshalb immer alle Zeilen.</para>
    /// </summary>
    public ObservableCollection<PropertyRowViewModel> VisibleProperties { get; } = new();

    // --- Filter ---

    /// <summary>Suchtext des Filterfeldes unter der Eigenschaften-Tabelle. Leer = kein Filter.</summary>
    [ObservableProperty]
    private string _filterText = string.Empty;

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    /// <summary>Bezieht die Spalte „Displayname" in den Filter ein.</summary>
    [ObservableProperty]
    private bool _filterByDisplayName = true;

    partial void OnFilterByDisplayNameChanged(bool value) => ApplyFilter();

    /// <summary>Bezieht die Spalte „PropertyID" in den Filter ein.</summary>
    [ObservableProperty]
    private bool _filterByPropertyId = true;

    partial void OnFilterByPropertyIdChanged(bool value) => ApplyFilter();

    /// <summary>Bezieht die Spalte „Wert" in den Filter ein.</summary>
    [ObservableProperty]
    private bool _filterByValue = true;

    partial void OnFilterByValueChanged(bool value) => ApplyFilter();

    /// <summary>
    /// Prueft, ob eine Zeile zum aktuellen Filter passt. Verglichen wird als Teilstring
    /// ohne Ruecksicht auf Gross-/Kleinschreibung ueber genau die angehakten Spalten;
    /// mehrere Haken wirken als Oder-Verknuepfung.
    ///
    /// <para>Ist kein Suchtext gesetzt oder ist keine einzige Spalte angehakt, passt
    /// jede Zeile. Der zweite Fall koennte auch alles ausblenden - eine Tabelle, die
    /// ohne erkennbaren Grund leer ist, waere aber die schlechtere Ueberraschung.</para>
    /// </summary>
    public bool Matches(PropertyRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        var needle = FilterText?.Trim() ?? string.Empty;
        if (needle.Length == 0)
        {
            return true;
        }

        if (!FilterByDisplayName && !FilterByPropertyId && !FilterByValue)
        {
            return true;
        }

        return (FilterByDisplayName && Contains(row.DisplayName, needle))
            || (FilterByPropertyId && Contains(row.PropertyId, needle))
            || (FilterByValue && Contains(row.Value, needle));

        static bool Contains(string? value, string needle) =>
            value?.Contains(needle, StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>Baut <see cref="VisibleProperties"/> aus <see cref="Properties"/> neu auf.</summary>
    private void ApplyFilter()
    {
        VisibleProperties.Clear();
        foreach (var row in Properties.Where(Matches))
        {
            VisibleProperties.Add(row);
        }
    }

    /// <summary>Reagiert auf eine neue Knoten-/Objektauswahl im Baum oder in der Objektliste.</summary>
    public void Receive(NodeSelectedMessage message) =>
        _ = InspectAsync(message.CmisObject, CancellationToken.None);

    /// <summary>Lädt die Typdefinition des Objekts nach und baut die Property-Zeilen auf.</summary>
    private async Task InspectAsync(CmisObjectDto? cmisObject, CancellationToken ct)
    {
        Properties.Clear();
        VisibleProperties.Clear();
        _current = cmisObject;

        if (cmisObject is null)
        {
            return;
        }

        var definitions = await LoadPropertyDefinitionsAsync(cmisObject, ct).ConfigureAwait(true);

        foreach (var property in cmisObject.Properties)
        {
            definitions.TryGetValue(property.Id, out var match);

            Properties.Add(new PropertyRowViewModel
            {
                DisplayName = match.Definition?.DisplayName ?? property.DisplayName ?? property.Id,
                PropertyId = property.Id,
                DataType = match.Definition?.PropertyType.ToString() ?? string.Empty,
                IsRequired = match.Definition?.IsRequired ?? false,
                Value = property.ValueAsString ?? property.Value?.ToString() ?? string.Empty,
                OwningTypeId = match.OwningTypeId ?? string.Empty
            });
        }

        ApplyFilter();
    }

    /// <summary>
    /// Exportiert die Eigenschaften des aktuell inspizierten Objekts als Excel-Datei
    /// (Kontextmenü der Eigenschaften-Tabelle). Fragt über den Dialog-Dienst einen
    /// Zielpfad ab und meldet Erfolg/Fehler über den Logger.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAsync(CancellationToken ct)
    {
        if (_current is not { } target || Properties.Count == 0)
        {
            return;
        }

        var suggestedName = $"{Sanitize(target.Name ?? target.Id)} - Eigenschaften.xlsx";
        var path = await _dialogService.PickSaveFileAsync(suggestedName).ConfigureAwait(true);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        if (!path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            path += ".xlsx";
        }

        var rows = Properties
            .Select(p => new PropertyRowExportDto(
                p.DisplayName, p.PropertyId, p.DataType, p.IsRequired, p.Value, p.OwningTypeId))
            .ToList();

        using (BeginBusy())
        {
            try
            {
                await _listExporter.ExportObjectPropertiesAsync(target, rows, path, ct).ConfigureAwait(true);
                _logger.LogInformation(
                    "Eigenschaften von \"{Name}\" wurden nach \"{Path}\" exportiert.",
                    target.Name ?? target.Id, path);
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

    private bool CanExport() => !IsBusy && Properties.Count > 0;

    /// <summary>Ersetzt in einem Vorschlags-Dateinamen die unter Windows unzulaessigen Zeichen.</summary>
    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return name;
    }

    // IsBusy liegt in der Basisklasse per [ObservableProperty]; der generierte
    // OnIsBusyChanged-Hook laesst sich hier nicht implementieren – daher ueber
    // OnPropertyChanged (Referenzmuster aus ExplorerDocumentViewModel).
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(IsBusy))
        {
            ExportCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    /// Lädt die Property-Definitionen des primären Typs und aller Secondary Types
    /// (Aspekte) des Objekts, je Property-ID mit dem liefernden Typ. Ein einzelner
    /// nicht ladbarer Typ (z. B. ein Server-Fehler bei einem Secondary Type) lässt die
    /// übrigen Typen unberührt; die betroffenen Zeilen zeigen dann trotzdem den rohen
    /// PropertyDto-Wert, nur ohne Metadaten.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, PropertyDefinitionWithOwner>> LoadPropertyDefinitionsAsync(
        CmisObjectDto cmisObject, CancellationToken ct)
    {
        var result = new Dictionary<string, PropertyDefinitionWithOwner>(StringComparer.Ordinal);
        var typeIds = EnumerateTypeIds(cmisObject).ToList();

        if (typeIds.Count == 0)
        {
            return result;
        }

        using (BeginBusy())
        {
            try
            {
                foreach (var typeId in typeIds)
                {
                    try
                    {
                        var typeDefinition = await _typeService.GetTypeDefinitionAsync(typeId, ct).ConfigureAwait(true);
                        foreach (var definition in typeDefinition.PropertyDefinitions)
                        {
                            result[definition.Id] = new PropertyDefinitionWithOwner(definition, typeId);
                        }
                    }
                    catch (CmisAppException)
                    {
                        // Definition dieses einen Typs nicht ladbar: übrige Typen/Properties bleiben nutzbar.
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        return result;
    }

    /// <summary>Primärer Typ zuerst, danach die zugewiesenen Secondary Types.</summary>
    private static IEnumerable<string> EnumerateTypeIds(CmisObjectDto cmisObject)
    {
        if (!string.IsNullOrEmpty(cmisObject.TypeId))
        {
            yield return cmisObject.TypeId;
        }

        foreach (var secondaryTypeId in cmisObject.SecondaryTypeIds)
        {
            if (!string.IsNullOrEmpty(secondaryTypeId))
            {
                yield return secondaryTypeId;
            }
        }
    }
}
