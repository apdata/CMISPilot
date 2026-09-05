using System;
using System.Linq;
using CMISPilot.Cmis.Models;
using CMISPilot.ViewModels.Dialogs;

namespace CMISPilot.Tests.ViewModels;

/// <summary>
/// Unit-Tests des Bearbeiten-/Anlegen-Dialog-ViewModels (M7, T7.3, FA-70/71/72):
/// Validierung von Pflichtfeldern/Datentypen und Aufbau der Properties-Map.
/// </summary>
public sealed class EditPropertiesViewModelTests
{
    private static CmisObjectDto Doc(params PropertyDto[] props) => new()
    {
        Id = "d",
        Name = "Datei",
        BaseType = CmisBaseType.Document,
        TypeId = "cmis:document",
        Properties = props
    };

    /// <summary>Ordnertyp-Kandidat für <see cref="EditPropertiesViewModel.ForCreate"/>.</summary>
    private static TypeDefinitionDto FolderType(string id = "cmis:folder", params PropertyDefinitionDto[] defs) =>
        new() { Id = id, DisplayName = id, BaseType = CmisBaseType.Folder, PropertyDefinitions = defs };

    [Fact]
    public void ForCreate_erfordert_Namen()
    {
        var vm = EditPropertiesViewModel.ForCreate("Neuer Ordner", new[] { FolderType() }, "cmis:folder");

        Assert.False(vm.Validate());
        Assert.Equal("Pflichtfeld.", vm.Properties[0].ErrorMessage);
    }

    [Fact]
    public void ForCreate_BuildProperties_enthaelt_Name_und_ObjectTypeId()
    {
        var vm = EditPropertiesViewModel.ForCreate("Neuer Ordner", new[] { FolderType() }, "cmis:folder");
        vm.Properties[0].Value = "Mein Ordner";

        var props = vm.BuildProperties();

        Assert.Equal("Mein Ordner", props["cmis:name"]);
        Assert.Equal("cmis:folder", props["cmis:objectTypeId"]);
    }

    [Fact]
    public void ForCreate_fragt_Pflichtfelder_des_gewaehlten_Typs_ab()
    {
        var typeWithRequired = FolderType("my:folder",
            new PropertyDefinitionDto
            {
                Id = "my:code", DisplayName = "Code", PropertyType = CmisPropertyType.String,
                IsRequired = true, Cardinality = CmisCardinality.Single, Updatability = CmisUpdatability.OnCreate
            },
            new PropertyDefinitionDto
            {
                Id = "my:optional", DisplayName = "Optional", PropertyType = CmisPropertyType.String,
                IsRequired = false
            });

        var vm = EditPropertiesViewModel.ForCreate("Neuer Ordner", new[] { typeWithRequired }, "my:folder");

        // Name + Pflichtfeld + optionales Feld werden abgefragt.
        Assert.Contains(vm.Properties, p => p.Id == "cmis:name");
        Assert.Contains(vm.Properties, p => p.Id == "my:code" && p.IsRequired);
        Assert.Contains(vm.Properties, p => p.Id == "my:optional" && !p.IsRequired);

        // Reihenfolge: Name zuerst, dann Pflichtfeld, dann optionales Feld.
        Assert.Equal("cmis:name", vm.Properties[0].Id);
        Assert.Equal("my:code", vm.Properties[1].Id);
        Assert.Equal("my:optional", vm.Properties[2].Id);

        // Ohne Wert für das Pflichtfeld schlägt die Validierung fehl.
        vm.Properties.First(p => p.Id == "cmis:name").Value = "X";
        Assert.False(vm.Validate());

        // Optionales Feld leer lassen ist ok, Pflichtfeld muss gefüllt sein.
        vm.Properties.First(p => p.Id == "my:code").Value = "ABC";
        Assert.True(vm.Validate());
        Assert.Equal("ABC", vm.BuildProperties()["my:code"]);
        Assert.Null(vm.BuildProperties()["my:optional"]);
    }

    [Fact]
    public void ForEdit_blendet_ReadOnly_und_MultiValue_Properties_aus()
    {
        var target = Doc(
            new PropertyDto { Id = "cmis:name", DisplayName = "Name", PropertyType = CmisPropertyType.String, ValueAsString = "Datei" },
            new PropertyDto { Id = "cmis:objectId", DisplayName = "ObjectId", PropertyType = CmisPropertyType.Id, ValueAsString = "d" },
            new PropertyDto { Id = "cmis:creationDate", DisplayName = "Erstellt", PropertyType = CmisPropertyType.DateTime, ValueAsString = "2026-01-01" },
            new PropertyDto { Id = "custom:tags", DisplayName = "Tags", PropertyType = CmisPropertyType.String, IsMultiValued = true, ValueAsString = "a,b" });

        var vm = EditPropertiesViewModel.ForEdit(target);

        Assert.Single(vm.Properties);
        Assert.Equal("cmis:name", vm.Properties[0].Id);
    }

    [Fact]
    public void Validate_meldet_ungueltige_Ganzzahl()
    {
        var target = Doc(
            new PropertyDto { Id = "cmis:name", DisplayName = "Name", PropertyType = CmisPropertyType.String, ValueAsString = "Datei" },
            new PropertyDto { Id = "custom:count", DisplayName = "Anzahl", PropertyType = CmisPropertyType.Integer, ValueAsString = "3" });

        var vm = EditPropertiesViewModel.ForEdit(target);
        var countField = vm.Properties[1];
        countField.Value = "nicht-numerisch";

        Assert.False(vm.Validate());
        Assert.NotNull(countField.ErrorMessage);
    }

    [Fact]
    public void BuildProperties_konvertiert_Ganzzahl_und_leeres_Feld_wird_null()
    {
        var target = Doc(
            new PropertyDto { Id = "cmis:name", DisplayName = "Name", PropertyType = CmisPropertyType.String, ValueAsString = "Datei" },
            new PropertyDto { Id = "custom:count", DisplayName = "Anzahl", PropertyType = CmisPropertyType.Integer, ValueAsString = "3" },
            new PropertyDto { Id = "custom:note", DisplayName = "Notiz", PropertyType = CmisPropertyType.String, ValueAsString = "x" });

        var vm = EditPropertiesViewModel.ForEdit(target);
        vm.Properties[1].Value = "42";
        vm.Properties[2].Value = string.Empty; // Feld geleert -> Property wird gelöscht (null).

        var props = vm.BuildProperties();

        Assert.Equal(42L, props["custom:count"]);
        Assert.Null(props["custom:note"]);
    }

    [Fact]
    public void BuildProperties_wirft_wenn_ungueltig()
    {
        var vm = EditPropertiesViewModel.ForCreate("Neuer Ordner", new[] { FolderType() }, "cmis:folder");
        Assert.Throws<InvalidOperationException>(() => vm.BuildProperties());
    }

    /// <summary>
    /// PortCMIS liefert das vorbelegte Datumsfeld ueber <c>Property.ValueAsString</c>, das
    /// intern <c>DateTime.ToString()</c> aufruft und damit in CurrentCulture formatiert (auf
    /// einem deutschen Windows z. B. "31.12.2025"). Vorher wurde beim Speichern ausschliesslich
    /// mit InvariantCulture geparst (Muster M/d/yyyy) - ein unveraendert uebernommener oder
    /// erneut deutsch eingegebener Wert wie "31.12.2025" scheiterte deshalb an der Validierung.
    /// </summary>
    [Fact]
    public void Validate_akzeptiert_Datum_in_CurrentCulture_Format()
    {
        var original = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");

            var target = Doc(
                new PropertyDto { Id = "cmis:name", DisplayName = "Name", PropertyType = CmisPropertyType.String, ValueAsString = "Datei" },
                new PropertyDto { Id = "custom:gueltigBis", DisplayName = "Gueltig bis", PropertyType = CmisPropertyType.DateTime, ValueAsString = "01.01.2026" });

            var vm = EditPropertiesViewModel.ForEdit(target);
            var dateField = vm.Properties[1];
            dateField.Value = "31.12.2025";

            Assert.True(vm.Validate());
            Assert.Null(dateField.ErrorMessage);
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = original;
        }
    }

    /// <summary>
    /// PortCMIS' <c>DateTimeHelper</c> rechnet beim Senden/Empfangen nur ueber
    /// <c>DateTime.Ticks</c> in Millisekunden um (ConvertDateTimeToMillis/
    /// ConvertMillisToDateTime), ganz ohne Zeitzonenumrechnung. Ein eingegebenes Datum
    /// darf deshalb beim Bauen der Properties-Map nicht ueber die lokale Zeitzone nach
    /// UTC verschoben werden - sonst kommt beim erneuten Anzeigen der falsche Tag heraus
    /// (real mit einem Alfresco-Testsystem in Winterzeit beobachtet: "01.01.2026"
    /// eingegeben, "31.12.2025 23:00:00" gespeichert).
    /// </summary>
    [Fact]
    public void BuildProperties_verschiebt_Datum_nicht_ueber_die_Zeitzone()
    {
        var target = Doc(
            new PropertyDto { Id = "cmis:name", DisplayName = "Name", PropertyType = CmisPropertyType.String, ValueAsString = "Datei" },
            new PropertyDto { Id = "custom:gueltigBis", DisplayName = "Gueltig bis", PropertyType = CmisPropertyType.DateTime, ValueAsString = "01.01.2026" });

        var vm = EditPropertiesViewModel.ForEdit(target);
        vm.Properties[1].Value = "01.01.2026";

        var props = vm.BuildProperties();

        var stored = Assert.IsType<DateTime>(props["custom:gueltigBis"]);
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0), stored);
    }

    // --- F1: Datei-Inhalt beim Anlegen ---

    /// <summary>Dokumenttyp-Kandidat (auch abgeleitete Typen haben BaseType = Document).</summary>
    private static TypeDefinitionDto DocType(string id = "cmis:document") =>
        new() { Id = id, DisplayName = id, BaseType = CmisBaseType.Document };

    [Fact]
    public void AllowContent_nur_bei_Dokumenttypen()
    {
        var doc = EditPropertiesViewModel.ForCreate("Neues Dokument", new[] { DocType() }, "cmis:document");
        Assert.True(doc.AllowContent);

        // Auch ein abgeleiteter Dokumenttyp erlaubt einen Inhalt.
        var abgeleitet = EditPropertiesViewModel.ForCreate(
            "Neues Dokument", new[] { DocType("my:rechnung") }, "my:rechnung");
        Assert.True(abgeleitet.AllowContent);

        // Ordner haben keinen Content-Stream.
        var ordner = EditPropertiesViewModel.ForCreate("Neuer Ordner", new[] { FolderType() }, "cmis:folder");
        Assert.False(ordner.AllowContent);

        // Beim Bearbeiten wird kein Inhalt mitgegeben (dafuer gibt es "Inhalt setzen").
        var bearbeiten = EditPropertiesViewModel.ForEdit(Doc());
        Assert.False(bearbeiten.AllowContent);
    }

    [Fact]
    public void Dateiauswahl_uebernimmt_den_Dateinamen_als_Namen_wenn_noch_keiner_gesetzt_ist()
    {
        var vm = EditPropertiesViewModel.ForCreate("Neues Dokument", new[] { DocType() }, "cmis:document");

        vm.ContentFilePath = System.IO.Path.Combine("C:", "tmp", "Rechnung.pdf");

        Assert.True(vm.HasContentFile);
        Assert.Equal("Rechnung.pdf", vm.ContentFileName);
        Assert.Equal("Rechnung.pdf", vm.Name);
    }

    [Fact]
    public void Dateiauswahl_ueberschreibt_einen_bereits_eingegebenen_Namen_nicht()
    {
        var vm = EditPropertiesViewModel.ForCreate("Neues Dokument", new[] { DocType() }, "cmis:document");
        vm.Properties.First(p => p.Id == "cmis:name").Value = "Eigener Name";

        vm.ContentFilePath = System.IO.Path.Combine("C:", "tmp", "Rechnung.pdf");

        Assert.Equal("Eigener Name", vm.Name);
        Assert.Equal("Rechnung.pdf", vm.ContentFileName);
    }
}
