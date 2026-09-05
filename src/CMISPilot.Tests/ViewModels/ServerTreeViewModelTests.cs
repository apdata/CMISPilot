using System.Collections.Generic;
using System.Threading;
using CMISPilot.Cmis.Contracts;
using CMISPilot.Cmis.Models;
using CMISPilot.ViewModels.Explorer;
using CMISPilot.ViewModels.Messages;
using CommunityToolkit.Mvvm.Messaging;
using NSubstitute;

namespace CMISPilot.Tests.ViewModels;

/// <summary>
/// VM-Unit-Tests für den Server-Baum (R4 Etappe 2): Aufbau Server → Repository →
/// Wurzelordner beim Verbinden, Lazy-Load der Unterordner (analog M4-Referenzmuster
/// <c>FolderNodeViewModel</c>) und Knotenauswahl per <see cref="NodeSelectedMessage"/>.
/// Ausschließlich gegen gemockte <see cref="IBrowseService"/>/<see cref="ISessionContext"/>
/// (Politik M11: keine Server-Tests), deterministisch ohne WPF.
/// </summary>
public sealed class ServerTreeViewModelTests
{
    private readonly IConnectionService _connection = Substitute.For<IConnectionService>();
    private readonly IBrowseService _browse = Substitute.For<IBrowseService>();
    private readonly ISessionContext _session = Substitute.For<ISessionContext>();
    private readonly IMessenger _messenger = new WeakReferenceMessenger();

    private ServerTreeViewModel CreateSut() => new(_connection, _browse, _session, _messenger);

    private static CmisObjectDto Folder(string id, string? name = null) => new()
    {
        Id = id,
        Name = name ?? id,
        BaseType = CmisBaseType.Folder,
        TypeId = "cmis:folder"
    };

    private static CmisObjectDto Doc(string id, string? name = null) => new()
    {
        Id = id,
        Name = name ?? id,
        BaseType = CmisBaseType.Document,
        TypeId = "cmis:document"
    };

    private static ConnectionProfile Profile(string url = "http://server/browser") => new()
    {
        BrowserUrl = url
    };

    private static RepositoryInfoDto Repo(string id = "A1", string? name = "InMemory") => new()
    {
        Id = id,
        Name = name
    };

    [Fact]
    public void OhneVerbindung_BaumBleibtLeer()
    {
        _session.IsConnected.Returns(false);

        var sut = CreateSut();

        Assert.Empty(sut.RootNodes);
    }

    [Fact]
    public void BeiVerbindung_BautServerRepositoryUndWurzelordner()
    {
        _session.IsConnected.Returns(true);
        _session.CurrentProfile.Returns(Profile());
        _session.CurrentRepository.Returns(Repo());
        _browse.GetRootFolderAsync(Arg.Any<CancellationToken>()).Returns(Folder("root", "Root"));

        var sut = CreateSut();

        Assert.Single(sut.RootNodes);
        var serverNode = sut.RootNodes[0];
        Assert.Equal(TreeNodeKind.Server, serverNode.Kind);
        Assert.Equal("http://server/browser", serverNode.Name);

        Assert.Single(serverNode.Children);
        var repoNode = serverNode.Children[0];
        Assert.Equal(TreeNodeKind.Repository, repoNode.Kind);
        Assert.Equal("InMemory", repoNode.Name);

        Assert.Single(repoNode.Children);
        var rootFolderNode = repoNode.Children[0];
        Assert.Equal(TreeNodeKind.Folder, rootFolderNode.Kind);
        Assert.Equal("Root", rootFolderNode.Name);
        Assert.Equal("root", rootFolderNode.ObjectId);
    }

    [Fact]
    public void BeiVerbindung_WurzelordnerWirdAufgeklapptUndLaedtNurOrdner()
    {
        _session.IsConnected.Returns(true);
        _session.CurrentProfile.Returns(Profile());
        _session.CurrentRepository.Returns(Repo());
        _browse.GetRootFolderAsync(Arg.Any<CancellationToken>()).Returns(Folder("root", "Root"));
        _browse.GetChildrenAsync("root", Arg.Any<CancellationToken>())
            .Returns(new List<CmisObjectDto> { Folder("f1", "Unterordner"), Doc("d1", "Datei") });

        var sut = CreateSut();
        var rootFolderNode = sut.RootNodes[0].Children[0].Children[0];

        // Der Wurzelordner wird direkt beim Verbinden aufgeklappt und laedt seine
        // Unterordner (nur Ordner, keine Dokumente), damit die Verzeichnisse sofort
        // sichtbar sind (die mit vollstaendigen Tasks gemockten Aufrufe laufen dabei
        // synchron durch).
        Assert.True(rootFolderNode.IsExpanded);
        Assert.True(rootFolderNode.AreChildrenLoaded);
        Assert.Single(rootFolderNode.Children);
        Assert.Equal("Unterordner", rootFolderNode.Children[0].Name);
    }

    [Fact]
    public void Trennen_LeertDenBaum()
    {
        _session.IsConnected.Returns(true);
        _session.CurrentProfile.Returns(Profile());
        _session.CurrentRepository.Returns(Repo());
        _browse.GetRootFolderAsync(Arg.Any<CancellationToken>()).Returns(Folder("root", "Root"));

        var sut = CreateSut();
        Assert.NotEmpty(sut.RootNodes);

        _session.IsConnected.Returns(false);
        _session.ConnectionChanged += Raise.Event<System.EventHandler>(_session, System.EventArgs.Empty);

        Assert.Empty(sut.RootNodes);
        Assert.Null(sut.SelectedNode);
    }

    [Fact]
    public void Selektion_SendetNodeSelectedMessageMitCmisObjekt()
    {
        _session.IsConnected.Returns(true);
        _session.CurrentProfile.Returns(Profile());
        _session.CurrentRepository.Returns(Repo());
        var root = Folder("root", "Root");
        _browse.GetRootFolderAsync(Arg.Any<CancellationToken>()).Returns(root);

        var sut = CreateSut();
        var rootFolderNode = sut.RootNodes[0].Children[0].Children[0];

        CmisObjectDto? received = null;
        var gotMessage = false;
        _messenger.Register<NodeSelectedMessage>(this, (_, m) =>
        {
            gotMessage = true;
            received = m.CmisObject;
        });

        sut.SelectedNode = rootFolderNode;

        Assert.True(gotMessage);
        Assert.Same(root, received);
    }

    [Fact]
    public void LeererRepositoryName_wirdZurId()
    {
        // Ein leerer repositoryName vom Server wuerde als Knoten ohne Beschriftung
        // durchgehen - der Nutzer saehe nur das Symbol.
        _session.IsConnected.Returns(true);
        _session.CurrentProfile.Returns(Profile());
        _session.CurrentRepository.Returns(Repo(name: "  "));
        _browse.GetRootFolderAsync(Arg.Any<CancellationToken>()).Returns(Folder("root", "Root"));

        var sut = CreateSut();

        Assert.Equal("A1", sut.RootNodes[0].Children[0].Name);
    }

    [Fact]
    public void AtomPubProfil_ZeigtDieAtomPubUrlAlsServerknoten()
    {
        // BrowserUrl ist bei einem AtomPub-Profil leer, nicht null - ein blosses ??
        // liefert dann eine leere Beschriftung statt der tatsaechlichen Adresse.
        _session.IsConnected.Returns(true);
        _session.CurrentProfile.Returns(new ConnectionProfile
        {
            BindingType = CmisBindingType.AtomPub,
            BrowserUrl = string.Empty,
            AtomPubUrl = "http://server/atom11"
        });
        _session.CurrentRepository.Returns(Repo());
        _browse.GetRootFolderAsync(Arg.Any<CancellationToken>()).Returns(Folder("root", "Root"));

        var sut = CreateSut();

        Assert.Equal("http://server/atom11", sut.RootNodes[0].Name);
    }
}
