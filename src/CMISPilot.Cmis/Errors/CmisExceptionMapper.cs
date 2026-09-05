using System;
using System.Net.Http;
using System.Net.Sockets;
using P = PortCMIS.Exceptions;

namespace CMISPilot.Cmis.Errors;

/// <summary>
/// Bildet PortCMIS- und Netzwerk-Exceptions auf die fachlichen
/// <see cref="CmisAppException"/>-Typen ab (T1.5). Zentral genutzt vom
/// <c>CmisExecutor</c>, sodass jede Serveraktion automatisch saubere Fehler liefert.
/// </summary>
public static class CmisExceptionMapper
{
    /// <summary>
    /// Wandelt eine beliebige Exception in eine <see cref="CmisAppException"/> um.
    /// <see cref="OperationCanceledException"/> wird unverändert weitergereicht,
    /// damit Abbruch (CancellationToken) korrekt als Abbruch behandelt wird.
    /// </summary>
    public static Exception Map(Exception ex)
    {
        switch (ex)
        {
            case OperationCanceledException:
            case CmisAppException:
                return ex; // Abbruch bzw. bereits gemappt: unverändert lassen.

            // Authentifizierung (Unauthorized ist Unterklasse von RuntimeException -> zuerst prüfen)
            case P.CmisUnauthorizedException:
            case P.CmisProxyAuthenticationException:
                return new CmisAuthException(
                    "Authentifizierung fehlgeschlagen. Bitte Benutzername und Passwort prüfen.", ex);

            case P.CmisPermissionDeniedException:
                return new CmisPermissionException(
                    "Zugriff verweigert. Für diese Aktion fehlen die Berechtigungen.", ex);

            // Netzwerk / Erreichbarkeit
            case P.CmisConnectionException:
            case P.CmisServiceUnavailableException:
                return new CmisNetworkException(
                    "Der Server ist nicht erreichbar. Bitte URL und Netzwerk prüfen.", ex);

            case P.CmisObjectNotFoundException:
                return new CmisNotFoundException("Das angeforderte Objekt wurde nicht gefunden.", ex);

            case P.CmisConstraintException:
            case P.CmisNameConstraintViolationException:
            case P.CmisContentAlreadyExistsException:
                return new CmisConstraintException(
                    "Die Operation verletzt eine Einschränkung des Repositories: " + ex.Message, ex);

            case P.CmisNotSupportedException:
            case P.CmisStreamNotSupportedException:
                return new CmisNotSupportedException(
                    "Diese Operation wird vom Repository nicht unterstützt.", ex);

            case P.CmisInvalidArgumentException:
            case P.CmisFilterNotValidException:
                return new CmisInvalidArgumentException(
                    "Ungültige Angabe: " + ex.Message, ex);

            case P.CmisUpdateConflictException:
            case P.CmisVersioningException:
                return new CmisConflictException(
                    "Konflikt: Das Objekt wurde zwischenzeitlich geändert.", ex);

            case P.CmisStorageException:
            case P.CmisRuntimeException:
            case P.CmisInvalidServerDataException:
                return new CmisServerException(
                    "Der Server hat einen Fehler gemeldet: " + ex.Message, ex);

            // Rohe Netzwerkfehler unterhalb von PortCMIS
            case HttpRequestException:
            case SocketException:
            case TimeoutException:
                return new CmisNetworkException(
                    "Der Server ist nicht erreichbar oder hat nicht rechtzeitig geantwortet.", ex);

            default:
                return new CmisServerException("Unerwarteter Fehler: " + ex.Message, ex);
        }
    }
}
