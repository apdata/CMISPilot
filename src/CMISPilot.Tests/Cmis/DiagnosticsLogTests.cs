using System;
using System.Threading.Tasks;
using CMISPilot.Cmis.Diagnostics;

namespace CMISPilot.Tests.Cmis;

/// <summary>
/// Unit-Tests für <see cref="InMemoryDiagnosticsLog"/> (T9.1): Ringpuffer-Grenze
/// und Thread-Sicherheit-Grundfall. Politik M11: keine Server-/Integrationstests.
/// </summary>
public class DiagnosticsLogTests
{
    private static DiagnosticsLogEntry Entry(string op) =>
        DiagnosticsLogEntry.Success("Test", op, TimeSpan.FromMilliseconds(1));

    [Fact]
    public void Record_ReturnsEntry_InGetEntries()
    {
        IDiagnosticsLog log = new InMemoryDiagnosticsLog();

        log.Record(Entry("A"));

        var entries = log.GetEntries();
        Assert.Single(entries);
        Assert.Equal("A", entries[0].Operation);
    }

    [Fact]
    public void Record_UeberKapazitaet_VerwirftAelteste()
    {
        IDiagnosticsLog log = new InMemoryDiagnosticsLog(capacity: 3);

        for (var i = 0; i < 5; i++)
        {
            log.Record(Entry($"op{i}"));
        }

        var entries = log.GetEntries();
        Assert.Equal(3, entries.Count);
        // aelteste (op0, op1) sind herausgefallen; Reihenfolge bleibt chronologisch.
        Assert.Equal("op2", entries[0].Operation);
        Assert.Equal("op3", entries[1].Operation);
        Assert.Equal("op4", entries[2].Operation);
    }

    [Fact]
    public void Clear_LeertDasProtokoll()
    {
        IDiagnosticsLog log = new InMemoryDiagnosticsLog();
        log.Record(Entry("A"));

        log.Clear();

        Assert.Empty(log.GetEntries());
    }

    [Fact]
    public void Constructor_UngueltigeKapazitaet_Wirft()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new InMemoryDiagnosticsLog(0));
    }

    [Fact]
    public async Task Record_IstThreadSicher_KeinVerlustUnterParallelitaet()
    {
        IDiagnosticsLog log = new InMemoryDiagnosticsLog(capacity: 1000);

        var tasks = new Task[20];
        for (var t = 0; t < tasks.Length; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                for (var i = 0; i < 50; i++)
                {
                    log.Record(Entry("parallel"));
                }
            });
        }

        await Task.WhenAll(tasks);

        Assert.Equal(1000, log.GetEntries().Count);
    }

    [Fact]
    public void NullDiagnosticsLog_IstNoOp()
    {
        IDiagnosticsLog log = NullDiagnosticsLog.Instance;

        log.Record(Entry("A"));

        Assert.Empty(log.GetEntries());
    }
}
