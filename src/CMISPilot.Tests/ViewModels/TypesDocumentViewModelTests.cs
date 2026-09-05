using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CMISPilot.Cmis.Contracts;
using CMISPilot.Cmis.Errors;
using CMISPilot.Cmis.Models;
using CMISPilot.ViewModels.Messages;
using CMISPilot.ViewModels.Types;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CMISPilot.Tests.ViewModels;

/// <summary>
/// VM-Unit-Tests für den Typen-Dokument-Tab (R5.2). Gemäß Politik M11 ohne Server:
/// gemockter <see cref="ITypeService"/> + gemockter <see cref="ISessionContext"/>
/// + echter Messenger. Prüft ContentId/ContextTabKey, Baumaufbau,
/// Selektion→Detailansicht und Verhalten bei Trennung (Referenzmuster M5/R4).
/// </summary>
public sealed class TypesDocumentViewModelTests
{
    private readonly ITypeService _types = Substitute.For<ITypeService>();
    private readonly ISessionContext _session = Substitute.For<ISessionContext>();
    private readonly IMessenger _messenger = new WeakReferenceMessenger();
    private readonly CMISPilot.ViewModels.Shell.IDialogService _dialogs =
        Substitute.For<CMISPilot.ViewModels.Shell.IDialogService>();
    private readonly CMISPilot.ViewModels.Export.ITypeDefinitionExporter _exporter =
        Substitute.For<CMISPilot.ViewModels.Export.ITypeDefinitionExporter>();

    private TypesDocumentViewModel CreateSut(bool connected = true)
    {
        _session.IsConnected.Returns(connected);
        return new TypesDocumentViewModel(
            _types, _session, _messenger, NullLogger<TypesDocumentViewModel>.Instance, _dialogs, _exporter);
    }

    private static TypeDefinitionDto Type(
        string id, CmisBaseType baseType, IReadOnlyList<TypeDefinitionDto>? children = null,
        IReadOnlyList<PropertyDefinitionDto>? props = null) => new()
    {
        Id = id,
        DisplayName = id,
        BaseType = baseType,
        Children = children ?? System.Array.Empty<TypeDefinitionDto>(),
        PropertyDefinitions = props ?? System.Array.Empty<PropertyDefinitionDto>()
    };

    private static IReadOnlyList<TypeDefinitionDto> SampleTree() => new List<TypeDefinitionDto>
    {
        Type("cmis:document", CmisBaseType.Document,
            children: new[] { Type("my:doc", CmisBaseType.Document) },
            props: new[]
            {
                new PropertyDefinitionDto { Id = "cmis:name", DisplayName = "Name",
                    PropertyType = CmisPropertyType.String, Cardinality = CmisCardinality.Single,
                    IsRequired = true, IsQueryable = true }
            }),
        Type("cmis:folder", CmisBaseType.Folder)
    };

    [Fact]
    public void ContentIdUndContextTabKey_SindFest()
    {
        var sut = CreateSut();

        Assert.Equal("types", sut.ContentId);
        Assert.Equal("types", sut.ContextTabKey);
        Assert.Equal("Typen", sut.Title);
    }

    [Fact]
    public void ExportBefehl_erst_ausfuehrbar_wenn_ein_Typ_gewaehlt_ist()
    {
        _types.GetTypeTreeAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(SampleTree());
        var sut = CreateSut();

        Assert.False(sut.ExportSelectedTypeCommand.CanExecute(null));

        sut.SelectedType = sut.RootTypes[0];
        Assert.True(sut.ExportSelectedTypeCommand.CanExecute(null));
    }

    [Fact]
    public async Task Export_ruft_den_Exporter_mit_gewaehltem_Typ_und_xlsx_Pfad()
    {
        _types.GetTypeTreeAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(SampleTree());
        var sut = CreateSut();
        sut.SelectedType = sut.RootTypes[0];

        // Der Dialog liefert einen Pfad ohne Endung; der Befehl ergaenzt .xlsx.
        _dialogs.PickSaveFileAsync(Arg.Any<string>()).Returns("C:\\tmp\\cmis_document");

        await sut.ExportSelectedTypeCommand.ExecuteAsync(null);

        await _exporter.Received(1).ExportAsync(
            sut.SelectedType!, "C:\\tmp\\cmis_document.xlsx", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Export_ohne_gewaehlten_Pfad_ruft_den_Exporter_nicht()
    {
        _types.GetTypeTreeAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(SampleTree());
        var sut = CreateSut();
        sut.SelectedType = sut.RootTypes[0];

        _dialogs.PickSaveFileAsync(Arg.Any<string>()).Returns((string?)null);

        await sut.ExportSelectedTypeCommand.ExecuteAsync(null);

        await _exporter.DidNotReceive().ExportAsync(
            Arg.Any<TypeDefinitionDto>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadTypes_baut_Baum_auf()
    {
        _types.GetTypeTreeAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(SampleTree());

        var sut = CreateSut();
        await sut.LoadTypesCommand.ExecuteAsync(null);

        Assert.Equal(2, sut.RootTypes.Count);
        Assert.True(sut.HasTypes);
        Assert.Single(sut.RootTypes[0].Children);
        Assert.Equal("my:doc", sut.RootTypes[0].Children[0].Id);
    }

    [Fact]
    public async Task Selektion_setzt_Detailansicht_mit_PropertyDefinitionen()
    {
        _types.GetTypeTreeAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(SampleTree());

        var sut = CreateSut();
        await sut.LoadTypesCommand.ExecuteAsync(null);

        Assert.False(sut.HasSelection);

        sut.SelectedType = sut.RootTypes[0];

        Assert.True(sut.HasSelection);
        Assert.Equal("cmis:document", sut.SelectedType?.Id);
        Assert.Single(sut.SelectedType!.PropertyDefinitions);
        Assert.Equal("cmis:name", sut.SelectedType.PropertyDefinitions[0].Id);
    }

    [Fact]
    public async Task LoadTypes_bei_CmisFehler_bleibt_leer()
    {
        _types.GetTypeTreeAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<TypeDefinitionDto>>(_ => throw new CmisNetworkException("down"));

        var sut = CreateSut();
        await sut.LoadTypesCommand.ExecuteAsync(null);

        Assert.Empty(sut.RootTypes);
        Assert.False(sut.HasTypes);
    }

    [Fact]
    public void LoadTypesCommand_ohne_Verbindung_nicht_ausfuehrbar()
    {
        var sut = CreateSut(connected: false);

        Assert.False(sut.LoadTypesCommand.CanExecute(null));
    }

    [Fact]
    public async Task Trennen_leert_Baum_und_Auswahl()
    {
        _types.GetTypeTreeAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(SampleTree());

        var sut = CreateSut();
        await sut.LoadTypesCommand.ExecuteAsync(null);
        sut.SelectedType = sut.RootTypes[0];

        _session.IsConnected.Returns(false);
        _messenger.Send(new ConnectionStateChangedMessage());

        Assert.Empty(sut.RootTypes);
        Assert.False(sut.HasTypes);
        Assert.Null(sut.SelectedType);
        Assert.False(sut.IsConnected);
    }
}
