using System.Collections.Generic;

namespace CMISPilot.Cmis.Diagnostics;

/// <summary>
/// Sammelt Diagnose-Einträge (Serveroperationen, ggf. Roh-HTTP-Requests des
/// Browser Bindings) in einem begrenzten Ringpuffer (T9.1, FA-80). Thread-sicher,
/// da <see cref="Execution.ICmisExecutor"/>-Aufrufe von mehreren Threads
/// gleichzeitig protokolliert werden können.
/// </summary>
public interface IDiagnosticsLog
{
    /// <summary>Maximale Anzahl gehaltener Einträge (älteste fallen beim Überlauf heraus).</summary>
    int Capacity { get; }

    /// <summary>Fügt einen Eintrag hinzu.</summary>
    void Record(DiagnosticsLogEntry entry);

    /// <summary>Momentaufnahme aller aktuell gehaltenen Einträge, älteste zuerst.</summary>
    IReadOnlyList<DiagnosticsLogEntry> GetEntries();

    /// <summary>Leert das Protokoll (T9.2 „Leeren"-Button).</summary>
    void Clear();
}
