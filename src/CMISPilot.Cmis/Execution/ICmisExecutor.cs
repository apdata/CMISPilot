using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CMISPilot.Cmis.Execution;

/// <summary>
/// Kapselt blockierende PortCMIS-Aufrufe und bietet nach außen ausschließlich
/// asynchrone Ausführung an (NFA-05/NFA-13). Synchrone Aufrufe laufen auf einem
/// Hintergrund-Thread (<c>Task.Run</c>), damit der UI-Thread nie blockiert.
/// Auftretende PortCMIS-Exceptions werden zentral auf <c>CmisAppException</c>
/// abgebildet.
/// </summary>
public interface ICmisExecutor
{
    /// <summary>
    /// Führt eine blockierende Aktion asynchron aus. <paramref name="operationName"/>
    /// wird für das Diagnose-Protokoll (T9.1) automatisch vom Aufrufer übernommen
    /// (<see cref="CallerMemberNameAttribute"/>) – Aufrufstellen müssen ihn nicht
    /// angeben.
    /// </summary>
    Task RunAsync(Action action, CancellationToken ct = default,
        [CallerMemberName] string operationName = "");

    /// <summary>Führt eine blockierende Funktion asynchron aus und liefert das Ergebnis.</summary>
    Task<T> RunAsync<T>(Func<T> func, CancellationToken ct = default,
        [CallerMemberName] string operationName = "");
}
