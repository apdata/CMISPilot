namespace CMISPilot.Cmis.Diagnostics;

/// <summary>
/// Statischer Ambient-Holder für das aktive <see cref="IDiagnosticsLog"/>.
/// </summary>
/// <remarks>
/// PortCMIS instanziiert einen eigenen HTTP-Invoker (<see cref="LoggingHttpInvoker"/>)
/// ausschließlich über <c>SessionParameter.HttpInvokerClass</c> +
/// <c>Activator.CreateInstance</c> mit parameterlosem Konstruktor (siehe
/// <c>BindingSession.GetHttpInvoker</c> in PortCMIS). Es gibt dort keinen Weg,
/// eine DI-Instanz durchzureichen (Session-Parameter sind reine
/// <c>IDictionary&lt;string,string&gt;</c>). Deshalb wird der Singleton-Log hier
/// beim ersten Erzeugen des <see cref="IDiagnosticsLog"/> hinterlegt – bewusste,
/// dokumentierte Ausnahme von „kein statischer Zustand".
/// </remarks>
public static class DiagnosticsLogAmbient
{
    /// <summary>Aktiver Log oder <see cref="NullDiagnosticsLog.Instance"/>, falls keiner gesetzt ist.</summary>
    public static IDiagnosticsLog Current { get; private set; } = NullDiagnosticsLog.Instance;

    /// <summary>Wird von der DI-Registrierung beim Erzeugen des Singleton-Logs gesetzt.</summary>
    public static void SetCurrent(IDiagnosticsLog log) => Current = log;
}
