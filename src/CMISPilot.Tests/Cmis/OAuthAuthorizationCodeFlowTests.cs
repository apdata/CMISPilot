using CMISPilot.Cmis.Connection;

namespace CMISPilot.Tests.Cmis;

/// <summary>
/// Unit-Tests für <see cref="OAuthAuthorizationCodeFlow"/>: nur die Redirect-URI-
/// Validierung, die vor jedem I/O (Listener, Browser, HTTP) prüft und daher ohne
/// echten Identity Provider testbar ist. Der eigentliche Flow (Browser-Login,
/// Listener, Token-Tausch) braucht einen echten OAuth-Server und wird manuell gegen
/// das Testsystem verifiziert, nicht hier.
/// </summary>
public sealed class OAuthAuthorizationCodeFlowTests
{
    private readonly OAuthAuthorizationCodeFlow _sut = new();

    [Theory]
    [InlineData("https://localhost/nuxeo-auth-done")]
    [InlineData("http://example.com:5000/callback")]
    [InlineData("not-a-url")]
    [InlineData("")]
    public async Task GetAccessTokenAsync_UngueltigeRedirectUri_WirftSofort(string redirectUri)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.GetAccessTokenAsync("http://host/authorize", "http://host/token", "client", "secret", redirectUri));
    }
}
