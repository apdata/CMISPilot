using System.Windows;

namespace CMISPilot.Desktop.Dialogs;

/// <summary>
/// Schlanker Umbenennen-Dialog: alter Name vorbelegt und selektiert, neuer Name
/// eingeben, „Umbenennen"/Enter bestätigt, „Abbrechen"/Esc verwirft. Ersetzt den
/// zunächst versuchten Inline-Bearbeitungsmodus der Explorer-Liste (siehe
/// CLAUDE.md, Fallstricke: der Bearbeitungsmodus der Zelle ließ sich nicht
/// zuverlässig wieder verlassen).
/// </summary>
public partial class RenameDialog : Window
{
    public RenameDialog(string currentName)
    {
        InitializeComponent();
        NameTextBox.Text = currentName;
        Loaded += (_, _) =>
        {
            NameTextBox.Focus();
            NameTextBox.SelectAll();
        };
    }

    /// <summary>Der eingegebene neue Name, getrimmt. Nur gültig, wenn <see cref="Window.DialogResult"/> <c>true</c> ist.</summary>
    public string NewName => NameTextBox.Text.Trim();

    private void OnRenameClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameTextBox.Text))
        {
            return;
        }

        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
