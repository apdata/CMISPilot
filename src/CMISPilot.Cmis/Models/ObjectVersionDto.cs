using System;

namespace CMISPilot.Cmis.Models;

/// <summary>
/// UI-freundliche Repräsentation einer Version innerhalb einer Versionsreihe
/// (R6.1). Eigene Abbildung von PortCMIS <c>IDocument</c> (Ausschnitt der
/// versionierungsrelevanten Properties, siehe <c>IDocumentProperties</c>).
/// </summary>
public sealed class ObjectVersionDto
{
    /// <summary>Objekt-ID dieser konkreten Version.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Versionskennzeichen (CMIS-Property <c>cmis:versionLabel</c>).</summary>
    public string? VersionLabel { get; init; }

    public bool? IsLatestVersion { get; init; }
    public bool? IsMajorVersion { get; init; }
    public bool? IsLatestMajorVersion { get; init; }

    public string? CreatedBy { get; init; }
    public DateTimeOffset? CreationDate { get; init; }
    public string? LastModifiedBy { get; init; }
    public DateTimeOffset? LastModificationDate { get; init; }

    /// <summary>Checkin-Kommentar dieser Version, sofern vorhanden.</summary>
    public string? CheckinComment { get; init; }

    public long? ContentStreamLength { get; init; }
    public string? ContentStreamFileName { get; init; }
}
