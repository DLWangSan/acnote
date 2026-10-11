using AcNote.Core;

namespace AcNote.App;

public static class SampleLibrary
{
    public static void Seed(LibraryStore store)
    {
        store.Save(Journal(
            "sample-zh-missing",
            ["张三", "李四", "王五", "赵六"],
            "城市绿地与夏季热环境（示例）",
            "地理学报",
            "2020",
            "",
            "4",
            ""));
        store.Save(Journal(
            "sample-zh-complete",
            ["李明", "周倩", "孙浩"],
            "滨海湿地碳通量观测（示例）",
            "生态学报",
            "2019",
            "39",
            "6",
            "1801-1810"));
        store.Save(new ReferenceItem
        {
            Id = "sample-en",
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
                AuthorFormatter.Parse("Brown, Robert"),
                AuthorFormatter.Parse("Davis, Mary"),
                AuthorFormatter.Parse("Wilson, James"),
            ],
        });
        store.Save(new ReferenceItem
        {
            Id = "sample-book",
            Type = "book",
            Title = "人间词话",
            PublisherPlace = "上海",
            Publisher = "上海古籍出版社",
            Year = "1998",
            Authors = [AuthorFormatter.Parse("王国维")],
        });
        store.Save(new ReferenceItem
        {
            Id = "sample-thesis",
            Type = "thesis",
            Title = "滨水空间使用评价（示例）",
            PublisherPlace = "广州",
            Year = "2021",
            Authors = [AuthorFormatter.Parse("陈晓")],
        });
    }

    private static ReferenceItem Journal(
        string id,
        IEnumerable<string> authors,
        string title,
        string journal,
        string year,
        string volume,
        string issue,
        string page) => new()
    {
        Id = id,
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
