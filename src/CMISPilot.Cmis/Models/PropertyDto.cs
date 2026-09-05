using System.Collections.Generic;

namespace CMISPilot.Cmis.Models;

/// <summary>
/// UI-freundliche CMIS-Property eines Objekts oder einer Query-Zeile. Eigene
/// Abbildung von PortCMIS <c>IProperty</c>/<c>IPropertyData</c>.
/// </summary>
public sealed class PropertyDto
{
    /// <summary>Property-ID, z. B. "cmis:name".</summary>
    public string Id { get; init; } = string.Empty;

    public string? DisplayName { get; init; }
    public string? QueryName { get; init; }

    /// <summary>Datentyp der Property (null, wenn unbekannt).</summary>
    public CmisPropertyType? PropertyType { get; init; }

    public bool IsMultiValued { get; init; }

    /// <summary>Erster/einziger Wert (null bei leerer Property).</summary>
    public object? Value { get; init; }

    /// <summary>Alle Werte (bei mehrwertigen Properties). Nie null, ggf. leer.</summary>
    public IReadOnlyList<object?> Values { get; init; } = System.Array.Empty<object?>();

    /// <summary>Anzeigefreundliche String-Repräsentation des Werts.</summary>
    public string? ValueAsString { get; init; }
}
