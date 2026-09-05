using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CMISPilot.Cmis.Models;

namespace CMISPilot.Cmis.Profiles;

/// <summary>
/// <see cref="IProfileStore"/>-Implementierung auf Basis einer JSON-Datei
/// (Standardpfad <c>%APPDATA%/CMISPilot/profiles.json</c>, FA-04/NFA-07).
/// Passwörter werden nur bei ausdrücklichem Wunsch gespeichert und dann über
/// <see cref="ISecretProtector"/> verschlüsselt abgelegt – nie im Klartext.
/// Datei-Pfad und Protector sind injizierbar, damit die Klasse ohne Windows/DPAPI
/// mit einer temporären Datei und einem Fake-Protector testbar ist (T10.4).
/// </summary>
public sealed class JsonProfileStore : IProfileStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly ISecretProtector _protector;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonProfileStore(string filePath, ISecretProtector protector)
    {
        _filePath = filePath;
        _protector = protector;
    }

    /// <summary>Standard-Speicherort unter <c>%APPDATA%</c> (NFA-07).</summary>
    public static string GetDefaultFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "CMISPilot", "profiles.json");
    }

    public async Task<IReadOnlyList<ConnectionProfile>> LoadAllAsync(CancellationToken ct = default)
    {
        var stored = await ReadStoredAsync(ct).ConfigureAwait(false);
        return stored.Select(ToConnectionProfile).ToList();
    }

    public async Task SaveAsync(ConnectionProfile profile, bool savePassword, CancellationToken ct = default)
    {
        if (profile is null) throw new ArgumentNullException(nameof(profile));
        if (string.IsNullOrWhiteSpace(profile.Name))
            throw new ArgumentException("Der Profilname darf nicht leer sein.", nameof(profile));

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var stored = await ReadStoredAsync(ct).ConfigureAwait(false);
            stored.RemoveAll(s => string.Equals(s.Name, profile.Name, StringComparison.OrdinalIgnoreCase));

            stored.Add(new StoredProfile
            {
                Name = profile.Name!.Trim(),
                BindingType = profile.BindingType,
                BrowserUrl = profile.BrowserUrl,
                AtomPubUrl = profile.AtomPubUrl,
                Authentication = profile.Authentication,
                User = profile.User,
                EncryptedPassword = savePassword && !string.IsNullOrEmpty(profile.Password)
                    ? _protector.Protect(profile.Password)
                    : null,
                // Der Bearer-Token ist ebenfalls ein Geheimnis: nur bei ausdruecklichem
                // Wunsch und dann DPAPI-verschluesselt (nie im Klartext), analog Passwort.
                EncryptedBearerToken = savePassword && !string.IsNullOrEmpty(profile.BearerToken)
                    ? _protector.Protect(profile.BearerToken)
                    : null,
                Compression = profile.Compression,
                CsrfHeader = profile.CsrfHeader,
                ConnectTimeoutMs = profile.ConnectTimeoutMs,
                ReadTimeoutMs = profile.ReadTimeoutMs,
                RepositoryId = profile.RepositoryId,
                // Kein Geheimnis (anders als Password/BearerToken) - immer
                // unverschluesselt gespeichert, unabhaengig von savePassword.
                AdditionalHeaders = profile.AdditionalHeaders
                    .Select(h => new StoredHeader { Name = h.Name, Value = h.Value })
                    .ToList(),
                OAuthAuthorizationEndpoint = profile.OAuthAuthorizationEndpoint,
                OAuthTokenEndpoint = profile.OAuthTokenEndpoint,
                OAuthClientId = profile.OAuthClientId,
                OAuthRedirectUri = profile.OAuthRedirectUri,
                OAuthUseClientCredentials = profile.OAuthUseClientCredentials,
                // Client-Secret ist ein Geheimnis wie Passwort/Bearer-Token.
                EncryptedOAuthClientSecret = savePassword && !string.IsNullOrEmpty(profile.OAuthClientSecret)
                    ? _protector.Protect(profile.OAuthClientSecret)
                    : null
            });

            await WriteStoredAsync(stored, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(string name, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var stored = await ReadStoredAsync(ct).ConfigureAwait(false);
            var removed = stored.RemoveAll(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                await WriteStoredAsync(stored, ct).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private ConnectionProfile ToConnectionProfile(StoredProfile stored)
    {
        return new ConnectionProfile
        {
            Name = stored.Name,
            BindingType = stored.BindingType,
            BrowserUrl = stored.BrowserUrl,
            AtomPubUrl = stored.AtomPubUrl,
            Authentication = stored.Authentication,
            User = stored.User,
            Password = Decrypt(stored.EncryptedPassword),
            BearerToken = Decrypt(stored.EncryptedBearerToken),
            Compression = stored.Compression,
            CsrfHeader = stored.CsrfHeader,
            ConnectTimeoutMs = stored.ConnectTimeoutMs,
            ReadTimeoutMs = stored.ReadTimeoutMs,
            RepositoryId = stored.RepositoryId,
            AdditionalHeaders = (stored.AdditionalHeaders ?? new List<StoredHeader>())
                .Select(h => new HttpHeaderEntry(h.Name, h.Value))
                .ToList(),
            OAuthAuthorizationEndpoint = stored.OAuthAuthorizationEndpoint ?? string.Empty,
            OAuthTokenEndpoint = stored.OAuthTokenEndpoint ?? string.Empty,
            OAuthClientId = stored.OAuthClientId ?? string.Empty,
            OAuthClientSecret = Decrypt(stored.EncryptedOAuthClientSecret),
            OAuthRedirectUri = stored.OAuthRedirectUri ?? string.Empty,
            OAuthUseClientCredentials = stored.OAuthUseClientCredentials
        };
    }

    /// <summary>
    /// Entschluesselt ein optionales Geheimnis. Schlaegt die Entschluesselung fehl
    /// (z. B. DPAPI nach Benutzerwechsel), wird leer zurueckgegeben, statt die gesamte
    /// Profilliste zu verwerfen.
    /// </summary>
    private string Decrypt(string? encrypted)
    {
        if (string.IsNullOrEmpty(encrypted))
        {
            return string.Empty;
        }

        try
        {
            return _protector.Unprotect(encrypted);
        }
        catch
        {
            return string.Empty;
        }
    }

    private async Task<List<StoredProfile>> ReadStoredAsync(CancellationToken ct)
    {
        if (!File.Exists(_filePath))
        {
            return new List<StoredProfile>();
        }

        await using var stream = File.OpenRead(_filePath);
        var list = await JsonSerializer.DeserializeAsync<List<StoredProfile>>(stream, SerializerOptions, ct)
            .ConfigureAwait(false);
        return list ?? new List<StoredProfile>();
    }

    private async Task WriteStoredAsync(List<StoredProfile> stored, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, stored, SerializerOptions, ct).ConfigureAwait(false);
    }

    /// <summary>Auf-Platte-Repräsentation eines Profils. Passwort nie im Klartext (NFA-07).</summary>
    private sealed class StoredProfile
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Fehlt das Feld in einer vor diesem Feature gespeicherten Datei, liefert der
        /// Default (Browser) das bisherige Verhalten unveraendert.
        /// </summary>
        [JsonPropertyName("bindingType")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public CmisBindingType BindingType { get; set; } = CmisBindingType.Browser;

        [JsonPropertyName("browserUrl")]
        public string BrowserUrl { get; set; } = string.Empty;

        [JsonPropertyName("atomPubUrl")]
        public string AtomPubUrl { get; set; } = string.Empty;

        [JsonPropertyName("authentication")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public CmisAuthenticationType Authentication { get; set; } = CmisAuthenticationType.Standard;

        [JsonPropertyName("user")]
        public string User { get; set; } = string.Empty;

        /// <summary>DPAPI-verschlüsseltes Passwort (Base64) oder <c>null</c>, wenn nicht gespeichert.</summary>
        [JsonPropertyName("encryptedPassword")]
        public string? EncryptedPassword { get; set; }

        /// <summary>DPAPI-verschlüsselter Bearer-Token (Base64) oder <c>null</c>, wenn nicht gespeichert.</summary>
        [JsonPropertyName("encryptedBearerToken")]
        public string? EncryptedBearerToken { get; set; }

        [JsonPropertyName("compression")]
        public bool Compression { get; set; } = true;

        [JsonPropertyName("csrfHeader")]
        public string? CsrfHeader { get; set; }

        [JsonPropertyName("connectTimeoutMs")]
        public int? ConnectTimeoutMs { get; set; }

        [JsonPropertyName("readTimeoutMs")]
        public int? ReadTimeoutMs { get; set; }

        [JsonPropertyName("repositoryId")]
        public string? RepositoryId { get; set; }

        /// <summary>Zusaetzliche HTTP-Header, kein Geheimnis, immer unverschluesselt.</summary>
        [JsonPropertyName("additionalHeaders")]
        public List<StoredHeader>? AdditionalHeaders { get; set; }

        [JsonPropertyName("oAuthAuthorizationEndpoint")]
        public string? OAuthAuthorizationEndpoint { get; set; }

        [JsonPropertyName("oAuthTokenEndpoint")]
        public string? OAuthTokenEndpoint { get; set; }

        [JsonPropertyName("oAuthClientId")]
        public string? OAuthClientId { get; set; }

        [JsonPropertyName("oAuthRedirectUri")]
        public string? OAuthRedirectUri { get; set; }

        [JsonPropertyName("oAuthUseClientCredentials")]
        public bool OAuthUseClientCredentials { get; set; }

        /// <summary>DPAPI-verschluesseltes OAuth-Client-Secret (Base64) oder <c>null</c>, wenn nicht gespeichert.</summary>
        [JsonPropertyName("encryptedOAuthClientSecret")]
        public string? EncryptedOAuthClientSecret { get; set; }
    }

    /// <summary>Auf-Platte-Repräsentation eines zusätzlichen HTTP-Headers.</summary>
    private sealed class StoredHeader
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;
    }
}
