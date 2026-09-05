using CMISPilot.ViewModels.Shell;

namespace CMISPilot.ViewModels.Messages;

/// <summary>
/// Bittet die Shell, eine globale Benachrichtigung in der InfoBar anzuzeigen
/// (FA-81). Bereichs-ViewModels senden diese Nachricht ueber den
/// <see cref="CommunityToolkit.Mvvm.Messaging.IMessenger"/>, statt die Shell
/// direkt zu referenzieren – so bleiben die Bereiche entkoppelt (Referenzmuster
/// fuer M4–M8).
/// </summary>
public sealed record NotificationRequestMessage(
    NotificationSeverity Severity,
    string Title,
    string Message);
