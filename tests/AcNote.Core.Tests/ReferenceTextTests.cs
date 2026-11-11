using AcNote.Core;
using Xunit;

namespace AcNote.Core.Tests;

public class ReferenceTextTests
{
    [Theory]
    [InlineData("Impact of <span style=\"font-variant:small-caps;\">M</span>n", "Impact of Mn")]
    [InlineData(@"Impact of \textlessspan style=""font-variant:small-caps;""\textgreaterM\textless/span\textgreatern", "Impact of Mn")]
    [InlineData(@"\textless{}span style='font-variant:small-caps;'\textgreater{}Fe\textless{}/span\textgreater{}–Mn", "Fe–Mn")]
    [InlineData("&amp;lt;span&amp;gt;Fe&amp;lt;/span&amp;gt;", "Fe")]
    [InlineData("Fe<sub>20</sub>Cr<sub>6</sub>Mn<sub>6</sub>CN", "Fe20Cr6Mn6CN")]
    [InlineData("x < y > z and a < b > c; A & B", "x < y > z and a < b > c; A & B")]
    public void RemovesPresentationMarkupWhilePreservingText(string input, string expected)
    {
        Assert.Equal(expected, ReferenceText.Plain(input));
        Assert.Equal(expected, ReferenceText.Plain(ReferenceText.Plain(input)));
    }

    [Fact]
    public void ExistingLibraryRecordFormatsCleanlyWithoutMutatingStoredMetadata()
    {
        var title = @"Impact of \textlessspan style=""font-variant:small-caps;""\textgreaterM\textless/span\textgreatern on austenitic Fe–Mn alloys: A First-Principles Study";
        var item = new ReferenceItem { Type = "article-journal", Language = "en", Title = title, Year = "2014", ContainerTitle = "Journal of Computational Chemistry", Volume = "35", Issue = "31", Page = "2239-2244" };
        var style = StyleCatalog.Load(Path.Combine(AppContext.BaseDirectory, "styles")).Get("wust-doctoral-science");
        var output = BibliographyFormatter.Format(item, style).Text;
        Assert.Contains("Impact of Mn on austenitic Fe–Mn alloys: a first-principles study[J].", output);
        Assert.DoesNotContain("textless", output); Assert.DoesNotContain("font-variant", output);
        Assert.Equal(title, item.Title);
    }

    [Fact]
    public void SentenceCasePreservesChemicalFormulaeAndScientificIdentifiers()
    {
        var item = new ReferenceItem { Type = "article-journal", Language = "en", Title = "Combined Effects of Carbon and Nitrogen in Fe20Cr6Mn6CN Alloys Using 2D Models", Year = "2021" };
        var style = StyleCatalog.Load(Path.Combine(AppContext.BaseDirectory, "styles")).Get("wust-doctoral-science");
        Assert.Contains("Combined effects of carbon and nitrogen in Fe20Cr6Mn6CN alloys using 2D models[J].", BibliographyFormatter.Format(item, style).Text);
    }
}
