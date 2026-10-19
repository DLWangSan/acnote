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
    public void RealWorks_CoverTheSupportedTypes()
    {
        var catalog = StyleCatalog.Load(Path.Combine(AppContext.BaseDirectory, "styles"));
        var numeric = catalog.Get("gb-t-7714-2015-numeric");
        var authorYear = catalog.Get("author-year");

        var yuan = Journal(["袁隆平"], "水稻的雄性不孕性", "科学通报", "1966", "17", "4", "185-188");
        Assert.Equal("袁隆平. 水稻的雄性不孕性[J]. 科学通报, 1966, 17(4): 185-188.", BibliographyFormatter.Format(yuan, numeric).Text);
        Assert.Equal("袁隆平. 1966. 水稻的雄性不孕性[J]. 科学通报, 17(4): 185-188.", BibliographyFormatter.Format(yuan, authorYear).Text);
        Assert.Equal("（袁隆平，1966）", InTextFormatter.Format([yuan]));
        Assert.Equal("袁隆平（1966）", InTextFormatter.Format([yuan], narrative: true));

        var watson = new ReferenceItem
        {
            Type = "article-journal",
            Title = "Molecular structure of nucleic acids: a structure for deoxyribose nucleic acid",
            ContainerTitle = "Nature",
            Year = "1953",
            Volume = "171",
            Issue = "4356",
            Page = "737-738",
            Authors = [AuthorFormatter.Parse("Watson, James D"), AuthorFormatter.Parse("Crick, Francis H C")],
        };
        Assert.Equal("(Watson and Crick, 1953)", InTextFormatter.Format([watson]));
        Assert.Equal("Watson and Crick (1953)", InTextFormatter.Format([watson], narrative: true));

        var artemisinin = Journal(
            ["刘静明", "倪慕云", "樊菊芬", "屠呦呦", "吴照华", "吴毓林", "周维善"],
            "青蒿素(Arteannuin)的结构和反应",
            "化学学报",
            "1979",
            "37",
            "2",
            "129-143");
        artemisinin.Language = "zh";
        Assert.StartsWith("刘静明, 倪慕云, 樊菊芬, 等. ", BibliographyFormatter.Format(artemisinin, numeric).Text);
        Assert.Equal("（刘静明等，1979）", InTextFormatter.Format([artemisinin]));
        Assert.Equal("刘静明等（1979）", InTextFormatter.Format([artemisinin], narrative: true));
        Assert.Equal("（袁隆平，1966；刘静明等，1979）", InTextFormatter.Format([yuan, artemisinin]));
        Assert.Equal("袁隆平（1966）；刘静明等（1979）", InTextFormatter.Format([yuan, artemisinin], narrative: true));

        var book = new ReferenceItem
        {
            Type = "book",
            Title = "人间词话",
            PublisherPlace = "北京",
            Publisher = "人民文学出版社",
            Year = "1960",
            Authors = [AuthorFormatter.Parse("王国维")],
        };
        Assert.Equal("王国维. 人间词话[M]. 北京: 人民文学出版社, 1960.", BibliographyFormatter.Format(book, numeric).Text);

        var thesis = new ReferenceItem
        {
            Type = "thesis",
            Title = "Systems of Logic Based on Ordinals",
            PublisherPlace = "Princeton",
            Publisher = "Princeton University",
            Year = "1938",
            Authors = [AuthorFormatter.Parse("Turing, Alan M")],
        };
        Assert.Equal("Turing A M. Systems of Logic Based on Ordinals[D]. Princeton: Princeton University, 1938.", BibliographyFormatter.Format(thesis, numeric).Text);

        var paper = new ReferenceItem
        {
            Type = "paper-conference",
            Title = "Attention is all you need",
            ContainerTitle = "Advances in Neural Information Processing Systems",
            Year = "2017",
            Page = "5998-6008",
            Authors =
            [
                AuthorFormatter.Parse("Vaswani, Ashish"),
                AuthorFormatter.Parse("Shazeer, Noam"),
                AuthorFormatter.Parse("Parmar, Niki"),
                AuthorFormatter.Parse("Uszkoreit, Jakob"),
            ],
        };
        Assert.Contains("[C]. Advances in Neural Information Processing Systems, 2017: 5998-6008.", BibliographyFormatter.Format(paper, numeric).Text);
        Assert.Equal("(Vaswani et al., 2017)", InTextFormatter.Format([paper]));
        Assert.Equal("(Watson and Crick, 1953; Vaswani et al., 2017)", InTextFormatter.Format([watson, paper]));

        var dataset = new ReferenceItem
        {
            Type = "dataset",
            Title = "The MNIST database of handwritten digits",
            Year = "1998",
            Url = "http://yann.lecun.com/exdb/mnist/",
            Authors =
            [
                AuthorFormatter.Parse("LeCun, Yann"),
                AuthorFormatter.Parse("Cortes, Corinna"),
                AuthorFormatter.Parse("Burges, Christopher J C"),
            ],
        };
        Assert.Equal(
            "LeCun Y, Cortes C, Burges C J C. The MNIST database of handwritten digits[DS]. 1998. http://yann.lecun.com/exdb/mnist/",
            BibliographyFormatter.Format(dataset, numeric).Text);
        Assert.Equal("(LeCun et al., 1998)", InTextFormatter.Format([dataset]));
    }

    [Fact]
    public void CitationAuthorList_ComesFromTheStyle()
    {
        var directory = Directory.CreateTempSubdirectory("acnote-cite-");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "styles", "gb-t-7714-2015-numeric.style"), Path.Combine(directory.FullName, "base.style"));
        File.WriteAllText(Path.Combine(directory.FullName, "school.style"),
            """
            {
              "id": "school",
              "name": "学校示例",
              "extends": "gb-t-7714-2015-numeric",
              "citation": { "etAl": { "minCount": 5, "useFirst": 2, "zh": "等", "en": " et al." } }
            }
            """);

        var style = StyleCatalog.Load(directory.FullName).Get("school");
        var item = Journal(["张三", "李四", "王五"], "题名", "学报", "2020", "1", "", "2-3");
        Assert.Equal("（张三、李四和王五，2020）", InTextFormatter.Format([item], style.CitationEtAl));
        Assert.Equal("张三, 李四, 王五. 题名[J]. 学报, 2020, 1: 2-3.", BibliographyFormatter.Format(item, style).Text);
    }

    [Fact]
    public void StyleList_ShowsClassicsThenUniversities()
    {
        var names = StyleCatalog.Load(Path.Combine(AppContext.BaseDirectory, "styles")).All.Select(style => style.Name).ToList();
        Assert.Equal(["GB/T 7714", "MDPI", "Author-Year"], names.Take(3));
        Assert.Equal(["中山大学", "北京大学", "清华大学"], names.Skip(3).OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public void MdpiStyle_FormatsNumericCitationsDoiAndEmphasis()
    {
        var style = StyleCatalog.Load(Path.Combine(AppContext.BaseDirectory, "styles")).Get("mdpi");
        var item = new ReferenceItem
        {
            Id = "mdpi-test",
            Type = "article-journal",
            Title = "A sample article",
            ContainerTitle = "Journal of Tests",
            Year = "2024",
            Volume = "12",
            Page = "1-9",
            Doi = "https://doi.org/10.1000/test",
            Authors = [AuthorFormatter.Parse("Smith, John"), AuthorFormatter.Parse("Doe, Alice B")],
        };

        var formatted = BibliographyFormatter.Format(item, style);

        Assert.Equal("Smith, J.; Doe, A.B. A sample article. Journal of Tests 2024, 12, 1-9. https://doi.org/10.1000/test", formatted.Text);
        Assert.Contains(formatted.Spans, span => span.Italic && formatted.Text.Substring(span.Start, span.Length) == "Journal of Tests");
        Assert.Contains(formatted.Spans, span => span.Bold && formatted.Text.Substring(span.Start, span.Length) == "12");
        Assert.Equal("–", style.CitationRangeSeparator);
        Assert.False(style.CitationFormat.Superscript);
        Assert.Equal("[1–3,5]", CitationNumbers.Format([1, 2, 3, 5], style.CitationRangeSeparator));
    }

    [Fact]
    public void LibraryRoundTrip_PreservesUrl()
    {
        var path = Path.Combine(Path.GetTempPath(), "acnote-lib-" + Guid.NewGuid().ToString("N") + ".acnote");
        var store = new LibraryStore(path);
        var item = new ReferenceItem
        {
            Id = "mnist",
            Type = "dataset",
            Title = "The MNIST database of handwritten digits",
            Year = "1998",
            Url = "http://yann.lecun.com/exdb/mnist/",
            Authors = [AuthorFormatter.Parse("LeCun, Yann")],
        };
        store.Save(item);
        Assert.Equal(item.Url, store.Find("mnist")?.Url);
    }

    [Fact]
    public void CitationNumbers_GroupConsecutiveEntries()
    {
        Assert.Equal("[1-3,5]", CitationNumbers.Format([3, 1, 2, 5]));
        Assert.Equal("[1,2]", CitationNumbers.Format([2, 1]));
        Assert.Equal("[3-5]", CitationNumbers.Format([5, 3, 4]));
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
