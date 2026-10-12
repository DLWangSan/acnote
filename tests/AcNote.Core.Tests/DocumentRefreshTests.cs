using AcNote.Core;
using AcNote.Word;
using Xunit;

namespace AcNote.Core.Tests;

public class DocumentRefreshTests
{
    [Fact]
    public void RefreshSharesYearLabelsAndAppliesOnlyBibliographyParagraphs()
    {
        var a = Paper("a", "Alpha"); var b = Paper("b", "Beta");
        var doc = new TestDocument();
        var cite = doc.ContentControls.Add(CitationDocument.BuildTag("p", ["b", "a"]), 0);
        var bib = doc.ContentControls.Add(CitationDocument.BibliographyTag, 100);
        var style = StyleCatalog.Load(Path.Combine(AppContext.BaseDirectory, "styles")).Get("author-year");
        style.CitationFormat.FontSize = 10;
        style.BibliographyFormat.FontSize = 12;
        style.BibliographyFormat.HangingIndent = 24;
        style.BibliographyFormat.LineSpacing = 1.5;
        CitationDocument.Refresh(doc, style, id => id == "a" ? a : b);
        Assert.Equal("(Smith, 2020b; Smith, 2020a)", cite.Range.Text);
        Assert.Contains("2020a", bib.Range.Text);
        Assert.Contains("2020b", bib.Range.Text);
        Assert.True(bib.Range.Text.IndexOf("Alpha", StringComparison.Ordinal) < bib.Range.Text.IndexOf("Beta", StringComparison.Ordinal));
        Assert.Equal(10, cite.Range.Font.Size);
        Assert.Equal(12, bib.Range.Font.Size);
        Assert.Equal(-24, bib.Range.ParagraphFormat.FirstLineIndent);
        Assert.Equal(0, cite.Range.ParagraphFormat.FirstLineIndent);
        Assert.Equal(18, bib.Range.ParagraphFormat.LineSpacing);
    }

    [Fact]
    public void MissingReferenceNeverOverwritesExistingText()
    {
        var doc = new TestDocument();
        var cite = doc.ContentControls.Add(CitationDocument.BuildTag("p", ["gone"]), 0);
        cite.Range.Text = "[1]";
        var bib = doc.ContentControls.Add(CitationDocument.BibliographyTag, 100);
        bib.Range.Text = "Keep bibliography";
        Assert.Throws<InvalidOperationException>(() => CitationDocument.Refresh(doc, new ResolvedStyle(), _ => null));
        Assert.Equal("[1]", cite.Range.Text);
        Assert.Equal("Keep bibliography", bib.Range.Text);
    }

    [Fact]
    public void ReordersNumbersAndRemovesBibliographyAfterLastCitation()
    {
        var doc = new TestDocument();
        var second = doc.ContentControls.Add(CitationDocument.BuildTag("p", ["b", "a"]), 10);
        var first = doc.ContentControls.Add(CitationDocument.BuildTag("p", ["a"]), 0);
        var bib = doc.ContentControls.Add(CitationDocument.BibliographyTag, 100);
        CitationDocument.Refresh(doc, new ResolvedStyle(), id => Paper(id, id));
        Assert.Equal("[1]", first.Range.Text);
        Assert.Equal("[1,2]", second.Range.Text);
        doc.ContentControls.Items.Remove(first); doc.ContentControls.Items.Remove(second);
        bib.LockContentControl = true;
        CitationDocument.Refresh(doc, new ResolvedStyle(), _ => null);
        Assert.True(bib.Deleted);
        Assert.False(bib.LockContentControl);
    }

    [Fact]
    public void InsertionMovesOutsideCitationButRejectsBibliography()
    {
        var doc = new TestDocument();
        var cite = doc.ContentControls.Add(CitationDocument.BuildTag("p", ["a"]), 10);
        var bib = doc.ContentControls.Add(CitationDocument.BibliographyTag, 100);
        Assert.Equal(16, CitationDocument.InsertionPosition(doc, 12));
        Assert.Equal(16, CitationDocument.InsertionPosition(doc, 15));
        Assert.Equal(20, CitationDocument.InsertionPosition(doc, 20));
        Assert.Throws<InvalidOperationException>(() => CitationDocument.InsertionPosition(doc, 102));
    }

    [Fact]
    public void NestedLegacyCitationsAreRejectedBeforeAnyTextChanges()
    {
        var doc = new TestDocument();
        var outer = doc.ContentControls.Add(CitationDocument.BuildTag("p", ["a"]), 10);
        outer.Range.End = 30; outer.Range.Text = "[1] 用户正文 [2]";
        var inner = doc.ContentControls.Add(CitationDocument.BuildTag("p", ["b"]), 20);
        Assert.Throws<InvalidOperationException>(() => CitationDocument.Refresh(doc, new(), id => Paper(id, id)));
        Assert.Equal("[1] 用户正文 [2]", outer.Range.Text);
        Assert.False(inner.Deleted);
    }

    [Fact]
    public void StyleSwitchOverridesDisplayAndRestoresStoredNarrativeChoice()
    {
        var doc = new TestDocument();
        var cite = doc.ContentControls.Add(CitationDocument.BuildTag("narrative", ["a"]), 0);
        doc.ContentControls.Add(CitationDocument.BibliographyTag, 100);
        var authorYear = new ResolvedStyle { Layout = "author-year" };
        CitationDocument.Refresh(doc, authorYear, id => Paper(id, id));
        Assert.Equal("Smith (2020)", cite.Range.Text);
        Assert.False(cite.LockContents);
        CitationDocument.Refresh(doc, new(), id => Paper(id, id));
        Assert.Equal("[1]", cite.Range.Text);
        CitationDocument.Refresh(doc, authorYear, id => Paper(id, id));
        Assert.Equal("Smith (2020)", cite.Range.Text);
        cite.Tag = CitationDocument.BuildTag("author-year", ["a"]);
        CitationDocument.Refresh(doc, new(), id => Paper(id, id));
        Assert.Equal("[1]", cite.Range.Text);
        CitationDocument.Refresh(doc, authorYear, id => Paper(id, id));
        Assert.Equal("(Smith, 2020)", cite.Range.Text);
    }

    [Fact]
    public void DocumentTypographyPersistsWithoutChangingStyleOrOtherDocuments()
    {
        var doc = new TestDocument();
        var cite = doc.ContentControls.Add(CitationDocument.BuildTag("p", ["a"]), 0);
        var bib = doc.ContentControls.Add(CitationDocument.BibliographyTag, 100);
        var style = new ResolvedStyle(); style.CitationFormat.FontSize = 9;
        CitationDocument.SetTypography(doc, new() { Citation = new() { FontSize = 12 }, Bibliography = new() { SpaceAfter = 6, HangingIndent = 24 } });
        CitationDocument.Refresh(doc, style, id => Paper(id, id));
        Assert.Equal(12, cite.Range.Font.Size);
        Assert.Equal(6, bib.Range.ParagraphFormat.SpaceAfter);
        Assert.Equal(0, cite.Range.ParagraphFormat.SpaceAfter);
        Assert.Equal(9, style.CitationFormat.FontSize);
        Assert.Equal(12, CitationDocument.GetTypography(doc).Citation.FontSize);
        Assert.Null(CitationDocument.GetTypography(new TestDocument()).Citation.FontSize);
        var saved = doc.Variables[CitationDocument.TypographyVariable].Value;
        Assert.Throws<InvalidOperationException>(() => CitationDocument.SetTypography(doc, new() { Citation = new() { FontSize = -1 } }));
        Assert.Equal(saved, doc.Variables[CitationDocument.TypographyVariable].Value);
        CitationDocument.SetTypography(doc, new());
        CitationDocument.Refresh(doc, style, id => Paper(id, id));
        Assert.Equal(9, cite.Range.Font.Size);
        Assert.Equal(0, bib.Range.ParagraphFormat.SpaceAfter);
        Assert.Equal(0, bib.Range.ParagraphFormat.FirstLineIndent);
    }

    [Fact]
    public void EntireControlSelectionSupportsLocalConversion()
    {
        var doc = new TestDocument();
        var cite = doc.ContentControls.Add(CitationDocument.BuildTag("parenthetical", ["a"]), 0);
        doc.ContentControls.Add(CitationDocument.BibliographyTag, 100);
        var selection = new TestSelection();
        selection.Range.ContentControls.Items.Add(cite);
        Assert.True(CitationDocument.SelectionIsCitation(selection));
        CitationDocument.ReplaceSelectedAndRefresh(doc, selection, ["a"], "narrative", new() { Layout = "author-year" }, id => Paper(id, id));
        Assert.Equal("Smith (2020)", cite.Range.Text);
    }

    [Fact]
    public void RefreshUnlocksPreviousVersionCitationAndCreatesSeparateBibliography()
    {
        var doc = new TestDocument();
        var cite = doc.ContentControls.Add(CitationDocument.BuildTag("parenthetical", ["a"]), 0);
        cite.LockContents = true;
        cite.Range.End = 5;
        doc.Content.End = 6;
        CitationDocument.Refresh(doc, new(), id => Paper(id, id));
        Assert.False(cite.LockContents);
        Assert.Equal(2, doc.Content.ParagraphsAppended);
        var bibliography = Assert.Single(doc.ContentControls.Items, c => c.Tag == CitationDocument.BibliographyTag);
        Assert.True(bibliography.Range.Start > cite.Range.End);
        CitationDocument.Refresh(doc, new(), id => Paper(id, id));
        Assert.False(cite.LockContents);
        Assert.Equal(2, doc.Content.ParagraphsAppended);
    }

    [Fact]
    public void DeleteRemovesWrapperAndCollapsesSelection()
    {
        var doc = new TestDocument();
        var cite = doc.ContentControls.Add(CitationDocument.BuildTag("p", ["a"]), 10);
        var bib = doc.ContentControls.Add(CitationDocument.BibliographyTag, 100);
        var selection = new TestSelection { ParentContentControl = cite };
        CitationDocument.RemoveSelectedAndRefresh(doc, selection, new(), id => Paper(id, id));
        Assert.Empty(doc.ContentControls.Items);
        Assert.Equal("", cite.Range.Text);
        Assert.True(bib.Deleted);
        Assert.Equal(selection.Range.Start, selection.Range.End);
        Assert.Null(selection.ParentContentControl);
    }

    [Fact]
    public void EmptyLegacyCitationIsRemovedAndNeverResurrected()
    {
        var doc = new TestDocument();
        var cite = doc.ContentControls.Add(CitationDocument.BuildTag("p", ["missing"]), 0);
        cite.ShowingPlaceholderText = true;
        var bib = doc.ContentControls.Add(CitationDocument.BibliographyTag, 100);
        CitationDocument.Refresh(doc, new(), _ => null);
        Assert.True(cite.Deleted);
        Assert.True(bib.Deleted);
    }

    [Fact]
    public void UserTypingAfterDeletingCitationCannotBeOverwrittenByRefresh()
    {
        var doc = new TestDocument();
        var cite = doc.ContentControls.Add(CitationDocument.BuildTag("p", ["a"]), 0);
        doc.ContentControls.Add(CitationDocument.BibliographyTag, 100);
        CitationDocument.Refresh(doc, new(), id => Paper(id, id));
        Assert.True(cite.Temporary);
        cite.UserEdit("这是删除引用后新写的正文");
        CitationDocument.Refresh(doc, new(), _ => null);
        Assert.Equal("这是删除引用后新写的正文", cite.Range.Text);
        Assert.True(cite.Deleted);
        Assert.Empty(doc.ContentControls.Items);
    }

    [Fact]
    public void StaleWrapperContainingProseIsDetachedWithoutDeletingText()
    {
        var doc = new TestDocument();
        var cite = doc.ContentControls.Add(CitationDocument.BuildTag("p", ["a"]), 0);
        doc.ContentControls.Add(CitationDocument.BibliographyTag, 100);
        CitationDocument.Refresh(doc, new(), id => Paper(id, id));
        // Emulate a legacy editor leaving a wrapper around later prose.
        cite.Range.Text = "[1] 后面新写的正文";
        CitationDocument.Refresh(doc, new(), _ => null);
        Assert.True(cite.Deleted);
        Assert.Equal("[1] 后面新写的正文", cite.Range.Text);
    }

    [Fact]
    public void BundledNumericStyleIsSuperscriptButAuthorYearIsNot()
    {
        var catalog = StyleCatalog.Load(Path.Combine(AppContext.BaseDirectory, "styles"));
        Assert.True(catalog.Get(StyleCatalog.DefaultId).CitationFormat.Superscript);
        Assert.False(catalog.Get("author-year").CitationFormat.Superscript);
    }

    [Fact]
    public void StylePunctuationSupportsNoSpaceAndCustomComma()
    {
        var punctuation = new CitationPunctuation { NarrativeSeparator = "", AuthorYearSeparator = " ", OpenBracket = "(", CloseBracket = ")" };
        Assert.Equal("Smith(2020)", InTextFormatter.Format([Paper("a", "a")], narrative: true, punctuation: punctuation));
        Assert.Equal("(Smith 2020)", InTextFormatter.Format([Paper("a", "a")], punctuation: punctuation));
        punctuation.AuthorYearSeparator = ",";
        Assert.Equal("(Smith,2020)", InTextFormatter.Format([Paper("a", "a")], punctuation: punctuation));
    }

    [Fact]
    public void AuthorYearGroupingAndOptionalNumbersDoNotChangeInlineCitations()
    {
        var doc = new TestDocument();
        var english = Paper("en", "English title");
        var chinese = new ReferenceItem { Id = "zh", Title = "中文题名", Language = "zh", Year = "2021", Authors = [AuthorFormatter.Parse("张三")] };
        var cite = doc.ContentControls.Add(CitationDocument.BuildTag("narrative", ["en", "zh"]), 0);
        var bib = doc.ContentControls.Add(CitationDocument.BibliographyTag, 100);
        var catalog = StyleCatalog.Load(Path.Combine(AppContext.BaseDirectory, "styles"));
        var style = catalog.Get("author-year");
        ReferenceItem Lookup(string id) => id == "zh" ? chinese : english;
        CitationDocument.Refresh(doc, style, Lookup);
        Assert.StartsWith("Smith", bib.Range.Text);
        var inline = cite.Range.Text;
        CitationDocument.SetTypography(doc, new() { List = new() { LanguageOrder = "western-first", NumberAuthorYear = true } });
        CitationDocument.Refresh(doc, style, Lookup);
        Assert.StartsWith("[1]\tSmith", bib.Range.Text);
        Assert.Contains("\r[2]\t张三", bib.Range.Text);
        Assert.Equal(inline, cite.Range.Text);
        Assert.Equal(21, bib.Range.ParagraphFormat.TabStops.Position);
        CitationDocument.Refresh(doc, catalog.Get(StyleCatalog.DefaultId), Lookup);
        Assert.Equal("[1,2]", cite.Range.Text);
        Assert.StartsWith("[1]\tSmith", bib.Range.Text);
        CitationDocument.Refresh(doc, style, Lookup);
        Assert.Equal(inline, cite.Range.Text);
    }

    [Fact]
    public void BibliographyDefaultsAreReadableAndExplicitSettingsWin()
    {
        var style = StyleCatalog.Resolve(new StyleFile { Id = "test" }, new Dictionary<string, StyleFile>());
        Assert.Equal(10.5, style.BibliographyFormat.FontSize);
        Assert.Equal(21, style.BibliographyFormat.HangingIndent);
        Assert.Equal(1.25, style.BibliographyFormat.LineSpacing);
        Assert.Equal(6, style.BibliographyFormat.SpaceAfter);
        Assert.Equal("left", style.BibliographyFormat.Alignment);
        var explicitStyle = StyleCatalog.Resolve(new StyleFile { Id = "test", BibliographyFormat = new() { HangingIndent = 0, SpaceAfter = 0, FontSize = 12 } }, new Dictionary<string, StyleFile>());
        Assert.Equal(0, explicitStyle.BibliographyFormat.HangingIndent);
        Assert.Equal(0, explicitStyle.BibliographyFormat.SpaceAfter);
        Assert.Equal(12, explicitStyle.BibliographyFormat.FontSize);
    }

    [Fact]
    public void LanguageOrderingDoesNotDependOnCurrentCulture()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        var zh = new ReferenceItem { Id = "zh", Language = "zh", Authors = [AuthorFormatter.Parse("张三")] };
        var en = Paper("en", "English");
        try
        {
            foreach (var culture in new[] { "en-US", "zh-CN" })
            {
                System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(culture);
                Assert.Equal(new[] { "en", "zh" }, new BibliographyOptions().Order([zh, en]).Select(i => i.Id));
                Assert.Equal(new[] { "zh", "en" }, new BibliographyOptions { LanguageOrder = "chinese-first" }.Order([en, zh]).Select(i => i.Id));
                Assert.Equal(new[] { "en", "zh" }, new BibliographyOptions { LanguageOrder = "western-first" }.Order([zh, en]).Select(i => i.Id));
            }
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = previous; }
    }

    private static ReferenceItem Paper(string id, string title) => new()
    { Id = id, Title = title, Year = "2020", Language = "en", Authors = [AuthorFormatter.Parse("Smith, John")] };
}

public class TestDocument
{
    public TestControls ContentControls { get; } = new();
    public TestRange Content { get; } = new() { End = 1000 };
    public TestVariables Variables { get; } = new();
    public TestRange Range(int start, int end) => new() { Start = start, End = end, Text = "\r" };
}
public class TestVariables : Dictionary<string, TestVariable>
{
    public void Add(string name, string value) => this[name] = new() { Value = value };
}
public class TestVariable { public string Value { get; set; } = ""; }
public class TestControls
{
    public List<TestControl> Items { get; } = [];
    public int Count => Items.Count;
    public TestControl this[int index] => Items[index - 1];
    public TestControl Add(int type, TestRange range) => Add("", range.Start);
    public TestControl Add(string tag, int start)
    {
        var control = new TestControl { Tag = tag, Owner = this }; control.Range.Text = "[1]"; control.Range.Start = start; control.Range.End = start + 5; Items.Add(control); return control;
    }
}
public class TestControl
{
    public TestControls? Owner { get; set; }
    public bool Temporary { get; set; }
    public bool ShowingPlaceholderText { get; set; }
    public string ID { get; } = Guid.NewGuid().ToString();
    public string Tag { get; set; } = "";
    public string Title { get; set; } = "";
    public int Appearance { get; set; }
    public TestRange Range { get; } = new();
    public bool LockContents { get; set; }
    public bool LockContentControl { get; set; }
    public bool Deleted { get; private set; }
    public void Delete(bool deleteContents = false) { Deleted = true; if (deleteContents) Range.Text = ""; Owner?.Items.Remove(this); }
    public void UserEdit(string text) { Range.Text = text; if (Temporary) Delete(false); else ShowingPlaceholderText = text.Length == 0; }
}
public class TestRange
{
    public TestControls ContentControls { get; } = new();
    public int Start { get; set; }
    public int End { get; set; }
    public int ParagraphsAppended { get; private set; }
    public void InsertParagraphAfter() { End++; ParagraphsAppended++; }
    public string Text { get; set; } = "";
    public TestFont Font { get; } = new();
    public TestParagraph ParagraphFormat { get; } = new();
}
public class TestSelection
{
    public TestFont Font { get; } = new();
    public void SetRange(int start, int end) { Range.Start = start; Range.End = end; ParentContentControl = null; Range.ContentControls.Items.Clear(); }
    public TestControl? ParentContentControl { get; set; }
    public TestRange Range { get; } = new();
}
public class TestFont
{
    public void Reset() { Name = ""; NameFarEast = ""; Size = 0; Superscript = 0; }
    public string Name { get; set; } = "";
    public string NameFarEast { get; set; } = "";
    public float Size { get; set; }
    public int Superscript { get; set; }
}
public class TestParagraph
{
    public TestTabStops TabStops { get; } = new();
    public void Reset() { LineSpacingRule = 0; LineSpacing = 0; SpaceBefore = 0; SpaceAfter = 0; LeftIndent = 0; FirstLineIndent = 0; Alignment = 0; }
    public int LineSpacingRule { get; set; }
    public float LineSpacing { get; set; }
    public float SpaceBefore { get; set; }
    public float SpaceAfter { get; set; }
    public float LeftIndent { get; set; }
    public float FirstLineIndent { get; set; }
    public int Alignment { get; set; }
}
public class TestTabStops
{
    public float Position { get; private set; }
    public void ClearAll() => Position = 0;
    public void Add(float position, int alignment, int leader) => Position = position;
}
