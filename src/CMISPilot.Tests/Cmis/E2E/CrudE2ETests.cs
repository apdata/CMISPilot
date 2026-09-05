using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CMISPilot.Cmis.Browse;
using CMISPilot.Cmis.Errors;
using CMISPilot.Cmis.Models;
using CMISPilot.Cmis.Objects;

namespace CMISPilot.Tests.Cmis.E2E;

/// <summary>
/// T11.4 (CRUD) + T11.5 (Dokumentinhalte) gegen A1 (<see cref="ObjectService"/>):
/// im Root einen Test-Ordner anlegen, Property updaten (umbenennen), darin ein
/// Dokument mit Inhalt anlegen, den Inhalt wieder laden und byteweise vergleichen,
/// dann per deleteTree aufräumen und die Löschung verifizieren. Best-effort-Teardown
/// auch bei Teilfehler (der InMemory-Server ist ohnehin flüchtig).
/// </summary>
[Trait("Category", "Integration")]
public class CrudE2ETests
{
    [Fact]
    public async Task Crud_CreateUpdate_Document_Content_DeleteTree()
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

            var root = await browse.GetRootFolderAsync();
            var folderName = "cmispilot-e2e-" + Guid.NewGuid().ToString("N")[..8];

            // FA-70: Ordner anlegen
            var folder = await objects.CreateFolderAsync(root.Id, new Dictionary<string, object?>
            {
                ["cmis:name"] = folderName,
                ["cmis:objectTypeId"] = "cmis:folder"
            });
            Assert.False(string.IsNullOrEmpty(folder.Id));

            var deleted = false;
            try
            {
                // FA-72: Property updaten (umbenennen)
                var newName = folderName + "-renamed";
                var updated = await objects.UpdatePropertiesAsync(folder.Id,
                    new Dictionary<string, object?> { ["cmis:name"] = newName });
                Assert.Contains(updated.Properties,
                    p => p.Id == "cmis:name" && string.Equals(p.Value?.ToString(), newName, StringComparison.Ordinal));

                // FA-71/73: Dokument mit Inhalt anlegen
                var docName = "doc-" + Guid.NewGuid().ToString("N")[..8] + ".txt";
                var payload = Encoding.UTF8.GetBytes("Hallo CMISPilot E2E – " + Guid.NewGuid());
                CmisObjectDto doc;
                using (var ms = new MemoryStream(payload))
                {
                    doc = await objects.CreateDocumentAsync(folder.Id, new Dictionary<string, object?>
                    {
                        ["cmis:name"] = docName,
                        ["cmis:objectTypeId"] = "cmis:document"
                    }, ms, docName, "text/plain");
                }
                Assert.False(string.IsNullOrEmpty(doc.Id));

                // T11.5 / FA-40: Content wieder laden und byteweise vergleichen
                using (var content = await objects.GetContentStreamAsync(doc.Id))
                {
                    using var read = new MemoryStream();
                    await content.Stream.CopyToAsync(read);
                    Assert.Equal(payload, read.ToArray());
                }

                // FA-74: deleteTree (nicht-leerer Ordner)
                await objects.DeleteTreeAsync(folder.Id);
                deleted = true;

                // Verifizieren: Ordner ist weg
                await Assert.ThrowsAnyAsync<CmisAppException>(() => browse.GetObjectAsync(folder.Id));
            }
            finally
            {
                if (!deleted)
                {
                    try { await objects.DeleteTreeAsync(folder.Id); } catch { /* best effort */ }
                }
            }
        }
    }
}
