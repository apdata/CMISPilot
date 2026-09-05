namespace CMISPilot.Cmis.Models;

/// <summary>
/// Ein zusaetzlicher, statischer HTTP-Header (Name/Wert), der bei jeder Anfrage an
/// den Server mitgeschickt wird - unabhaengig von der gewaehlten
/// Authentifizierungsart (z. B. ein API-Gateway- oder Mandanten-Header).
/// </summary>
public sealed record HttpHeaderEntry(string Name, string Value);

/// <summary>
/// Art der Authentifizierung fuer das Browser Binding. Beschraenkt auf die
/// Verfahren, die PortCMIS ueber das Browser Binding tatsaechlich unterstuetzt
/// (siehe StandardAuthenticationProvider): keine, Basic (Benutzer/Passwort) und
/// OAuth 2.0 Bearer Token. NTLM/Client-Zertifikat aus der CMIS Workbench sind mit
/// dem Browser Binding hier nicht abgebildet.
/// </summary>
public enum CmisAuthenticationType
{
    /// <summary>Ohne Authentifizierung (Standard-Credentials der Umgebung).</summary>
    None,

    /// <summary>Basic Authentication mit Benutzer/Passwort.</summary>
    Standard,

    /// <summary>OAuth 2.0 mit einem Bearer-Token im Authorization-Header.</summary>
    OAuthBearer
}

/// <summary>
/// Das CMIS-Binding, über das die Session aufgebaut wird. Default
/// <see cref="Browser"/>, damit vor diesem Feld gespeicherte Profile ohne
/// Migration weiterhin als Browser-Binding-Profile geladen werden.
/// </summary>
public enum CmisBindingType
{
    /// <summary>CMIS Browser Binding (JSON).</summary>
    Browser,

    /// <summary>CMIS AtomPub Binding (XML).</summary>
    AtomPub
}

/// <summary>
/// UI-freundliches Verbindungsprofil für den Aufbau einer CMIS-Session über das
/// Browser Binding oder das AtomPub Binding (FA-01). Enthält keine PortCMIS-Typen.
/// Zugangsdaten werden nur zur Laufzeit im Speicher gehalten (siehe Konzept §7).
/// </summary>
public sealed class ConnectionProfile
{
    /// <summary>
    /// Vorgabe für das Verbindungs-Timeout in Millisekunden (30 s), wie sie auch die
    /// CMIS Workbench verwendet. Ohne gesetzten Wert greift der Standard des
    /// <c>HttpClient</c> von 100 s.
    /// </summary>
    public const int DefaultConnectTimeoutMs = 30_000;

    /// <summary>
    /// Vorgabe für das Lese-Timeout in Millisekunden (600 s), wie sie auch die
    /// CMIS Workbench verwendet.
    ///
    /// <para>Achtung: <c>SessionParameter.ReadTimeout</c> wird vom mitgelieferten
    /// PortCMIS zwar deklariert, aber nirgends ausgewertet — das Lese-Timeout ist
    /// derzeit wirkungslos. Der Vorgabewert macht nur sichtbar, welcher Wert gälte.</para>
    /// </summary>
    public const int DefaultReadTimeoutMs = 600_000;

    /// <summary>Optionaler Anzeigename des Profils (für gespeicherte Profile, M10).</summary>
    public string? Name { get; set; }

    /// <summary>Gewähltes Binding (Standard: Browser Binding).</summary>
    public CmisBindingType BindingType { get; set; } = CmisBindingType.Browser;

    /// <summary>Browser-Binding-URL, z. B. <c>http://host:8080/inmemory/browser</c>.</summary>
    public string BrowserUrl { get; set; } = string.Empty;

    /// <summary>AtomPub-Binding-URL, z. B. <c>http://host:8080/inmemory/atom11</c>.</summary>
    public string AtomPubUrl { get; set; } = string.Empty;

    /// <summary>Art der Authentifizierung (Standard = Basic).</summary>
    public CmisAuthenticationType Authentication { get; set; } = CmisAuthenticationType.Standard;

    /// <summary>Benutzername für Basic Auth (nur bei <see cref="CmisAuthenticationType.Standard"/>).</summary>
    public string User { get; set; } = string.Empty;

    /// <summary>Passwort für Basic Auth (nur zur Laufzeit im Speicher).</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// OAuth-2.0-Bearer-Token (nur bei <see cref="CmisAuthenticationType.OAuthBearer"/>,
    /// nur zur Laufzeit im Speicher). Entweder manuell eingetragen, oder Ergebnis des
    /// Authorization-Code-Flows ueber <see cref="OAuthAuthorizationEndpoint"/>/
    /// <see cref="OAuthTokenEndpoint"/> (siehe <c>Connection.OAuthAuthorizationCodeFlow</c>).
    /// </summary>
    public string BearerToken { get; set; } = string.Empty;

    /// <summary>Authorization-Endpoint des Identity Providers (nur OAuth-Anmeldung, nicht der Bearer-Token selbst).</summary>
    public string OAuthAuthorizationEndpoint { get; set; } = string.Empty;

    /// <summary>Token-Endpoint des Identity Providers (Code-gegen-Token-Tausch).</summary>
    public string OAuthTokenEndpoint { get; set; } = string.Empty;

    /// <summary>Client-ID der bei OAuth registrierten CMISPilot-Anwendung.</summary>
    public string OAuthClientId { get; set; } = string.Empty;

    /// <summary>Client-Secret der Anwendung (nur zur Laufzeit im Speicher, wie <see cref="Password"/>).</summary>
    public string OAuthClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Redirect-URI, auf die der Identity Provider nach dem Login zurueckleitet.
    /// Muss <c>http://localhost:PORT/pfad</c> sein (kein <c>https</c>, kein anderer
    /// Host) - CMISPilot faengt die Weiterleitung selbst mit einem kurzzeitigen
    /// lokalen HTTP-Listener ab und muss deshalb exakt das registrieren, was auch
    /// beim Identity Provider fuer diesen Client hinterlegt ist.
    /// </summary>
    public string OAuthRedirectUri { get; set; } = string.Empty;

    /// <summary>
    /// True: OAuth-Token per Client-Credentials-Flow holen (Client-ID/Secret direkt
    /// gegen ein Token tauschen, kein Browser-Login, keine Redirect-URI noetig - das
    /// Token repraesentiert dann die Anwendung/einen Service-Account, nicht einen
    /// einzelnen Nutzer). False (Standard): Authorization-Code-Flow mit Login im
    /// System-Browser. Siehe <c>Connection.OAuthAuthorizationCodeFlow</c>.
    /// </summary>
    public bool OAuthUseClientCredentials { get; set; }

    /// <summary>
    /// Ziel-Repository. Für das reine Auflisten der Repositories optional (null);
    /// für <c>ConnectAsync</c> erforderlich.
    /// </summary>
    public string? RepositoryId { get; set; }

    /// <summary>
    /// Anzeigename des Ziel-Repositories, übernommen aus der Auswahl im Verbinden-Dialog.
    ///
    /// <para>Nötig, weil zwischen Dialog und Sitzung sonst nur die <see cref="RepositoryId"/>
    /// weitergereicht wird: der Name der Anzeige stammt aus <c>getRepositoryInfo</c> beim
    /// Verbinden, und liefert der Server ihn dort leer, bliebe die Beschriftung im Baum
    /// leer. Dieses Feld dient dann als Rückfall (siehe <c>ConnectionService.ConnectAsync</c>).</para>
    /// </summary>
    public string? RepositoryName { get; set; }

    /// <summary>GZip-Kompression der Serverantworten (PortCMIS-Compression). Standard: an.</summary>
    public bool Compression { get; set; } = true;

    /// <summary>
    /// Optionaler Name des CSRF-Header (leer = keiner). Nur beim Browser Binding
    /// relevant, wird bei <see cref="CmisBindingType.AtomPub"/> nicht gesetzt.
    /// </summary>
    public string? CsrfHeader { get; set; }

    /// <summary>Verbindungs-Timeout in Millisekunden (optional).</summary>
    public int? ConnectTimeoutMs { get; set; }

    /// <summary>Lese-Timeout in Millisekunden (optional).</summary>
    public int? ReadTimeoutMs { get; set; }

    /// <summary>
    /// Zusaetzliche statische HTTP-Header, die bei jeder Anfrage mitgeschickt
    /// werden (unabhaengig von <see cref="Authentication"/>). Siehe
    /// <see cref="Connection.HeaderInjectingAuthenticationProvider"/>.
    /// </summary>
    public IReadOnlyList<HttpHeaderEntry> AdditionalHeaders { get; set; } = Array.Empty<HttpHeaderEntry>();
}
