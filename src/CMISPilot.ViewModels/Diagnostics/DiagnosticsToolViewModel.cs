using APX.Wpf.Shell.ViewModels;
using APX.Wpf.Shell.ViewModels.Logging;
using APX.Wpf.Shell.ViewModels.Workspace;
using System.Collections.ObjectModel;
using CMISPilot.Cmis.Diagnostics;
using CMISPilot.ViewModels.Workspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CMISPilot.ViewModels.Diagnostics;

/// <summary>
/// Werkzeugfenster „Diagnose" (R5.3): zeigt das Diagnose-Protokoll
/// (<see cref="IDiagnosticsLog"/>) im unteren Andockbereich neben Ausgabe und
/// Fehlerliste (R3) — Serveroperationen (Kategorie "Executor") und, sofern
/// verbunden, Roh-HTTP-Requests des Browser Bindings (Kategorie "HTTP"), FA-80.
/// Logik aus <c>DiagnosticsAreaViewModel</c> (entfernt) (M9) übernommen und nach
/// dem in R3 etablierten Muster (<see cref="Logging.OutputToolViewModel"/>) auf
/// <see cref="ToolViewModelBase"/> umgestellt. Ersetzt den Platzhalter „Konsole"
/// aus R1 (<see cref="WorkspaceServiceCollectionExtensions"/>).
///
/// Rein lesend, WPF-frei (NFA-03); der Log selbst wird vom
/// <see cref="CMISPilot.Cmis.Execution.CmisExecutor"/> bzw.
/// <see cref="LoggingHttpInvoker"/> befüllt.
/// </summary>
public sealed partial class DiagnosticsToolViewModel : ToolViewModelBase
{
    private readonly IDiagnosticsLog _log;

    public DiagnosticsToolViewModel(IDiagnosticsLog log)
        : base("tool:diagnostics", ToolDock.Bottom)
    {
        _log = log;
        Title = "Diagnose";

        RefreshEntries();
    }

    /// <summary>Protokolleinträge, neueste zuerst (praktischer als chronologisch aufsteigend).</summary>
    public ObservableCollection<DiagnosticsLogEntry> Entries { get; } = new();

    public bool HasEntries => Entries.Count > 0;

    /// <summary>Aktuell im Grid selektierter Eintrag; treibt die ein-/ausblendbare Detailansicht.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private DiagnosticsLogEntry? _selectedEntry;

    public bool HasSelection => SelectedEntry is not null;

    /// <summary>Lädt die aktuelle Momentaufnahme aus dem <see cref="IDiagnosticsLog"/> (T9.2 „Aktualisieren").</summary>
    [RelayCommand]
    private void RefreshEntries()
    {
        var snapshot = _log.GetEntries();

        SelectedEntry = null;
        Entries.Clear();
        // neueste zuerst
        for (var i = snapshot.Count - 1; i >= 0; i--)
        {
            Entries.Add(snapshot[i]);
        }

        OnPropertyChanged(nameof(HasEntries));
    }

    /// <summary>Leert das Protokoll (T9.2 „Leeren"-Button).</summary>
    [RelayCommand]
    private void ClearEntries()
    {
        _log.Clear();
        RefreshEntries();
    }
}
