using APX.Wpf.Shell.ViewModels.Workspace;
using System;
using CMISPilot.Cmis.Diagnostics;
using CMISPilot.ViewModels.Diagnostics;
using CMISPilot.ViewModels.Workspace;

namespace CMISPilot.Tests.ViewModels;

/// <summary>
/// VM-Unit-Tests des Diagnose-Werkzeugfensters (R5.3). Gemäß Politik M11 ohne
/// Server: echter <see cref="InMemoryDiagnosticsLog"/>, keine PortCMIS-Abhängigkeit.
/// Verhalten aus <c>DiagnosticsAreaViewModel</c> (entfernt) (M9) übernommen,
/// jetzt als <see cref="ToolViewModelBase"/> (Bottom-Dock, ersetzt R1-Platzhalter
/// „Konsole").
/// </summary>
public sealed class DiagnosticsToolViewModelTests
{
    private static DiagnosticsLogEntry Entry(string op) =>
        DiagnosticsLogEntry.Success("Executor", op, TimeSpan.FromMilliseconds(5));

    [Fact]
    public void Konstruktor_SetztContentIdUndDock()
    {
        var log = new InMemoryDiagnosticsLog();

        var sut = new DiagnosticsToolViewModel(log);

        Assert.Equal("tool:diagnostics", sut.ContentId);
        Assert.Equal(ToolDock.Bottom, sut.Dock);
        Assert.Equal("Diagnose", sut.Title);
    }

    [Fact]
    public void Konstruktor_LaedtVorhandeneEintraege()
    {
        var log = new InMemoryDiagnosticsLog();
        log.Record(Entry("A"));
        log.Record(Entry("B"));

        var sut = new DiagnosticsToolViewModel(log);

        Assert.Equal(2, sut.Entries.Count);
        Assert.True(sut.HasEntries);
        // neueste zuerst
        Assert.Equal("B", sut.Entries[0].Operation);
        Assert.Equal("A", sut.Entries[1].Operation);
    }

    [Fact]
    public void RefreshEntries_HoltNeueEintraegeNach()
    {
        var log = new InMemoryDiagnosticsLog();
        var sut = new DiagnosticsToolViewModel(log);
        Assert.False(sut.HasEntries);

        log.Record(Entry("C"));
        sut.RefreshEntriesCommand.Execute(null);

        Assert.True(sut.HasEntries);
        Assert.Single(sut.Entries);
        Assert.Equal("C", sut.Entries[0].Operation);
    }

    [Fact]
    public void ClearEntries_LeertLogUndAnzeige()
    {
        var log = new InMemoryDiagnosticsLog();
        log.Record(Entry("A"));
        var sut = new DiagnosticsToolViewModel(log);
        sut.SelectedEntry = sut.Entries[0];

        sut.ClearEntriesCommand.Execute(null);

        Assert.Empty(sut.Entries);
        Assert.False(sut.HasEntries);
        Assert.Null(sut.SelectedEntry);
        Assert.Empty(log.GetEntries());
    }

    [Fact]
    public void Selektion_SetztHasSelection()
    {
        var log = new InMemoryDiagnosticsLog();
        log.Record(Entry("A"));
        var sut = new DiagnosticsToolViewModel(log);

        Assert.False(sut.HasSelection);

        sut.SelectedEntry = sut.Entries[0];

        Assert.True(sut.HasSelection);
    }
}
