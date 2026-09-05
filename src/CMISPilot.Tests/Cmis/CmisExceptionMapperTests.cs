using System;
using System.Net.Http;
using CMISPilot.Cmis.Errors;
using CMISPilot.Cmis.Models;
using P = PortCMIS.Exceptions;

namespace CMISPilot.Tests.Cmis;

public class CmisExceptionMapperTests
{
    [Fact]
    public void Map_Unauthorized_YieldsAuthentication()
    {
        var result = CmisExceptionMapper.Map(new P.CmisUnauthorizedException("nope"));
        var app = Assert.IsType<CmisAuthException>(result);
        Assert.Equal(CmisErrorKind.Authentication, app.Kind);
    }

    [Fact]
    public void Map_Connection_YieldsNetwork()
    {
        var result = CmisExceptionMapper.Map(new P.CmisConnectionException("down"));
        Assert.Equal(CmisErrorKind.Network, Assert.IsType<CmisNetworkException>(result).Kind);
    }

    [Fact]
    public void Map_ObjectNotFound_YieldsNotFound()
    {
        var result = CmisExceptionMapper.Map(new P.CmisObjectNotFoundException("gone"));
        Assert.Equal(CmisErrorKind.NotFound, Assert.IsType<CmisNotFoundException>(result).Kind);
    }

    [Fact]
    public void Map_Constraint_YieldsConstraint()
    {
        var result = CmisExceptionMapper.Map(new P.CmisConstraintException("bad"));
        Assert.Equal(CmisErrorKind.Constraint, Assert.IsType<CmisConstraintException>(result).Kind);
    }

    [Fact]
    public void Map_HttpRequestException_YieldsNetwork()
    {
        var result = CmisExceptionMapper.Map(new HttpRequestException("no route"));
        Assert.Equal(CmisErrorKind.Network, Assert.IsType<CmisNetworkException>(result).Kind);
    }

    [Fact]
    public void Map_UnknownException_YieldsServer()
    {
        var result = CmisExceptionMapper.Map(new InvalidOperationException("weird"));
        Assert.Equal(CmisErrorKind.Server, Assert.IsType<CmisServerException>(result).Kind);
    }

    [Fact]
    public void Map_OperationCanceled_IsPassedThrough()
    {
        var original = new OperationCanceledException();
        Assert.Same(original, CmisExceptionMapper.Map(original));
    }

    [Fact]
    public void Map_AlreadyMappedException_IsPassedThrough()
    {
        var original = new CmisAuthException("already");
        Assert.Same(original, CmisExceptionMapper.Map(original));
    }
}
