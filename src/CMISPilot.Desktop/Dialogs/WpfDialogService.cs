using System.Threading.Tasks;
using System.Windows;
using CMISPilot.ViewModels.Dialogs;
using CMISPilot.ViewModels.Shell;

namespace CMISPilot.Desktop.Dialogs;

/// <summary>
/// Dialoge von CMISPilot. Rückfrage, Öffnen und Speichern kommen aus der Shell;
/// hier kommt nur der Bearbeiten-/Anlegen-Dialog dazu (R4 Etappe 4).
/// </summary>
public sealed class WpfDialogService : APX.Wpf.Shell.Dialogs.WpfDialogService, IDialogService
{
    /// <inheritdoc />
    public Task<bool> ShowEditPropertiesAsync(EditPropertiesViewModel vm)
    {
        var dialog = new EditPropertiesDialog(vm) { Owner = Application.Current?.MainWindow };
        return Task.FromResult(dialog.ShowDialog() == true);
    }

    /// <inheritdoc />
    public Task<string?> ShowRenameDialogAsync(string currentName)
    {
        var dialog = new RenameDialog(currentName) { Owner = Application.Current?.MainWindow };
        return Task.FromResult(dialog.ShowDialog() == true ? dialog.NewName : null);
    }

    /// <summary>
    /// In CMISPilot ist jede Rückfrage eine Löschbestätigung (Objekt löschen,
    /// Profil löschen). Deshalb durchgängig die rote Bestätigung mit der
    /// Beschriftung „Löschen" statt des neutralen „Ja" aus der Shell.
    /// </summary>
    public override Task<bool> ConfirmAsync(string title, string message) =>
        ConfirmAsync(title, message, "Löschen", "Abbrechen", isDestructive: true);
}
