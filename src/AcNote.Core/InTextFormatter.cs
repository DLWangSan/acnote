namespace AcNote.Core;

public static class InTextFormatter
{
    public static string Format(IEnumerable<ReferenceItem?> items, bool narrative = false)
    {
        var list = items.ToList();
        if (list.Count == 0)
            return "";

        var chinese = list.Any(item => item is not null && LanguageDetector.Resolve(item) == "zh");
        var parts = new List<string>();
        foreach (var item in list)
        {
            if (item is null)
            {
                parts.Add(chinese ? "未找到" : "missing");
                continue;
            }

            var text = Piece(item, narrative, chinese);
            if (text.Length > 0)
                parts.Add(text);
        }

        if (parts.Count == 0)
            return "";
        if (!narrative)
        {
            var body = string.Join(chinese ? "；" : "; ", parts);
            return chinese ? "（" + body + "）" : "(" + body + ")";
        }

        return string.Join(chinese ? "；" : "; ", parts);
    }

    public static string SortKey(ReferenceItem item) => CiteNames(item);

    private static string Piece(ReferenceItem item, bool narrative, bool chinese)
    {
        var names = CiteNames(item);
        var year = item.Year.Trim();
        if (narrative)
        {
            if (names.Length == 0)
                return YearOnly(year, chinese);
            if (year.Length == 0)
                return names;
            return chinese ? $"{names}（{year}）" : $"{names} ({year})";
        }

        if (names.Length == 0)
            return year;
        if (year.Length == 0)
            return names;
        return chinese ? $"{names}，{year}" : $"{names}, {year}";
    }

    private static string YearOnly(string year, bool chinese) =>
        year.Length == 0 ? "" : chinese ? $"（{year}）" : $"({year})";

    private static string CiteNames(ReferenceItem item)
    {
        var authors = item.Authors.Where(HasName).ToList();
        if (authors.Count == 0)
            return "";

        var chinese = LanguageDetector.Resolve(item) == "zh";
        if (authors.Count >= 3)
            return CiteName(authors[0]) + (chinese ? "等" : " et al.");
        if (authors.Count == 1)
            return CiteName(authors[0]);

        var left = CiteName(authors[0]);
        var right = CiteName(authors[1]);
        return left + (chinese ? "和" : " and ") + right;
    }

    private static string CiteName(Author author)
    {
        var literal = author.Literal.Trim();
        var family = author.Family.Trim();
        var given = author.Given.Trim();
        if (LanguageDetector.ContainsCjk(literal) || LanguageDetector.ContainsCjk(family) || LanguageDetector.ContainsCjk(given))
            return LanguageDetector.ContainsCjk(literal) ? literal : family + given;
        if (!string.IsNullOrEmpty(family))
            return family;
        return literal;
    }

    private static bool HasName(Author author) =>
        !string.IsNullOrWhiteSpace(author.Family)
        || !string.IsNullOrWhiteSpace(author.Given)
        || !string.IsNullOrWhiteSpace(author.Literal);
}
