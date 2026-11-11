using AcNote.Core;
using Xunit;

namespace AcNote.Core.Tests;

public class AuthorSeparatorTests
{
    private static ReferenceItem EnglishPaper => new()
    {
        Language = "en", Year = "2024",
        Authors = [AuthorFormatter.Parse("Smith, John"), AuthorFormatter.Parse("Brown, Alice")],
    };

    [Fact]
    public void MixedCitationUsesEachReferencesLanguageForAuthorSeparators()
    {
        var rule = EtAlRule.CitationDefault();
        rule.MinCount = 5;
        rule.AuthorSeparatorZh = "，";
        rule.AuthorSeparatorEn = ", ";
        Assert.Equal("（张三，李四，王五，赵六，2024；Smith, Brown，2024）", InTextFormatter.Format([Paper, EnglishPaper], rule));
        Assert.Equal("张三，李四，王五，赵六（2024）；Smith, Brown（2024）", InTextFormatter.Format([Paper, EnglishPaper], rule, narrative: true));
    }

    [Fact]
    public void BibliographyUsesSeparateChineseAndEnglishSettings()
    {
        var rule = EtAlRule.Default();
        rule.MinCount = 5;
        rule.AuthorSeparatorZh = "，";
        rule.AuthorSeparatorEn = ", ";
        Assert.Equal("张三，李四，王五，赵六", AuthorFormatter.FormatList(Paper, rule));
        Assert.Equal("Smith J, Brown A", AuthorFormatter.FormatList(EnglishPaper, rule));
    }

    [Fact]
    public void LanguageOverridePreservesLegacyFallbackAndAllowsEmptySeparator()
    {
        var rule = EtAlRule.CitationDefault();
        rule.MinCount = 5;
        rule.AuthorSeparator = "; ";
        rule.AuthorSeparatorZh = "";
        Assert.Equal("张三李四王五赵六", InTextFormatter.AuthorLabel(Paper, rule));
        Assert.Equal("Smith; Brown", InTextFormatter.AuthorLabel(EnglishPaper, rule));
        rule.MinCount = 4; rule.UseFirst = 2; rule.AuthorSeparatorZh = "，";
        Assert.Equal("张三，李四等", InTextFormatter.AuthorLabel(Paper, rule));
    }

    [Fact]
    public void LanguageSettingsSurviveStyleInheritanceAndSerialization()
    {
        var folder = Directory.CreateTempSubdirectory("acnote-language-separators-");
        try
        {
            File.WriteAllText(Path.Combine(folder.FullName, "base.style"), """{"id":"base","etAl":{"authorSeparator":"; ","authorSeparatorZh":"，"},"citation":{"etAl":{"authorSeparatorZh":"，"}}}""");
            File.WriteAllText(Path.Combine(folder.FullName, "child.style"), """{"id":"child","extends":"base","etAl":{"authorSeparatorEn":", "},"citation":{"etAl":{"authorSeparatorEn":", "}}}""");
            var style = StyleCatalog.Load(folder.FullName).Get("child");
            Assert.Equal("，", style.EtAl.SeparatorFor("zh"));
            Assert.Equal(", ", style.EtAl.SeparatorFor("en"));
            Assert.Equal("，", style.CitationEtAl.SeparatorFor("zh"));
            Assert.Equal(", ", style.CitationEtAl.SeparatorFor("en"));
            var copy = StyleLibrary.Snapshot(style);
            File.WriteAllText(Path.Combine(folder.FullName, "child.style"), copy.ToJsonString());
            Assert.Equal("，", StyleCatalog.Load(folder.FullName).Get("child").CitationEtAl.SeparatorFor("zh"));
        }
        finally { folder.Delete(true); }
    }

    private static ReferenceItem Paper => new()
    {
        Language = "zh", Year = "2024",
        Authors = [AuthorFormatter.Parse("张三"), AuthorFormatter.Parse("李四"), AuthorFormatter.Parse("王五"), AuthorFormatter.Parse("赵六")],
    };

    [Theory]
    [InlineData("，", "张三，李四，王五, 等")]
    [InlineData("; ", "张三; 李四; 王五, 等")]
    [InlineData("", "张三李四王五, 等")]
    public void BibliographySeparatesAuthorsWithoutChangingTruncationSuffix(string separator, string expected)
    {
        var rule = EtAlRule.Default();
        rule.AuthorSeparator = separator;
        Assert.Equal(expected, AuthorFormatter.FormatList(Paper, rule));
    }

    [Fact]
    public void CitationUsesChosenSeparatorAndKeepsLanguageDefaultWhenUnspecified()
    {
        var rule = EtAlRule.CitationDefault();
        rule.MinCount = 5;
        Assert.Equal("张三、李四、王五和赵六", InTextFormatter.AuthorLabel(Paper, rule));
        rule.AuthorSeparator = "，";
        Assert.Equal("张三，李四，王五，赵六", InTextFormatter.AuthorLabel(Paper, rule));
        rule.MinCount = 4;
        rule.UseFirst = 2;
        Assert.Equal("张三，李四等", InTextFormatter.AuthorLabel(Paper, rule));
    }
}
