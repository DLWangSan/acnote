using AcNote.Core;
using AcNote.Word;
using Xunit;

namespace AcNote.Core.Tests;

public class WustStyleTests
{
    private static ResolvedStyle Style => StyleCatalog.Load(Path.Combine(AppContext.BaseDirectory, "styles")).Get("wust-doctoral-science");
    private static ReferenceItem Item(string type, string title, string authors, string year = "") => new()
    { Type = type, Title = title, Authors = AuthorFormatter.ParseList(authors), Year = year, Language = "zh" };
    private static void Output(string expected, ReferenceItem item) => Assert.Equal(expected, BibliographyFormatter.Format(item, Style).Text);

    [Fact]
    public void ChineseAndEnglishJournalExamplesMatchTemplatePunctuation()
    {
        var zh = Item("article-journal", "支持向量机算法优化铝碳材料工艺参数", "金胜利\n李亚伟\n秦洪奎\n其他作者", "2006");
        zh.ContainerTitle = "武汉科技大学学报：自然科学版"; zh.Volume = "29"; zh.Issue = "1"; zh.Page = "10-13";
        Output("金胜利，李亚伟，秦洪奎，等. 支持向量机算法优化铝碳材料工艺参数[J].武汉科技大学学报：自然科学版，2006，29（1）：10-13.", zh);
        var en = Item("article-journal", "A novel compensation technology of static synchronous compensator integrated with distribution transformer", "Wang, C P\nYin, X G\nZhang, Z\nSmith, John", "2013");
        en.Language = "en"; en.ContainerTitle = "IEEE transactions on power delivery"; en.Volume = "28"; en.Issue = "2"; en.Page = "1032-1039";
        Output("Wang C P, Yin X G, Zhang Z, et al. A novel compensation technology of static synchronous compensator integrated with distribution transformer[J]. IEEE Transactions on Power Delivery. 2013, 28(2): 1032-1039.", en);
        Assert.Empty(MissingFieldChecker.Check(zh, Style));
        Assert.Empty(MissingFieldChecker.Check(en, Style));
    }

    [Fact]
    public void BooksTranslationsAndProceedingsMatchTemplate()
    {
        var book = Item("book", "疲劳强度设计", "徐灏", "1980");
        book.PublisherPlace = "北京"; book.Publisher = "机械工业出版社"; book.Page = "15-21";
        Output("徐灏. 疲劳强度设计[M]. 北京：机械工业出版社，1980：15-21.", book);
        var translated = Item("translated-book", "电力系统电磁暂态计算理论", "Dommel, H W", "1986");
        translated.Extra["translator"] = "李永庄"; translated.PublisherPlace = "北京"; translated.Publisher = "水利电力出版社"; translated.Page = "36-38";
        Output("Dommel H W. 电力系统电磁暂态计算理论[M]. 李永庄，译. 北京：水利电力出版社，1986：36-38.", translated);
        var proceedings = Item("proceedings", "信息技术与信息服务国际研讨会论文集：A集", "辛希孟", "1994");
        proceedings.PublisherPlace = "北京"; proceedings.Publisher = "中国社会科学出版社"; proceedings.Page = "65-79";
        Output("辛希孟. 信息技术与信息服务国际研讨会论文集：A集[C]. 北京：中国社会科学出版社，1994：65-79.", proceedings);
    }

    [Fact]
    public void ExtractedConferencePaperAndThesisMatchTemplate()
    {
        var paper = Item("paper-conference", "非线性规划在可燃毒物配置中的应用", "钟文发", "1996");
        paper.Extra["editors"] = "赵玮";
        paper.ContainerTitle = "运筹学的理论与应用：中国运筹学会第五届大会论文集";
        paper.PublisherPlace = "西安"; paper.Publisher = "西安电子科技大学出版社"; paper.Page = "468-471";
        Output("钟文发. 非线性规划在可燃毒物配置中的应用[C]∥赵玮. 运筹学的理论与应用：中国运筹学会第五届大会论文集. 西安：西安电子科技大学出版社，1996：468-471.", paper);
        var thesis = Item("thesis", "冷轧机液压AGC系统的控制模型与仿真研究", "陈亚琛", "2004");
        thesis.PublisherPlace = "武汉"; thesis.Publisher = "武汉科技大学";
        Output("陈亚琛. 冷轧机液压AGC系统的控制模型与仿真研究[D]. 武汉：武汉科技大学，2004.", thesis);
    }

    [Fact]
    public void PatentsStandardsAndReportsMatchTemplate()
    {
        var patent = Item("patent", "建筑砌块联接件", "王杏林");
        patent.Extra = new() { ["country"] = "中国", ["patent-number"] = "1036800", ["publication-date"] = "1997-09-27" };
        Output("王杏林. 建筑砌块联接件：中国，1036800[P]. 1997-09-27.", patent);
        var standard = Item("standard", "汉语拼音正词法规则", ""); standard.Extra["standard-number"] = "GB/T 16159—1996";
        Output("GB/T 16159—1996，汉语拼音正词法规则[S].", standard);
        var report = Item("report", "核反应堆压力管道与压力容器的LBB分析", "冯西桥", "1997");
        report.PublisherPlace = "北京"; report.Publisher = "清华大学核能技术设计研究院";
        Output("冯西桥. 核反应堆压力管道与压力容器的LBB分析[R]. 北京：清华大学核能技术设计研究院，1997.", report);
        Assert.Empty(MissingFieldChecker.Check(patent, Style));
    }

    [Fact]
    public void OnlineExampleUsesFullwidthDateParenthesesButValidUrlProtocol()
    {
        var web = Item("webpage", "中国学术期刊标准化数据库系统工程的进展", "王明亮");
        web.Extra = new() { ["updated"] = "1998-08-16", ["accessed"] = "1998-10-04" };
        web.Url = "http://www.cajcd.edu.cn/pub/wnl.txt/980810-2thml";
        Output("王明亮. 中国学术期刊标准化数据库系统工程的进展[EB/OL].（1998-08-16）[1998-10-04]. http://www.cajcd.edu.cn/pub/wnl.txt/980810-2thml.", web);
        Assert.Empty(MissingFieldChecker.Check(web, Style));
    }

    [Fact]
    public void HeadingAndParagraphSettingsMatchExplicitInstructions()
    {
        var style = Style;
        Assert.Equal("参考文献", style.BibliographyHeading);
        Assert.Equal("黑体", style.BibliographyHeadingFormat.EastAsiaFontFamily);
        Assert.Equal(18, style.BibliographyHeadingFormat.FontSize);
        Assert.True(style.BibliographyHeadingFormat.Bold);
        Assert.True(style.BibliographyHeadingFormat.PageBreakBefore);
        Assert.Equal(1, style.BibliographyHeadingFormat.SpaceAfterLines);
        Assert.Equal("宋体", style.BibliographyFormat.EastAsiaFontFamily);
        Assert.Equal("Times New Roman", style.BibliographyFormat.FontFamily);
        Assert.Equal(12, style.BibliographyFormat.FontSize);
        Assert.Equal(1.25, style.BibliographyFormat.LineSpacing);
    }

    [Fact]
    public void ManagedHeadingDoesNotShiftReferenceJumpToWrongParagraph()
    {
        var doc = new TestDocument();
        doc.ContentControls.Add(CitationDocument.BuildTag("p", ["one"]), 0);
        var bib = doc.ContentControls.Add(CitationDocument.BibliographyTag, 100);
        var item = Item("book", "疲劳强度设计", "徐灏", "1980"); item.Id = "one";
        item.PublisherPlace = "北京"; item.Publisher = "机械工业出版社";
        CitationDocument.Refresh(doc, Style, _ => item);
        Assert.StartsWith("参考文献\r[1]\t徐灏", bib.Range.Text);
        Assert.Equal(bib.Range.Start + "参考文献\r".Length, doc.Bookmarks[1].Range.Start);
        CitationDocument.Refresh(doc, Style, _ => item);
        Assert.Equal(1, doc.Bookmarks.Count);
        Assert.Equal(1, doc.Hyperlinks.Count);
    }

    [Fact]
    public void EditorCopiesPreserveLocalizedPunctuationAndEmphasis()
    {
        var original = new StyleToken { Field = "issue", Prefix = "(", PrefixZh = "（", SuffixZh = "）", PrefixEn = "(", TextCaseEn = "title", Bold = true };
        var copy = Assert.Single(TemplateEditing.Copy([original]));
        Assert.Equal("（", copy.PrefixZh); Assert.Equal("）", copy.SuffixZh); Assert.True(copy.Bold);
        Assert.Equal("title", copy.TextCaseEn);
    }
}
