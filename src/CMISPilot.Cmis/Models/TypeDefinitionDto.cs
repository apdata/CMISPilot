using System;
using System.Collections.Generic;

namespace CMISPilot.Cmis.Models;

/// <summary>
/// UI-freundliche CMIS-Typdefinition (FA-60/61). Eigene Abbildung von PortCMIS
/// <c>ITypeDefinition</c>/<c>IObjectType</c>. Über <see cref="Children"/> lässt
/// sich der Typbaum (Basistypen + abgeleitete Typen) abbilden.
/// </summary>
public sealed class TypeDefinitionDto
{
    public string Id { get; init; } = string.Empty;
    public string? LocalName { get; init; }
    public string? LocalNamespace { get; init; }
    public string? DisplayName { get; init; }
    public string? QueryName { get; init; }
    public string? Description { get; init; }

    public CmisBaseType BaseType { get; init; } = CmisBaseType.Unknown;
    public string? ParentTypeId { get; init; }

    public bool? IsCreatable { get; init; }
    public bool? IsFileable { get; init; }
    public bool? IsQueryable { get; init; }
    public bool? IsFulltextIndexed { get; init; }
    public bool? IsIncludedInSupertypeQuery { get; init; }
    public bool? IsControllablePolicy { get; init; }
    public bool? IsControllableAcl { get; init; }

    /// <summary>Property-Definitionen des Typs. Nie null, ggf. leer.</summary>
    public IReadOnlyList<PropertyDefinitionDto> PropertyDefinitions { get; init; }
        = Array.Empty<PropertyDefinitionDto>();

    /// <summary>Abgeleitete Typen (für Baumdarstellung). Nie null, ggf. leer.</summary>
    public IReadOnlyList<TypeDefinitionDto> Children { get; init; }
        = Array.Empty<TypeDefinitionDto>();
}
