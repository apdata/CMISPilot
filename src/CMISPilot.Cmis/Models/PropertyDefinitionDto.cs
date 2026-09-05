namespace CMISPilot.Cmis.Models;

/// <summary>
/// UI-freundliche CMIS-Property-Definition (FA-62). Eigene Abbildung von PortCMIS
/// <c>IPropertyDefinition</c>.
/// </summary>
public sealed class PropertyDefinitionDto
{
    public string Id { get; init; } = string.Empty;
    public string? LocalName { get; init; }
    public string? DisplayName { get; init; }
    public string? QueryName { get; init; }
    public string? Description { get; init; }

    public CmisPropertyType PropertyType { get; init; } = CmisPropertyType.String;
    public CmisCardinality? Cardinality { get; init; }
    public CmisUpdatability? Updatability { get; init; }

    public bool? IsInherited { get; init; }
    public bool? IsRequired { get; init; }
    public bool? IsQueryable { get; init; }
    public bool? IsOrderable { get; init; }
    public bool? IsOpenChoice { get; init; }

    /// <summary>Maximale Laenge (nur String-Properties, sonst null).</summary>
    public long? MaxLength { get; init; }

    /// <summary>Kleinster erlaubter Wert (nur Integer-/Decimal-Properties), als invarianter Text.</summary>
    public string? MinValue { get; init; }

    /// <summary>Groesster erlaubter Wert (nur Integer-/Decimal-Properties), als invarianter Text.</summary>
    public string? MaxValue { get; init; }

    /// <summary>Dezimal-Praezision in Bit ("32"/"64", nur Decimal-Properties, sonst null).</summary>
    public string? Precision { get; init; }
}
