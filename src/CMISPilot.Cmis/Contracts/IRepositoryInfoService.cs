using System.Threading;
using System.Threading.Tasks;
using CMISPilot.Cmis.Models;

namespace CMISPilot.Cmis.Contracts;

/// <summary>
/// Liefert die Informationen des aktuell verbundenen Repositories (FA-10/FA-11):
/// Eckdaten und Capabilities als DTO sowie dieselben Angaben als CMIS-JSON.
/// </summary>
public interface IRepositoryInfoService
{
    /// <summary>
    /// Die Repository-Information der aktiven Sitzung.
    /// </summary>
    /// <exception cref="Errors.CmisAppException">Wenn keine Verbindung besteht.</exception>
    Task<RepositoryInfoDto> GetRepositoryInfoAsync(CancellationToken ct = default);

    /// <summary>
    /// Dieselbe Information in der Darstellung des CMIS Browser Binding, eingerückt
    /// formatiert und damit lesbar speicherbar.
    ///
    /// <para>Erzeugt aus der eingelesenen Repository-Information, nicht mitgeschnitten:
    /// die Serverantwort selbst wird nirgends aufbewahrt, weil der HTTP-Abfangpunkt den
    /// Response-Stream bewusst ungelesen an PortCMIS durchreicht. Feldnamen und
    /// CMIS-Werte entsprechen der Spezifikation; die Ausgabe ist deshalb keine byteweise
    /// Kopie der Antwort, wohl aber dieselbe Information in derselben Form. Beim
    /// AtomPub-Binding, wo der Server XML liefert, ist dies die einzige Möglichkeit,
    /// überhaupt JSON zu erhalten.</para>
    /// </summary>
    /// <exception cref="Errors.CmisAppException">Wenn keine Verbindung besteht.</exception>
    Task<string> GetRepositoryInfoJsonAsync(CancellationToken ct = default);
}
