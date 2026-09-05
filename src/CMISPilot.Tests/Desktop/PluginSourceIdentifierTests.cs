using CMISPilot.Desktop.Plugins;
using CMISPilot.Plugins;

namespace CMISPilot.Tests.Desktop;

/// <summary>
/// Tests für <see cref="PluginSourceIdentifier"/>. Die zweite Erkennung
/// (Typname im Ausnahmetext) ist kein Vorgriff, sondern eine Nachbesserung: die
/// erste Fassung (nur Stapelrahmen) identifizierte genau den Fehlerfall nicht,
/// bei dem ein Bindungsfehler im <c>DataTemplate</c> eines Plugins ausschließlich
/// innerhalb von WPF-Interna wirft, sodass kein Stapelrahmen dem Plugin gehört.
/// </summary>
public sealed class PluginSourceIdentifierTests
{
    private sealed class FakePlugin : IPlugin
    {
        public string Id => "fake.plugin";
        public string Name => "Fake-Plugin";
        public Version Version { get; } = new(1, 0);
        public Version ContractVersion => PluginContract.Version;
        public void ConfigureServices(Microsoft.Extensions.DependencyInjection.IServiceCollection services) { }
        public PluginContributions GetContributions(IServiceProvider services) => new();
    }

    /// <summary>Platzhalter-Typ, dessen voll qualifizierter Name im Ausnahmetext gesucht wird.</summary>
    private sealed class FakePluginDocumentViewModel
    {
    }

    [Fact]
    public void UeberStapelrahmen_FindetPluginWennDessenCodeWirftAusnahme()
    {
        var plugin = new FakePlugin();
        var pluginAssemblies = new Dictionary<IPlugin, System.Reflection.Assembly>
        {
            // Die eigene Testassembly steht stellvertretend für eine Plugin-Assembly:
            // die hier ausgeloeste Ausnahme traegt tatsaechlich einen Stapelrahmen
            // aus dieser Assembly (ThrowFromThisAssembly unten).
            [plugin] = typeof(PluginSourceIdentifierTests).Assembly
        };

        Exception caught;
        try
        {
            ThrowFromThisAssembly();
            throw new InvalidOperationException("Sollte nicht erreicht werden.");
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        var found = PluginSourceIdentifier.TryIdentify(caught, pluginAssemblies);

        Assert.Same(plugin, found);
    }

    [Fact]
    public void OhneStapelrahmenAberMitTypnameImText_FindetPluginUeberDenAusnahmetext()
    {
        var plugin = new FakePlugin();
        var pluginAssemblies = new Dictionary<IPlugin, System.Reflection.Assembly>
        {
            [plugin] = typeof(FakePluginDocumentViewModel).Assembly
        };

        // Nachgestellter Wortlaut der echten WPF-Meldung: kein Code des Plugins auf
        // dem Stapel, nur der Typname im Text.
        var exception = new InvalidOperationException(
            $"TwoWay- oder OneWayToSource-Bindungen funktionieren nicht mit der " +
            $"schreibgeschützten Eigenschaft \"ClickCount\" vom Typ " +
            $"\"{typeof(FakePluginDocumentViewModel).FullName}\".");

        var found = PluginSourceIdentifier.TryIdentify(exception, pluginAssemblies);

        Assert.Same(plugin, found);
    }

    [Fact]
    public void WederStapelrahmenNochTypnameImText_LiefertNull()
    {
        var plugin = new FakePlugin();
        var pluginAssemblies = new Dictionary<IPlugin, System.Reflection.Assembly>
        {
            [plugin] = typeof(FakePluginDocumentViewModel).Assembly
        };

        var exception = new InvalidOperationException("Unabhängiger Fehler ohne jeden Bezug.");

        var found = PluginSourceIdentifier.TryIdentify(exception, pluginAssemblies);

        Assert.Null(found);
    }

    [Fact]
    public void PrueftAuchInnereAusnahmen()
    {
        var plugin = new FakePlugin();
        var pluginAssemblies = new Dictionary<IPlugin, System.Reflection.Assembly>
        {
            [plugin] = typeof(FakePluginDocumentViewModel).Assembly
        };

        var inner = new InvalidOperationException(
            $"…vom Typ \"{typeof(FakePluginDocumentViewModel).FullName}\".");
        var outer = new InvalidOperationException("Äußere, generische WPF-Meldung.", inner);

        var found = PluginSourceIdentifier.TryIdentify(outer, pluginAssemblies);

        Assert.Same(plugin, found);
    }

    private static void ThrowFromThisAssembly() =>
        throw new InvalidOperationException("Testausnahme aus dieser Assembly.");

    [Fact]
    public void TryIdentifyOwner_FindetPluginAnhandDerAssemblyDesInstanztyps()
    {
        // P2.6b: das aktive Dokument beim Absturz einem Plugin zuordnen, um dessen
        // Tab gezielt zu schliessen.
        var plugin = new FakePlugin();
        var pluginAssemblies = new Dictionary<IPlugin, System.Reflection.Assembly>
        {
            [plugin] = typeof(FakePluginDocumentViewModel).Assembly
        };

        var found = PluginSourceIdentifier.TryIdentifyOwner(new FakePluginDocumentViewModel(), pluginAssemblies);

        Assert.Same(plugin, found);
    }

    [Fact]
    public void TryIdentifyOwner_FremderTyp_LiefertNull()
    {
        var plugin = new FakePlugin();
        var pluginAssemblies = new Dictionary<IPlugin, System.Reflection.Assembly>
        {
            [plugin] = typeof(FakePluginDocumentViewModel).Assembly
        };

        var found = PluginSourceIdentifier.TryIdentifyOwner(new object(), pluginAssemblies);

        Assert.Null(found);
    }
}
