using AcNote.Core;
using AcNote.Word;

// Opt-in integration check. Creates and closes only its own unsaved documents.
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var type = Type.GetTypeFromProgID("Word.Application") ?? throw new InvalidOperationException("Word is not installed.");
        dynamic app = Activator.CreateInstance(type)!;
        dynamic? doc = null;
        var preview = args.Contains("--preview");
        try
        {
            if (preview)
            {
                app.Visible = true;
                doc = app.Documents.Add();
                doc.Content.Text = "AcNote 插件验证。";
                app.Selection.SetRange((int)doc.Content.End - 1, (int)doc.Content.End - 1);
                doc.SaveAs2(Path.Combine(AppContext.BaseDirectory, "AcNote-plugin-preview.docx"));
                Console.WriteLine("Preview document opened: " + (string)doc.Name);
                return;
            }
            app.Visible = false;
            doc = app.Documents.Add();
            doc.Content.Text = "正文测试。";
            var records = new[] { Paper("a", "Alpha"), Paper("b", "Beta") }.ToDictionary(x => x.Id);
            ReferenceItem? Lookup(string id) => records.GetValueOrDefault(id);
            var styles = StyleCatalog.Load(Path.Combine(AppContext.BaseDirectory, "styles"));
            var numeric = styles.Get(StyleCatalog.DefaultId);
            app.Selection.SetRange(6, 6);
            CitationDocument.InsertAndRefresh(doc, app.Selection, new[] { "a", "b" }, "parenthetical", numeric, (Func<string, ReferenceItem?>)Lookup);
            var text = (string)doc.Content.Text;
            Check(text.Contains("正文测试。[1,2]\r\r[1]"), "Bibliography starts after a blank paragraph", text);
            Check((int)doc.ContentControls.Count == 2, "Citation control survives link creation", text);
            Check((int)doc.Hyperlinks.Count == 2, "Each numeric citation links to its bibliography entry", text);
            Check((int)doc.Bookmarks.Count == 2, "Bibliography entries have internal jump targets", text);
            var targets = new List<string>();
            for (var i = 1; i <= (int)doc.Hyperlinks.Count; i++)
            {
                dynamic link = doc.Hyperlinks[i];
                var target = (string)link.SubAddress;
                Check((bool)doc.Bookmarks.Exists(target), "Citation hyperlink target exists", target);
                targets.Add(target);
            }
            Check(targets.Distinct(StringComparer.Ordinal).Count() == 2, "Grouped citation links target distinct entries", string.Join(",", targets));
            dynamic firstLink = doc.Hyperlinks[1];
            firstLink.Follow();
            var followedTarget = doc.Bookmarks[(string)firstLink.SubAddress];
            var selectionStart = (int)app.Selection.Range.Start;
            Check(selectionStart >= (int)followedTarget.Range.Start && selectionStart <= (int)followedTarget.Range.End,
                "Following a citation hyperlink lands on its bibliography entry", (string)doc.Content.Text);
            Check(!CitationDocument.SelectionIsCitation(app.Selection), "Caret exits the inserted control", text);
            var before = text;
            CitationDocument.Refresh(doc, numeric, (Func<string, ReferenceItem?>)Lookup);
            Check((string)doc.Content.Text == before, "Refresh is idempotent", (string)doc.Content.Text);
            var authorYear = styles.Get("author-year");
            authorYear.BibliographyFormat.FontSize = 12;
            authorYear.BibliographyFormat.HangingIndent = 24;
            CitationDocument.Refresh(doc, authorYear, (Func<string, ReferenceItem?>)Lookup);
            text = (string)doc.Content.Text;
            Check(text.Contains("2020a") && text.Contains("2020b"), "Shared year disambiguation", text);
            Check((int)doc.Hyperlinks.Count == 2, "Citation links survive style changes and refresh", text);
            dynamic bib = doc.SelectContentControlsByTag(CitationDocument.BibliographyTag)[1];
            Check((float)bib.Range.ParagraphFormat.FirstLineIndent == -24, "Hanging indent is applied", text);
            // Reproduce the old inline bibliography and repair it on refresh.
            var start = (int)bib.Range.Start - 1;
            doc.Range(start - 2, start).Delete();
            CitationDocument.Refresh(doc, authorYear, (Func<string, ReferenceItem?>)Lookup);
            text = (string)doc.Content.Text;
            Check(text.Contains(")\rSmith") || text.Contains(")\r\rSmith"), "Legacy inline bibliography is separated", text);
            before = text;
            CitationDocument.Refresh(doc, authorYear, (Func<string, ReferenceItem?>)Lookup);
            Check((string)doc.Content.Text == before, "Repair does not add repeated blank lines", (string)doc.Content.Text);
            dynamic cite = doc.ContentControls[1];
            app.Selection.SetRange((int)cite.Range.Start, (int)cite.Range.Start);
            CitationDocument.RemoveSelectedAndRefresh(doc, app.Selection, authorYear, (Func<string, ReferenceItem?>)Lookup);
            Check((int)doc.ContentControls.Count == 0, "Removing last citation removes the bibliography", (string)doc.Content.Text);
            Check((int)doc.Hyperlinks.Count == 0 && (int)doc.Bookmarks.Count == 0, "Removing all citations removes their navigation links", (string)doc.Content.Text);
            Check(!((string)doc.Content.Text).Contains("Smith"), "No stale bibliography text remains", (string)doc.Content.Text);
            doc.Close(0);
            doc = app.Documents.Add();
            doc.Content.Text = "正文。";
            var many = Enumerable.Range(0, 12).Select(i => Paper(Guid.NewGuid().ToString("N"), "Paper " + i)).ToDictionary(item => item.Id);
            app.Selection.SetRange(3, 3);
            CitationDocument.InsertAndRefresh(doc, app.Selection, many.Keys.ToList(), "parenthetical", numeric,
                (Func<string, ReferenceItem?>)(id => many.GetValueOrDefault(id)));
            Check(((string)doc.Content.Text).Contains("[1-12]"), "Twelve selected references can be inserted together", (string)doc.Content.Text);
            Check(CitationDocument.ReadTag((string)doc.ContentControls[1].Tag).Ids.Count == 12, "All record IDs survive Word tag storage", (string)doc.ContentControls[1].Tag);

        }
        finally
        {
            if (!preview)
            {
                if (doc is not null) doc.Close(0);
                app.Quit(0);
            }
        }
    }

    private static ReferenceItem Paper(string id, string title) => new()
    { Id = id, Title = title, Year = "2020", Language = "en", Authors = [AuthorFormatter.Parse("Smith, John")] };

    private static void Check(bool condition, string label, string text)
    {
        if (!condition) throw new InvalidOperationException(label + ": " + text.Replace("\r", "\\r"));
        Console.WriteLine("PASS " + label);
    }
}
