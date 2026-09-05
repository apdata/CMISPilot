namespace CMISPilot.ViewModels.Explorer;

/// <summary>Art eines Knotens im Server-Baum (R4 Etappe 2).</summary>
public enum TreeNodeKind
{
    /// <summary>Wurzelknoten: der verbundene Server.</summary>
    Server,

    /// <summary>Repository-Knoten unterhalb des Servers.</summary>
    Repository,

    /// <summary>Ordnerknoten (CMIS-Ordner) unterhalb des Repository- oder eines anderen Ordnerknotens.</summary>
    Folder
}
