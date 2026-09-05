using System;
using System.Collections.Generic;

namespace CMISPilot.Cmis.Models;

/// <summary>
/// Ergebnis einer CMISQL-Abfrage (FA-50/51). Spaltennamen entsprechen den
/// Query-Namen der SELECT-Liste; jede Zeile liefert die Werte als Properties.
/// </summary>
public sealed class QueryResultDto
{
    /// <summary>Spaltennamen (Query-Namen) in Reihenfolge der SELECT-Liste.</summary>
    public IReadOnlyList<string> ColumnNames { get; init; } = Array.Empty<string>();

    /// <summary>Ergebniszeilen.</summary>
    public IReadOnlyList<QueryRowDto> Rows { get; init; } = Array.Empty<QueryRowDto>();
}

/// <summary>
/// Eine Ergebniszeile einer CMISQL-Abfrage.
/// </summary>
public sealed class QueryRowDto
{
    /// <summary>Objekt-ID der Zeile (cmis:objectId), falls in der Auswahl enthalten.</summary>
    public string? ObjectId { get; init; }

    /// <summary>Properties der Zeile in Reihenfolge der Spalten.</summary>
    public IReadOnlyList<PropertyDto> Properties { get; init; } = Array.Empty<PropertyDto>();

    /// <summary>Werte je Query-Name (Spaltenname) für schnellen Zugriff.</summary>
    public IReadOnlyDictionary<string, object?> ValuesByColumn { get; init; }
        = new Dictionary<string, object?>();
}
