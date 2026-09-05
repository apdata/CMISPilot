using CMISPilot.Cmis.Contracts;
using CMISPilot.Cmis.Models;
using CMISPilot.Cmis.Profiles;
using CMISPilot.ViewModels.Connection;
using CMISPilot.ViewModels.Shell;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CMISPilot.Tests.ViewModels;

/// <summary>
/// VM-Unit-Tests für den Verbinden-Dialog (A3, seit dem Redesign auch die
/// Profilverwaltung, vormals <c>ProfileManagerViewModel</c>): Sichtbarkeits-Flags,
/// <see cref="ConnectDialogViewModel.CanLoadRepositories"/> (indirekt über den
/// Command), das Mapping in <see cref="ConnectDialogViewModel.BuildProfile"/> sowie
/// Profilliste/Neu/Speichern/Duplizieren/Löschen. Politik M11: keine Server-Tests,
/// <see cref="IConnectionService"/>/<see cref="IProfileStore"/>/<see cref="IDialogService"/>
/// gemockt.
/// </summary>
public sealed class ConnectDialogViewModelTests
{
    private static IProfileStore CreateEmptyProfileStore()
    {
        var store = Substitute.For<IProfileStore>();
        store.LoadAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ConnectionProfile>>(Array.Empty<ConnectionProfile>()));
        return store;
    }

    private static ConnectDialogViewModel CreateSut(IProfileStore? profileStore = null, IDialogService? dialogService = null) =>
        new(
            Substitute.For<IConnectionService>(),
            profileStore ?? CreateEmptyProfileStore(),
            dialogService ?? Substitute.For<IDialogService>(),
            NullLogger<ConnectDialogViewModel>.Instance);

    [Fact]
    public void Default_IstBrowserBinding()
    {
        var sut = CreateSut();

        Assert.Equal(CmisBindingType.Browser, sut.BindingType);
        Assert.True(sut.IsBrowserBinding);
        Assert.False(sut.IsAtomPubBinding);
    }

    [Fact]
    public void BindingTypeGewechselt_AktualisiertSichtbarkeitsFlags()
    {
        var sut = CreateSut();

        sut.BindingType = CmisBindingType.AtomPub;

        Assert.False(sut.IsBrowserBinding);
        Assert.True(sut.IsAtomPubBinding);
    }

    [Fact]
    public void CanLoadRepositories_AtomPubMitBrowserUrlAberOhneAtomPubUrl_IstFalse()
    {
        var sut = CreateSut();
        sut.BindingType = CmisBindingType.AtomPub;
        sut.BrowserUrl = "http://host/browser";
        sut.AtomPubUrl = "";

        Assert.False(sut.LoadRepositoriesCommand.CanExecute(null));
    }

    [Fact]
    public void CanLoadRepositories_AtomPubMitAtomPubUrl_IstTrue()
    {
        var sut = CreateSut();
        sut.BindingType = CmisBindingType.AtomPub;
        sut.AtomPubUrl = "http://host/atom11";

        Assert.True(sut.LoadRepositoriesCommand.CanExecute(null));
    }

    [Fact]
    public void BuildProfile_AtomPub_MapptBindingTypeUndAtomPubUrlUndKeinenCsrfHeader()
    {
        var sut = CreateSut();
        sut.BindingType = CmisBindingType.AtomPub;
        sut.AtomPubUrl = " http://host/atom11 ";
        sut.BrowserUrl = "http://host/browser";
        sut.CsrfHeader = "X-CSRF-Token";

        var profile = sut.BuildProfile("pw");

        Assert.Equal(CmisBindingType.AtomPub, profile.BindingType);
        Assert.Equal("http://host/atom11", profile.AtomPubUrl);
        Assert.Null(profile.CsrfHeader);
    }

    [Fact]
    public void BuildProfile_Browser_MapptCsrfHeader()
    {
        var sut = CreateSut();
        sut.BrowserUrl = "http://host/browser";
        sut.CsrfHeader = "X-CSRF-Token";

        var profile = sut.BuildProfile("pw");

        Assert.Equal(CmisBindingType.Browser, profile.BindingType);
        Assert.Equal("X-CSRF-Token", profile.CsrfHeader);
    }

    [Fact]
    public void Default_TimeoutsSindVorbelegt()
    {
        var sut = CreateSut();

        Assert.Equal(ConnectionProfile.DefaultConnectTimeoutMs / 1000, sut.ConnectTimeoutSeconds);
        Assert.Equal(ConnectionProfile.DefaultReadTimeoutMs / 1000, sut.ReadTimeoutSeconds);

        var profile = sut.BuildProfile("geheim");
        Assert.Equal(ConnectionProfile.DefaultConnectTimeoutMs, profile.ConnectTimeoutMs);
        Assert.Equal(ConnectionProfile.DefaultReadTimeoutMs, profile.ReadTimeoutMs);
    }

    [Fact]
    public void BuildProfile_UebernimmtNamenDesGewaehltenRepositories()
    {
        // Der Name steht nur in der Auswahlliste; ohne diese Uebergabe geht er auf dem
        // Weg zur Sitzung verloren (nur die Id wandert ins Profil).
        var sut = CreateSut();
        sut.SelectedRepository = new RepositoryInfoDto { Id = "A1", Name = "Testablage" };

        var profile = sut.BuildProfile("geheim");

        Assert.Equal("A1", profile.RepositoryId);
        Assert.Equal("Testablage", profile.RepositoryName);
    }

    [Fact]
    public void BuildProfile_UebernimmtName()
    {
        var sut = CreateSut();
        sut.Name = "  Testserver  ";

        var profile = sut.BuildProfile("geheim");

        Assert.Equal("Testserver", profile.Name);
    }

    [Fact]
    public void BuildProfile_UebernimmtOAuthFelder()
    {
        var sut = CreateSut();
        sut.OAuthAuthorizationEndpoint = " http://host/oauth2/authorize ";
        sut.OAuthTokenEndpoint = " http://host/oauth2/token ";
        sut.OAuthClientId = " cmispilot ";
        sut.OAuthClientSecret = "geheim";
        sut.OAuthRedirectUri = " http://localhost:51737/auth-done ";

        var profile = sut.BuildProfile("pw");

        Assert.Equal("http://host/oauth2/authorize", profile.OAuthAuthorizationEndpoint);
        Assert.Equal("http://host/oauth2/token", profile.OAuthTokenEndpoint);
        Assert.Equal("cmispilot", profile.OAuthClientId);
        Assert.Equal("geheim", profile.OAuthClientSecret);
        Assert.Equal("http://localhost:51737/auth-done", profile.OAuthRedirectUri);
    }

    [Fact]
    public async Task SelectedListEntry_GespeichertesProfil_OAuthClientSecretFaelltAufGespeichertesZurueck()
    {
        var profile = new ConnectionProfile
        {
            Name = "Testserver",
            BrowserUrl = "http://host",
            Authentication = CmisAuthenticationType.OAuthBearer,
            OAuthClientSecret = "geheimes-secret"
        };
        var store = Substitute.For<IProfileStore>();
        store.LoadAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ConnectionProfile>>(new[] { profile }));

        var sut = CreateSut(profileStore: store);
        await sut.LoadProfilesCommand.ExecuteAsync(null);
        sut.SelectedListEntry = sut.ListEntries.First(e => !e.IsNewEntry);

        Assert.Equal(string.Empty, sut.OAuthClientSecret);
        Assert.True(sut.HasStoredOAuthClientSecret);
        Assert.Equal("geheimes-secret", sut.EffectiveOAuthClientSecret);
        var built = sut.BuildProfile(sut.Password);
        Assert.Equal("geheimes-secret", built.OAuthClientSecret);
    }

    [Fact]
    public void HasStoredOAuthClientSecret_NeuesVerbindungsziel_IstFalse()
    {
        var sut = CreateSut();

        Assert.False(sut.HasStoredOAuthClientSecret);
        Assert.Equal(string.Empty, sut.EffectiveOAuthClientSecret);
    }

    [Fact]
    public void EffectiveOAuthClientSecret_NeuEingetipptesUeberschreibtGespeichertes()
    {
        var sut = CreateSut();
        sut.OAuthClientSecret = "neu-eingetippt";

        Assert.Equal("neu-eingetippt", sut.EffectiveOAuthClientSecret);
    }

    [Fact]
    public void CanLoginWithOAuth_NurMitVollstaendigenAngaben()
    {
        var sut = CreateSut();
        Assert.False(sut.LoginWithOAuthCommand.CanExecute(null));

        sut.OAuthAuthorizationEndpoint = "http://host/authorize";
        sut.OAuthTokenEndpoint = "http://host/token";
        sut.OAuthClientId = "cmispilot";
        sut.OAuthRedirectUri = "http://localhost:51737/auth-done";

        Assert.True(sut.LoginWithOAuthCommand.CanExecute(null));
    }

    [Fact]
    public void CanLoginWithOAuth_ClientCredentials_BrauchtWederAuthorizationEndpointNochRedirectUri()
    {
        var sut = CreateSut();
        sut.OAuthUseClientCredentials = true;

        Assert.False(sut.LoginWithOAuthCommand.CanExecute(null));

        sut.OAuthTokenEndpoint = "http://host/token";
        sut.OAuthClientId = "cmispilot";

        Assert.True(sut.LoginWithOAuthCommand.CanExecute(null));
    }

    [Fact]
    public void BuildProfile_UebernimmtOAuthUseClientCredentials()
    {
        var sut = CreateSut();
        sut.OAuthUseClientCredentials = true;

        var profile = sut.BuildProfile("pw");

        Assert.True(profile.OAuthUseClientCredentials);
    }

    [Fact]
    public void IsOAuthAuthorizationCodeFlow_IstKehrwertVonOAuthUseClientCredentials()
    {
        var sut = CreateSut();
        Assert.True(sut.IsOAuthAuthorizationCodeFlow);

        sut.OAuthUseClientCredentials = true;

        Assert.False(sut.IsOAuthAuthorizationCodeFlow);
    }

    [Fact]
    public void BuildProfile_UebernimmtZusaetzlicheHeaderUndUeberspringtLeereNamen()
    {
        var sut = CreateSut();
        sut.AdditionalHeaders.Add(new AdditionalHeaderRow { Name = " X-Mandant ", Value = "42" });
        sut.AdditionalHeaders.Add(new AdditionalHeaderRow { Name = "", Value = "wird ignoriert" });

        var profile = sut.BuildProfile("geheim");

        var header = Assert.Single(profile.AdditionalHeaders);
        Assert.Equal("X-Mandant", header.Name);
        Assert.Equal("42", header.Value);
    }

    [Fact]
    public void ListEntries_EnthaeltNeuesVerbindungszielAlsErstenEintrag()
    {
        var sut = CreateSut();

        Assert.True(sut.ListEntries[0].IsNewEntry);
        Assert.Same(sut.ListEntries[0], sut.SelectedListEntry);
    }

    [Fact]
    public void NewCommand_SetztFormularAufStandardwerteUndSelektiertNeuesVerbindungsziel()
    {
        var sut = CreateSut();
        sut.Name = "Irrelevant";
        sut.BrowserUrl = "http://host";

        sut.NewCommand.Execute(null);

        Assert.Equal(string.Empty, sut.Name);
        Assert.Equal(string.Empty, sut.BrowserUrl);
        Assert.Equal(CmisBindingType.Browser, sut.BindingType);
        Assert.Equal(CmisAuthenticationType.Standard, sut.Authentication);
        Assert.True(sut.SelectedListEntry?.IsNewEntry);
    }

    [Fact]
    public async Task SelectedListEntry_GespeichertesProfil_UebernimmtFelderUndLeertPasswort()
    {
        var profile = new ConnectionProfile
        {
            Name = "Testserver",
            BindingType = CmisBindingType.AtomPub,
            AtomPubUrl = "http://host/atom11",
            Authentication = CmisAuthenticationType.OAuthBearer,
            Password = "geheim",
            BearerToken = "token",
            RepositoryId = "A1",
            RepositoryName = "Testablage"
        };
        var store = Substitute.For<IProfileStore>();
        store.LoadAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ConnectionProfile>>(new[] { profile }));

        var sut = CreateSut(profileStore: store);
        await sut.LoadProfilesCommand.ExecuteAsync(null);

        sut.SelectedListEntry = sut.ListEntries.First(e => !e.IsNewEntry);

        Assert.Equal("Testserver", sut.Name);
        Assert.Equal(CmisBindingType.AtomPub, sut.BindingType);
        Assert.Equal("http://host/atom11", sut.AtomPubUrl);
        Assert.Equal(CmisAuthenticationType.OAuthBearer, sut.Authentication);
        Assert.Equal(string.Empty, sut.Password);
        Assert.Equal(string.Empty, sut.BearerToken);
        Assert.True(sut.SavePassword);
        Assert.Equal("A1", sut.SelectedRepository?.Id);
    }

    [Fact]
    public async Task BuildProfile_NachProfilwahlOhneRetippen_VerwendetGespeichertesPasswortUndToken()
    {
        // Regressionstest: Password/BearerToken werden nach einem Profilwechsel im
        // Formular geleert (Sicherheitsverhalten), duerfen aber nicht verloren gehen,
        // solange der Nutzer nichts Neues eintippt - sonst schlaegt "Verbinden" (und ein
        // erneutes "Speichern") mit einem geladenen Profil fehl.
        var profile = new ConnectionProfile
        {
            Name = "Testserver",
            BrowserUrl = "http://host",
            Authentication = CmisAuthenticationType.OAuthBearer,
            Password = "geheim",
            BearerToken = "token"
        };
        var store = Substitute.For<IProfileStore>();
        store.LoadAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ConnectionProfile>>(new[] { profile }));

        var sut = CreateSut(profileStore: store);
        await sut.LoadProfilesCommand.ExecuteAsync(null);
        sut.SelectedListEntry = sut.ListEntries.First(e => !e.IsNewEntry);

        // Genau das, was ConnectDialog.OnConnectClick tut: das (leere) PasswordBox-
        // Passwort uebergeben, ohne dass der Nutzer etwas eingetippt hat.
        var built = sut.BuildProfile(sut.Password);

        Assert.Equal("geheim", built.Password);
        Assert.Equal("token", built.BearerToken);
    }

    [Fact]
    public async Task SaveAsync_LeererName_SetztErrorMessage()
    {
        var store = CreateEmptyProfileStore();
        var sut = CreateSut(profileStore: store);
        sut.Name = "   ";

        await sut.SaveCommand.ExecuteAsync(null);

        Assert.False(string.IsNullOrEmpty(sut.ErrorMessage));
        await store.DidNotReceive().SaveAsync(Arg.Any<ConnectionProfile>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_GueltigesProfil_RuftProfileStoreSaveAsyncAuf()
    {
        var store = CreateEmptyProfileStore();
        var sut = CreateSut(profileStore: store);
        sut.Name = "Neues Ziel";
        sut.BrowserUrl = "http://host";
        sut.SavePassword = true;

        await sut.SaveCommand.ExecuteAsync(null);

        await store.Received(1).SaveAsync(
            Arg.Is<ConnectionProfile>(p => p.Name == "Neues Ziel" && p.BrowserUrl == "http://host"),
            true,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void DuplicateCommand_CanExecute_NurBeiEchtemProfil()
    {
        var sut = CreateSut();

        Assert.False(sut.DuplicateCommand.CanExecute(null));
    }

    [Fact]
    public async Task DeleteAsync_BestaetigtUeberDialogService_RuftProfileStoreDeleteAsyncAuf()
    {
        var profile = new ConnectionProfile { Name = "Testserver", BrowserUrl = "http://host" };
        var store = Substitute.For<IProfileStore>();
        store.LoadAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ConnectionProfile>>(new[] { profile }));
        var dialogService = Substitute.For<IDialogService>();
        dialogService.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.FromResult(true));

        var sut = CreateSut(profileStore: store, dialogService: dialogService);
        await sut.LoadProfilesCommand.ExecuteAsync(null);
        sut.SelectedListEntry = sut.ListEntries.First(e => !e.IsNewEntry);

        await sut.DeleteCommand.ExecuteAsync(null);

        await store.Received(1).DeleteAsync("Testserver", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_Abgebrochen_LoeschtNicht()
    {
        var profile = new ConnectionProfile { Name = "Testserver", BrowserUrl = "http://host" };
        var store = Substitute.For<IProfileStore>();
        store.LoadAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ConnectionProfile>>(new[] { profile }));
        var dialogService = Substitute.For<IDialogService>();
        dialogService.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.FromResult(false));

        var sut = CreateSut(profileStore: store, dialogService: dialogService);
        await sut.LoadProfilesCommand.ExecuteAsync(null);
        sut.SelectedListEntry = sut.ListEntries.First(e => !e.IsNewEntry);

        await sut.DeleteCommand.ExecuteAsync(null);

        await store.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
