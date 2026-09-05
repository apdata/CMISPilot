using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CMISPilot.Cmis.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CMISPilot.ViewModels.Explorer;

/// <summary>
/// Einheitlicher Knoten des Server-Baums (R4 Etappe 2): Server, Repository oder
/// Ordner. Laedt seine Kinder bei Ordnerknoten <b>lazy</b>, analog zum bestehenden
/// Muster in <c>FolderNodeViewModel</c>: ein noch nicht geladener Knoten erhaelt
/// einen Platzhalter-Kindknoten (damit die TreeView den Aufklapp-Pfeil zeigt); beim
/// Aufklappen (<see cref="IsExpanded"/>=true) laedt der injizierte Loader nach.
/// Server- und Repository-Knoten werden ohne Loader angelegt und ihre Kinder direkt
/// vom <see cref="ServerTreeViewModel"/> befuellt.
///
/// Bewusst WPF-frei (NFA-03): reine <see cref="ObservableObject"/>-Bindungsquelle.
/// </summary>
public sealed partial class TreeNodeViewModel : ObservableObject
{
    private readonly Func<TreeNodeViewModel, CancellationToken, Task>? _loadChildren;

    /// <summary>Erzeugt einen echten Knoten.</summary>
    /// <param name="kind">Art des Knotens (Server/Repository/Ordner).</param>
    /// <param name="name">Anzeigename im Baum.</param>
    /// <param name="objectId">CMIS-Id (nur bei Ordnerknoten gesetzt).</param>
    /// <param name="cmisObject">Das zugrunde liegende CMIS-Objekt (nur bei Ordnerknoten gesetzt).</param>
    /// <param name="loadChildren">Lazy-Load-Funktion fuer die Kinder (nur bei Ordnerknoten).</param>
    public TreeNodeViewModel(
        TreeNodeKind kind,
        string name,
        string? objectId = null,
        CmisObjectDto? cmisObject = null,
        Func<TreeNodeViewModel, CancellationToken, Task>? loadChildren = null)
    {
        Kind = kind;
        Name = name;
        ObjectId = objectId;
        CmisObject = cmisObject;
        _loadChildren = loadChildren;

        if (_loadChildren is not null)
        {
            // Platzhalter, damit der Aufklapp-Pfeil erscheint, bevor wir wissen ob
            // es Unterordner gibt. Wird beim ersten Laden ersetzt.
            Children.Add(CreatePlaceholder());
        }
    }

    private TreeNodeViewModel()
    {
        IsPlaceholder = true;
        Name = "…";
        Kind = TreeNodeKind.Folder;
    }

    /// <summary>Art des Knotens (Server/Repository/Ordner).</summary>
    public TreeNodeKind Kind { get; }

    /// <summary>Anzeigename im Baum.</summary>
    public string Name { get; }

    /// <summary>CMIS-Id des Objekts (nur bei Ordnerknoten gesetzt, sonst null).</summary>
    public string? ObjectId { get; }

    /// <summary>Das zugrunde liegende CMIS-Objekt (nur bei Ordnerknoten gesetzt, sonst null).</summary>
    public CmisObjectDto? CmisObject { get; }

    /// <summary>Reiner Platzhalterknoten (fuer den Aufklapp-Pfeil, nicht selektierbar).</summary>
    public bool IsPlaceholder { get; }

    /// <summary>Kindknoten (Repositories unter Server, Unterordner unter Ordnern).</summary>
    public ObservableCollection<TreeNodeViewModel> Children { get; } = new();

    /// <summary>True, sobald die echten Kinder geladen wurden (bei Ordnerknoten).</summary>
    public bool AreChildrenLoaded { get; private set; }

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSelected;

    partial void OnIsExpandedChanged(bool value)
    {
        // Fire-and-forget fuer die UI (M4-Referenzmuster). Tests treiben den
        // Lazy-Load deterministisch ueber die awaitbare LoadChildrenAsync-Methode.
        if (value && !AreChildrenLoaded && _loadChildren is not null)
        {
            _ = LoadChildrenAsync(CancellationToken.None);
        }
    }

    /// <summary>Laedt die Kinder genau einmal nach (idempotent).</summary>
    public async Task LoadChildrenAsync(CancellationToken ct)
    {
        if (AreChildrenLoaded || _loadChildren is null)
        {
            return;
        }

        await _loadChildren(this, ct).ConfigureAwait(true);
        AreChildrenLoaded = true;
    }

    /// <summary>Ersetzt die (Platzhalter-)Kinder durch die echten Kindknoten.</summary>
    internal void SetChildren(IEnumerable<TreeNodeViewModel> nodes)
    {
        Children.Clear();
        foreach (var node in nodes)
        {
            Children.Add(node);
        }
    }

    private static TreeNodeViewModel CreatePlaceholder() => new();
}
