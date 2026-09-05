using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CMISPilot.Cmis.Contracts;
using CMISPilot.Cmis.Diagnostics;
using CMISPilot.Cmis.Errors;
using CMISPilot.Cmis.Execution;
using CMISPilot.Cmis.Models;
using PortCMIS;
using PortCMIS.Client;
using PortCMIS.Client.Impl;

namespace CMISPilot.Cmis.Connection;

/// <summary>
/// Baut CMIS-Sessions über das Browser Binding oder das AtomPub Binding auf
/// (FA-01/02/10/11, je nach <see cref="ConnectionProfile.BindingType"/>) und hält
/// den aktiven Zustand im <see cref="SessionContext"/>. Alle blockierenden
/// PortCMIS-Aufrufe laufen über den <see cref="ICmisExecutor"/> (NFA-05/13).
/// </summary>
public sealed class ConnectionService : IConnectionService
{
    private readonly ICmisExecutor _executor;
    private readonly SessionContext _context;

    public ConnectionService(ICmisExecutor executor, SessionContext context)
    {
        _executor = executor;
        _context = context;
    }

    public Task<IReadOnlyList<RepositoryInfoDto>> GetRepositoriesAsync(
        ConnectionProfile profile, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ValidateUrl(profile);

        return _executor.RunAsync<IReadOnlyList<RepositoryInfoDto>>(() =>
        {
            var factory = SessionFactory.NewInstance();
            var parameters = BuildParameters(profile, includeRepositoryId: false);
            IList<IRepository> repositories = CreateAdditionalHeadersProvider(profile) is { } provider
                ? factory.GetRepositories(parameters, null, provider, null)
                : factory.GetRepositories(parameters);
            return repositories.Select(r => CmisModelMapper.ToDto(r)).ToList();
        }, ct);
    }

    public Task<RepositoryInfoDto> ConnectAsync(ConnectionProfile profile, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ValidateUrl(profile);
        if (string.IsNullOrWhiteSpace(profile.RepositoryId))
        {
            throw new CmisInvalidArgumentException(
                "Für den Verbindungsaufbau muss ein Repository (RepositoryId) angegeben werden.");
        }

        return _executor.RunAsync(() =>
        {
            var factory = SessionFactory.NewInstance();
            var parameters = BuildParameters(profile, includeRepositoryId: true);
            ISession session = CreateAdditionalHeadersProvider(profile) is { } provider
                ? factory.CreateSession(parameters, null, provider, null)
                : factory.CreateSession(parameters);

            // Der im Verbinden-Dialog gewaehlte Name springt ein, wenn der Server bei
            // getRepositoryInfo keinen liefert.
            var dto = CmisModelMapper.ToDto(session.RepositoryInfo, profile.RepositoryName);
            _context.Set(session, dto, profile);
            return dto;
        }, ct);
    }

    public Task DisconnectAsync(CancellationToken ct = default)
    {
        _context.Clear();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Liefert einen <see cref="HeaderInjectingAuthenticationProvider"/>, wenn das
    /// Profil zusaetzliche Header hinterlegt hat, sonst <c>null</c> - dann bleibt der
    /// bisherige, einfachere Aufruf (<c>factory.CreateSession(parameters)</c>) fuer
    /// alle Profile ohne zusaetzliche Header unveraendert.
    /// </summary>
    private static HeaderInjectingAuthenticationProvider? CreateAdditionalHeadersProvider(ConnectionProfile profile) =>
        profile.AdditionalHeaders.Count > 0 ? new HeaderInjectingAuthenticationProvider(profile.AdditionalHeaders) : null;

    private static void ValidateUrl(ConnectionProfile profile)
    {
        switch (profile.BindingType)
        {
            case CmisBindingType.AtomPub:
                if (string.IsNullOrWhiteSpace(profile.AtomPubUrl))
                {
                    throw new CmisInvalidArgumentException("Es wurde keine AtomPub-URL angegeben.");
                }
                break;
            case CmisBindingType.Browser:
            default:
                if (string.IsNullOrWhiteSpace(profile.BrowserUrl))
                {
                    throw new CmisInvalidArgumentException("Es wurde keine Browser-URL angegeben.");
                }
                break;
        }
    }

    private static IDictionary<string, string> BuildParameters(
        ConnectionProfile profile, bool includeRepositoryId)
    {
        var p = new Dictionary<string, string>
        {
            // GZip-Kompression der Serverantworten (Workbench: "Compression").
            [SessionParameter.Compression] = profile.Compression ? "true" : "false",
            // T9.1/FA-80: eigener HTTP-Invoker fuer das Roh-Protokoll der
            // Requests, binding-unabhaengig. PortCMIS instanziiert die Klasse
            // selbst (Activator.CreateInstance, kein DI-Hook moeglich); der
            // aktive IDiagnosticsLog wird ueber DiagnosticsLogAmbient
            // bereitgestellt (siehe LoggingHttpInvoker-Doku).
            [SessionParameter.HttpInvokerClass] = typeof(LoggingHttpInvoker).AssemblyQualifiedName!
        };

        switch (profile.BindingType)
        {
            case CmisBindingType.AtomPub:
                p[SessionParameter.BindingType] = BindingType.AtomPub;
                p[SessionParameter.AtomPubUrl] = profile.AtomPubUrl;
                // Kein CsrfHeader: der CMIS-1.1-CSRF-Schutz ist eine reine
                // Browser-Binding-Eigenschaft (schuetzt JSON-POSTs), bei AtomPub
                // ohne Bedeutung.
                break;
            case CmisBindingType.Browser:
            default:
                p[SessionParameter.BindingType] = BindingType.Browser;
                p[SessionParameter.BrowserUrl] = profile.BrowserUrl;
                if (!string.IsNullOrWhiteSpace(profile.CsrfHeader))
                {
                    p[SessionParameter.CsrfHeader] = profile.CsrfHeader!;
                }
                break;
        }

        // Authentifizierung, binding-unabhaengig. Achtung: PortCMIS'
        // StandardAuthenticationProvider waehlt Basic, sobald der User-Parameter
        // gesetzt ist (auch leer!), und nur dann Bearer, wenn KEIN User gesetzt
        // ist. Deshalb User/Password bei OAuth/None bewusst weglassen.
        switch (profile.Authentication)
        {
            case CmisAuthenticationType.Standard:
                p[SessionParameter.User] = profile.User ?? string.Empty;
                p[SessionParameter.Password] = profile.Password ?? string.Empty;
                // Praeemptiv senden: manche Server (z.B. Alfresco) antworten auf
                // 401 mit einem nicht standardkonformen WWW-Authenticate-Schema
                // (z.B. "AlfTicket"), das .NET's NetworkCredential-Challenge-
                // Response nicht erkennt. Ohne dieses Flag wird der Basic-Header
                // dann nie gesendet und die Anmeldung schlaegt trotz korrekter
                // Zugangsdaten fehl.
                p[SessionParameter.PreemptivAuthentication] = "true";
                break;
            case CmisAuthenticationType.OAuthBearer:
                if (!string.IsNullOrWhiteSpace(profile.BearerToken))
                {
                    p[SessionParameter.OAuthBearerToken] = profile.BearerToken;
                }
                break;
            case CmisAuthenticationType.None:
                break;
        }

        if (includeRepositoryId && !string.IsNullOrWhiteSpace(profile.RepositoryId))
        {
            p[SessionParameter.RepositoryId] = profile.RepositoryId!;
        }

        if (profile.ConnectTimeoutMs is int ct)
        {
            p[SessionParameter.ConnectTimeout] = ct.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (profile.ReadTimeoutMs is int rt)
        {
            p[SessionParameter.ReadTimeout] = rt.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return p;
    }
}
