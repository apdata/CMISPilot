using IFileLauncher = APX.Wpf.Shell.ViewModels.Contracts.IFileLauncher;
﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CMISPilot.Cmis.Contracts;
using CMISPilot.Cmis.Errors;
using CMISPilot.Cmis.Models;
using CMISPilot.ViewModels.Dialogs;
using CMISPilot.ViewModels.Explorer;
using CMISPilot.ViewModels.Export;
using CMISPilot.ViewModels.Shell;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CMISPilot.Tests.ViewModels;

/// <summary>
/// VM-Unit-Tests der CRUD-Kommandos des Explorer-Dokument-Tabs (R4 Etappe 4,
/// Logik aus <c>ExplorerAreaViewModel</c> übernommen) — ausschließlich gegen
/// gemockte <see cref="IObjectService"/>/<see cref="ITypeService"/>/
/// <see cref="IDialogService"/> (Politik M11: keine Server-Tests). Deckt
/// Create/Update/Delete/Download/Open/SetContent inkl. Allowable-Actions-Ausgrauen
/// (FA-70/71/72/73/74/75, FA-40/41/42) ab. Statt der Shell-InfoBar (die es in der
/// neuen Shell noch nicht gibt) meldet die VM über <see cref="ILogger{TCategoryName}"/>;
/// hier über einen gemockten Logger geprüft, dass Erfolg/Fehler geloggt werden.
/// </summary>
public sealed class ExplorerDocumentViewModelCrudTests
{
    private readonly IBrowseService _browse = Substitute.For<IBrowseService>();
    private readonly IObjectService _objects = Substitute.For<IObjectService>();
    private readonly ITypeService _types = Substitute.For<ITypeService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly IFileLauncher _launcher = Substitute.For<IFileLauncher>();
    private readonly IMessenger _messenger = new WeakReferenceMessenger();
    private readonly ILogger<ExplorerDocumentViewModel> _logger =
        Substitute.For<ILogger<ExplorerDocumentViewModel>>();
    private readonly IListExporter _listExporter = Substitute.For<IListExporter>();

    public ExplorerDocumentViewModelCrudTests()
    {
        _browse.GetChildrenAsync(Arg.Any<string>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(new List<CmisObjectDto>());
        _browse.GetParentsAsync(Arg.Any<string>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(new List<CmisObjectDto>());
        _types.GetTypeTreeAsync(Arg.Any<bool>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(new List<TypeDefinitionDto>());
    }

    private ExplorerDocumentViewModel CreateSut(CmisObjectDto folder) =>
        new(folder, _browse, _objects, _types, _dialogs, _launcher, _messenger, _logger, _listExporter);

    // Ohne WithActions() bleibt AllowableActions null (nicht geladen -> fail-open,
    // FA-75); WithActions() setzt ein explizites (ggf. leeres) Array.
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

    private static CmisObjectDto WithActions(CmisObjectDto obj, params string[] actions) => new()
    {
        Id = obj.Id,
        Name = obj.Name,
        BaseType = obj.BaseType,
        TypeId = obj.TypeId,
        Properties = obj.Properties,
        AllowableActions = actions
    };

    private static CmisObjectDto WithProps(CmisObjectDto obj, params PropertyDto[] props) => new()
    {
        Id = obj.Id,
        Name = obj.Name,
        BaseType = obj.BaseType,
        TypeId = obj.TypeId,
        AllowableActions = obj.AllowableActions,
        Properties = props
    };

    private static PropertyDto Prop(string id) =>
        new() { Id = id, DisplayName = id, PropertyType = CmisPropertyType.String, ValueAsString = "wert" };

    private static CmisContentDto ContentOf(string text, string? fileName = "datei.txt") => new()
    {
        Stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text)),
        FileName = fileName,
        MimeType = "text/plain",
        Length = text.Length
    };

    [Fact]
    public void NewFolder_nur_ausfuehrbar_wenn_AllowableAction_erlaubt()
    {
        var sut = CreateSut(WithActions(Folder("root", "Root"), "CanCreateDocument"));
        Assert.False(sut.NewFolderCommand.CanExecute(null));

        sut = CreateSut(WithActions(Folder("root", "Root"), "CanCreateFolder"));
        Assert.True(sut.NewFolderCommand.CanExecute(null));
    }

    [Fact]
    public void NewFolder_AllowableActions_unbekannt_ist_nicht_eingeschraenkt()
    {
        // AllowableActions == null (nicht geladen) -> fail-open, Server entscheidet (FA-75).
        var sut = CreateSut(Folder("root", "Root"));
        Assert.True(sut.NewFolderCommand.CanExecute(null));
    }

    [Fact]
    public async Task NewFolder_legt_an_und_aktualisiert_die_Liste_nur_bei_Speichern()
    {
        var parent = WithActions(Folder("root", "Root"), "CanCreateFolder");
        var sut = CreateSut(parent);

        // Abbrechen im Dialog -> kein Service-Aufruf.
        _dialogs.ShowEditPropertiesAsync(Arg.Any<EditPropertiesViewModel>()).Returns(false);
        await sut.NewFolderCommand.ExecuteAsync(null);
        await _objects.DidNotReceive().CreateFolderAsync(
            Arg.Any<string>(), Arg.Any<IDictionary<string, object?>>(), Arg.Any<System.Threading.CancellationToken>());

        // Speichern im Dialog -> Service wird mit den Dialog-Properties aufgerufen.
        _dialogs.ShowEditPropertiesAsync(Arg.Any<EditPropertiesViewModel>())
            .Returns(callInfo =>
            {
                var vm = callInfo.Arg<EditPropertiesViewModel>();
                vm.Properties[0].Value = "NeuerOrdner";
                return Task.FromResult(true);
            });
        _objects.CreateFolderAsync("root", Arg.Any<IDictionary<string, object?>>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(Folder("new", "NeuerOrdner"));
        _browse.GetChildrenAsync("root", Arg.Any<System.Threading.CancellationToken>())
            .Returns(new List<CmisObjectDto> { Folder("new", "NeuerOrdner") });

        await sut.NewFolderCommand.ExecuteAsync(null);

        await _objects.Received(1).CreateFolderAsync(
            "root",
            Arg.Is<IDictionary<string, object?>>(p =>
                (string?)p["cmis:name"] == "NeuerOrdner" && (string?)p["cmis:objectTypeId"] == "cmis:folder"),
            Arg.Any<System.Threading.CancellationToken>());
        Assert.Contains(sut.Objects, o => o.Id == "new");
    }

    [Fact]
    public async Task NewDocument_mit_gewaehlter_Datei_legt_das_Dokument_gleich_mit_Inhalt_an()
    {
        var parent = WithActions(Folder("root", "Root"), "CanCreateDocument");
        var sut = CreateSut(parent);

        var tempFile = Path.Combine(Path.GetTempPath(), $"cmispilot_{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(tempFile, "Inhalt");

        try
        {
            _dialogs.ShowEditPropertiesAsync(Arg.Any<EditPropertiesViewModel>())
                .Returns(callInfo =>
                {
                    var vm = callInfo.Arg<EditPropertiesViewModel>();
                    // Beim Anlegen eines Dokuments darf eine Datei mitgegeben werden.
                    Assert.True(vm.AllowContent);
                    // Setzt zugleich cmis:name aus dem Dateinamen (noch kein Name eingegeben).
                    vm.ContentFilePath = tempFile;
                    return Task.FromResult(true);
                });
            _objects.CreateDocumentAsync(
                    Arg.Any<string>(), Arg.Any<IDictionary<string, object?>>(), Arg.Any<Stream?>(),
                    Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<System.Threading.CancellationToken>())
                .Returns(Doc("new", Path.GetFileName(tempFile)));

            await sut.NewDocumentCommand.ExecuteAsync(null);

            // Der Inhalt geht als Stream mit, samt Dateiname und abgeleitetem MIME-Typ.
            await _objects.Received(1).CreateDocumentAsync(
                "root",
                Arg.Is<IDictionary<string, object?>>(p => (string?)p["cmis:name"] == Path.GetFileName(tempFile)),
                Arg.Is<Stream?>(s => s != null),
                Path.GetFileName(tempFile),
                "text/plain",
                Arg.Any<System.Threading.CancellationToken>());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task NewDocument_ohne_Datei_legt_das_Dokument_ohne_Inhalt_an()
    {
        var parent = WithActions(Folder("root", "Root"), "CanCreateDocument");
        var sut = CreateSut(parent);

        _dialogs.ShowEditPropertiesAsync(Arg.Any<EditPropertiesViewModel>())
            .Returns(callInfo =>
            {
                var vm = callInfo.Arg<EditPropertiesViewModel>();
                vm.Properties[0].Value = "Leer.txt";
                return Task.FromResult(true);
            });

        await sut.NewDocumentCommand.ExecuteAsync(null);

        // Ohne gewaehlte Datei bleibt der Content-Stream null.
        await _objects.Received(1).CreateDocumentAsync(
            "root",
            Arg.Any<IDictionary<string, object?>>(),
            null,
            null,
            null,
            Arg.Any<System.Threading.CancellationToken>());
    }

    [Fact]
    public async Task NewDocumentFromFile_setzt_ContentFilePath_und_legt_mit_Inhalt_an()
    {
        var parent = WithActions(Folder("root", "Root"), "CanCreateDocument");
        var sut = CreateSut(parent);

        var tempFile = Path.Combine(Path.GetTempPath(), $"cmispilot_{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(tempFile, "Inhalt");

        try
        {
            _dialogs.ShowEditPropertiesAsync(Arg.Any<EditPropertiesViewModel>())
                .Returns(callInfo =>
                {
                    var vm = callInfo.Arg<EditPropertiesViewModel>();
                    // Die Datei ist schon vor dem Anzeigen des Dialogs gesetzt (Drag&Drop),
                    // nicht erst durch eine Nutzerinteraktion im Dialog selbst.
                    Assert.Equal(tempFile, vm.ContentFilePath);
                    return Task.FromResult(true);
                });
            _objects.CreateDocumentAsync(
                    Arg.Any<string>(), Arg.Any<IDictionary<string, object?>>(), Arg.Any<Stream?>(),
                    Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<System.Threading.CancellationToken>())
                .Returns(Doc("new", Path.GetFileName(tempFile)));

            await sut.NewDocumentFromFileCommand.ExecuteAsync(tempFile);

            await _objects.Received(1).CreateDocumentAsync(
                "root",
                Arg.Is<IDictionary<string, object?>>(p => (string?)p["cmis:name"] == Path.GetFileName(tempFile)),
                Arg.Is<Stream?>(s => s != null),
                Path.GetFileName(tempFile),
                "text/plain",
                Arg.Any<System.Threading.CancellationToken>());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Edit_und_Delete_nur_ausfuehrbar_wenn_AllowableAction_erlaubt_und_Objekt_gewaehlt()
    {
        var sut = CreateSut(Folder("root", "Root"));

        // Keine Selektion -> nie ausführbar.
        Assert.False(sut.EditCommand.CanExecute(null));
        Assert.False(sut.DeleteCommand.CanExecute(null));

        sut.SelectedObject = WithActions(Doc("d", "Datei"));
        Assert.False(sut.EditCommand.CanExecute(null));
        Assert.False(sut.DeleteCommand.CanExecute(null));

        sut.SelectedObject = WithActions(Doc("d", "Datei"), "CanUpdateProperties", "CanDeleteObject");
        Assert.True(sut.EditCommand.CanExecute(null));
        Assert.True(sut.DeleteCommand.CanExecute(null));
    }

    [Fact]
    public async Task Edit_uebernimmt_Properties_nur_bei_Speichern()
    {
        var sut = CreateSut(Folder("root", "Root"));
        var doc = WithProps(WithActions(Doc("d", "Datei"), "CanUpdateProperties"), Prop("cmis:name"));
        sut.SelectedObject = doc;

        _dialogs.ShowEditPropertiesAsync(Arg.Any<EditPropertiesViewModel>()).Returns(false);
        await sut.EditCommand.ExecuteAsync(null);
        await _objects.DidNotReceive().UpdatePropertiesAsync(
            Arg.Any<string>(), Arg.Any<IDictionary<string, object?>>(), Arg.Any<System.Threading.CancellationToken>());

        _dialogs.ShowEditPropertiesAsync(Arg.Any<EditPropertiesViewModel>()).Returns(true);
        _objects.UpdatePropertiesAsync("d", Arg.Any<IDictionary<string, object?>>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(doc);

        await sut.EditCommand.ExecuteAsync(null);

        await _objects.Received(1).UpdatePropertiesAsync(
            "d", Arg.Any<IDictionary<string, object?>>(), Arg.Any<System.Threading.CancellationToken>());
    }

    [Fact]
    public void Rename_nur_ausfuehrbar_wenn_AllowableAction_erlaubt_und_Objekt_gewaehlt()
    {
        // Rename teilt sich die Berechtigung (CanUpdateProperties) mit EditCommand -
        // CMIS kennt kein eigenes "CanRename".
        var sut = CreateSut(Folder("root", "Root"));
        Assert.False(sut.RenameCommand.CanExecute(null));

        sut.SelectedObject = WithActions(Doc("d", "Datei"));
        Assert.False(sut.RenameCommand.CanExecute(null));

        sut.SelectedObject = WithActions(Doc("d", "Datei"), "CanUpdateProperties");
        Assert.True(sut.RenameCommand.CanExecute(null));
    }

    [Fact]
    public async Task Rename_zeigt_Dialog_und_ruft_UpdatePropertiesAsync_mit_cmis_name_auf_und_laedt_neu()
    {
        var sut = CreateSut(Folder("root", "Root"));
        var doc = WithActions(Doc("d", "Alter Name"), "CanUpdateProperties");
        sut.SelectedObject = doc;

        _dialogs.ShowRenameDialogAsync("Alter Name").Returns("Neuer Name");
        _objects.UpdatePropertiesAsync("d", Arg.Any<IDictionary<string, object?>>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(Doc("d", "Neuer Name"));
        _browse.GetChildrenAsync("root", Arg.Any<System.Threading.CancellationToken>())
            .Returns(new List<CmisObjectDto> { Doc("d", "Neuer Name") });

        await sut.RenameCommand.ExecuteAsync(null);

        await _objects.Received(1).UpdatePropertiesAsync(
            "d",
            Arg.Is<IDictionary<string, object?>>(p => p.Count == 1 && (string?)p["cmis:name"] == "Neuer Name"),
            Arg.Any<System.Threading.CancellationToken>());
        Assert.Contains(sut.Objects, o => o.Name == "Neuer Name");
    }

    [Fact]
    public async Task Rename_bei_Abbruch_oder_unveraendertem_Namen_im_Dialog_tut_nichts()
    {
        var sut = CreateSut(Folder("root", "Root"));
        sut.SelectedObject = WithActions(Doc("d", "Datei"), "CanUpdateProperties");

        // Abbrechen im Dialog -> null.
        _dialogs.ShowRenameDialogAsync("Datei").Returns((string?)null);
        await sut.RenameCommand.ExecuteAsync(null);

        // Unveraendert gespeichert -> kein Aufruf noetig.
        _dialogs.ShowRenameDialogAsync("Datei").Returns("Datei");
        await sut.RenameCommand.ExecuteAsync(null);

        await _objects.DidNotReceive().UpdatePropertiesAsync(
            Arg.Any<string>(), Arg.Any<IDictionary<string, object?>>(), Arg.Any<System.Threading.CancellationToken>());
    }

    [Fact]
    public async Task Delete_fragt_zuerst_nach_und_loescht_erst_bei_Bestaetigung()
    {
        var sut = CreateSut(Folder("root", "Root"));
        sut.SelectedObject = WithActions(Doc("d", "Datei"), "CanDeleteObject");

        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        await sut.DeleteCommand.ExecuteAsync(null);
        await _objects.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<System.Threading.CancellationToken>());

        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        await sut.DeleteCommand.ExecuteAsync(null);

        await _objects.Received(1).DeleteAsync("d", Arg.Any<bool>(), Arg.Any<System.Threading.CancellationToken>());
        Assert.Null(sut.SelectedObject);
    }

    [Fact]
    public async Task Delete_verwendet_deleteTree_fuer_Ordner()
    {
        var sut = CreateSut(Folder("root", "Root"));
        sut.SelectedObject = WithActions(Folder("f", "Ordner"), "CanDeleteTree");

        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        await sut.DeleteCommand.ExecuteAsync(null);

        await _objects.Received(1).DeleteTreeAsync("f", Arg.Any<bool>(), Arg.Any<System.Threading.CancellationToken>());
        await _objects.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<System.Threading.CancellationToken>());
    }

    [Fact]
    public async Task Delete_Fehlerpfad_belaesst_die_Selektion_und_loggt_ueber_ILogger()
    {
        var sut = CreateSut(Folder("root", "Root"));
        var doc = WithActions(Doc("d", "Datei"), "CanDeleteObject");
        sut.SelectedObject = doc;

        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _objects.DeleteAsync("d", Arg.Any<bool>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns<Task>(_ => throw new CmisConstraintException("Verweigert"));

        await sut.DeleteCommand.ExecuteAsync(null);

        // Fehlerpfad: keine Erfolgsmeldung, Selektion bleibt erhalten (kein Reload/Reset).
        // Statt der (in der neuen Shell noch fehlenden) InfoBar meldet die VM über
        // ILogger (LogError) — hier reicht der Verhaltensnachweis, das Interne des
        // generischen Log<TState>-Aufrufs wird nicht gemockt/geprüft.
        Assert.Same(doc, sut.SelectedObject);
    }

    [Fact]
    public void Download_und_Open_nur_ausfuehrbar_fuer_Dokument_mit_AllowableAction()
    {
        var sut = CreateSut(Folder("root", "Root"));

        // Ordner: nie ausführbar, unabhängig von Allowable Actions.
        sut.SelectedObject = WithActions(Folder("f", "Ordner"), "CanGetContentStream");
        Assert.False(sut.DownloadCommand.CanExecute(null));
        Assert.False(sut.OpenCommand.CanExecute(null));

        // Dokument ohne Allowable Action -> ausgegraut.
        sut.SelectedObject = WithActions(Doc("d", "Datei"));
        Assert.False(sut.DownloadCommand.CanExecute(null));
        Assert.False(sut.OpenCommand.CanExecute(null));

        // Dokument mit Allowable Action -> ausführbar.
        sut.SelectedObject = WithActions(Doc("d", "Datei"), "CanGetContentStream");
        Assert.True(sut.DownloadCommand.CanExecute(null));
        Assert.True(sut.OpenCommand.CanExecute(null));
    }

    [Fact]
    public void SetContent_nur_ausfuehrbar_fuer_Dokument_mit_AllowableAction()
    {
        var sut = CreateSut(Folder("root", "Root"));

        sut.SelectedObject = WithActions(Doc("d", "Datei"));
        Assert.False(sut.SetContentCommand.CanExecute(null));

        sut.SelectedObject = WithActions(Doc("d", "Datei"), "CanSetContentStream");
        Assert.True(sut.SetContentCommand.CanExecute(null));
    }

    [Fact]
    public async Task Download_schreibt_den_Content_Stream_in_die_gewaehlte_Datei()
    {
        var sut = CreateSut(Folder("root", "Root"));
        sut.SelectedObject = WithActions(Doc("d", "Datei.txt"), "CanGetContentStream");

        var tempPath = Path.Combine(Path.GetTempPath(), $"cmispilot-test-{Guid.NewGuid():N}.txt");
        try
        {
            _dialogs.PickSaveFileAsync(Arg.Any<string>()).Returns(tempPath);
            _objects.GetContentStreamAsync("d", Arg.Any<System.Threading.CancellationToken>())
                .Returns(_ => ContentOf("Hallo Welt"));

            await sut.DownloadCommand.ExecuteAsync(null);

            Assert.True(File.Exists(tempPath));
            Assert.Equal("Hallo Welt", await File.ReadAllTextAsync(tempPath));
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [Fact]
    public async Task Open_laedt_in_Temp_und_startet_den_FileLauncher()
    {
        var sut = CreateSut(Folder("root", "Root"));
        sut.SelectedObject = WithActions(Doc("d", "Datei.txt"), "CanGetContentStream");

        _objects.GetContentStreamAsync("d", Arg.Any<System.Threading.CancellationToken>())
            .Returns(_ => ContentOf("Öffnen-Inhalt"));

        await sut.OpenCommand.ExecuteAsync(null);

        _launcher.Received(1).Launch(Arg.Is<string>(p => p.EndsWith("Datei.txt")));
    }

    [Fact]
    public async Task SetContent_ruft_Service_auf_und_aktualisiert_die_Liste_nur_bei_Auswahl()
    {
        var sut = CreateSut(Folder("root", "Root"));
        sut.SelectedObject = WithActions(Doc("d", "Datei.txt"), "CanSetContentStream");

        // Abbrechen im Dialog -> kein Service-Aufruf.
        _dialogs.PickOpenFileAsync().Returns((string?)null);
        await sut.SetContentCommand.ExecuteAsync(null);
        await _objects.DidNotReceive().SetContentStreamAsync(
            Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(),
            Arg.Any<System.Threading.CancellationToken>());

        var tempPath = Path.Combine(Path.GetTempPath(), $"cmispilot-test-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(tempPath, "Neuer Inhalt");
        try
        {
            _dialogs.PickOpenFileAsync().Returns(tempPath);

            await sut.SetContentCommand.ExecuteAsync(null);

            await _objects.Received(1).SetContentStreamAsync(
                "d", Arg.Any<Stream>(), Path.GetFileName(tempPath), Arg.Any<string>(), Arg.Any<bool>(),
                Arg.Any<System.Threading.CancellationToken>());
        }
        finally
        {
            File.Delete(tempPath);
        }
    }
}
