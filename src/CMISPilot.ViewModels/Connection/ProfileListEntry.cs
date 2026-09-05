using CMISPilot.Cmis.Models;

namespace CMISPilot.ViewModels.Connection;

/// <summary>
/// Ein Eintrag der Profilliste im Verbinden-Dialog (<see cref="ConnectDialogViewModel.ListEntries"/>).
/// Ein Wrapper statt direkt <see cref="ConnectionProfile"/>, damit der angepinnte
/// „Neues Verbindungsziel"-Eintrag (<see cref="IsNewEntry"/>) sich sauber von den
/// echten, in <see cref="ConnectDialogViewModel.SavedProfiles"/> gehaltenen Profilen
/// trennen lässt, ohne dort ein Sentinel-Objekt einzuschleusen.
/// </summary>
public sealed class ProfileListEntry
{
    /// <summary>Das gewählte Profil, oder <c>null</c> für den angepinnten "Neu"-Eintrag.</summary>
    public ConnectionProfile? Profile { get; init; }

    /// <summary>True für den angepinnten Eintrag, der ein neues, ungespeichertes Formular startet.</summary>
    public bool IsNewEntry => Profile is null;

    /// <summary>Anzeigetext in der Liste.</summary>
    public string DisplayName => Profile?.Name ?? "Neues Verbindungsziel";
}
