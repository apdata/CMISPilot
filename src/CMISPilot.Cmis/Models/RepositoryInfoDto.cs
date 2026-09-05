using System.Collections.Generic;

namespace CMISPilot.Cmis.Models;

/// <summary>
/// UI-freundliche Repository-Information (FA-10). Eigene Abbildung von PortCMIS
/// <c>IRepositoryInfo</c>.
/// </summary>
public sealed class RepositoryInfoDto
{
    public string Id { get; init; } = string.Empty;
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? VendorName { get; init; }
    public string? ProductName { get; init; }
    public string? ProductVersion { get; init; }

    /// <summary>CMIS-Version als String, z. B. "1.1".</summary>
    public string? CmisVersion { get; init; }

    public string? RootFolderId { get; init; }
    public string? ThinClientUri { get; init; }
    public string? LatestChangeLogToken { get; init; }

    /// <summary>Repository-Capabilities (FA-11). Null, wenn vom Server nicht geliefert.</summary>
    public RepositoryCapabilitiesDto? Capabilities { get; init; }

    /// <summary>ACL-Capabilities (FA-11). Null, wenn vom Server nicht geliefert.</summary>
    public AclCapabilitiesDto? AclCapabilities { get; init; }

    /// <summary>Gibt an, ob das Änderungsprotokoll unvollständig ist.</summary>
    public bool? ChangesIncomplete { get; init; }

    /// <summary>Basistypen, für die der Server Änderungen meldet (als String, z. B. "cmis:document").</summary>
    public IReadOnlyList<string> ChangesOnType { get; init; } = [];

    /// <summary>Principal-ID für anonymen Zugriff, sofern der Server eine kennt.</summary>
    public string? PrincipalIdAnonymous { get; init; }

    /// <summary>Principal-ID für „jeder", sofern der Server eine kennt.</summary>
    public string? PrincipalIdAnyone { get; init; }

    /// <summary>Optionale Erweiterungen, die der Server meldet.</summary>
    public IReadOnlyList<ExtensionFeatureDto> ExtensionFeatures { get; init; } = [];

    /// <summary>
    /// Herstellerspezifische Erweiterungsdaten der Antwort. Genau hier unterscheiden
    /// sich die Server; CMIS gibt für diesen Bereich nichts vor.
    /// </summary>
    public IReadOnlyList<CmisExtensionElementDto> Extensions { get; init; } = [];
}

/// <summary>
/// UI-freundliche ACL-Capabilities (FA-11). Eigene Abbildung von PortCMIS
/// <c>IAclCapabilities</c>; Enums werden wie bei den übrigen Capabilities als
/// String übernommen, damit keine PortCMIS-Typen nach außen lecken.
/// </summary>
public sealed class AclCapabilitiesDto
{
    /// <summary>Unterstützter Berechtigungssatz, z. B. "Basic" oder "Both".</summary>
    public string? SupportedPermissions { get; init; }

    /// <summary>Art der ACL-Weitergabe, z. B. "Propagate" oder "ObjectOnly".</summary>
    public string? AclPropagation { get; init; }

    /// <summary>Die vom Repository definierten Berechtigungen.</summary>
    public IReadOnlyList<PermissionDefinitionDto> Permissions { get; init; } = [];

    /// <summary>
    /// Zuordnung von CMIS-Schlüsseln (z. B. <c>canGetProperties.Object</c>) auf die
    /// Berechtigungen, die dafür genügen.
    /// </summary>
    public IReadOnlyList<PermissionMappingDto> PermissionMapping { get; init; } = [];
}

/// <summary>Eine vom Repository definierte Berechtigung.</summary>
/// <param name="Id">Kennung der Berechtigung, z. B. <c>cmis:read</c>.</param>
/// <param name="Description">Beschreibender Text des Servers.</param>
public sealed record PermissionDefinitionDto(string Id, string? Description);

/// <summary>Eine Zeile der Berechtigungszuordnung.</summary>
/// <param name="Key">CMIS-Schlüssel der Operation.</param>
/// <param name="Permissions">Berechtigungen, die dafür genügen.</param>
public sealed record PermissionMappingDto(string Key, IReadOnlyList<string> Permissions);

/// <summary>Eine vom Server gemeldete optionale Erweiterung.</summary>
/// <param name="Id">Kennung der Erweiterung.</param>
/// <param name="CommonName">Sprechender Name, sofern vorhanden.</param>
/// <param name="VersionLabel">Versionsbezeichnung, sofern vorhanden.</param>
/// <param name="Url">Adresse mit weiteren Angaben, sofern vorhanden.</param>
/// <param name="Description">Beschreibender Text des Servers, sofern vorhanden.</param>
/// <param name="FeatureData">Zusätzliche Angaben der Erweiterung als Schlüssel/Wert-Paare.</param>
public sealed record ExtensionFeatureDto(
    string Id,
    string? CommonName,
    string? VersionLabel,
    string? Url,
    string? Description,
    IReadOnlyList<KeyValuePair<string, string>> FeatureData);

/// <summary>
/// UI-freundliche Repository-Capabilities (FA-11). Eigene Abbildung von PortCMIS
/// <c>IRepositoryCapabilities</c>; Enum-Fähigkeiten werden als String übernommen,
/// damit keine PortCMIS-Enums nach außen lecken.
/// </summary>
public sealed class RepositoryCapabilitiesDto
{
    public string? ContentStreamUpdates { get; init; }
    public string? Changes { get; init; }
    public string? Renditions { get; init; }
    public string? OrderBy { get; init; }
    public string? Query { get; init; }
    public string? Join { get; init; }
    public string? Acl { get; init; }

    public bool? GetDescendantsSupported { get; init; }
    public bool? GetFolderTreeSupported { get; init; }
    public bool? MultifilingSupported { get; init; }
    public bool? UnfilingSupported { get; init; }
    public bool? VersionSpecificFilingSupported { get; init; }
    public bool? PwcSearchableSupported { get; init; }
    public bool? PwcUpdatableSupported { get; init; }
    public bool? AllVersionsSearchableSupported { get; init; }

    /// <summary>
    /// Property-Typen, die sich in neuen Typdefinitionen anlegen lassen (CMIS 1.1,
    /// <c>capabilityCreatablePropertyTypes</c>). Leer, wenn der Server nichts meldet.
    /// </summary>
    public IReadOnlyList<string> CreatablePropertyTypes { get; init; } = [];

    /// <summary>
    /// Attribute, die beim Anlegen eines neuen Typs gesetzt werden dürfen (CMIS 1.1,
    /// <c>capabilityNewTypeSettableAttributes</c>). Null, wenn vom Server nicht geliefert.
    /// </summary>
    public NewTypeSettableAttributesDto? NewTypeSettableAttributes { get; init; }
}

/// <summary>
/// Welche Attribute einer neuen Typdefinition der Server setzen lässt (CMIS 1.1).
/// Alle Angaben optional: <c>null</c> heißt „der Server sagt dazu nichts".
/// </summary>
public sealed class NewTypeSettableAttributesDto
{
    public bool? Id { get; init; }
    public bool? LocalName { get; init; }
    public bool? LocalNamespace { get; init; }
    public bool? DisplayName { get; init; }
    public bool? QueryName { get; init; }
    public bool? Description { get; init; }
    public bool? Creatable { get; init; }
    public bool? Fileable { get; init; }
    public bool? Queryable { get; init; }
    public bool? FulltextIndexed { get; init; }
    public bool? IncludedInSupertypeQuery { get; init; }
    public bool? ControllablePolicy { get; init; }
    public bool? ControllableAcl { get; init; }
}

/// <summary>
/// Ein Element der herstellerspezifischen Erweiterungsdaten. Rekursiv: ein Element hat
/// entweder einen Wert oder Kinder.
/// </summary>
/// <param name="Name">Name des Elements.</param>
/// <param name="Namespace">Namensraum, sofern angegeben.</param>
/// <param name="Value">Wert des Elements; null, wenn es Kinder hat.</param>
/// <param name="Attributes">Attribute des Elements.</param>
/// <param name="Children">Untergeordnete Elemente.</param>
public sealed record CmisExtensionElementDto(
    string Name,
    string? Namespace,
    string? Value,
    IReadOnlyList<KeyValuePair<string, string>> Attributes,
    IReadOnlyList<CmisExtensionElementDto> Children);
