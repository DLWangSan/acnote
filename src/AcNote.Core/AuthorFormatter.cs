namespace AcNote.Core;

public static class AuthorFormatter
{
    public static List<Author> ParseList(string? text)
    {
        var authors = new List<Author>();
        if (string.IsNullOrWhiteSpace(text))
            return authors;

        var lines = text.Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (LanguageDetector.ContainsCjk(line) && (line.Contains('，') || line.Contains(',')))
            {
                foreach (var piece in line.Split(['，', ','], StringSplitOptions.RemoveEmptyEntries))
                    Add(authors, Parse(piece));
            }
            else
            {
                Add(authors, Parse(line));
            }
        }
        return authors;
    }

    public static string ToEditorText(IEnumerable<Author> authors) =>
        string.Join(Environment.NewLine, authors.Select(ToEditorLine).Where(line => line.Length > 0));

    public static string FormatList(ReferenceItem item, EtAlRule rule)
    {
        var authors = item.Authors.Where(HasName).ToList();
        if (authors.Count == 0)
            return "";

        var truncate = authors.Count >= rule.MinCount && rule.UseFirst > 0 && rule.UseFirst < authors.Count;
        var take = truncate ? rule.UseFirst : authors.Count;
        var text = string.Join(rule.AuthorSeparator ?? ", ", authors.Take(take).Select(author => FormatOne(author, rule)));
        if (!truncate)
            return text;

        var suffix = LanguageDetector.Resolve(item) == "zh"
            ? rule.Zh
            : rule.En;
        return text + suffix;
    }

    public static Author Parse(string raw)
    {
        raw = raw.Trim().Trim(';', '；');
        if (LanguageDetector.ContainsCjk(raw))
        {
            var literal = raw.Replace("，", "", StringComparison.Ordinal).Replace(",", "", StringComparison.Ordinal).Trim();
            return new Author { Family = literal, Literal = literal };
        }

        var comma = raw.IndexOf(',');
        if (comma > 0)
        {
            return new Author
            {
                Family = raw[..comma].Trim(),
                Given = raw[(comma + 1)..].Trim().Trim('.'),
            };
        }

        var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
            return new Author { Family = parts[0] };

        return new Author
        {
            Family = parts[^1],
            Given = string.Join(' ', parts[..^1]),
        };
    }

    public static string FormatOne(Author author) => FormatOne(author, null);

    private static string FormatOne(Author author, EtAlRule? rule)
    {
        var literal = author.Literal.Trim();
        var family = author.Family.Trim();
        var given = author.Given.Trim();

        if (LanguageDetector.ContainsCjk(literal))
            return literal;
        if (LanguageDetector.ContainsCjk(family) || LanguageDetector.ContainsCjk(given))
            return string.IsNullOrEmpty(literal) ? family + given : literal;

        if (string.IsNullOrEmpty(family) && !string.IsNullOrEmpty(literal))
            return FormatOne(Parse(literal));

        var initials = Initials(given, rule);
        return string.IsNullOrEmpty(initials) ? family : $"{family}{rule?.FamilyInitialsSeparator ?? " "}{initials}";
    }

    private static string ToEditorLine(Author author)
    {
        if (LanguageDetector.ContainsCjk(author.Literal) || LanguageDetector.ContainsCjk(author.Family))
            return string.IsNullOrEmpty(author.Literal) ? author.Family + author.Given : author.Literal;
        if (string.IsNullOrEmpty(author.Family))
            return author.Literal;
        if (string.IsNullOrEmpty(author.Given))
            return author.Family;
        return $"{author.Family}, {author.Given}";
    }

    private static string Initials(string given, EtAlRule? rule = null)
    {
        if (string.IsNullOrWhiteSpace(given))
            return "";

        var letters = new List<string>();
        foreach (var part in given.Split([' ', '-', '.'], StringSplitOptions.RemoveEmptyEntries))
        {
            var letter = part.FirstOrDefault(char.IsLetter);
            if (letter != default)
                letters.Add(char.ToUpperInvariant(letter).ToString());
        }
        var separator = rule?.InitialsSeparator ?? " ";
        return string.Join(separator, rule?.InitialsUsePeriods == true ? letters.Select(letter => letter + ".") : letters);
    }

    private static bool HasName(Author author) =>
        !string.IsNullOrWhiteSpace(author.Family)
        || !string.IsNullOrWhiteSpace(author.Given)
        || !string.IsNullOrWhiteSpace(author.Literal);

    private static void Add(List<Author> authors, Author author)
    {
        if (HasName(author))
            authors.Add(author);
    }
}
