using CMISPilot.Cmis;
using CMISPilot.Cmis.Contracts;
using CMISPilot.Cmis.Diagnostics;
using CMISPilot.Cmis.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace CMISPilot.Tests.Cmis;

public class ServiceRegistrationTests
{
    [Fact]
    public void AddCmis_RegistersConnectionFundament()
    {
        using var provider = new ServiceCollection().AddCmis().BuildServiceProvider();

        Assert.NotNull(provider.GetService<ICmisExecutor>());
        Assert.NotNull(provider.GetService<IConnectionService>());
        Assert.NotNull(provider.GetService<ISessionContext>());
    }

    [Fact]
    public void AddCmis_SessionContext_IsSingleton()
    {
        using var provider = new ServiceCollection().AddCmis().BuildServiceProvider();

        var first = provider.GetService<ISessionContext>();
        var second = provider.GetService<ISessionContext>();
        Assert.Same(first, second);
    }

    [Fact]
    public void AddCmis_RegistersObjectService()
    {
        // M7: IObjectService (CRUD-Vertikale) muss über AddCmis() registriert sein.
        using var provider = new ServiceCollection().AddCmis().BuildServiceProvider();

        Assert.NotNull(provider.GetService<IObjectService>());
    }

    [Fact]
    public void AddCmis_RegistersDiagnosticsLog_AlsSingleton()
    {
        // M9: IDiagnosticsLog muss über AddCmis() registriert sein und ist
        // Singleton, damit alle Serveroperationen in ein Protokoll landen.
        using var provider = new ServiceCollection().AddCmis().BuildServiceProvider();

        var first = provider.GetService<IDiagnosticsLog>();
        var second = provider.GetService<IDiagnosticsLog>();

        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    [Fact]
    public async System.Threading.Tasks.Task AddCmis_CmisExecutor_ErhaeltDenRegistriertenDiagnosticsLog()
    {
        using var provider = new ServiceCollection().AddCmis().BuildServiceProvider();

        var log = provider.GetRequiredService<IDiagnosticsLog>();
        var executor = provider.GetRequiredService<ICmisExecutor>();

        // Indirekter Nachweis: Nach einer Operation landet ein Eintrag im
        // ueber DI aufgeloesten Log (kein separates No-op-Log).
        await executor.RunAsync(() => 1);

        Assert.NotEmpty(log.GetEntries());
    }
}
