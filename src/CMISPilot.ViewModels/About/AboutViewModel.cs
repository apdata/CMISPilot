using System;
using System.Collections.Generic;
using System.Reflection;

namespace CMISPilot.ViewModels.About;

/// <summary>
/// ViewModel des „Über CMISPilot"-Fensters (R6.4). Liest Produktname, Version/Build
/// und Copyright per Reflection aus dem Einstiegsassembly (<see cref="Assembly.GetEntryAssembly"/>),
/// damit keine Werte redundant gepflegt werden müssen. Reflection ist kein WPF-Bezug,
/// das ViewModel bleibt WPF-frei (NFA-03).
/// </summary>
public sealed class AboutViewModel
{
    public AboutViewModel()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

        ProductName = GetAttribute<AssemblyProductAttribute>(assembly)?.Product ?? "CMISPilot";
        Version = assembly.GetName().Version?.ToString() ?? "0.0.0";
        InformationalVersion = GetAttribute<AssemblyInformationalVersionAttribute>(assembly)?.InformationalVersion;
        Copyright = GetAttribute<AssemblyCopyrightAttribute>(assembly)?.Copyright
            ?? $"© {DateTime.Now.Year} CMISPilot";
        Description = GetAttribute<AssemblyDescriptionAttribute>(assembly)?.Description
            ?? "CMISPilot – Werkbank für CMIS-Repositories (Browser Binding).";
    }

    public string ProductName { get; }
    public string Version { get; }
    public string? InformationalVersion { get; }
    public string Copyright { get; }
    public string Description { get; }

    /// <summary>Die Laufzeitversion von .NET, unter der die Anwendung läuft.</summary>
    public string RuntimeVersion => Environment.Version.ToString();

    /// <summary>
    /// Die verwendeten Kernkomponenten mit ihrer Lizenz, für die Tabelle im Fenster.
    /// Die vollständigen Lizenztexte liegen in den jeweiligen NuGet-Paketen und
    /// Repositories; hier steht nur die Übersicht.
    /// </summary>
    public IReadOnlyList<ComponentInfo> Components { get; } =
    [
        new("PortCMIS", "Apache-2.0"),
        new("Fluent.Ribbon", "MS-PL"),
        new("AvalonDock", "MS-PL"),
        new("AvalonEdit", "MIT"),
        new("CommunityToolkit.Mvvm", "MIT"),
        new("ClosedXML", "MIT"),
        new("Microsoft.Extensions.*", "MIT"),
        new("Serilog", "Apache-2.0")
    ];

    /// <summary>
    /// Die Eckdaten als mehrzeiliger Text für die Zwischenablage — gedacht für
    /// Supportfälle, damit man nicht abtippen muss, welcher Stand läuft.
    /// </summary>
    public string VersionSummary =>
        $"{ProductName} {Version}"
        + (string.IsNullOrWhiteSpace(InformationalVersion) ? string.Empty : $" ({InformationalVersion})")
        + Environment.NewLine
        + $".NET {RuntimeVersion}";

    private static T? GetAttribute<T>(Assembly assembly) where T : Attribute =>
        assembly.GetCustomAttribute<T>();
}

/// <summary>Eine verwendete Komponente mit ihrer Lizenz (Zeile der Komponententabelle).</summary>
/// <param name="Name">Name der Komponente, wie sie als Paket oder Projekt heißt.</param>
/// <param name="License">Kurzbezeichnung der Lizenz, z. B. <c>MIT</c>.</param>
public sealed record ComponentInfo(string Name, string License);
