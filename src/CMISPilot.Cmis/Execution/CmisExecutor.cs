using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CMISPilot.Cmis.Diagnostics;
using CMISPilot.Cmis.Errors;

namespace CMISPilot.Cmis.Execution;

/// <summary>
/// Standard-Implementierung von <see cref="ICmisExecutor"/>. Führt blockierende
/// PortCMIS-Aufrufe via <see cref="Task.Run(Func{object},CancellationToken)"/> auf
/// dem ThreadPool aus und bildet Fehler zentral über
/// <see cref="CmisExceptionMapper"/> ab.
/// </summary>
/// <remarks>
/// PortCMIS-Aufrufe sind selbst nicht abbrechbar. Der <see cref="CancellationToken"/>
/// wird vor dem Start und über <c>Task.Run</c> berücksichtigt; ein bereits
/// gestarteter Serveraufruf läuft im Hintergrund zu Ende, während der Aufrufer den
/// Abbruch (OperationCanceledException) sofort beobachtet. Bewusste Design-
/// Entscheidung (siehe Umsetzungsplan §4, Risiko "Blockierendes PortCMIS").
/// </remarks>
/// <remarks>
/// T9.1/FA-80: protokolliert jede Serveroperation (Start/Ende/Dauer/Ergebnis)
/// im <see cref="IDiagnosticsLog"/> unter Kategorie "Executor". Der Operations-
/// Name kommt automatisch über <see cref="CallerMemberNameAttribute"/> vom
/// jeweiligen Cmis-Dienst (z. B. "ConnectAsync", "CreateDocumentAsync") – keine
/// Änderung an bestehenden Aufrufstellen nötig. Konstruktor bewusst minimal-
/// invasiv erweitert: ohne Argument bleibt <c>new CmisExecutor()</c> (Tests)
/// unverändert lauffähig, Logging ist dann ein No-op.
/// </remarks>
public sealed class CmisExecutor : ICmisExecutor
{
    private const string Category = "Executor";

    private readonly IDiagnosticsLog _log;

    public CmisExecutor() : this(NullDiagnosticsLog.Instance) { }

    public CmisExecutor(IDiagnosticsLog? diagnosticsLog) => _log = diagnosticsLog ?? NullDiagnosticsLog.Instance;

    public async Task RunAsync(Action action, CancellationToken ct = default,
        [CallerMemberName] string operationName = "")
    {
        ArgumentNullException.ThrowIfNull(action);
        ct.ThrowIfCancellationRequested();
        var sw = Stopwatch.StartNew();
        try
        {
            await Task.Run(action, ct).ConfigureAwait(false);
            sw.Stop();
            _log.Record(DiagnosticsLogEntry.Success(Category, operationName, sw.Elapsed));
        }
        catch (Exception ex)
        {
            sw.Stop();
            RecordFailure(operationName, sw.Elapsed, ex);
            throw CmisExceptionMapper.Map(ex);
        }
    }

    public async Task<T> RunAsync<T>(Func<T> func, CancellationToken ct = default,
        [CallerMemberName] string operationName = "")
    {
        ArgumentNullException.ThrowIfNull(func);
        ct.ThrowIfCancellationRequested();
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await Task.Run(func, ct).ConfigureAwait(false);
            sw.Stop();
            _log.Record(DiagnosticsLogEntry.Success(Category, operationName, sw.Elapsed));
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            RecordFailure(operationName, sw.Elapsed, ex);
            throw CmisExceptionMapper.Map(ex);
        }
    }

    private void RecordFailure(string operationName, TimeSpan elapsed, Exception ex)
    {
        var entry = ex is OperationCanceledException
            ? DiagnosticsLogEntry.Cancelled(Category, operationName, elapsed)
            : DiagnosticsLogEntry.Failed(Category, operationName, elapsed, ex.Message);
        _log.Record(entry);
    }
}
