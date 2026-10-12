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
    public void LocalNarrativeWorksWithNumericStyleAndCanBeRestored()
    {
        var doc = new TestDocument();
        var cite = doc.ContentControls.Add(CitationDocument.BuildTag("narrative", ["a"]), 0);
        doc.ContentControls.Add(CitationDocument.BibliographyTag, 100);
        CitationDocument.Refresh(doc, new(), id => Paper(id, id));
        Assert.Equal("Smith (2020)", cite.Range.Text);
        Assert.False(cite.LockContents);
        cite.Tag = CitationDocument.BuildTag("author-year", ["a"]);
        CitationDocument.Refresh(doc, new(), id => Paper(id, id));
        Assert.Equal("(Smith, 2020)", cite.Range.Text);
        cite.Tag = CitationDocument.BuildTag("parenthetical", ["a"]);
        CitationDocument.Refresh(doc, new(), id => Paper(id, id));
        Assert.Equal("[1]", cite.Range.Text);
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
        CitationDocument.ReplaceSelectedAndRefresh(doc, selection, ["a"], "narrative", new(), id => Paper(id, id));
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
        var control = new TestControl { Tag = tag }; control.Range.Start = start; control.Range.End = start + 5; Items.Add(control); return control;
    }
}
public class TestControl
{
    public string ID { get; } = Guid.NewGuid().ToString();
    public string Tag { get; set; } = "";
    public string Title { get; set; } = "";
    public int Appearance { get; set; }
    public TestRange Range { get; } = new();
    public bool LockContents { get; set; }
    public bool LockContentControl { get; set; }
    public bool Deleted { get; private set; }
    public void Delete(bool deleteContents = false) => Deleted = true;
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
    public void Reset() { LineSpacingRule = 0; LineSpacing = 0; SpaceBefore = 0; SpaceAfter = 0; LeftIndent = 0; FirstLineIndent = 0; Alignment = 0; }
    public int LineSpacingRule { get; set; }
    public float LineSpacing { get; set; }
    public float SpaceBefore { get; set; }
    public float SpaceAfter { get; set; }
    public float LeftIndent { get; set; }
    public float FirstLineIndent { get; set; }
    public int Alignment { get; set; }
}
