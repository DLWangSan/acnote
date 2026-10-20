namespace AcNote.Core;

public sealed class Author
{
    public string Family { get; set; } = "";
    public string Given { get; set; } = "";
    public string Literal { get; set; } = "";
}

public sealed class ReferenceItem
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "article-journal";
    public string Title { get; set; } = "";
    public string ContainerTitle { get; set; } = "";
    public string Year { get; set; } = "";
    public string Volume { get; set; } = "";
    public string Issue { get; set; } = "";
    public string Page { get; set; } = "";
    public string Publisher { get; set; } = "";
    public string PublisherPlace { get; set; } = "";
    public string Doi { get; set; } = "";
    public string Url { get; set; } = "";
    public string Language { get; set; } = "";
    public string SourceKey { get; set; } = "";
    public List<Author> Authors { get; set; } = [];
    public Dictionary<string, string> Extra { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public static class ReferenceTypes
{
    public static readonly IReadOnlyList<(string Id, string Label)> All =
    [
        ("article-journal", "期刊论文"),
        ("book", "图书"),
        ("thesis", "学位论文"),
        ("chapter", "图书章节"),
        ("paper-conference", "会议论文"),
        ("preprint", "预印本"),
        ("dataset", "数据集"),
        ("document", "其他"),
    ];

    public static string LabelOf(string? type) =>
        All.FirstOrDefault(item => item.Id == type).Label is { Length: > 0 } label
            ? label
            : "其他";
}
