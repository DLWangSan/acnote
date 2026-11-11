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
        ("preprint", "container-title") => "预印本平台/来源",
        ("preprint", "archive-id") => "预印本编号",
        ("preprint", "url") => "在线地址",
        ("thesis", "publisher") => "保存单位",
        ("thesis", "publisher-place") => "保存地",
        ("book", "publisher") => "出版社",
        ("book", "publisher-place") => "出版地",
        ("author", _) => "作者",
        (_, "author") => "作者",
        (_, "author-or-editor") => "作者或编者",
        (_, "translator") => "译者",
        (_, "editors") => "原文献编者",
        (_, "country") => "国别",
        (_, "standard-number") => "标准编号",
        (_, "patent-number") => "专利号",
        (_, "report-number") => "报告编号",
        (_, "publication-date") => "发布日期（YYYY-MM-DD）",
        (_, "updated") => "公告/更新日期（YYYY-MM-DD）",
        (_, "accessed") => "引用日期（YYYY-MM-DD）",
        (_, "formal-version-check") => "正式版本核验说明（尚未发表时注明）",
        (_, "title") => "题名",
        (_, "year") => "年份",
        (_, "volume") => "卷号",
        (_, "issue") => "期号",
        (_, "page") => "页码",
        (_, "publisher") => "出版者",
        (_, "publisher-place") => "出版地",
        (_, "url") => "网址",
        (_, "container-title") => "来源",
        _ => field,
    };
}
