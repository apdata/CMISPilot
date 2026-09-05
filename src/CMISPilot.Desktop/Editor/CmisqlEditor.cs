using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Xml;
using APX.Wpf.Shell.Editor;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace CMISPilot.Desktop.Editor;

/// <summary>
/// Editor fuer CMISQL-Abfragen (Query Browser): erbt von <see cref="CodeEditorBase"/>
/// und ergaenzt nur, was CMISQL ausmacht - Syntaxhervorhebung fuer Schluesselwoerter
/// (SELECT/FROM/WHERE/...), die CMISQL-eigenen Funktionen (IN_FOLDER/IN_TREE/
/// CONTAINS/SCORE), cmis:-Property-Praefixe und Autovervollstaendigung fuer all das.
/// Alles Sprachneutrale (Zeilennummern, Suchleiste, Themenfarben, Faltungsgeruest)
/// liefert bereits die Basisklasse.
///
/// Anders als <see cref="CodeEditorBase"/> - fuer reine Quelltextanzeigen gedacht,
/// deshalb dort <c>IsReadOnly = true</c> - ist dieser Editor beschreibbar, weil der
/// Nutzer hier seine Abfrage eingibt.
/// </summary>
public sealed class CmisqlEditor : CodeEditorBase
{
    private static readonly IHighlightingDefinition Highlighting = LoadHighlighting();

    private static readonly string[] Keywords =
    [
        "SELECT", "FROM", "WHERE", "AND", "OR", "NOT", "AS", "IN", "LIKE", "IS", "NULL",
        "TRUE", "FALSE", "ANY", "ORDER", "BY", "ASC", "DESC", "JOIN", "INNER", "LEFT",
        "OUTER", "ON", "TIMESTAMP"
    ];

    private static readonly string[] Functions = ["IN_FOLDER", "IN_TREE", "CONTAINS", "SCORE"];

    // Die immer vorhandenen cmis:-Basisproperties (CMIS-Spezifikation, Kapitel 2.1.2).
    // Repository-eigene Properties (aus Custom Types) kennt der Editor bewusst nicht -
    // dafuer bräuchte es eine aktive Session, das ist ein möglicher Ausbauschritt.
    private static readonly string[] BaseProperties =
    [
        "cmis:objectId", "cmis:objectTypeId", "cmis:baseTypeId", "cmis:name",
        "cmis:createdBy", "cmis:creationDate", "cmis:lastModifiedBy",
        "cmis:lastModificationDate", "cmis:changeToken", "cmis:path", "cmis:parentId",
        "cmis:contentStreamFileName", "cmis:contentStreamLength", "cmis:contentStreamMimeType",
        "cmis:isImmutable", "cmis:isLatestVersion", "cmis:isMajorVersion",
        "cmis:versionLabel", "cmis:checkinComment"
    ];

    private CompletionWindow? _completionWindow;

    public CmisqlEditor()
    {
        IsReadOnly = false;
        SyntaxHighlighting = Highlighting;

        TextArea.TextEntered += OnTextEntered;
    }

    /// <summary>
    /// Öffnet bei jedem getippten Buchstaben ein <see cref="CompletionWindow"/> mit
    /// Schlüsselwörtern/Funktionen/Basisproperties, sofern noch keins offen ist -
    /// AvalonEdit filtert die Liste danach selbst anhand der weiteren Eingabe.
    /// <see cref="CompletionWindow.StartOffset"/> wird um ein Zeichen zurückgesetzt,
    /// damit der gerade getippte Buchstabe selbst schon zum Filter zählt (sonst würde
    /// „S" in „SELECT" beim Öffnen verloren gehen, weil der Cursor bereits dahinter steht).
    /// </summary>
    private void OnTextEntered(object? sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        if (_completionWindow is not null || e.Text.Length == 0 || !char.IsLetter(e.Text[0]))
        {
            return;
        }

        _completionWindow = new CompletionWindow(TextArea) { StartOffset = TextArea.Caret.Offset - 1 };

        var data = _completionWindow.CompletionList.CompletionData;
        foreach (var keyword in Keywords)
        {
            data.Add(new CmisqlCompletionData(keyword, "CMISQL-Schlüsselwort"));
        }

        foreach (var function in Functions)
        {
            data.Add(new CmisqlCompletionData(function, "CMISQL-Funktion"));
        }

        foreach (var property in BaseProperties)
        {
            data.Add(new CmisqlCompletionData(property, "CMIS-Basisproperty"));
        }

        _completionWindow.Show();
        _completionWindow.Closed += (_, _) => _completionWindow = null;
    }

    /// <summary>Laedt <c>CmisqlHighlighting.xshd</c> aus den eingebetteten Ressourcen dieser Assembly.</summary>
    private static IHighlightingDefinition LoadHighlighting()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using Stream stream = assembly.GetManifestResourceStream(
            "CMISPilot.Desktop.Editor.CmisqlHighlighting.xshd")
            ?? throw new InvalidOperationException(
                "CmisqlHighlighting.xshd nicht als eingebettete Ressource gefunden.");

        using var reader = new XmlTextReader(stream);
        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }
}

/// <summary>Ein Vorschlag im Autovervollständigungs-Popup von <see cref="CmisqlEditor"/>.</summary>
internal sealed class CmisqlCompletionData(string text, string description) : ICompletionData
{
    public ImageSource? Image => null;

    public string Text { get; } = text;

    public object Content => Text;

    public object Description { get; } = description;

    public double Priority => 0;

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs) =>
        textArea.Document.Replace(completionSegment, Text);
}
