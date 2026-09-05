using System.Collections.Generic;

namespace CMISPilot.Cmis.Diagnostics;

/// <summary>
/// No-op-Implementierung von <see cref="IDiagnosticsLog"/>. Default für
/// <see cref="Execution.CmisExecutor"/>, wenn kein Log injiziert wird (z. B.
/// bestehende <c>new CmisExecutor()</c>-Aufrufe in Tests, minimal-invasiv
/// gemäß Umsetzungsplan §4).
/// </summary>
public sealed class NullDiagnosticsLog : IDiagnosticsLog
{
    public static readonly NullDiagnosticsLog Instance = new();

    private NullDiagnosticsLog() { }

    public int Capacity => 0;

    public void Record(DiagnosticsLogEntry entry) { }

    public IReadOnlyList<DiagnosticsLogEntry> GetEntries() => System.Array.Empty<DiagnosticsLogEntry>();

    public void Clear() { }
}
