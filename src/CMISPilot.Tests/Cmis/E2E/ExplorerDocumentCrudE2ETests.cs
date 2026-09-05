using IFileLauncher = APX.Wpf.Shell.ViewModels.Contracts.IFileLauncher;
﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CMISPilot.Cmis.Browse;
using CMISPilot.Cmis.Errors;
using CMISPilot.Cmis.Objects;
using CMISPilot.Cmis.Types;
using CMISPilot.ViewModels.Dialogs;
using CMISPilot.ViewModels.Explorer;
using CMISPilot.ViewModels.Export;
using CMISPilot.ViewModels.Shell;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CMISPilot.Tests.Cmis.E2E;

/// <summary>
/// R4 Etappe 4 – E2E: treibt die CRUD-Kommandos von <see cref="ExplorerDocumentViewModel"/>
/// real gegen den A1-Testserver. Legt per <see cref="ExplorerDocumentViewModel.NewFolderCommand"/>
/// einen Ordner im Wurzelordner an, prüft, dass er nach dem automatischen Reload in der
/// Objektliste erscheint, und löscht ihn anschließend wieder per
/// <see cref="ExplorerDocumentViewModel.DeleteCommand"/> (Aufräumen). Soft-Skip, wenn der
/// Server nicht erreichbar ist. Der Bearbeiten-Dialog wird über einen gemockten
/// <see cref="IDialogService"/> simuliert (kein UI im Test, Politik M11).
/// </summary>
[Trait("Category", "Integration")]
public class ExplorerDocumentCrudE2ETests
{
    [Fact]
    public async Task NewFolder_LegtOrdnerAnUndListeReloaded_Delete_RaeumtAuf()
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
            var folderName = "cmispilot-e2e-explorer-" + Guid.NewGuid().ToString("N")[..8];

            var documentVm = new ExplorerDocumentViewModel(
                root, browse, objects, types, dialogs, launcher, messenger,
                NullLogger<ExplorerDocumentViewModel>.Instance,
                new ClosedXmlListExporter());
            await WaitAsync(() => !documentVm.IsBusy);

            // "Neuer Ordner": der Anlegen-Dialog wird simuliert, indem der gemockte
            // IDialogService den Namen ins Dialog-VM schreibt (wie im echten Dialog nach
            // Benutzereingabe) und dann "Speichern" (true) liefert.
            dialogs.ShowEditPropertiesAsync(Arg.Any<EditPropertiesViewModel>())
                .Returns(callInfo =>
                {
                    var vm = callInfo.Arg<EditPropertiesViewModel>();
                    vm.Properties[0].Value = folderName;
                    return Task.FromResult(true);
                });

            Assert.True(documentVm.NewFolderCommand.CanExecute(null));
            await documentVm.NewFolderCommand.ExecuteAsync(null);

            // Reload nach dem Anlegen: der neue Ordner erscheint in der Objektliste.
            await WaitAsync(() => documentVm.Objects.Any(o => o.Name == folderName));
            var created = documentVm.Objects.FirstOrDefault(o => o.Name == folderName);
            Assert.NotNull(created);

            try
            {
                // "Löschen": Rückfrage wird über den gemockten IDialogService bestätigt.
                documentVm.SelectedObject = created;
                dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

                Assert.True(documentVm.DeleteCommand.CanExecute(null));
                await documentVm.DeleteCommand.ExecuteAsync(null);

                await WaitAsync(() => documentVm.Objects.All(o => o.Name != folderName));
                Assert.DoesNotContain(documentVm.Objects, o => o.Name == folderName);
            }
            finally
            {
                // Best-effort-Aufräumen, falls der Löschen-Schritt oben fehlschlug.
                if (created is not null)
                {
                    try { await objects.DeleteTreeAsync(created.Id); } catch { /* best effort */ }
                }
            }
        }
    }

    /// <summary>Pollt bis zur Bedingung oder Timeout (Laden läuft asynchron/fire-and-forget).</summary>
    private static async Task WaitAsync(Func<bool> condition, int timeoutMs = 8000)
    {
        var sw = Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < timeoutMs)
        {
            await Task.Delay(50);
        }
    }
}
