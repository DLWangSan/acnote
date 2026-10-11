using AcNote.Core;
using Xunit;

namespace AcNote.Core.Tests;

public class BibliographyTests
{
    private static ResolvedStyle Style =>
        StyleCatalog.Load(Path.Combine(AppContext.BaseDirectory, "styles")).Get(StyleCatalog.DefaultId);

    [Fact]
    public void ChinesePaper_UsesDeng_WhenMoreThanThreeAuthors()
    {
        var item = Journal(
            ["张三", "李四", "王五", "赵六"],
            "城市绿地与夏季热环境",
            "地理学报",
            "2020",
            volume: "",
            issue: "4",
            page: "");

        var formatted = BibliographyFormatter.Format(item, Style);

        Assert.Equal("张三, 李四, 王五, 等. 城市绿地与夏季热环境[J]. 地理学报, 2020(4).", formatted.Text);
        Assert.Equal(["卷号", "页码"], formatted.Missing);
        Assert.DoesNotContain("缺", formatted.Text);
    }

    [Fact]
    public void EnglishPaper_UsesEtAl_AndWesternInitials()
    {
        var item = new ReferenceItem
        {
            Type = "article-journal",
            Title = "Street trees and summer heat",
            ContainerTitle = "Sample Journal of Cities",
            Year = "2022",
            Volume = "8",
            Issue = "2",
            Page = "15-28",
            Authors =
            [
                AuthorFormatter.Parse("Smith, John"),
                AuthorFormatter.Parse("Johnson, Alice"),
                AuthorFormatter.Parse("Brown, Robert Paul"),
                AuthorFormatter.Parse("Davis, Mary"),
                AuthorFormatter.Parse("Wilson, James"),
            ],
        };

        var formatted = BibliographyFormatter.Format(item, Style);

        Assert.Equal("Smith J, Johnson A, Brown R P, et al. Street trees and summer heat[J]. Sample Journal of Cities, 2022, 8(2): 15-28.", formatted.Text);
        Assert.Empty(formatted.Missing);
    }

    [Fact]
    public void ThreeAuthors_AreKept()
    {
        var item = Journal(["李明", "周倩", "孙浩"], "滨海湿地碳通量观测", "生态学报", "2019", "39", "6", "1801-1810");
        var formatted = BibliographyFormatter.Format(item, Style);
        Assert.Equal("李明, 周倩, 孙浩. 滨海湿地碳通量观测[J]. 生态学报, 2019, 39(6): 1801-1810.", formatted.Text);
        Assert.DoesNotContain("等", formatted.Text);
    }

    [Fact]
    public void ExplicitEnglishLanguage_WinsOverChineseTitle()
    {
        var item = Journal(["张三", "李四", "王五", "赵六"], "城市绿地与夏季热环境", "地理学报", "2020", "12", "", "1-10");
        item.Language = "en";
        var formatted = BibliographyFormatter.Format(item, Style);
        Assert.Contains(", et al.", formatted.Text);
        Assert.DoesNotContain("等", formatted.Text);
    }

    [Fact]
    public void BookAndThesis_OmitEmptyFields()
    {
        var book = new ReferenceItem
        {
            Type = "book",
            Title = "人间词话",
            PublisherPlace = "上海",
            Publisher = "上海古籍出版社",
            Year = "1998",
            Authors = [AuthorFormatter.Parse("王国维")],
        };
        Assert.Equal("王国维. 人间词话[M]. 上海: 上海古籍出版社, 1998.", BibliographyFormatter.Format(book, Style).Text);

        var thesis = new ReferenceItem
        {
            Type = "thesis",
            Title = "滨水空间使用评价",
            PublisherPlace = "广州",
            Year = "2021",
            Authors = [AuthorFormatter.Parse("陈晓")],
        };
        var formatted = BibliographyFormatter.Format(thesis, Style);
        Assert.Equal("陈晓. 滨水空间使用评价[D]. 广州, 2021.", formatted.Text);
        Assert.Equal(["保存单位"], formatted.Missing);
    }

    [Fact]
    public void ChildStyle_CanOverrideEtAl()
    {
        var directory = Directory.CreateTempSubdirectory("acnote-style-");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "styles", "gb-t-7714-2015-numeric.style"), Path.Combine(directory.FullName, "base.style"));
        File.WriteAllText(Path.Combine(directory.FullName, "school.style"),
            """
            {
              "id": "school",
              "name": "学校示例",
              "extends": "gb-t-7714-2015-numeric",
              "etAl": { "minCount": 3, "useFirst": 1, "zh": "等" }
            }
            """);

        var style = StyleCatalog.Load(directory.FullName).Get("school");
        var item = Journal(["张三", "李四", "王五"], "题名", "学报", "2020", "1", "", "2-3");
        Assert.Equal("张三等. 题名[J]. 学报, 2020, 1: 2-3.", BibliographyFormatter.Format(item, style).Text);
    }

    [Fact]
    public void LaterStyleFile_OverridesSameId()
    {
        var bundled = Directory.CreateTempSubdirectory("acnote-bundled-");
        var user = Directory.CreateTempSubdirectory("acnote-user-");
        File.WriteAllText(Path.Combine(bundled.FullName, "a.style"), """{ "id": "custom", "name": "内置" }""");
        File.WriteAllText(Path.Combine(user.FullName, "a.style"), """{ "id": "custom", "name": "社区覆盖" }""");

        var style = StyleCatalog.Load(bundled.FullName, user.FullName).Get("custom");
        Assert.Equal("社区覆盖", style.Name);
    }

    [Fact]
    public void CitationNumbers_GroupConsecutiveEntries()
    {
        Assert.Equal("[1-3,5]", CitationNumbers.Format([3, 1, 2, 5]));
        Assert.Equal("[1]", CitationNumbers.Format([1]));
    }

    [Fact]
    public void LibraryRoundTrip_PreservesAuthors()
    {
        var path = Path.Combine(Path.GetTempPath(), "acnote-lib-" + Guid.NewGuid().ToString("N") + ".db");
        var store = new LibraryStore(path);
        var item = Journal(["张三", "李四"], "题名", "学报", "2020", "1", "2", "3-4");
        item.Id = "abc";
        store.Save(item);

        var loaded = store.Find("abc");
        Assert.NotNull(loaded);
        Assert.Equal("张三" + Environment.NewLine + "李四", AuthorFormatter.ToEditorText(loaded.Authors));
        Assert.Equal("题名", loaded.Title);
    }

    private static ReferenceItem Journal(
        IEnumerable<string> authors,
        string title,
        string journal,
        string year,
        string volume,
        string issue,
        string page) => new()
    {
        Type = "article-journal",
        Title = title,
        ContainerTitle = journal,
        Year = year,
        Volume = volume,
        Issue = issue,
        Page = page,
        Authors = authors.Select(AuthorFormatter.Parse).ToList(),
    };
}
