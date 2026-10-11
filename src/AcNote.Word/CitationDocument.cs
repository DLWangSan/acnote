using AcNote.Core;

namespace AcNote.Word;

public static class CitationDocument
{
    public const string CitePrefix = "acnote-cite|";
    public const string BibliographyTag = "acnote-bib";
    public const string StyleVariable = "AcNoteStyle";

    public static void InsertAndRefresh(
        object document,
        object selection,
        IReadOnlyList<string> ids,
        ResolvedStyle style,
        Func<string, ReferenceItem?> lookup)
    {
        dynamic wordDocument = document;
        dynamic wordSelection = selection;
        if (ids.Count == 0)
            return;

        SetStyleId(wordDocument, style.Id);
        dynamic range = wordSelection.Range;
        range.Collapse(0);
        dynamic control = wordDocument.ContentControls.Add(0, range);
        Prepare(control, CitePrefix + string.Join(';', ids));
        Refresh(wordDocument, style, lookup);
    }

    public static void ReplaceSelectedAndRefresh(
        object document,
        object selection,
        IReadOnlyList<string> ids,
        ResolvedStyle style,
        Func<string, ReferenceItem?> lookup)
    {
        dynamic wordDocument = document;
        dynamic wordSelection = selection;
        dynamic control = wordSelection.ParentContentControl;
        var tag = (string)(control.Tag ?? "");
        if (!tag.StartsWith(CitePrefix, StringComparison.Ordinal))
            throw new InvalidOperationException("请把光标放在一处 AcNote 引用里。");

        control.Tag = CitePrefix + string.Join(';', ids);
        Refresh(wordDocument, style, lookup);
    }

    public static bool SelectionIsCitation(object selection)
    {
        try
        {
            dynamic wordSelection = selection;
            dynamic control = wordSelection.ParentContentControl;
            var tag = (string)(control.Tag ?? "");
            return tag.StartsWith(CitePrefix, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    public static void Refresh(object document, ResolvedStyle style, Func<string, ReferenceItem?> lookup)
    {
        dynamic wordDocument = document;
        var cites = new List<(int Start, dynamic Control, List<string> Ids)>();
        dynamic? bibliography = null;
        var count = (int)wordDocument.ContentControls.Count;
        for (var index = 1; index <= count; index++)
        {
            dynamic control = wordDocument.ContentControls[index];
            var tag = (string)(control.Tag ?? "");
            if (tag.StartsWith(CitePrefix, StringComparison.Ordinal))
            {
                var ids = tag[CitePrefix.Length..]
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();
                cites.Add(((int)control.Range.Start, control, ids));
            }
            else if (tag == BibliographyTag)
            {
                bibliography = control;
            }
        }

        cites.Sort((left, right) => left.Start.CompareTo(right.Start));
        var order = new List<string>();
        var numbers = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var cite in cites)
        {
            foreach (var id in cite.Ids)
            {
                if (numbers.ContainsKey(id))
                    continue;
                order.Add(id);
                numbers[id] = order.Count;
            }
        }

        foreach (var cite in cites)
        {
            var rendered = CitationNumbers.Format(cite.Ids.Select(id => numbers[id]));
            SetText(cite.Control, rendered);
        }

        if (order.Count == 0)
        {
            if (bibliography is not null)
                bibliography.Delete();
            return;
        }

        var lines = new List<string>();
        foreach (var id in order)
        {
            var item = lookup(id);
            var body = item is null
                ? "（库中已无此文献）"
                : BibliographyFormatter.Format(item, style).Text;
            lines.Add($"[{numbers[id]}] {body}");
        }

        if (bibliography is null)
            bibliography = CreateBibliography(wordDocument);

        SetText(bibliography, string.Join("\r", lines));
    }

    public static string GetStyleId(object document)
    {
        try
        {
            dynamic wordDocument = document;
            return (string)wordDocument.Variables[StyleVariable].Value;
        }
        catch
        {
            return "";
        }
    }

    public static void SetStyleId(object document, string styleId)
    {
        dynamic wordDocument = document;
        try
        {
            wordDocument.Variables[StyleVariable].Value = styleId;
        }
        catch
        {
            wordDocument.Variables.Add(StyleVariable, styleId);
        }
    }

    private static dynamic CreateBibliography(dynamic document)
    {
        dynamic range = document.Content;
        range.Collapse(0);
        range.InsertParagraphBefore();
        range = document.Content;
        range.Collapse(0);
        range.MoveEnd(1, -1);
        dynamic control = document.ContentControls.Add(0, range);
        Prepare(control, BibliographyTag);
        return control;
    }

    private static void Prepare(dynamic control, string tag)
    {
        control.Tag = tag;
        control.Title = "AcNote";
        try { control.Appearance = 2; } catch { /* 旧版本没有隐藏外观 */ }
        try { control.LockContentControl = true; } catch { /* 保持可更新 */ }
    }

    private static void SetText(dynamic control, string text)
    {
        try { control.LockContents = false; } catch { /* 继续写入 */ }
        dynamic range = control.Range;
        range.Text = text ?? "";
    }
}
