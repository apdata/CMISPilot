using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using CMISPilot.Cmis;
using CMISPilot.Cmis.Profiles;
using APX.Wpf.Shell;
using APX.Wpf.Shell.Logging;
using APX.Wpf.Shell.ViewModels.Contracts;
using APX.Wpf.Shell.ViewModels.Logging;
using CMISPilot.Desktop.Dialogs;
using CMISPilot.Desktop.Services;
using CMISPilot.ViewModels.Workspace;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace CMISPilot.Desktop;

/// <summary>
/// Einstiegspunkt der neuen Präsentationsschicht (Redesign, R0). Baut den Generic
/// Host für DI und Logging auf und zeigt das <see cref="MainWindow"/> (RibbonWindow)
/// aus dem Container an.
///
/// Aufbau schrittweise: R1 Werkbank-Modell, R2 Themes, R3 Serilog-Wiring, R4 die
/// echten CMIS-Dienste (<c>AddCmis</c>) plus App-seitige Dienste (DPAPI, Datei-Launcher).
/// Der Bearbeiten-Dialog (IDialogService) folgt mit dem CRUD-Teil von R4.
/// </summary>
public partial class App : Application
{
    private readonly IHost _host;

    /// <summary>
    /// Dieselbe <see cref="LogFeed"/>-Instanz, die auch der Serilog-Sink
    /// (<see cref="ObservableLogSink"/>) füttert. Wird nach <c>AddWorkspace</c>
    /// explizit im Container registriert, damit Ausgabe- und Fehlerliste-Werkzeugfenster
    /// dieselbe Instanz sehen wie der Sink (dessen Konstruktion vor dem Container-Build
    /// erfolgen muss).
    /// </summary>
    private readonly LogFeed _logFeed = new();

    /// <summary>
    /// Plugins aus <c>Plugins/</c>. Wird vor dem Container-Aufbau geladen, weil die
    /// Plugins ihre Dienste selbst registrieren. Ohne Plugin-Verzeichnis bleibt die
    /// Liste leer und die Anwendung laeuft unveraendert — der Auslieferungsfall.
    /// </summary>
    private readonly Plugins.PluginLoader _pluginLoader = new();

    /// <summary>Erstellt den Host, hängt Serilog ein und registriert die Dienste.</summary>
    public App()
    {
        // Ohne diese Zeile rendern alle XAML-Bindungen mit StringFormat (Datum, Zahl)
        // fest in en-US, unabhaengig von der Windows-Ländereinstellung - WPFs
        // FrameworkElement.Language ist standardmaessig hart auf "en-US" gesetzt und
        // wird NICHT automatisch aus CultureInfo.CurrentCulture abgeleitet (bekannte
        // WPF-Falle). Einmaliges Ueberschreiben der Metadaten hier wirkt global fuer
        // die gesamte Anwendung, ohne dass jedes Element/jede Bindung es einzeln
        // setzen muesste. Getrennt von der geplanten Mehrsprachigkeit (siehe
        // CLAUDE.md, "## Mehrsprachigkeit"): dort geht es um die Sprache der UI-Texte
        // (CurrentUICulture/RESX), hier um das Zahlen-/Datumsformat (CurrentCulture),
        // das ohnehin am System haengt.
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));

        _pluginLoader.Load(Plugins.PluginLoader.GetDefaultDirectory());

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                // S5: gemeinsame Shell (Log-Strom, Ausgabe, Fehlerliste, Theme,
                // Dialoge, Datei-Start). Vor AddWorkspace, damit Ausgabe und
                // Fehlerliste in der Anzeigereihenfolge vor den CMIS-Werkzeugen
                // stehen.
                services.AddApxShell("CMISPilot");

                // R1: Werkbank-Modell (WorkspaceViewModel + CMIS-Werkzeugfenster).
                services.AddWorkspace();

                // R4: echte CMIS-Dienste (Browse/Object/Type/Query/Connection/Profile).
                services.AddCmis();

                // R4: App-seitige Dienste, die die Cmis-Library/VMs nur als Interface
                // kennen (plattformspezifisch, daher hier in der Präsentationsschicht).
                // Geheimnisschutz bleibt bewusst hier: das Interface gehoert zur
                // Cmis-Schicht (Profile), nicht zur Oberflaeche.
                services.AddSingleton<ISecretProtector, DpapiSecretProtector>();

                // R4 Etappe 4: Bearbeiten-/Anlegen-Dialog zusaetzlich zu den
                // generischen Dialogen der Shell. Ueberschreibt deren Registrierung
                // mit der abgeleiteten Fassung.
                services.AddSingleton<ViewModels.Shell.IDialogService, Dialogs.WpfDialogService>();
                services.AddSingleton<IDialogService>(
                    sp => sp.GetRequiredService<ViewModels.Shell.IDialogService>());

                // Ueberschreibt die ILogFeed-Registrierung aus AddApxShell mit der
                // konkreten Instanz, die der Serilog-Sink unten verwendet. Ohne das
                // saehen die Werkzeugfenster einen anderen Strom als den gefuellten.
                services.AddSingleton<ILogFeed>(_logFeed);

                services.AddSingleton<MainWindow>();

                // R4 Etappe 2: Verbinden-Dialog (pro Aufruf neu, damit Formular/Fehler
                // nicht zwischen Aufrufen hängen bleiben).
                services.AddTransient<ConnectDialog>();

                // Zuletzt: die Plugins registrieren ihre eigenen Dienste. Bewusst am
                // Ende, damit sie die Dienste des Hosts voraussetzen koennen, aber
                // nichts davon versehentlich ueberschreiben, worauf der Host selbst
                // baut. Ein Fehler in einem Plugin darf den Start nicht verhindern.
                foreach (var plugin in _pluginLoader.Plugins)
                {
                    try
                    {
                        plugin.ConfigureServices(services);
                    }
                    catch (Exception ex)
                    {
                        // Das Log steht hier noch nicht — Meldung sammeln, in
                        // OnStartup ausgeben.
                        _pluginLoader.AddMessage(
                            $"Plugin \"{plugin.Id}\": Dienste konnten nicht registriert werden: {ex.Message}");
                    }
                }
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();

                var logDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "CMISPilot", "logs");

#if DEBUG
                const LogEventLevel minimumLevel = LogEventLevel.Debug;
#else
                const LogEventLevel minimumLevel = LogEventLevel.Information;
#endif

                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Is(minimumLevel)
                    .WriteTo.File(
                        Path.Combine(logDirectory, "cmis-.log"),
                        rollingInterval: RollingInterval.Day)
                    .WriteTo.Sink(new ObservableLogSink(_logFeed, Dispatcher))
                    .CreateLogger();

                // Hängt Serilog unter Microsoft.Extensions.Logging ein, sodass
                // bestehender ILogger<T>-Code unverändert weiterfunktioniert.
                logging.AddSerilog(Log.Logger, dispose: true);
            })
            .Build();
    }

    /// <summary>Startet den Host und zeigt das Hauptfenster aus dem Container an.</summary>
    protected override async void OnStartup(StartupEventArgs e)
    {
        // Globaler Fehlerhaken: unbehandelte UI-Ausnahmen landen über Serilog in der
        // Log-Datei UND (gefiltert als Fehler) in der Fehlerliste.
        //
        // P2.6a (Plan-Plugin-Schnittstelle.md): bevor CMISPilot durch einen Fehler in
        // einem Plugin abstürzt, wird versucht, die Herkunft zu benennen — verwandelt
        // einen rätselhaften WPF-internen Stacktrace in "Fehler stammt aus Plugin X".
        //
        // P2.6b: bei Treffer zusätzlich versuchen, weiterzulaufen — das betroffene
        // Dokument-Tab schließen statt die ganze Anwendung mitzunehmen. Bewusst nur
        // *best effort*: nach einem Fehler mitten im Layout-Durchlauf kann die
        // Oberfläche in einem halben Zustand stehen. Gelingt das Schließen nicht
        // (kein aktives Dokument dieses Plugins), bleibt die Ausnahme unbehandelt.
        DispatcherUnhandledException += (_, args) =>
        {
            if (_pluginLoader.TryIdentifySource(args.Exception) is not { } plugin)
            {
                Log.Fatal(args.Exception, "Unbehandelte Ausnahme im UI-Thread");
                return;
            }

            Log.Fatal(
                args.Exception,
                "Unbehandelte Ausnahme im UI-Thread, vermutlich aus Plugin {PluginId} ({PluginName})",
                plugin.Id, plugin.Name);

            if (TryCloseAffectedPluginDocument(plugin))
            {
                Log.Warning(
                    "Fortgesetzt nach Plugin-Fehler: Dokument-Tab von Plugin {PluginId} geschlossen",
                    plugin.Id);
                args.Handled = true;
            }
        };

        await _host.StartAsync();

        // Jetzt steht das Log — nachholen, was der Loader vor dem Container gesammelt hat.
        var logger = _host.Services.GetRequiredService<ILogger<App>>();
        foreach (var message in _pluginLoader.Messages)
        {
            logger.LogInformation("Plugins: {Message}", message);
        }

        // Ressourcen-Beitraege der Plugins (DataTemplates) vor dem Fenster mischen,
        // sonst findet WPF beim ersten Anzeigen kein Template.
        var contributions = CollectPluginContributions(logger);
        MergePluginResources(contributions, logger);

        // R2: gespeichertes Theme anwenden, bevor das Fenster erscheint (kein Flackern).
        _host.Services.GetRequiredService<IThemeService>().Initialize();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.ApplyPluginContributions(contributions, logger);
        mainWindow.Show();

        base.OnStartup(e);
    }

    /// <summary>
    /// P2.6b: schließt das aktive Dokument-Tab, wenn es vom angegebenen Plugin
    /// stammt. Liefert <c>false</c> (nichts geschlossen), wenn kein aktives Dokument
    /// existiert oder es nicht diesem Plugin gehört — dann bleibt die Ausnahme
    /// unbehandelt, statt fälschlich einen Erfolg vorzutäuschen.
    /// </summary>
    private bool TryCloseAffectedPluginDocument(CMISPilot.Plugins.IPlugin plugin)
    {
        try
        {
            var workspace = _host.Services.GetRequiredService<APX.Wpf.Shell.ViewModels.Workspace.WorkspaceViewModel>();
            var active = workspace.ActiveDocument;

            if (active is null || _pluginLoader.TryIdentifyOwner(active) != plugin)
            {
                return false;
            }

            workspace.CloseDocumentCommand.Execute(active);
            return true;
        }
        catch (Exception ex)
        {
            // Best effort: schlaegt sogar das Schliessen fehl, bleibt die urspruengliche
            // Ausnahme unbehandelt (der Aufrufer prueft den Rueckgabewert).
            Log.Error(ex, "Plugin {PluginId}: betroffenes Dokument-Tab konnte nicht geschlossen werden", plugin.Id);
            return false;
        }
    }

    /// <summary>
    /// Fragt bei jedem Plugin die Beiträge ab. Ein Plugin, das dabei scheitert, wird
    /// übersprungen — die Anwendung läuft ohne dessen Oberfläche weiter.
    /// </summary>
    private List<CMISPilot.Plugins.PluginContributions> CollectPluginContributions(Microsoft.Extensions.Logging.ILogger logger)
    {
        var result = new List<CMISPilot.Plugins.PluginContributions>();

        foreach (var plugin in _pluginLoader.Plugins)
        {
            try
            {
                result.Add(plugin.GetContributions(_host.Services));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Plugin {PluginId}: Beiträge konnten nicht ermittelt werden", plugin.Id);
            }
        }

        return result;
    }

    /// <summary>
    /// Mischt die Ressourcen-Wörterbücher der Plugins in <see cref="Application.Resources"/>.
    /// Genau hier entscheidet sich, ob ein <c>pack://</c>-URI auf eine erst zur
    /// Laufzeit geladene Assembly aufgeht.
    /// </summary>
    private void MergePluginResources(
        IEnumerable<CMISPilot.Plugins.PluginContributions> contributions, Microsoft.Extensions.Logging.ILogger logger)
    {
        foreach (var uri in contributions.Select(c => c.ResourceDictionary).OfType<Uri>())
        {
            try
            {
                Resources.MergedDictionaries.Add(new ResourceDictionary { Source = uri });
                logger.LogDebug("Plugin-Ressourcen gemischt: {Uri}", uri);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Plugin-Ressourcen aus {Uri} konnten nicht geladen werden", uri);
            }
        }
    }

    /// <summary>Fährt den Host beim Beenden sauber herunter.</summary>
    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();

        base.OnExit(e);
    }
}
