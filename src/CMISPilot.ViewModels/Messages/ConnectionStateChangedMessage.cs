namespace CMISPilot.ViewModels.Messages;

/// <summary>
/// Broadcast-Nachricht (CommunityToolkit-Messaging), die signalisiert, dass sich
/// der CMIS-Verbindungszustand geaendert hat (Connect/Disconnect). Sie traegt
/// bewusst keine Nutzlast: Empfaenger lesen den aktuellen Zustand aus dem
/// <see cref="Cmis.Contracts.ISessionContext"/> (Single Source of Truth).
///
/// Referenzmuster (M3): Wer eine Verbindung auf-/abbaut, sendet diese Nachricht
/// NACH dem await (also auf dem UI-Thread). So aktualisieren Shell-Top-Leiste und
/// Verbindungsbereich threadsicher, ohne dass die ViewModels WPF-Typen brauchen
/// (NFA-03) und ohne das serverseitige <c>ConnectionChanged</c>-Event vom
/// Hintergrund-Thread aus die UI anzufassen.
/// </summary>
public sealed record ConnectionStateChangedMessage;
