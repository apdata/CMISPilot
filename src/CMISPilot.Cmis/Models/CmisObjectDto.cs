using System;
using System.Collections.Generic;

namespace CMISPilot.Cmis.Models;

/// <summary>
/// UI-freundliche Repräsentation eines CMIS-Objekts (Ordner oder Dokument).
/// Eigene Abbildung von PortCMIS <c>ICmisObject</c> (FA-20/22/30).
/// </summary>
public sealed class CmisObjectDto
{
    public string Id { get; init; } = string.Empty;
    public string? Name { get; init; }

    public CmisBaseType BaseType { get; init; } = CmisBaseType.Unknown;

    /// <summary>Konkrete Typ-ID des Objekts, z. B. "cmis:folder".</summary>
    public string? TypeId { get; init; }

    public bool IsFolder => BaseType == CmisBaseType.Folder;
    public bool IsDocument => BaseType == CmisBaseType.Document;

    public string? CreatedBy { get; init; }
    public DateTimeOffset? CreationDate { get; init; }
    public string? LastModifiedBy { get; init; }
    public DateTimeOffset? LastModificationDate { get; init; }

    // Dokument-spezifisch (null bei Ordnern):
    public long? ContentStreamLength { get; init; }
    public string? ContentStreamMimeType { get; init; }
    public string? ContentStreamFileName { get; init; }

    /// <summary>Alle Properties des Objekts. Nie null, ggf. leer.</summary>
    public IReadOnlyList<PropertyDto> Properties { get; init; } = Array.Empty<PropertyDto>();

    /// <summary>
    /// IDs der dem Objekt zugewiesenen Secondary Types (Aspekte, <c>cmis:secondaryObjectTypeIds</c>).
    /// Nie null, ggf. leer.
    /// </summary>
    public IReadOnlyList<string> SecondaryTypeIds { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Erlaubte Aktionen (Allowable Actions) als String-IDs, sofern geladen (FA-75).
    /// Null, wenn nicht abgefragt.
    /// </summary>
    public IReadOnlyList<string>? AllowableActions { get; init; }
}
