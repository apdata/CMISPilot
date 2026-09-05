using System.Net.Http;
using CMISPilot.Cmis.Connection;
using CMISPilot.Cmis.Models;

namespace CMISPilot.Tests.Cmis;

/// <summary>
/// Unit-Tests für <see cref="HeaderInjectingAuthenticationProvider"/>: fügt die
/// konfigurierten Header in eine <see cref="HttpRequestMessage"/> ein, ohne dass
/// dafür eine echte PortCMIS-<c>IBindingSession</c> nötig ist (die geerbte
/// <see cref="PortCMIS.Binding.StandardAuthenticationProvider.PrepareHttpRequestMessage"/>
/// greift nur auf Felder zu, die erst <c>PrepareHttpClientHandler</c> setzt).
/// </summary>
public sealed class HeaderInjectingAuthenticationProviderTests
{
    [Fact]
    public void PrepareHttpRequestMessage_FuegtKonfigurierteHeaderEin()
    {
        var sut = new HeaderInjectingAuthenticationProvider(
            new[] { new HttpHeaderEntry("X-Mandant", "42"), new HttpHeaderEntry("X-Gateway-Key", "abc") });
        var request = new HttpRequestMessage(HttpMethod.Get, "http://host/browser");

        sut.PrepareHttpRequestMessage(request);

        Assert.Equal("42", Assert.Single(request.Headers.GetValues("X-Mandant")));
        Assert.Equal("abc", Assert.Single(request.Headers.GetValues("X-Gateway-Key")));
    }

    [Fact]
    public void PrepareHttpRequestMessage_OhneHeader_AendertNichts()
    {
        var sut = new HeaderInjectingAuthenticationProvider(Array.Empty<HttpHeaderEntry>());
        var request = new HttpRequestMessage(HttpMethod.Get, "http://host/browser");

        sut.PrepareHttpRequestMessage(request);

        Assert.Empty(request.Headers);
    }

    [Fact]
    public void PrepareHttpRequestMessage_HeaderMitLeeremNamen_WirdUebersprungen()
    {
        var sut = new HeaderInjectingAuthenticationProvider(new[] { new HttpHeaderEntry("", "wert") });
        var request = new HttpRequestMessage(HttpMethod.Get, "http://host/browser");

        sut.PrepareHttpRequestMessage(request);

        Assert.Empty(request.Headers);
    }
}
