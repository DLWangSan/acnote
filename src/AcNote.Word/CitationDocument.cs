using AcNote.Core;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AcNote.Word;

public static class CitationDocument
{
    public const string CitePrefix = "acnote-cite|";
    public const string BibliographyTag = "acnote-bib";
    public const string LibraryVariable = "AcNoteLibrary";
    public const string TypographyVariable = "AcNoteTypography";
    private const string RenderedPrefix = "AcNoteRendered_";
    private const string ReferenceBookmarkPrefix = "AcNoteRef_";
    private sealed record CitationLinkSpan(int Start, int Length, string ReferenceId);

    private static string? PreviousText(dynamic document, string id)
    {
        try { return (string)document.Variables[RenderedPrefix + id].Value; }
        catch { return null; }
    }

    private static void RememberText(dynamic document, string id, string text)
    {
        try { document.Variables[RenderedPrefix + id].Value = text; }
        catch { document.Variables.Add(RenderedPrefix + id, text); }
    }

    private static bool EditedCitation(dynamic document, dynamic control)
    {
        var text = (string)control.Range.Text;
        var previous = PreviousText(document, (string)control.ID);
        if (previous is not null) return text != previous;
        // Migrate old documents conservatively. Never overwrite prose typed into
        // an old empty citation wrapper. New controls use an explicit sentinel.
        return text != "…" && !Regex.IsMatch(text, @"^\[[\d,，\s–—-]+\]$")
            && !Regex.IsMatch(text, @"[\(（].*\d{4}[a-z]?.*[\)）]$");
    }

    public static DocumentTypography GetTypography(object document)
    {
        dynamic doc = document;
        string json;
        try { json = (string)doc.Variables[TypographyVariable].Value; }
        catch { return new(); }
        var value = JsonSerializer.Deserialize<DocumentTypography>(json)
            ?? throw new InvalidOperationException("文档的字体段落设置无法读取。");
        value.Validate();
        return value;
    }

    public static void SetTypography(object document, DocumentTypography value)
    {
        value.Validate();
        dynamic doc = document;
        var json = JsonSerializer.Serialize(value);
        try { doc.Variables[TypographyVariable].Value = json; }
        catch { doc.Variables.Add(TypographyVariable, json); }
    }

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
        return ReadTag((string)SelectedControl(selection).Tag).Ids;
    }

    private static dynamic SelectedControl(object selection)
    {
        dynamic selected = selection;
        dynamic? parent = selected.ParentContentControl;
        if (parent is not null && IsCitationTag((string)(parent.Tag ?? ""))) return parent;
        // Selecting the whole control can leave ParentContentControl empty.
        dynamic controls = selected.Range.ContentControls;
        if ((int)controls.Count == 1 && IsCitationTag((string)(controls[1].Tag ?? ""))) return controls[1];
        throw new InvalidOperationException("请选中一处 AcNote 引用或把光标放在引用内。");
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
        // Never nest a new citation inside an existing control, including at its
        // closing boundary. Use a fresh document range instead of a live Selection.
        var position = InsertionPosition(document, (int)wordSelection.Range.End);
        dynamic range = wordDocument.Range(position, position);
        dynamic control = wordDocument.ContentControls.Add(0, range);
        Prepare(control, BuildTag(form, ids));
        // A new control initially shows Word's placeholder. Give it real text so
        // refresh can distinguish it from an existing citation the user emptied.
        SetText(control, "…");
        var controlId = (string)control.ID;
        Refresh(wordDocument, style, lookup);
        MoveAfterControl(wordDocument, wordSelection, FindControl(document, controlId));
    }

    public static int InsertionPosition(object document, int position)
    {
        dynamic doc = document;
        bool moved;
        do
        {
            moved = false;
            for (var i = 1; i <= (int)doc.ContentControls.Count; i++)
            {
                dynamic control = doc.ContentControls[i];
                var tag = (string)(control.Tag ?? "");
                if (tag != BibliographyTag && !IsCitationTag(tag)) continue;
                var start = (int)control.Range.Start;
                var end = (int)control.Range.End;
                if (position < start || position > end) continue;
                if (tag == BibliographyTag)
                    throw new InvalidOperationException("请把光标移到正文后插入引用，不能插入参考文献列表内部。");
                position = end + 1;
                moved = true;
            }
        } while (moved);
        if (position > (int)doc.Content.End - 1)
            throw new InvalidOperationException("请在引用后按右方向键并换行，再插入文献。");
        return position;
    }

    private static dynamic FindControl(object document, string id)
    {
        dynamic doc = document;
        for (var i = 1; i <= (int)doc.ContentControls.Count; i++)
            if ((string)doc.ContentControls[i].ID == id) return doc.ContentControls[i];
        throw new InvalidOperationException("引用在更新过程中被删除，请撤销本次操作后重试。");
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
        dynamic control = SelectedControl(selection);
        if (ids.Count == 0)
        {
            RemoveSelectedAndRefresh(document, selection, style, lookup);
            return;
        }
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
        dynamic control = SelectedControl(selection);
        ValidateReferences(document, [], lookup, (string)control.ID);
        control.LockContentControl = false;
        control.LockContents = false;
        var position = Math.Max(0, (int)control.Range.Start - 1);
        // Delete contents and wrapper together; emptying Range.Text first creates
        // a placeholder and can leave a stale selection inside the old wrapper.
        control.Delete(true);
        dynamic selected = selection;
        selected.SetRange(position, position);
        selected.Font.Superscript = 0;
        Refresh(document, style, lookup);
    }

    public static string SelectionForm(object selection)
    {
        try
        {
            dynamic control = SelectedControl(selection);
            return ReadTag((string)(control.Tag ?? "")).Form;
        }
        catch
        {
            return "parenthetical";
        }
    }

    public static string BuildTag(string form, IReadOnlyList<string> ids) =>
        CitePrefix + (form == "narrative" ? "n" : form == "author-year" ? "a" : "p") + "|" + string.Join(';', ids.Distinct());

    public static void PrepareForConversion(object contentControl, string tag)
    {
        dynamic control = contentControl;
        Prepare(control, tag);
        // Use the same sentinel as newly inserted citations so Refresh never
        // mistakes an imported EndNote display string for user-edited prose.
        SetText(control, "…");
    }

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
        else if (body.StartsWith("a|", StringComparison.Ordinal))
        {
            form = "author-year";
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
            dynamic control = SelectedControl(selection);
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
        var typography = GetTypography(document);
        var citationFormat = DocumentTypography.Resolve(style.CitationFormat, typography.Citation);
        var bibliographyFormat = DocumentTypography.Resolve(style.BibliographyFormat, typography.Bibliography);
        var listOptions = BibliographyOptions.Resolve(style.BibliographyOptions, typography.List);
        var cites = new List<(int Start, int End, string Id, string Form, List<string> Ids)>();
        var citationLinks = new List<(string Id, string Text, List<CitationLinkSpan> Spans)>();
        dynamic? bibliography = null;
        var count = (int)wordDocument.ContentControls.Count;
        var boundaries = new List<(int Start, int End)>();
        for (var i = 1; i <= count; i++)
        {
            dynamic candidate = wordDocument.ContentControls[i];
            if (IsCitationTag((string)(candidate.Tag ?? "")))
                boundaries.Add(((int)candidate.Range.Start, (int)candidate.Range.End));
        }
        boundaries.Sort((a, b) => a.Start.CompareTo(b.Start));
        for (var i = 1; i < boundaries.Count; i++)
            if (boundaries[i].Start < boundaries[i - 1].End)
                throw new InvalidOperationException("检测到旧版生成的嵌套引用，已停止更新以保留正文。请先撤销出错的插入，再在引用外重新插入。");
        for (var index = count; index >= 1; index--)
        {
            dynamic control = wordDocument.ContentControls[index];
            var tag = (string)(control.Tag ?? "");
            if (tag.StartsWith(CitePrefix, StringComparison.Ordinal) || tag == BibliographyTag)
                HideAcNoteLabel(control);
            if (tag.StartsWith(CitePrefix, StringComparison.Ordinal))
            {
                if ((bool)control.ShowingPlaceholderText || string.IsNullOrWhiteSpace((string)control.Range.Text))
                {
                    control.LockContents = false;
                    control.LockContentControl = false;
                    control.Delete(true);
                    continue;
                }
                if (EditedCitation(wordDocument, control))
                {
                    control.LockContents = false;
                    control.LockContentControl = false;
                    control.Delete(false); // User text survives; stale metadata does not.
                    continue;
                }
                var parsed = ReadTag(tag);
                cites.Add(((int)control.Range.Start, (int)control.Range.End, (string)control.ID, parsed.Form, parsed.Ids));
            }
            else if (tag == BibliographyTag)
            {
                if ((bool)control.ShowingPlaceholderText)
                {
                    control.LockContents = false;
                    control.LockContentControl = false;
                    control.Delete(true);
                    continue;
                }
                // The bibliography is a generated block owned by AcNote. Keep
                // its control even if the user edited its text so the next
                // refresh can restore the complete canonical list in place.
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
        foreach (var cite in cites.AsEnumerable().Reverse())
        {
            var rendered = authorYear
                ? InTextFormatter.Format(cite.Ids.Select(id => records[id]), style.CitationEtAl, cite.Form == "narrative", years, style.CitationPunctuation)
                : CitationNumbers.Format(cite.Ids.Select(id => numbers[id]));
            dynamic current = FindControl(document, cite.Id);
            SetText(current, rendered);
            ApplyFormat(current.Range, citationFormat, false);
            RememberText(wordDocument, cite.Id, rendered);
            citationLinks.Add((cite.Id, rendered, BuildCitationLinkSpans(rendered, cite.Ids, authorYear,
                cite.Form == "narrative", records, numbers, years, style)));
            // Word removes the wrapper when the user edits/deletes its text.
            // This avoids empty "click here" placeholders and stale tags later
            // overwriting prose. Programmatic refresh disables this first.
            current.LockContentControl = false;
            current.Temporary = true;
        }

        if (order.Count == 0)
        {
            RemoveReferenceHyperlinks(wordDocument);
            RemoveReferenceBookmarks(wordDocument);
            if (bibliography is not null)
            {
                bibliography.LockContentControl = false;
                bibliography.LockContents = false;
                bibliography.Delete(true);
            }
            return;
        }

        var lines = new List<string>();
        var bibliographyIds = new List<string>();
        var numberedList = !authorYear || listOptions.NumberAuthorYear == true;
        var numberSeparator = bibliographyFormat.HangingIndent > 0 ? "\t" : " ";
        if (authorYear)
        {
            var entries = listOptions.Order(order.Select(id => records[id]));
            foreach (var entry in entries)
            {
                var prefix = numberedList ? $"[{lines.Count + 1}]" + numberSeparator : "";
                lines.Add(prefix + BibliographyFormatter.Format(entry, style, years.GetValueOrDefault(entry.Id)).Text);
                bibliographyIds.Add(entry.Id);
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
                lines.Add($"[{numbers[id]}]" + numberSeparator + body);
                bibliographyIds.Add(id);
            }
        }

        if (bibliography is null)
            bibliography = CreateBibliography(wordDocument);

        EnsureBibliographyParagraph(wordDocument, bibliography);
        RemoveReferenceHyperlinks(wordDocument);
        RemoveReferenceBookmarks(wordDocument);
        SetText(bibliography, string.Join("\r", lines));
        ApplyFormat(bibliography.Range, bibliographyFormat, true);
        if (numberedList && bibliographyFormat.HangingIndent is > 0)
        {
            // A real tab stop aligns first-line text with wrapped lines, instead
            // of estimating alignment with spaces after variable-width numbers.
            dynamic tabs = bibliography.Range.ParagraphFormat.TabStops;
            tabs.ClearAll();
            tabs.Add((float)bibliographyFormat.HangingIndent.Value, 0, 0);
        }
        RememberText(wordDocument, (string)bibliography.ID, string.Join("\r", lines));
        var bookmarks = CreateReferenceBookmarks(wordDocument, bibliography, bibliographyIds, lines);
        foreach (var citation in citationLinks)
        {
            dynamic current = FindControl(document, citation.Id);
            AddCitationHyperlinks(wordDocument, current, citation.Spans, bookmarks);
        }
    }

    private static List<CitationLinkSpan> BuildCitationLinkSpans(
        string rendered,
        IReadOnlyList<string> ids,
        bool authorYear,
        bool narrative,
        IReadOnlyDictionary<string, ReferenceItem> records,
        IReadOnlyDictionary<string, int> numbers,
        IReadOnlyDictionary<string, string> years,
        ResolvedStyle style)
    {
        var spans = new List<CitationLinkSpan>();
        if (!authorYear)
        {
            var idByNumber = numbers.ToDictionary(pair => pair.Value, pair => pair.Key);
            foreach (Match match in Regex.Matches(rendered, @"\d+"))
                if (int.TryParse(match.Value, out var number) && idByNumber.TryGetValue(number, out var id)
                    && ids.Contains(id, StringComparer.Ordinal))
                    spans.Add(new(match.Index, match.Length, id));
            return spans;
        }

        var offset = 0;
        foreach (var id in ids)
        {
            if (!records.TryGetValue(id, out var item)) continue;
            var piece = InTextFormatter.Format([item], style.CitationEtAl, narrative, years, style.CitationPunctuation);
            if (!narrative)
            {
                var chinese = LanguageDetector.Resolve(item) == "zh";
                var open = style.CitationPunctuation.OpenBracket ?? (chinese ? "（" : "(");
                var close = style.CitationPunctuation.CloseBracket ?? (chinese ? "）" : ")");
                if (open.Length > 0 && piece.StartsWith(open, StringComparison.Ordinal)) piece = piece[open.Length..];
                if (close.Length > 0 && piece.EndsWith(close, StringComparison.Ordinal)) piece = piece[..^close.Length];
            }
            if (piece.Length == 0) continue;
            var start = rendered.IndexOf(piece, offset, StringComparison.Ordinal);
            if (start < 0) continue;
            spans.Add(new(start, piece.Length, id));
            offset = start + piece.Length;
        }
        return spans;
    }

    private static string ReferenceBookmarkName(string id)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id)));
        return ReferenceBookmarkPrefix + hash[..24];
    }

    private static void RemoveReferenceBookmarks(dynamic document)
    {
        dynamic bookmarks = document.Bookmarks;
        for (var index = (int)bookmarks.Count; index >= 1; index--)
        {
            dynamic bookmark = bookmarks[index];
            if (((string)bookmark.Name).StartsWith(ReferenceBookmarkPrefix, StringComparison.Ordinal))
                bookmark.Delete();
        }
    }

    private static void RemoveReferenceHyperlinks(dynamic document)
    {
        dynamic hyperlinks = document.Hyperlinks;
        for (var index = (int)hyperlinks.Count; index >= 1; index--)
        {
            dynamic hyperlink = hyperlinks[index];
            try
            {
                var target = (string)hyperlink.SubAddress;
                if (target.StartsWith(ReferenceBookmarkPrefix, StringComparison.Ordinal))
                    hyperlink.Delete();
            }
            catch { /* Ignore unrelated or unsupported hyperlinks. */ }
        }
    }

    private static Dictionary<string, string> CreateReferenceBookmarks(
        dynamic document,
        dynamic bibliography,
        IReadOnlyList<string> ids,
        IReadOnlyList<string> lines)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var offset = 0;
        var start = (int)bibliography.Range.Start;
        for (var index = 0; index < Math.Min(ids.Count, lines.Count); index++)
        {
            var line = lines[index];
            if (line.Length > 0)
            {
                var name = ReferenceBookmarkName(ids[index]);
                dynamic target = document.Range(start + offset, start + offset + line.Length);
                document.Bookmarks.Add(name, target);
                result[ids[index]] = name;
            }
            offset += line.Length + 1; // Word paragraph marks are one character in Range positions.
        }
        return result;
    }

    private static void AddCitationHyperlinks(
        dynamic document,
        dynamic control,
        IReadOnlyList<CitationLinkSpan> spans,
        IReadOnlyDictionary<string, string> bookmarks)
    {
        var start = (int)control.Range.Start;
        foreach (var span in spans.Where(span => bookmarks.ContainsKey(span.ReferenceId)).OrderByDescending(span => span.Start))
        {
            dynamic anchor = document.Range(start + span.Start, start + span.Start + span.Length);
            document.Hyperlinks.Add(anchor, "", bookmarks[span.ReferenceId], "Ctrl+单击跳转到文末参考文献");
            // Keep the link functional without letting Word's default blue/underline
            // hyperlink style alter the citation's bibliography style.
            anchor.Font.Color = -16777216; // wdColorAutomatic
            anchor.Font.Underline = 0; // wdUnderlineNone
        }
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
        // In an otherwise empty document, Content.End - 1 can still be inside
        // the final citation. Insert after the full document range (including
        // its final paragraph mark), not at that ambiguous collapsed boundary.
        document.Content.InsertParagraphAfter();
        document.Content.InsertParagraphAfter();
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
        selection.Font.Superscript = 0;
    }

    private static void Prepare(dynamic control, string tag)
    {
        control.Tag = tag;
        HideAcNoteLabel(control);
        try { control.LockContentControl = false; } catch { /* Citations remain removable with normal Word editing. */ }
    }

    private static void HideAcNoteLabel(dynamic control)
    {
        try { control.Title = ""; } catch { /* Some WPS builds do not expose the title setter. */ }
        try { control.Appearance = 2; } catch { /* Older Word builds have no hidden appearance. */ }
    }

    private static void ValidateReferences(dynamic document, IReadOnlyList<string> ids, Func<string, ReferenceItem?> lookup, string? excludeControl = null)
    {
        var all = new HashSet<string>(ids);
        for (var i = 1; i <= (int)document.ContentControls.Count; i++)
        {
            dynamic control = document.ContentControls[i];
            if ((string)control.ID == excludeControl) continue;
            var tag = (string)(control.Tag ?? "");
            if (IsCitationTag(tag) && !(bool)control.ShowingPlaceholderText && !string.IsNullOrWhiteSpace((string)control.Range.Text) && !EditedCitation(document, control))
                all.UnionWith(ReadTag(tag).Ids);
        }
        foreach (var id in all)
            if (lookup(id) is null) throw new InvalidOperationException("找不到引用文献，未修改文档：" + id);
    }

    private static void ApplyFormat(dynamic range, TextLayout format, bool paragraph)
    {
        format.Validate();
        // Remove the previous AcNote overrides so "follow style" really resets
        // to the document's underlying Word style, then apply the current values.
        range.Font.Reset();
        if (!string.IsNullOrWhiteSpace(format.FontFamily)) range.Font.Name = format.FontFamily;
        if (!string.IsNullOrWhiteSpace(format.EastAsiaFontFamily)) range.Font.NameFarEast = format.EastAsiaFontFamily;
        if (format.FontSize is double size) range.Font.Size = (float)size;
        range.Font.Superscript = format.Superscript == true ? -1 : 0;
        if (!paragraph) return;
        dynamic p = range.ParagraphFormat;
        p.Reset();
        if (format.LineSpacing is double line) { p.LineSpacingRule = 5; p.LineSpacing = (float)(12 * line); }
        if (format.LineSpacingPoints is double points) { p.LineSpacingRule = 4; p.LineSpacing = (float)points; }
        if (format.SpaceBefore is double before) p.SpaceBefore = (float)before;
        if (format.SpaceAfter is double after) p.SpaceAfter = (float)after;
        if (format.HangingIndent is double indent) { p.LeftIndent = (float)indent; p.FirstLineIndent = (float)-indent; }
        if (format.Alignment is string alignment) p.Alignment = alignment switch { "center" => 1, "right" => 2, "justify" => 3, _ => 0 };
    }

    private static void SetText(dynamic control, string text)
    {
        control.LockContents = false;
        control.LockContentControl = false;
        control.Temporary = false;
        dynamic range = control.Range;
        range.Text = text ?? "";
    }
}
