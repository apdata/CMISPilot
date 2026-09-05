using System.Threading.Tasks;
using CMISPilot.ViewModels.Dialogs;

namespace CMISPilot.ViewModels.Shell;

/// <summary>
/// Dialoge von CMISPilot. Erbt Rückfrage, Öffnen und Speichern aus der Shell und
/// ergänzt den Bearbeiten-/Anlegen-Dialog für Properties (FA-70/71/72).
///
/// <para>Dieser eine Zusatz ist auch der Grund, warum es das Interface hier noch
/// gibt: er hängt an <see cref="EditPropertiesViewModel"/> und damit an CMIS.
/// In der Shell wäre er fehl am Platz.</para>
///
/// <para>Bereichs-ViewModels injizieren weiterhin nur dieses eine Interface und
/// bekommen beides.</para>
/// </summary>
public interface IDialogService : APX.Wpf.Shell.ViewModels.Contracts.IDialogService
{
    /// <summary>
    /// Zeigt den separaten Bearbeiten-/Anlegen-Dialog für Properties (FA-70/71/72).
    /// Liefert <c>true</c> nur bei „Speichern" (nach erfolgreicher Validierung der
    /// Pflichtfelder/Datentypen); bei „Abbrechen" <c>false</c>. Übernommen wird
    /// <paramref name="vm"/> nur, wenn <c>true</c> geliefert wird.
    /// </summary>
    Task<bool> ShowEditPropertiesAsync(EditPropertiesViewModel vm);

    /// <summary>
    /// Zeigt den schlanken Umbenennen-Dialog, vorbelegt mit <paramref name="currentName"/>.
    /// Liefert den neuen Namen bei „Umbenennen", <c>null</c> bei „Abbrechen".
    /// </summary>
    Task<string?> ShowRenameDialogAsync(string currentName);
}
