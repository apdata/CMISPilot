using System;
using CMISPilot.Cmis.Models;

namespace CMISPilot.Cmis.Errors;

/// <summary>
/// Basis für alle fachlichen Fehler der CMIS-Library. Kapselt PortCMIS-Exceptions,
/// damit die UI-Schichten stabile, verständliche Fehlertypen erhalten (FA-05, NFA-06)
/// und keine PortCMIS-Typen nach außen lecken (NFA-03a).
/// </summary>
public class CmisAppException : Exception
{
    public CmisErrorKind Kind { get; }

    public CmisAppException(CmisErrorKind kind, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
    }
}

/// <summary>Authentifizierung fehlgeschlagen (falsche Zugangsdaten, 401).</summary>
public sealed class CmisAuthException : CmisAppException
{
    public CmisAuthException(string message, Exception? inner = null)
        : base(CmisErrorKind.Authentication, message, inner) { }
}

/// <summary>Server nicht erreichbar / Netzwerkfehler (falsche URL, Timeout).</summary>
public sealed class CmisNetworkException : CmisAppException
{
    public CmisNetworkException(string message, Exception? inner = null)
        : base(CmisErrorKind.Network, message, inner) { }
}

/// <summary>Objekt/Ressource nicht gefunden.</summary>
public sealed class CmisNotFoundException : CmisAppException
{
    public CmisNotFoundException(string message, Exception? inner = null)
        : base(CmisErrorKind.NotFound, message, inner) { }
}

/// <summary>Constraint-/Namensverletzung (z. B. beim Anlegen/Ändern).</summary>
public sealed class CmisConstraintException : CmisAppException
{
    public CmisConstraintException(string message, Exception? inner = null)
        : base(CmisErrorKind.Constraint, message, inner) { }
}

/// <summary>Zugriff verweigert (Berechtigungen).</summary>
public sealed class CmisPermissionException : CmisAppException
{
    public CmisPermissionException(string message, Exception? inner = null)
        : base(CmisErrorKind.PermissionDenied, message, inner) { }
}

/// <summary>Vom Server/Repository nicht unterstützte Operation.</summary>
public sealed class CmisNotSupportedException : CmisAppException
{
    public CmisNotSupportedException(string message, Exception? inner = null)
        : base(CmisErrorKind.NotSupported, message, inner) { }
}

/// <summary>Ungültiges Argument / fehlerhafte Anfrage.</summary>
public sealed class CmisInvalidArgumentException : CmisAppException
{
    public CmisInvalidArgumentException(string message, Exception? inner = null)
        : base(CmisErrorKind.InvalidArgument, message, inner) { }
}

/// <summary>Update-Konflikt (veraltete Version, gleichzeitige Änderung).</summary>
public sealed class CmisConflictException : CmisAppException
{
    public CmisConflictException(string message, Exception? inner = null)
        : base(CmisErrorKind.Conflict, message, inner) { }
}

/// <summary>Allgemeiner Server-/Laufzeitfehler ohne spezifischere Kategorie.</summary>
public sealed class CmisServerException : CmisAppException
{
    public CmisServerException(string message, Exception? inner = null)
        : base(CmisErrorKind.Server, message, inner) { }
}
