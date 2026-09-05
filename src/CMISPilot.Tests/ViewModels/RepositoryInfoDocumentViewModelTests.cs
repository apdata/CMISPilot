using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CMISPilot.ViewModels.Shell;
using CMISPilot.Cmis.Contracts;
using CMISPilot.Cmis.Models;
using CMISPilot.ViewModels.Export;
using CMISPilot.ViewModels.Repository;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CMISPilot.Tests.ViewModels;

/// <summary>
/// Unit-Tests des Repository-Info-Tabs (FA-10/FA-11): Aufbau der drei Abschnitte,
/// Verhalten beim Verbinden und Trennen sowie die Ausfuehrbarkeit der Export-Kommandos.
/// Politik M11: keine Server-Tests, alle Dienste gemockt.
/// </summary>
public sealed class RepositoryInfoDocumentViewModelTests
{
    private readonly IRepositoryInfoService _service = Substitute.For<IRepositoryInfoService>();
    private readonly ISessionContext _session = Substitute.For<ISessionContext>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly IListExporter _exporter = Substitute.For<IListExporter>();

    private RepositoryInfoDocumentViewModel CreateSut() => new(
        _service, _session, _dialogs, _exporter,
        NullLogger<RepositoryInfoDocumentViewModel>.Instance);

    private static RepositoryInfoDto Info() => new()
    {
        Id = "A1",
        Name = "Testablage",
        VendorName = "Apache",
        ProductName = "OpenCMIS InMemory",
        ProductVersion = "1.1",
        CmisVersion = "1.1",
        RootFolderId = "root",
        ChangesOnType = ["Document", "Folder"],
        Capabilities = new RepositoryCapabilitiesDto
        {
            Query = "bothcombined",
            GetDescendantsSupported = true,
            CreatablePropertyTypes = ["Boolean", "String"],
            NewTypeSettableAttributes = new NewTypeSettableAttributesDto { Id = true, Queryable = false }
        },
        AclCapabilities = new AclCapabilitiesDto
        {
            SupportedPermissions = "Basic",
            AclPropagation = "ObjectOnly",
            Permissions = [new PermissionDefinitionDto("cmis:read", "Lesen")],
            PermissionMapping = [new PermissionMappingDto("canGetProperties.Object", ["cmis:read", "cmis:write"])]
        },
        ExtensionFeatures =
        [
            new("apx:feature", "APX-Erweiterung", "1.0", "https://example.com/feature", "Beschreibung",
                [new KeyValuePair<string, string>("modus", "aktiv")])
        ],
        Extensions =
        [
            new("vendorBlock", null, null, [],
                [new CmisExtensionElementDto("maxSize", null, "42", [], [])])
        ]
    };

    [Fact]
    public void OhneVerbindung_BleibtDieAnsichtLeer()
    {
        _session.IsConnected.Returns(false);

        var sut = CreateSut();

        Assert.False(sut.HasData);
        Assert.Empty(sut.GeneralRows);
        Assert.False(sut.ExportToExcelCommand.CanExecute(null));
        Assert.False(sut.SaveJsonCommand.CanExecute(null));
    }

    [Fact]
    public void BeiVerbindung_BautEckdatenCapabilitiesUndAclAuf()
    {
        _session.IsConnected.Returns(true);
        _service.GetRepositoryInfoAsync(Arg.Any<CancellationToken>()).Returns(Info());

        var sut = CreateSut();

        Assert.True(sut.HasData);
        Assert.Equal("Testablage", sut.RepositoryTitle);

        Assert.Contains(sut.GeneralRows, r => r.Name == "Name" && r.Value == "Testablage");
        Assert.Contains(sut.GeneralRows, r => r.Name == "ID" && r.Value == "A1");
        Assert.Contains(sut.GeneralRows, r => r.Name == "Änderungen für Typen" && r.Value == "Document, Folder");

        Assert.Contains(sut.CapabilityRows, r => r.Name == "Abfragen" && r.Value == "bothcombined");
        // Boolesche Faehigkeiten werden lesbar ausgeschrieben, nicht als True/False.
        Assert.Contains(sut.CapabilityRows, r => r.Name == "GetDescendants" && r.Value == "Ja");

        Assert.Contains(sut.AclRows, r => r.Name == "Unterstützte Berechtigungen" && r.Value == "Basic");
        Assert.Contains(sut.AclRows, r => r.Name == "Berechtigung: cmis:read" && r.Value == "Lesen");
        Assert.Contains(sut.AclRows, r => r.Name == "canGetProperties.Object" && r.Value == "cmis:read, cmis:write");
    }

    [Fact]
    public void LeereAngaben_ErzeugenKeineZeile()
    {
        // Die Server liefern sehr unterschiedlich viel; halb leere Zeilen waeren nur Rauschen.
        _session.IsConnected.Returns(true);
        _service.GetRepositoryInfoAsync(Arg.Any<CancellationToken>())
            .Returns(new RepositoryInfoDto { Id = "A1", Name = "Testablage" });

        var sut = CreateSut();

        Assert.DoesNotContain(sut.GeneralRows, r => r.Name == "Beschreibung");
        Assert.Empty(sut.CapabilityRows);
        Assert.Empty(sut.AclRows);
    }

    [Fact]
    public void NachDemTrennen_IstDieAnsichtWiederLeer()
    {
        _session.IsConnected.Returns(true);
        _service.GetRepositoryInfoAsync(Arg.Any<CancellationToken>()).Returns(Info());

        var sut = CreateSut();
        Assert.True(sut.HasData);

        _session.IsConnected.Returns(false);
        _session.ConnectionChanged += Raise.Event<EventHandler>(_session, EventArgs.Empty);

        Assert.False(sut.HasData);
        Assert.Empty(sut.GeneralRows);
        Assert.Equal(string.Empty, sut.RepositoryTitle);
    }

    [Fact]
    public void MitDaten_SindDieExportKommandosAusfuehrbar()
    {
        _session.IsConnected.Returns(true);
        _service.GetRepositoryInfoAsync(Arg.Any<CancellationToken>()).Returns(Info());

        var sut = CreateSut();

        Assert.True(sut.ExportToExcelCommand.CanExecute(null));
        Assert.True(sut.SaveJsonCommand.CanExecute(null));
    }

    [Fact]
    public void BuildExportRows_GruppiertNachAbschnitt()
    {
        _session.IsConnected.Returns(true);
        _service.GetRepositoryInfoAsync(Arg.Any<CancellationToken>()).Returns(Info());

        var sut = CreateSut();
        var rows = sut.BuildExportRows();

        Assert.Contains(rows, r => r.Section == "Eckdaten" && r.Name == "ID");
        Assert.Contains(rows, r => r.Section == "Capabilities" && r.Name == "Abfragen");
        Assert.Contains(rows, r => r.Section == "ACL-Capabilities");
        Assert.Equal(sut.GeneralRows.Count + sut.CapabilityRows.Count + sut.AclRows.Count, rows.Count);
    }

    [Fact]
    public async Task ExportNachExcel_OhneZielpfad_SchreibtNichts()
    {
        _session.IsConnected.Returns(true);
        _service.GetRepositoryInfoAsync(Arg.Any<CancellationToken>()).Returns(Info());
        _dialogs.PickSaveFileAsync(Arg.Any<string>()).Returns((string?)null);

        var sut = CreateSut();
        await sut.ExportToExcelCommand.ExecuteAsync(null);

        await _exporter.DidNotReceive().ExportRepositoryInfoAsync(
            Arg.Any<RepositoryInfoDto>(),
            Arg.Any<IReadOnlyList<RepositoryInfoRowExportDto>>(),
            Arg.Any<IReadOnlyList<PermissionMappingExportDto>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Capabilities_EnthaltenDieCmis11Faehigkeiten()
    {
        _session.IsConnected.Returns(true);
        _service.GetRepositoryInfoAsync(Arg.Any<CancellationToken>()).Returns(Info());

        var sut = CreateSut();

        Assert.Contains(sut.CapabilityRows, r => r.Name == "Anlegbare Property-Typen" && r.Value == "Boolean, String");
        Assert.Contains(sut.CapabilityRows, r => r.Name == "Neuer Typ: ID setzbar" && r.Value == "Ja");
        Assert.Contains(sut.CapabilityRows, r => r.Name == "Neuer Typ: Queryable setzbar" && r.Value == "Nein");
    }

    [Fact]
    public void ErweiterungenUndErweiterungsdaten_LandenInDenEckdaten()
    {
        _session.IsConnected.Returns(true);
        _service.GetRepositoryInfoAsync(Arg.Any<CancellationToken>()).Returns(Info());

        var sut = CreateSut();

        Assert.Contains(sut.GeneralRows, r => r.Name == "Erweiterung: APX-Erweiterung");
        Assert.Contains(sut.GeneralRows, r => r.Name == "Erweiterung APX-Erweiterung: modus" && r.Value == "aktiv");
        // Der Erweiterungsbaum wird als Pfad geplattet, damit die Herkunft ablesbar bleibt.
        Assert.Contains(sut.GeneralRows,
            r => r.Name == "Erweiterungsdaten > vendorBlock > maxSize" && r.Value == "42");
    }

    [Fact]
    public void BuildPermissionMappingRows_LiefertEineZeileJePaar()
    {
        _session.IsConnected.Returns(true);
        _service.GetRepositoryInfoAsync(Arg.Any<CancellationToken>()).Returns(Info());

        var sut = CreateSut();
        var mapping = sut.BuildPermissionMappingRows();

        Assert.Equal(2, mapping.Count);
        Assert.All(mapping, m => Assert.Equal("canGetProperties.Object", m.Key));
        Assert.Contains(mapping, m => m.Permission == "cmis:read");
        Assert.Contains(mapping, m => m.Permission == "cmis:write");
    }
}
