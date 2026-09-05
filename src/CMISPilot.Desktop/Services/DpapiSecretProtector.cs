using System.Security.Cryptography;
using System.Text;
using CMISPilot.Cmis.Profiles;

namespace CMISPilot.Desktop.Services;

/// <summary>
/// Windows-DPAPI-Implementierung von <see cref="ISecretProtector"/> (Konzept §7,
/// NFA-07): verschlüsselt/entschlüsselt an den aktuellen Windows-Benutzer gebunden
/// (<see cref="DataProtectionScope.CurrentUser"/>). Bewusst in der Präsentationsschicht,
/// damit <c>CMISPilot.Cmis</c> plattformunabhängig bleibt (die Library nutzt nur das
/// Interface). Übernommen aus der Alt-App (R4).
/// </summary>
public sealed class DpapiSecretProtector : ISecretProtector
{
    /// <summary>Verschlüsselt den Klartext benutzergebunden und liefert ihn Base64-kodiert.</summary>
    public string Protect(string plainText)
    {
        var bytes = Encoding.UTF8.GetBytes(plainText);
        var protectedBytes = ProtectedData.Protect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    /// <summary>Entschlüsselt einen zuvor mit <see cref="Protect"/> erzeugten Wert.</summary>
    public string Unprotect(string protectedText)
    {
        var protectedBytes = Convert.FromBase64String(protectedText);
        var bytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(bytes);
    }
}
