using AcNote.Core;
using Xunit;

namespace AcNote.Core.Tests;

public class StyleEditingTests
{
    private static StyleCatalog Catalog() => StyleCatalog.Load(Path.Combine(AppContext.BaseDirectory, "styles"));

    [Theory]
    [InlineData("pku")]
    [InlineData("thu")]
    public void SchoolStylesUseOfficialFixedLineSpacing(string id)
    {
        var style = Catalog().Get(id);
        Assert.Equal("source-reviewed", style.Status);
        Assert.StartsWith("https://", style.Source);
        Assert.Equal(16, style.BibliographyFormat.LineSpacingPoints);
        Assert.Null(style.BibliographyFormat.LineSpacing);
        Assert.Equal(10.5, style.BibliographyFormat.FontSize);
        Assert.Equal(3, style.BibliographyFormat.SpaceBefore);
        Assert.Equal(0, style.BibliographyFormat.SpaceAfter);
        var doc = new TestDocument();
        doc.ContentControls.Add(AcNote.Word.CitationDocument.BuildTag("p", ["one"]), 0);
        var bib = doc.ContentControls.Add(AcNote.Word.CitationDocument.BibliographyTag, 100);
        AcNote.Word.CitationDocument.Refresh(doc, style, _ => new() { Id = "one", Title = "Example" });
        Assert.Equal(4, bib.Range.ParagraphFormat.LineSpacingRule);
        Assert.Equal(16, bib.Range.ParagraphFormat.LineSpacing);
    }

    [Fact]
    public void SysuJournalUsesHandbookTemplateWithoutGenericJMarker()
    {
        var text = BibliographyFormatter.Format(new() { Type = "article-journal", Authors = [AuthorFormatter.Parse("张三")], Title = "研究", ContainerTitle = "学报", Year = "2024", Volume = "12", Issue = "3", Page = "1-8" }, Catalog().Get("sysu")).Text;
        Assert.Equal("张三. 研究. 学报, 2024, 12(3): 1-8", text);
    }

    [Fact]
    public void MdpiPreprintUsesArchiveIdAndOnlineAddress()
    {
        var style = Catalog().Get("mdpi");
        var item = new ReferenceItem
        {
            Type = "preprint",
            Authors = [AuthorFormatter.Parse("Khanam, Rahima"), AuthorFormatter.Parse("Hussain, Muhammad")],
            Title = "YOLOv11: An Overview of the Key Architectural Enhancements",
            ContainerTitle = "arXiv",
            Year = "2024",
            Url = "https://arxiv.org/abs/2410.17725",
            Extra = new(StringComparer.OrdinalIgnoreCase) { ["archive-id"] = "2410.17725" },
        };

        Assert.Equal("Khanam, R.; Hussain, M. YOLOv11: An Overview of the Key Architectural Enhancements. arXiv 2024, arXiv:2410.17725. Available online: https://arxiv.org/abs/2410.17725.",
            BibliographyFormatter.Format(item, style).Text);
        Assert.Empty(MissingFieldChecker.Check(item, style));
    }

    [Fact]
    public void OneClickDoiIsIdempotentAndMissingDoiLeavesNoLabel()
    {
        var style = Catalog().Get("author-year");
        var original = style.Templates["article-journal"];
        var copy = TemplateEditing.Copy(original);
        TemplateEditing.AddField(copy, "doi"); TemplateEditing.AddField(copy, "doi");
        Assert.Single(copy, t => t.Field == "doi");
        Assert.DoesNotContain(original, t => t.Field == "doi");
        style.Templates["article-journal"] = copy;
        var item = new ReferenceItem { Title = "Example", Doi = "https://doi.org/10.1234/example" };
        Assert.Contains("DOI: 10.1234/example", BibliographyFormatter.Format(item, style).Text);
        item.Doi = "";
        Assert.DoesNotContain("DOI:", BibliographyFormatter.Format(item, style).Text);
        TemplateEditing.AddField(copy, "doi-url", true);
        Assert.Single(copy, t => t.Field is "doi" or "doi-url");
        Assert.Contains(copy, t => t.Field == "doi-url");
    }

    [Fact]
    public void DoiLinkDoesNotDuplicateExistingResolverPrefix()
    {
        var style = new ResolvedStyle(); style.Templates["document"] = [];
        TemplateEditing.AddField(style.Templates["document"], "doi-url", true);
        Assert.Equal("https://doi.org/10.1234/example.", BibliographyFormatter.Format(new() { Type = "document", Doi = "doi: 10.1234/example" }, style).Text);
    }

    [Fact]
    public void LegacyMigrationDoesNotOverwriteCustomizedSchoolStyle()
    {
        Assert.True(AppPaths.IsLegacySchoolPlaceholder("""{"id":"thu","status":"unverified","extends":"gb-t-7714-2015-numeric","name":"清华大学","group":"university"}"""));
        Assert.False(AppPaths.IsLegacySchoolPlaceholder("""{"id":"thu","status":"unverified","templates":{}}"""));
        Assert.False(AppPaths.IsLegacySchoolPlaceholder("""{"id":"thu","status":"custom"}"""));
    }

    [Fact]
    public void DocumentMultiplierCanOverrideSchoolFixedSpacing()
    {
        var result = DocumentTypography.Resolve(Catalog().Get("thu").BibliographyFormat, new() { LineSpacing = 1.5 });
        Assert.Equal(1.5, result.LineSpacing);
        Assert.Null(result.LineSpacingPoints);
    }
}
