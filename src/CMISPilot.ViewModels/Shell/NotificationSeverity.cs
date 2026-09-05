namespace CMISPilot.ViewModels.Shell;

/// <summary>
/// UI-neutrale Schweregrade fuer Benachrichtigungen (InfoBar). Wird in der View
/// auf die WPF-UI-Severity abgebildet, damit die ViewModels WPF-frei bleiben
/// (NFA-03, FA-81).
/// </summary>
public enum NotificationSeverity
{
    Info,
    Success,
    Warning,
    Error
}
