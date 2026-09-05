namespace CMISPilot.Desktop;

/// <summary>
/// Einstellungsabschnitt für Position, Größe und Maximierung des Hauptfensters
/// (S7.3), über <c>ISettingsStore</c> abgelegt.
///
/// <para>Bewusst CMISPilot-eigen und nicht in APX.Wpf.Shell: das Bedürfnis ist
/// zwar generisch, aber es gibt noch keinen zweiten tatsächlichen Nutzer dafür
/// (Zwei-Nutzer-Regel). Sollte PB Browser Reborn dasselbe brauchen, wandert es
/// hoch.</para>
/// </summary>
public sealed class WindowSettings
{
    /// <summary>Ob überhaupt schon einmal gespeichert wurde.</summary>
    public bool HasValue { get; set; }

    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; } = 1160;
    public double Height { get; set; } = 740;
    public bool IsMaximized { get; set; }
}
