using CMISPilot.Cmis.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CMISPilot.ViewModels.Connection;

/// <summary>
/// Bildet den aktuellen Verbindungszustand fuer die Statusbar ab (R4 Etappe 2).
/// Abonniert <see cref="ISessionContext.ConnectionChanged"/> und spiegelt danach
/// die aktuellen Werte aus dem <see cref="ISessionContext"/> (Single Source of
/// Truth). Als Singleton registriert, WPF-frei (NFA-03).
/// </summary>
public sealed partial class ConnectionStatusViewModel : ObservableObject
{
    private readonly ISessionContext _sessionContext;

    /// <param name="sessionContext">Haelt den Zustand der aktiven CMIS-Verbindung.</param>
    public ConnectionStatusViewModel(ISessionContext sessionContext)
    {
        _sessionContext = sessionContext;
        _sessionContext.ConnectionChanged += (_, _) => Refresh();
        Refresh();
    }

    /// <summary>True, wenn aktuell eine Verbindung besteht.</summary>
    [ObservableProperty]
    private bool _isConnected;

    /// <summary>Kurztext fuer die Statusbar ("Verbunden"/"Nicht verbunden").</summary>
    [ObservableProperty]
    private string _statusText = "Nicht verbunden";

    /// <summary>Name des aktuell verbundenen Repositories, sonst "-".</summary>
    [ObservableProperty]
    private string _repositoryName = "-";

    /// <summary>
    /// True, solange ein Verbindungsvorgang laeuft (Verbinden/Trennen). Treibt die
    /// unbestimmte Fortschrittsanzeige in der Statusleiste.
    /// </summary>
    [ObservableProperty]
    private bool _isBusy;

    private int _busyDepth;

    /// <summary>
    /// Markiert den Beginn eines laufenden Vorgangs. Bis das zurueckgegebene
    /// <see cref="IDisposable"/> freigegeben ist, meldet <see cref="IsBusy"/> True.
    /// Ueberlappende Aufrufe werden gezaehlt; erst der letzte setzt zurueck.
    /// Nur aus dem UI-Thread aufrufen.
    /// </summary>
    public IDisposable BeginBusy()
    {
        _busyDepth++;
        IsBusy = true;
        return new BusyReleaser(this);
    }

    private void EndBusy()
    {
        if (_busyDepth > 0)
        {
            _busyDepth--;
        }

        if (_busyDepth == 0)
        {
            IsBusy = false;
        }
    }

    /// <summary>Liest den aktuellen Zustand aus dem <see cref="ISessionContext"/> nach.</summary>
    private void Refresh()
    {
        IsConnected = _sessionContext.IsConnected;
        StatusText = IsConnected ? "Verbunden" : "Nicht verbunden";
        // IsNullOrWhiteSpace statt ??: ein leerer Name vom Server soll wie ein
        // fehlender behandelt werden, sonst steht hier ein leeres Feld.
        var repository = _sessionContext.CurrentRepository;
        RepositoryName = repository is null ? "-"
            : string.IsNullOrWhiteSpace(repository.Name) ? repository.Id : repository.Name;
    }

    private sealed class BusyReleaser(ConnectionStatusViewModel owner) : IDisposable
    {
        private bool _released;

        public void Dispose()
        {
            if (_released)
            {
                return;
            }

            _released = true;
            owner.EndBusy();
        }
    }
}
