using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CMISPilot.Cmis.Contracts;
using CMISPilot.Cmis.Errors;
using CMISPilot.Cmis.Models;
using CMISPilot.ViewModels.Export;
using CMISPilot.ViewModels.Messages;
using CMISPilot.ViewModels.Shell;
using CMISPilot.ViewModels.Query;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CMISPilot.Tests.ViewModels;

/// <summary>
/// VM-Unit-Tests für den Abfrage-Dokument-Tab (R5.1). Gemäß Politik M11 ohne
/// Server: gemockter <see cref="IQueryService"/> + gemockter <see cref="ISessionContext"/>
/// + echter Messenger. Prüft ContentId/ContextTabKey, Ausführen→Ergebnis, Fehlerpfad,
/// CanExecute und Verhalten bei Trennung (Referenzmuster M6/R4).
/// </summary>
public sealed class QueryDocumentViewModelTests
{
    private readonly IQueryService _queryService = Substitute.For<IQueryService>();
    private readonly ISessionContext _session = Substitute.For<ISessionContext>();
    private readonly IMessenger _messenger = new WeakReferenceMessenger();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly IListExporter _listExporter = Substitute.For<IListExporter>();

    private QueryDocumentViewModel CreateSut(bool connected = true)
    {
        _session.IsConnected.Returns(connected);
        return new QueryDocumentViewModel(
            _queryService, _session, _messenger, NullLogger<QueryDocumentViewModel>.Instance,
            _dialogs, _listExporter);
    }

    private static QueryResultDto SampleResult() => new()
    {
        ColumnNames = new List<string> { "cmis:name", "cmis:objectId" },
        Rows = new List<QueryRowDto>
        {
            new()
            {
                ObjectId = "1",
                ValuesByColumn = new Dictionary<string, object?>
                {
                    ["cmis:name"] = "Dokument A",
                    ["cmis:objectId"] = "1"
                }
            },
            new()
            {
                ObjectId = "2",
                ValuesByColumn = new Dictionary<string, object?>
                {
                    ["cmis:name"] = "Dokument B",
                    ["cmis:objectId"] = "2"
                }
            }
        }
    };

    [Fact]
    public void ContentIdUndContextTabKey_SindFest()
    {
        var sut = CreateSut();

        Assert.Equal("query", sut.ContentId);
        Assert.Equal("query", sut.ContextTabKey);
        Assert.Equal("Abfrage", sut.Title);
    }

    [Fact]
    public async Task ExecuteQuery_fuellt_Ergebnis()
    {
        _queryService.QueryAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(SampleResult());

        var sut = CreateSut();
        sut.CmisqlText = "SELECT cmis:name, cmis:objectId FROM cmis:document";
        await sut.ExecuteQueryCommand.ExecuteAsync(null);

        Assert.True(sut.HasResult);
        Assert.Equal(2, sut.Rows.Count);
        Assert.Equal(2, sut.ColumnNames.Count);
        Assert.Contains("cmis:name", sut.ColumnNames);
    }

    [Fact]
    public async Task ExecuteQuery_bei_CmisFehler_bleibt_leer()
    {
        _queryService.QueryAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns<QueryResultDto>(_ => throw new CmisInvalidArgumentException("ungueltige CMISQL"));

        var sut = CreateSut();
        sut.CmisqlText = "SELECT invalid syntax";
        await sut.ExecuteQueryCommand.ExecuteAsync(null);

        Assert.Empty(sut.Rows);
        Assert.False(sut.HasResult);
    }

    [Fact]
    public void ExecuteQueryCommand_ohne_Verbindung_nicht_ausfuehrbar()
    {
        var sut = CreateSut(connected: false);
        sut.CmisqlText = "SELECT * FROM cmis:document";

        Assert.False(sut.ExecuteQueryCommand.CanExecute(null));
    }

    [Fact]
    public void ExecuteQueryCommand_bei_leerer_Query_nicht_ausfuehrbar()
    {
        var sut = CreateSut();
        sut.CmisqlText = "   ";

        Assert.False(sut.ExecuteQueryCommand.CanExecute(null));
    }

    [Fact]
    public async Task Trennen_leert_Ergebnis()
    {
        _queryService.QueryAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(SampleResult());

        var sut = CreateSut();
        sut.CmisqlText = "SELECT * FROM cmis:document";
        await sut.ExecuteQueryCommand.ExecuteAsync(null);
        Assert.True(sut.HasResult);

        _session.IsConnected.Returns(false);
        _messenger.Send(new ConnectionStateChangedMessage());

        Assert.Empty(sut.Rows);
        Assert.False(sut.HasResult);
        Assert.Empty(sut.ColumnNames);
        Assert.False(sut.IsConnected);
    }

    // --- Excel-Export des Ergebnisses (F3) ---

    [Fact]
    public void ExportResultCommand_ohne_Ergebnis_nicht_ausfuehrbar()
    {
        var sut = CreateSut();

        Assert.False(sut.ExportResultCommand.CanExecute(null));
    }

    [Fact]
    public async Task ExportResult_schreibt_Spalten_und_Zeilen_an_den_gewaehlten_Pfad()
    {
        _queryService.QueryAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(SampleResult());
        _dialogs.PickSaveFileAsync(Arg.Any<string>()).Returns(@"C:\temp\ergebnis.xlsx");

        var sut = CreateSut();
        sut.CmisqlText = "SELECT cmis:name, cmis:objectId FROM cmis:document";
        await sut.ExecuteQueryCommand.ExecuteAsync(null);

        Assert.True(sut.ExportResultCommand.CanExecute(null));
        await sut.ExportResultCommand.ExecuteAsync(null);

        await _listExporter.Received(1).ExportQueryResultAsync(
            Arg.Is<IReadOnlyList<string>>(c => c.Count == 2),
            Arg.Is<IReadOnlyList<QueryRowDto>>(r => r.Count == 2),
            sut.CmisqlText,
            @"C:\temp\ergebnis.xlsx",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExportResult_ergaenzt_fehlende_Dateiendung()
    {
        _queryService.QueryAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(SampleResult());
        _dialogs.PickSaveFileAsync(Arg.Any<string>()).Returns(@"C:\temp\ergebnis");

        var sut = CreateSut();
        sut.CmisqlText = "SELECT * FROM cmis:document";
        await sut.ExecuteQueryCommand.ExecuteAsync(null);
        await sut.ExportResultCommand.ExecuteAsync(null);

        await _listExporter.Received(1).ExportQueryResultAsync(
            Arg.Any<IReadOnlyList<string>>(), Arg.Any<IReadOnlyList<QueryRowDto>>(), Arg.Any<string?>(),
            @"C:\temp\ergebnis.xlsx", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExportResult_bei_abgebrochenem_Dialog_exportiert_nicht()
    {
        _queryService.QueryAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(SampleResult());
        _dialogs.PickSaveFileAsync(Arg.Any<string>()).Returns((string?)null);

        var sut = CreateSut();
        sut.CmisqlText = "SELECT * FROM cmis:document";
        await sut.ExecuteQueryCommand.ExecuteAsync(null);
        await sut.ExportResultCommand.ExecuteAsync(null);

        await _listExporter.DidNotReceiveWithAnyArgs().ExportQueryResultAsync(
            default!, default!, default, default!, default);
    }
}
