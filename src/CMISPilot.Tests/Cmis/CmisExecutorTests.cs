using System;
using System.Threading;
using System.Threading.Tasks;
using CMISPilot.Cmis.Diagnostics;
using CMISPilot.Cmis.Errors;
using CMISPilot.Cmis.Execution;
using P = PortCMIS.Exceptions;

namespace CMISPilot.Tests.Cmis;

public class CmisExecutorTests
{
    private readonly ICmisExecutor _sut = new CmisExecutor();

    [Fact]
    public async Task RunAsync_Func_ReturnsResult()
    {
        var result = await _sut.RunAsync(() => 21 * 2);
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task RunAsync_Action_Runs()
    {
        var ran = false;
        await _sut.RunAsync(() => ran = true);
        Assert.True(ran);
    }

    [Fact]
    public async Task RunAsync_MapsPortCmisException()
    {
        await Assert.ThrowsAsync<CmisNotFoundException>(() =>
            _sut.RunAsync<int>(() => throw new P.CmisObjectNotFoundException("x")));
    }

    [Fact]
    public async Task RunAsync_AlreadyCanceledToken_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _sut.RunAsync(() => 1, cts.Token));
    }

    [Fact]
    public async Task RunAsync_NullFunc_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _sut.RunAsync<int>(null!));
    }

    [Fact]
    public async Task RunAsync_Erfolg_ProtokolliertEintragMitOperationsname()
    {
        var log = new InMemoryDiagnosticsLog();
        var sut = new CmisExecutor(log);

        await sut.RunAsync(() => 1, operationName: "MeineOperation");

        var entry = Assert.Single(log.GetEntries());
        Assert.Equal("Executor", entry.Category);
        Assert.Equal("MeineOperation", entry.Operation);
        Assert.Equal(DiagnosticsResult.Success, entry.Result);
    }

    [Fact]
    public async Task RunAsync_Fehler_ProtokolliertFehlschlagMitException()
    {
        var log = new InMemoryDiagnosticsLog();
        var sut = new CmisExecutor(log);

        await Assert.ThrowsAsync<CmisNotFoundException>(() =>
            sut.RunAsync<int>(() => throw new P.CmisObjectNotFoundException("x"), operationName: "Lesen"));

        var entry = Assert.Single(log.GetEntries());
        Assert.Equal("Executor", entry.Category);
        Assert.Equal("Lesen", entry.Operation);
        Assert.Equal(DiagnosticsResult.Failed, entry.Result);
        Assert.NotNull(entry.ErrorMessage);
    }

    [Fact]
    public async Task RunAsync_OhneLog_BleibtNoOp()
    {
        // Minimal-invasiv: new CmisExecutor() (Standardkonstruktor) darf weiterhin
        // funktionieren, ohne dass irgendwo ein Log existiert.
        var result = await _sut.RunAsync(() => 42);
        Assert.Equal(42, result);
    }
}
