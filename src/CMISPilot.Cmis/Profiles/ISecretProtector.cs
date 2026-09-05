namespace CMISPilot.Cmis.Profiles;

/// <summary>
/// Kleine Abstraktion über die Verschlüsselung eines einzelnen Geheimnisses
/// (hier: gespeichertes Verbindungspasswort, FA-04/NFA-07). Die konkrete
/// Windows-DPAPI-Implementierung liegt in der App-Schicht
/// (<c>CMISPilot.App.Services.DpapiSecretProtector</c>), damit
/// <see cref="IProfileStore"/> selbst plattformunabhängig bleibt und mit
/// einem Fake-Protector testbar ist (T10.4).
/// </summary>
public interface ISecretProtector
{
    /// <summary>Verschlüsselt einen Klartext zu einer speicherbaren Zeichenkette.</summary>
    string Protect(string plainText);

    /// <summary>Entschlüsselt eine zuvor mit <see cref="Protect"/> erzeugte Zeichenkette.</summary>
    string Unprotect(string protectedText);
}
