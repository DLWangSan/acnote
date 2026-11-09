using AcNote.Core;
using Xunit;

namespace AcNote.Core.Tests;

public class AuthorSeparatorTests
{
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
