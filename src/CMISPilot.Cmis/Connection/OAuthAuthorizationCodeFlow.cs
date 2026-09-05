using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace CMISPilot.Cmis.Connection;

/// <summary>
/// Beschafft ein OAuth-2.0-Access-Token gegen einen Identity Provider (z. B. Nuxeo),
/// entweder per Authorization-Code-Flow (<see cref="GetAccessTokenAsync"/> - interaktiver
/// Login im System-Browser) oder per Client-Credentials-Flow
/// (<see cref="GetClientCredentialsTokenAsync"/> - kein Login, direkter Tausch von
/// Client-ID/Secret gegen ein Token, das die Anwendung statt eines Nutzers repraesentiert).
///
/// <para><b>Redirect-URI-Einschraenkung (nur Authorization-Code-Flow):</b> muss
/// <c>http://localhost:PORT/pfad</c> sein. <see cref="HttpListener"/> kann ohne
/// Adminrechte nur auf <c>localhost</c> binden, und ohne ein echtes, gebundenes
/// TLS-Zertifikat funktioniert kein <c>https://localhost</c>. Der Identity Provider
/// muss fuer den CMISPilot-Client exakt dieselbe Redirect-URI registriert haben.</para>
///
/// <para>Bewusst ohne PKCE und ohne Refresh-Token-Handling (v1): der Nutzer meldet
/// sich einmal je Verbindungsaufbau an, das Ergebnis ist ein normales Bearer-Token
/// wie beim manuell eingetragenen <see cref="Models.CmisAuthenticationType.OAuthBearer"/>.</para>
/// </summary>
public sealed class OAuthAuthorizationCodeFlow
{
    private static readonly TimeSpan CallbackTimeout = TimeSpan.FromMinutes(3);

    /// <summary>
    /// Fuehrt den kompletten Flow durch und liefert das Access Token.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Ungueltige Redirect-URI, Zeitueberschreitung, Ablehnung durch den Nutzer/Provider,
    /// oder der Token-Endpoint hat kein Access Token geliefert.
    /// </exception>
    public async Task<string> GetAccessTokenAsync(
        string authorizationEndpoint,
        string tokenEndpoint,
        string clientId,
        string clientSecret,
        string redirectUri,
        CancellationToken ct = default)
    {
        var trimmedRedirectUri = ValidateRedirectUri(redirectUri, out var listenerPrefix);
        var state = Guid.NewGuid().ToString("N");

        using var listener = new HttpListener();
        listener.Prefixes.Add(listenerPrefix);

        try
        {
            listener.Start();
        }
        catch (HttpListenerException ex)
        {
            throw new InvalidOperationException(
                $"Der lokale Listener für \"{listenerPrefix}\" konnte nicht gestartet werden " +
                $"(Port evtl. belegt): {ex.Message}", ex);
        }

        try
        {
            var authorizationUrl = BuildAuthorizationUrl(authorizationEndpoint, clientId, trimmedRedirectUri, state);
            OpenInSystemBrowser(authorizationUrl);

            var code = await WaitForAuthorizationCodeAsync(listener, state, ct).ConfigureAwait(false);
            return await ExchangeCodeForTokenAsync(
                tokenEndpoint, clientId, clientSecret, trimmedRedirectUri, code, ct).ConfigureAwait(false);
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>
    /// Client-Credentials-Flow (RFC 6749 §4.4): tauscht Client-ID/Secret direkt und
    /// ohne jede Nutzerinteraktion gegen ein Access Token, das die Anwendung selbst
    /// repraesentiert (kein Browser, kein Listener, keine Redirect-URI noetig). Nur
    /// nutzbar, wenn der Identity Provider dieses Grant fuer den registrierten Client
    /// erlaubt - die Rechte des Tokens entsprechen dann dem hinterlegten
    /// Service-Account, nicht denen eines einzelnen angemeldeten Nutzers.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Der Token-Endpoint hat den Tausch abgelehnt oder kein Access Token geliefert.
    /// </exception>
    public async Task<string> GetClientCredentialsTokenAsync(
        string tokenEndpoint,
        string clientId,
        string clientSecret,
        CancellationToken ct = default)
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret
        });

        return await PostTokenRequestAsync(tokenEndpoint, form, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Prueft die Redirect-URI und liefert sie unveraendert zurueck (als
    /// <c>redirect_uri</c>-Parameter muss sie exakt so beim Identity Provider
    /// registriert sein - viele Provider vergleichen sie zeichengenau, inklusive
    /// eines eventuell fehlenden abschliessenden "/"). Der <paramref name="listenerPrefix"/>-
    /// Rueckgabewert ist bewusst nur der Host+Port-Teil mit abschliessendem "/"
    /// (<see cref="HttpListener"/>-Praefixe MUESSEN mit "/" enden): der Listener lauscht
    /// auf dem ganzen Port statt exakt auf dem Pfad, weil ein Praefix mit erzwungenem
    /// Schrägstrich eine Anfrage auf den (korrekten, schrägstrichlosen) Pfad sonst gar
    /// nicht treffen wuerde. Auf diesem lokalen, kurzlebigen Port ist ohnehin nichts
    /// anderes gebunden, ein Pfadabgleich ist daher nicht noetig.
    /// </summary>
    private static string ValidateRedirectUri(string redirectUri, out string listenerPrefix)
    {
        if (string.IsNullOrWhiteSpace(redirectUri) ||
            !Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttp ||
            !uri.IsLoopback)
        {
            throw new InvalidOperationException(
                "Die Redirect-URI muss \"http://localhost:PORT/pfad\" sein (kein https, kein anderer Host) - " +
                "CMISPilot faengt die Weiterleitung selbst mit einem lokalen Listener ab.");
        }

        listenerPrefix = $"{uri.Scheme}://{uri.Authority}/";
        return redirectUri.Trim();
    }

    private static string BuildAuthorizationUrl(string authorizationEndpoint, string clientId, string redirectUri, string state)
    {
        var query = string.Join('&',
            "response_type=code",
            $"client_id={Uri.EscapeDataString(clientId)}",
            $"redirect_uri={Uri.EscapeDataString(redirectUri)}",
            $"state={Uri.EscapeDataString(state)}");

        var separator = authorizationEndpoint.Contains('?') ? '&' : '?';
        return $"{authorizationEndpoint}{separator}{query}";
    }

    /// <summary>Oeffnet die URL im Standardbrowser des Betriebssystems (kein eingebetteter Browser).</summary>
    private static void OpenInSystemBrowser(string url) =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    private static async Task<string> WaitForAuthorizationCodeAsync(HttpListener listener, string expectedState, CancellationToken ct)
    {
        using var timeoutCts = new CancellationTokenSource(CallbackTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        HttpListenerContext context;
        try
        {
            context = await listener.GetContextAsync().WaitAsync(linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            throw new InvalidOperationException(
                "Zeitüberschreitung: Es kam innerhalb von 3 Minuten keine Antwort vom Identity Provider zurück.");
        }

        var query = ParseQueryString(context.Request.Url?.Query);
        RespondToBrowser(context, query.ContainsKey("error"));

        if (query.TryGetValue("error", out var error))
        {
            var description = query.GetValueOrDefault("error_description", error);
            throw new InvalidOperationException($"Anmeldung abgelehnt: {description}");
        }

        if (!query.TryGetValue("state", out var actualState) || actualState != expectedState)
        {
            throw new InvalidOperationException("Ungültige Antwort vom Identity Provider (state stimmt nicht überein).");
        }

        return query.TryGetValue("code", out var code) && !string.IsNullOrEmpty(code)
            ? code
            : throw new InvalidOperationException("Der Identity Provider hat keinen Autorisierungscode geliefert.");
    }

    private static void RespondToBrowser(HttpListenerContext context, bool isError)
    {
        var message = isError
            ? "Anmeldung fehlgeschlagen. Dieses Fenster kann geschlossen werden."
            : "Anmeldung erfolgreich. Dieses Fenster kann geschlossen werden.";
        var html = $"<html><body style=\"font-family:sans-serif\"><p>{message}</p></body></html>";
        var buffer = Encoding.UTF8.GetBytes(html);

        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = buffer.Length;
        context.Response.OutputStream.Write(buffer, 0, buffer.Length);
        context.Response.OutputStream.Close();
    }

    private static Task<string> ExchangeCodeForTokenAsync(
        string tokenEndpoint, string clientId, string clientSecret, string redirectUri, string code, CancellationToken ct)
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret
        });

        return PostTokenRequestAsync(tokenEndpoint, form, ct);
    }

    /// <summary>Gemeinsamer POST-ans-Token-Endpoint-und-access_token-auslesen-Teil beider Flows.</summary>
    private static async Task<string> PostTokenRequestAsync(string tokenEndpoint, FormUrlEncodedContent form, CancellationToken ct)
    {
        using var httpClient = new HttpClient();
        using var response = await httpClient.PostAsync(tokenEndpoint, form, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Der Token-Endpoint hat die Anfrage abgelehnt (HTTP {(int)response.StatusCode}): {body}");
        }

        using var json = JsonDocument.Parse(body);
        return json.RootElement.TryGetProperty("access_token", out var token) && token.ValueKind == JsonValueKind.String
            ? token.GetString()!
            : throw new InvalidOperationException("Die Antwort des Token-Endpoints enthielt kein access_token.");
    }

    private static Dictionary<string, string> ParseQueryString(string? query)
    {
        var result = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(query))
        {
            return result;
        }

        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]);
            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            result[key] = value;
        }

        return result;
    }
}
