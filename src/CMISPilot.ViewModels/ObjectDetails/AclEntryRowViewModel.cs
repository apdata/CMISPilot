namespace CMISPilot.ViewModels.ObjectDetails;

/// <summary>Eine ACL-Zeile der erweiterten Objektdetails (R6.1).</summary>
public sealed class AclEntryRowViewModel
{
    public string PrincipalId { get; init; } = string.Empty;

    /// <summary>Berechtigungen, mit ", " zusammengefügt (z. B. "cmis:read, cmis:write").</summary>
    public string Permissions { get; init; } = string.Empty;

    public bool IsDirect { get; init; }
}
