using System.Windows.Media;
using APX.Wpf.Shell.ViewModels.Workspace;
using CMISPilot.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace SamplePlugin;

/// <summary>
/// Vorlage-Plugin: minimales, vollstaendiges Beispiel fuer alle Beitragspunkte
/// des Vertrags (<c>CMISPilot.Plugins.Abstractions</c>) — eine Start-Schaltflaeche,
/// ein kontextbezogener Ribbon-Tab, ein Dokument-Tab mit Command-Bindung, sowie
/// Host- und plugin-eigene Icons. Ursprünglich der P0-Spike, der den Beitragspunkt
/// erst bewiesen hat (siehe <c>Docs/status/P0-spike.md</c>); seit P3 dauerhafte
/// Vorlage statt Wegwerfcode.
/// </summary>
public sealed class SamplePlugin : IPlugin
{
    public string Id => "sample.plugin";
    public string Name => "Beispiel-Plugin";
    public Version Version => new(0, 1);

    /// <summary>Gegen diesen Vertrag gebaut — der Loader prueft die Hauptversion.</summary>
    public Version ContractVersion => PluginContract.Version;

    public void ConfigureServices(IServiceCollection services) =>
        services.AddTransient<SampleDocumentViewModel>();

    public PluginContributions GetContributions(IServiceProvider services) => new()
    {
        ResourceDictionary = ResourceUri,
        RibbonTabs = new[]
        {
            new RibbonTabContribution
            {
                ContextTabKey = "sample",
                GroupHeader = "Beispiel-Werkzeuge",
                AccentColor = Color.FromRgb(0xB0, 0x3A, 0x2B),
                ResourceDictionary = ResourceUri,
                ResourceKey = "SampleRibbonTab"
            }
        },
        DocumentCommands = new[]
        {
            new DocumentCommandContribution
            {
                Header = "Beispiel",
                // Icon aus dem Bestand des Hosts, ueber den Vertrag benannt.
                IconResourceKey = "Icon.Database",
                ToolTip = "Oeffnet den Dokument-Tab des Beispiel-Plugins",
                CreateDocument = () => services.GetRequiredService<SampleDocumentViewModel>()
            }
        }
    };

    /// <summary>
    /// pack://-URI auf das eigene Woerterbuch. Loest WPF diese URI fuer eine erst
    /// zur Laufzeit aus <c>Plugins/</c> geladene Assembly auf? Genau das hat der
    /// P0-Spike bewiesen.
    /// </summary>
    private static Uri ResourceUri { get; } =
        new("pack://application:,,,/SamplePlugin;component/Themes/SampleResources.xaml", UriKind.Absolute);
}
