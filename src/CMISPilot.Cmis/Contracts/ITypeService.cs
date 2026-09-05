using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CMISPilot.Cmis.Models;

namespace CMISPilot.Cmis.Contracts;

/// <summary>
/// Lädt Typdefinitionen des Repositories (FA-60/61/62). Setzt eine aktive Session voraus.
/// </summary>
public interface ITypeService
{
    /// <summary>
    /// Lädt den gesamten Typbaum ab den Basistypen als verschachtelte
    /// <see cref="TypeDefinitionDto"/> (über <c>Children</c>). Basis für die
    /// Baumdarstellung im Typen-Bereich (FA-60).
    /// </summary>
    Task<IReadOnlyList<TypeDefinitionDto>> GetTypeTreeAsync(
        bool includePropertyDefinitions = true, CancellationToken ct = default);

    /// <summary>
    /// Lädt die direkten Untertypen eines Typs (oder die Basistypen, wenn
    /// <paramref name="typeId"/> null ist).
    /// </summary>
    Task<IReadOnlyList<TypeDefinitionDto>> GetTypeChildrenAsync(
        string? typeId, bool includePropertyDefinitions = true, CancellationToken ct = default);

    /// <summary>Lädt eine einzelne Typdefinition inkl. Property-Definitionen (FA-61/62).</summary>
    Task<TypeDefinitionDto> GetTypeDefinitionAsync(string typeId, CancellationToken ct = default);
}
