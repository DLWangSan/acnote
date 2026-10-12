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

    private static ReferenceItem Paper(string id, string title) => new()
    { Id = id, Title = title, Year = "2020", Language = "en", Authors = [AuthorFormatter.Parse("Smith, John")] };
}

public class TestDocument { public TestControls ContentControls { get; } = new(); public TestRange Range(int start, int end) => new() { Text = "\r" }; }
public class TestControls
{
    public List<TestControl> Items { get; } = [];
    public int Count => Items.Count;
    public TestControl this[int index] => Items[index - 1];
    public TestControl Add(string tag, int start)
    {
        var control = new TestControl { Tag = tag }; control.Range.Start = start; Items.Add(control); return control;
    }
}
public class TestControl
{
    public string Tag { get; set; } = "";
    public TestRange Range { get; } = new();
    public bool LockContents { get; set; }
    public bool LockContentControl { get; set; }
    public bool Deleted { get; private set; }
    public void Delete(bool deleteContents = false) => Deleted = true;
}
public class TestRange
{
    public int Start { get; set; }
    public string Text { get; set; } = "";
    public TestFont Font { get; } = new();
    public TestParagraph ParagraphFormat { get; } = new();
}
public class TestFont
{
    public string Name { get; set; } = "";
    public string NameFarEast { get; set; } = "";
    public float Size { get; set; }
    public int Superscript { get; set; }
}
public class TestParagraph
{
    public int LineSpacingRule { get; set; }
    public float LineSpacing { get; set; }
    public float SpaceBefore { get; set; }
    public float SpaceAfter { get; set; }
    public float LeftIndent { get; set; }
    public float FirstLineIndent { get; set; }
    public int Alignment { get; set; }
}
