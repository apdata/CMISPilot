using CMISPilot.Cmis.Contracts;
using CMISPilot.Cmis.Models;
using CMISPilot.ViewModels.Connection;
using NSubstitute;

namespace CMISPilot.Tests.ViewModels;

/// <summary>
/// Unit-Tests für <see cref="ConnectionStatusViewModel"/> (R4 Etappe 2): spiegelt
/// den <see cref="ISessionContext"/> beim Erzeugen und bei jedem
/// <see cref="ISessionContext.ConnectionChanged"/>-Ereignis. Gemockter
/// <see cref="ISessionContext"/> (NSubstitute), deterministisch ohne Server.
/// </summary>
public sealed class ConnectionStatusViewModelTests
{
    private readonly ISessionContext _session = Substitute.For<ISessionContext>();

    [Fact]
    public void Initialisierung_ohne_Verbindung_zeigt_getrennten_Zustand()
    {
        _session.IsConnected.Returns(false);
        _session.CurrentRepository.Returns((RepositoryInfoDto?)null);

        var sut = new ConnectionStatusViewModel(_session);

        Assert.False(sut.IsConnected);
        Assert.Equal("Nicht verbunden", sut.StatusText);
        Assert.Equal("-", sut.RepositoryName);
    }

    [Fact]
    public void ConnectionChanged_aktualisiert_Zustand_und_Repository()
    {
        _session.IsConnected.Returns(false);
        var sut = new ConnectionStatusViewModel(_session);

        _session.IsConnected.Returns(true);
        _session.CurrentRepository.Returns(new RepositoryInfoDto { Id = "A1", Name = "InMemory" });
        _session.ConnectionChanged += Raise.Event<System.EventHandler>(_session, System.EventArgs.Empty);

        Assert.True(sut.IsConnected);
        Assert.Equal("Verbunden", sut.StatusText);
        Assert.Equal("InMemory", sut.RepositoryName);
    }

    [Fact]
    public void ConnectionChanged_beim_Trennen_setzt_Repository_auf_Platzhalter()
    {
        _session.IsConnected.Returns(true);
        _session.CurrentRepository.Returns(new RepositoryInfoDto { Id = "A1", Name = "InMemory" });
        var sut = new ConnectionStatusViewModel(_session);
        Assert.True(sut.IsConnected);

        _session.IsConnected.Returns(false);
        _session.CurrentRepository.Returns((RepositoryInfoDto?)null);
        _session.ConnectionChanged += Raise.Event<System.EventHandler>(_session, System.EventArgs.Empty);

        Assert.False(sut.IsConnected);
        Assert.Equal("Nicht verbunden", sut.StatusText);
        Assert.Equal("-", sut.RepositoryName);
    }

    [Fact]
    public void LeererRepositoryName_faellt_auf_die_Id_zurueck()
    {
        // Manche Server liefern bei getRepositoryInfo einen leeren repositoryName.
        // Ein blosses ?? faengt das nicht ab und die Statusleiste bliebe leer.
        _session.IsConnected.Returns(true);
        _session.CurrentRepository.Returns(new RepositoryInfoDto { Id = "A1", Name = "   " });

        var sut = new ConnectionStatusViewModel(_session);

        Assert.Equal("A1", sut.RepositoryName);
    }

    [Fact]
    public void BeginBusy_setzt_IsBusy_und_Dispose_setzt_zurueck()
    {
        var sut = new ConnectionStatusViewModel(_session);
        Assert.False(sut.IsBusy);

        var scope = sut.BeginBusy();
        Assert.True(sut.IsBusy);

        scope.Dispose();
        Assert.False(sut.IsBusy);
    }

    [Fact]
    public void BeginBusy_geschachtelt_bleibt_busy_bis_beide_Scopes_freigegeben_sind()
    {
        var sut = new ConnectionStatusViewModel(_session);

        var outer = sut.BeginBusy();
        var inner = sut.BeginBusy();
        Assert.True(sut.IsBusy);

        inner.Dispose();
        Assert.True(sut.IsBusy);

        outer.Dispose();
        Assert.False(sut.IsBusy);
    }

    [Fact]
    public void Dispose_ist_idempotent()
    {
        var sut = new ConnectionStatusViewModel(_session);

        var scope = sut.BeginBusy();
        scope.Dispose();
        scope.Dispose();

        Assert.False(sut.IsBusy);
    }
}
