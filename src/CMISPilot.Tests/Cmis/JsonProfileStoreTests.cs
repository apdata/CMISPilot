using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CMISPilot.Cmis.Models;
using CMISPilot.Cmis.Profiles;

namespace CMISPilot.Tests.Cmis;

/// <summary>
/// Unit-Tests für <see cref="JsonProfileStore"/> (M10, T10.4, FA-04/NFA-07).
/// Läuft ohne Windows/DPAPI: die Datei liegt in einem temporären Verzeichnis,
/// die Verschlüsselung wird durch einen einfachen Fake-Protector ersetzt.
/// </summary>
public sealed class JsonProfileStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _filePath;
    private readonly FakeSecretProtector _protector = new();

    public JsonProfileStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "CMISPilotTests_" + Guid.NewGuid().ToString("N"));
        _filePath = Path.Combine(_tempDir, "sub", "profiles.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private JsonProfileStore CreateSut() => new(_filePath, _protector);

    [Fact]
    public async Task SaveAsync_ohne_SavePassword_speichert_Passwort_nicht()
    {
        var sut = CreateSut();
        var profile = new ConnectionProfile
        {
            Name = "Testserver",
            BrowserUrl = "http://host:8080/inmemory/browser",
            User = "test",
            Password = "geheim"
        };

        await sut.SaveAsync(profile, savePassword: false);

        var loaded = (await sut.LoadAllAsync()).Single();
        Assert.Equal("Testserver", loaded.Name);
        Assert.Equal(string.Empty, loaded.Password);

        // Die Datei selbst darf das Passwort an keiner Stelle im Klartext enthalten.
        var raw = await File.ReadAllTextAsync(_filePath);
        Assert.DoesNotContain("geheim", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveAsync_mit_SavePassword_speichert_verschluesselt_und_Roundtrip_liefert_Klartext()
    {
        var sut = CreateSut();
        var profile = new ConnectionProfile
        {
            Name = "Testserver",
            BrowserUrl = "http://host:8080/inmemory/browser",
            User = "test",
            Password = "geheim"
        };

        await sut.SaveAsync(profile, savePassword: true);

        // Datei enthaelt das Passwort nicht im Klartext (nur verschluesselt/fake-transformiert).
        var raw = await File.ReadAllTextAsync(_filePath);
        Assert.DoesNotContain("geheim", raw, StringComparison.Ordinal);
        Assert.True(_protector.ProtectCallCount > 0);

        // Aber der Roundtrip ueber den Store liefert das Passwort wieder im Klartext.
        var loaded = (await sut.LoadAllAsync()).Single();
        Assert.Equal("geheim", loaded.Password);
    }

    [Fact]
    public async Task SaveAsync_mit_AdditionalHeaders_Roundtrip_liefert_dieselben_Header()
    {
        var sut = CreateSut();
        var profile = new ConnectionProfile
        {
            Name = "Testserver",
            BrowserUrl = "http://host",
            AdditionalHeaders = new[] { new HttpHeaderEntry("X-Mandant", "42"), new HttpHeaderEntry("X-Gateway-Key", "abc") }
        };

        // Kein Geheimnis: AdditionalHeaders werden unabhaengig von savePassword gespeichert.
        await sut.SaveAsync(profile, savePassword: false);

        var loaded = (await sut.LoadAllAsync()).Single();
        Assert.Equal(2, loaded.AdditionalHeaders.Count);
        Assert.Contains(loaded.AdditionalHeaders, h => h.Name == "X-Mandant" && h.Value == "42");
        Assert.Contains(loaded.AdditionalHeaders, h => h.Name == "X-Gateway-Key" && h.Value == "abc");
    }

    [Fact]
    public async Task SaveAsync_mit_OAuthFeldern_Roundtrip_und_ClientSecret_nur_mit_SavePassword()
    {
        var sut = CreateSut();
        var profile = new ConnectionProfile
        {
            Name = "Testserver",
            Authentication = CmisAuthenticationType.OAuthBearer,
            OAuthAuthorizationEndpoint = "http://host/oauth2/authorize",
            OAuthTokenEndpoint = "http://host/oauth2/token",
            OAuthClientId = "cmispilot",
            OAuthClientSecret = "geheim",
            OAuthRedirectUri = "http://localhost:51737/auth-done"
        };

        await sut.SaveAsync(profile, savePassword: false);

        var loadedOhneSecret = (await sut.LoadAllAsync()).Single();
        Assert.Equal("http://host/oauth2/authorize", loadedOhneSecret.OAuthAuthorizationEndpoint);
        Assert.Equal("http://host/oauth2/token", loadedOhneSecret.OAuthTokenEndpoint);
        Assert.Equal("cmispilot", loadedOhneSecret.OAuthClientId);
        Assert.Equal("http://localhost:51737/auth-done", loadedOhneSecret.OAuthRedirectUri);
        Assert.Equal(string.Empty, loadedOhneSecret.OAuthClientSecret);

        await sut.SaveAsync(profile, savePassword: true);
        var raw = await File.ReadAllTextAsync(_filePath);
        Assert.DoesNotContain("geheim", raw, StringComparison.Ordinal);

        var loadedMitSecret = (await sut.LoadAllAsync()).Single();
        Assert.Equal("geheim", loadedMitSecret.OAuthClientSecret);
    }

    [Fact]
    public async Task SaveAsync_gleicher_Name_ueberschreibt_bestehendes_Profil()
    {
        var sut = CreateSut();
        await sut.SaveAsync(new ConnectionProfile { Name = "A", BrowserUrl = "http://alt", User = "u1" }, false);
        await sut.SaveAsync(new ConnectionProfile { Name = "A", BrowserUrl = "http://neu", User = "u2" }, false);

        var all = await sut.LoadAllAsync();
        var single = Assert.Single(all);
        Assert.Equal("http://neu", single.BrowserUrl);
        Assert.Equal("u2", single.User);
    }

    [Fact]
    public async Task DeleteAsync_entfernt_Profil()
    {
        var sut = CreateSut();
        await sut.SaveAsync(new ConnectionProfile { Name = "A", BrowserUrl = "http://a", User = "u" }, false);
        await sut.SaveAsync(new ConnectionProfile { Name = "B", BrowserUrl = "http://b", User = "u" }, false);

        await sut.DeleteAsync("A");

        var all = await sut.LoadAllAsync();
        var single = Assert.Single(all);
        Assert.Equal("B", single.Name);
    }

    [Fact]
    public async Task DeleteAsync_unbekannter_Name_wirft_nicht()
    {
        var sut = CreateSut();
        await sut.SaveAsync(new ConnectionProfile { Name = "A", BrowserUrl = "http://a", User = "u" }, false);

        await sut.DeleteAsync("Unbekannt");

        var all = await sut.LoadAllAsync();
        Assert.Single(all);
    }

    [Fact]
    public async Task LoadAllAsync_ohne_vorhandene_Datei_liefert_leere_Liste()
    {
        var sut = CreateSut();

        var all = await sut.LoadAllAsync();

        Assert.Empty(all);
        Assert.False(File.Exists(_filePath));
    }

    [Fact]
    public async Task SaveAsync_ohne_Name_wirft_ArgumentException()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.SaveAsync(new ConnectionProfile { Name = "  ", BrowserUrl = "http://a", User = "u" }, false));
    }

    [Fact]
    public async Task SaveAsync_speichert_neue_Felder_und_Roundtrip_liefert_sie_zurueck()
    {
        var sut = CreateSut();
        var profile = new ConnectionProfile
        {
            Name = "OAuthServer",
            BrowserUrl = "https://api/browser",
            Authentication = CmisAuthenticationType.OAuthBearer,
            BearerToken = "tok-123",
            Compression = false,
            CsrfHeader = "X-CSRF-Token",
            ConnectTimeoutMs = 30000,
            ReadTimeoutMs = 600000,
            RepositoryId = "repoA"
        };

        await sut.SaveAsync(profile, savePassword: true);

        var loaded = (await sut.LoadAllAsync()).Single();
        Assert.Equal(CmisAuthenticationType.OAuthBearer, loaded.Authentication);
        Assert.Equal("tok-123", loaded.BearerToken);
        Assert.False(loaded.Compression);
        Assert.Equal("X-CSRF-Token", loaded.CsrfHeader);
        Assert.Equal(30000, loaded.ConnectTimeoutMs);
        Assert.Equal(600000, loaded.ReadTimeoutMs);
        Assert.Equal("repoA", loaded.RepositoryId);

        // Der Bearer-Token ist ein Geheimnis und darf nie im Klartext in der Datei stehen.
        var raw = await File.ReadAllTextAsync(_filePath);
        Assert.DoesNotContain("tok-123", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveAsync_ohne_SavePassword_speichert_BearerToken_nicht()
    {
        var sut = CreateSut();
        var profile = new ConnectionProfile
        {
            Name = "OAuthServer",
            BrowserUrl = "https://api/browser",
            Authentication = CmisAuthenticationType.OAuthBearer,
            BearerToken = "tok-xyz"
        };

        await sut.SaveAsync(profile, savePassword: false);

        var loaded = (await sut.LoadAllAsync()).Single();
        Assert.Equal(string.Empty, loaded.BearerToken);
    }

    [Fact]
    public async Task SaveAsync_AtomPubProfil_Roundtrip_liefert_BindingTypeUndAtomPubUrl()
    {
        var sut = CreateSut();
        var profile = new ConnectionProfile
        {
            Name = "AtomServer",
            BindingType = CmisBindingType.AtomPub,
            AtomPubUrl = "http://host:8080/inmemory/atom11",
            User = "test"
        };

        await sut.SaveAsync(profile, savePassword: false);

        var loaded = (await sut.LoadAllAsync()).Single();
        Assert.Equal(CmisBindingType.AtomPub, loaded.BindingType);
        Assert.Equal("http://host:8080/inmemory/atom11", loaded.AtomPubUrl);
    }

    [Fact]
    public async Task LoadAllAsync_AltesJsonSchemaOhneBindingFelder_liefertBrowserBindingAlsDefault()
    {
        // Simuliert eine profiles.json, die vor diesem Feature gespeichert wurde:
        // kein "bindingType", kein "atomPubUrl" im JSON.
        var directory = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(_filePath, """
            [
              {
                "name": "Altprofil",
                "browserUrl": "http://host:8080/inmemory/browser",
                "authentication": "Standard",
                "user": "test",
                "compression": true
              }
            ]
            """);

        var sut = CreateSut();
        var loaded = (await sut.LoadAllAsync()).Single();

        Assert.Equal(CmisBindingType.Browser, loaded.BindingType);
        Assert.Equal("http://host:8080/inmemory/browser", loaded.BrowserUrl);
        Assert.Equal(string.Empty, loaded.AtomPubUrl);
    }

    /// <summary>
    /// Einfacher, umkehrbarer Fake-Protector für Tests ohne Windows/DPAPI
    /// (Base64 statt echter Verschlüsselung, aber kein Klartext in der Datei).
    /// </summary>
    private sealed class FakeSecretProtector : ISecretProtector
    {
        public int ProtectCallCount { get; private set; }

        public string Protect(string plainText)
        {
            ProtectCallCount++;
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("fake:" + plainText));
        }

        public string Unprotect(string protectedText)
        {
            var text = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(protectedText));
            return text.StartsWith("fake:", StringComparison.Ordinal) ? text["fake:".Length..] : text;
        }
    }
}
