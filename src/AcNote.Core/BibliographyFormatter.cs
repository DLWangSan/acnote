using System.Text;
using System.Text.RegularExpressions;

namespace AcNote.Core;

public sealed class FormattedReference
{
    public required string Text { get; init; }
    public required IReadOnlyList<string> Missing { get; init; }
    public IReadOnlyList<FormattedTextSpan> Spans { get; init; } = [];
}

public sealed record FormattedTextSpan(int Start, int Length, bool Italic, bool Bold);

public static class CitationNumbers
{
    public static string Format(IEnumerable<int> numbers, string rangeSeparator = "-")
    {
        var ordered = numbers.Where(number => number > 0).Distinct().OrderBy(number => number).ToList();
        if (ordered.Count == 0)
            return "";

        var parts = new List<string>();
        var index = 0;
        while (index < ordered.Count)
        {
            var start = ordered[index];
            var end = start;
            while (index + 1 < ordered.Count && ordered[index + 1] == end + 1)
            {
                index++;
                end = ordered[index];
            }

            if (end >= start + 2)
                parts.Add($"{start}{rangeSeparator}{end}");
            else if (end == start)
                parts.Add(start.ToString());
            else
            {
                parts.Add(start.ToString());
                parts.Add(end.ToString());
            }
            index++;
        }

        return "[" + string.Join(",", parts) + "]";
    }
}

public static class BibliographyFormatter
{
    public static FormattedReference Format(ReferenceItem item, ResolvedStyle style, string? yearLabel = null)
    {
        var tokens = style.Templates.TryGetValue(item.Type, out var typed)
            ? typed
            : style.Templates.GetValueOrDefault("document") ?? [];

        var builder = new StringBuilder();
        var chinese = LanguageDetector.Resolve(item) == "zh";
        var spans = new List<FormattedTextSpan>();
        foreach (var token in tokens)
        {
            if (!string.IsNullOrEmpty(token.Literal) && string.IsNullOrEmpty(token.Field))
            {
                builder.Append(token.Literal);
                continue;
            }

            if (string.IsNullOrEmpty(token.Field))
                continue;

            var value = token.Field == "year" && yearLabel is not null ? yearLabel : FieldValue(item, token.Field, style);
            if (string.IsNullOrEmpty(value))
                continue;

            if (!chinese && token.TextCaseEn is string casing) value = EnglishCase(value, casing);
            builder.Append((chinese ? token.PrefixZh : token.PrefixEn) ?? token.Prefix);
            var valueStart = builder.Length;
            builder.Append(value);
            builder.Append((chinese ? token.SuffixZh : token.SuffixEn) ?? token.Suffix);
            if (token.Italic == true || token.Bold == true)
                spans.Add(new(valueStart, value.Length, token.Italic == true, token.Bold == true));
        }

        var sourceText = builder.ToString();
        var text = Tidy(sourceText);
        var normalizedSpans = new List<FormattedTextSpan>();
        var searchFrom = 0;
        foreach (var span in spans)
        {
            var value = sourceText.Substring(span.Start, span.Length);
            var actualStart = text.IndexOf(value, searchFrom, StringComparison.Ordinal);
            if (actualStart < 0) continue;
            normalizedSpans.Add(span with { Start = actualStart });
            searchFrom = actualStart + value.Length;
        }

        return new FormattedReference
        {
            Text = text,
            Missing = MissingFieldChecker.Check(item, style),
            Spans = normalizedSpans,
        };
    }

    public static string FieldValue(ReferenceItem item, string field, ResolvedStyle style)
    {
        var value = field switch
        {
            "author" => AuthorFormatter.FormatList(item, style.EtAl),
            "author-or-editor" => item.Authors.Count > 0 ? AuthorFormatter.FormatList(item, style.EtAl) : item.Extra.GetValueOrDefault("editors") ?? "",
            "translator" => item.Extra.GetValueOrDefault("translator") ?? item.Extra.GetValueOrDefault("translators") ?? "",
            "editors" => item.Extra.GetValueOrDefault("editors") ?? item.Extra.GetValueOrDefault("editor") ?? "",
            "standard-number" => item.Extra.GetValueOrDefault("standard-number") ?? item.Extra.GetValueOrDefault("number") ?? "",
            "patent-number" => item.Extra.GetValueOrDefault("patent-number") ?? item.Extra.GetValueOrDefault("number") ?? "",
            "report-number" => item.Extra.GetValueOrDefault("report-number") ?? item.Extra.GetValueOrDefault("number") ?? "",
            "publication-date" => item.Extra.GetValueOrDefault("publication-date") ?? item.Extra.GetValueOrDefault("date") ?? "",
            "updated" => item.Extra.GetValueOrDefault("updated") ?? item.Extra.GetValueOrDefault("publication-date") ?? "",
            "accessed" => item.Extra.GetValueOrDefault("accessed") ?? item.Extra.GetValueOrDefault("access-date") ?? "",
            "title" => item.Title,
            "container-title" => item.ContainerTitle,
            "year" => item.Year,
            "volume" => item.Volume,
            "issue" => item.Issue,
            "page" => item.Page,
            "publisher" => item.Publisher,
            "publisher-place" => item.PublisherPlace,
            "doi" => CleanDoi(item.Doi),
            "doi-url" => string.IsNullOrWhiteSpace(item.Doi) ? "" : "https://doi.org/" + CleanDoi(item.Doi),
            "archive-id" => ArchiveIdentifier(item),
            "url" => item.Url,
            _ => item.Extra.GetValueOrDefault(field) ?? "",
        };
        return value?.Trim() ?? "";
    }

    private static string CleanDoi(string value) => Regex.Replace(value.Trim(), @"^(?:https?://(?:dx\.)?doi\.org/|doi:\s*)", "", RegexOptions.IgnoreCase);

    private static string EnglishCase(string text, string casing)
    {
        if (casing is not ("sentence" or "title")) return text;
        var minor = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "a", "an", "the", "and", "or", "but", "for", "nor", "as", "at", "by", "in", "of", "on", "per", "to", "via", "with" };
        var first = true;
        return Regex.Replace(text, @"[A-Za-z]+(?:['’][A-Za-z]+)?", match =>
        {
            var word = match.Value;
            // Preserve supplied acronyms and internal capitals in technical names.
            var acronym = word.Length > 1 && word.All(char.IsUpper);
            var internalCapital = word.Skip(1).Any(char.IsUpper);
            var lower = word.ToLowerInvariant();
            var result = acronym || internalCapital ? word :
                casing == "title" && !first && minor.Contains(lower) ? lower :
                first || casing == "title" ? char.ToUpperInvariant(lower[0]) + lower[1..] : lower;
            first = false;
            return result;
        });
    }

    private static string ArchiveIdentifier(ReferenceItem item)
    {
        var identifier = item.Extra.GetValueOrDefault("archive-id")?.Trim() ?? "";
        var archive = item.ContainerTitle.Trim();
        if (identifier.Length == 0 || archive.Length == 0 || identifier.Contains(':'))
            return identifier;
        return archive.Equals("arXiv", StringComparison.OrdinalIgnoreCase)
            ? $"arXiv:{identifier}"
            : $"{archive}:{identifier}";
    }

    public static string Tidy(string text)
    {
        var current = text.Replace("\r", " ").Replace("\n", " ");
        for (var pass = 0; pass < 6; pass++)
        {
            var next = current;
            next = Regex.Replace(next, @"[ \t]{2,}", " ");
            next = Regex.Replace(next, @"\(\s*\)", "");
            next = Regex.Replace(next, @":\s*,", ",");
            next = Regex.Replace(next, @"\.\s*,", ".");
            next = Regex.Replace(next, @",\s*,", ",");
            next = Regex.Replace(next, @",\s*\.", ".");
            next = Regex.Replace(next, @"\.\s*\.", ".");
            next = Regex.Replace(next, @"\s+([,.:;)\]])", "$1");
            next = Regex.Replace(next, @"([(\[])\s+", "$1");
            next = Regex.Replace(next, @"\s{2,}", " ");
            next = next.Trim();
            if (next == current)
                break;
            current = next;
        }

        return current;
    }
}
