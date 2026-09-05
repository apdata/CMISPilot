namespace CMISPilot.ViewModels.ObjectDetails;

/// <summary>
/// Eine Zeile der erweiterten Property-Ansicht (R6.1, <see cref="ExtendedPropertiesViewModel"/>).
/// Anders als das schlanke Eigenschaften-Werkzeugfenster (<see cref="Explorer.PropertiesViewModel"/>)
/// zeigt diese Zeile zusätzlich Query-Name/Local-Name und ob die Property mehrwertig ist.
/// </summary>
public sealed class ExtendedPropertyRowViewModel
{
    public string DisplayName { get; init; } = string.Empty;
    public string PropertyId { get; init; } = string.Empty;
    public string LocalName { get; init; } = string.Empty;
    public string QueryName { get; init; } = string.Empty;
    public string DataType { get; init; } = string.Empty;
    public bool IsMultiValued { get; init; }
    public bool IsRequired { get; init; }

    /// <summary>Wert (bei Mehrfachwerten mit "; " zusammengefügt).</summary>
    public string Value { get; init; } = string.Empty;
}
