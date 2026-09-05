using APX.Wpf.Shell.ViewModels.Workspace;
using CommunityToolkit.Mvvm.Input;

namespace SamplePlugin;

/// <summary>
/// Dokument-Tab des Vorlage-Plugins. Zeigt das vollstaendige Muster: Ableitung von
/// der Host-Basisklasse aus einer erst zur Laufzeit geladenen Assembly, ein
/// DataTemplate, das WPF dafuer findet, und eine Command-Bindung aus dem
/// Ribbon-Tab des Plugins, die auf dieses ViewModel durchgreift
/// (<c>{Binding ActiveDocument.PingCommand}</c> in <c>SampleResources.xaml</c>).
/// </summary>
public sealed partial class SampleDocumentViewModel : DocumentViewModelBase
{
    public SampleDocumentViewModel()
        : base("sample:document")
    {
        Title = "Beispiel-Dokument";
    }

    /// <summary>Macht den kontextbezogenen Ribbon-Tab des Plugins sichtbar.</summary>
    public override string? ContextTabKey => "sample";

    /// <summary>Wird vom DataTemplate angezeigt — beweist eine funktionierende Bindung.</summary>
    public string Message =>
        $"Geladen aus: {GetType().Assembly.Location}";

    /// <summary>Zaehler, den der Ribbon-Befehl des Plugins hochzaehlt.</summary>
    public int ClickCount
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>
    /// Beweist, dass eine <c>Command</c>-Bindung aus dem Ribbon-Tab des Plugins auf
    /// das ViewModel des Plugins durchgreift (ueber <c>ActiveDocument</c> des Hosts).
    /// </summary>
    [RelayCommand]
    private void Ping() => ClickCount++;
}
