using APX.Wpf.Shell.ViewModels;
using APX.Wpf.Shell.ViewModels.Logging;
using APX.Wpf.Shell.ViewModels.Workspace;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CMISPilot.Cmis.Connection;
using CMISPilot.Cmis.Contracts;
using CMISPilot.Cmis.Errors;
using CMISPilot.Cmis.Models;
using CMISPilot.Cmis.Profiles;
using CMISPilot.ViewModels.Shell;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace CMISPilot.ViewModels.Connection;

/// <summary>
/// ViewModel des Verbinden-Dialogs. Laedt die auf dem Server verfuegbaren
/// Repositories, stellt die Formularfelder bereit und verwaltet zusaetzlich die
/// gespeicherten Verbindungsprofile (Liste, Neu/Duplizieren/Loeschen/Speichern) –
/// bis zum Redesign des Verbinden-Dialogs lag das getrennt im
/// Backstage-Tab „Profile" (<c>ProfileManagerViewModel</c>, seitdem entfernt).
/// Ein FTP-Client-Login-Dialog (WinSCP/FileZilla-Stil): links
/// <see cref="ListEntries"/> mit einem angepinnten „Neues Verbindungsziel"-Eintrag,
/// rechts das Formular des jeweils gewaehlten Eintrags.
///
/// Das Passwort wird bewusst NICHT als bindbare Property gefuehrt
/// (<c>PasswordBox.Password</c> ist aus Sicherheitsgruenden nicht bindbar): das
/// Fenster setzt es imperativ ueber <see cref="Password"/> und uebergibt es
/// zusaetzlich direkt an <see cref="BuildProfile"/>. Der eigentliche
/// Verbindungsaufbau (<c>IConnectionService.ConnectAsync</c>) liegt bewusst weiter
/// beim Aufrufer (Fenster, <c>ConnectDialog.OnConnectClick</c>) statt in einem
/// VM-Kommando – so bleibt es beim bisherigen Aufteilungs-Schnitt (Repository-
/// Nachladen vor dem eigentlichen Verbindungsaufbau ist Fenster-Orchestrierung)
/// statt zwei unterschiedliche Verbindungs-Codepfade im selben Dialog zu haben.
/// </summary>
public sealed partial class ConnectDialogViewModel : ViewModelBase
{
    private readonly IConnectionService _connectionService;
    private readonly IProfileStore _profileStore;
    private readonly IDialogService _dialogService;
    private readonly ILogger<ConnectDialogViewModel> _logger;

    /// <summary>Der angepinnte "Neues Verbindungsziel"-Eintrag, immer an erster Stelle von <see cref="ListEntries"/>.</summary>
    private readonly ProfileListEntry _newEntry = new();

    /// <summary>
    /// Das tatsaechliche, entschluesselte Geheimnis des gewaehlten Profils (leer bei
    /// "Neu" oder wenn keines gespeichert war). <see cref="Password"/>/<see cref="BearerToken"/>
    /// werden beim Profilwechsel aus Sicherheitsgruenden im Formular geleert (muss zum
    /// Aendern neu eingegeben werden) - ohne diesen Rueckfall wuerde <see cref="BuildProfile"/>
    /// dann mit leerem Geheimnis verbinden/speichern, obwohl eines hinterlegt ist.
    /// </summary>
    private string _storedPassword = string.Empty;
    private string _storedBearerToken = string.Empty;
    private string _storedOAuthClientSecret = string.Empty;

    /// <param name="connectionService">Dient dem Laden der verfuegbaren Repositories und dem Verbindungsaufbau.</param>
    /// <param name="profileStore">Laedt/speichert/loescht Profile (JSON, DPAPI-Passwort).</param>
    /// <param name="dialogService">Zeigt die Loeschbestaetigung.</param>
    /// <param name="logger">Meldet Erfolg/Fehler (landet in Ausgabe/Fehlerliste).</param>
    public ConnectDialogViewModel(
        IConnectionService connectionService,
        IProfileStore profileStore,
        IDialogService dialogService,
        ILogger<ConnectDialogViewModel> logger)
    {
        _connectionService = connectionService;
        _profileStore = profileStore;
        _dialogService = dialogService;
        _logger = logger;

        ListEntries.Add(_newEntry);
        SelectedListEntry = _newEntry;

        _ = LoadProfilesAsync(CancellationToken.None);
    }

    /// <summary>Verfuegbare Bindings (fuer die Auswahl im Dialog).</summary>
    public System.Collections.Generic.IReadOnlyList<CmisBindingType> BindingTypes { get; } =
        new[] { CmisBindingType.Browser, CmisBindingType.AtomPub };

    /// <summary>Gewaehltes Binding (Standard: Browser Binding).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadRepositoriesCommand))]
    [NotifyPropertyChangedFor(nameof(IsBrowserBinding))]
    [NotifyPropertyChangedFor(nameof(IsAtomPubBinding))]
    private CmisBindingType _bindingType = CmisBindingType.Browser;

    /// <summary>True, wenn das Browser-Binding-URL-Feld (und CSRF-Header) gilt.</summary>
    public bool IsBrowserBinding => BindingType == CmisBindingType.Browser;

    /// <summary>True, wenn das AtomPub-URL-Feld gilt.</summary>
    public bool IsAtomPubBinding => BindingType == CmisBindingType.AtomPub;

    /// <summary>Browser-Binding-URL des Servers.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadRepositoriesCommand))]
    private string _browserUrl = string.Empty;

    /// <summary>AtomPub-Binding-URL des Servers.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadRepositoriesCommand))]
    private string _atomPubUrl = string.Empty;

    /// <summary>Verfuegbare Authentifizierungsarten (fuer die Auswahl im Dialog).</summary>
    public System.Collections.Generic.IReadOnlyList<CmisAuthenticationType> AuthenticationTypes { get; } =
        new[] { CmisAuthenticationType.None, CmisAuthenticationType.Standard, CmisAuthenticationType.OAuthBearer };

    /// <summary>Gewaehlte Authentifizierungsart (Standard = Basic).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStandardAuth))]
    [NotifyPropertyChangedFor(nameof(IsOAuthBearer))]
    private CmisAuthenticationType _authentication = CmisAuthenticationType.Standard;

    /// <summary>True, wenn Benutzer/Passwort-Felder gelten (Basic).</summary>
    public bool IsStandardAuth => Authentication == CmisAuthenticationType.Standard;

    /// <summary>True, wenn das Bearer-Token-Feld gilt (OAuth 2.0).</summary>
    public bool IsOAuthBearer => Authentication == CmisAuthenticationType.OAuthBearer;

    /// <summary>Benutzername fuer Basic Auth.</summary>
    [ObservableProperty]
    private string _user = string.Empty;

    /// <summary>
    /// Passwort. Nur zur Laufzeit im Speicher, vom Fenster-Code-Behind ueber die
    /// PasswordBox gesetzt (kein XAML-Binding).
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>OAuth-2.0-Bearer-Token (bei <see cref="CmisAuthenticationType.OAuthBearer"/>).</summary>
    [ObservableProperty]
    private string _bearerToken = string.Empty;

    /// <summary>Authorization-Endpoint des Identity Providers (OAuth-Anmeldung, Tab "OAuth").</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginWithOAuthCommand))]
    private string _oAuthAuthorizationEndpoint = string.Empty;

    /// <summary>Token-Endpoint des Identity Providers (Code-gegen-Token-Tausch).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginWithOAuthCommand))]
    private string _oAuthTokenEndpoint = string.Empty;

    /// <summary>Client-ID der bei OAuth registrierten CMISPilot-Anwendung.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginWithOAuthCommand))]
    private string _oAuthClientId = string.Empty;

    /// <summary>
    /// OAuth-Client-Secret. Nur zur Laufzeit im Speicher, vom Fenster-Code-Behind ueber
    /// eine PasswordBox gesetzt (kein XAML-Binding, analog <see cref="Password"/>).
    /// </summary>
    public string OAuthClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// True, wenn fuer das gewaehlte Profil ein Client-Secret hinterlegt ist (ohne den
    /// Wert selbst preiszugeben) - das Fenster zeigt dann einen Platzhalter statt eines
    /// komplett leeren Feldes, damit sichtbar ist, dass "leer lassen" hier "unveraendert
    /// lassen" bedeutet und nicht "kein Secret vorhanden".
    /// </summary>
    public bool HasStoredOAuthClientSecret => !string.IsNullOrEmpty(_storedOAuthClientSecret);

    /// <summary>
    /// Das Client-Secret, das tatsaechlich verwendet wuerde, wenn jetzt gespeichert/
    /// verbunden wird: das neu eingetippte, oder in Ermangelung dessen das gespeicherte.
    /// Nur fuer den "Auge"-Button im OAuth-Tab (Klartext anzeigen) - nicht sonst binden.
    /// </summary>
    public string EffectiveOAuthClientSecret =>
        string.IsNullOrEmpty(OAuthClientSecret) ? _storedOAuthClientSecret : OAuthClientSecret;

    /// <summary>
    /// Redirect-URI fuer den OAuth-Login, muss "http://localhost:PORT/pfad" sein
    /// (siehe <see cref="Cmis.Connection.OAuthAuthorizationCodeFlow"/>). Nur beim
    /// Authorization-Code-Flow relevant, siehe <see cref="OAuthUseClientCredentials"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginWithOAuthCommand))]
    private string _oAuthRedirectUri = string.Empty;

    /// <summary>
    /// True: OAuth-Token per Client-Credentials-Flow holen (Client-ID/Secret direkt
    /// gegen ein Token tauschen, kein Browser-Login noetig - repraesentiert die
    /// Anwendung/einen Service-Account statt eines einzelnen Nutzers). False
    /// (Standard): interaktiver Authorization-Code-Flow mit Login im System-Browser.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginWithOAuthCommand))]
    [NotifyPropertyChangedFor(nameof(IsOAuthAuthorizationCodeFlow))]
    [NotifyPropertyChangedFor(nameof(OAuthLoginButtonLabel))]
    private bool _oAuthUseClientCredentials;

    /// <summary>Kehrwert von <see cref="OAuthUseClientCredentials"/> fuer Sichtbarkeits-Bindungen im OAuth-Tab.</summary>
    public bool IsOAuthAuthorizationCodeFlow => !OAuthUseClientCredentials;

    /// <summary>Beschriftung des Anmelden-Buttons je nach gewaehltem OAuth-Flow.</summary>
    public string OAuthLoginButtonLabel =>
        OAuthUseClientCredentials ? "Token via Client Credentials holen" : "Bei Identity Provider anmelden";

    /// <summary>GZip-Kompression der Serverantworten (Standard: an).</summary>
    [ObservableProperty]
    private bool _compression = true;

    /// <summary>Optionaler CSRF-Header-Name (leer = keiner).</summary>
    [ObservableProperty]
    private string _csrfHeader = string.Empty;

    /// <summary>Verbindungs-Timeout in Sekunden, vorbelegt mit der ueblichen Vorgabe.</summary>
    [ObservableProperty]
    private int? _connectTimeoutSeconds = ConnectionProfile.DefaultConnectTimeoutMs / 1000;

    /// <summary>
    /// Lese-Timeout in Sekunden, vorbelegt mit der ueblichen Vorgabe. Wirkungslos,
    /// solange PortCMIS <c>SessionParameter.ReadTimeout</c> nicht auswertet
    /// (siehe <see cref="ConnectionProfile.DefaultReadTimeoutMs"/>).
    /// </summary>
    [ObservableProperty]
    private int? _readTimeoutSeconds = ConnectionProfile.DefaultReadTimeoutMs / 1000;

    /// <summary>Auf dem Server verfuegbare Repositories (aus <see cref="LoadRepositoriesAsync"/>).</summary>
    public ObservableCollection<RepositoryInfoDto> Repositories { get; } = new();

    /// <summary>Im Dialog gewaehltes Repository.</summary>
    [ObservableProperty]
    private RepositoryInfoDto? _selectedRepository;

    /// <summary>Fehlermeldung fuer die Anzeige im Dialog (null, wenn keine).</summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// Zusaetzliche statische HTTP-Header (Name/Wert), die bei jeder Anfrage
    /// mitgeschickt werden - Formularspiegel von <see cref="ConnectionProfile.AdditionalHeaders"/>
    /// fuer das Grid im Tab "Zusätzliche Header".
    /// </summary>
    public ObservableCollection<AdditionalHeaderRow> AdditionalHeaders { get; } = new();

    // --- Profilverwaltung (frueher ProfileManagerViewModel, Backstage-Tab "Profile") ---

    /// <summary>Bezeichnung des Profils. Pflichtfeld nur beim Speichern.</summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>Geheimnis (Passwort/Bearer-Token) DPAPI-verschluesselt merken.</summary>
    [ObservableProperty]
    private bool _savePassword;

    /// <summary>Alle gespeicherten Profile, wie sie <see cref="IProfileStore"/> liefert.</summary>
    public ObservableCollection<ConnectionProfile> SavedProfiles { get; } = new();

    /// <summary>
    /// Bindungsquelle der Profilliste im Dialog: die gespeicherten Profile plus
    /// einen angepinnten "Neues Verbindungsziel"-Eintrag an erster Stelle.
    /// </summary>
    public ObservableCollection<ProfileListEntry> ListEntries { get; } = new();

    /// <summary>Der in der Liste gewaehlte Eintrag.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DuplicateCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private ProfileListEntry? _selectedListEntry;

    partial void OnSelectedListEntryChanged(ProfileListEntry? value)
    {
        if (value is null || value.IsNewEntry)
        {
            New();
            return;
        }

        var profile = value.Profile!;

        ErrorMessage = null;
        Password = string.Empty;
        BearerToken = string.Empty;
        _storedPassword = profile.Password ?? string.Empty;
        _storedBearerToken = profile.BearerToken ?? string.Empty;
        OAuthClientSecret = string.Empty;
        _storedOAuthClientSecret = profile.OAuthClientSecret ?? string.Empty;
        OAuthAuthorizationEndpoint = profile.OAuthAuthorizationEndpoint ?? string.Empty;
        OAuthTokenEndpoint = profile.OAuthTokenEndpoint ?? string.Empty;
        OAuthClientId = profile.OAuthClientId ?? string.Empty;
        OAuthRedirectUri = profile.OAuthRedirectUri ?? string.Empty;
        OAuthUseClientCredentials = profile.OAuthUseClientCredentials;

        Name = profile.Name ?? string.Empty;
        BindingType = profile.BindingType;
        BrowserUrl = profile.BrowserUrl;
        AtomPubUrl = profile.AtomPubUrl;
        Authentication = profile.Authentication;
        User = profile.User;
        Compression = profile.Compression;
        CsrfHeader = profile.CsrfHeader ?? string.Empty;
        ConnectTimeoutSeconds = profile.ConnectTimeoutMs is int cm
            ? cm / 1000 : ConnectionProfile.DefaultConnectTimeoutMs / 1000;
        ReadTimeoutSeconds = profile.ReadTimeoutMs is int rm
            ? rm / 1000 : ConnectionProfile.DefaultReadTimeoutMs / 1000;
        // Ein gespeichertes Geheimnis (Passwort, Bearer-Token oder OAuth-Client-Secret) haelt den Haken.
        SavePassword = !string.IsNullOrEmpty(profile.Password) || !string.IsNullOrEmpty(profile.BearerToken) ||
            !string.IsNullOrEmpty(profile.OAuthClientSecret);

        AdditionalHeaders.Clear();
        foreach (var header in profile.AdditionalHeaders)
        {
            AdditionalHeaders.Add(new AdditionalHeaderRow { Name = header.Name, Value = header.Value });
        }

        // Ohne dies zeigt die Repository-Combo nach einem Profilwechsel nichts an,
        // bis erneut "Repositories laden" gedrueckt wird - mit gespeicherter
        // RepositoryId/RepositoryName kann das Dropdown das aber sofort anzeigen.
        Repositories.Clear();
        if (!string.IsNullOrEmpty(profile.RepositoryId))
        {
            var repo = new RepositoryInfoDto { Id = profile.RepositoryId, Name = profile.RepositoryName ?? profile.RepositoryId };
            Repositories.Add(repo);
            SelectedRepository = repo;
        }
        else
        {
            SelectedRepository = null;
        }
    }

    /// <summary>Laedt die gespeicherten Profile neu (initial und nach Aenderungen).</summary>
    [RelayCommand]
    public async Task LoadProfilesAsync(CancellationToken ct)
    {
        using (BeginBusy())
        {
            try
            {
                var profiles = await _profileStore.LoadAllAsync(ct).ConfigureAwait(true);

                SavedProfiles.Clear();
                foreach (var profile in profiles)
                {
                    SavedProfiles.Add(profile);
                }

                RebuildListEntries();
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private void RebuildListEntries()
    {
        var previouslySelected = SelectedListEntry?.Profile?.Name;

        ListEntries.Clear();
        ListEntries.Add(_newEntry);
        foreach (var profile in SavedProfiles)
        {
            ListEntries.Add(new ProfileListEntry { Profile = profile });
        }

        SelectedListEntry = previouslySelected is null
            ? _newEntry
            : ListEntries.FirstOrDefault(
                e => string.Equals(e.Profile?.Name, previouslySelected, StringComparison.OrdinalIgnoreCase))
              ?? _newEntry;
    }

    /// <summary>Leert das Formular fuer ein neues Profil.</summary>
    [RelayCommand]
    private void New()
    {
        SelectedListEntry = _newEntry;
        Name = string.Empty;
        BindingType = CmisBindingType.Browser;
        BrowserUrl = string.Empty;
        AtomPubUrl = string.Empty;
        Authentication = CmisAuthenticationType.Standard;
        User = string.Empty;
        Password = string.Empty;
        BearerToken = string.Empty;
        _storedPassword = string.Empty;
        _storedBearerToken = string.Empty;
        OAuthAuthorizationEndpoint = string.Empty;
        OAuthTokenEndpoint = string.Empty;
        OAuthClientId = string.Empty;
        OAuthClientSecret = string.Empty;
        _storedOAuthClientSecret = string.Empty;
        OAuthRedirectUri = string.Empty;
        OAuthUseClientCredentials = false;
        SavePassword = false;
        Compression = true;
        CsrfHeader = string.Empty;
        ConnectTimeoutSeconds = ConnectionProfile.DefaultConnectTimeoutMs / 1000;
        ReadTimeoutSeconds = ConnectionProfile.DefaultReadTimeoutMs / 1000;
        Repositories.Clear();
        SelectedRepository = null;
        AdditionalHeaders.Clear();
        ErrorMessage = null;
    }

    /// <summary>Speichert das Formular als Profil (Anlegen bei neuem Namen, Ueberschreiben bei bestehendem).</summary>
    [RelayCommand]
    private async Task SaveAsync(CancellationToken ct)
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Bitte einen Profilnamen angeben.";
            return;
        }

        var profile = BuildProfile(Password);

        using (BeginBusy())
        {
            try
            {
                await _profileStore.SaveAsync(profile, SavePassword, ct).ConfigureAwait(true);
            }
            catch (ArgumentException ex)
            {
                ErrorMessage = ex.Message;
                return;
            }
        }

        _logger.LogInformation("Das Profil \"{Name}\" wurde gespeichert.", profile.Name);
        await LoadProfilesAsync(ct).ConfigureAwait(true);

        // RebuildListEntries() (in LoadProfilesAsync) haelt nur eine *bereits*
        // gewaehlte Selektion ueber den Namen am Leben - beim Speichern eines neuen
        // Profils war vorher aber der angepinnte Eintrag selektiert, nicht dieses
        // Profil. Deshalb hier explizit auf das gerade gespeicherte Profil setzen.
        SelectedListEntry = ListEntries.FirstOrDefault(
            e => string.Equals(e.Profile?.Name, profile.Name, StringComparison.OrdinalIgnoreCase)) ?? _newEntry;
    }

    /// <summary>Uebernimmt das gewaehlte Profil als neues, noch nicht gespeichertes Formular mit Namenszusatz.</summary>
    [RelayCommand(CanExecute = nameof(HasRealSelection))]
    private void Duplicate()
    {
        if (SelectedListEntry?.Profile is not { } source)
        {
            return;
        }

        var copyName = $"{source.Name} (Kopie)";
        New();
        Name = copyName;
        BindingType = source.BindingType;
        BrowserUrl = source.BrowserUrl;
        AtomPubUrl = source.AtomPubUrl;
        Authentication = source.Authentication;
        User = source.User;
        Compression = source.Compression;
        CsrfHeader = source.CsrfHeader ?? string.Empty;
        ConnectTimeoutSeconds = source.ConnectTimeoutMs is int cm
            ? cm / 1000 : ConnectionProfile.DefaultConnectTimeoutMs / 1000;
        ReadTimeoutSeconds = source.ReadTimeoutMs is int rm
            ? rm / 1000 : ConnectionProfile.DefaultReadTimeoutMs / 1000;

        // Kein Geheimnis (anders als Password/BearerToken/OAuthClientSecret, die
        // New() bereits geleert hat) - beim Duplizieren sinnvollerweise mit uebernehmen.
        OAuthAuthorizationEndpoint = source.OAuthAuthorizationEndpoint ?? string.Empty;
        OAuthTokenEndpoint = source.OAuthTokenEndpoint ?? string.Empty;
        OAuthClientId = source.OAuthClientId ?? string.Empty;
        OAuthRedirectUri = source.OAuthRedirectUri ?? string.Empty;
        OAuthUseClientCredentials = source.OAuthUseClientCredentials;

        foreach (var header in source.AdditionalHeaders)
        {
            AdditionalHeaders.Add(new AdditionalHeaderRow { Name = header.Name, Value = header.Value });
        }
    }

    /// <summary>Loescht das gewaehlte Profil nach Rueckfrage.</summary>
    [RelayCommand(CanExecute = nameof(HasRealSelection))]
    private async Task DeleteAsync(CancellationToken ct)
    {
        if (SelectedListEntry?.Profile is not { } target)
        {
            return;
        }

        var confirmed = await _dialogService.ConfirmAsync(
            "Profil löschen",
            $"Soll das Profil \"{target.Name}\" endgültig gelöscht werden?").ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        using (BeginBusy())
        {
            await _profileStore.DeleteAsync(target.Name!, ct).ConfigureAwait(true);
        }

        _logger.LogInformation("Das Profil \"{Name}\" wurde gelöscht.", target.Name);
        New();
        await LoadProfilesAsync(ct).ConfigureAwait(true);
    }

    private bool HasRealSelection() => SelectedListEntry is { IsNewEntry: false };

    /// <summary>Listet die Repositories des Servers, ohne eine dauerhafte Session aufzubauen.</summary>
    [RelayCommand(CanExecute = nameof(CanLoadRepositories))]
    private async Task LoadRepositoriesAsync(CancellationToken ct)
    {
        ErrorMessage = null;
        var profile = BuildProfile(Password);

        using (BeginBusy())
        {
            try
            {
                var repos = await _connectionService.GetRepositoriesAsync(profile, ct).ConfigureAwait(true);

                Repositories.Clear();
                foreach (var repo in repos)
                {
                    Repositories.Add(repo);
                }

                // Bei genau einem Repository ist die Vorauswahl die richtige Antwort;
                // bei mehreren ist sie nur ein Startpunkt, den der Nutzer aendert.
                SelectedRepository = Repositories.FirstOrDefault();
            }
            catch (OperationCanceledException)
            {
            }
            catch (CmisAppException ex)
            {
                ErrorMessage = Describe(ex);
            }
        }
    }

    private bool CanLoadRepositories() =>
        !string.IsNullOrWhiteSpace(IsAtomPubBinding ? AtomPubUrl : BrowserUrl) && !IsBusy;

    /// <summary>
    /// Fuehrt den OAuth-Authorization-Code-Flow durch (Tab "OAuth"): oeffnet den
    /// System-Browser zum Login beim Identity Provider und fuellt bei Erfolg
    /// <see cref="BearerToken"/> automatisch mit dem erhaltenen Access Token.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanLoginWithOAuth))]
    private async Task LoginWithOAuthAsync(CancellationToken ct)
    {
        ErrorMessage = null;

        using (BeginBusy())
        {
            try
            {
                var flow = new OAuthAuthorizationCodeFlow();
                var clientSecret = string.IsNullOrEmpty(OAuthClientSecret) ? _storedOAuthClientSecret : OAuthClientSecret;
                BearerToken = OAuthUseClientCredentials
                    ? await flow.GetClientCredentialsTokenAsync(
                        OAuthTokenEndpoint.Trim(), OAuthClientId.Trim(), clientSecret, ct).ConfigureAwait(true)
                    : await flow.GetAccessTokenAsync(
                        OAuthAuthorizationEndpoint.Trim(),
                        OAuthTokenEndpoint.Trim(),
                        OAuthClientId.Trim(),
                        clientSecret,
                        OAuthRedirectUri.Trim(),
                        ct).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
            }
            catch (InvalidOperationException ex)
            {
                ErrorMessage = ex.Message;
            }
        }
    }

    /// <summary>
    /// Client-Credentials braucht nur Token-Endpoint + Client-ID (Secret optional -
    /// mancher Provider laesst auch ein leeres Secret zu); der interaktive
    /// Authorization-Code-Flow zusaetzlich Authorization-Endpoint + Redirect-URI.
    /// </summary>
    private bool CanLoginWithOAuth() =>
        !IsBusy &&
        !string.IsNullOrWhiteSpace(OAuthTokenEndpoint) &&
        !string.IsNullOrWhiteSpace(OAuthClientId) &&
        (OAuthUseClientCredentials ||
            (!string.IsNullOrWhiteSpace(OAuthAuthorizationEndpoint) && !string.IsNullOrWhiteSpace(OAuthRedirectUri)));

    /// <summary>
    /// Baut das Verbindungsprofil aus den Formularfeldern und dem uebergebenen Passwort.
    /// Ist <paramref name="password"/> leer (Nutzer hat nach einem Profilwechsel nichts
    /// neu eingetippt) bzw. ist <see cref="BearerToken"/> leer, greift der Rueckfall auf
    /// das tatsaechlich gespeicherte Geheimnis des gewaehlten Profils
    /// (<see cref="_storedPassword"/>/<see cref="_storedBearerToken"/>) - sonst wuerde
    /// weder Verbinden noch erneutes Speichern ohne Retippen funktionieren.
    /// </summary>
    public ConnectionProfile BuildProfile(string password) => new()
    {
        Name = string.IsNullOrWhiteSpace(Name) ? null : Name.Trim(),
        BindingType = BindingType,
        BrowserUrl = BrowserUrl.Trim(),
        AtomPubUrl = AtomPubUrl.Trim(),
        Authentication = Authentication,
        User = User,
        Password = string.IsNullOrEmpty(password) ? _storedPassword : password,
        BearerToken = string.IsNullOrEmpty(BearerToken) ? _storedBearerToken : BearerToken,
        OAuthAuthorizationEndpoint = OAuthAuthorizationEndpoint.Trim(),
        OAuthTokenEndpoint = OAuthTokenEndpoint.Trim(),
        OAuthClientId = OAuthClientId.Trim(),
        OAuthClientSecret = string.IsNullOrEmpty(OAuthClientSecret) ? _storedOAuthClientSecret : OAuthClientSecret,
        OAuthRedirectUri = OAuthRedirectUri.Trim(),
        OAuthUseClientCredentials = OAuthUseClientCredentials,
        Compression = Compression,
        // CSRF-Header ist eine reine Browser-Binding-Eigenschaft; bei AtomPub
        // bewusst weglassen, auch wenn das Feld noch etwas enthaelt.
        CsrfHeader = IsBrowserBinding && !string.IsNullOrWhiteSpace(CsrfHeader) ? CsrfHeader.Trim() : null,
        ConnectTimeoutMs = ConnectTimeoutSeconds is int cs ? cs * 1000 : null,
        ReadTimeoutMs = ReadTimeoutSeconds is int rs ? rs * 1000 : null,
        RepositoryId = SelectedRepository?.Id,
        // Den Anzeigenamen mitgeben: er steht in der Auswahlliste, kommt aber nicht
        // zwingend aus getRepositoryInfo zurueck (siehe ConnectionProfile.RepositoryName).
        RepositoryName = SelectedRepository?.Name,
        // Zeilen mit leerem Namen (z. B. eine vom DataGrid automatisch angehaengte
        // leere Neu-Zeile) fliegen raus, statt als Header mit leerem Namen zu enden.
        AdditionalHeaders = AdditionalHeaders
            .Where(h => !string.IsNullOrWhiteSpace(h.Name))
            .Select(h => new HttpHeaderEntry(h.Name.Trim(), h.Value))
            .ToList()
    };

    // IsBusy (aus ViewModelBase) wirkt auf CanExecute; der generierte OnIsBusyChanged-
    // Hook liegt in der Basisklasse und ist hier nicht implementierbar (M3-Referenzmuster).
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(IsBusy))
        {
            LoadRepositoriesCommand.NotifyCanExecuteChanged();
            DuplicateCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
            LoginWithOAuthCommand.NotifyCanExecuteChanged();
        }
    }

    private static string Describe(CmisAppException ex) => ex.Kind switch
    {
        CmisErrorKind.Authentication => "Anmeldung fehlgeschlagen. Bitte Benutzername und Passwort pruefen.",
        CmisErrorKind.Network => "Server nicht erreichbar. Bitte URL und Netzwerk pruefen.",
        CmisErrorKind.NotFound => "Repository oder Ressource nicht gefunden.",
        CmisErrorKind.InvalidArgument => ex.Message,
        CmisErrorKind.PermissionDenied => "Zugriff verweigert. Fehlende Berechtigungen.",
        CmisErrorKind.NotSupported => "Die Operation wird vom Server nicht unterstuetzt.",
        _ => string.IsNullOrWhiteSpace(ex.Message) ? "Unerwarteter Serverfehler." : ex.Message
    };
}
