using System;
using System.Threading;
using CMISPilot.Cmis.Contracts;
using CMISPilot.Cmis.Errors;
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
/// VM-Unit-Tests für das Eigenschaften-Werkzeugfenster (R4 Etappe 3): Reaktion auf
/// <see cref="NodeSelectedMessage"/> und Mapping von Displayname/PropertyID/Datentyp/
/// Pflichtfeld aus <see cref="PropertyDto"/> + <see cref="PropertyDefinitionDto"/>
/// (gemockter <see cref="ITypeService"/>, Politik M11: keine Server-Tests).
/// </summary>
public sealed class PropertiesViewModelTests
{
    private readonly ITypeService _types = Substitute.For<ITypeService>();
    private readonly IMessenger _messenger = new WeakReferenceMessenger();

    private PropertiesViewModel CreateSut() => new(
        _types, _messenger,
        Substitute.For<IDialogService>(),
        Substitute.For<IListExporter>(),
        NullLogger<PropertiesViewModel>.Instance);

    private static CmisObjectDto Folder(
        string typeId = "cmis:folder", string[]? secondaryTypeIds = null, params PropertyDto[] props) => new()
    {
        Id = "f1",
        Name = "Ordner",
        BaseType = CmisBaseType.Folder,
        TypeId = typeId,
        SecondaryTypeIds = secondaryTypeIds ?? Array.Empty<string>(),
        Properties = props
    };

    private static PropertyDto Prop(string id, string? displayName = null, string? value = "wert") => new()
    {
        Id = id,
        DisplayName = displayName,
        ValueAsString = value
    };

    [Fact]
    public void OhneAuswahl_BleibenPropertiesLeer()
    {
        var sut = CreateSut();

        _messenger.Send(new NodeSelectedMessage(null));

        Assert.Empty(sut.Properties);
    }

    [Fact]
    public void BeiAuswahl_MapptDisplaynamePropertyIdDatentypUndPflichtfeld()
    {
        var typeDef = new TypeDefinitionDto
        {
            Id = "cmis:folder",
            PropertyDefinitions = new[]
            {
                new PropertyDefinitionDto
                {
                    Id = "cmis:name",
                    DisplayName = "Name",
                    PropertyType = CmisPropertyType.String,
                    IsRequired = true
                }
            }
        };
        _types.GetTypeDefinitionAsync("cmis:folder", Arg.Any<CancellationToken>()).Returns(typeDef);

        var sut = CreateSut();
        var folder = Folder(props: Prop("cmis:name", value: "Testordner"));

        _messenger.Send(new NodeSelectedMessage(folder));

        var row = Assert.Single(sut.Properties);
        Assert.Equal("Name", row.DisplayName);
        Assert.Equal("cmis:name", row.PropertyId);
        Assert.Equal("String", row.DataType);
        Assert.True(row.IsRequired);
        Assert.Equal("Testordner", row.Value);
        Assert.Equal("cmis:folder", row.OwningTypeId);
    }

    [Fact]
    public void SecondaryType_LiefertMetadatenUndHerkunftsTypFuerAspektProperty()
    {
        _types.GetTypeDefinitionAsync("cmis:folder", Arg.Any<CancellationToken>())
            .Returns(new TypeDefinitionDto { Id = "cmis:folder" });
        _types.GetTypeDefinitionAsync("P:aspekt:akomv", Arg.Any<CancellationToken>())
            .Returns(new TypeDefinitionDto
            {
                Id = "P:aspekt:akomv",
                PropertyDefinitions = new[]
                {
                    new PropertyDefinitionDto
                    {
                        Id = "akomv:aktenzeichen",
                        DisplayName = "Aktenzeichen",
                        PropertyType = CmisPropertyType.String
                    }
                }
            });

        var sut = CreateSut();
        var folder = Folder(
            secondaryTypeIds: new[] { "P:aspekt:akomv" },
            props: Prop("akomv:aktenzeichen", value: "AZ-123"));

        _messenger.Send(new NodeSelectedMessage(folder));

        var row = Assert.Single(sut.Properties);
        Assert.Equal("Aktenzeichen", row.DisplayName);
        Assert.Equal("String", row.DataType);
        Assert.Equal("AZ-123", row.Value);
        Assert.Equal("P:aspekt:akomv", row.OwningTypeId);
    }

    [Fact]
    public void SecondaryType_FehlerBeimLadenLaesstPrimaerenTypUnberuehrt()
    {
        _types.GetTypeDefinitionAsync("cmis:folder", Arg.Any<CancellationToken>())
            .Returns(new TypeDefinitionDto
            {
                Id = "cmis:folder",
                PropertyDefinitions = new[]
                {
                    new PropertyDefinitionDto { Id = "cmis:name", DisplayName = "Name", PropertyType = CmisPropertyType.String }
                }
            });
        _types.GetTypeDefinitionAsync("P:aspekt:defekt", Arg.Any<CancellationToken>())
            .Returns<TypeDefinitionDto>(_ => throw new CmisNotFoundException("Typ nicht gefunden"));

        var sut = CreateSut();
        var folder = Folder(
            secondaryTypeIds: new[] { "P:aspekt:defekt" },
            props: Prop("cmis:name", value: "Testordner"));

        _messenger.Send(new NodeSelectedMessage(folder));

        var row = Assert.Single(sut.Properties);
        Assert.Equal("Name", row.DisplayName);
        Assert.Equal("cmis:folder", row.OwningTypeId);
    }

    [Fact]
    public void OhnePassendeDefinition_ZeigtRohenWertMitLeerenMetadaten()
    {
        _types.GetTypeDefinitionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TypeDefinitionDto { Id = "cmis:folder" });

        var sut = CreateSut();
        var folder = Folder(props: Prop("apx:unbekannt", "Unbekannt", "roh"));

        _messenger.Send(new NodeSelectedMessage(folder));

        var row = Assert.Single(sut.Properties);
        Assert.Equal("Unbekannt", row.DisplayName);
        Assert.Equal("apx:unbekannt", row.PropertyId);
        Assert.Equal(string.Empty, row.DataType);
        Assert.False(row.IsRequired);
        Assert.Equal("roh", row.Value);
    }

    [Fact]
    public void FehlerBeimLadenDerTypdefinition_ZeigtTrotzdemRohePropertyWerte()
    {
        _types.GetTypeDefinitionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<TypeDefinitionDto>(_ => throw new CmisNotFoundException("Typ nicht gefunden"));

        var sut = CreateSut();
        var folder = Folder(props: Prop("cmis:name", value: "Testordner"));

        _messenger.Send(new NodeSelectedMessage(folder));

        var row = Assert.Single(sut.Properties);
        Assert.Equal("cmis:name", row.DisplayName);
        Assert.Equal(string.Empty, row.DataType);
        Assert.False(row.IsRequired);
        Assert.Equal("Testordner", row.Value);
    }

    [Fact]
    public void NeueAuswahl_ErsetztVorherigeProperties()
    {
        _types.GetTypeDefinitionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TypeDefinitionDto { Id = "cmis:folder" });

        var sut = CreateSut();

        _messenger.Send(new NodeSelectedMessage(Folder(props: Prop("cmis:name", value: "Erster"))));
        Assert.Single(sut.Properties);

        _messenger.Send(new NodeSelectedMessage(null));
        Assert.Empty(sut.Properties);
    }

    // --- Filter (Matches) ---

    private static PropertyRowViewModel Row(
        string displayName = "Name", string propertyId = "cmis:name", string value = "Testordner") =>
        new() { DisplayName = displayName, PropertyId = propertyId, Value = value };

    [Fact]
    public void OhneFiltertext_PasstJedeZeile()
    {
        var sut = CreateSut();

        Assert.True(sut.Matches(Row()));

        sut.FilterText = "   ";
        Assert.True(sut.Matches(Row()));
    }

    [Fact]
    public void OhneAngehakteSpalte_PasstJedeZeile()
    {
        // Alles auszublenden waere die schlechtere Ueberraschung: eine ohne
        // erkennbaren Grund leere Tabelle.
        var sut = CreateSut();
        sut.FilterText = "kommt nicht vor";
        sut.FilterByDisplayName = false;
        sut.FilterByPropertyId = false;
        sut.FilterByValue = false;

        Assert.True(sut.Matches(Row()));
    }

    [Fact]
    public void JeCheckbox_GreiftGenauDieZugehoerigeSpalte()
    {
        var sut = CreateSut();
        var row = Row(displayName: "Erstellt von", propertyId: "cmis:createdBy", value: "admin");

        sut.FilterByDisplayName = true;
        sut.FilterByPropertyId = false;
        sut.FilterByValue = false;

        sut.FilterText = "Erstellt";
        Assert.True(sut.Matches(row));
        sut.FilterText = "createdBy";
        Assert.False(sut.Matches(row));
        sut.FilterText = "admin";
        Assert.False(sut.Matches(row));

        sut.FilterByDisplayName = false;
        sut.FilterByPropertyId = true;
        sut.FilterText = "createdBy";
        Assert.True(sut.Matches(row));

        sut.FilterByPropertyId = false;
        sut.FilterByValue = true;
        sut.FilterText = "admin";
        Assert.True(sut.Matches(row));
    }

    [Fact]
    public void Filter_IgnoriertGrossKleinschreibung()
    {
        var sut = CreateSut();
        sut.FilterText = "ERSTELLT";

        Assert.True(sut.Matches(Row(displayName: "Erstellt von")));
    }

    [Fact]
    public void MehrereCheckboxen_WirkenAlsOderVerknuepfung()
    {
        var sut = CreateSut();
        sut.FilterByDisplayName = true;
        sut.FilterByPropertyId = true;
        sut.FilterByValue = false;
        sut.FilterText = "cmis:createdBy";

        // Treffer allein ueber die PropertyID, obwohl der Displayname nicht passt.
        Assert.True(sut.Matches(Row(displayName: "Erstellt von", propertyId: "cmis:createdBy", value: "admin")));
    }

    [Fact]
    public void OhneTreffer_PasstDieZeileNicht()
    {
        var sut = CreateSut();
        sut.FilterText = "kommt nicht vor";

        Assert.False(sut.Matches(Row()));
    }

    // --- Filter: Wirkung auf VisibleProperties ---
    //
    // Matches allein sagt nichts darueber, ob die Tabelle sich auch aendert. Genau
    // daran ist die erste Fassung gescheitert: das Praedikat war gesetzt, wurde aber
    // nie neu ausgewertet. Diese Tests pruefen deshalb die Sammlung, an der die
    // Tabelle haengt.

    private PropertiesViewModel SutMitDreiZeilen()
    {
        _types.GetTypeDefinitionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TypeDefinitionDto { Id = "cmis:folder" });

        var sut = CreateSut();
        _messenger.Send(new NodeSelectedMessage(Folder(
            "cmis:folder", null,
            Prop("cmis:name", value: "Testordner"),
            Prop("cmis:createdBy", value: "admin"),
            Prop("cmis:objectId", value: "abc-123"))));

        Assert.Equal(3, sut.VisibleProperties.Count);
        return sut;
    }

    [Fact]
    public void OhneFilter_ZeigtDieTabelleAlleZeilen()
    {
        var sut = SutMitDreiZeilen();

        Assert.Equal(sut.Properties.Count, sut.VisibleProperties.Count);
    }

    [Fact]
    public void Filtertext_GrenztDieTabelleSofortEin()
    {
        var sut = SutMitDreiZeilen();

        sut.FilterText = "createdBy";

        var row = Assert.Single(sut.VisibleProperties);
        Assert.Equal("cmis:createdBy", row.PropertyId);
        // Die zugrunde liegende Sammlung bleibt vollstaendig (der Export nutzt sie).
        Assert.Equal(3, sut.Properties.Count);
    }

    [Fact]
    public void CheckboxWechsel_WertetDenFilterNeuAus()
    {
        var sut = SutMitDreiZeilen();
        sut.FilterText = "admin";

        // Trifft nur ueber die Wert-Spalte.
        Assert.Single(sut.VisibleProperties);

        sut.FilterByValue = false;
        Assert.Empty(sut.VisibleProperties);

        sut.FilterByValue = true;
        Assert.Single(sut.VisibleProperties);
    }

    [Fact]
    public void LeerenDesFeldes_StelltAlleZeilenWiederHer()
    {
        var sut = SutMitDreiZeilen();
        sut.FilterText = "createdBy";
        Assert.Single(sut.VisibleProperties);

        sut.FilterText = string.Empty;

        Assert.Equal(3, sut.VisibleProperties.Count);
    }

    [Fact]
    public void NeueAuswahl_WendetDenBestehendenFilterAn()
    {
        var sut = SutMitDreiZeilen();
        sut.FilterText = "createdBy";
        Assert.Single(sut.VisibleProperties);

        _messenger.Send(new NodeSelectedMessage(Folder(props:
            Prop("cmis:name", value: "Anderer Ordner"))));

        Assert.Empty(sut.VisibleProperties);
        Assert.Single(sut.Properties);
    }

    [Fact]
    public void OhneAuswahl_IstAuchDieTabelleLeer()
    {
        var sut = SutMitDreiZeilen();

        _messenger.Send(new NodeSelectedMessage(null));

        Assert.Empty(sut.VisibleProperties);
    }
}
