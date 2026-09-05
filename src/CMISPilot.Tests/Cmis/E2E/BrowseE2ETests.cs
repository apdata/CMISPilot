using System.Linq;
using System.Threading.Tasks;
using CMISPilot.Cmis.Browse;
using CMISPilot.Cmis.Errors;
using CMISPilot.Cmis.Models;

namespace CMISPilot.Tests.Cmis.E2E;

/// <summary>
/// T11.1 – Explorer/Browse gegen A1 (<see cref="BrowseService"/>): Wurzelordner
/// laden, dessen Children auflisten, ein Objekt inspizieren (Properties).
/// </summary>
[Trait("Category", "Integration")]
public class BrowseE2ETests
{
    [Fact]
    public async Task Browse_RootChildrenAndObjectInspection()
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

            var root = await browse.GetRootFolderAsync();
            Assert.Equal(CmisBaseType.Folder, root.BaseType);
            Assert.False(string.IsNullOrEmpty(root.Id));

            var children = await browse.GetChildrenAsync(root.Id);
            Assert.NotNull(children);
            // Der InMemory-Server liefert im Root vordefinierte Beispieldaten.
            Assert.NotEmpty(children);

            // Ein Objekt genauer inspizieren: erneut per Id laden und Properties prüfen.
            var first = children.First();
            var obj = await browse.GetObjectAsync(first.Id);
            Assert.Equal(first.Id, obj.Id);
            Assert.NotEmpty(obj.Properties);
            Assert.Contains(obj.Properties, p => p.Id == "cmis:objectId");
            Assert.Contains(obj.Properties, p => p.Id == "cmis:name");
        }
    }
}
