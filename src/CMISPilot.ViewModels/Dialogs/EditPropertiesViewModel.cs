using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using CMISPilot.Cmis.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CMISPilot.ViewModels.Dialogs;

/// <summary>Ob der Dialog ein neues Objekt anlegt oder ein bestehendes bearbeitet.</summary>
public enum EditPropertiesMode
{
    Create,
    Edit
}

/// <summary>
/// ViewModel des separaten Bearbeiten-/Anlegen-Dialogs (M7, T7.3, FA-70/71/72).
/// Deckt beide Fälle ab: „Neuer Ordner"/„Neues Dokument" (<see cref="ForCreate"/>)
/// und „Eigenschaften bearbeiten" (<see cref="ForEdit"/>, vorbelegt aus dem Objekt).
///
/// Beim Anlegen kann der konkrete <see cref="SelectedType">Objekttyp</see> aus den
/// erzeugbaren Untertypen gewählt werden; die Pflicht-Property-Definitionen des
/// gewählten Typs werden dann automatisch als Felder abgefragt (FA-71/72). Validiert
/// Pflichtfelder und Datentypen (<see cref="Validate"/>); übernommen wird nur bei
/// „Speichern". Bewusst WPF-frei (NFA-03).
/// </summary>
public sealed partial class EditPropertiesViewModel : ObservableObject
{
    /// <summary>
    /// Serverseitig verwaltete System-Properties, die im Dialog nicht editierbar sind
    /// (Audit-Felder, Content-Stream-Infos, Versionierung, IDs).
    /// </summary>
    private static readonly HashSet<string> ReadOnlyIds = new(StringComparer.Ordinal)
    {
        "cmis:objectId", "cmis:baseTypeId", "cmis:objectTypeId", "cmis:secondaryObjectTypeIds",
        "cmis:createdBy", "cmis:creationDate", "cmis:lastModifiedBy", "cmis:lastModificationDate",
        "cmis:changeToken", "cmis:path", "cmis:parentId",
        "cmis:contentStreamLength", "cmis:contentStreamMimeType", "cmis:contentStreamFileName",
        "cmis:contentStreamId", "cmis:versionSeriesId", "cmis:versionLabel", "cmis:isLatestVersion",
        "cmis:isMajorVersion", "cmis:isLatestMajorVersion", "cmis:versionSeriesCheckedOutBy",
        "cmis:versionSeriesCheckedOutId", "cmis:isVersionSeriesCheckedOut", "cmis:checkinComment",
        "cmis:isImmutable"
    };

    private EditPropertiesViewModel(
        EditPropertiesMode mode,
        string dialogTitle,
        string? objectId,
        IEnumerable<EditablePropertyViewModel> properties)
    {
        Mode = mode;
        DialogTitle = dialogTitle;
        ObjectId = objectId;
        foreach (var p in properties)
        {
            Properties.Add(p);
        }
    }

    public EditPropertiesMode Mode { get; }
    public string DialogTitle { get; }

    /// <summary>Objekt-ID des bearbeiteten Objekts (null im Create-Modus).</summary>
    public string? ObjectId { get; }

    /// <summary>Editierbare Felder (Multi-Value-Properties und System-Felder sind ausgeblendet).</summary>
    public ObservableCollection<EditablePropertyViewModel> Properties { get; } = new();

    // --- Objekttyp-Auswahl (nur Create, FA-71) ---

    /// <summary>Erzeugbare Objekttypen zur Auswahl (Basistyp + abgeleitete, nur Create).</summary>
    public ObservableCollection<TypeDefinitionDto> AvailableTypes { get; } = new();

    /// <summary>
    /// Gewählter Objekttyp. Ein Wechsel baut die abgefragten Pflichtfelder neu auf und
    /// entscheidet mit, ob eine Datei als Inhalt mitgegeben werden kann (<see cref="AllowContent"/>).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllowContent))]
    private TypeDefinitionDto? _selectedType;

    /// <summary>True, wenn im Dialog ein Objekttyp gewählt werden kann (Create mit mehr als einem Typ).</summary>
    public bool ShowTypeSelector => Mode == EditPropertiesMode.Create && AvailableTypes.Count > 1;

    /// <summary>Aktueller Name (für Titel/Meldungen des aufrufenden ViewModels).</summary>
    public string Name => Properties.FirstOrDefault(p => p.Id == "cmis:name")?.Value ?? string.Empty;

    // --- Datei-Inhalt beim Anlegen (F1) ---

    /// <summary>
    /// True, wenn beim Anlegen eine Datei als Inhalt mitgegeben werden kann: also im
    /// Create-Modus und wenn der gewählte Typ auf <c>cmis:document</c> basiert (das gilt
    /// damit auch für alle abgeleiteten Dokumenttypen). Ordner haben keinen Inhalt.
    /// </summary>
    public bool AllowContent =>
        Mode == EditPropertiesMode.Create && SelectedType?.BaseType == CmisBaseType.Document;

    /// <summary>
    /// Lokaler Pfad der Datei, die beim Anlegen als Content-Stream mitgespeichert wird
    /// (null bzw. leer = Dokument ohne Inhalt anlegen). Wird vom Dialog gesetzt.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContentFileName))]
    [NotifyPropertyChangedFor(nameof(HasContentFile))]
    private string? _contentFilePath;

    /// <summary>Dateiname der gewählten Datei (für die Anzeige im Dialog).</summary>
    public string ContentFileName =>
        string.IsNullOrEmpty(ContentFilePath) ? string.Empty : Path.GetFileName(ContentFilePath);

    /// <summary>True, wenn eine Datei gewählt wurde.</summary>
    public bool HasContentFile => !string.IsNullOrEmpty(ContentFilePath);

    /// <summary>
    /// Übernimmt den Dateinamen als Objektnamen, solange der Nutzer noch keinen Namen
    /// eingegeben hat. Spart beim häufigsten Fall („Datei hochladen") die Doppeleingabe;
    /// ein bereits eingegebener Name bleibt unangetastet.
    /// </summary>
    partial void OnContentFilePathChanged(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        var nameField = Properties.FirstOrDefault(p => p.Id == "cmis:name");
        if (nameField is not null && string.IsNullOrWhiteSpace(nameField.Value))
        {
            nameField.Value = Path.GetFileName(value);
            OnPropertyChanged(nameof(Name));
        }
    }

    /// <summary>
    /// Erzeugt das ViewModel für „Neuer Ordner"/„Neues Dokument" (FA-70/71). Aus den
    /// übergebenen erzeugbaren Typen kann einer gewählt werden; abgefragt werden der
    /// Name und die Pflicht-Properties des gewählten Typs.
    /// </summary>
    public static EditPropertiesViewModel ForCreate(
        string dialogTitle, IReadOnlyList<TypeDefinitionDto> candidateTypes, string? defaultTypeId)
    {
        ArgumentNullException.ThrowIfNull(candidateTypes);

        var vm = new EditPropertiesViewModel(
            EditPropertiesMode.Create, dialogTitle, objectId: null,
            properties: Array.Empty<EditablePropertyViewModel>());

        foreach (var t in candidateTypes)
        {
            vm.AvailableTypes.Add(t);
        }

        vm.SelectedType =
            vm.AvailableTypes.FirstOrDefault(t => string.Equals(t.Id, defaultTypeId, StringComparison.Ordinal))
            ?? vm.AvailableTypes.FirstOrDefault();

        // Falls kein Typ vorhanden ist (Fallback), wenigstens den Namen abfragen.
        if (vm.SelectedType is null)
        {
            vm.Properties.Add(NameField());
        }

        return vm;
    }

    /// <summary>
    /// Erzeugt das ViewModel für „Eigenschaften bearbeiten" (FA-72), vorbelegt mit den
    /// aktuellen (einwertigen, nicht system-verwalteten) Properties des Objekts.
    /// </summary>
    public static EditPropertiesViewModel ForEdit(CmisObjectDto target)
    {
        ArgumentNullException.ThrowIfNull(target);

        var editable = target.Properties
            .Where(p => !ReadOnlyIds.Contains(p.Id) && !p.IsMultiValued)
            .Select(p => new EditablePropertyViewModel(
                p.Id,
                p.DisplayName ?? p.Id,
                p.PropertyType ?? CmisPropertyType.String,
                isRequired: p.Id == "cmis:name",
                value: p.ValueAsString ?? string.Empty));

        return new EditPropertiesViewModel(
            EditPropertiesMode.Edit, $"Eigenschaften bearbeiten – {target.Name}", target.Id, editable);
    }

    /// <summary>
    /// Baut bei Typwechsel die abgefragten Felder neu auf: Name, dann alle beim Anlegen
    /// beschreibbaren Properties des Typs — Pflichtfelder zuerst, danach die optionalen.
    /// </summary>
    partial void OnSelectedTypeChanged(TypeDefinitionDto? value)
    {
        // Bereits eingegebenen Namen über den Typwechsel hinweg erhalten.
        var currentName = Name;

        Properties.Clear();
        Properties.Add(NameField(currentName));

        if (value is not null)
        {
            foreach (var pd in CreatableFields(value))
            {
                Properties.Add(new EditablePropertyViewModel(
                    pd.Id, pd.DisplayName ?? pd.Id, pd.PropertyType,
                    isRequired: pd.IsRequired == true, value: string.Empty));
            }
        }

        OnPropertyChanged(nameof(Name));
    }

    /// <summary>
    /// Property-Definitionen eines Typs, die beim Anlegen gesetzt werden können:
    /// einwertig, kein System-/Name-Feld und beim Anlegen beschreibbar. Sortiert so,
    /// dass Pflichtfelder zuerst erscheinen (innerhalb der Gruppen bleibt die
    /// Definitionsreihenfolge erhalten – <see cref="Enumerable.OrderByDescending{TSource, TKey}(IEnumerable{TSource}, Func{TSource, TKey})"/> ist stabil).
    /// </summary>
    private static IEnumerable<PropertyDefinitionDto> CreatableFields(TypeDefinitionDto type) =>
        type.PropertyDefinitions
            .Where(pd =>
                pd.Id != "cmis:name"
                && !ReadOnlyIds.Contains(pd.Id)
                && pd.Cardinality != CmisCardinality.Multi
                && pd.Updatability is null or CmisUpdatability.ReadWrite or CmisUpdatability.OnCreate)
            .OrderByDescending(pd => pd.IsRequired == true);

    private static EditablePropertyViewModel NameField(string value = "") =>
        new("cmis:name", "Name", CmisPropertyType.String, isRequired: true, value: value);

    /// <summary>
    /// Validiert Pflichtfelder und Datentypen (FA-72) und setzt je Feld
    /// <see cref="EditablePropertyViewModel.ErrorMessage"/>. Liefert <c>true</c>, wenn alle
    /// Felder gültig sind.
    /// </summary>
    public bool Validate()
    {
        var ok = true;
        foreach (var p in Properties)
        {
            p.ErrorMessage = null;

            if (p.IsRequired && string.IsNullOrWhiteSpace(p.Value))
            {
                p.ErrorMessage = "Pflichtfeld.";
                ok = false;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(p.Value) && !TryConvert(p, out _, out var error))
            {
                p.ErrorMessage = error;
                ok = false;
            }
        }

        return ok;
    }

    /// <summary>
    /// Baut die Properties-Map für den <c>IObjectService</c>-Aufruf (nur nach gültiger
    /// Validierung aufrufen; wirft sonst). Leere, nicht-pflichtige Felder werden als
    /// <c>null</c> übergeben (löscht die Property serverseitig bei Update).
    /// </summary>
    public IDictionary<string, object?> BuildProperties()
    {
        if (!Validate())
        {
            throw new InvalidOperationException(
                "Die Properties sind ungültig; Validate() muss vorher erfolgreich sein.");
        }

        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var p in Properties)
        {
            if (string.IsNullOrWhiteSpace(p.Value))
            {
                result[p.Id] = null;
                continue;
            }

            result[p.Id] = TryConvert(p, out var value, out _) ? value : p.Value;
        }

        if (Mode == EditPropertiesMode.Create && SelectedType is not null)
        {
            result["cmis:objectTypeId"] = SelectedType.Id;
        }

        return result;
    }

    private static bool TryConvert(EditablePropertyViewModel p, out object? value, out string? error)
    {
        error = null;
        switch (p.PropertyType)
        {
            case CmisPropertyType.Boolean:
                if (bool.TryParse(p.Value, out var b))
                {
                    value = b;
                    return true;
                }
                error = "Ungültiger Wert (erwartet: true/false).";
                value = null;
                return false;

            case CmisPropertyType.Integer:
                if (long.TryParse(p.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
                {
                    value = l;
                    return true;
                }
                error = "Ungültige Ganzzahl.";
                value = null;
                return false;

            case CmisPropertyType.Decimal:
                if (decimal.TryParse(p.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
                {
                    value = d;
                    return true;
                }
                error = "Ungültige Dezimalzahl.";
                value = null;
                return false;

            case CmisPropertyType.DateTime:
                // Vorbelegt wird mit PortCMIS' Property.ValueAsString (FormatValue ruft
                // value.ToString() auf ein DateTime auf, also in CurrentCulture formatiert
                // – auf einer deutschen Windows-Installation "31.12.2025"). Damit ein
                // unveraendert uebernommener Wert wieder geparst werden kann, muss hier
                // dieselbe Kultur gelten; reines InvariantCulture-Parsing (MM/dd/yyyy)
                // wies deutsch eingegebene Daten wie "31.12.2025" zurueck. Invariant bleibt
                // als Fallback fuer direkt eingegebene ISO-Werte (z. B. copy&paste).
                //
                // .DateTime statt .UtcDateTime: PortCMIS' DateTimeHelper rechnet beim
                // Senden/Empfangen nur ueber DateTime.Ticks in Millisekunden um
                // (ConvertDateTimeToMillis/ConvertMillisToDateTime), ganz ohne
                // Zeitzonenumrechnung - der Server behandelt cmis:DateTime-Werte also
                // als zeitzonenlose Wanduhrzeit, nicht als echten UTC-Zeitpunkt. Mit
                // .UtcDateTime (frueher hier) wurde "01.01.2026" ueber die lokale
                // Zeitzone in UTC verschoben und kam als "31.12.2025 23:00:00" zurueck
                // (Winterzeit UTC+1) - real mit dem A1-analogen Alfresco-Testsystem
                // reproduziert. .DateTime uebernimmt die eingegebenen Wanduhr-Werte
                // unveraendert, genau das, was beim erneuten Anzeigen (ebenfalls ohne
                // Zeitzonenumrechnung, siehe oben) wieder herauskommen muss.
                if (DateTimeOffset.TryParse(
                        p.Value, CultureInfo.CurrentCulture, DateTimeStyles.None, out var dto)
                    || DateTimeOffset.TryParse(
                        p.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out dto))
                {
                    value = dto.DateTime;
                    return true;
                }
                error = "Ungültiges Datum.";
                value = null;
                return false;

            default:
                value = p.Value;
                return true;
        }
    }
}
