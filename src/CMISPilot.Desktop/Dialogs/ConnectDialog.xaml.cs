using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CMISPilot.Cmis.Contracts;
using CMISPilot.Cmis.Errors;
using CMISPilot.Cmis.Models;
using CMISPilot.ViewModels.Connection;

namespace CMISPilot.Desktop.Dialogs;

/// <summary>
/// Verbinden-Dialog. Bindet an <see cref="ConnectDialogViewModel"/> (Profilliste,
/// Formularfelder, Repositories); das Passwort wird aus Sicherheitsgründen nicht
/// gebunden, sondern per Code-Behind aus der <see cref="PasswordBox"/> gelesen.
/// Der eigentliche Verbindungsaufbau erfolgt hier über
/// <see cref="IConnectionService.ConnectAsync"/>: bei Erfolg wird das Fenster mit
/// <see cref="Window.DialogResult"/> = <c>true</c> geschlossen.
/// </summary>
public partial class ConnectDialog : Window
{
    /// <summary>
    /// Angezeigt in <see cref="OAuthClientSecretBox"/>, solange ein gespeichertes
    /// Client-Secret existiert und noch nicht angefasst wurde - reine Anzeige, damit
    /// "leer lassen" sichtbar "unverändert lassen" statt "kein Secret vorhanden"
    /// bedeutet. Nie an <see cref="ConnectDialogViewModel.OAuthClientSecret"/> weitergereicht
    /// (siehe <see cref="_suppressOAuthClientSecretChanged"/>).
    /// </summary>
    private const string OAuthClientSecretPlaceholder = "••••••••";

    private readonly ConnectDialogViewModel _viewModel;
    private readonly IConnectionService _connectionService;
    private readonly ConnectionStatusViewModel _connectionStatus;

    /// <summary>Unterdrückt <see cref="OnOAuthClientSecretChanged"/>, während der Platzhalter programmatisch gesetzt wird.</summary>
    private bool _suppressOAuthClientSecretChanged;

    /// <summary>True, solange <see cref="OAuthClientSecretBox"/> noch den unangetasteten Platzhalter zeigt.</summary>
    private bool _isOAuthClientSecretPlaceholderShown;

    /// <param name="viewModel">Formular-, Profil- und Repository-Ladelogik (aus DI).</param>
    /// <param name="connectionService">Baut die eigentliche Verbindung auf.</param>
    /// <param name="connectionStatus">Statusleisten-VM; treibt die Fortschrittsanzeige beim Verbinden.</param>
    public ConnectDialog(
        ConnectDialogViewModel viewModel,
        IConnectionService connectionService,
        ConnectionStatusViewModel connectionStatus)
    {
        _viewModel = viewModel;
        _connectionService = connectionService;
        _connectionStatus = connectionStatus;
        DataContext = _viewModel;
        InitializeComponent();

        // Die PasswordBox ist nicht gebunden - beim Profilwechsel leert das VM
        // Password/BearerToken (Sicherheitsverhalten, muss neu eingegeben werden),
        // die sichtbare Box muss dem von Hand folgen.
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ConnectDialogViewModel.SelectedListEntry))
        {
            return;
        }

        PasswordBox.Clear();

        // Ein zuvor eingeblendetes Klartextfeld wieder verstecken, statt das
        // Secret des vorherigen Profils sichtbar stehen zu lassen.
        if (OAuthClientSecretRevealToggle.IsChecked == true)
        {
            OAuthClientSecretRevealToggle.IsChecked = false;
        }

        _suppressOAuthClientSecretChanged = true;
        _isOAuthClientSecretPlaceholderShown = _viewModel.HasStoredOAuthClientSecret;
        OAuthClientSecretBox.Password = _isOAuthClientSecretPlaceholderShown ? OAuthClientSecretPlaceholder : string.Empty;
        _suppressOAuthClientSecretChanged = false;
    }

    /// <summary>Reicht das Passwort aus der PasswordBox ans ViewModel weiter (nicht bindbar).</summary>
    private void OnPasswordChanged(object sender, RoutedEventArgs e) => _viewModel.Password = PasswordBox.Password;

    /// <summary>Reicht das OAuth-Client-Secret ans ViewModel weiter (nicht bindbar, analog Passwort).</summary>
    private void OnOAuthClientSecretChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressOAuthClientSecretChanged)
        {
            return;
        }

        // Sobald tatsaechlich getippt wird, gilt der Platzhalter nicht mehr.
        _isOAuthClientSecretPlaceholderShown = false;
        _viewModel.OAuthClientSecret = OAuthClientSecretBox.Password;
    }

    /// <summary>Klick in die maskierte Box, waehrend noch der Platzhalter steht: leeren statt die Punkte muehsam markieren zu lassen.</summary>
    private void OnOAuthClientSecretGotFocus(object sender, RoutedEventArgs e)
    {
        if (_isOAuthClientSecretPlaceholderShown)
        {
            _isOAuthClientSecretPlaceholderShown = false;
            OAuthClientSecretBox.Clear();
        }
    }

    /// <summary>Feld ohne Eingabe verlassen, obwohl ein Secret gespeichert ist: Platzhalter wiederherstellen (rein optisch).</summary>
    private void OnOAuthClientSecretLostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(OAuthClientSecretBox.Password) && _viewModel.HasStoredOAuthClientSecret)
        {
            _suppressOAuthClientSecretChanged = true;
            _isOAuthClientSecretPlaceholderShown = true;
            OAuthClientSecretBox.Password = OAuthClientSecretPlaceholder;
            _suppressOAuthClientSecretChanged = false;
        }
    }

    /// <summary>
    /// "Auge"-Button: blendet Klartext-TextBox und maskierte PasswordBox um. Beim
    /// Einblenden zeigt die TextBox das tatsaechlich wirksame Secret
    /// (<see cref="ConnectDialogViewModel.EffectiveOAuthClientSecret"/> - getipptes
    /// oder, falls nichts getippt wurde, das gespeicherte), nicht nur den Platzhalter.
    /// </summary>
    private void OnToggleOAuthClientSecretVisibility(object sender, RoutedEventArgs e)
    {
        if (OAuthClientSecretRevealToggle.IsChecked == true)
        {
            OAuthClientSecretTextBox.Text = _isOAuthClientSecretPlaceholderShown
                ? _viewModel.EffectiveOAuthClientSecret
                : OAuthClientSecretBox.Password;
            OAuthClientSecretBox.Visibility = Visibility.Collapsed;
            OAuthClientSecretTextBox.Visibility = Visibility.Visible;
            OAuthClientSecretTextBox.Focus();
        }
        else
        {
            _isOAuthClientSecretPlaceholderShown = false;
            OAuthClientSecretBox.Password = OAuthClientSecretTextBox.Text;
            OAuthClientSecretTextBox.Visibility = Visibility.Collapsed;
            OAuthClientSecretBox.Visibility = Visibility.Visible;
        }
    }

    /// <summary>Live-Eingabe in der Klartext-Ansicht ans ViewModel weiterreichen (analog PasswordChanged).</summary>
    private void OnOAuthClientSecretTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (OAuthClientSecretRevealToggle.IsChecked == true)
        {
            _isOAuthClientSecretPlaceholderShown = false;
            _viewModel.OAuthClientSecret = OAuthClientSecretTextBox.Text;
        }
    }

    /// <summary>
    /// Waehlt ein gespeichertes Profil in der Liste aus (R6.3, Schnellauswahl im
    /// Start-Ribbon) - derselbe Codepfad wie ein Klick in der Liste
    /// (<see cref="ConnectDialogViewModel.OnSelectedListEntryChanged"/>): Formular
    /// wird befuellt, Geheimnisse werden geleert. Laedt die Profilliste zuerst neu,
    /// falls der anfängliche Ladevorgang aus dem VM-Konstruktor noch nicht fertig ist.
    /// </summary>
    public async Task SelectSavedProfileAsync(string name)
    {
        await _viewModel.LoadProfilesCommand.ExecuteAsync(null);
        _viewModel.SelectedListEntry = _viewModel.ListEntries.FirstOrDefault(
            e => !e.IsNewEntry && string.Equals(e.Profile!.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Baut die Verbindung zum gewählten Repository auf und schließt bei Erfolg den Dialog.
    ///
    /// <para>Ist noch kein Repository gewählt, werden die Repositories zuerst geladen,
    /// statt den Nutzer auf die Schaltfläche „Repositories laden" zu verweisen. Steht
    /// danach genau eines zur Auswahl, gibt es nichts zu entscheiden und die Verbindung
    /// wird gleich aufgebaut; bei mehreren bleibt der Dialog offen, damit der Nutzer
    /// wählen kann.</para>
    /// </summary>
    private async void OnConnectClick(object sender, RoutedEventArgs e)
    {
        _viewModel.Password = PasswordBox.Password;

        if (_viewModel.SelectedRepository is null)
        {
            await _viewModel.LoadRepositoriesCommand.ExecuteAsync(null);

            if (_viewModel.Repositories.Count == 0)
            {
                _viewModel.ErrorMessage ??= "Auf diesem Server wurde kein Repository gefunden.";
                return;
            }

            if (_viewModel.Repositories.Count > 1)
            {
                // Mehrere zur Auswahl: der Nutzer entscheidet, ein zweiter Klick verbindet.
                _viewModel.ErrorMessage = "Bitte das gewünschte Repository auswählen und erneut auf „Verbinden“ klicken.";
                return;
            }
        }

        if (_viewModel.SelectedRepository is null)
        {
            return;
        }

        var profile = _viewModel.BuildProfile(PasswordBox.Password);

        ConnectButton.IsEnabled = false;
        using var _ = _connectionStatus.BeginBusy();
        try
        {
            await _connectionService.ConnectAsync(profile);
            DialogResult = true;
        }
        catch (CmisAppException ex)
        {
            _viewModel.ErrorMessage = Describe(ex);
        }
        finally
        {
            ConnectButton.IsEnabled = true;
        }
    }

    private static string Describe(CmisAppException ex) => ex.Kind switch
    {
        CmisErrorKind.Authentication => "Anmeldung fehlgeschlagen. Bitte Benutzername und Passwort prüfen.",
        CmisErrorKind.Network => "Server nicht erreichbar. Bitte URL und Netzwerk prüfen.",
        CmisErrorKind.NotFound => "Repository oder Ressource nicht gefunden.",
        CmisErrorKind.InvalidArgument => ex.Message,
        CmisErrorKind.PermissionDenied => "Zugriff verweigert. Fehlende Berechtigungen.",
        CmisErrorKind.NotSupported => "Die Operation wird vom Server nicht unterstützt.",
        _ => string.IsNullOrWhiteSpace(ex.Message) ? "Unerwarteter Serverfehler." : ex.Message
    };
}
