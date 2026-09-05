using CMISPilot.Cmis.Connection;
using CMISPilot.Cmis.Models;
using PortCMIS.Enums;

namespace CMISPilot.Tests.Cmis;

public class ModelMapperTests
{
    [Theory]
    [InlineData(BaseTypeId.CmisDocument, CmisBaseType.Document)]
    [InlineData(BaseTypeId.CmisFolder, CmisBaseType.Folder)]
    [InlineData(BaseTypeId.CmisRelationship, CmisBaseType.Relationship)]
    [InlineData(BaseTypeId.CmisPolicy, CmisBaseType.Policy)]
    [InlineData(BaseTypeId.CmisItem, CmisBaseType.Item)]
    [InlineData(BaseTypeId.CmisSecondary, CmisBaseType.Secondary)]
    public void ToBaseType_MapsAllKnownBaseTypes(BaseTypeId input, CmisBaseType expected)
    {
        Assert.Equal(expected, CmisModelMapper.ToBaseType(input));
    }

    [Fact]
    public void CmisObjectDto_IsFolder_DerivedFromBaseType()
    {
        var folder = new CmisObjectDto { BaseType = CmisBaseType.Folder };
        var doc = new CmisObjectDto { BaseType = CmisBaseType.Document };

        Assert.True(folder.IsFolder);
        Assert.False(folder.IsDocument);
        Assert.True(doc.IsDocument);
        Assert.False(doc.IsFolder);
    }
}
