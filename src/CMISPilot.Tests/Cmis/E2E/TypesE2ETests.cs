using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CMISPilot.Cmis.Errors;
using CMISPilot.Cmis.Models;
using CMISPilot.Cmis.Types;

namespace CMISPilot.Tests.Cmis.E2E;

/// <summary>
/// T11.2 – Typen gegen A1 (<see cref="TypeService"/>): gesamten Typbaum laden,
/// Basistypen (cmis:document / cmis:folder) und deren Property-Definitionen prüfen.
/// </summary>
[Trait("Category", "Integration")]
public class TypesE2ETests
{
    [Fact]
    public async Task Types_LoadTreeAndInspectBaseTypes()
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
            var types = new TypeService(s.Executor, s.Context);

            var tree = await types.GetTypeTreeAsync(includePropertyDefinitions: true);
            Assert.NotEmpty(tree);

            // Basistypen einsammeln (rekursiv, da Baumstruktur).
            var all = Flatten(tree).ToList();
            Assert.Contains(all, t => t.Id == "cmis:document");
            Assert.Contains(all, t => t.Id == "cmis:folder");

            var document = all.First(t => t.Id == "cmis:document");
            Assert.Equal(CmisBaseType.Document, document.BaseType);
            Assert.NotEmpty(document.PropertyDefinitions);
            Assert.Contains(document.PropertyDefinitions, p => p.Id == "cmis:name");
            Assert.Contains(document.PropertyDefinitions, p => p.Id == "cmis:objectTypeId");

            var folder = all.First(t => t.Id == "cmis:folder");
            Assert.Equal(CmisBaseType.Folder, folder.BaseType);

            // Einzelne Typdefinition direkt laden.
            var single = await types.GetTypeDefinitionAsync("cmis:folder");
            Assert.Equal("cmis:folder", single.Id);
            Assert.NotEmpty(single.PropertyDefinitions);
        }
    }

    private static IEnumerable<TypeDefinitionDto> Flatten(IEnumerable<TypeDefinitionDto> types)
    {
        foreach (var t in types)
        {
            yield return t;
            foreach (var c in Flatten(t.Children))
            {
                yield return c;
            }
        }
    }
}
