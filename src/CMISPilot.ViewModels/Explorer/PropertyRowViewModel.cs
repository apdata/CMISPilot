namespace CMISPilot.ViewModels.Explorer;

/// <summary>
/// Eine Zeile im Eigenschaften-Werkzeugfenster (R4 Etappe 3): fasst den Property-Wert
/// (<c>PropertyDto</c>) mit den Metadaten aus der passenden Property-Definition des
/// Objekttyps (<c>PropertyDefinitionDto</c>) zusammen. Reines Datenobjekt, WPF-frei
/// (NFA-03).
/// </summary>
public sealed record PropertyRowViewModel
{
    /// <summary>Anzeigename der Property (aus der Typdefinition, sonst die PropertyID).</summary>
    public required string DisplayName { get; init; }

    /// <summary>Property-ID, z. B. "cmis:name".</summary>
    public required string PropertyId { get; init; }

    /// <summary>Datentyp der Property als Text (leer, wenn keine Definition gefunden wurde).</summary>
    public string DataType { get; init; } = string.Empty;

    /// <summary>True, wenn die Property laut Typdefinition ein Pflichtfeld ist.</summary>
    public bool IsRequired { get; init; }

    /// <summary>String-Darstellung des aktuellen Werts.</summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>
    /// ID des Typs, der diese Property-Definition liefert — der primäre Objekttyp oder
    /// einer der zugewiesenen Secondary Types (Aspekte). Leer, wenn keine Definition
    /// gefunden wurde.
    /// </summary>
    public string OwningTypeId { get; init; } = string.Empty;
}
