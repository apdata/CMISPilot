using IFileLauncher = APX.Wpf.Shell.ViewModels.Contracts.IFileLauncher;
﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CMISPilot.Cmis.Contracts;
using CMISPilot.Cmis.Models;
using CMISPilot.ViewModels.Explorer;
using CMISPilot.ViewModels.Export;
using CMISPilot.ViewModels.Messages;
using CMISPilot.ViewModels.Shell;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CMISPilot.Tests.ViewModels;

/// <summary>
/// VM-Unit-Tests für den Explorer-Dokument-Tab (R4 Etappe 3/4): lädt Objektliste +
/// Breadcrumb des übergebenen Ordners und meldet die Listen-Selektion als
/// <see cref="NodeSelectedMessage"/> (gemockter <see cref="IBrowseService"/>,
/// Politik M11: keine Server-Tests). Die CRUD-Kommandos werden separat in
/// <see cref="ExplorerDocumentViewModelCrudTests"/> getestet.
/// </summary>
public sealed class ExplorerDocumentViewModelTests
{
    private readonly IBrowseService _browse = Substitute.For<IBrowseService>();
    private readonly IObjectService _objects = Substitute.For<IObjectService>();
    private readonly ITypeService _types = Substitute.For<ITypeService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly IFileLauncher _launcher = Substitute.For<IFileLauncher>();
    private readonly IMessenger _messenger = new WeakReferenceMessenger();
    private readonly IListExporter _listExporter = Substitute.For<IListExporter>();

    private static CmisObjectDto Folder(string id, string? name = null) => new()
    {
        Id = id,
        Name = name ?? id,
        BaseType = CmisBaseType.Folder,
        TypeId = "cmis:folder"
    };

    private static CmisObjectDto Doc(string id, string? name = null) => new()
    {
        Id = id,
        Name = name ?? id,
        BaseType = CmisBaseType.Document,
        TypeId = "cmis:document"
    };

    public ExplorerDocumentViewModelTests()
    {
        _browse.GetParentsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<CmisObjectDto>());
    }

    private ExplorerDocumentViewModel CreateSut(CmisObjectDto folder) => new(
        folder, _browse, _objects, _types, _dialogs, _launcher, _messenger,
        NullLogger<ExplorerDocumentViewModel>.Instance, _listExporter);

    [Fact]
    public void ContentIdIstKonstant_TitelLeitetSichVomOrdnerAb()
    {
        var folder = Folder("f1", "Mein Ordner");
        _browse.GetChildrenAsync("f1", Arg.Any<CancellationToken>()).Returns(new List<CmisObjectDto>());

        var sut = CreateSut(folder);

        // Genau ein Explorer-Tab (feste ContentId), der beim Klick in-place navigiert.
        Assert.Equal("explorer", sut.ContentId);
        Assert.Equal("Mein Ordner", sut.Title);
        Assert.Equal("explorer", sut.ContextTabKey);
    }

    [Fact]
    public void NavigateTo_WechseltOrdnerTitelUndListeInPlace()
    {
        var start = Folder("f1", "Start");
        var ziel = Folder("f2", "Ziel");
        _browse.GetChildrenAsync("f1", Arg.Any<CancellationToken>()).Returns(new List<CmisObjectDto>());
        _browse.GetChildrenAsync("f2", Arg.Any<CancellationToken>())
            .Returns(new List<CmisObjectDto> { Doc("d1", "Datei") });
        _browse.GetParentsAsync("f2", Arg.Any<CancellationToken>()).Returns(new List<CmisObjectDto>());

        var sut = CreateSut(start);
        sut.NavigateTo(ziel);

        Assert.Same(ziel, sut.Folder);
        Assert.Equal("Ziel", sut.Title);
        Assert.Equal("explorer", sut.ContentId);
        Assert.Single(sut.Objects);
        Assert.Equal("Datei", sut.Objects[0].Name);
    }

    [Fact]
    public void Erzeugung_LaedtObjektlisteUndBreadcrumb()
    {
        var folder = Folder("f1", "Ordner");
        var parent = Folder("root", "Root");
        _browse.GetChildrenAsync("f1", Arg.Any<CancellationToken>())
            .Returns(new List<CmisObjectDto> { Folder("sub", "Unterordner"), Doc("d1", "Datei") });
        _browse.GetParentsAsync("f1", Arg.Any<CancellationToken>())
            .Returns(new List<CmisObjectDto> { parent });

        var sut = CreateSut(folder);

        Assert.Equal(2, sut.Objects.Count);
        Assert.Contains(sut.Objects, o => o.Id == "sub");
        Assert.Contains(sut.Objects, o => o.Id == "d1");

        Assert.Equal(2, sut.PathSegments.Count);
        Assert.Equal("root", sut.PathSegments[0].Id);
        Assert.Equal("f1", sut.PathSegments[1].Id);
    }

    [Fact]
    public void Selektion_SendetNodeSelectedMessageMitAusgewaehltemObjekt()
    {
        var folder = Folder("f1", "Ordner");
        var child = Doc("d1", "Datei");
        _browse.GetChildrenAsync("f1", Arg.Any<CancellationToken>())
            .Returns(new List<CmisObjectDto> { child });

        var sut = CreateSut(folder);

        CmisObjectDto? received = null;
        var gotMessage = false;
        _messenger.Register<NodeSelectedMessage>(this, (_, m) =>
        {
            gotMessage = true;
            received = m.CmisObject;
        });

        sut.SelectedObject = child;

        Assert.True(gotMessage);
        Assert.Same(child, received);
    }

    // --- Excel-Export der Objektliste (F3) ---

    [Fact]
    public void ExportListCommand_bei_leerem_Ordner_nicht_ausfuehrbar()
    {
        _browse.GetChildrenAsync("f1", Arg.Any<CancellationToken>()).Returns(new List<CmisObjectDto>());

        var sut = CreateSut(Folder("f1", "Leer"));

        Assert.False(sut.ExportListCommand.CanExecute(null));
    }

    [Fact]
    public async Task ExportList_schreibt_Ordner_und_Objektliste_an_den_gewaehlten_Pfad()
    {
        var folder = Folder("f1", "Rechnungen");
        _browse.GetChildrenAsync("f1", Arg.Any<CancellationToken>())
            .Returns(new List<CmisObjectDto> { Folder("sub", "Unterordner"), Doc("d1", "Datei") });
        _dialogs.PickSaveFileAsync(Arg.Any<string>()).Returns(@"C:\temp\liste.xlsx");

        var sut = CreateSut(folder);

        Assert.True(sut.ExportListCommand.CanExecute(null));
        await sut.ExportListCommand.ExecuteAsync(null);

        // Der Dateiname wird mit dem Ordnernamen vorbelegt.
        await _dialogs.Received(1).PickSaveFileAsync("Rechnungen.xlsx");
        await _listExporter.Received(1).ExportObjectListAsync(
            folder,
            Arg.Is<IReadOnlyList<CmisObjectDto>>(o => o.Count == 2),
            @"C:\temp\liste.xlsx",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExportList_ergaenzt_fehlende_Dateiendung()
    {
        _browse.GetChildrenAsync("f1", Arg.Any<CancellationToken>())
            .Returns(new List<CmisObjectDto> { Doc("d1", "Datei") });
        _dialogs.PickSaveFileAsync(Arg.Any<string>()).Returns(@"C:\temp\liste");

        var sut = CreateSut(Folder("f1", "Ordner"));
        await sut.ExportListCommand.ExecuteAsync(null);

        await _listExporter.Received(1).ExportObjectListAsync(
            Arg.Any<CmisObjectDto>(), Arg.Any<IReadOnlyList<CmisObjectDto>>(),
            @"C:\temp\liste.xlsx", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExportList_bei_abgebrochenem_Dialog_exportiert_nicht()
    {
        _browse.GetChildrenAsync("f1", Arg.Any<CancellationToken>())
            .Returns(new List<CmisObjectDto> { Doc("d1", "Datei") });
        _dialogs.PickSaveFileAsync(Arg.Any<string>()).Returns((string?)null);

        var sut = CreateSut(Folder("f1", "Ordner"));
        await sut.ExportListCommand.ExecuteAsync(null);

        await _listExporter.DidNotReceiveWithAnyArgs().ExportObjectListAsync(
            default!, default!, default!, default);
    }
}
