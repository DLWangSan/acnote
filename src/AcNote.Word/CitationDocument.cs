using AcNote.Core;

namespace AcNote.Word;

public static class CitationDocument
{
    public const string CitePrefix = "acnote-cite|";
    public const string BibliographyTag = "acnote-bib";
    public const string LibraryVariable = "AcNoteLibrary";

    public static void BindLibrary(object document, string path)
    {
        dynamic doc = document;
        var existing = GetLibraryPath(document);
        if (!string.IsNullOrEmpty(existing) && !string.Equals(Path.GetFullPath(existing), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("此文档绑定了另一份文献库，请先打开：" + existing);
        try { doc.Variables[LibraryVariable].Value = Path.GetFullPath(path); }
        catch { doc.Variables.Add(LibraryVariable, Path.GetFullPath(path)); }
    }

    public static string GetLibraryPath(object document)
    {
        try { dynamic doc = document; return (string)doc.Variables[LibraryVariable].Value; }
        catch { return ""; }
    }

    public static List<string> SelectionIds(object selection)
    {
        dynamic selected = selection;
        return ReadTag((string)selected.ParentContentControl.Tag).Ids;
    }

    public const string StyleVariable = "AcNoteStyle";

    public static void InsertAndRefresh(
        object document,
        object selection,
        IReadOnlyList<string> ids,
        string form,
        ResolvedStyle style,
        Func<string, ReferenceItem?> lookup)
    {
        dynamic wordDocument = document;
        dynamic wordSelection = selection;
        if (ids.Count == 0)
            return;

        ValidateReferences(wordDocument, ids, lookup);
        SetStyleId(wordDocument, style.Id);
        dynamic range = wordSelection.Range;
        range.Collapse(0);
        dynamic control = wordDocument.ContentControls.Add(0, range);
        Prepare(control, BuildTag(form, ids));
        Refresh(wordDocument, style, lookup);
        MoveAfterControl(wordDocument, wordSelection, control);
    }

    public static void ReplaceSelectedAndRefresh(
        object document,
        object selection,
        IReadOnlyList<string> ids,
        string form,
        ResolvedStyle style,
        Func<string, ReferenceItem?> lookup)
    {
        dynamic wordDocument = document;
        dynamic wordSelection = selection;
        dynamic control = wordSelection.ParentContentControl;
        var tag = (string)(control.Tag ?? "");
        if (!IsCitationTag(tag))
            throw new InvalidOperationException("请把光标放在一处 AcNote 引用里。");

        ValidateReferences(wordDocument, ids, lookup, (string)control.ID);
        control.Tag = BuildTag(form, ids);
        Refresh(wordDocument, style, lookup);
    }

    public static void RemoveSelectedAndRefresh(object document, object selection, ResolvedStyle style, Func<string, ReferenceItem?> lookup)
    {
        if (!SelectionIsCitation(selection)) throw new InvalidOperationException("请把光标放在要移除的 AcNote 引用里。");
        dynamic selected = selection;
        dynamic control = selected.ParentContentControl;
        ValidateReferences(document, [], lookup, (string)control.ID);
        control.LockContentControl = false;
        control.LockContents = false;
        control.Range.Text = "";
        control.Delete(true);
        Refresh(document, style, lookup);
    }

    public static string SelectionForm(object selection)
    {
        try
        {
            dynamic wordSelection = selection;
            dynamic control = wordSelection.ParentContentControl;
            return ReadTag((string)(control.Tag ?? "")).Form;
        }
        catch
        {
            return "parenthetical";
        }
    }

    public static string BuildTag(string form, IReadOnlyList<string> ids) =>
        CitePrefix + (form == "narrative" ? "n" : "p") + "|" + string.Join(';', ids);

    public static bool IsCitationTag(string tag) =>
        tag.StartsWith(CitePrefix, StringComparison.Ordinal);

    public static (string Form, List<string> Ids) ReadTag(string tag)
    {
        var body = tag.StartsWith(CitePrefix, StringComparison.Ordinal) ? tag[CitePrefix.Length..] : tag;
        string form;
        if (body.StartsWith("n|", StringComparison.Ordinal))
        {
            form = "narrative";
            body = body[2..];
        }
        else if (body.StartsWith("p|", StringComparison.Ordinal))
        {
            form = "parenthetical";
            body = body[2..];
        }
        else
        {
            form = "parenthetical";
        }

        var ids = body.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        return (form, ids);
    }

    public static bool SelectionIsCitation(object selection)
    {
        try
        {
            dynamic wordSelection = selection;
            dynamic control = wordSelection.ParentContentControl;
            var tag = (string)(control.Tag ?? "");
            return IsCitationTag(tag);
        }
        catch
        {
            return false;
        }
    }

    public static void Refresh(object document, ResolvedStyle style, Func<string, ReferenceItem?> lookup)
    {
        dynamic wordDocument = document;
        var cites = new List<(int Start, dynamic Control, string Form, List<string> Ids)>();
        dynamic? bibliography = null;
        var count = (int)wordDocument.ContentControls.Count;
        for (var index = 1; index <= count; index++)
        {
            dynamic control = wordDocument.ContentControls[index];
            var tag = (string)(control.Tag ?? "");
            if (tag.StartsWith(CitePrefix, StringComparison.Ordinal))
            {
                var parsed = ReadTag(tag);
                cites.Add(((int)control.Range.Start, control, parsed.Form, parsed.Ids));
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

        // Resolve every record before touching document text.
        var records = order.ToDictionary(id => id, id => lookup(id) ?? throw new InvalidOperationException("找不到引用文献，请恢复文献或打开正确的文献库。未更新文档：" + id));
        var years = YearDisambiguator.Resolve(records.Values, style.CitationEtAl);
        var authorYear = string.Equals(style.Layout, "author-year", StringComparison.OrdinalIgnoreCase);
        foreach (var cite in cites)
        {
            var rendered = authorYear
                ? InTextFormatter.Format(cite.Ids.Select(id => records[id]), style.CitationEtAl, cite.Form == "narrative", years)
                : CitationNumbers.Format(cite.Ids.Select(id => numbers[id]));
            SetText(cite.Control, rendered);
            ApplyFormat(cite.Control.Range, style.CitationFormat, false);
        }

        if (order.Count == 0)
        {
            if (bibliography is not null)
            {
                bibliography.LockContentControl = false;
                bibliography.Delete(true);
            }
            return;
        }

        var lines = new List<string>();
        if (authorYear)
        {
            var entries = order
                .Select(id => (Id: id, Item: records[id]))
                .OrderBy(entry => entry.Item is null ? "" : InTextFormatter.SortKey(entry.Item), StringComparer.CurrentCulture)
                .ThenBy(entry => entry.Item?.Year ?? "", StringComparer.Ordinal)
                .ThenBy(entry => entry.Item?.Title ?? "", StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.Id, StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                lines.Add(entry.Item is null
                    ? "（库中已无此文献）"
                    : BibliographyFormatter.Format(entry.Item, style, years.GetValueOrDefault(entry.Id)).Text);
            }
        }
        else
        {
            foreach (var id in order)
            {
                var item = records[id];
                var body = item is null
                    ? "（库中已无此文献）"
                    : BibliographyFormatter.Format(item, style).Text;
                lines.Add($"[{numbers[id]}] {body}");
            }
        }

        if (bibliography is null)
            bibliography = CreateBibliography(wordDocument);

        EnsureBibliographyParagraph(wordDocument, bibliography);
        SetText(bibliography, string.Join("\r", lines));
        ApplyFormat(bibliography.Range, style.BibliographyFormat, true);
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
        // Word's final paragraph mark is outside ContentControls. Insert the list
        // into a new, empty paragraph rather than collapsing beyond that mark.
        var end = (int)document.Content.End - 1;
        dynamic tail = document.Range(end, end);
        tail.InsertAfter("\r\r");
        var start = (int)document.Content.End - 1;
        dynamic range = document.Range(start, start);
        dynamic control = document.ContentControls.Add(0, range);
        Prepare(control, BibliographyTag);
        return control;
    }

    private static void EnsureBibliographyParagraph(dynamic document, dynamic bibliography)
    {
        var start = (int)bibliography.Range.Start;
        if (start <= 1) return;
        // Start - 1 is the opening content-control boundary, not bibliography text.
        var outside = start - 1;
        var preceding = (string)document.Range(Math.Max(0, outside - 1), outside).Text;
        if (preceding != "\r") document.Range(outside, outside).InsertBefore("\r\r");
    }

    private static void MoveAfterControl(dynamic document, dynamic selection, dynamic control)
    {
        var position = Math.Min((int)control.Range.End + 1, (int)document.Content.End - 1);
        selection.SetRange(position, position);
    }

    private static void Prepare(dynamic control, string tag)
    {
        control.Tag = tag;
        control.Title = "AcNote";
        try { control.Appearance = 2; } catch { /* Older Word builds have no hidden appearance. */ }
        try { control.LockContentControl = false; } catch { /* Citations remain removable with normal Word editing. */ }
    }

    private static void ValidateReferences(dynamic document, IReadOnlyList<string> ids, Func<string, ReferenceItem?> lookup, string? excludeControl = null)
    {
        var all = new HashSet<string>(ids);
        for (var i = 1; i <= (int)document.ContentControls.Count; i++)
        {
            dynamic control = document.ContentControls[i];
            if ((string)control.ID == excludeControl) continue;
            var tag = (string)(control.Tag ?? "");
            if (IsCitationTag(tag)) all.UnionWith(ReadTag(tag).Ids);
        }
        foreach (var id in all)
            if (lookup(id) is null) throw new InvalidOperationException("找不到引用文献，未修改文档：" + id);
    }

    private static void ApplyFormat(dynamic range, TextLayout format, bool paragraph)
    {
        format.Validate();
        if (!string.IsNullOrWhiteSpace(format.FontFamily)) range.Font.Name = format.FontFamily;
        if (!string.IsNullOrWhiteSpace(format.EastAsiaFontFamily)) range.Font.NameFarEast = format.EastAsiaFontFamily;
        if (format.FontSize is double size) range.Font.Size = (float)size;
        range.Font.Superscript = format.Superscript == true ? -1 : 0;
        if (!paragraph) return;
        dynamic p = range.ParagraphFormat;
        if (format.LineSpacing is double line) { p.LineSpacingRule = 5; p.LineSpacing = (float)(12 * line); }
        if (format.SpaceBefore is double before) p.SpaceBefore = (float)before;
        if (format.SpaceAfter is double after) p.SpaceAfter = (float)after;
        if (format.HangingIndent is double indent) { p.LeftIndent = (float)indent; p.FirstLineIndent = (float)-indent; }
        if (format.Alignment is string alignment) p.Alignment = alignment switch { "center" => 1, "right" => 2, "justify" => 3, _ => 0 };
    }

    private static void SetText(dynamic control, string text)
    {
        try { control.LockContents = false; } catch { /* Continue writing when the lock cannot be cleared. */ }
        dynamic range = control.Range;
        range.Text = text ?? "";
    }
}
