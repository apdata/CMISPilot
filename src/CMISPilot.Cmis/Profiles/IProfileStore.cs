using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CMISPilot.Cmis.Models;

namespace CMISPilot.Cmis.Profiles;

/// <summary>
/// Verwaltet lokal gespeicherte Verbindungsprofile (FA-04/NFA-07, M10/T10.1).
/// UI-frei und WPF-unabhängig (NFA-03), damit sowohl Settings- als auch
/// Verbindungsbereich dieselbe Quelle nutzen können.
/// </summary>
public interface IProfileStore
{
    /// <summary>
    /// Lädt alle gespeicherten Profile. Ein gespeichertes Passwort wird
    /// entschlüsselt zurückgegeben; ohne gespeichertes Passwort ist
    /// <see cref="ConnectionProfile.Password"/> leer.
    /// </summary>
    Task<IReadOnlyList<ConnectionProfile>> LoadAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Speichert (legt an oder überschreibt anhand des Namens) ein Profil.
    /// Das Passwort wird nur gespeichert, wenn <paramref name="savePassword"/>
    /// gesetzt ist – dann verschlüsselt über <see cref="ISecretProtector"/>
    /// (Konzept §7). Standardmäßig wird kein Passwort persistiert.
    /// </summary>
    Task SaveAsync(ConnectionProfile profile, bool savePassword, CancellationToken ct = default);

    /// <summary>Löscht ein Profil anhand seines Namens (fehlertolerant, falls nicht vorhanden).</summary>
    Task DeleteAsync(string name, CancellationToken ct = default);
}
