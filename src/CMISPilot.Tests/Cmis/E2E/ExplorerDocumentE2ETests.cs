using IFileLauncher = APX.Wpf.Shell.ViewModels.Contracts.IFileLauncher;
﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CMISPilot.Cmis.Browse;
using CMISPilot.Cmis.Errors;
using CMISPilot.Cmis.Objects;
using CMISPilot.Cmis.Types;
using CMISPilot.ViewModels.Explorer;
using CMISPilot.ViewModels.Export;
using CMISPilot.ViewModels.Messages;
using CMISPilot.ViewModels.Shell;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CMISPilot.Tests.Cmis.E2E;

/// <summary>
/// R4 Etappe 3 – E2E: treibt <see cref="ExplorerDocumentViewModel"/> und
/// <see cref="PropertiesViewModel"/> real gegen den A1-Testserver. Verifiziert, dass
/// der Explorer-Dokument-Tab die Objektliste des Wurzelordners lädt und dass das
/// Eigenschaften-Werkzeugfenster bei der Listen-Selektion Datentyp/Pflichtfeld aus der
/// echten Typdefinition nachlädt. Soft-Skip, wenn der Server nicht erreichbar ist.
/// </summary>
[Trait("Category", "Integration")]
public class ExplorerDocumentE2ETests
{
    [Fact]
    public async Task ExplorerDocument_LaedtObjektlisteDesWurzelordnersUndEigenschaftenFolgenDerAuswahl()
    {
        if (!E2EServer.Reachable()) return;

        E2ESession s;
        try
        {
            s = await E2EServer.ConnectAsync();
        }
        catch (CmisAppException)
        {
            return;
        }

        await using (s)
        {
            var browse = new BrowseService(s.Executor, s.Context);
            var objects = new ObjectService(s.Executor, s.Context);
            var types = new TypeService(s.Executor, s.Context);
            var dialogs = Substitute.For<IDialogService>();
            var launcher = Substitute.For<IFileLauncher>();
            var messenger = new WeakReferenceMessenger();

            var root = await browse.GetRootFolderAsync();

            // Explorer-Dokument-Tab: laedt beim Erzeugen die Objektliste des Ordners
            // (der InMemory-Server liefert im Root vordefinierte Beispieldaten).
            var documentVm = new ExplorerDocumentViewModel(
                root, browse, objects, types, dialogs, launcher, messenger,
                NullLogger<ExplorerDocumentViewModel>.Instance,
                new ClosedXmlListExporter());
            await WaitAsync(() => documentVm.Objects.Count > 0);
            Assert.NotEmpty(documentVm.Objects);
            Assert.Equal("explorer", documentVm.ContentId);
            Assert.Equal("explorer", documentVm.ContextTabKey);

            // Eigenschaften-Werkzeugfenster: registriert sich selbst als Empfaenger;
            // die Listen-Selektion im Dokument-Tab loest NodeSelectedMessage aus.
            var propertiesVm = new PropertiesViewModel(
                types, messenger, dialogs, new ClosedXmlListExporter(),
                NullLogger<PropertiesViewModel>.Instance);

            var target = documentVm.Objects.First();
            documentVm.SelectedObject = target;

            await WaitAsync(() => propertiesVm.Properties.Count > 0);
            Assert.NotEmpty(propertiesVm.Properties);

            // "cmis:name" ist bei jedem Objekttyp definiert (Pflichtfeld, String).
            var nameRow = propertiesVm.Properties.First(p => p.PropertyId == "cmis:name");
            Assert.Equal("String", nameRow.DataType);
            Assert.True(nameRow.IsRequired);
            Assert.False(string.IsNullOrEmpty(nameRow.Value));
        }
    }

    /// <summary>Pollt bis zur Bedingung oder Timeout (Laden läuft asynchron/fire-and-forget).</summary>
    private static async Task WaitAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var sw = Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < timeoutMs)
        {
            await Task.Delay(50);
        }
    }
}
