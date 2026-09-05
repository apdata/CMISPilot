using System.Collections.Generic;
using System.Threading.Tasks;
using CMISPilot.Cmis.Connection;
using CMISPilot.Cmis.Errors;
using CMISPilot.Cmis.Execution;
using CMISPilot.Cmis.Objects;

namespace CMISPilot.Tests.Cmis;

/// <summary>
/// Unit-Tests der M7-Vertikale „CRUD" für <see cref="ObjectService"/> (T7.1).
/// Analog zu <see cref="ConnectionServiceTests"/>: deckt ausschließlich die
/// Argument-Validierung (keine Session nötig, keine Server-Tests, Politik M11)
/// ab; die eigentlichen CMIS-Operationen sind über die
/// <c>ExplorerAreaViewModelTests</c> gegen einen gemockten <c>IObjectService</c>
/// abgedeckt (T7.5).
/// </summary>
public class ObjectServiceTests
{
    private static ObjectService CreateSut() => new(new CmisExecutor(), new SessionContext());

    private static IDictionary<string, object?> Props() =>
        new Dictionary<string, object?> { ["cmis:name"] = "Neu" };

    [Fact]
    public async Task CreateFolderAsync_OhneParentId_ThrowsInvalidArgument()
    {
        var sut = CreateSut();
        await Assert.ThrowsAsync<CmisInvalidArgumentException>(() => sut.CreateFolderAsync(string.Empty, Props()));
    }

    [Fact]
    public async Task CreateFolderAsync_OhneProperties_ThrowsInvalidArgument()
    {
        var sut = CreateSut();
        await Assert.ThrowsAsync<CmisInvalidArgumentException>(
            () => sut.CreateFolderAsync("root", new Dictionary<string, object?>()));
    }

    [Fact]
    public async Task CreateDocumentAsync_OhneParentId_ThrowsInvalidArgument()
    {
        var sut = CreateSut();
        await Assert.ThrowsAsync<CmisInvalidArgumentException>(
            () => sut.CreateDocumentAsync(string.Empty, Props()));
    }

    [Fact]
    public async Task UpdatePropertiesAsync_OhneObjectId_ThrowsInvalidArgument()
    {
        var sut = CreateSut();
        await Assert.ThrowsAsync<CmisInvalidArgumentException>(
            () => sut.UpdatePropertiesAsync(string.Empty, Props()));
    }

    [Fact]
    public async Task UpdatePropertiesAsync_OhneProperties_ThrowsInvalidArgument()
    {
        var sut = CreateSut();
        await Assert.ThrowsAsync<CmisInvalidArgumentException>(
            () => sut.UpdatePropertiesAsync("id", new Dictionary<string, object?>()));
    }

    [Fact]
    public async Task DeleteAsync_OhneObjectId_ThrowsInvalidArgument()
    {
        var sut = CreateSut();
        await Assert.ThrowsAsync<CmisInvalidArgumentException>(() => sut.DeleteAsync(string.Empty));
    }

    [Fact]
    public async Task DeleteTreeAsync_OhneFolderId_ThrowsInvalidArgument()
    {
        var sut = CreateSut();
        await Assert.ThrowsAsync<CmisInvalidArgumentException>(() => sut.DeleteTreeAsync(string.Empty));
    }

    [Fact]
    public async Task CreateFolderAsync_OhneSession_ThrowsCmisAppException()
    {
        // Keine aktive Session (SessionContext frisch) -> RequireSession() wirft.
        var sut = CreateSut();
        await Assert.ThrowsAsync<CmisAppException>(() => sut.CreateFolderAsync("root", Props()));
    }
}
