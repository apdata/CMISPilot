using System.Threading;
using System.Threading.Tasks;
using CMISPilot.Cmis.Models;

namespace CMISPilot.ViewModels.Export;

/// <summary>
/// Exportiert eine CMIS-Typdefinition als Tabelle in eine Datei (F2). Abstraktion,
/// damit die ViewModels WPF- und dateiformat-frei bleiben; die konkrete
/// Excel-Erzeugung (ClosedXML) liegt in der Implementierung.
/// </summary>
public interface ITypeDefinitionExporter
{
    /// <summary>
    /// Schreibt Typ-Attribute und alle Property-Definitionen des Typs (eine Zeile je
    /// Property) in die angegebene Datei.
    /// </summary>
    /// <param name="type">Der zu exportierende Typ inkl. Property-Definitionen.</param>
    /// <param name="filePath">Zielpfad der Datei.</param>
    Task ExportAsync(TypeDefinitionDto type, string filePath, CancellationToken ct = default);
}
