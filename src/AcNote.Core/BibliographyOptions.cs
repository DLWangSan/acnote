using System.Globalization;

namespace AcNote.Core;

public sealed class BibliographyOptions
{
    public string? LanguageOrder { get; set; }
    public bool? NumberAuthorYear { get; set; }

    public void Merge(BibliographyOptions other)
    {
        LanguageOrder = other.LanguageOrder ?? LanguageOrder;
        NumberAuthorYear = other.NumberAuthorYear ?? NumberAuthorYear;
    }

    public void Validate()
    {
        if (LanguageOrder is not (null or "chinese-first" or "western-first" or "mixed"))
            throw new InvalidOperationException("文献排序须为中文优先、西文优先或不分文种。");
    }

    public static BibliographyOptions Resolve(BibliographyOptions style, BibliographyOptions document)
    {
        var result = new BibliographyOptions { LanguageOrder = "western-first", NumberAuthorYear = false };
        result.Merge(style); result.Merge(document); result.Validate();
        return result;
    }

    public IEnumerable<ReferenceItem> Order(IEnumerable<ReferenceItem> items)
    {
        Validate();
        // Explicit collation avoids depending on the Windows display language.
        var comparer = StringComparer.Create(CultureInfo.GetCultureInfo("zh-CN"), true);
        return items.OrderBy(LanguageRank)
            .ThenBy(AuthorKey, comparer)
            .ThenBy(item => item.Year, StringComparer.Ordinal)
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id, StringComparer.Ordinal);
    }

    private int LanguageRank(ReferenceItem item)
    {
        if (LanguageOrder == "mixed") return 0;
        var language = (item.Language ?? "").Trim().ToLowerInvariant().Split('-', '_')[0];
        var rank = language switch
        {
            "ja" or "japanese" or "日文" => 1,
            "ru" or "russian" or "俄文" => 3,
            "en" or "fr" or "de" or "es" or "pt" or "it" or "nl" or "sv" or "la" or "english" or "英文" => 2,
            "zh" or "cn" or "chinese" or "中文" => 0,
            "" or "auto" => LanguageDetector.Resolve(item) == "zh" ? 0 : 2,
            _ => 4
        };
        return (LanguageOrder ?? "western-first") == "western-first" ? rank switch { 2 => 0, 0 => 1, 1 => 2, _ => rank } : rank;
    }

    private static string AuthorKey(ReferenceItem item)
    {
        var key = string.Join(";", item.Authors.Select(a => string.IsNullOrWhiteSpace(a.Family)
            ? a.Literal.Trim() : a.Family.Trim() + " " + a.Given.Trim()));
        return string.IsNullOrWhiteSpace(key) ? item.Title : key;
    }
}
