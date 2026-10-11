namespace AcNote.Core;

public static class MissingFieldChecker
{
    public static IReadOnlyList<string> Check(ReferenceItem item, ResolvedStyle style)
    {
        if (!style.Required.TryGetValue(item.Type, out var fields) || fields.Count == 0)
            return [];

        var missing = new List<string>();
        foreach (var field in fields)
        {
            if (string.IsNullOrWhiteSpace(BibliographyFormatter.FieldValue(item, field, style)))
                missing.Add(Label(item.Type, field));
        }
        return missing;
    }

    public static string Label(string? type, string field) => (type, field) switch
    {
        ("article-journal", "container-title") => "刊名",
        ("book", "container-title") => "书名",
        ("chapter", "container-title") => "书名",
        ("paper-conference", "container-title") => "会议名称",
        ("thesis", "publisher") => "保存单位",
        ("thesis", "publisher-place") => "保存地",
        ("book", "publisher") => "出版社",
        ("book", "publisher-place") => "出版地",
        ("author", _) => "作者",
        (_, "author") => "作者",
        (_, "title") => "题名",
        (_, "year") => "年份",
        (_, "volume") => "卷号",
        (_, "issue") => "期号",
        (_, "page") => "页码",
        (_, "publisher") => "出版者",
        (_, "publisher-place") => "出版地",
        (_, "container-title") => "来源",
        _ => field,
    };
}
