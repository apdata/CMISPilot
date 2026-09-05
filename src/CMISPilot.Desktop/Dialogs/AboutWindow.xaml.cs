using System.Windows;
using CMISPilot.ViewModels.About;

namespace CMISPilot.Desktop.Dialogs;

/// <summary>
/// „Über CMISPilot"-Fenster (R6.4): Produktname, Version/Build, Copyright und ein
/// kurzer Komponentenhinweis. Erreichbar über Ribbon (Hilfe/Info) bzw. Backstage
/// (siehe <c>MainWindow.xaml.cs</c>, <c>OnAboutClick</c>). Rein anzeigend, daher genügt
/// hier ein direkt erzeugtes <see cref="AboutViewModel"/> statt DI (analog
/// <see cref="ConfirmDialog"/>).
/// </summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        DataContext = new AboutViewModel();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// Legt die Eckdaten (Produkt, Version, .NET-Version) in die Zwischenablage.
    /// Gedacht fuer Supportfaelle: der Nutzer muss nicht abtippen, welcher Stand laeuft.
    ///
    /// <para>Der Zugriff auf die Zwischenablage kann fehlschlagen, wenn eine andere
    /// Anwendung sie gerade haelt (Win32 oeffnet sie exklusiv). Das ist kein Fehler,
    /// den man dem Nutzer melden muesste - ein zweiter Klick geht in aller Regel durch.</para>
    /// </summary>
    private void OnCopyVersionClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(((AboutViewModel)DataContext).VersionSummary);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
        }
    }
}
