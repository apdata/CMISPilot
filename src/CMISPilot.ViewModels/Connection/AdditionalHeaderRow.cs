namespace CMISPilot.ViewModels.Connection;

/// <summary>
/// Eine Zeile im "Zusätzliche Header"-Grid des Verbinden-Dialogs (Name/Wert).
/// Bewusst eine einfache, nicht beobachtbare Klasse: das DataGrid schreibt bei der
/// Zelleneingabe direkt in diese Properties, eine Aktualisierung an anderer Stelle
/// der UI ist waehrend der Bearbeitung nicht noetig.
/// </summary>
public sealed class AdditionalHeaderRow
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
