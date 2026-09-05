using System;

namespace CMISPilot.Cmis.Diagnostics;

/// <summary>Ergebnis eines protokollierten Vorgangs (T9.1, FA-80).</summary>
public enum DiagnosticsResult
{
    Success,
    Cancelled,
    Failed
}

/// <summary>
/// Ein einzelner Eintrag im Diagnose-Protokoll: Zeitstempel, Kategorie
/// (z. B. "Executor" für Operations-Level, "HTTP" für Roh-Requests des
/// Browser Bindings), konkrete Operation, Dauer und Ergebnis/Fehler.
/// </summary>
public sealed record DiagnosticsLogEntry(
    DateTimeOffset Timestamp,
    string Category,
    string Operation,
    TimeSpan Duration,
    DiagnosticsResult Result,
    string? Detail = null,
    string? ErrorMessage = null)
{
    public static DiagnosticsLogEntry Success(string category, string operation, TimeSpan duration, string? detail = null) =>
        new(DateTimeOffset.Now, category, operation, duration, DiagnosticsResult.Success, detail);

    public static DiagnosticsLogEntry Cancelled(string category, string operation, TimeSpan duration) =>
        new(DateTimeOffset.Now, category, operation, duration, DiagnosticsResult.Cancelled);

    public static DiagnosticsLogEntry Failed(string category, string operation, TimeSpan duration, string errorMessage, string? detail = null) =>
        new(DateTimeOffset.Now, category, operation, duration, DiagnosticsResult.Failed, detail, errorMessage);
}
