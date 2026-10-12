using AcNote.Core;
using Xunit;

namespace AcNote.Core.Tests;

public class YearDisambiguationTests
{
    private static ReferenceItem Paper(string id, string title, string year = "2020") => new()
    {
        Id = id, Title = title, Year = year, Language = "en",
        Authors = [AuthorFormatter.Parse("Smith, John")],
    };

    [Fact]
    public void LabelsAreStableAndSharedWithoutChangingRecords()
    {
        var a = Paper("a", "Alpha");
        var b = Paper("b", "Beta");
        var rule = EtAlRule.CitationDefault();
        var years = YearDisambiguator.Resolve([b, a, b], rule);
        Assert.Equal("2020a", years["a"]);
        Assert.Equal("2020b", years["b"]);
        Assert.Equal("(Smith, 2020a; Smith, 2020b)", InTextFormatter.Format([a, b], rule, years: years));
        Assert.Equal("Smith (2020b)", InTextFormatter.Format([b], rule, true, years));
        var style = StyleCatalog.Load(Path.Combine(AppContext.BaseDirectory, "styles")).Get("author-year");
        Assert.Contains("2020a", BibliographyFormatter.Format(a, style, years[a.Id]).Text);
        Assert.Equal("2020", a.Year);
        Assert.Empty(YearDisambiguator.Resolve([a], rule));
    }

    [Fact]
    public void ChineseAndTruncatedAuthorCollisionsAreDisambiguated()
    {
        var a = Paper("a", "甲"); a.Language = "zh"; a.Authors = [AuthorFormatter.Parse("张三")];
        var b = Paper("b", "乙"); b.Language = "zh"; b.Authors = [AuthorFormatter.Parse("张三")];
        var years = YearDisambiguator.Resolve([a, b], EtAlRule.CitationDefault());
        Assert.Equal(2, years.Count);
        Assert.Contains(years[a.Id], InTextFormatter.Format([a], years: years));
        var x = Paper("x", "Alpha"); var y = Paper("y", "Beta");
        x.Authors.AddRange([AuthorFormatter.Parse("Jones, A"), AuthorFormatter.Parse("Brown, B")]);
        y.Authors.AddRange([AuthorFormatter.Parse("Doe, A"), AuthorFormatter.Parse("Green, B")]);
        Assert.Equal(2, YearDisambiguator.Resolve([x, y], EtAlRule.CitationDefault()).Count);
    }

    [Fact]
    public void MoreThan26WorksAndDifferentYears()
    {
        var items = Enumerable.Range(0, 28).Select(i => Paper(i.ToString(), i.ToString("D2"))).ToList();
        var labels = YearDisambiguator.Resolve(items, EtAlRule.CitationDefault());
        Assert.Equal("2020z", labels["25"]);
        Assert.Equal("2020aa", labels["26"]);
        Assert.Empty(YearDisambiguator.Resolve([Paper("a", "A"), Paper("b", "B", "2021")], EtAlRule.CitationDefault()));
    }

    [Fact]
    public void LayoutInheritancePreservesParentAndExplicitZero()
    {
        var parent = new StyleFile { Id = "base", BibliographyFormat = new() { FontSize = 12, SpaceAfter = 6, HangingIndent = 24 } };
        var child = new StyleFile { Id = "child", Extends = "base", BibliographyFormat = new() { SpaceAfter = 0 } };
        var result = StyleCatalog.Resolve(child, new Dictionary<string, StyleFile> { ["base"] = parent });
        Assert.Equal(12, result.BibliographyFormat.FontSize);
        Assert.Equal(0, result.BibliographyFormat.SpaceAfter);
        Assert.Equal(6, parent.BibliographyFormat.SpaceAfter);
        Assert.Throws<InvalidOperationException>(() => new TextLayout { FontSize = -1 }.Validate());
    }

    [Fact]
    public void SemicolonInLibraryPathIsSupported()
    {
        var folder = Directory.CreateTempSubdirectory("acnote-semicolon-");
        var store = new LibraryStore(Path.Combine(folder.FullName, "a;b.acnote"));
        store.Save(Paper("one", "Example"));
        Assert.Equal("Example", store.Find("one")!.Title);
    }

    [Fact]
    public void InvalidStyleCannotOverwriteInstalledStyle()
    {
        var folder = Directory.CreateTempSubdirectory("acnote-install-");
        var original = """{"id":"custom","name":"Original"}""";
        StyleCatalog.Install(original, folder.FullName);
        Assert.Throws<InvalidOperationException>(() => StyleCatalog.Install(
            """{"id":"custom","extends":"custom"}""", folder.FullName));
        Assert.Equal(original, File.ReadAllText(Path.Combine(folder.FullName, "custom.style")));
    }
}
