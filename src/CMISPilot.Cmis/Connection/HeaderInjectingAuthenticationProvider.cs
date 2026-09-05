using System.Net.Http;
using CMISPilot.Cmis.Models;
using PortCMIS.Binding;

namespace CMISPilot.Cmis.Connection;

/// <summary>
/// Fuegt zusaetzliche, statische HTTP-Header (<see cref="ConnectionProfile.AdditionalHeaders"/>)
/// in jede Anfrage ein, unabhaengig von der gewaehlten Authentifizierungsart.
///
/// <para>PortCMIS' <see cref="StandardAuthenticationProvider"/> kennt nur Basic/Bearer/
/// CSRF - fuer beliebige Header gibt es kein <c>SessionParameter</c>-Aequivalent, der
/// einzige Erweiterungspunkt ist <see cref="IPortableAuthenticationProvider.PrepareHttpRequestMessage"/>.
/// Erbt bewusst von <see cref="StandardAuthenticationProvider"/> statt direkt von
/// <see cref="AbstractAuthenticationProvider"/>, damit Basic/Bearer/CSRF-Verhalten
/// unveraendert erhalten bleiben - diese Klasse ergaenzt nur, ersetzt nichts.</para>
/// </summary>
public sealed class HeaderInjectingAuthenticationProvider(IReadOnlyList<HttpHeaderEntry> headers)
    : StandardAuthenticationProvider
{
    public override void PrepareHttpRequestMessage(HttpRequestMessage httpRequestMessage)
    {
        base.PrepareHttpRequestMessage(httpRequestMessage);

        foreach (var header in headers)
        {
            if (string.IsNullOrWhiteSpace(header.Name))
            {
                continue;
            }

            // Remove+TryAdd statt Add: idempotent, falls PortCMIS dieselbe Anfrage
            // erneut vorbereitet (z. B. bei einem Retry), und TryAddWithoutValidation
            // akzeptiert auch Header-Namen/-Werte, die HttpHeaders sonst strikt ablehnt.
            httpRequestMessage.Headers.Remove(header.Name);
            httpRequestMessage.Headers.TryAddWithoutValidation(header.Name, header.Value);
        }
    }
}
