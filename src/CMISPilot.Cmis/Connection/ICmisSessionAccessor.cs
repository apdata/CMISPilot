using PortCMIS.Client;

namespace CMISPilot.Cmis.Connection;

/// <summary>
/// Library-interner Zugriff auf die aktive PortCMIS-Session. Bewusst
/// <c>internal</c>, damit die PortCMIS-Typen nicht nach außen lecken (NFA-03a).
/// Sibling-Dienste (Browse/Query/Type/Object) beziehen ihre Session hierüber.
/// </summary>
internal interface ICmisSessionAccessor
{
    /// <summary>Aktive PortCMIS-Session oder null, wenn getrennt.</summary>
    ISession? Session { get; }

    /// <summary>Liefert die aktive Session oder wirft, wenn keine Verbindung besteht.</summary>
    ISession RequireSession();
}
