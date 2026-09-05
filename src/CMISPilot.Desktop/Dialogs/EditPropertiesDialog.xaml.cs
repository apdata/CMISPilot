using System.Windows;
using CMISPilot.ViewModels.Dialogs;

namespace CMISPilot.Desktop.Dialogs;

/// <summary>
/// Separater Bearbeiten-/Anlegen-Dialog für Properties (R4 Etappe 4, FA-70/71/72).
/// Übernimmt <see cref="EditPropertiesViewModel"/> nur bei „Speichern" (nach
/// erfolgreicher Validierung); „Abbrechen" verwirft die Eingaben. Portiert aus der
/// Alt-App (<c>CMISPilot.App.Dialogs.EditPropertiesDialog</c>), dabei die
/// WPF-UI-Controls durch Standard-WPF-Controls ersetzt. Die Dialog-VM
/// (<see cref="EditPropertiesViewModel"/>) bleibt unverändert (WPF-frei, NFA-03).
/// </summary>
public partial class EditPropertiesDialog : Window
{
    public EditPropertiesDialog(EditPropertiesViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private EditPropertiesViewModel ViewModel => (EditPropertiesViewModel)DataContext;

    /// <summary>
    /// F1: wählt die Datei, die beim Anlegen eines Dokuments gleich als Inhalt
    /// mitgespeichert wird. Der Dateidialog ist ein reines View-Belang (analog
    /// PasswordBox-Muster), das ViewModel haelt nur den Pfad (WPF-frei, NFA-03).
    /// </summary>
    private void OnPickContentFileClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Datei als Inhalt des Dokuments wählen",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            ViewModel.ContentFilePath = dialog.FileName;
        }
    }

    /// <summary>Verwirft die gewählte Datei; das Dokument wird dann ohne Inhalt angelegt.</summary>
    private void OnClearContentFileClick(object sender, RoutedEventArgs e) =>
        ViewModel.ContentFilePath = null;

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.Validate())
        {
            return;
        }

        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
