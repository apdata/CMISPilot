using APX.Wpf.Shell.ViewModels.Workspace;
using CMISPilot.Cmis.Contracts;
using CMISPilot.ViewModels.Connection;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using CmisWorkspaceViewModel = CMISPilot.ViewModels.Workspace.WorkspaceViewModel;
using ShellWorkspaceViewModel = APX.Wpf.Shell.ViewModels.Workspace.WorkspaceViewModel;

namespace CMISPilot.Tests.ViewModels;

/// <summary>
/// Prüft den Plugin-Anschlusspunkt aus Plan-Plugin-Schnittstelle.md P1.5: die
/// APX.Wpf.Shell-Basisklasse von <see cref="WorkspaceViewModel"/> muss über DI
/// auflösbar sein und **dieselbe Instanz** liefern wie der abgeleitete Typ — ein
/// Plugin, das nur den Vertrag kennt, injiziert sich die Basis und muss auf
/// demselben Workspace landen wie der Host.
///
/// Registriert hier bewusst nur <see cref="WorkspaceViewModel"/> selbst (nicht
/// das volle <c>AddWorkspace()</c>, das CMIS- und Shell-Dienste aus anderen
/// Registrierungen voraussetzt) — der Anschlusspunkt ist unabhängig vom Rest der
/// Werkbank-Registrierung.
/// </summary>
public sealed class WorkspaceServiceCollectionExtensionsTests
{
    [Fact]
    public void BasisTyp_LoestZurSelbenInstanzAufWieAbgeleiteterTyp()
    {
        var services = new ServiceCollection();

        // ConnectionStatusViewModel ist sealed (nicht mockbar) - echte Instanz mit
        // gemockter Interface-Abhaengigkeit.
        services.AddSingleton(new ConnectionStatusViewModel(Substitute.For<ISessionContext>()));
        services.AddSingleton<IEnumerable<IToolViewModel>>(Array.Empty<IToolViewModel>());

        services.AddSingleton<CmisWorkspaceViewModel>();

        // Derselbe Registrierungssatz wie in WorkspaceServiceCollectionExtensions.AddWorkspace.
        services.AddSingleton<ShellWorkspaceViewModel>(
            sp => sp.GetRequiredService<CmisWorkspaceViewModel>());

        using var provider = services.BuildServiceProvider();

        var derived = provider.GetRequiredService<CmisWorkspaceViewModel>();
        var basis = provider.GetRequiredService<ShellWorkspaceViewModel>();

        Assert.Same(derived, basis);
    }
}
