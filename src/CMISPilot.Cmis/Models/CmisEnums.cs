namespace CMISPilot.Cmis.Models;

/// <summary>
/// CMIS-Basistyp eines Objekts bzw. einer Typdefinition. Eigene Abbildung von
/// PortCMIS <c>BaseTypeId</c>, damit keine PortCMIS-Typen nach außen lecken (NFA-03a).
/// </summary>
public enum CmisBaseType
{
    Document,
    Folder,
    Relationship,
    Policy,
    Item,
    Secondary,
    Unknown
}

/// <summary>
/// Datentyp einer CMIS-Property. Eigene Abbildung von PortCMIS <c>PropertyType</c>.
/// </summary>
public enum CmisPropertyType
{
    Boolean,
    Id,
    Integer,
    DateTime,
    Decimal,
    Html,
    String,
    Uri
}

/// <summary>
/// Kardinalität einer Property-Definition (einwertig / mehrwertig).
/// </summary>
public enum CmisCardinality
{
    Single,
    Multi
}

/// <summary>
/// Änderbarkeit einer Property-Definition.
/// </summary>
public enum CmisUpdatability
{
    ReadOnly,
    ReadWrite,
    WhenCheckedOut,
    OnCreate
}

/// <summary>
/// Fachliche Kategorie eines Fehlers für saubere UI-Meldungen (siehe Errors/CmisAppException).
/// </summary>
public enum CmisErrorKind
{
    Authentication,
    Network,
    NotFound,
    Constraint,
    PermissionDenied,
    NotSupported,
    InvalidArgument,
    Conflict,
    Server,
    Canceled,
    Unknown
}
