using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CMISPilot.Cmis.Models;

namespace CMISPilot.ViewModels.Export;

/// <summary>
/// Exportiert Listen als Tabelle in eine Datei (F3): das Ergebnis einer CMISQL-Abfrage
/// und die Objektliste eines Ordners. Abstraktion analog
/// <see cref="ITypeDefinitionExporter"/>, damit die ViewModels WPF- und
/// dateiformat-frei bleiben; die konkrete Excel-Erzeugung (ClosedXML) liegt in der
/// Implementierung.
/// </summary>
public interface IListExporter
{
    /// <summary>
    /// Schreibt ein Abfrageergebnis (eine Zeile je Treffer, eine Spalte je Query-Name)
    /// in die angegebene Datei. Ueber dem Tabellenkopf steht ein Kopfbereich mit der
    /// ausgefuehrten Abfrage und der Trefferzahl.
    /// </summary>
    /// <param name="columnNames">Spaltennamen (Query-Namen) in Reihenfolge der SELECT-Liste.</param>
    /// <param name="rows">Die zu exportierenden Ergebniszeilen.</param>
    /// <param name="cmisql">Die ausgefuehrte Abfrage fuer den Kopfbereich (optional).</param>
    /// <param name="filePath">Zielpfad der Datei.</param>
    Task ExportQueryResultAsync(
        IReadOnlyList<string> columnNames,
        IReadOnlyList<QueryRowDto> rows,
        string? cmisql,
        string filePath,
        CancellationToken ct = default);

    /// <summary>
    /// Schreibt die Objektliste eines Ordners (eine Zeile je Kindobjekt) in die
    /// angegebene Datei. Ueber dem Tabellenkopf steht ein Kopfbereich mit Ordnername
    /// und Objektzahl.
    /// </summary>
    /// <param name="folder">Der Ordner, dessen Inhalt exportiert wird (Kopfbereich).</param>
    /// <param name="objects">Die zu exportierenden Kindobjekte.</param>
    /// <param name="filePath">Zielpfad der Datei.</param>
    Task ExportObjectListAsync(
        CmisObjectDto folder,
        IReadOnlyList<CmisObjectDto> objects,
        string filePath,
        CancellationToken ct = default);

    /// <summary>
    /// Schreibt die Eigenschaften eines einzelnen Objekts (eine Zeile je Property) in
    /// die angegebene Datei — das Excel-Gegenstück zum Eigenschaften-Werkzeugfenster.
    /// </summary>
    /// <param name="target">Das inspizierte Objekt (Kopfbereich: Name, ID, Typ).</param>
    /// <param name="rows">Die anzuzeigenden Property-Zeilen, in Anzeigereihenfolge.</param>
    /// <param name="filePath">Zielpfad der Datei.</param>
    /// <summary>
    /// Schreibt die Repository-Informationen (FA-10/FA-11) als Excel-Datei: Eckdaten im
    /// Kopfbereich, darunter eine Tabelle mit Abschnitt, Bezeichnung und Wert.
    /// </summary>
    /// <param name="info">Das beschriebene Repository (Kopfbereich, Dateiname).</param>
    /// <param name="rows">Die Zeilen, bereits nach Abschnitten gruppiert und beschriftet.</param>
    /// <param name="filePath">Zielpfad der .xlsx-Datei.</param>
    /// <param name="ct">Abbruchtoken.</param>
    /// <param name="permissionMapping">
    /// Die Berechtigungszuordnung, eine Zeile je Paar. Kommt auf ein eigenes Blatt,
    /// weil sie als einzige Angabe eine andere Spaltenform hat als der Rest — in der
    /// Uebersichtstabelle stuende sie mit Komma verbunden in einer Zelle und liesse
    /// sich nicht filtern. Leer: das Blatt entfaellt.
    /// </param>
    Task ExportRepositoryInfoAsync(
        RepositoryInfoDto info,
        IReadOnlyList<RepositoryInfoRowExportDto> rows,
        IReadOnlyList<PermissionMappingExportDto> permissionMapping,
        string filePath,
        CancellationToken ct = default);

    Task ExportObjectPropertiesAsync(
        CmisObjectDto target,
        IReadOnlyList<PropertyRowExportDto> rows,
        string filePath,
        CancellationToken ct = default);
}

/// <summary>
/// Eine Property-Zeile für <see cref="IListExporter.ExportObjectPropertiesAsync"/> —
/// dieselben Spalten wie das Eigenschaften-Werkzeugfenster. Bewusst kein
/// ViewModel-Typ (NFA-03: der Exporter kennt nur <c>CMISPilot.Cmis.Models</c>).
/// </summary>
public sealed record PropertyRowExportDto(
    string DisplayName,
    string PropertyId,
    string DataType,
    bool IsRequired,
    string Value,
    string OwningTypeId);

/// <summary>Eine Zeile des Repository-Info-Exports.</summary>
/// <param name="Section">Abschnitt, z. B. „Eckdaten" oder „Capabilities".</param>
/// <param name="Name">Bezeichnung des Wertes.</param>
/// <param name="Value">Der Wert als Text.</param>
public sealed record RepositoryInfoRowExportDto(string Section, string Name, string Value);

/// <summary>Eine Zeile der Berechtigungszuordnung im Repository-Info-Export.</summary>
/// <param name="Key">CMIS-Schluessel der Operation, z. B. <c>canGetProperties.Object</c>.</param>
/// <param name="Permission">Eine Berechtigung, die dafuer genuegt.</param>
public sealed record PermissionMappingExportDto(string Key, string Permission);
