namespace AcNote.Core;

public static class InTextFormatter
{
    public static string Format(IEnumerable<ReferenceItem?> items, EtAlRule? rule = null, bool narrative = false)
    {
        rule ??= EtAlRule.CitationDefault();
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

            var text = Piece(item, rule, narrative, chinese);
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

    public static string SortKey(ReferenceItem item) => CiteNames(item, EtAlRule.CitationDefault());

    private static string Piece(ReferenceItem item, EtAlRule rule, bool narrative, bool chinese)
    {
        var names = CiteNames(item, rule);
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

    private static string CiteNames(ReferenceItem item, EtAlRule rule)
    {
        var authors = item.Authors.Where(HasName).Select(CiteName).ToList();
        if (authors.Count == 0)
            return "";

        var chinese = LanguageDetector.Resolve(item) == "zh";
        var truncate = authors.Count >= rule.MinCount && rule.UseFirst > 0 && rule.UseFirst < authors.Count;
        if (!truncate)
            return JoinNames(authors, chinese);

        var suffix = chinese ? rule.Zh ?? "等" : rule.En ?? " et al.";
        return JoinNames(authors.Take(rule.UseFirst).ToList(), chinese) + suffix;
    }

    private static string JoinNames(IReadOnlyList<string> names, bool chinese)
    {
        if (names.Count == 0)
            return "";
        if (names.Count == 1)
            return names[0];
        if (names.Count == 2)
            return names[0] + (chinese ? "和" : " and ") + names[1];

        var head = string.Join(chinese ? "、" : ", ", names.Take(names.Count - 1));
        return chinese ? head + "和" + names[^1] : head + " and " + names[^1];
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
